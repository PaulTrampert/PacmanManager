using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Config;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Tests for <see cref="AccessTokenService"/> against the in-memory provider. Name collisions are
/// decided by the unique index, which the in-memory provider does not enforce, so the index itself is
/// exercised by <see cref="AccessTokenServiceCollisionTests"/> against Postgres; here the mapping of
/// the database's refusal is exercised by simulating it.
/// </summary>
[TestFixture]
public class AccessTokenServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    private string _databaseName = null!;
    private SimulatedSaveFailure _saveFailure = null!;
    private PacmanManagerDbContext _dbContext = null!;
    private TestActorAccessor _actors = null!;
    private AccessTokenService _service = null!;

    private User _owner = null!;
    private User _other = null!;

    [SetUp]
    public void SetUp()
    {
        _databaseName = Guid.NewGuid().ToString();
        _saveFailure = new SimulatedSaveFailure();
        _dbContext = NewContext(_saveFailure);

        using (var seed = NewContext())
        {
            _owner = seed.Add(new User { DisplayName = "owner", NormalizedDisplayName = "owner", Email = "owner@test.com" }).Entity;
            _other = seed.Add(new User { DisplayName = "other", NormalizedDisplayName = "other", Email = "other@test.com" }).Entity;
            seed.SaveChanges();
        }

        _actors = new TestActorAccessor { Actor = Actor.For(_owner, ActorScope.Unrestricted) };
        _service = new AccessTokenService(
            _dbContext,
            _actors,
            new AccessTokenAccessPolicy(),
            new FixedTimeProvider(Now),
            new TestOutputLogger<AccessTokenService>());
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Dispose();
    }

    #region Minting

    [Test]
    public async Task Create_ReturnsTheTokenWithASecret()
    {
        var created = await _service.CreateAccessTokenAsync(Request("laptop"));

        Assert.Multiple(() =>
        {
            Assert.That(created.Name, Is.EqualTo("laptop"));
            Assert.That(created.Username, Is.EqualTo(AccessTokenFormat.FormatUsername(created.Id)));
            Assert.That(created.Secret, Does.StartWith(AccessTokenFormat.SecretPrefix));
            Assert.That(created.Secret, Has.Length.EqualTo(AccessTokenFormat.SecretLength));
            Assert.That(created.CreatedAt, Is.EqualTo(Now));
            Assert.That(created.LastUsedAt, Is.Null);
        });
    }

    [Test]
    public async Task Create_StoresOnlyTheHashOfTheSecret_ForTheActorsUser()
    {
        var created = await _service.CreateAccessTokenAsync(Request("laptop"));

        var stored = StoredToken(created.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.UserId, Is.EqualTo(_owner.Id));
            Assert.That(stored.TokenHash, Is.EqualTo(AccessTokenFormat.HashSecret(created.Secret)));
            Assert.That(stored.TokenHash, Does.Not.Contain(created.Secret[AccessTokenFormat.SecretPrefix.Length..]));
        });
    }

    [Test]
    public async Task Create_MintsADifferentSecretEachTime()
    {
        var first = await _service.CreateAccessTokenAsync(Request("first"));
        var second = await _service.CreateAccessTokenAsync(Request("second"));

        Assert.That(second.Secret, Is.Not.EqualTo(first.Secret));
    }

    [Test]
    public async Task Create_MintedTokenVerifies()
    {
        var created = await _service.CreateAccessTokenAsync(Request("laptop"));

        using var context = NewContext();
        var users = new UserService(
            context,
            Options.Create(new AccessTokenConfig()),
            new FixedTimeProvider(Now),
            new TestOutputLogger<UserService>());
        var user = await users.GetUserByAccessTokenAsync(created.Username, created.Secret);

        Assert.That(user?.Id, Is.EqualTo(_owner.Id));
    }

    [Test]
    public async Task Create_KeepsTheNameAsEntered_AndNormalizesItWithTheInvariantCulture()
    {
        var created = await _service.CreateAccessTokenAsync(Request("My Laptop"));

        var stored = StoredToken(created.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Name, Is.EqualTo("My Laptop"));
            Assert.That(stored.NormalizedName, Is.EqualTo("my laptop"));
        });
    }

    [Test]
    [SetCulture("tr-TR")]
    public async Task Create_NormalizesWithTheInvariantCulture_UnderATurkishCurrentCulture()
    {
        // Under tr-TR the current culture lowers "I" to the dotless "ı"; the invariant culture to "i".
        Assert.That("TITLE".ToLower(), Is.EqualTo("tıtle"), "precondition: the current culture is Turkish");

        var created = await _service.CreateAccessTokenAsync(Request("TITLE"));

        Assert.That(StoredToken(created.Id).NormalizedName, Is.EqualTo("title"));
    }

    [Test]
    public async Task Create_StoresTheExpiry()
    {
        var expiresAt = Now.AddDays(30);

        var created = await _service.CreateAccessTokenAsync(Request("laptop", expiresAt));

        Assert.Multiple(() =>
        {
            Assert.That(created.ExpiresAt, Is.EqualTo(expiresAt));
            Assert.That(StoredToken(created.Id).ExpiresAt, Is.EqualTo(expiresAt));
        });
    }

    [Test]
    public async Task Create_WithoutAnExpiry_StoresATokenThatNeverExpires()
    {
        var created = await _service.CreateAccessTokenAsync(Request("laptop"));

        Assert.That(StoredToken(created.Id).ExpiresAt, Is.Null);
    }

    [Test]
    public async Task Create_OnANameCollision_ThrowsItemExists()
    {
        _saveFailure.Failure = () => UniqueViolation(NameIndex());

        var e = await Assert.ThrowsAsync<ItemExistsException>(() => _service.CreateAccessTokenAsync(Request("laptop")));

        Assert.That(e!.InnerException, Is.InstanceOf<DbUpdateException>());
    }

    [Test]
    public async Task Create_OnAnyOtherFailureToSave_RethrowsIt()
    {
        _saveFailure.Failure = () => UniqueViolation("some_other_index");

        await Assert.ThrowsAsync<DbUpdateException>(() => _service.CreateAccessTokenAsync(Request("laptop")));
    }

    #endregion

    #region The secret is shown once

    [Test]
    public void TheListingModel_HasNoSecret()
    {
        Assert.That(typeof(AccessToken).GetProperty(nameof(CreatedAccessToken.Secret)), Is.Null);
    }

    [Test]
    public async Task List_NeverReturnsTheSecret()
    {
        var created = await _service.CreateAccessTokenAsync(Request("laptop"));

        var page = await _service.GetAccessTokensAsync(new PaginationParams());

        var json = JsonSerializer.Serialize(page);
        Assert.Multiple(() =>
        {
            Assert.That(page.Results.Single().Id, Is.EqualTo(created.Id));
            Assert.That(page.Results.Single().Username, Is.EqualTo(created.Username));
            Assert.That(json, Does.Not.Contain(created.Secret));
            Assert.That(json, Does.Not.Contain(created.Secret[AccessTokenFormat.SecretPrefix.Length..]));
        });
    }

    #endregion

    #region Listing

    [Test]
    public async Task List_ReturnsOnlyTheActorsOwnTokens()
    {
        var mine = GivenToken(_owner, "mine");
        GivenToken(_other, "theirs");

        var page = await _service.GetAccessTokensAsync(new PaginationParams());

        Assert.Multiple(() =>
        {
            Assert.That(page.Results.Select(t => t.Id), Is.EqualTo(new[] { mine.Id }));
            Assert.That(page.Total, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task List_FilterByName_IsCaseInsensitive()
    {
        var laptop = GivenToken(_owner, "My Laptop");
        GivenToken(_owner, "desktop");

        var page = await _service.GetAccessTokensAsync(
            new PaginationParams(),
            new AccessTokenFilter { NameContains = "LAPTOP" });

        Assert.That(page.Results.Select(t => t.Id), Is.EqualTo(new[] { laptop.Id }));
    }

    [Test]
    public async Task List_FilterCanNotReachAnotherUsersTokens()
    {
        GivenToken(_other, "laptop");

        var page = await _service.GetAccessTokensAsync(
            new PaginationParams(),
            new AccessTokenFilter { NameContains = "laptop" });

        Assert.That(page.Results, Is.Empty);
    }

    [Test]
    public async Task List_DefaultsToNewestFirst()
    {
        var older = GivenToken(_owner, "older", createdAt: Now.AddDays(-2));
        var newer = GivenToken(_owner, "newer", createdAt: Now.AddDays(-1));

        var page = await _service.GetAccessTokensAsync(new PaginationParams());

        Assert.That(page.Results.Select(t => t.Id), Is.EqualTo(new[] { newer.Id, older.Id }));
    }

    [Test]
    public async Task List_SortedByName_RunsAToZIgnoringCase()
    {
        var b = GivenToken(_owner, "b");
        var a = GivenToken(_owner, "A");
        var c = GivenToken(_owner, "C");

        var page = await _service.GetAccessTokensAsync(
            new PaginationParams(),
            sort: new SortOptions<AccessTokenSortField> { SortBy = AccessTokenSortField.Name });

        Assert.That(page.Results.Select(t => t.Id), Is.EqualTo(new[] { a.Id, b.Id, c.Id }));
    }

    [Test]
    public async Task List_SortedByExpiry_Ascending_PutsTheSoonestFirst()
    {
        var later = GivenToken(_owner, "later", expiresAt: Now.AddDays(10));
        var sooner = GivenToken(_owner, "sooner", expiresAt: Now.AddDays(1));

        var page = await _service.GetAccessTokensAsync(
            new PaginationParams(),
            sort: new SortOptions<AccessTokenSortField>
            {
                SortBy = AccessTokenSortField.ExpiresAt,
                Direction = SortDirection.Ascending
            });

        Assert.That(page.Results.Select(t => t.Id), Is.EqualTo(new[] { sooner.Id, later.Id }));
    }

    [Test]
    public async Task List_SortedByLastUse_Descending_PutsTheMostRecentFirst()
    {
        var stale = GivenToken(_owner, "stale", lastUsedAt: Now.AddDays(-10));
        var recent = GivenToken(_owner, "recent", lastUsedAt: Now.AddDays(-1));

        var page = await _service.GetAccessTokensAsync(
            new PaginationParams(),
            sort: new SortOptions<AccessTokenSortField> { SortBy = AccessTokenSortField.LastUsedAt });

        Assert.That(page.Results.Select(t => t.Id), Is.EqualTo(new[] { recent.Id, stale.Id }));
    }

    [Test]
    public async Task List_Pages()
    {
        var tokens = Enumerable.Range(0, 5)
            .Select(i => GivenToken(_owner, $"token-{i}", createdAt: Now.AddMinutes(-i)))
            .ToList();

        var page = await _service.GetAccessTokensAsync(new PaginationParams { Offset = 1, PageSize = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(page.Results.Select(t => t.Id), Is.EqualTo(new[] { tokens[1].Id, tokens[2].Id }));
            Assert.That(page.Offset, Is.EqualTo(1));
            Assert.That(page.Total, Is.EqualTo(5));
        });
    }

    #endregion

    #region Deletion

    [Test]
    public async Task Delete_OwnToken_DeletesIt()
    {
        var token = GivenToken(_owner, "laptop");

        var deleted = await _service.DeleteAccessTokenAsync(token.Id);

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(TokenExists(token.Id), Is.False);
        });
    }

    [Test]
    public async Task Delete_AnotherUsersToken_IsAMissAndLeavesItInPlace()
    {
        var theirs = GivenToken(_other, "laptop");

        var deleted = await _service.DeleteAccessTokenAsync(theirs.Id);

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.False);
            Assert.That(TokenExists(theirs.Id), Is.True);
        });
    }

    [Test]
    public async Task Delete_UnknownToken_IsAMiss()
    {
        Assert.That(await _service.DeleteAccessTokenAsync(Guid.CreateVersion7()), Is.False);
    }

    #endregion

    #region No user

    [Test]
    public async Task Anonymous_CanNotList()
    {
        _actors.Actor = Actor.Anonymous;

        await Assert.ThrowsAsync<NoCurrentUserException>(() => _service.GetAccessTokensAsync(new PaginationParams()));
    }

    [Test]
    public async Task Anonymous_CanNotMint()
    {
        _actors.Actor = Actor.Anonymous;

        await Assert.ThrowsAsync<NoCurrentUserException>(() => _service.CreateAccessTokenAsync(Request("laptop")));
    }

    [Test]
    public async Task Anonymous_CanNotDelete()
    {
        _actors.Actor = Actor.Anonymous;
        var token = GivenToken(_owner, "laptop");

        await Assert.ThrowsAsync<NoCurrentUserException>(() => _service.DeleteAccessTokenAsync(token.Id));
        Assert.That(TokenExists(token.Id), Is.True);
    }

    [Test]
    public async Task SystemActorWithoutAUser_HasNoTokensToManage()
    {
        // A system actor bypasses visibility elsewhere, but tokens are always somebody's own, and
        // it is nobody.
        _actors.Actor = Actor.System;

        await Assert.ThrowsAsync<NoCurrentUserException>(() => _service.GetAccessTokensAsync(new PaginationParams()));
    }

    #endregion

    #region Scope

    // AccessTokenAccessPolicyTests covers every row of the verdicts; these show that each method asks
    // its own verdict before doing anything, and turns Forbidden into InsufficientScopeException.

    [Test]
    public void List_WhenTheVerdictIsForbidden_ThrowsInsufficientScope()
    {
        _actors.Actor = ScopedActor("pacman-manager:tokens:create pacman-manager:tokens:delete");

        var e = Assert.ThrowsAsync<InsufficientScopeException>(() => _service.GetAccessTokensAsync(new PaginationParams()));

        AssertRefused(e, ScopeValues.ActionNames.Read);
    }

    [Test]
    public async Task List_WhenTheVerdictIsAllowed_Proceeds()
    {
        var mine = GivenToken(_owner, "mine");
        _actors.Actor = ScopedActor("pacman-manager:tokens:read");

        var page = await _service.GetAccessTokensAsync(new PaginationParams());

        Assert.That(page.Results.Select(t => t.Id), Is.EqualTo(new[] { mine.Id }));
    }

    [Test]
    public void Create_WhenTheVerdictIsForbidden_ThrowsInsufficientScope_AndMintsNothing()
    {
        // The scope a Basic credential carries.
        _actors.Actor = ScopedActor("pacman-manager:*:read");

        var e = Assert.ThrowsAsync<InsufficientScopeException>(() => _service.CreateAccessTokenAsync(Request("laptop")));

        AssertRefused(e, ScopeValues.ActionNames.Create);
        Assert.That(TokenCount(), Is.Zero, "no token was minted");
    }

    [Test]
    public async Task Create_WhenTheVerdictIsAllowed_Proceeds()
    {
        _actors.Actor = ScopedActor("pacman-manager:tokens:create pacman-manager:tokens:read");

        var created = await _service.CreateAccessTokenAsync(Request("laptop"));

        Assert.That(StoredToken(created.Id).UserId, Is.EqualTo(_owner.Id));
    }

    [Test]
    public void Delete_WhenTheVerdictIsForbidden_ThrowsInsufficientScope_AndLeavesTheTokenInPlace()
    {
        var token = GivenToken(_owner, "laptop");
        _actors.Actor = ScopedActor("pacman-manager:tokens:read pacman-manager:tokens:create");

        var e = Assert.ThrowsAsync<InsufficientScopeException>(() => _service.DeleteAccessTokenAsync(token.Id));

        AssertRefused(e, ScopeValues.ActionNames.Delete);
        Assert.That(TokenExists(token.Id), Is.True, "still there");
    }

    [Test]
    public async Task Delete_WhenTheVerdictIsAllowed_Proceeds()
    {
        var token = GivenToken(_owner, "laptop");
        _actors.Actor = ScopedActor("pacman-manager:tokens:delete pacman-manager:tokens:read");

        var deleted = await _service.DeleteAccessTokenAsync(token.Id);

        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(TokenExists(token.Id), Is.False);
        });
    }

    [Test]
    public void Delete_AnotherUsersToken_WithoutTheScope_IsRefusedBeforeItIsLookedFor()
    {
        // The verdict is asked first, so a refusal says nothing about whether the id names a token.
        var theirs = GivenToken(_other, "laptop");
        _actors.Actor = ScopedActor("pacman-manager:tokens:read");

        Assert.ThrowsAsync<InsufficientScopeException>(() => _service.DeleteAccessTokenAsync(theirs.Id));
        Assert.ThrowsAsync<InsufficientScopeException>(() => _service.DeleteAccessTokenAsync(Guid.CreateVersion7()));
    }

    private Actor ScopedActor(string claim) => Actor.For(_owner, ActorScope.Parse(claim, NullLogger.Instance));

    private static void AssertRefused(InsufficientScopeException? e, string action)
    {
        Assert.Multiple(() =>
        {
            Assert.That(e!.Entity, Is.EqualTo(ScopeValues.EntityNames.Tokens));
            Assert.That(e.Action, Is.EqualTo(action));
        });
    }

    #endregion

    #region Helpers

    private static CreateAccessTokenRequest Request(string name, DateTimeOffset? expiresAt = null) =>
        new() { Name = name, ExpiresAt = expiresAt };

    private PacmanAccessToken GivenToken(
        User owner,
        string name,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? expiresAt = null,
        DateTimeOffset? lastUsedAt = null)
    {
        using var seed = NewContext();
        var token = new PacmanAccessToken
        {
            UserId = owner.Id,
            Name = name,
            NormalizedName = name.ToLowerInvariant(),
            TokenHash = AccessTokenFormat.HashSecret(AccessTokenFormat.GenerateSecret()),
            CreatedAt = createdAt ?? Now,
            ExpiresAt = expiresAt,
            LastUsedAt = lastUsedAt,
        };
        seed.Add(token);
        seed.SaveChanges();
        return token;
    }

    private int TokenCount()
    {
        using var read = NewContext();
        return read.PacmanAccessTokens.Count();
    }

    private PacmanAccessToken StoredToken(Guid id)
    {
        using var read = NewContext();
        return read.PacmanAccessTokens.AsNoTracking().Single(t => t.Id == id);
    }

    private bool TokenExists(Guid id)
    {
        using var read = NewContext();
        return read.PacmanAccessTokens.Any(t => t.Id == id);
    }

    private string NameIndex() =>
        _dbContext.Model
            .FindEntityType(typeof(PacmanAccessToken))!
            .GetIndexes()
            .Single(i => i.IsUnique)
            .GetDatabaseName()!;

    private static DbUpdateException UniqueViolation(string constraintName) =>
        new("Simulated unique violation.", new PostgresException(
            "duplicate key value violates unique constraint",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.UniqueViolation,
            constraintName: constraintName));

    private PacmanManagerDbContext NewContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(_databaseName)
            .AddInterceptors(interceptors)
            .Options;
        return new PacmanManagerDbContext(options);
    }

    /// <summary>
    /// Fails saves on demand with the exception a test supplies, the way the database refusing a row
    /// would.
    /// </summary>
    private class SimulatedSaveFailure : SaveChangesInterceptor
    {
        public Func<Exception>? Failure { get; set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            Failure is { } failure ? throw failure() : ValueTask.FromResult(result);
    }

    private class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    #endregion
}
