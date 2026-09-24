using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Config;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Authentication;

/// <summary>
/// Tests for <see cref="BasicAuthenticationHandler"/>, the selector
/// <see cref="PacmanAuthenticationServiceCollectionExtensions.AddPacmanAuthentication"/> registers, and
/// <see cref="RejectInvalidBasicCredentialsExtensions.UseRejectInvalidBasicCredentials"/>.
/// </summary>
/// <remarks>
/// Requests run through a pipeline assembled the way <c>Program.cs</c> assembles it — authentication,
/// the rejecting middleware, authorization — over the real <see cref="UserService"/> and an in-memory
/// database, so that a failure is whatever the service really decides. The pipeline ends in two
/// test-only endpoints: one <c>[AllowAnonymous]</c>, standing in for the pacman routes that do not
/// exist yet, and one requiring an authenticated caller. Both echo the principal they were handed.
/// </remarks>
[TestFixture]
public class BasicAuthenticationHandlerTests
{
    private const string AnonymousPath = "/anonymous";
    private const string ProtectedPath = "/protected";
    private const string ClientAddress = "198.51.100.23";
    private const string ExpectedChallenge = "Basic realm=\"pacman\"";

    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private InMemoryDatabaseRoot _databaseRoot = null!;
    private CapturingLoggerProvider _logs = null!;
    private List<string> _jwtSaw = null!;
    private ServiceProvider _services = null!;
    private RequestDelegate _pipeline = null!;

    private User _owner = null!;
    private GivenAccessToken _token = null!;
    private GivenAccessToken _othersToken = null!;
    private GivenAccessToken _expiredToken = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseRoot = new InMemoryDatabaseRoot();
        _logs = new CapturingLoggerProvider();
        _jwtSaw = [];

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Trace).AddProvider(_logs));
        services.AddDbContext<PacmanManagerDbContext>(o => o.UseInMemoryDatabase(nameof(BasicAuthenticationHandlerTests), _databaseRoot));
        services.Configure<AccessTokenConfig>(_ => { });
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IClaimsTransformation, ClaimsTransformer>();
        // Routing asks for the listener the web host would otherwise supply.
        services.AddSingleton(new System.Diagnostics.DiagnosticListener(nameof(BasicAuthenticationHandlerTests)));
        services.AddRouting();
        services.AddAuthorization();
        services.AddPacmanAuthentication();
        // Records every request the Bearer handler is asked about, and what header it was given.
        services.Configure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, o =>
            o.Events = new JwtBearerEvents
            {
                OnMessageReceived = ctx =>
                {
                    _jwtSaw.Add(ctx.Request.Headers.Authorization.ToString());
                    return Task.CompletedTask;
                },
            });
        _services = services.BuildServiceProvider();

        var app = new ApplicationBuilder(_services);
        app.UseRouting();
        app.UseAuthentication();
        app.UseRejectInvalidBasicCredentials();
        app.UseAuthorization();
        app.UseEndpoints(endpoints =>
        {
            endpoints.MapGet(AnonymousPath, EchoPrincipal).AllowAnonymous();
            endpoints.MapGet(ProtectedPath, EchoPrincipal).RequireAuthorization();
        });
        _pipeline = app.Build();

        using var seed = NewContext();
        _owner = seed.Add(new User { DisplayName = "owner", NormalizedDisplayName = "owner", Email = "owner@test.com" }).Entity;
        var other = seed.Add(new User { DisplayName = "other", NormalizedDisplayName = "other", Email = "other@test.com" }).Entity;
        seed.SaveChanges();

        _token = GivenToken(_owner, "laptop");
        _othersToken = GivenToken(other, "desktop");
        _expiredToken = GivenToken(_owner, "old", expiresAt: Now.AddDays(-1));
    }

    [TearDown]
    public async Task TearDown()
    {
        await _services.DisposeAsync();
        _logs.Dispose();
    }

    #region Success

    [TestCase(AnonymousPath)]
    [TestCase(ProtectedPath)]
    public async Task ValidHeader_AuthenticatesAsTheOwner_WithTheReadScopeAndNothingElse(string path)
    {
        var response = await SendAsync(path, Basic(_token.Username, _token.Password));

        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(response.Body, Is.EqualTo(
                $"{AuthnConstants.AppUserIdClaimType}={_owner.Id};{AuthnConstants.ScopeClaimType}=pacman-manager:*:read"));
        });
    }

    [Test]
    public async Task ValidHeader_SchemeInAnyCase_Authenticates()
    {
        var response = await SendAsync(ProtectedPath, "bAsIc " + Encode($"{_token.Username}:{_token.Password}"));

        Assert.That(response.Status, Is.EqualTo(StatusCodes.Status200OK));
    }

    [Test]
    public async Task ValidHeader_ProvisionsNoUserAndNoExternalProviderUserMapping()
    {
        int users, mappings;
        await using (var before = NewContext())
        {
            users = await before.Users.CountAsync();
            mappings = await before.UserMappings.CountAsync();
        }

        var response = await SendAsync(ProtectedPath, Basic(_token.Username, _token.Password));

        await using var after = NewContext();
        Assert.Multiple(() =>
        {
            Assert.That(response.Status, Is.EqualTo(StatusCodes.Status200OK));
            Assert.That(after.Users.Count(), Is.EqualTo(users), "users");
            Assert.That(after.UserMappings.Count(), Is.EqualTo(mappings), "external provider user mappings");
        });
    }

    #endregion

    #region Failure

    /// <summary>
    /// Every way a presented Basic credential can be wrong. Each is a function of the fixture's tokens
    /// so that the cases can name them before <see cref="SetUp"/> has made them.
    /// </summary>
    private static IEnumerable<TestCaseData> InvalidCredentials()
    {
        TestCaseData Case(string name, Func<BasicAuthenticationHandlerTests, string> header) =>
            new TestCaseData(header).SetArgDisplayNames(name);

        yield return Case("not Base64", _ => "Basic !!!not-base64!!!");
        yield return Case("no parameter", _ => "Basic ");
        yield return Case("no colon", t => "Basic " + Encode(t._token.Username + t._token.Password));
        yield return Case("not UTF-8", _ => "Basic " + Convert.ToBase64String([0xFF, 0xFE, (byte)':', 0xFD]));
        yield return Case("wrong secret", t => Basic(t._token.Username, AccessTokenFormat.GenerateSecret()));
        yield return Case("unknown token id", t => Basic(AccessTokenFormat.FormatUsername(Guid.CreateVersion7()), t._token.Password));
        yield return Case("expired token", t => Basic(t._expiredToken.Username, t._expiredToken.Password));
        yield return Case("empty password", t => Basic(t._token.Username, ""));
        yield return Case("fields swapped", t => Basic(t._token.Password, t._token.Username));
        yield return Case("right secret, empty username", t => Basic("", t._token.Password));
        yield return Case("right secret, owner's display name", t => Basic(t._owner.DisplayName, t._token.Password));
        yield return Case("right secret, arbitrary username", t => Basic("x-access-token", t._token.Password));
        yield return Case("right secret, another token's identifier", t => Basic(t._othersToken.Username, t._token.Password));
    }

    [TestCaseSource(nameof(InvalidCredentials))]
    public async Task InvalidCredential_OnAProtectedRoute_IsTheSameBare401(Func<BasicAuthenticationHandlerTests, string> header)
    {
        var response = await SendAsync(ProtectedPath, header(this));

        Assert.That(response, Is.EqualTo(new Snapshot(StatusCodes.Status401Unauthorized, ExpectedChallenge, "")));
    }

    /// <summary>
    /// The case the rejecting middleware exists for: on an <c>[AllowAnonymous]</c> route ASP.NET Core
    /// would otherwise discard the failure and answer as if no credential had been offered.
    /// </summary>
    [TestCaseSource(nameof(InvalidCredentials))]
    public async Task InvalidCredential_OnAnAllowAnonymousRoute_IsStillTheSameBare401(Func<BasicAuthenticationHandlerTests, string> header)
    {
        var response = await SendAsync(AnonymousPath, header(this));

        Assert.That(response, Is.EqualTo(new Snapshot(StatusCodes.Status401Unauthorized, ExpectedChallenge, "")));
    }

    [TestCaseSource(nameof(InvalidCredentials))]
    public async Task InvalidCredential_TheHandlerFailsWithTheSameMessage(Func<BasicAuthenticationHandlerTests, string> header)
    {
        var (result, _) = await AuthenticateWithBasicAsync(header(this));

        Assert.Multiple(() =>
        {
            Assert.That(result.Succeeded, Is.False);
            Assert.That(result.Failure?.Message, Is.EqualTo("Invalid credentials."));
        });
    }

    /// <summary>
    /// Asked directly, the Basic handler has nothing to say about a request without a Basic
    /// credential; the selector never asks it. Its challenge is still the same bare <c>401</c>.
    /// </summary>
    [TestCase(null)]
    [TestCase("Bearer abc.def.ghi")]
    [TestCase("Digest username=\"x\"")]
    [TestCase("Basic")]
    public async Task MissingOrNonBasicHeader_TheHandlerHasNoResult_AndChallengesWithTheSame401(string? header)
    {
        var (result, challenge) = await AuthenticateWithBasicAsync(header);

        Assert.Multiple(() =>
        {
            Assert.That(result.None, Is.True, "no result");
            Assert.That(challenge, Is.EqualTo(new Snapshot(StatusCodes.Status401Unauthorized, ExpectedChallenge, "")));
        });
    }

    [Test]
    public async Task EveryInvalidCredential_ProducesAnIdenticalResponse()
    {
        var responses = new HashSet<Snapshot>();
        foreach (var data in InvalidCredentials())
        {
            var header = (Func<BasicAuthenticationHandlerTests, string>)data.Arguments[0]!;
            responses.Add(await SendAsync(ProtectedPath, header(this)));
            responses.Add(await SendAsync(AnonymousPath, header(this)));
        }

        Assert.That(responses, Has.Count.EqualTo(1));
    }

    #endregion

    #region Selection

    [TestCase("Basic abc", ExpectedResult = AuthnConstants.BasicScheme)]
    [TestCase("basic abc", ExpectedResult = AuthnConstants.BasicScheme)]
    [TestCase("BASIC abc", ExpectedResult = AuthnConstants.BasicScheme)]
    [TestCase("Bearer abc", ExpectedResult = JwtBearerDefaults.AuthenticationScheme)]
    [TestCase(null, ExpectedResult = JwtBearerDefaults.AuthenticationScheme)]
    [TestCase("", ExpectedResult = JwtBearerDefaults.AuthenticationScheme)]
    [TestCase("Digest username=\"x\"", ExpectedResult = JwtBearerDefaults.AuthenticationScheme)]
    [TestCase("Basicabc", ExpectedResult = JwtBearerDefaults.AuthenticationScheme)]
    [TestCase(" Basic abc", ExpectedResult = JwtBearerDefaults.AuthenticationScheme)]
    public string SelectScheme_RoutesByPrefix(string? header)
    {
        var context = new DefaultHttpContext();
        if (header is not null)
        {
            context.Request.Headers.Authorization = header;
        }

        return PacmanAuthenticationServiceCollectionExtensions.SelectScheme(context);
    }

    [Test]
    public async Task BearerHeader_IsHandledByTheJwtScheme_Untouched()
    {
        const string header = "Bearer not.a.jwt";

        var response = await SendAsync(ProtectedPath, header);

        Assert.Multiple(() =>
        {
            Assert.That(_jwtSaw, Is.EqualTo(new[] { header }), "the Bearer handler saw the header as sent");
            Assert.That(response.Status, Is.EqualTo(StatusCodes.Status401Unauthorized));
            Assert.That(response.WwwAuthenticate, Does.StartWith("Bearer"), "challenged by Bearer, not Basic");
            Assert.That(_logs.Logs, Has.None.Matches<CapturedLog>(l => l.Category == typeof(UserService).FullName),
                "no token verification was attempted");
        });
    }

    [Test]
    public async Task AbsentHeader_FallsThroughToAnonymous()
    {
        var anonymous = await SendAsync(AnonymousPath, null);
        var @protected = await SendAsync(ProtectedPath, null);

        Assert.Multiple(() =>
        {
            Assert.That(_jwtSaw, Has.Count.EqualTo(2), "the Bearer handler was asked, as before");
            Assert.That(anonymous, Is.EqualTo(new Snapshot(StatusCodes.Status200OK, "", "")), "anonymous route");
            Assert.That(@protected.Status, Is.EqualTo(StatusCodes.Status401Unauthorized), "protected route");
            Assert.That(@protected.WwwAuthenticate, Is.EqualTo("Bearer"), "protected route challenges as before");
        });
    }

    [Test]
    public async Task UnrecognisedScheme_IsTreatedAsBearer_NotAsBasic()
    {
        const string header = "Digest username=\"pmt_x\"";

        var anonymous = await SendAsync(AnonymousPath, header);
        var @protected = await SendAsync(ProtectedPath, header);

        Assert.Multiple(() =>
        {
            Assert.That(_jwtSaw, Is.EqualTo(new[] { header, header }), "the Bearer handler was asked");
            Assert.That(anonymous, Is.EqualTo(new Snapshot(StatusCodes.Status200OK, "", "")), "anonymous route");
            Assert.That(@protected.WwwAuthenticate, Is.EqualTo("Bearer"), "protected route");
        });
    }

    [Test]
    public async Task BasicHeader_IsNotHandledByTheJwtScheme()
    {
        await SendAsync(ProtectedPath, Basic(_token.Username, _token.Password));

        Assert.That(_jwtSaw, Is.Empty);
    }

    #endregion

    #region Logging

    [Test]
    public async Task Verification_LogLinesCarryTheClientAddress()
    {
        await SendAsync(ProtectedPath, Basic(_token.Username, _token.Password));
        await SendAsync(ProtectedPath, Basic(_token.Username, AccessTokenFormat.GenerateSecret()));
        await SendAsync(ProtectedPath, "Basic !!!");

        var verification = _logs.Logs
            .Where(l => l.Category == typeof(UserService).FullName
                        || l.Category == typeof(BasicAuthenticationHandler).FullName)
            .Where(l => l.Message.Contains("verif", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Multiple(() =>
        {
            Assert.That(verification.Select(l => l.Message), Has.Some.Contains("verified"), "success");
            Assert.That(verification.Select(l => l.Message), Has.Some.Contains("NoMatch"), "wrong secret");
            Assert.That(verification.Select(l => l.Message), Has.Some.Contains("Undecodable"), "undecodable header");
            Assert.That(verification, Has.All.Matches<CapturedLog>(l =>
                    l.Scope.TryGetValue(BasicAuthenticationHandler.ClientAddressScopeKey, out var address)
                    && Equals(address, ClientAddress)),
                "every verification line carries the client address");
        });
    }

    [Test]
    public async Task NoLogLineContainsTheSecretOrTheHeader()
    {
        var header = Basic(_token.Username, _token.Password);
        await SendAsync(ProtectedPath, header);
        await SendAsync(ProtectedPath, Basic(_token.Password, _token.Username));

        var body = _token.Password[AccessTokenFormat.SecretPrefix.Length..];
        var logged = _logs.Logs.Select(l => l.Message + " " + string.Join(" ", l.Scope.Values)).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(logged, Has.None.Contains(body[..8]), "part of the secret");
            Assert.That(logged, Has.None.Contains(header["Basic ".Length..][..8]), "part of the header");
        });
    }

    #endregion

    #region Helpers

    private record GivenAccessToken(Guid Id, string Username, string Password);

    /// <summary>
    /// What a client can observe of a response.
    /// </summary>
    private record Snapshot(int Status, string WwwAuthenticate, string Body);

    private static string Encode(string value) => Convert.ToBase64String(Encoding.UTF8.GetBytes(value));

    private static string Basic(string username, string password) => "Basic " + Encode($"{username}:{password}");

    private static Task EchoPrincipal(HttpContext context)
    {
        var claims = context.User.Claims
            .Where(c => c.Type is AuthnConstants.AppUserIdClaimType or AuthnConstants.ScopeClaimType)
            .Select(c => $"{c.Type}={c.Value}");
        return context.Response.WriteAsync(string.Join(";", claims));
    }

    private DefaultHttpContext NewHttpContext(IServiceProvider services, string path, string? header)
    {
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = path;
        context.Connection.RemoteIpAddress = IPAddress.Parse(ClientAddress);
        context.Response.Body = new MemoryStream();
        if (header is not null)
        {
            context.Request.Headers.Authorization = header;
        }

        return context;
    }

    private static async Task<Snapshot> SnapshotAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body);
        return new Snapshot(
            context.Response.StatusCode,
            context.Response.Headers.WWWAuthenticate.ToString(),
            await reader.ReadToEndAsync());
    }

    private async Task<Snapshot> SendAsync(string path, string? header)
    {
        await using var scope = _services.CreateAsyncScope();
        var context = NewHttpContext(scope.ServiceProvider, path, header);
        await _pipeline(context);
        return await SnapshotAsync(context);
    }

    /// <summary>
    /// Asks the Basic scheme itself, bypassing the selector, to authenticate a request and then to
    /// challenge it.
    /// </summary>
    private async Task<(AuthenticateResult Result, Snapshot Challenge)> AuthenticateWithBasicAsync(string? header)
    {
        await using var scope = _services.CreateAsyncScope();
        var context = NewHttpContext(scope.ServiceProvider, ProtectedPath, header);
        var result = await context.AuthenticateAsync(AuthnConstants.BasicScheme);
        await context.ChallengeAsync(AuthnConstants.BasicScheme);
        return (result, await SnapshotAsync(context));
    }

    private GivenAccessToken GivenToken(User owner, string name, DateTimeOffset? expiresAt = null)
    {
        var password = AccessTokenFormat.GenerateSecret();
        using var seed = NewContext();
        var token = new PacmanAccessToken
        {
            UserId = owner.Id,
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            TokenHash = AccessTokenFormat.HashSecret(password),
            CreatedAt = Now.AddDays(-7),
            ExpiresAt = expiresAt,
        };
        seed.PacmanAccessTokens.Add(token);
        seed.SaveChanges();
        return new GivenAccessToken(token.Id, AccessTokenFormat.FormatUsername(token.Id), password);
    }

    private PacmanManagerDbContext NewContext() =>
        new(new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(nameof(BasicAuthenticationHandlerTests), _databaseRoot)
            .Options);

    private class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    /// <summary>
    /// A log line, with every key/value pair from the logging scopes open when it was written.
    /// </summary>
    private record CapturedLog(string Category, LogLevel Level, string Message, IReadOnlyDictionary<string, object?> Scope);

    /// <summary>
    /// Captures every log line with the scopes around it. It shares the logger factory's scope
    /// provider, so a scope opened through one logger is seen by lines another logger writes, as it is
    /// under Serilog's <c>FromLogContext</c>.
    /// </summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
    {
        private IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

        public ConcurrentQueue<CapturedLog> Logs { get; } = new();

        public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this, categoryName);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger(CapturingLoggerProvider provider, string category) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
                provider._scopes.Push(state);

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                var scope = new Dictionary<string, object?>();
                provider._scopes.ForEachScope((s, values) =>
                {
                    if (s is IEnumerable<KeyValuePair<string, object?>> pairs)
                    {
                        foreach (var (key, value) in pairs)
                        {
                            values[key] = value;
                        }
                    }
                }, scope);
                provider.Logs.Enqueue(new CapturedLog(category, logLevel, formatter(state, exception), scope));
            }
        }
    }

    #endregion
}
