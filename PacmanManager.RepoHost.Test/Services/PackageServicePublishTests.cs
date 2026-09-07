using System.Security.Cryptography;
using LibAlpmSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.CliTools;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;
using PacmanManager.RepoHost.Startup.LibAlpm;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Tests for <c>PackageService</c>'s publish path.
/// </summary>
/// <remarks>
/// <para>
/// The file system, the path resolver and the per repository lock are the real ones over a
/// temporary <c>DATA_DIR</c>, because most of what publishing promises is about which files exist
/// when: a mocked <see cref="IFileSystem"/> would let a rollback that never deleted anything pass.
/// libalpm and the CLI tools are doubles, since the point of each test is what the service does
/// with what they report.
/// </para>
/// <para>
/// The uploaded bytes are the committed fixture package, so the compression sniffing that names the
/// stored file is exercised for real and the checksums have something to be right about.
/// </para>
/// </remarks>
[TestFixture]
public class PackageServicePublishTests
{
    private string _dataDir = null!;
    private FailingCommitDbContext _dbContext = null!;
    private TestActorAccessor _actors = null!;
    private Mock<ICliToolRunner> _cliRunner = null!;
    private Mock<ILibAlpm> _libAlpm = null!;
    private PackagePathResolver _pathResolver = null!;
    private PackageService _service = null!;

    private User _owner = null!;
    private User _other = null!;
    private PacmanRepository _repository = null!;
    private PacmanRepository _othersPublicRepository = null!;
    private PacmanRepository _othersPrivateRepository = null!;

    private byte[] _fixtureBytes = null!;
    private string _fixtureSha256 = null!;
    private string _fixtureMd5 = null!;

    private string _packageName = PackageFixtures.MinimalPackageName;
    private string _packageVersion = PackageFixtures.MinimalPackageVersion;
    private string _packageArchitecture = PackageFixtures.MinimalPackageArchitecture;

    [SetUp]
    public void SetUp()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), $"pacmanmanager-publish-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataDir);

        var options = new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new FailingCommitDbContext(options);

        _owner = _dbContext.Add(new User { DisplayName = "owner", Email = "owner@test.com" }).Entity;
        _other = _dbContext.Add(new User { DisplayName = "somebody else", Email = "other@test.com" }).Entity;
        _dbContext.SaveChanges();

        _repository = GivenRepository("mine", _owner, isPublic: false);
        _othersPublicRepository = GivenRepository("theirs-public", _other, isPublic: true);
        _othersPrivateRepository = GivenRepository("theirs-private", _other, isPublic: false);
        _dbContext.SaveChanges();

        _fixtureBytes = File.ReadAllBytes(PackageFixtures.MinimalPackagePath);
        _fixtureSha256 = Convert.ToHexStringLower(SHA256.HashData(_fixtureBytes));
        _fixtureMd5 = Convert.ToHexStringLower(MD5.HashData(_fixtureBytes));

        _packageName = PackageFixtures.MinimalPackageName;
        _packageVersion = PackageFixtures.MinimalPackageVersion;
        _packageArchitecture = PackageFixtures.MinimalPackageArchitecture;

        _cliRunner = new Mock<ICliToolRunner>();
        _cliRunner
            .Setup(c => c.RunToolAsync(It.IsAny<ICliTool>(), It.IsAny<ICliOutputHandler>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        _libAlpm = new Mock<ILibAlpm>();
        _libAlpm
            .Setup(a => a.LoadPackageFile(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>()))
            .Returns(() => LoadedPackage());

        var settings = Options.Create(new PacmanConfigSettings { DataDir = _dataDir });
        _pathResolver = new PackagePathResolver(settings);

        _actors = new TestActorAccessor { Actor = Actor.For(_owner) };
        _service = new PackageService(
            _dbContext,
            _actors,
            new RepositoryAccessPolicy(),
            new PackageAccessPolicy(),
            _cliRunner.Object,
            new PhysicalFileSystem(),
            _pathResolver,
            new RepositoryDatabaseLock(),
            new Lazy<ILibAlpm>(() => _libAlpm.Object),
            settings,
            new TestOutputLogger<PackageService>());
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();

        if (Directory.Exists(_dataDir))
        {
            Directory.Delete(_dataDir, recursive: true);
        }
    }

    #region The happy path

    [Test]
    public async Task PublishPackageAsync_ReportsANewPackageAsCreated()
    {
        // Act
        var result = await PublishAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.Created, Is.True, "A package new to its repository is a 201.");
            Assert.That(result.Package.Name, Is.EqualTo(PackageFixtures.MinimalPackageName));
            Assert.That(result.Package.Version, Is.EqualTo(PackageFixtures.MinimalPackageVersion));
            Assert.That(result.Package.RepositoryId, Is.EqualTo(_repository.Id));
            Assert.That(result.Package.Publisher.Id, Is.EqualTo(_owner.Id));
        });
    }

    [Test]
    public async Task PublishPackageAsync_StoresTheMetadataLibalpmReported()
    {
        // Act
        await PublishAsync();

        // Assert
        var stored = await _dbContext.PacmanPackages.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(stored.Description, Is.EqualTo(PackageFixtures.MinimalPackageDescription));
            Assert.That(stored.Base, Is.EqualTo(PackageFixtures.MinimalPackageBase));
            Assert.That(stored.Url, Is.EqualTo(PackageFixtures.MinimalPackageUrl));
            Assert.That(stored.Packager, Is.EqualTo(PackageFixtures.MinimalPackagePackager));
            Assert.That(stored.InstalledSize, Is.EqualTo(PackageFixtures.MinimalPackageInstalledSize));
            Assert.That(stored.Licenses, Is.EqualTo(new[] { PackageFixtures.MinimalPackageLicense }));
            Assert.That(stored.Groups, Is.EqualTo(new[] { PackageFixtures.MinimalPackageGroup }));
            Assert.That(stored.CompressedSize, Is.EqualTo(_fixtureBytes.Length),
                "The compressed size is the length of the file actually stored.");
        });
    }

    [Test]
    public async Task PublishPackageAsync_ComputesBothChecksumsOverTheUploadedBytes()
    {
        // libalpm reports no checksums for a package loaded off disk, and repo-add writes real ones
        // into the same repository's database, so these have to be computed rather than read.
        // Act
        await PublishAsync();

        // Assert
        var stored = await _dbContext.PacmanPackages.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(stored.Sha256Sum, Is.EqualTo(_fixtureSha256));
            Assert.That(stored.Md5Sum, Is.EqualTo(_fixtureMd5));
        });
    }

    [Test]
    public async Task PublishPackageAsync_StoresTheFileUnderADerivedName_AndAddsItToTheDatabase()
    {
        // Act
        var result = await PublishAsync();

        // Assert
        var expectedPath = _pathResolver.GetPackageFilePath(_repository.Id, result!.Package.FileName);
        Assert.Multiple(() =>
        {
            Assert.That(result.Package.FileName, Is.EqualTo(PackageFixtures.MinimalPackageFileName),
                "The stored name comes from the metadata and the sniffed compression.");
            Assert.That(File.Exists(expectedPath), Is.True);
            Assert.That(File.ReadAllBytes(expectedPath), Is.EqualTo(_fixtureBytes));
        });

        VerifyRepoAdd(expectedPath, Times.Once());
    }

    [Test]
    public async Task PublishPackageAsync_TouchesTheRepositorysOwnTimestamp()
    {
        // Arrange
        var before = _repository.UpdatedAt;

        // Act
        await PublishAsync();

        // Assert
        var repository = await _dbContext.PacmanRepositories.SingleAsync(r => r.Id == _repository.Id);
        Assert.That(repository.UpdatedAt, Is.GreaterThan(before), "Its contents changed, so it did too.");
    }

    [Test]
    public async Task PublishPackageAsync_LeavesNothingBehindInTheTemporaryDirectory()
    {
        // Act
        await PublishAsync();

        // Assert
        var tmpDir = Path.Combine(_dataDir, "tmp");
        Assert.That(Directory.EnumerateFileSystemEntries(tmpDir), Is.Empty);
    }

    #endregion

    #region Replacement

    [Test]
    public async Task PublishPackageAsync_ReplacingAPackage_UpdatesItInPlaceAndReportsItAsNotCreated()
    {
        // Arrange
        var first = await PublishAsync();

        // A system actor publishes on somebody else's behalf, which is the only way one repository
        // gets two publishers before a grant table exists.
        _actors.Actor = Actor.SystemFor(_other);
        _packageVersion = PackageFixtures.UpgradePackageVersion;

        // Act
        var second = await PublishAsync();

        // Assert
        var stored = await _dbContext.PacmanPackages.SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(second!.Created, Is.False, "Replacing an existing package is a 200.");
            Assert.That(stored.Id, Is.EqualTo(first!.Package.Id), "A replacement keeps the package's id.");
            Assert.That(stored.CreatedAt, Is.EqualTo(first.Package.CreatedAt),
                "CreatedAt records when the package first appeared here, not when it was last pushed.");
            Assert.That(stored.Version, Is.EqualTo(PackageFixtures.UpgradePackageVersion));
            Assert.That(stored.PublisherId, Is.EqualTo(_other.Id),
                "The publisher is whoever pushed it last.");
            Assert.That(stored.UpdatedAt, Is.GreaterThanOrEqualTo(first.Package.UpdatedAt));
        });
    }

    [Test]
    public async Task PublishPackageAsync_ReplacingAPackage_RemovesTheSupersededFileAfterTheCommit()
    {
        // Arrange
        var first = await PublishAsync();
        var previousPath = _pathResolver.GetPackageFilePath(_repository.Id, first!.Package.FileName);
        _packageVersion = PackageFixtures.UpgradePackageVersion;

        // Act
        var second = await PublishAsync();

        // Assert
        var currentPath = _pathResolver.GetPackageFilePath(_repository.Id, second!.Package.FileName);
        Assert.Multiple(() =>
        {
            Assert.That(currentPath, Is.Not.EqualTo(previousPath));
            Assert.That(File.Exists(currentPath), Is.True);
            Assert.That(File.Exists(previousPath), Is.False, "The superseded file is unreferenced once committed.");
        });
    }

    [Test]
    public async Task PublishPackageAsync_RejectsARepublishOfTheVersionAlreadyThere()
    {
        // The bytes of a published version are bytes a client may already have downloaded and
        // checksummed against the database it synced, so a rebuild arrives under a new version
        // rather than overwriting them.
        // Arrange
        var first = await PublishAsync();
        _cliRunner.Invocations.Clear();

        // Act & Assert
        var thrown = Assert.ThrowsAsync<PackageNotNewerException>(async () => await PublishAsync());

        var storedPath = _pathResolver.GetPackageFilePath(_repository.Id, first!.Package.FileName);
        Assert.Multiple(() =>
        {
            Assert.That(thrown!.PublishedVersion, Is.EqualTo(PackageFixtures.MinimalPackageVersion));
            Assert.That(thrown.OfferedVersion, Is.EqualTo(PackageFixtures.MinimalPackageVersion));
            Assert.That(File.ReadAllBytes(storedPath), Is.EqualTo(_fixtureBytes),
                "The published file is left exactly as it was.");
            Assert.That(_dbContext.PacmanPackages.Single().Version,
                Is.EqualTo(PackageFixtures.MinimalPackageVersion));
        });

        VerifyNoDatabaseToolRan();
    }

    [Test]
    public async Task PublishPackageAsync_RejectsAVersionOlderThanTheOneAlreadyThere()
    {
        // Pacman has no rollback, so a repository never moves backwards either.
        // Arrange
        await PublishAsync();
        _packageVersion = "1.2.3-3";

        // Act & Assert
        var thrown = Assert.ThrowsAsync<PackageNotNewerException>(async () => await PublishAsync());
        Assert.Multiple(() =>
        {
            Assert.That(thrown!.OfferedVersion, Is.EqualTo("1.2.3-3"));
            Assert.That(_dbContext.PacmanPackages.Single().Version,
                Is.EqualTo(PackageFixtures.MinimalPackageVersion));
        });
    }

    [Test]
    public async Task PublishPackageAsync_OrdersVersionsTheWayPacmanDoes()
    {
        // Arrange
        _packageVersion = "1.9-1";
        await PublishAsync();
        _packageVersion = "1.10-1";

        // Act
        var second = await PublishAsync();

        // Assert
        Assert.That(second!.Package.Version, Is.EqualTo("1.10-1"),
            "1.10 is newer than 1.9; comparing the versions as strings would refuse this.");
    }

    [Test]
    public async Task PublishPackageAsync_AcceptsTheSameVersionBuiltForAnotherArchitecture()
    {
        // A build for a different architecture is a different package file rather than a re-push
        // of the same one, so it is allowed to carry the version it was built with.
        // Arrange
        _packageArchitecture = "any";
        await PublishAsync();
        _packageArchitecture = "x86_64";

        // Act
        var second = await PublishAsync();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(second!.Created, Is.False);
            Assert.That(second.Package.Architecture, Is.EqualTo("x86_64"));
            Assert.That(second.Package.Version, Is.EqualTo(PackageFixtures.MinimalPackageVersion));
        });
    }

    #endregion

    #region Validation

    [Test]
    public void PublishPackageAsync_RejectsAPackageBuiltForAnotherArchitecture()
    {
        // Arrange
        _packageArchitecture = "aarch64";

        // Act & Assert
        var thrown = Assert.ThrowsAsync<PackageArchitectureMismatchException>(async () => await PublishAsync());
        Assert.Multiple(() =>
        {
            Assert.That(thrown!.PackageArchitecture, Is.EqualTo("aarch64"));
            Assert.That(thrown.RepositoryArchitecture, Is.EqualTo("x86_64"));
            Assert.That(_dbContext.PacmanPackages.Any(), Is.False, "Nothing is stored for a rejected upload.");
            Assert.That(Directory.Exists(_pathResolver.GetRepositoryDirectory(_repository.Id)), Is.False);
        });

        VerifyNoDatabaseToolRan();
    }

    [Test]
    public async Task PublishPackageAsync_AcceptsAnArchitectureIndependentPackage()
    {
        // Arrange
        _packageArchitecture = "any";

        // Act
        var result = await PublishAsync();

        // Assert
        Assert.That(result!.Package.Architecture, Is.EqualTo("any"),
            "A repository serves its own architecture and 'any'.");
    }

    [Test]
    public void PublishPackageAsync_RejectsAFileThatIsNotInASupportedCompressionFormat()
    {
        // The stored file's extension comes from the sniffed compression, so a file with no
        // recognised magic has no name to be stored under.
        // Act & Assert
        Assert.ThrowsAsync<UnsupportedPackageCompressionException>(
            async () => await PublishAsync(content: "not a package at all"u8.ToArray()));
        Assert.That(_dbContext.PacmanPackages.Any(), Is.False);
    }

    [Test]
    public void PublishPackageAsync_RejectsMetadataThatCouldNotNameAFile()
    {
        // Arrange
        _packageName = "../../etc/passwd";

        // Act & Assert
        Assert.ThrowsAsync<InvalidPackageMetadataException>(async () => await PublishAsync());
        Assert.That(_dbContext.PacmanPackages.Any(), Is.False);
    }

    #endregion

    #region Authorization

    [Test]
    public async Task PublishPackageAsync_ReturnsNull_ForSomebodyElsesPrivateRepository_WithoutReadingTheBody()
    {
        // Arrange
        using var body = new MemoryStream(_fixtureBytes);

        // Act
        var result = await _service.PublishPackageAsync(_othersPrivateRepository.Id, body);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null, "A private repository must be indistinguishable from one that is not there.");
            Assert.That(body.Position, Is.Zero, "Authorization comes before the body is read.");
        });
    }

    [Test]
    public void PublishPackageAsync_Throws_ForSomebodyElsesPublicRepository_WithoutReadingTheBody()
    {
        // Arrange
        using var body = new MemoryStream(_fixtureBytes);

        // Act & Assert
        var thrown = Assert.ThrowsAsync<PackageForbiddenException>(
            async () => await _service.PublishPackageAsync(_othersPublicRepository.Id, body));

        Assert.Multiple(() =>
        {
            Assert.That(thrown!.RepositoryId, Is.EqualTo(_othersPublicRepository.Id));
            Assert.That(body.Position, Is.Zero, "An unauthorized caller does not get to upload megabytes first.");
        });
    }

    [Test]
    public void PublishPackageAsync_Throws_WhenThereIsNoCurrentUser()
    {
        // Arrange
        _actors.Actor = Actor.Anonymous;
        using var body = new MemoryStream(_fixtureBytes);

        // Act & Assert
        Assert.ThrowsAsync<NoCurrentUserException>(
            async () => await _service.PublishPackageAsync(_othersPublicRepository.Id, body));
    }

    [Test]
    public async Task PublishPackageAsync_ReturnsNull_WhenTheRepositoryDoesNotExist()
    {
        // Act
        var result = await PublishAsync(Guid.NewGuid());

        // Assert
        Assert.That(result, Is.Null);
    }

    #endregion

    #region Failure and compensation

    [Test]
    public void PublishPackageAsync_RepoAddFailure_LeavesNoRowAndNoNewFile()
    {
        // Arrange
        GivenRepoAddFails(stdErr: "==> ERROR: could not read the package");

        // Act & Assert
        Assert.ThrowsAsync<RepositoryDatabaseToolException>(async () => await PublishAsync());

        var expectedPath = _pathResolver.GetPackageFilePath(_repository.Id, PackageFixtures.MinimalPackageFileName);
        Assert.Multiple(() =>
        {
            Assert.That(_dbContext.PacmanPackages.Any(), Is.False, "The commit was never reached.");
            Assert.That(File.Exists(expectedPath), Is.False, "The file just written is removed again.");
        });
    }

    [Test]
    public void PublishPackageAsync_RepoAddFailure_OnAReplacement_LeavesThePreviousVersionInPlace()
    {
        // Arrange
        var first = PublishAsync().GetAwaiter().GetResult();
        var previousPath = _pathResolver.GetPackageFilePath(_repository.Id, first!.Package.FileName);
        _packageVersion = PackageFixtures.UpgradePackageVersion;
        _cliRunner.Invocations.Clear();
        GivenRepoAddFails(stdErr: "==> ERROR: could not read the package");

        // Act & Assert
        Assert.ThrowsAsync<RepositoryDatabaseToolException>(async () => await PublishAsync());

        var stored = _dbContext.PacmanPackages.Single();
        Assert.Multiple(() =>
        {
            Assert.That(stored.Version, Is.EqualTo(PackageFixtures.MinimalPackageVersion),
                "The row is unchanged, so it still names the previous file.");
            Assert.That(File.Exists(previousPath), Is.True, "And the previous file is still there for it to name.");
        });
    }

    [Test]
    public void PublishPackageAsync_ALockFileFailure_IsReportedLikeEveryOtherToolFailure()
    {
        // The tools distinguish their reasons only in prose, and a package name may contain the
        // word 'lock', so reading a reason out of the message would be wrong often enough to
        // matter. Every failure means the same thing to the caller: nothing was published.
        // Arrange
        GivenRepoAddFails(stdErr: "==> ERROR: Failed to acquire lockfile: db.lck.");

        // Act & Assert
        var thrown = Assert.ThrowsAsync<RepositoryDatabaseToolException>(async () => await PublishAsync());
        Assert.Multiple(() =>
        {
            Assert.That(thrown!.Tool, Is.EqualTo("repo-add"));
            Assert.That(thrown.StandardError, Does.Contain("lockfile"),
                "The diagnostics travel with the exception, so the reason is not lost.");
            Assert.That(_dbContext.PacmanPackages.Any(), Is.False);
        });
    }

    [Test]
    public void PublishPackageAsync_CommitFailureAfterRepoAdd_RunsTheCompensatingRepoRemove()
    {
        // The database file now advertises a package the rows do not know about, and nothing else
        // would ever notice, so publishing has to undo it explicitly.
        // Arrange
        _dbContext.FailNextCommit = true;

        // Act & Assert
        Assert.ThrowsAsync<CommitFailedException>(async () => await PublishAsync());

        var expectedPath = _pathResolver.GetPackageFilePath(_repository.Id, PackageFixtures.MinimalPackageFileName);
        VerifyRepoRemove(PackageFixtures.MinimalPackageName, Times.Once());
        Assert.Multiple(() =>
        {
            Assert.That(_dbContext.PacmanPackages.Any(), Is.False);
            Assert.That(File.Exists(expectedPath), Is.False, "A new package's file goes with its entry.");
        });
    }

    [Test]
    public void PublishPackageAsync_CommitFailureAfterRepoAdd_OnAReplacement_RestoresThePreviousFile()
    {
        // Arrange
        var first = PublishAsync().GetAwaiter().GetResult();
        var previousPath = _pathResolver.GetPackageFilePath(_repository.Id, first!.Package.FileName);
        _packageVersion = PackageFixtures.UpgradePackageVersion;
        _cliRunner.Invocations.Clear();
        _dbContext.FailNextCommit = true;

        // Act & Assert
        Assert.ThrowsAsync<CommitFailedException>(async () => await PublishAsync());

        var stored = _dbContext.PacmanPackages.Single();
        var orphanPath = _pathResolver.GetPackageFilePath(_repository.Id, PackageFixtures.UpgradePackageFileName);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Version, Is.EqualTo(PackageFixtures.MinimalPackageVersion),
                "The commit failed, so the row still describes the previous version.");
            Assert.That(File.Exists(previousPath), Is.True,
                "The previous file is not deleted until after the commit, precisely so this can restore it.");
            Assert.That(File.Exists(orphanPath), Is.False,
                "And the file the rolled back publish wrote is removed, rather than left for nothing to name.");
        });

        VerifyRepoRemove(PackageFixtures.MinimalPackageName, Times.Once());
        VerifyRepoAdd(previousPath, Times.Once());
    }

    [Test]
    public void PublishPackageAsync_CommitCancelledAfterRepoAdd_StillCompensates()
    {
        // A client that disconnects mid-commit cancels the token the publish is running under, and
        // that is one of the ordinary ways to reach the compensation at all. Compensation that
        // reused the token would abandon itself at its first await, leaving the database file
        // advertising a package no row describes — exactly what it exists to prevent.
        // Arrange
        using var cancellation = new CancellationTokenSource();
        _dbContext.CancelOnCommit = cancellation;

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await PublishAsync(cancellationToken: cancellation.Token));

        var expectedPath = _pathResolver.GetPackageFilePath(_repository.Id, PackageFixtures.MinimalPackageFileName);
        _cliRunner.Verify(
            c => c.RunToolAsync(
                It.Is<ICliTool>(t => t is RepoRemove),
                It.IsAny<ICliOutputHandler>(),
                It.Is<CancellationToken>(t => !t.IsCancellationRequested)),
            Times.Once,
            "The compensating repo-remove runs, and not on the cancelled token.");

        Assert.Multiple(() =>
        {
            Assert.That(_dbContext.PacmanPackages.Any(), Is.False);
            Assert.That(File.Exists(expectedPath), Is.False);
        });
    }

    #endregion

    #region Helpers

    private Task<PublishPackageResult?> PublishAsync(
        byte[]? content = null,
        CancellationToken cancellationToken = default) =>
        PublishAsync(_repository.Id, content, cancellationToken);

    private Task<PublishPackageResult?> PublishAsync(
        Guid repositoryId,
        byte[]? content = null,
        CancellationToken cancellationToken = default)
    {
        var body = new MemoryStream(content ?? _fixtureBytes);
        return _service.PublishPackageAsync(repositoryId, body, cancellationToken);
    }

    private PacmanRepository GivenRepository(string name, User owner, bool isPublic) =>
        _dbContext.Add(new PacmanRepository
        {
            Name = name,
            Architecture = "x86_64",
            IsPublic = isPublic,
            Owner = owner,
            UpdatedAt = DateTimeOffset.UtcNow.AddDays(-1),
        }).Entity;

    /// <summary>
    /// A double for the package libalpm would report for the uploaded file. Every value it returns
    /// is read from a field, so a test can change the version or the architecture between two
    /// publishes.
    /// </summary>
    private IPackage LoadedPackage()
    {
        var package = new Mock<IPackage>();
        package.SetupGet(p => p.Name).Returns(() => _packageName);
        package.SetupGet(p => p.Version).Returns(() => _packageVersion);
        package.SetupGet(p => p.Description).Returns(PackageFixtures.MinimalPackageDescription);
        package.Setup(p => p.GetBase()).Returns(PackageFixtures.MinimalPackageBase);
        package.Setup(p => p.GetUrl()).Returns(PackageFixtures.MinimalPackageUrl);
        package.Setup(p => p.GetArchitecture()).Returns(() => _packageArchitecture);
        package.Setup(p => p.GetPackager()).Returns(PackageFixtures.MinimalPackagePackager);
        package.Setup(p => p.GetInstalledSize()).Returns(PackageFixtures.MinimalPackageInstalledSize);
        package.Setup(p => p.GetDownloadSize()).Returns(_fixtureBytes.Length);
        package.Setup(p => p.GetBuildDate())
            .Returns(DateTimeOffset.FromUnixTimeSeconds(PackageFixtures.MinimalPackageBuildDateUnixSeconds));
        package.Setup(p => p.GetLicenses()).Returns(new List<string> { PackageFixtures.MinimalPackageLicense });
        package.Setup(p => p.GetGroups()).Returns(new List<string> { PackageFixtures.MinimalPackageGroup });

        // AlpmDependency has no public constructor, so the dependency lists are empty here. What
        // publishing does with them is a straight projection; how they are spelled is libalpm's
        // business and is covered by LibAlpmSharp's own tests.
        package.Setup(p => p.GetDependencies()).Returns(new List<AlpmDependency>());
        package.Setup(p => p.GetOptionalDependencies()).Returns(new List<AlpmDependency>());
        package.Setup(p => p.GetMakeDepends()).Returns(new List<AlpmDependency>());
        package.Setup(p => p.GetCheckDepends()).Returns(new List<AlpmDependency>());
        package.Setup(p => p.GetProvides()).Returns(new List<AlpmDependency>());
        package.Setup(p => p.GetConflicts()).Returns(new List<AlpmDependency>());
        package.Setup(p => p.GetReplaces()).Returns(new List<AlpmDependency>());
        return package.Object;
    }

    private void GivenRepoAddFails(string stdErr)
    {
        _cliRunner
            .Setup(c => c.RunToolAsync(It.IsAny<RepoAdd>(), It.IsAny<ICliOutputHandler>(),
                It.IsAny<CancellationToken>()))
            .Returns<ICliTool, ICliOutputHandler, CancellationToken>(async (tool, handler, _) =>
            {
                await WriteToolOutputAsync(tool, handler, stdErr);
                return 1;
            });
    }

    /// <summary>
    /// Feeds a tool's diagnostics through the handler the service passed in, the way the real runner
    /// would, so that the service sees the message it has to classify.
    /// </summary>
    private static async Task WriteToolOutputAsync(ICliTool tool, ICliOutputHandler handler, string stdErr)
    {
        using var stdOutReader = new StreamReader(new MemoryStream());
        using var stdErrReader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(stdErr)));
        await handler.HandleOutputAsync(tool, stdOutReader, stdErrReader);
    }

    private void VerifyRepoAdd(string packageFilePath, Times times) =>
        _cliRunner.Verify(
            c => c.RunToolAsync(
                It.Is<ICliTool>(t => t is RepoAdd && t.Arguments.Contains(packageFilePath)),
                It.IsAny<ICliOutputHandler>(),
                It.IsAny<CancellationToken>()),
            times);

    private void VerifyRepoRemove(string packageName, Times times) =>
        _cliRunner.Verify(
            c => c.RunToolAsync(
                It.Is<ICliTool>(t => t is RepoRemove && t.Arguments.Contains(packageName)),
                It.IsAny<ICliOutputHandler>(),
                It.IsAny<CancellationToken>()),
            times);

    private void VerifyNoDatabaseToolRan() =>
        _cliRunner.Verify(
            c => c.RunToolAsync(It.IsAny<ICliTool>(), It.IsAny<ICliOutputHandler>(), It.IsAny<CancellationToken>()),
            Times.Never);

    /// <summary>
    /// The failure the compensation cases need, and the one that is otherwise unreachable: a commit
    /// that fails after the side effects have already happened.
    /// </summary>
    private sealed class CommitFailedException() : Exception("The commit failed, as this test asked it to.");

    /// <summary>
    /// A context whose asynchronous commit can be made to fail once, so that a test can reach the
    /// window between a successful <c>repo-add</c> and a persisted row.
    /// </summary>
    private sealed class FailingCommitDbContext(DbContextOptions<PacmanManagerDbContext> options)
        : PacmanManagerDbContext(options)
    {
        public bool FailNextCommit { get; set; }

        /// <summary>
        /// Cancelled as the commit fails, which is what a client disconnecting mid-commit does to
        /// the token the publish is running under.
        /// </summary>
        public CancellationTokenSource? CancelOnCommit { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (CancelOnCommit is not null)
            {
                CancelOnCommit.Cancel();
                throw new OperationCanceledException(CancelOnCommit.Token);
            }

            if (FailNextCommit)
            {
                FailNextCommit = false;
                throw new CommitFailedException();
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    #endregion
}
