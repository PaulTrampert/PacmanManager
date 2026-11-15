using LibAlpmSharp;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using PacmanManager.Authentication;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Services;
using PacmanManager.TestUtils;
using PacmanManager.Packages.CliTools;
using PacmanManager.Packages;
using PacmanManager.Packages.Infrastructure;
using PacmanManager.Packages.LibAlpm;
using PacmanManager.Packages.Authorization;

namespace PacmanManager.RepoHost.Test.Services.PackageServiceTests;

/// <summary>
/// Tests for <c>PackageService</c>'s delete path: resolving the package and authorizing the change.
/// How the removal then happens is <c>PackagePublisherRemoveTests</c>' subject, in
/// <c>PacmanManager.Packages.Test</c>.
/// </summary>
/// <remarks>
/// <para>
/// The file system, the path resolver and the publisher are the real ones over a temporary
/// <c>DATA_DIR</c>, as in <see cref="PackageServicePublishTests"/>, so that a delete the service
/// allows really happens and one it refuses really does not. The CLI tools are doubles.
/// </para>
/// <para>
/// Packages are seeded directly rather than published through the service. Delete resolves a row and
/// a file, so arranging them is all publishing would contribute, and libalpm has nothing to say
/// about a package that is already stored.
/// </para>
/// </remarks>
[TestFixture]
public class PackageServiceDeleteTests
{
    private const string PackageName = "minimal-package";
    private const string PackageFileName = "minimal-package-1.2.3-1-x86_64.pkg.tar.zst";

    private string _dataDir = null!;
    private PacmanManagerDbContext _dbContext = null!;
    private TestActorAccessor _actors = null!;
    private Mock<ICliToolRunner> _cliRunner = null!;
    private PackagePathResolver _pathResolver = null!;
    private PackageService _service = null!;

    private User _owner = null!;
    private User _other = null!;
    private PacmanRepository _repository = null!;
    private PacmanRepository _othersPublicRepository = null!;
    private PacmanRepository _othersPrivateRepository = null!;

    [SetUp]
    public void SetUp()
    {
        _dataDir = Path.Combine(Path.GetTempPath(), $"pacmanmanager-delete-{Guid.NewGuid():N}");
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

        _cliRunner = new Mock<ICliToolRunner>();
        _cliRunner
            .Setup(c => c.RunToolAsync(It.IsAny<ICliTool>(), It.IsAny<ICliOutputHandler>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);

        var settings = Options.Create(new PacmanConfigSettings { DataDir = _dataDir });
        _pathResolver = new PackagePathResolver(settings);

        _actors = new TestActorAccessor { Actor = Actor.For(_owner, ActorScope.Unrestricted) };
        // The publisher is the real one, so that an authorized delete really happens;
        // PackagePublisherRemoveTests covers how it removes the file, the entries and the row.
        var publisher = new PackagePublisher(
            _dbContext,
            _cliRunner.Object,
            new PhysicalFileSystem(),
            _pathResolver,
            Mock.Of<IRepositoryDatabaseLock>(l =>
                l.AcquireAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()) == Task.FromResult(Mock.Of<IDisposable>())),
            new PackageFileReader(new Lazy<ILibAlpm>(Mock.Of<ILibAlpm>)),
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
    public async Task DeletePackageAsync_ById_RemovesTheRow_TheDatabaseEntryAndTheFile()
    {
        // Arrange
        var package = GivenPublishedPackage(_repository, publisher: _owner);
        var filePath = _pathResolver.GetPackageFilePath(_repository.Id, package.FileName);

        // Act
        var deleted = await _service.DeletePackageAsync(package.Id);

        // Assert
        VerifyRepoRemove(PackageName, Times.Once());
        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(_dbContext.PacmanPackages.Any(), Is.False, "The row is gone.");
            Assert.That(File.Exists(filePath), Is.False, "And so is the file nothing names any more.");
        });
    }

    [Test]
    public async Task DeletePackageAsync_ByName_DeletesTheSamePackageTheIdFormWould()
    {
        // The natural key is what a build script knows: it has just published 'minimal-package' to
        // a repository and has never seen a package id.
        // Arrange
        var package = GivenPublishedPackage(_repository, publisher: _owner);
        var filePath = _pathResolver.GetPackageFilePath(_repository.Id, package.FileName);

        // Act
        var deleted = await _service.DeletePackageAsync(_repository.Id, PackageName);

        // Assert
        VerifyRepoRemove(PackageName, Times.Once());
        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(_dbContext.PacmanPackages.Any(), Is.False);
            Assert.That(File.Exists(filePath), Is.False);
        });
    }

    #endregion

    #region Authorization

    [Test]
    public async Task DeletePackageAsync_LetsTheOwnerDeleteAPackageSomebodyElsePublished()
    {
        // Publish is held over the repository, not over the package. PublisherId records who pushed
        // it last and confers nothing.
        // Arrange
        var package = GivenPublishedPackage(_repository, publisher: _other);

        // Act
        var deleted = await _service.DeletePackageAsync(package.Id);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(_dbContext.PacmanPackages.Any(), Is.False);
        });
    }

    [Test]
    public async Task DeletePackageAsync_Throws_ForAPackageInSomebodyElsesPublicRepository()
    {
        // The repository is public, so its existence is already known and refusing the delete leaks
        // nothing.
        // Arrange
        var package = GivenPublishedPackage(_othersPublicRepository, publisher: _other);
        var filePath = _pathResolver.GetPackageFilePath(_othersPublicRepository.Id, package.FileName);

        // Act & Assert
        var thrown = await Assert.ThrowsAsync<PackageForbiddenException>(
            async () => await _service.DeletePackageAsync(package.Id));

        Assert.Multiple(() =>
        {
            Assert.That(thrown!.RepositoryId, Is.EqualTo(_othersPublicRepository.Id));
            Assert.That(_dbContext.PacmanPackages.Any(), Is.True);
            Assert.That(File.Exists(filePath), Is.True);
        });

        VerifyNoDatabaseToolRan();
    }

    [Test]
    public async Task DeletePackageAsync_ReportsAPackageInSomebodyElsesPrivateRepositoryAsMissing()
    {
        // A private repository has to stay indistinguishable from one that is not there, so this is
        // a 404 rather than the 403 the public case gets.
        // Arrange
        var package = GivenPublishedPackage(_othersPrivateRepository, publisher: _other);

        // Act
        var deleted = await _service.DeletePackageAsync(package.Id);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.False);
            Assert.That(_dbContext.PacmanPackages.Any(), Is.True, "Nothing was deleted.");
        });

        VerifyNoDatabaseToolRan();
    }

    [Test]
    public async Task DeletePackageAsync_Throws_WhenThereIsNoCurrentUser()
    {
        // Arrange
        var package = GivenPublishedPackage(_othersPublicRepository, publisher: _other);
        _actors.Actor = Actor.Anonymous;

        // Act & Assert
        await Assert.ThrowsAsync<NoCurrentUserException>(async () => await _service.DeletePackageAsync(package.Id));
        Assert.That(_dbContext.PacmanPackages.Any(), Is.True);
    }

    [Test]
    public async Task DeletePackageAsync_ReportsAPackageThatDoesNotExistAsMissing()
    {
        // Act
        var deleted = await _service.DeletePackageAsync(Guid.NewGuid());

        // Assert
        Assert.That(deleted, Is.False);
        VerifyNoDatabaseToolRan();
    }

    [Test]
    public async Task DeletePackageAsync_ReportsAPackageThatWasAlreadyDeletedAsMissing()
    {
        // Arrange
        var package = GivenPublishedPackage(_repository, publisher: _owner);
        await _service.DeletePackageAsync(package.Id);

        // Act
        var deletedAgain = await _service.DeletePackageAsync(package.Id);

        // Assert
        Assert.That(deletedAgain, Is.False, "A second delete cannot half-succeed; it is simply a 404.");
    }

    [Test]
    public async Task DeletePackageAsync_ByName_ReportsAPackageInAnotherRepositoryAsMissing()
    {
        // Arrange
        GivenPublishedPackage(_repository, publisher: _owner);
        var otherRepository = GivenRepository("mine-too", _owner, isPublic: false);
        _dbContext.SaveChanges();

        // Act
        var deleted = await _service.DeletePackageAsync(otherRepository.Id, PackageName);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.False);
            Assert.That(_dbContext.PacmanPackages.Any(), Is.True);
        });
    }

    #endregion

    #region Helpers

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
    /// Seeds a package that has already been published: a committed row and the file it names.
    /// </summary>
    private PacmanPackage GivenPublishedPackage(
        PacmanRepository repository,
        User publisher,
        string? name = null,
        string architecture = Architectures.X86_64)
    {
        var packageName = name ?? PackageName;
        var fileName = $"{packageName}-1.2.3-1-{architecture}.pkg.tar.zst";

        var package = _dbContext.Add(new PacmanPackage
        {
            RepositoryId = repository.Id,
            Repository = repository,
            PublisherId = publisher.Id,
            Publisher = publisher,
            Name = packageName,
            Version = "1.2.3-1",
            Architecture = architecture,
            FileName = fileName,
            Sha256Sum = new string('a', 64),
            Md5Sum = new string('b', 32),
        }).Entity;
        _dbContext.SaveChanges();

        Directory.CreateDirectory(_pathResolver.GetRepositoryDirectory(repository.Id));
        File.WriteAllBytes(_pathResolver.GetPackageFilePath(repository.Id, fileName), "a package"u8.ToArray());

        return package;
    }

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

    #endregion
}
