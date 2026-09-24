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

    private PacmanManagerDbContext _dbContext = null!;
    private TestActorAccessor _actors = null!;
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

        var pathResolver = new PackagePathResolver(Options.Create(new PacmanConfigSettings { DataDir = DataDir }));
        var repositories = new RepositoryService(
            _dbContext,
            new Mock<ICliToolRunner>().Object,
            _actors,
            new RepositoryAccessPolicy(),
            new TestOutputLogger<RepositoryService>(),
            _fileSystem.Object,
            pathResolver);

        _subject = new PacmanRepoService(repositories, pathResolver, _fileSystem.Object);
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

        Assert.That(result, Is.EqualTo(new RepositoryFile(content, LastModified)));
    }

    [TestCase($"{RepoName}.files", TestName = "repo.files is the files database")]
    [TestCase($"{RepoName}.files.tar.gz", TestName = "repo.files.tar.gz is the files database")]
    public async Task ResolveAsync_ServesTheFilesDatabase(string fileName)
    {
        await GivenRepositoryAsync();
        var content = GivenFileOnDisk(FilesDbPath);

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, fileName);

        Assert.That(result, Is.EqualTo(new RepositoryFile(content, LastModified)));
    }

    [Test]
    public async Task ResolveAsync_ReadsTheRequestedArchitecturesDatabase()
    {
        await GivenRepositoryAsync(architectures: [Architectures.X86_64, "aarch64"]);
        var content = GivenFileOnDisk($"{RepoDir}/db/aarch64/{RepoId}.db.tar.gz");

        var result = await _subject.ResolveAsync(RepoName, "aarch64", $"{RepoName}.db");

        Assert.That(result?.Content, Is.SameAs(content));
    }

    [Test]
    public async Task ResolveAsync_ServesAPackageFileStraightFromTheRepositorysDirectory()
    {
        await GivenRepositoryAsync();
        var content = GivenFileOnDisk($"{RepoDir}/{PackageFileName}");

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, PackageFileName);

        Assert.That(result, Is.EqualTo(new RepositoryFile(content, LastModified)));
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
    public async Task ResolveAsync_ResolvesNothingForAnythingElse_WithoutTouchingTheDisk(string fileName)
    {
        await GivenRepositoryAsync();

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, fileName);

        AssertUnresolvedWithoutTouchingTheDisk(result);
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

    [Test]
    public async Task ResolveAsync_ResolvesNothingForAPackageBuiltForAnotherArchitecture_WithoutTouchingTheDisk()
    {
        await GivenRepositoryAsync(architectures: [Architectures.X86_64, "aarch64"]);

        var result = await _subject.ResolveAsync(RepoName, "aarch64", PackageFileName);

        AssertUnresolvedWithoutTouchingTheDisk(result);
    }

    [TestCase(Architectures.X86_64)]
    [TestCase("aarch64")]
    public async Task ResolveAsync_ServesAnAnyPackageUnderEverySupportedArchitecture(string repoArch)
    {
        await GivenRepositoryAsync(architectures: [Architectures.X86_64, "aarch64"]);
        var content = GivenFileOnDisk($"{RepoDir}/{AnyPackageFileName}");

        var result = await _subject.ResolveAsync(RepoName, repoArch, AnyPackageFileName);

        Assert.That(result?.Content, Is.SameAs(content));
    }

    #endregion

    #region Package file-name validation

    [TestCase("my-tool-:1.4.2-x86_64.pkg.tar.zst", TestName = "a bad version")]
    [TestCase("my-tool-1.4.2-1-x86/64.pkg.tar.zst", TestName = "a bad architecture token")]
    [TestCase("my-tool-1.4.2-1-_x86_64.pkg.tar.zst", TestName = "an architecture token that is not an architecture")]
    [TestCase("my-tool-1.4.2-1-x86_64", TestName = "no .pkg.tar suffix")]
    [TestCase("my-tool-1.4.2-1-x86_64.tar.zst", TestName = "a .tar suffix without .pkg")]
    [TestCase("my-tool-1.4.2-1-x86_64.pkg.tar.lz4", TestName = "an unrecognised compression")]
    public async Task ResolveAsync_RejectsAMalformedPackageName_BeforeAnyFileSystemCall(string fileName)
    {
        await GivenRepositoryAsync();

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, fileName);

        AssertUnresolvedWithoutTouchingTheDisk(result);
    }

    [TestCase("..", TestName = "the parent directory")]
    [TestCase(".", TestName = "the current directory")]
    [TestCase("../../../etc/passwd", TestName = "a decoded traversal")]
    [TestCase("..%2F..%2F..%2Fetc%2Fpasswd", TestName = "an encoded traversal")]
    [TestCase("..%2F" + PackageFileName, TestName = "an encoded traversal to a package-shaped name")]
    [TestCase("../" + PackageFileName, TestName = "a decoded traversal to a package-shaped name")]
    [TestCase("..\\" + PackageFileName, TestName = "a backslash traversal to a package-shaped name")]
    [TestCase("%2e%2e", TestName = "encoded dots")]
    [TestCase("my-tool%00-1.4.2-1-x86_64.pkg.tar.zst", TestName = "an encoded control character")]
    [TestCase("my-tool\0-1.4.2-1-x86_64.pkg.tar.zst", TestName = "a decoded NUL")]
    [TestCase(PackageFileName + "\n", TestName = "a trailing newline")]
    [TestCase("my-tool\n-1.4.2-1-x86_64.pkg.tar.zst", TestName = "an embedded newline")]
    public async Task ResolveAsync_RejectsATraversalAttempt_BeforeAnyFileSystemCall(string fileName)
    {
        await GivenRepositoryAsync();

        var result = await _subject.ResolveAsync(RepoName, Architectures.X86_64, fileName);

        AssertUnresolvedWithoutTouchingTheDisk(result);
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

        Assert.That(result?.LastModified, Is.EqualTo(distinctive));
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

        Assert.That(result?.Content, Is.SameAs(content));
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

    private void AssertUnresolvedWithoutTouchingTheDisk(RepositoryFile? result)
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
