using LibAlpmSharp.Config;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Startup.LibAlpm;

namespace PacmanManager.RepoHost.Test.Startup.LibAlpm;

/// <summary>
/// Exercises <see cref="PacmanDirectoriesInitializer"/> against a real temporary directory.
/// </summary>
[TestFixture]
public class PacmanDirectoriesInitializerTests
{
    private string _root = null!;
    private string _dbPath = null!;
    private PacmanDirectoriesInitializer _subject = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"pacmanmanager-init-{Guid.CreateVersion7()}");
        _dbPath = Path.Combine(_root, "libalpm");
        _subject = new PacmanDirectoriesInitializer(new PacmanConfig { DBPath = _dbPath }, new PhysicalFileSystem());
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public async Task StartAsync_CreatesSyncAndLocalUnderTheDatabasePath()
    {
        await _subject.StartAsync(CancellationToken.None);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(Directory.Exists(Path.Combine(_dbPath, "sync")), Is.True);
            Assert.That(Directory.Exists(Path.Combine(_dbPath, "local")), Is.True);
        }
    }

    [Test]
    public async Task StartAsync_LeavesExistingDirectoriesAndTheirContentsAlone()
    {
        await _subject.StartAsync(CancellationToken.None);
        var marker = Path.Combine(_dbPath, "local", "marker");
        await File.WriteAllTextAsync(marker, "kept");

        await _subject.StartAsync(CancellationToken.None);

        Assert.That(await File.ReadAllTextAsync(marker), Is.EqualTo("kept"));
    }

    [Test]
    public async Task StopAsync_LeavesTheDirectoriesInPlace()
    {
        await _subject.StartAsync(CancellationToken.None);

        await _subject.StopAsync(CancellationToken.None);

        Assert.That(Directory.Exists(Path.Combine(_dbPath, "sync")), Is.True);
    }
}
