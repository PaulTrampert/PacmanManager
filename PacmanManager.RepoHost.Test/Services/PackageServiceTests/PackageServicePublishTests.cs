using PacmanManager.Authentication;
using System.Security.Cryptography;
using LibAlpmSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;
using PacmanManager.TestUtils;
using PacmanManager.Packages;
using PacmanManager.Packages.Infrastructure;
using PacmanManager.Packages.Exceptions;
using PacmanManager.Packages.LibAlpm;
using PacmanManager.Packages.Authorization;

namespace PacmanManager.RepoHost.Test.Services.PackageServiceTests;

/// <summary>
/// Tests for <c>PackageService</c>'s publish path: authorizing the change and receiving the upload.
/// What publishing then does is <c>PackagePublisherPublishTests</c>' subject, in
/// <c>PacmanManager.Packages.Test</c>.
/// </summary>
/// <remarks>
/// <para>
/// The file system, the path resolver and the publisher are the real ones over a temporary
/// <c>DATA_DIR</c>, so that what is left in the temporary directory is what is actually on disk.
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
    private PacmanManagerDbContext _dbContext = null!;
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
        _dbContext = new PacmanManagerDbContext(options);

        _owner = _dbContext.Add(new User { DisplayName = "owner", NormalizedDisplayName = "owner", Email = "owner@test.com" }).Entity;
        _other = _dbContext.Add(new User { DisplayName = "somebody else", NormalizedDisplayName = "somebody else", Email = "other@test.com" }).Entity;
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

        _actors = new TestActorAccessor { Actor = Actor.For(_owner, ActorScope.Unrestricted) };
        // The publisher is the real one, so that what a caller gets back is what a publish produces;
        // PackagePublisherPublishTests covers what it does with the file, the databases and the rows.
        var publisher = new PackagePublisher(
            _dbContext,
            _cliRunner.Object,
            new PhysicalFileSystem(),
            _pathResolver,
            Mock.Of<IRepositoryDatabaseLock>(l =>
                l.AcquireAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult(Mock.Of<IDisposable>())),
            new PackageFileReader(new Lazy<ILibAlpm>(() => _libAlpm.Object)),
            new TestOutputLogger<PackagePublisher>());
        _service = new PackageService(
            _dbContext,
            _actors,
            new PackageAccessPolicy(new RepositoryAccessPolicy()),
            publisher,
            new PhysicalFileSystem(),
            _pathResolver,
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
    public async Task PublishPackageAsync_ComputesBothChecksumsOverTheUploadedBytes()
    {
        // libalpm reports no checksums for a package loaded off disk, so both have to be computed
        // over the upload rather than read back. Pacman 7's repo-add records only %SHA256SUM%, so
        // this is the only place the stored MD5 is held to the bytes it was computed from.
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
    public async Task PublishPackageAsync_LeavesNothingBehindInTheTemporaryDirectory()
    {
        // Act
        await PublishAsync();

        // Assert
        var tmpDir = Path.Combine(_dataDir, "tmp");
        Assert.That(Directory.EnumerateFileSystemEntries(tmpDir), Is.Empty);
    }

    [Test]
    public async Task PublishPackageAsync_LeavesNothingBehindInTheTemporaryDirectory_WhenThePublishIsRejected()
    {
        // Arrange
        _packageArchitecture = "aarch64";

        // Act & Assert
        await Assert.ThrowsAsync<PackageArchitectureMismatchException>(async () => await PublishAsync());
        var tmpDir = Path.Combine(_dataDir, "tmp");
        Assert.That(Directory.EnumerateFileSystemEntries(tmpDir), Is.Empty,
            "The upload is discarded whether or not the publisher took it.");
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
    public async Task PublishPackageAsync_Throws_ForSomebodyElsesPublicRepository_WithoutReadingTheBody()
    {
        // Arrange
        using var body = new MemoryStream(_fixtureBytes);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<PackageForbiddenException>(
            async () => await _service.PublishPackageAsync(_othersPublicRepository.Id, body));

        Assert.Multiple(() =>
        {
            Assert.That(thrown!.RepositoryId, Is.EqualTo(_othersPublicRepository.Id));
            Assert.That(body.Position, Is.Zero, "An unauthorized caller does not get to upload megabytes first.");
        });
    }

    [Test]
    public async Task PublishPackageAsync_Throws_WhenThereIsNoCurrentUser()
    {
        // Arrange
        _actors.Actor = Actor.Anonymous;
        using var body = new MemoryStream(_fixtureBytes);

        // Act & Assert
        await Assert.ThrowsAsync<NoCurrentUserException>(
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

    private PacmanRepository GivenRepository(
        string name,
        User owner,
        bool isPublic,
        IEnumerable<string>? architectures = null) =>
        _dbContext.Add(new PacmanRepository
        {
            Name = name,
            SupportedArchitectures = architectures?.ToList() ?? [Architectures.X86_64],
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

    #endregion
}
