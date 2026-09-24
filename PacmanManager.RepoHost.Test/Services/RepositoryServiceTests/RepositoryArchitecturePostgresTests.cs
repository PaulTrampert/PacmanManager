using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Npgsql;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;
using PacmanManager.RepoHost.Startup.LibAlpm;
using PacmanManager.RepoHost.Test.Containers;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services.RepositoryServiceTests;

/// <summary>
/// The parts of a repository's architectures that only Postgres can answer: how a query over the
/// <c>text[]</c> column translates, and what the package index refuses. The database is built by the
/// migrations image, so these also run against the schema the migrations produce.
/// </summary>
[TestFixture]
public class RepositoryArchitecturePostgresTests
{
    private DatabaseContainer _database = null!;
    private INetwork _network = null!;
    private DbContextOptions<PacmanManagerDbContext> _dbContextOptions = null!;
    private PacmanManagerDbContext _dbContext = null!;
    private RepositoryService _service = null!;
    private User _user = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetup()
    {
        _network = new NetworkBuilder()
            .WithName(nameof(RepositoryArchitecturePostgresTests))
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
        _user = _dbContext.Add(new User { DisplayName = "tester", NormalizedDisplayName = "tester", Email = "test@test.com" }).Entity;
        await _dbContext.SaveChangesAsync();

        var pacmanSettings = new Mock<IOptionsSnapshot<PacmanConfigSettings>>();
        pacmanSettings.Setup(s => s.Value).Returns(new PacmanConfigSettings { DataDir = "/tmp/pacman" });

        _service = new RepositoryService(
            _dbContext,
            new Mock<ICliToolRunner>().Object,
            new TestActorAccessor { Actor = Actor.For(_user) },
            new RepositoryAccessPolicy(),
            pacmanSettings.Object,
            new TestOutputLogger<RepositoryService>(),
            new Mock<IFileSystem>().Object);
    }

    [TearDown]
    public async Task TearDown()
    {
        await _dbContext.PacmanPackages.ExecuteDeleteAsync();
        await _dbContext.PacmanRepositories.ExecuteDeleteAsync();
        await _dbContext.Users.ExecuteDeleteAsync();
        await _dbContext.DisposeAsync();
    }

    [Test]
    public async Task SupportedArchitectures_RoundTripThroughTheColumn()
    {
        // Arrange
        var repository = await GivenRepositoryAsync("round-trip", Architectures.X86_64, "aarch64");

        // Act
        await using var fresh = new PacmanManagerDbContext(_dbContextOptions);
        var stored = await fresh.PacmanRepositories.SingleAsync(r => r.Id == repository.Id);

        // Assert
        Assert.That(stored.SupportedArchitectures, Is.EqualTo(new[] { Architectures.X86_64, "aarch64" }));
    }

    [Test]
    public async Task GetRepositoriesAsync_FiltersByArchitecture_AsContainmentInTheColumn()
    {
        // The column is an array, so the filter is a containment test rather than an equality: a
        // repository supporting two architectures matches either.
        // Arrange
        await GivenRepositoryAsync("x86-only", Architectures.X86_64);
        await GivenRepositoryAsync("arm-only", "aarch64");
        await GivenRepositoryAsync("both", "aarch64", Architectures.X86_64);

        // Act
        var result = await _service.GetRepositoriesAsync(
            new PaginationParams { PageSize = 50 },
            new RepositoryFilter { Architecture = Architectures.X86_64 });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Total, Is.EqualTo(2));
            Assert.That(result.Results.Select(r => r.Name), Is.EquivalentTo(new[] { "x86-only", "both" }));
        });
    }

    [Test]
    public async Task PackageIndex_AdmitsOneNameBuiltForTwoArchitectures()
    {
        // Arrange
        var repository = await GivenRepositoryAsync("two-builds", Architectures.X86_64, "aarch64");
        _dbContext.AddRange(NewPackage(repository, "foo", Architectures.X86_64), NewPackage(repository, "foo", "aarch64"));

        // Act
        await _dbContext.SaveChangesAsync();

        // Assert
        Assert.That(await _dbContext.PacmanPackages.CountAsync(p => p.Name == "foo"), Is.EqualTo(2));
    }

    [Test]
    public async Task PackageIndex_RefusesASecondRowForTheSameNameAndArchitecture()
    {
        // Arrange
        var repository = await GivenRepositoryAsync("duplicate-build", Architectures.X86_64);
        _dbContext.Add(NewPackage(repository, "foo", Architectures.X86_64));
        await _dbContext.SaveChangesAsync();
        _dbContext.Add(NewPackage(repository, "foo", Architectures.X86_64));

        // Act
        var thrown = Assert.ThrowsAsync<DbUpdateException>(async () => await _dbContext.SaveChangesAsync());

        // Assert
        var index = _dbContext.Model.FindEntityType(typeof(PacmanPackage))!.GetIndexes().Single(i => i.IsUnique);
        Assert.Multiple(() =>
        {
            Assert.That(index.GetDatabaseName(), Is.EqualTo("IX_PacmanPackages_RepositoryId_Name_Architecture"));
            Assert.That(thrown!.InnerException, Is.InstanceOf<PostgresException>()
                .With.Property(nameof(PostgresException.ConstraintName)).EqualTo(index.GetDatabaseName()));
        });
        _dbContext.ChangeTracker.Clear();
    }

    private async Task<PacmanRepository> GivenRepositoryAsync(string name, params string[] architectures)
    {
        var repository = _dbContext.Add(new PacmanRepository
        {
            Id = Guid.CreateVersion7(),
            Name = name,
            SupportedArchitectures = architectures,
            IsPublic = true,
            Owner = _user,
        }).Entity;
        await _dbContext.SaveChangesAsync();
        return repository;
    }

    private PacmanPackage NewPackage(PacmanRepository repository, string name, string architecture) => new()
    {
        RepositoryId = repository.Id,
        PublisherId = _user.Id,
        Name = name,
        Version = "1.0.0-1",
        Architecture = architecture,
        FileName = $"{name}-1.0.0-1-{architecture}.pkg.tar.zst",
        Sha256Sum = new string('a', PackageValidationConstants.Sha256SumLength),
        Md5Sum = new string('b', PackageValidationConstants.Md5SumLength),
    };
}
