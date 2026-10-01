using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using PacmanManager.CliTools;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Config;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;
using PacmanManager.RepoHost.Startup.LibAlpm;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services.PacmanRepoServiceTests;

/// <summary>
/// Resolution of <c>/pacman/{repoName}/{repoArch}/{fileName}</c> paths. The service is composed with
/// a real <see cref="RepositoryService"/> over the in-memory provider and a real
/// <see cref="PackagePathResolver"/>, so that visibility is decided by the rules that decide it in
/// production; only the disk is mocked.
/// </summary>
[TestFixture]
public class PacmanRepoServiceTests
{
    private const string DataDir = "/data";
    private const string RepoName = "myrepo";
    private const string PackageFileName = "my-tool-1.4.2-1-x86_64.pkg.tar.zst";
    private const string AnyPackageFileName = "my-docs-1.0-1-any.pkg.tar.zst";

    private const string RepoIdText = "0199a2c0-0000-7000-8000-0000000000aa";

    private static readonly Guid RepoId = Guid.Parse(RepoIdText);
    private static readonly DateTimeOffset LastModified = new(2024, 7, 28, 12, 34, 56, TimeSpan.Zero);

    private static readonly string RepoDir = $"{DataDir}/repositories/{RepoId}";
    private static readonly string SyncDbPath = $"{RepoDir}/db/x86_64/{RepoId}.db.tar.gz";
    private static readonly string FilesDbPath = $"{RepoDir}/db/x86_64/{RepoId}.files.tar.gz";

    private static readonly DateTimeOffset Now = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private PacmanManagerDbContext _dbContext = null!;
    private TestActorAccessor _actors = null!;
    private TestTimeProvider _clock = null!;
    private RepositoryService _repositories = null!;
    private Mock<IFileSystem> _fileSystem = null!;
    private PacmanRepoService _subject = null!;
    private User _owner = null!;
    private User _otherUser = null!;

    [SetUp]
    public void SetUp()
    {
        _dbContext = new PacmanManagerDbContext(new DbContextOptionsBuilder<PacmanManagerDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
        _owner = _dbContext.Add(new User
        {
            DisplayName = "owner", NormalizedDisplayName = "owner", Email = "owner@test.com"
        }).Entity;
        _otherUser = _dbContext.Add(new User
        {
            DisplayName = "somebody else", NormalizedDisplayName = "somebody else", Email = "other@test.com"
        }).Entity;
        _dbContext.SaveChanges();

        _actors = new TestActorAccessor { Actor = Actor.For(_owner, ActorScope.Unrestricted) };
        _fileSystem = new Mock<IFileSystem>();
        _clock = new TestTimeProvider(Now);

        var pathResolver = new PackagePathResolver(Options.Create(new PacmanConfigSettings { DataDir = DataDir }));
        _repositories = new RepositoryService(
            _dbContext,
            new Mock<ICliToolRunner>().Object,
            _actors,
            new RepositoryAccessPolicy(),
            new TestOutputLogger<RepositoryService>(),
            _fileSystem.Object,
            pathResolver,
            Options.Create(new RepositoryRenameConfig()),
            _clock);

        _subject = new PacmanRepoService(_repositories, pathResolver, _fileSystem.Object);
    }

    [TearDown]
    public void TearDown()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    #region The file-name table

    [TestCase($"{RepoName}.db", TestName = "repo.db is the sync database")]
    [TestCase($"{RepoName}.db.tar.gz", TestName = "repo.db.tar.gz is the sync database")]
    public async Task ResolveAsync_ServesTheSyncDatabase(string fileName)
    {
        await GivenRepositoryAsync();
        var content = GivenFileOnDisk(SyncDbPath);

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, fileName);

        Assert.That(result?.File, Is.EqualTo(new RepositoryFile(content, LastModified)));
    }

    [TestCase($"{RepoName}.files", TestName = "repo.files is the files database")]
    [TestCase($"{RepoName}.files.tar.gz", TestName = "repo.files.tar.gz is the files database")]
    public async Task ResolveAsync_ServesTheFilesDatabase(string fileName)
    {
        await GivenRepositoryAsync();
        var content = GivenFileOnDisk(FilesDbPath);

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, fileName);

        Assert.That(result?.File, Is.EqualTo(new RepositoryFile(content, LastModified)));
    }

    [Test]
    public async Task ResolveAsync_ReadsTheRequestedArchitecturesDatabase()
    {
        await GivenRepositoryAsync(architectures: [Architectures.X86_64, "aarch64"]);
        var content = GivenFileOnDisk($"{RepoDir}/db/aarch64/{RepoId}.db.tar.gz");

        var result = await _subject.ResolveAsync(RepoName, "aarch64", $"{RepoName}.db");

        Assert.That(result?.File?.Content, Is.SameAs(content));
    }

    [Test]
    public async Task ResolveAsync_ServesAPackageFileStraightFromTheRepositorysDirectory()
    {
        await GivenRepositoryAsync();
        var content = GivenFileOnDisk($"{RepoDir}/{PackageFileName}");

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, PackageFileName);

        Assert.That(result?.File, Is.EqualTo(new RepositoryFile(content, LastModified)));
    }

    [TestCase("readme.txt", TestName = "an unrecognised name")]
    [TestCase("index.html", TestName = "a directory index")]
    [TestCase($"{RepoName}", TestName = "the bare repository name")]
    [TestCase($"{RepoName}.db.sig", TestName = "a sync database signature")]
    [TestCase($"{RepoName}.files.sig", TestName = "a files database signature")]
    [TestCase($"{PackageFileName}.sig", TestName = "a package signature")]
    [TestCase($"{RepoName}.db.tar.gz.old", TestName = "a database backup")]
    [TestCase($"{RepoIdText}.db.tar.gz", TestName = "the database under its stored name")]
    [TestCase($"{RepoIdText}.db", TestName = "repo-add's symlink")]
    [TestCase("other.db", TestName = "a sync database that disagrees with the repository name")]
    [TestCase("other.files", TestName = "a files database that disagrees with the repository name")]
    [TestCase("MYREPO.db", TestName = "a database name in the wrong case")]
    [TestCase("db", TestName = "the db subdirectory")]
    public async Task ResolveAsync_ResolvesNothingForAnythingElse(string fileName)
    {
        // Not a database of this repository, so it is looked for as a package file in the
        // repository's directory, where there is nothing of that name.
        await GivenRepositoryAsync();

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, fileName);

        Assert.That(result, Is.Null);
    }

    #endregion

    #region Architecture

    [TestCase("aarch64", TestName = "an architecture the repository does not support")]
    [TestCase(Architectures.Any, TestName = "any, which no repository supports")]
    [TestCase("X86_64", TestName = "a supported architecture in the wrong case")]
    public async Task ResolveAsync_ResolvesNothingForAnUnsupportedArchitecture_WithoutTouchingTheDisk(string repoArch)
    {
        await GivenRepositoryAsync();

        foreach (var fileName in new[] { $"{RepoName}.db", $"{RepoName}.files", PackageFileName, AnyPackageFileName })
        {
            Assert.That(await _subject.ResolveAsync(RepoName, repoArch, fileName), Is.Null, fileName);
        }

        _fileSystem.VerifyNoOtherCalls();
    }

    [TestCase(Architectures.X86_64)]
    [TestCase("aarch64")]
    public async Task ResolveAsync_ServesAnAnyPackageUnderEverySupportedArchitecture(string repoArch)
    {
        await GivenRepositoryAsync(architectures: [Architectures.X86_64, "aarch64"]);
        var content = GivenFileOnDisk($"{RepoDir}/{AnyPackageFileName}");

        var result = await _subject.ResolveAsync(RepoName, repoArch, AnyPackageFileName);

        Assert.That(result?.File?.Content, Is.SameAs(content));
    }

    #endregion

    #region Package file names

    [TestCase("..", TestName = "the parent directory")]
    [TestCase(".", TestName = "the current directory")]
    [TestCase("../../../etc/passwd", TestName = "a decoded traversal")]
    [TestCase("../" + PackageFileName, TestName = "a decoded traversal to a package-shaped name")]
    [TestCase("..\\" + PackageFileName, TestName = "a backslash traversal to a package-shaped name")]
    [TestCase("my-tool\0-1.4.2-1-x86_64.pkg.tar.zst", TestName = "a decoded NUL")]
    [TestCase(PackageFileName + "\n", TestName = "a trailing newline")]
    [TestCase("my-tool\n-1.4.2-1-x86_64.pkg.tar.zst", TestName = "an embedded newline")]
    public async Task ResolveAsync_RejectsAFileNameThatIsNotAPlainBasename_BeforeAnyFileSystemCall(string fileName)
    {
        await GivenRepositoryAsync();

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, fileName);

        AssertUnresolvedWithoutTouchingTheDisk(result);
    }

    [TestCase("..%2F..%2F..%2Fetc%2Fpasswd", TestName = "an encoded traversal")]
    [TestCase("%2e%2e", TestName = "encoded dots")]
    [TestCase("my-tool%00-1.4.2-1-x86_64.pkg.tar.zst", TestName = "an encoded control character")]
    public async Task ResolveAsync_TreatsAnEncodedTraversalAsAnOrdinaryName(string fileName)
    {
        // Routing decodes a segment before it gets here, so an encoded sequence that survives is
        // just characters in a name: it names a file inside the repository's directory, or nothing.
        await GivenRepositoryAsync();

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, fileName);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            _fileSystem.Verify(f => f.OpenRead(It.IsAny<string>()), Times.Never);
            _fileSystem.Verify(
                f => f.Exists(It.Is<string>(path => !path.StartsWith($"{RepoDir}/", StringComparison.Ordinal))),
                Times.Never);
        });
    }

    #endregion

    #region Files absent from disk

    [TestCase($"{RepoName}.db")]
    [TestCase($"{RepoName}.files")]
    [TestCase(PackageFileName)]
    public async Task ResolveAsync_ResolvesNothingForAFileAbsentFromDisk(string fileName)
    {
        await GivenRepositoryAsync();
        _fileSystem.Setup(f => f.Exists(It.IsAny<string>())).Returns(false);

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, fileName);

        Assert.Multiple(() =>
        {
            Assert.That(result, Is.Null);
            _fileSystem.Verify(f => f.OpenRead(It.IsAny<string>()), Times.Never);
        });
    }

    [Test]
    public async Task ResolveAsync_ResolvesNothingForAFileThatDisappearsBeforeItIsOpened()
    {
        await GivenRepositoryAsync();
        _fileSystem.Setup(f => f.Exists(It.IsAny<string>())).Returns(true);
        _fileSystem.Setup(f => f.GetLastWriteTimeUtc(It.IsAny<string>())).Returns(LastModified);
        _fileSystem.Setup(f => f.OpenRead(It.IsAny<string>())).Throws<FileNotFoundException>();

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, PackageFileName);

        Assert.That(result, Is.Null);
    }

    #endregion

    #region Modification time

    [TestCase($"{RepoName}.db")]
    [TestCase(PackageFileName)]
    public async Task ResolveAsync_TakesTheModificationTimeFromTheFileSystemNotTheStream(string fileName)
    {
        // A MemoryStream has no modification time at all, so the only place the result's can have
        // come from is IFileSystem -- which is what lets a mocked file system exercise the
        // conditional-GET path.
        await GivenRepositoryAsync();
        _fileSystem.Setup(f => f.Exists(It.IsAny<string>())).Returns(true);
        _fileSystem.Setup(f => f.OpenRead(It.IsAny<string>())).Returns(() => new MemoryStream());
        var distinctive = new DateTimeOffset(2001, 2, 3, 4, 5, 6, TimeSpan.Zero);
        _fileSystem.Setup(f => f.GetLastWriteTimeUtc(It.IsAny<string>())).Returns(distinctive);

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, fileName);

        Assert.That(result?.File?.LastModified, Is.EqualTo(distinctive));
    }

    #endregion

    #region Visibility and case

    [Test]
    public async Task ResolveAsync_ResolvesAPrivateRepositoryForItsOwner()
    {
        await GivenRepositoryAsync(isPublic: false);
        GivenFileOnDisk(SyncDbPath);

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, $"{RepoName}.db");

        Assert.That(result, Is.Not.Null);
    }

    [Test]
    public async Task ResolveAsync_ResolvesNothingInAPrivateRepositoryForAnotherUser()
    {
        await GivenRepositoryAsync(isPublic: false);
        _actors.Actor = Actor.For(_otherUser, ActorScope.Unrestricted);

        foreach (var fileName in new[] { $"{RepoName}.db", $"{RepoName}.files", PackageFileName })
        {
            Assert.That(await _subject.ResolveAsync(RepoName, Architectures.X86_64, fileName), Is.Null, fileName);
        }

        _fileSystem.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ResolveAsync_ResolvesNothingInAPrivateRepositoryForAnAnonymousCaller()
    {
        await GivenRepositoryAsync(isPublic: false);
        _actors.Actor = Actor.Anonymous;

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, PackageFileName);

        AssertUnresolvedWithoutTouchingTheDisk(result);
    }

    [Test]
    public async Task ResolveAsync_ResolvesAPublicRepositoryForAnAnonymousCaller()
    {
        await GivenRepositoryAsync(isPublic: true);
        _actors.Actor = Actor.Anonymous;
        var content = GivenFileOnDisk($"{RepoDir}/{PackageFileName}");

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, PackageFileName);

        Assert.That(result?.File?.Content, Is.SameAs(content));
    }

    [TestCase("MyRepo")]
    [TestCase("MYREPO")]
    public async Task ResolveAsync_MatchesTheRepositoryNameCaseSensitively(string repoName)
    {
        await GivenRepositoryAsync(isPublic: true);

        var result = await _subject.ResolveAsync(repoName, Architectures.X86_64, $"{repoName}.db");

        AssertUnresolvedWithoutTouchingTheDisk(result);
    }

    [Test]
    public async Task ResolveAsync_ResolvesNothingForARepositoryThatDoesNotExist()
    {
        var result = await _subject.ResolveAsync("missing", Architectures.X86_64, "missing.db");

        AssertUnresolvedWithoutTouchingTheDisk(result);
    }

    #endregion

    #region Retired names

    [TestCase(".db")]
    [TestCase(".db.tar.gz")]
    [TestCase(".files")]
    [TestCase(".files.tar.gz")]
    public async Task ResolveAsync_RedirectsADatabaseByTheOldName_ToTheDatabaseNamedForTheNewOne(string extension)
    {
        await GivenRepositoryAsync(isPublic: true);
        await RenameAsync("renamed");

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, RepoName + extension);

        Assert.That(result, Is.EqualTo(PacmanResolution.RedirectTo(new PacmanRedirect("renamed", "renamed" + extension))));
    }

    [TestCase(PackageFileName)]
    [TestCase($"{RepoName}.db.sig")]
    [TestCase("other.db")]
    public async Task ResolveAsync_RedirectsAnythingElseByTheOldName_UnderTheNameItWasRequestedBy(string fileName)
    {
        // Only a database is named for its repository; the target answers for anything else itself.
        await GivenRepositoryAsync(isPublic: true);
        await RenameAsync("renamed");

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, fileName);

        Assert.That(result, Is.EqualTo(PacmanResolution.RedirectTo(new PacmanRedirect("renamed", fileName))));
    }

    [Test]
    public async Task ResolveAsync_RedirectsWithoutTouchingTheDisk()
    {
        await GivenRepositoryAsync(isPublic: true);
        await RenameAsync("renamed");
        _fileSystem.Invocations.Clear();

        await _subject.ResolveAsync(RepoName, Architectures.X86_64, PackageFileName);

        _fileSystem.VerifyNoOtherCalls();
    }

    [Test]
    public async Task ResolveAsync_RedirectsEveryOldName_StraightToTheCurrentName()
    {
        await GivenRepositoryAsync(isPublic: true);
        await RenameAsync("second");
        await RenameAsync("third");

        var fromFirst = await _subject.ResolveAsync(RepoName, Architectures.X86_64, $"{RepoName}.db");
        var fromSecond = await _subject.ResolveAsync("second", Architectures.X86_64, "second.db");

        Assert.Multiple(() =>
        {
            Assert.That(fromFirst?.Redirect, Is.EqualTo(new PacmanRedirect("third", "third.db")));
            Assert.That(fromSecond?.Redirect, Is.EqualTo(new PacmanRedirect("third", "third.db")));
        });
    }

    [Test]
    public async Task ResolveAsync_IsGone_OnceTheRedirectWindowHasPassed()
    {
        await GivenRepositoryAsync(isPublic: true);
        await RenameAsync("renamed");
        _clock.Advance(RepositoryRenameConfig.DefaultRedirectWindow);

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, $"{RepoName}.db");

        Assert.That(result, Is.SameAs(PacmanResolution.Gone));
    }

    [Test]
    public async Task ResolveAsync_StillRedirects_JustBeforeTheRedirectWindowHasPassed()
    {
        await GivenRepositoryAsync(isPublic: true);
        await RenameAsync("renamed");
        _clock.Advance(RepositoryRenameConfig.DefaultRedirectWindow - TimeSpan.FromSeconds(1));

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, $"{RepoName}.db");

        Assert.That(result?.Redirect, Is.Not.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ResolveAsync_ResolvesNothingByTheOldName_ForAnArchitectureTheRepositoryDoesNotSupport(bool gone)
    {
        await GivenRepositoryAsync(isPublic: true);
        await RenameAsync("renamed");
        if (gone)
        {
            _clock.Advance(RepositoryRenameConfig.DefaultRedirectWindow);
        }

        var result = await _subject.ResolveAsync(RepoName, "aarch64", $"{RepoName}.db");

        Assert.That(result, Is.Null);
    }

    [TestCase(false)]
    [TestCase(true)]
    public async Task ResolveAsync_ResolvesNothingByAPrivateRepositorysOldName_ForAnotherUser(bool gone)
    {
        // The old name of a repository the caller may not see is as absent as its current one.
        await GivenRepositoryAsync(isPublic: false);
        await RenameAsync("renamed");
        if (gone)
        {
            _clock.Advance(RepositoryRenameConfig.DefaultRedirectWindow);
        }

        _actors.Actor = Actor.For(_otherUser, ActorScope.Unrestricted);

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, $"{RepoName}.db");

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ResolveAsync_RedirectsByAPrivateRepositorysOldName_ForItsOwner()
    {
        await GivenRepositoryAsync(isPublic: false);
        await RenameAsync("renamed");

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, $"{RepoName}.db");

        Assert.That(result?.Redirect, Is.EqualTo(new PacmanRedirect("renamed", "renamed.db")));
    }

    [Test]
    public async Task ResolveAsync_MatchesAnOldNameCaseSensitively()
    {
        await GivenRepositoryAsync(isPublic: true);
        await RenameAsync("renamed");

        var result = await _subject.ResolveAsync("MyRepo", Architectures.X86_64, "MyRepo.db");

        Assert.That(result, Is.Null);
    }

    #endregion

    private async Task RenameAsync(string newName)
    {
        var current = await _repositories.GetRepositoryByIdAsync(RepoId);
        await _repositories.UpdateRepositoryAsync(RepoId, new WriteRepositoryRequest
        {
            Name = newName,
            SupportedArchitectures = current!.SupportedArchitectures,
            IsPublic = current.IsPublic,
        });
    }

    private void AssertUnresolvedWithoutTouchingTheDisk(PacmanResolution? result)
    {
        Assert.That(result, Is.Null);
        _fileSystem.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Puts a file on the mocked disk, with <see cref="LastModified"/> as its modification time.
    /// </summary>
    private MemoryStream GivenFileOnDisk(string path)
    {
        var content = new MemoryStream();
        _fileSystem.Setup(f => f.Exists(path)).Returns(true);
        _fileSystem.Setup(f => f.GetLastWriteTimeUtc(path)).Returns(LastModified);
        _fileSystem.Setup(f => f.OpenRead(path)).Returns(content);
        return content;
    }

    private async Task GivenRepositoryAsync(bool isPublic = false, IEnumerable<string>? architectures = null)
    {
        var now = DateTimeOffset.UtcNow;
        _dbContext.Add(new PacmanRepository
        {
            Id = RepoId,
            Name = RepoName,
            SupportedArchitectures = architectures?.ToList() ?? [Architectures.X86_64],
            IsPublic = isPublic,
            Owner = _owner,
            CreatedAt = now,
            UpdatedAt = now
        });
        await _dbContext.SaveChangesAsync();
    }
}
