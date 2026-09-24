using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Moq;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Services;

[TestFixture]
public class UserManagementServiceTests
{
    private TestActorAccessor _actorAccessor;
    private PacmanManagerDbContext _dbContext;
    private UserManagementService _subject;

    [SetUp]
    public void SetUp()
    {
        _actorAccessor = new TestActorAccessor();
        _dbContext = new PacmanManagerDbContext(new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options);
        _subject = new UserManagementService(_actorAccessor, _dbContext);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Test]
    public async Task GetCurrentUserAsync_WithCurrentUser_ReturnsItProjectedToCurrentUser()
    {
        var user = new User { DisplayName = "Alex", NormalizedDisplayName = "alex", Email = "alex@example.com" };
        _actorAccessor.Actor = Actor.For(user, ActorScope.Unrestricted);

        var result = await _subject.GetCurrentUserAsync();

        Assert.That(result, Is.EqualTo(new CurrentUser
        {
            Id = user.Id,
            DisplayName = "Alex",
            Email = "alex@example.com",
        }));
    }

    [Test]
    public void GetCurrentUserAsync_WithNoCurrentUser_ThrowsNoCurrentUserException()
    {
        _actorAccessor.Actor = Actor.Anonymous;

        Assert.ThrowsAsync<NoCurrentUserException>(() => _subject.GetCurrentUserAsync());
    }

    [Test]
    public void GetCurrentUserAsync_AsSystemWithNoUser_ThrowsNoCurrentUserException()
    {
        _actorAccessor.Actor = Actor.System;

        Assert.ThrowsAsync<NoCurrentUserException>(() => _subject.GetCurrentUserAsync());
    }

    [Test]
    public async Task GetCurrentUserAsync_PassesCancellationTokenThrough()
    {
        using var cts = new CancellationTokenSource();
        var user = new User { DisplayName = "Alex", NormalizedDisplayName = "alex", Email = "alex@example.com" };
        var actorAccessor = new Mock<IActorAccessor>(MockBehavior.Strict);
        actorAccessor
            .Setup(a => a.GetActorAsync(cts.Token))
            .ReturnsAsync(Actor.For(user, ActorScope.Unrestricted));
        var subject = new UserManagementService(actorAccessor.Object, _dbContext);

        await subject.GetCurrentUserAsync(cts.Token);

        actorAccessor.Verify(a => a.GetActorAsync(cts.Token), Times.Once);
    }

    [Test]
    public async Task GetUserByIdAsync_WithKnownId_ReturnsThatUserAsPublicUserInfo()
    {
        var user = _dbContext.Users.Add(new User { DisplayName = "Alex", NormalizedDisplayName = "alex", Email = "alex@example.com" }).Entity;
        _dbContext.Users.Add(new User { DisplayName = "Sam", NormalizedDisplayName = "sam", Email = "sam@example.com" });
        await _dbContext.SaveChangesAsync();

        var result = await _subject.GetUserByIdAsync(user.Id);

        Assert.That(result, Is.EqualTo(new PublicUserInfo
        {
            Id = user.Id,
            DisplayName = "Alex",
        }));
    }

    [Test]
    public async Task GetUserByIdAsync_WithUnknownId_ReturnsNull()
    {
        _dbContext.Users.Add(new User { DisplayName = "Alex", NormalizedDisplayName = "alex", Email = "alex@example.com" });
        await _dbContext.SaveChangesAsync();

        var result = await _subject.GetUserByIdAsync(Guid.CreateVersion7());

        Assert.That(result, Is.Null);
    }

    private User GivenUser(string displayName, string? email = null)
    {
        var user = _dbContext.Users.Add(new User
        {
            DisplayName = displayName,
            NormalizedDisplayName = displayName.ToLowerInvariant(),
            Email = email ?? $"{Guid.NewGuid():N}@example.com",
        }).Entity;
        _dbContext.SaveChanges();
        return user;
    }

    [Test]
    public async Task ListUsersAsync_Unsorted_IsAlphabeticalByDisplayNameIgnoringCase()
    {
        var charlie = GivenUser("charlie");
        var alex = GivenUser("Alex");
        var bea = GivenUser("bea");

        var page = await _subject.ListUsersAsync(new PaginationParams());

        Assert.That(page.Results.Select(u => u.Id), Is.EqualTo(new[] { alex.Id, bea.Id, charlie.Id }));
    }

    [Test]
    public async Task ListUsersAsync_SortedByDisplayNameDescending_RunsZToA()
    {
        var alex = GivenUser("Alex");
        var charlie = GivenUser("charlie");
        var bea = GivenUser("Bea");

        var page = await _subject.ListUsersAsync(
            new PaginationParams(),
            sort: new SortOptions<UserSortField>
            {
                SortBy = UserSortField.DisplayName,
                Direction = SortDirection.Descending,
            });

        Assert.That(page.Results.Select(u => u.Id), Is.EqualTo(new[] { charlie.Id, bea.Id, alex.Id }));
    }

    [Test]
    public async Task ListUsersAsync_Pages()
    {
        var users = Enumerable.Range(0, 5).Select(i => GivenUser($"user-{i}")).ToList();

        var page = await _subject.ListUsersAsync(new PaginationParams { Offset = 1, PageSize = 2 });

        Assert.Multiple(() =>
        {
            Assert.That(page.Results.Select(u => u.Id), Is.EqualTo(new[] { users[1].Id, users[2].Id }));
            Assert.That(page.Offset, Is.EqualTo(1));
            Assert.That(page.Total, Is.EqualTo(5));
        });
    }

    [Test]
    public async Task ListUsersAsync_WithDisplayNameContains_ReturnsOnlyMatchingUsers()
    {
        var alex = GivenUser("Alex");
        var alexandra = GivenUser("Alexandra");
        GivenUser("Sam");

        var page = await _subject.ListUsersAsync(
            new PaginationParams(),
            new UserFilter { DisplayNameContains = "alex" });

        Assert.Multiple(() =>
        {
            Assert.That(page.Results.Select(u => u.Id), Is.EqualTo(new[] { alex.Id, alexandra.Id }));
            Assert.That(page.Total, Is.EqualTo(2));
        });
    }

    [Test]
    public async Task ListUsersAsync_WithDisplayNameContains_IgnoresCase()
    {
        // Neither side's case matches the other's, so a naive DisplayName.Contains(term) finds nothing
        // under either the in-memory provider or Npgsql.
        var alex = GivenUser("Alex");
        GivenUser("Sam");

        var page = await _subject.ListUsersAsync(
            new PaginationParams(),
            new UserFilter { DisplayNameContains = "aLEX" });

        Assert.That(page.Results.Select(u => u.Id), Is.EqualTo(new[] { alex.Id }));
    }

    [Test]
    public async Task ListUsersAsync_WithNoMatches_ReturnsAnEmptyPage()
    {
        GivenUser("Alex");

        var page = await _subject.ListUsersAsync(
            new PaginationParams(),
            new UserFilter { DisplayNameContains = "zzz" });

        Assert.Multiple(() =>
        {
            Assert.That(page.Results, Is.Empty);
            Assert.That(page.Total, Is.Zero);
        });
    }

    [Test]
    public async Task ListUsersAsync_WhenAnonymous_ListsEveryUser()
    {
        _actorAccessor.Actor = Actor.Anonymous;
        var alex = GivenUser("Alex");
        var sam = GivenUser("Sam");

        var page = await _subject.ListUsersAsync(new PaginationParams());

        Assert.That(page.Results.Select(u => u.Id), Is.EqualTo(new[] { alex.Id, sam.Id }));
    }

    [Test]
    public async Task ListUsersAsync_ProjectsToPublicUserInfo_WithNoEmail()
    {
        var alex = GivenUser("Alex", "alex@example.com");

        var page = await _subject.ListUsersAsync(new PaginationParams());

        var json = JsonSerializer.Serialize(page);
        Assert.Multiple(() =>
        {
            Assert.That(page.Results.Single(), Is.EqualTo(new PublicUserInfo { Id = alex.Id, DisplayName = "Alex" }));
            Assert.That(json, Does.Not.Contain("alex@example.com"));
            Assert.That(json, Does.Not.Contain("Email").IgnoreCase);
        });
    }
}
