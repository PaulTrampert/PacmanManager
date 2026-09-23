using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;
using PacmanManager.RepoHost.Test.Containers;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Access token name collisions. Nothing checks for a collision before writing; the unique index on
/// <c>(UserId, NormalizedName)</c> decides, and the in-memory provider does not enforce unique
/// indexes, so these run against Postgres.
/// </summary>
[TestFixture]
public class AccessTokenServiceCollisionTests
{
    private DatabaseContainer _database = null!;
    private INetwork _network = null!;
    private DbContextOptions<PacmanManagerDbContext> _dbContextOptions = null!;
    private PacmanManagerDbContext _dbContext = null!;
    private TestActorAccessor _actors = null!;
    private AccessTokenService _service = null!;
    private User _owner = null!;
    private User _other = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _network = new NetworkBuilder()
            .WithName(nameof(AccessTokenServiceCollisionTests))
            .WithCleanUp(true)
            .Build();
        _database = new DatabaseContainer(_network);
        await _database.StartAsync(await TestImages.Migrations.GetAsync());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _database.DisposeAsync();
        await _network.DisposeAsync();
    }

    [SetUp]
    public async Task SetUp()
    {
        _dbContextOptions = new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseNpgsql(_database.LocalConnectionString)
            .Options;

        _dbContext = new PacmanManagerDbContext(_dbContextOptions);
        _owner = _dbContext.Add(new User { DisplayName = "owner", NormalizedDisplayName = "owner", Email = "owner@test.com" }).Entity;
        _other = _dbContext.Add(new User { DisplayName = "other", NormalizedDisplayName = "other", Email = "other@test.com" }).Entity;
        await _dbContext.SaveChangesAsync();

        _actors = new TestActorAccessor { Actor = Actor.For(_owner, ActorScope.Unrestricted) };
        _service = new AccessTokenService(
            _dbContext,
            _actors,
            TimeProvider.System,
            new TestOutputLogger<AccessTokenService>());
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.PacmanAccessTokens.ExecuteDeleteAsync();
        await _dbContext.Users.ExecuteDeleteAsync();
        _dbContext.Dispose();
    }

    [Test]
    public async Task Create_Throws_WhenTheUserAlreadyHasTheName()
    {
        await _service.CreateAccessTokenAsync(new CreateAccessTokenRequest { Name = "laptop" });

        Assert.ThrowsAsync<ItemExistsException>(
            () => _service.CreateAccessTokenAsync(new CreateAccessTokenRequest { Name = "laptop" }));
        Assert.That(await CountAsync(_owner), Is.EqualTo(1));
    }

    [Test]
    public async Task Create_Throws_WhenTheNameDiffersOnlyInCase()
    {
        await _service.CreateAccessTokenAsync(new CreateAccessTokenRequest { Name = "laptop" });

        Assert.ThrowsAsync<ItemExistsException>(
            () => _service.CreateAccessTokenAsync(new CreateAccessTokenRequest { Name = "Laptop" }));
        Assert.That(await CountAsync(_owner), Is.EqualTo(1));
    }

    [Test]
    public async Task Create_Succeeds_WhenTheNameIsTakenOnlyByAnotherUser()
    {
        _actors.Actor = Actor.For(_other, ActorScope.Unrestricted);
        await _service.CreateAccessTokenAsync(new CreateAccessTokenRequest { Name = "laptop" });
        _actors.Actor = Actor.For(_owner, ActorScope.Unrestricted);

        var created = await _service.CreateAccessTokenAsync(new CreateAccessTokenRequest { Name = "laptop" });

        Assert.That(created.Name, Is.EqualTo("laptop"));
    }

    private async Task<int> CountAsync(User user)
    {
        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        return await fresh.PacmanAccessTokens.CountAsync(t => t.UserId == user.Id);
    }
}
