using PacmanManager.RepoHost.CliTools;

namespace PacmanManager.RepoHost.Test.CliTools;

public class RepositoryDatabaseTests
{
    private const string RepositoryId = "0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b";
    private const string RepoHome = "/data/libalpm";

    [Test]
    public void FileName_IsTheNameWithTheDatabaseExtension()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepoHome);

        Assert.That(subject.FileName, Is.EqualTo($"{RepositoryId}.db.tar.gz"));
    }

    [Test]
    public void SyncDirectory_IsTheSyncSubdirectoryOfTheDatabaseHome()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepoHome);

        Assert.That(subject.SyncDirectory, Is.EqualTo("/data/libalpm/sync"));
    }
}
