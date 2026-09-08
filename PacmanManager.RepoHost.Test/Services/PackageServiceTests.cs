using LibAlpmSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;
using PacmanManager.RepoHost.Startup.LibAlpm;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Tests for <see cref="PackageService"/>'s read paths.
/// </summary>
/// <remarks>
/// The fixture holds one public repository owned by somebody else, one private repository owned by
/// the caller and one private repository owned by somebody else, so that every filter and lookup
/// can be asked whether it stays inside the visible set.
/// </remarks>
[TestFixture]
public class PackageServiceTests
{
    private static readonly DateTimeOffset Oldest = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Middle = new(2024, 6, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Newest = new(2024, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private PacmanManagerDbContext _dbContext = null!;
    private TestActorAccessor _actors = null!;
    private PackageService _service = null!;

    private User _caller = null!;
    private User _other = null!;
    private PacmanRepository _publicRepository = null!;
    private PacmanRepository _callersPrivateRepository = null!;
    private PacmanRepository _othersPrivateRepository = null!;

    [SetUp]
    public void SetUp()
    {
        var options = new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new PacmanManagerDbContext(options);

        _caller = _dbContext.Add(new User { DisplayName = "tester", Email = "test@test.com" }).Entity;
        _other = _dbContext.Add(new User { DisplayName = "somebody else", Email = "other@test.com" }).Entity;
        _dbContext.SaveChanges();

        _publicRepository = GivenRepository("public-repo", _other, isPublic: true);
        _callersPrivateRepository = GivenRepository("mine", _caller, isPublic: false);
        _othersPrivateRepository = GivenRepository("theirs", _other, isPublic: false);
        _dbContext.SaveChanges();

        // Most tests care about what an identified caller can see, so that is the default actor.
        // Tests that exercise the visibility rules override it.
        _actors = new TestActorAccessor { Actor = Actor.For(_caller) };
        // The read paths reach none of the publishing collaborators, so they are supplied as bare
        // doubles here; PackageServicePublishTests wires up the real ones.
        _service = new PackageService(
            _dbContext,
            _actors,
            new RepositoryAccessPolicy(),
            new PackageAccessPolicy(),
            new RepositoryDatabaseToolRunner(
                Mock.Of<ICliToolRunner>(),
                new TestOutputLogger<RepositoryDatabaseToolRunner>()),
            Mock.Of<IFileSystem>(),
            Mock.Of<IPackagePathResolver>(),
            new RepositoryDatabaseLock(),
            Mock.Of<ILibAlpm>(),
            Options.Create(new PacmanConfigSettings { DataDir = "/tmp/pacman" }),
            new TestOutputLogger<PackageService>());
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region GetPackageByIdAsync

    [Test]
    public async Task GetPackageByIdAsync_ReturnsThePackage_WhenItsRepositoryIsVisible()
    {
        // Arrange
        var package = GivenPackage(_publicRepository, "my-tool", version: "1.4.2-1");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackageByIdAsync(package.Id);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Id, Is.EqualTo(package.Id));
            Assert.That(result.Name, Is.EqualTo("my-tool"));
            Assert.That(result.Version, Is.EqualTo("1.4.2-1"));
            Assert.That(result.RepositoryId, Is.EqualTo(_publicRepository.Id));
        });
    }

    [Test]
    public async Task GetPackageByIdAsync_ProjectsThePublisherAsASummary()
    {
        // Arrange
        var package = GivenPackage(_publicRepository, "my-tool", publisher: _other);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackageByIdAsync(package.Id);

        // Assert
        Assert.That(result!.Publisher, Is.EqualTo(PublicUserInfo.FromUser(_other)));
    }

    [Test]
    public async Task GetPackageByIdAsync_ReturnsNull_WhenNoSuchPackageExists()
    {
        // Act
        var result = await _service.GetPackageByIdAsync(Guid.NewGuid());

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetPackageByIdAsync_ReturnsNull_WhenTheRepositoryIsSomebodyElsesPrivateOne()
    {
        // Arrange
        var package = GivenPackage(_othersPrivateRepository, "hidden");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackageByIdAsync(package.Id);

        // Assert
        Assert.That(result, Is.Null, "A package in a repository the caller cannot see must look absent.");
    }

    [Test]
    public async Task GetPackageByIdAsync_ReturnsThePackage_FromTheCallersOwnPrivateRepository()
    {
        // Arrange
        var package = GivenPackage(_callersPrivateRepository, "mine-only");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackageByIdAsync(package.Id);

        // Assert
        Assert.That(result, Is.Not.Null);
    }

    #endregion

    #region GetPackageByNameAsync

    [Test]
    public async Task GetPackageByNameAsync_ReturnsThePackageInTheNamedRepository()
    {
        // Arrange
        GivenPackage(_publicRepository, "my-tool", version: "1.0.0-1");
        GivenPackage(_callersPrivateRepository, "my-tool", version: "2.0.0-1");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackageByNameAsync(_callersPrivateRepository.Id, "my-tool");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Version, Is.EqualTo("2.0.0-1"));
        });
    }

    [Test]
    public async Task GetPackageByNameAsync_ReturnsNull_WhenTheRepositoryHoldsNoSuchPackage()
    {
        // Arrange
        GivenPackage(_publicRepository, "my-tool");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackageByNameAsync(_publicRepository.Id, "some-other-tool");

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task GetPackageByNameAsync_ReturnsNull_WhenTheRepositoryIsSomebodyElsesPrivateOne()
    {
        // Arrange
        GivenPackage(_othersPrivateRepository, "hidden");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackageByNameAsync(_othersPrivateRepository.Id, "hidden");

        // Assert
        Assert.That(result, Is.Null, "A private repository must be indistinguishable from one that is not there.");
    }

    #endregion

    #region GetPackagesAsync — visibility

    [Test]
    public async Task GetPackagesAsync_AnonymousActor_SeesOnlyPackagesInPublicRepositories()
    {
        // Arrange
        GivenPackage(_publicRepository, "visible");
        GivenPackage(_callersPrivateRepository, "mine");
        GivenPackage(_othersPrivateRepository, "theirs");
        await _dbContext.SaveChangesAsync();
        _actors.Actor = Actor.Anonymous;

        // Act
        var result = await _service.GetPackagesAsync(new PaginationParams { PageSize = 50 });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "visible" }));
            Assert.That(result.Total, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetPackagesAsync_AuthenticatedActor_SeesPublicPackagesAndThoseInTheirOwnRepositories()
    {
        // Arrange
        GivenPackage(_publicRepository, "visible");
        GivenPackage(_callersPrivateRepository, "mine");
        GivenPackage(_othersPrivateRepository, "theirs");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(new PaginationParams { PageSize = 50 });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EquivalentTo(new[] { "visible", "mine" }));
    }

    [Test]
    public async Task GetPackagesAsync_SystemActor_SeesEveryPackage()
    {
        // Arrange
        GivenPackage(_publicRepository, "visible");
        GivenPackage(_callersPrivateRepository, "mine");
        GivenPackage(_othersPrivateRepository, "theirs");
        await _dbContext.SaveChangesAsync();
        _actors.Actor = Actor.System;

        // Act
        var result = await _service.GetPackagesAsync(new PaginationParams { PageSize = 50 });

        // Assert
        Assert.That(result.Total, Is.EqualTo(3));
    }

    #endregion

    #region GetPackagesAsync — paging

    [Test]
    public async Task GetPackagesAsync_ReturnsAnEmptyPage_WhenThereAreNoPackages()
    {
        // Act
        var result = await _service.GetPackagesAsync(new PaginationParams { PageSize = 10 });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Results, Is.Empty);
            Assert.That(result.Total, Is.Zero);
            Assert.That(result.Offset, Is.Zero);
        });
    }

    [Test]
    public async Task GetPackagesAsync_AppliesOffsetAndPageSize_AndReportsTheUnpagedTotal()
    {
        // Arrange
        GivenPackage(_publicRepository, "alpha");
        GivenPackage(_publicRepository, "bravo");
        GivenPackage(_publicRepository, "charlie");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(new PaginationParams { Offset = 1, PageSize = 1 });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "bravo" }));
            Assert.That(result.Total, Is.EqualTo(3));
            Assert.That(result.Offset, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task GetPackagesAsync_TreatsAnOmittedFilterAsNoCriteriaAtAll()
    {
        // Arrange
        GivenPackage(_publicRepository, "alpha");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(new PaginationParams { PageSize = 10 }, null);

        // Assert
        Assert.That(result.Total, Is.EqualTo(1));
    }

    #endregion

    #region GetPackagesAsync — filtering

    [Test]
    public async Task GetPackagesAsync_FiltersByRepositoryIds()
    {
        // Arrange
        GivenPackage(_publicRepository, "in-public");
        GivenPackage(_callersPrivateRepository, "in-mine");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { RepositoryIds = [_callersPrivateRepository.Id] });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "in-mine" }));
    }

    [Test]
    public async Task GetPackagesAsync_FiltersByPublisherIds()
    {
        // Arrange
        GivenPackage(_publicRepository, "theirs", publisher: _other);
        GivenPackage(_publicRepository, "mine", publisher: _caller);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { PublisherIds = [_caller.Id] });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "mine" }));
    }

    [Test]
    public async Task GetPackagesAsync_FiltersByArchitecture()
    {
        // Arrange
        GivenPackage(_publicRepository, "native", architecture: "x86_64");
        GivenPackage(_publicRepository, "portable", architecture: "any");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { Architecture = "any" });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "portable" }));
    }

    [Test]
    public async Task GetPackagesAsync_FiltersByNameContains()
    {
        // Arrange
        GivenPackage(_publicRepository, "my-tool");
        GivenPackage(_publicRepository, "other-thing");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { NameContains = "tool" });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "my-tool" }));
    }

    [Test]
    public async Task GetPackagesAsync_FiltersByUpdatedSince()
    {
        // Arrange
        GivenPackage(_publicRepository, "stale", updatedAt: Oldest);
        GivenPackage(_publicRepository, "fresh", updatedAt: Newest);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { UpdatedSince = Middle });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "fresh" }));
    }

    [Test]
    public async Task GetPackagesAsync_SearchMatchesTheName()
    {
        // Arrange
        GivenPackage(_publicRepository, "my-tool");
        GivenPackage(_publicRepository, "unrelated");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { Search = "tool" });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "my-tool" }));
    }

    [Test]
    public async Task GetPackagesAsync_SearchMatchesThePackageBase()
    {
        // Arrange
        GivenPackage(_publicRepository, "libwidget", packageBase: "widget-suite");
        GivenPackage(_publicRepository, "unrelated");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { Search = "widget-suite" });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "libwidget" }));
    }

    [Test]
    public async Task GetPackagesAsync_SearchMatchesTheDescription()
    {
        // Arrange
        GivenPackage(_publicRepository, "opaque-name", description: "Frobnicates the widgets");
        GivenPackage(_publicRepository, "unrelated");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { Search = "frobnicates" });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "opaque-name" }));
    }

    [Test]
    public async Task GetPackagesAsync_SearchIgnoresCase()
    {
        // A bare string.Contains is case-sensitive under both Npgsql and the in-memory provider, so
        // this is the case that fails if either side of the comparison stops being lowered.
        // Arrange
        GivenPackage(_publicRepository, "my-tool", packageBase: "MyToolSuite", description: "Builds THINGS");
        await _dbContext.SaveChangesAsync();

        // Act
        var byName = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 }, new PackageFilter { Search = "MY-TOOL" });
        var byBase = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 }, new PackageFilter { Search = "mytoolsuite" });
        var byDescription = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 }, new PackageFilter { Search = "things" });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(byName.Total, Is.EqualTo(1), "The name should have matched regardless of case.");
            Assert.That(byBase.Total, Is.EqualTo(1), "The package base should have matched regardless of case.");
            Assert.That(byDescription.Total, Is.EqualTo(1), "The description should have matched regardless of case.");
        });
    }

    [Test]
    public async Task GetPackagesAsync_SearchToleratesPackagesWithNoBaseOrDescription()
    {
        // Base and Description are nullable, and a repository will usually hold at least one package
        // that has neither. Searching such a repository must return the packages that match and
        // leave the rest out, rather than failing on the way past the nulls.
        // Arrange
        GivenPackage(_publicRepository, "bare", packageBase: null, description: null);
        GivenPackage(_publicRepository, "my-tool", packageBase: "my-tool", description: "A tool");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { Search = "tool" });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "my-tool" }));
    }

    [Test]
    public async Task GetPackagesAsync_SearchContributesNothing_WhenItIsUnset()
    {
        // Arrange
        GivenPackage(_publicRepository, "alpha");
        GivenPackage(_publicRepository, "bravo");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { Search = null });

        // Assert
        Assert.That(result.Total, Is.EqualTo(2));
    }

    [Test]
    public async Task GetPackagesAsync_CombinesCriteria_ByNarrowingWithEachOne()
    {
        // Arrange
        GivenPackage(_publicRepository, "my-tool", architecture: "x86_64");
        GivenPackage(_publicRepository, "my-tool-docs", architecture: "any");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { Search = "my-tool", Architecture = "any" });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "my-tool-docs" }));
    }

    #endregion

    #region GetPackagesAsync — no filter widens the visible set

    [Test]
    public async Task GetPackagesAsync_RepositoryIdsNamingAnInvisibleRepository_ReturnsEmpty()
    {
        // Arrange
        GivenPackage(_othersPrivateRepository, "hidden");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { RepositoryIds = [_othersPrivateRepository.Id] });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result.Results, Is.Empty, "Naming a repository the caller cannot see must yield nothing.");
            Assert.That(result.Total, Is.Zero);
        });
    }

    [Test]
    public async Task GetPackagesAsync_RepositoryIdsNamingAnUnknownRepository_ReturnsEmptyRatherThanErroring()
    {
        // Arrange
        GivenPackage(_publicRepository, "visible");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { RepositoryIds = [Guid.NewGuid()] });

        // Assert
        Assert.That(result.Results, Is.Empty);
    }

    [Test]
    public async Task GetPackagesAsync_PublisherIdsCannotRevealPackagesInAnInvisibleRepository()
    {
        // Arrange
        GivenPackage(_othersPrivateRepository, "hidden", publisher: _other);
        GivenPackage(_publicRepository, "visible", publisher: _other);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { PublisherIds = [_other.Id] });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "visible" }));
    }

    [Test]
    public async Task GetPackagesAsync_NameContainsCannotRevealPackagesInAnInvisibleRepository()
    {
        // Arrange
        GivenPackage(_othersPrivateRepository, "secret-tool");
        await _dbContext.SaveChangesAsync();
        _actors.Actor = Actor.Anonymous;

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { NameContains = "secret" });

        // Assert
        Assert.That(result.Results, Is.Empty);
    }

    [Test]
    public async Task GetPackagesAsync_SearchCannotRevealPackagesInAnInvisibleRepository()
    {
        // Arrange
        GivenPackage(_othersPrivateRepository, "secret-tool", description: "Very secret");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { Search = "secret" });

        // Assert
        Assert.That(result.Results, Is.Empty);
    }

    [Test]
    public async Task GetPackagesAsync_UpdatedSinceCannotRevealPackagesInAnInvisibleRepository()
    {
        // Arrange
        GivenPackage(_othersPrivateRepository, "hidden", updatedAt: Newest);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { UpdatedSince = Oldest });

        // Assert
        Assert.That(result.Results, Is.Empty);
    }

    [Test]
    public async Task GetPackagesAsync_ArchitectureCannotRevealPackagesInAnInvisibleRepository()
    {
        // Arrange
        GivenPackage(_othersPrivateRepository, "hidden", architecture: "any");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            new PackageFilter { Architecture = "any" });

        // Assert
        Assert.That(result.Results, Is.Empty);
    }

    #endregion

    #region GetPackagesAsync — sorting

    [Test]
    public async Task GetPackagesAsync_SortsByNameAscending_WhenNothingIsRequested()
    {
        // Arrange
        await GivenTheSortFixtureAsync();

        // Act
        var result = await _service.GetPackagesAsync(new PaginationParams { PageSize = 50 });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "alpha", "bravo", "charlie" }));
    }

    [Test]
    public async Task GetPackagesAsync_SortsByNameDescending_WhenAskedTo()
    {
        // Arrange
        await GivenTheSortFixtureAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            sort: new SortOptions<PackageSortField>
            {
                SortBy = PackageSortField.Name,
                Direction = SortDirection.Descending
            });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "charlie", "bravo", "alpha" }));
    }

    [Test]
    public async Task GetPackagesAsync_SortsByUpdated_NewestFirstByDefault()
    {
        // Arrange
        await GivenTheSortFixtureAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            sort: new SortOptions<PackageSortField> { SortBy = PackageSortField.Updated });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "alpha", "charlie", "bravo" }));
    }

    [Test]
    public async Task GetPackagesAsync_SortsByCreated_NewestFirstByDefault()
    {
        // Arrange
        await GivenTheSortFixtureAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            sort: new SortOptions<PackageSortField> { SortBy = PackageSortField.Created });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "bravo", "alpha", "charlie" }));
    }

    [Test]
    public async Task GetPackagesAsync_SortsByInstalledSize_LargestFirstByDefault()
    {
        // Arrange
        await GivenTheSortFixtureAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            sort: new SortOptions<PackageSortField> { SortBy = PackageSortField.InstalledSize });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "charlie", "bravo", "alpha" }));
    }

    [Test]
    public async Task GetPackagesAsync_SortsByInstalledSizeAscending_WhenAskedTo()
    {
        // Arrange
        await GivenTheSortFixtureAsync();

        // Act
        var result = await _service.GetPackagesAsync(
            new PaginationParams { PageSize = 50 },
            sort: new SortOptions<PackageSortField>
            {
                SortBy = PackageSortField.InstalledSize,
                Direction = SortDirection.Ascending
            });

        // Assert
        Assert.That(result.Results.Select(p => p.Name), Is.EqualTo(new[] { "alpha", "bravo", "charlie" }));
    }

    #endregion

    /// <summary>
    /// Three packages in the public repository, ordered differently by every sortable property, so
    /// that a case ordering by the wrong one cannot produce the expected sequence by accident.
    /// </summary>
    private async Task GivenTheSortFixtureAsync()
    {
        GivenPackage(_publicRepository, "alpha", createdAt: Middle, updatedAt: Newest, installedSize: 100);
        GivenPackage(_publicRepository, "bravo", createdAt: Newest, updatedAt: Oldest, installedSize: 200);
        GivenPackage(_publicRepository, "charlie", createdAt: Oldest, updatedAt: Middle, installedSize: 300);
        await _dbContext.SaveChangesAsync();
    }

    private PacmanRepository GivenRepository(string name, User owner, bool isPublic) =>
        _dbContext.Add(new PacmanRepository
        {
            Name = name,
            Architecture = "x86_64",
            IsPublic = isPublic,
            Owner = owner,
        }).Entity;

    private PacmanPackage GivenPackage(
        PacmanRepository repository,
        string name,
        User? publisher = null,
        string version = "1.0.0-1",
        string architecture = "x86_64",
        string? packageBase = null,
        string? description = null,
        long installedSize = 0,
        DateTimeOffset? createdAt = null,
        DateTimeOffset? updatedAt = null) =>
        _dbContext.Add(new PacmanPackage
        {
            Repository = repository,
            Publisher = publisher ?? _caller,
            Name = name,
            Version = version,
            Architecture = architecture,
            Base = packageBase,
            Description = description,
            FileName = $"{name}-{version}-{architecture}.pkg.tar.zst",
            Sha256Sum = new string('a', 64),
            Md5Sum = new string('b', 32),
            InstalledSize = installedSize,
            CreatedAt = createdAt ?? Middle,
            UpdatedAt = updatedAt ?? Middle,
        }).Entity;
}
