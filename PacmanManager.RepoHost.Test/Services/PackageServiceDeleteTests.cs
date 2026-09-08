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
using PacmanManager.RepoHost.Services;
using PacmanManager.RepoHost.Startup.LibAlpm;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Tests for <c>PackageService</c>'s delete path.
/// </summary>
/// <remarks>
/// <para>
/// The file system, the path resolver and the per repository lock are the real ones over a
/// temporary <c>DATA_DIR</c>, as in <see cref="PackageServicePublishTests"/>: most of what deleting
/// promises is about which files exist when, and a mocked <see cref="IFileSystem"/> would let a
/// compensation that restored nothing pass. The CLI tools are doubles, since the point of each test
/// is what the service does with what they report.
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
    private FailingCommitDbContext _dbContext = null!;
    private TestActorAccessor _actors = null!;
    private Mock<ICliToolRunner> _cliRunner = null!;
    private FaultInjectingFileSystem _fileSystem = null!;
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
        _dbContext = new FailingCommitDbContext(options);

        _owner = _dbContext.Add(new User { DisplayName = "owner", Email = "owner@test.com" }).Entity;
        _other = _dbContext.Add(new User { DisplayName = "somebody else", Email = "other@test.com" }).Entity;
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
        _fileSystem = new FaultInjectingFileSystem(new PhysicalFileSystem());

        _actors = new TestActorAccessor { Actor = Actor.For(_owner) };
        _service = new PackageService(
            _dbContext,
            _actors,
            new RepositoryAccessPolicy(),
            new PackageAccessPolicy(),
            _cliRunner.Object,
            _fileSystem,
            _pathResolver,
            new RepositoryDatabaseLock(),
            new Lazy<ILibAlpm>(Mock.Of<ILibAlpm>),
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

    [Test]
    public async Task DeletePackageAsync_TouchesTheRepositorysOwnTimestamp()
    {
        // Arrange
        var package = GivenPublishedPackage(_repository, publisher: _owner);
        var before = _repository.UpdatedAt;

        // Act
        await _service.DeletePackageAsync(package.Id);

        // Assert
        var repository = await _dbContext.PacmanRepositories.SingleAsync(r => r.Id == _repository.Id);
        Assert.That(repository.UpdatedAt, Is.GreaterThan(before), "Its contents changed, so it did too.");
    }

    [Test]
    public async Task DeletePackageAsync_LeavesTheOtherPackagesInTheRepositoryAlone()
    {
        // Arrange
        var package = GivenPublishedPackage(_repository, publisher: _owner);
        var bystander = GivenPublishedPackage(_repository, publisher: _owner, name: "another-package");
        var bystanderPath = _pathResolver.GetPackageFilePath(_repository.Id, bystander.FileName);

        // Act
        await _service.DeletePackageAsync(package.Id);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(_dbContext.PacmanPackages.Single().Id, Is.EqualTo(bystander.Id));
            Assert.That(File.Exists(bystanderPath), Is.True);
        });

        VerifyRepoRemove("another-package", Times.Never());
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
    public void DeletePackageAsync_Throws_ForAPackageInSomebodyElsesPublicRepository()
    {
        // The repository is public, so its existence is already known and refusing the delete leaks
        // nothing.
        // Arrange
        var package = GivenPublishedPackage(_othersPublicRepository, publisher: _other);
        var filePath = _pathResolver.GetPackageFilePath(_othersPublicRepository.Id, package.FileName);

        // Act & Assert
        var thrown = Assert.ThrowsAsync<PackageForbiddenException>(
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
    public void DeletePackageAsync_Throws_WhenThereIsNoCurrentUser()
    {
        // Arrange
        var package = GivenPublishedPackage(_othersPublicRepository, publisher: _other);
        _actors.Actor = Actor.Anonymous;

        // Act & Assert
        Assert.ThrowsAsync<NoCurrentUserException>(async () => await _service.DeletePackageAsync(package.Id));
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

    #region Failure and compensation

    [Test]
    public void DeletePackageAsync_RepoRemoveFailure_LeavesTheRowAndTheFileIntact()
    {
        // The side effect comes first precisely so that this is the failure: the package is still
        // listed and still installable, which is the recoverable state. Committing first would
        // delete a row whose entry is still in the database a pacman client syncs.
        // Arrange
        var package = GivenPublishedPackage(_repository, publisher: _owner);
        var filePath = _pathResolver.GetPackageFilePath(_repository.Id, package.FileName);
        GivenRepoRemoveFails(stdErr: "==> ERROR: could not rewrite the database");

        // Act & Assert
        var thrown = Assert.ThrowsAsync<CliToolFailedException>(
            async () => await _service.DeletePackageAsync(package.Id));

        Assert.Multiple(() =>
        {
            Assert.That(thrown!.Tool, Is.EqualTo("repo-remove"));
            Assert.That(_dbContext.PacmanPackages.Any(), Is.True, "The commit was never reached.");
            Assert.That(File.Exists(filePath), Is.True, "And the file the row names is still there.");
        });
    }

    [Test]
    public void DeletePackageAsync_ALockFileFailure_IsReportedLikeEveryOtherToolFailure()
    {
        // The tools distinguish their reasons only in prose and there is no exit code for a lock
        // file, so reading a reason out of the message would be wrong often enough to matter. Every
        // failure means the same thing to the caller: nothing was deleted.
        // Arrange
        var package = GivenPublishedPackage(_repository, publisher: _owner);
        GivenRepoRemoveFails(stdErr: "==> ERROR: Failed to acquire lockfile: db.lck.");

        // Act & Assert
        var thrown = Assert.ThrowsAsync<CliToolFailedException>(
            async () => await _service.DeletePackageAsync(package.Id));

        Assert.Multiple(() =>
        {
            Assert.That(thrown!.Tool, Is.EqualTo("repo-remove"));
            Assert.That(thrown.Diagnostics, Does.Contain("lockfile"),
                "The diagnostics travel with the exception, so the reason is not lost.");
            Assert.That(_dbContext.PacmanPackages.Any(), Is.True);
        });
    }

    [Test]
    public void DeletePackageAsync_CommitFailureAfterRepoRemove_RunsTheCompensatingRepoAdd()
    {
        // The database file no longer advertises a package the rows still describe, and nothing else
        // would ever notice, so deleting has to undo it explicitly.
        // Arrange
        var package = GivenPublishedPackage(_repository, publisher: _owner);
        var filePath = _pathResolver.GetPackageFilePath(_repository.Id, package.FileName);
        _dbContext.FailNextCommit = true;

        // Act & Assert
        Assert.ThrowsAsync<CommitFailedException>(async () => await _service.DeletePackageAsync(package.Id));

        VerifyRepoRemove(PackageName, Times.Once());
        VerifyRepoAdd(filePath, Times.Once());
        Assert.Multiple(() =>
        {
            Assert.That(_dbContext.PacmanPackages.Any(), Is.True, "The row survived the failed commit.");
            Assert.That(File.Exists(filePath), Is.True,
                "The file is not deleted until after the commit, precisely so this can restore its entry.");
        });
    }

    [Test]
    public void DeletePackageAsync_CommitCancelledAfterRepoRemove_StillCompensates()
    {
        // A client that disconnects mid-commit cancels the token the delete is running under, and
        // that is one of the ordinary ways to reach the compensation at all. Compensation that
        // reused the token would abandon itself at its first await, leaving the database file
        // missing a package the rows still describe.
        // Arrange
        var package = GivenPublishedPackage(_repository, publisher: _owner);
        using var cancellation = new CancellationTokenSource();
        _dbContext.CancelOnCommit = cancellation;

        // Act & Assert
        Assert.ThrowsAsync<OperationCanceledException>(
            async () => await _service.DeletePackageAsync(package.Id, cancellation.Token));

        _cliRunner.Verify(
            c => c.RunToolAsync(
                It.Is<ICliTool>(t => t is RepoAdd),
                It.IsAny<ICliOutputHandler>(),
                It.Is<CancellationToken>(t => !t.IsCancellationRequested)),
            Times.Once,
            "The compensating repo-add runs, and not on the cancelled token.");

        Assert.That(_dbContext.PacmanPackages.Any(), Is.True);
    }

    [Test]
    public async Task DeletePackageAsync_AFileThatCannotBeRemoved_DoesNotFailTheRequest()
    {
        // The row is the source of truth and it is already gone, so a file left behind is an inert
        // orphan. Failing here would report a delete that did happen as one that did not.
        // Arrange
        var package = GivenPublishedPackage(_repository, publisher: _owner);
        var filePath = _pathResolver.GetPackageFilePath(_repository.Id, package.FileName);
        _fileSystem.FailDeleteOf = filePath;

        // Act
        var deleted = await _service.DeletePackageAsync(package.Id);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(deleted, Is.True);
            Assert.That(_dbContext.PacmanPackages.Any(), Is.False, "The row is gone, which is what was asked.");
            Assert.That(File.Exists(filePath), Is.True, "The orphan is left behind, and logged at warning.");
        });
    }

    #endregion

    #region Helpers

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
    /// Seeds a package that has already been published: a committed row and the file it names.
    /// </summary>
    private PacmanPackage GivenPublishedPackage(PacmanRepository repository, User publisher, string? name = null)
    {
        var packageName = name ?? PackageName;
        var fileName = name is null ? PackageFileName : $"{name}-1.2.3-1-x86_64.pkg.tar.zst";

        var package = _dbContext.Add(new PacmanPackage
        {
            RepositoryId = repository.Id,
            Repository = repository,
            PublisherId = publisher.Id,
            Publisher = publisher,
            Name = packageName,
            Version = "1.2.3-1",
            Architecture = "x86_64",
            FileName = fileName,
            Sha256Sum = new string('a', 64),
            Md5Sum = new string('b', 32),
        }).Entity;
        _dbContext.SaveChanges();

        Directory.CreateDirectory(_pathResolver.GetRepositoryDirectory(repository.Id));
        File.WriteAllBytes(_pathResolver.GetPackageFilePath(repository.Id, fileName), "a package"u8.ToArray());

        return package;
    }

    private void GivenRepoRemoveFails(string stdErr) =>
        _cliRunner
            .Setup(c => c.RunToolAsync(It.IsAny<RepoRemove>(), It.IsAny<ICliOutputHandler>(),
                It.IsAny<CancellationToken>()))
            .Returns<ICliTool, ICliOutputHandler, CancellationToken>(async (tool, handler, _) =>
            {
                await WriteToolOutputAsync(tool, handler, stdErr);
                return 1;
            });

    /// <summary>
    /// Feeds a tool's diagnostics through the handler the service passed in, the way the real runner
    /// would, so that the exception carries the message an operator would see.
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
    /// that fails after the side effect has already happened.
    /// </summary>
    private sealed class CommitFailedException() : Exception("The commit failed, as this test asked it to.");

    /// <summary>
    /// A context whose asynchronous commit can be made to fail once, so that a test can reach the
    /// window between a successful <c>repo-remove</c> and a removed row. It mirrors the double
    /// <see cref="PackageServicePublishTests"/> keeps for the same window on the other side of the
    /// commit.
    /// </summary>
    private sealed class FailingCommitDbContext(DbContextOptions<PacmanManagerDbContext> options)
        : PacmanManagerDbContext(options)
    {
        public bool FailNextCommit { get; set; }

        /// <summary>
        /// Cancelled as the commit fails, which is what a client disconnecting mid-commit does to
        /// the token the delete is running under.
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

    /// <summary>
    /// The real file system, with one file made undeletable. Deleting the package file is the only
    /// step of a delete that is allowed to fail, and nothing but a real failure proves it.
    /// </summary>
    private sealed class FaultInjectingFileSystem(IFileSystem inner) : IFileSystem
    {
        /// <summary>The one path whose deletion throws. Null leaves every delete alone.</summary>
        public string? FailDeleteOf { get; set; }

        public bool Exists(string path) => inner.Exists(path);

        public bool DirectoryExists(string path) => inner.DirectoryExists(path);

        public void Delete(string path)
        {
            if (FailDeleteOf is not null && string.Equals(path, FailDeleteOf, StringComparison.Ordinal))
            {
                throw new IOException($"'{path}' cannot be deleted, as this test asked.");
            }

            inner.Delete(path);
        }

        public Stream OpenRead(string path) => inner.OpenRead(path);

        public Stream OpenWrite(string path) => inner.OpenWrite(path);

        public void Move(string sourcePath, string destinationPath, bool overwrite = false) =>
            inner.Move(sourcePath, destinationPath, overwrite);

        public void CreateDirectory(string path) => inner.CreateDirectory(path);
    }

    #endregion
}
