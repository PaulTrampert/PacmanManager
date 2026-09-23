using PacmanManager.RepoHost.CliTools;

namespace PacmanManager.RepoHost.Test.CliTools;

public class RepositoryDatabaseTests
{
    private const string RepositoryId = "0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b";
    private const string RepoHome = "/data/libalpm";

    [Test]
    public void FileName_IsTheNameWithTheDatabaseExtension()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepoHome, "x86_64");

        Assert.That(subject.FileName, Is.EqualTo($"{RepositoryId}.db.tar.gz"));
    }

    [Test]
    public void SyncDirectory_IsTheArchitecturesDirectoryUnderTheSyncSubdirectoryOfTheDatabaseHome()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepoHome, "x86_64");

        Assert.That(subject.SyncDirectory, Is.EqualTo("/data/libalpm/sync/x86_64"));
    }

    [Test]
    public void FilePath_IsTheFileNameInsideTheSyncDirectory()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepoHome, "x86_64");

        Assert.That(subject.FilePath, Is.EqualTo($"/data/libalpm/sync/x86_64/{RepositoryId}.db.tar.gz"));
    }

    [Test]
    public void EachArchitecture_HasItsOwnDatabase_UnderTheSameFileName()
    {
        var x86 = new RepositoryDatabase(RepositoryId, RepoHome, "x86_64");
        var arm = new RepositoryDatabase(RepositoryId, RepoHome, "aarch64");

        Assert.Multiple(() =>
        {
            Assert.That(arm.FileName, Is.EqualTo(x86.FileName), "The file keeps the repository's id as its name.");
            Assert.That(arm.FilePath, Is.Not.EqualTo(x86.FilePath));
            Assert.That(arm.SyncDirectory, Is.EqualTo("/data/libalpm/sync/aarch64"));
        });
    }
}
