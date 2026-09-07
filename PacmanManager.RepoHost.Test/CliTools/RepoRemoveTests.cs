using PacmanManager.RepoHost.CliTools;

namespace PacmanManager.RepoHost.Test.CliTools;

public class RepoRemoveTests
{
    private const string RepositoryId = "0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b";
    private const string RepoHome = "/data/libalpm";

    [Test]
    public void Executable_IsRepoRemove()
    {
        var subject = new RepoRemove(RepositoryId, RepoHome, "my-tool");

        Assert.Multiple(() =>
        {
            Assert.That(subject.Name, Is.EqualTo("repo-remove"));
            Assert.That(subject.Executable, Is.EqualTo("repo-remove"));
        });
    }

    [Test]
    public void WorkingDirectory_IsTheSyncDirectoryOfTheDatabaseHome()
    {
        var subject = new RepoRemove(RepositoryId, RepoHome, "my-tool");

        Assert.That(subject.WorkingDirectory, Is.EqualTo("/data/libalpm/sync"));
    }

    [Test]
    public void Arguments_AreTheDatabaseFileNameThenThePackageName()
    {
        var subject = new RepoRemove(RepositoryId, RepoHome, "my-tool");

        Assert.That(subject.Arguments, Is.EqualTo(new[] { $"{RepositoryId}.db.tar.gz", "my-tool" }));
    }

    [Test]
    public void Arguments_WithSeveralPackages_KeepsThePackageOrder()
    {
        var subject = new RepoRemove(RepositoryId, RepoHome, "my-tool", "other-tool");

        Assert.That(subject.Arguments, Is.EqualTo(new[] { $"{RepositoryId}.db.tar.gz", "my-tool", "other-tool" }));
    }

    [Test]
    public void Arguments_AcceptsAnEnumerableOfPackageNames()
    {
        var names = new List<string> { "my-tool", "other-tool" };

        var subject = new RepoRemove(RepositoryId, RepoHome, names);

        Assert.That(subject.Arguments, Is.EqualTo(new[] { $"{RepositoryId}.db.tar.gz", "my-tool", "other-tool" }));
    }

    [Test]
    public void Arguments_AreStableAcrossEnumerations()
    {
        var names = new[] { "my-tool" };

        var subject = new RepoRemove(RepositoryId, RepoHome, names.Select(n => n));
        var first = subject.Arguments.ToArray();
        var second = subject.Arguments.ToArray();

        Assert.That(second, Is.EqualTo(first));
    }

    [Test]
    public void Constructor_WithNoPackageNames_Throws()
    {
        Assert.That(
            () => new RepoRemove(RepositoryId, RepoHome),
            Throws.ArgumentException.With.Property("ParamName").EqualTo("packageNames"));
    }
}
