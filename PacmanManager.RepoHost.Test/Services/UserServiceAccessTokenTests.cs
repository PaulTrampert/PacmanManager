using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Config;
using PacmanManager.RepoHost.Services;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Tests for <see cref="UserService.GetUserByAccessTokenAsync"/>.
/// </summary>
/// <remarks>
/// Every query the service runs is recorded by <see cref="QueryRecorder"/>, as the LINQ expression EF
/// is about to compile, so that a test can assert how many queries ran and what they were predicated
/// on. EF compiles a query once and caches it in the context's internal service provider, which would
/// hide a query another test had already compiled, so every context gets a service provider of its
/// own and the contexts share their data through an explicit <see cref="InMemoryDatabaseRoot"/>.
/// </remarks>
[TestFixture]
public class UserServiceAccessTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private InMemoryDatabaseRoot _databaseRoot = null!;
    private QueryRecorder _queries = null!;
    private FailingSaveChanges _saveChanges = null!;
    private PacmanManagerDbContext _dbContext = null!;
    private TestOutputLogger<UserService> _logger = null!;
    private AccessTokenConfig _config = null!;
    private UserService _service = null!;

    private User _owner = null!;
    private User _other = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseRoot = new InMemoryDatabaseRoot();
        _queries = new QueryRecorder();
        _saveChanges = new FailingSaveChanges();
        _dbContext = NewContext(_queries, _saveChanges);
        _logger = new TestOutputLogger<UserService>();
        _config = new AccessTokenConfig();
        _service = new UserService(_dbContext, Options.Create(_config), new FixedTimeProvider(Now), _logger);

        // Seeded through a context of their own, so that the service's context starts empty and
        // every save it records is one the service made.
        using var seed = NewContext();
        _owner = seed.Add(new User { DisplayName = "owner", Email = "owner@test.com" }).Entity;
        _other = seed.Add(new User { DisplayName = "other", Email = "other@test.com" }).Entity;
        seed.SaveChanges();
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    #region Success

    [Test]
    public async Task ValidToken_ReturnsOwner()
    {
        var token = GivenToken(_owner);

        var result = await Verify(token.Username, token.Password);

        Assert.That(result?.Id, Is.EqualTo(_owner.Id));
    }

    [Test]
    public async Task ValidToken_SecretContainingUnderscore_ReturnsOwner()
    {
        var bytes = Enumerable.Repeat((byte)0xFF, AccessTokenFormat.SecretByteLength).ToArray();
        var token = GivenToken(_owner, password: AccessTokenFormat.FormatSecret(bytes));
        Assert.That(token.Password[AccessTokenFormat.SecretPrefix.Length..], Does.Contain("_"));

        var result = await Verify(token.Username, token.Password);

        Assert.That(result?.Id, Is.EqualTo(_owner.Id));
    }

    [Test]
    public async Task ValidToken_WithFutureExpiry_ReturnsOwner()
    {
        var token = GivenToken(_owner, expiresAt: Now.AddMinutes(1));

        var result = await Verify(token.Username, token.Password);

        Assert.That(result?.Id, Is.EqualTo(_owner.Id));
    }

    [Test]
    public async Task ValidToken_OfOneUser_ReturnsThatUserNotAnother()
    {
        GivenToken(_owner);
        var othersToken = GivenToken(_other);

        var result = await Verify(othersToken.Username, othersToken.Password);

        Assert.That(result?.Id, Is.EqualTo(_other.Id));
    }

    #endregion

    #region Rejection

    [TestCase(0)]
    [TestCase(20)]
    [TestCase(42)]
    public async Task SecretDifferingInOneCharacter_ReturnsNull(int position)
    {
        var token = GivenToken(_owner);
        var chars = token.Password.ToCharArray();
        var index = AccessTokenFormat.SecretPrefix.Length + position;
        chars[index] = chars[index] == 'A' ? 'Q' : 'A';

        var result = await Verify(token.Username, new string(chars));

        Assert.That(result, Is.Null);
    }

    private static IEnumerable<TestCaseData> MalformedCredentials()
    {
        var id = Guid.CreateVersion7();
        var username = AccessTokenFormat.FormatUsername(id);
        var password = AccessTokenFormat.GenerateSecret();
        var hex = id.ToString("N");
        var body = password[AccessTokenFormat.SecretPrefix.Length..];

        yield return new TestCaseData(null, password).SetName("Malformed_NullUsername");
        yield return new TestCaseData("", password).SetName("Malformed_EmptyUsername");
        yield return new TestCaseData(hex, password).SetName("Malformed_UsernameWithoutPrefix");
        yield return new TestCaseData("pmx_" + hex, password).SetName("Malformed_UsernameWrongPrefix");
        yield return new TestCaseData(username[..^1], password).SetName("Malformed_UsernameTruncated");
        yield return new TestCaseData(username + "0", password).SetName("Malformed_UsernameTooLong");
        yield return new TestCaseData("pmt_" + hex[..^1] + "z", password).SetName("Malformed_UsernameNotHex");
        yield return new TestCaseData("owner", password).SetName("Malformed_UsernameIsADisplayName");
        yield return new TestCaseData(username, null).SetName("Malformed_NullPassword");
        yield return new TestCaseData(username, "").SetName("Malformed_EmptyPassword");
        yield return new TestCaseData(username, body).SetName("Malformed_PasswordWithoutPrefix");
        yield return new TestCaseData(username, "pmx_" + body).SetName("Malformed_PasswordWrongPrefix");
        yield return new TestCaseData(username, password[..^1]).SetName("Malformed_PasswordTruncated");
        yield return new TestCaseData(username, password + "=").SetName("Malformed_PasswordPadded");
        yield return new TestCaseData(username, "pms_" + body.Replace('-', '+').Replace('_', '/')[..^1] + "+")
            .SetName("Malformed_PasswordIsBase64");
        yield return new TestCaseData(password, username).SetName("Malformed_FieldsSwapped");
        yield return new TestCaseData("pms_" + hex, "pmt_" + body).SetName("Malformed_PrefixesSwapped");
        yield return new TestCaseData(username + ":" + password, "").SetName("Malformed_WholeCredentialInUsername");
    }

    [TestCaseSource(nameof(MalformedCredentials))]
    public async Task MalformedCredential_ReturnsNullWithoutQuerying(string? username, string? password)
    {
        User? result = null;

        Assert.That(async () => result = await Verify(username!, password!), Throws.Nothing);
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            Assert.That(_queries.Queries, Is.Empty, "A malformed credential must fail before any database access.");
            Assert.That(Messages(), Has.Some.Contains("Malformed"));
        });
    }

    [Test]
    public async Task UnknownTokenId_ReturnsNull()
    {
        GivenToken(_owner);
        var password = AccessTokenFormat.GenerateSecret();

        var result = await Verify(AccessTokenFormat.FormatUsername(Guid.CreateVersion7()), password);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task RealIdWithAnotherTokensSecret_ReturnsNull()
    {
        var token = GivenToken(_owner);
        var another = GivenToken(_owner, name: "another");

        var result = await Verify(token.Username, another.Password);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task RealIdWithAnotherUsersSecret_ReturnsNull()
    {
        var token = GivenToken(_owner);
        var othersToken = GivenToken(_other);

        var result = await Verify(token.Username, othersToken.Password);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ExpiredToken_ReturnsNull()
    {
        var token = GivenToken(_owner, expiresAt: Now.AddSeconds(-1));

        var result = await Verify(token.Username, token.Password);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task TokenExpiringNow_ReturnsNull()
    {
        var token = GivenToken(_owner, expiresAt: Now);

        var result = await Verify(token.Username, token.Password);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ExpiredToken_DoesNotRecordUse()
    {
        var token = GivenToken(_owner, expiresAt: Now.AddDays(-1));

        await Verify(token.Username, token.Password);

        Assert.That(StoredToken(token.Id).LastUsedAt, Is.Null);
    }

    #endregion

    #region The lookup is one query on both halves

    [Test]
    public async Task ValidToken_IsOneQueryPredicatedOnIdAndHash()
    {
        var token = GivenToken(_owner);

        await Verify(token.Username, token.Password);

        AssertOneQueryOnIdAndHash();
    }

    [Test]
    public async Task UnknownTokenId_IsOneQueryPredicatedOnIdAndHash()
    {
        var password = AccessTokenFormat.GenerateSecret();

        await Verify(AccessTokenFormat.FormatUsername(Guid.CreateVersion7()), password);

        AssertOneQueryOnIdAndHash();
    }

    [Test]
    public async Task WrongSecret_IsOneQueryPredicatedOnIdAndHash()
    {
        var token = GivenToken(_owner);

        await Verify(token.Username, AccessTokenFormat.GenerateSecret());

        AssertOneQueryOnIdAndHash();
    }

    /// <summary>
    /// Asserts that exactly one query ran, and that a single <c>Where</c> in it compares both
    /// <see cref="PacmanAccessToken.Id"/> and <see cref="PacmanAccessToken.TokenHash"/>. Fetching by
    /// id and comparing the hash in memory leaves <c>TokenHash</c> out of every predicate, and fails.
    /// </summary>
    private void AssertOneQueryOnIdAndHash()
    {
        Assert.That(_queries.Queries, Has.Count.EqualTo(1), "Verification must be exactly one query.");

        var predicates = PredicateMembers.On<PacmanAccessToken>(_queries.Queries.Single());

        Assert.That(predicates, Has.Some.Matches<ISet<string>>(members =>
                members.Contains(nameof(PacmanAccessToken.Id))
                && members.Contains(nameof(PacmanAccessToken.TokenHash))),
            "The query must be predicated on both the token id and the hash, in the same Where.");
    }

    #endregion

    #region Storage

    [Test]
    public void StoredRow_DoesNotHoldTheSecret()
    {
        var token = GivenToken(_owner);
        var body = token.Password[AccessTokenFormat.SecretPrefix.Length..];

        var row = StoredToken(token.Id);
        var stored = typeof(PacmanAccessToken).GetProperties()
            .Where(p => p.PropertyType == typeof(string))
            .Select(p => (string?)p.GetValue(row))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(row.TokenHash, Is.Not.EqualTo(token.Password));
            Assert.That(row.TokenHash, Is.Not.EqualTo(body));
            Assert.That(stored, Has.None.Contains(body));
            // The hash is SHA-256 of the secret: one-way, so the row does not give the secret back.
            Assert.That(row.TokenHash, Is.EqualTo(AccessTokenFormat.HashSecret(token.Password)));
        });
    }

    #endregion

    #region LastUsedAt

    [Test]
    public async Task NeverUsedToken_RecordsUse()
    {
        var token = GivenToken(_owner);

        await Verify(token.Username, token.Password);

        Assert.That(StoredToken(token.Id).LastUsedAt, Is.EqualTo(Now));
    }

    [Test]
    public async Task StaleLastUsedAt_AtDefaultResolution_IsWritten()
    {
        var token = GivenToken(_owner, lastUsedAt: Now - TimeSpan.FromHours(1));

        await Verify(token.Username, token.Password);

        Assert.That(StoredToken(token.Id).LastUsedAt, Is.EqualTo(Now));
    }

    [Test]
    public async Task FreshLastUsedAt_AtDefaultResolution_IsNotWritten()
    {
        var lastUsedAt = Now - TimeSpan.FromMinutes(59);
        var token = GivenToken(_owner, lastUsedAt: lastUsedAt);

        await Verify(token.Username, token.Password);

        Assert.Multiple(() =>
        {
            Assert.That(StoredToken(token.Id).LastUsedAt, Is.EqualTo(lastUsedAt));
            Assert.That(_saveChanges.Saves, Is.Zero, "A fresh LastUsedAt must not be written at all.");
        });
    }

    [Test]
    public async Task StaleLastUsedAt_AtConfiguredResolution_IsWritten()
    {
        _config.LastUsedAtResolution = TimeSpan.FromMinutes(5);
        var token = GivenToken(_owner, lastUsedAt: Now - TimeSpan.FromMinutes(10));

        await Verify(token.Username, token.Password);

        Assert.That(StoredToken(token.Id).LastUsedAt, Is.EqualTo(Now));
    }

    [Test]
    public async Task FreshLastUsedAt_AtConfiguredResolution_IsNotWritten()
    {
        _config.LastUsedAtResolution = TimeSpan.FromDays(1);
        var lastUsedAt = Now - TimeSpan.FromHours(12);
        var token = GivenToken(_owner, lastUsedAt: lastUsedAt);

        await Verify(token.Username, token.Password);

        Assert.That(StoredToken(token.Id).LastUsedAt, Is.EqualTo(lastUsedAt));
    }

    [Test]
    public async Task FailureToRecordUse_DoesNotFailVerification()
    {
        var token = GivenToken(_owner);
        _saveChanges.Fail = true;

        var result = await Verify(token.Username, token.Password);

        Assert.Multiple(() =>
        {
            Assert.That(result?.Id, Is.EqualTo(_owner.Id));
            Assert.That(_logger.LogEvents, Has.Some.Matches<LogEvent>(e =>
                e.LogLevel == LogLevel.Warning && e.Exception is DbUpdateException));
        });
    }

    #endregion

    #region The use log

    [Test]
    public async Task Success_LogsTheTokenId()
    {
        var token = GivenToken(_owner);

        await Verify(token.Username, token.Password);

        Assert.That(Messages(), Has.Some.Matches<string>(m => m.Contains(token.Id.ToString()) && m.Contains("verified")));
    }

    [Test]
    public async Task NoMatch_LogsTheTokenIdAndReason()
    {
        var token = GivenToken(_owner);

        await Verify(token.Username, AccessTokenFormat.GenerateSecret());

        Assert.That(Messages(), Has.Some.Matches<string>(m => m.Contains(token.Id.ToString()) && m.Contains("NoMatch")));
    }

    [Test]
    public async Task UnknownId_LogsTheTokenIdAndNoMatch()
    {
        var id = Guid.CreateVersion7();

        await Verify(AccessTokenFormat.FormatUsername(id), AccessTokenFormat.GenerateSecret());

        Assert.That(Messages(), Has.Some.Matches<string>(m => m.Contains(id.ToString()) && m.Contains("NoMatch")));
    }

    [Test]
    public async Task Expired_LogsTheTokenIdAndReason()
    {
        var token = GivenToken(_owner, expiresAt: Now.AddDays(-1));

        await Verify(token.Username, token.Password);

        Assert.That(Messages(), Has.Some.Matches<string>(m => m.Contains(token.Id.ToString()) && m.Contains("Expired")));
    }

    [Test]
    public async Task MalformedPassword_WithWellFormedUsername_LogsTheTokenIdAndReason()
    {
        var id = Guid.CreateVersion7();

        await Verify(AccessTokenFormat.FormatUsername(id), "pms_nope");

        Assert.That(Messages(), Has.Some.Matches<string>(m => m.Contains(id.ToString()) && m.Contains("Malformed")));
    }

    /// <summary>
    /// Runs every outcome — success, fresh and stale use, a failed use write, no match, expiry, and
    /// each malformed shape, including a secret presented in the username field — and asserts that
    /// no captured log line, nor any exception attached to one, contains the secret.
    /// </summary>
    [Test]
    public async Task NoLogLine_ContainsTheSecret()
    {
        var valid = GivenToken(_owner);
        var fresh = GivenToken(_owner, name: "fresh", lastUsedAt: Now.AddMinutes(-1));
        var expired = GivenToken(_owner, name: "expired", expiresAt: Now.AddDays(-1));
        var failingWrite = GivenToken(_owner, name: "failing");
        var secrets = new[] { valid, fresh, expired, failingWrite }.Select(t => t.Password).ToList();

        await Verify(valid.Username, valid.Password);
        await Verify(fresh.Username, fresh.Password);
        await Verify(expired.Username, expired.Password);
        await Verify(valid.Username, fresh.Password);
        await Verify(AccessTokenFormat.FormatUsername(Guid.CreateVersion7()), valid.Password);
        await Verify(valid.Password, valid.Username);
        await Verify(valid.Password, valid.Password);
        await Verify(valid.Username, valid.Password + "=");
        await Verify(valid.Username, valid.Password[..^1]);
        await Verify(valid.Username + ":" + valid.Password, "");
        _saveChanges.Fail = true;
        await Verify(failingWrite.Username, failingWrite.Password);

        var logged = _logger.LogEvents
            .SelectMany(e => new[] { e.Message, e.Exception?.ToString() ?? "" })
            .ToList();
        Assert.That(logged, Is.Not.Empty);
        foreach (var secret in secrets)
        {
            var body = secret[AccessTokenFormat.SecretPrefix.Length..];
            Assert.That(logged, Has.None.Contains(body), "A log line contains a secret.");
            // A prefix of the secret is as much a leak as the whole of it.
            Assert.That(logged, Has.None.Contains(body[..8]), "A log line contains part of a secret.");
        }
    }

    #endregion

    #region Helpers

    private record GivenAccessToken(Guid Id, string Username, string Password);

    /// <summary>
    /// Stores a token for <paramref name="owner"/> the way minting will: a CSPRNG secret, of which
    /// only the hash is kept.
    /// </summary>
    private GivenAccessToken GivenToken(
        User owner,
        string name = "laptop",
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? lastUsedAt = null,
        string? password = null)
    {
        password ??= AccessTokenFormat.GenerateSecret();
        using var seed = NewContext();
        var token = new PacmanAccessToken
        {
            UserId = owner.Id,
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            TokenHash = AccessTokenFormat.HashSecret(password),
            ExpiresAt = expiresAt,
            LastUsedAt = lastUsedAt,
        };
        seed.PacmanAccessTokens.Add(token);
        seed.SaveChanges();
        return new GivenAccessToken(token.Id, AccessTokenFormat.FormatUsername(token.Id), password);
    }

    /// <summary>
    /// Reads a token back through a context of its own, so the assertion sees what was saved rather
    /// than what the service's context is tracking.
    /// </summary>
    private PacmanAccessToken StoredToken(Guid id)
    {
        using var read = NewContext();
        return read.PacmanAccessTokens.AsNoTracking().Single(t => t.Id == id);
    }

    private Task<User?> Verify(string username, string password) =>
        _service.GetUserByAccessTokenAsync(username, password);

    private IEnumerable<string> Messages() => _logger.LogEvents.Select(e => e.Message);

    private PacmanManagerDbContext NewContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(nameof(UserServiceAccessTokenTests), _databaseRoot)
            .EnableServiceProviderCaching(false)
            .AddInterceptors(interceptors)
            .Options;
        return new PacmanManagerDbContext(options);
    }

    /// <summary>
    /// Records every query expression EF compiles.
    /// </summary>
    private class QueryRecorder : IQueryExpressionInterceptor
    {
        public List<Expression> Queries { get; } = [];

        public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
        {
            Queries.Add(queryExpression);
            return queryExpression;
        }
    }

    /// <summary>
    /// Counts saves, and fails them on demand the way a lost connection would.
    /// </summary>
    private class FailingSaveChanges : SaveChangesInterceptor
    {
        public bool Fail { get; set; }

        public int Saves { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            Saves++;
            if (Fail)
            {
                throw new DbUpdateException("Simulated failure to write.");
            }

            return ValueTask.FromResult(result);
        }
    }

    /// <summary>
    /// Collects, for each <c>Where</c> over <typeparamref name="T"/>, the names of the members of
    /// <typeparamref name="T"/> its predicate reads.
    /// </summary>
    private class PredicateMembers : ExpressionVisitor
    {
        private readonly Type _entity;
        private readonly List<ISet<string>> _predicates = [];

        private PredicateMembers(Type entity) => _entity = entity;

        public static IReadOnlyList<ISet<string>> On<T>(Expression query)
        {
            var visitor = new PredicateMembers(typeof(T));
            visitor.Visit(query);
            return visitor._predicates;
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (node.Method.Name == nameof(Queryable.Where)
                && node.Arguments.Count == 2
                && StripQuotes(node.Arguments[1]) is LambdaExpression lambda
                && lambda.Parameters.Single().Type == _entity)
            {
                var members = new MemberNames(lambda.Parameters.Single());
                members.Visit(lambda.Body);
                _predicates.Add(members.Names);
            }

            return base.VisitMethodCall(node);
        }

        private static Expression StripQuotes(Expression e) =>
            e is UnaryExpression { NodeType: ExpressionType.Quote } quote ? quote.Operand : e;

        private class MemberNames(ParameterExpression parameter) : ExpressionVisitor
        {
            public HashSet<string> Names { get; } = [];

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Expression == parameter)
                {
                    Names.Add(node.Member.Name);
                }

                return base.VisitMember(node);
            }
        }
    }

    private class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    #endregion
}
