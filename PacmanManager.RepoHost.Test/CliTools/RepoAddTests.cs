using PacmanManager.Entities;
using PacmanManager.RepoHost.CliTools;

namespace PacmanManager.RepoHost.Test.CliTools;

public class RepoAddTests
{
    private const string RepositoryId = "0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b";
    private const string RepoHome = "/data/libalpm";
    private const string Architecture = Architectures.X86_64;

    [Test]
    public void Executable_IsRepoAdd()
    {
        var subject = new RepoAdd(RepositoryId, RepoHome, Architecture);

        Assert.Multiple(() =>
        {
            Assert.That(subject.Name, Is.EqualTo("repo-add"));
            Assert.That(subject.Executable, Is.EqualTo("repo-add"));
        });
    }

    [Test]
    public void WorkingDirectory_IsTheArchitecturesDirectoryUnderTheSyncDirectory()
    {
        var subject = new RepoAdd(RepositoryId, RepoHome, Architecture, "/data/repositories/repo/my-tool-1.0-1-x86_64.pkg.tar.zst");

        Assert.That(subject.WorkingDirectory, Is.EqualTo("/data/libalpm/sync/x86_64"));
    }

    [Test]
    public void Arguments_WithNoPackages_IsJustTheDatabaseFileName()
    {
        var subject = new RepoAdd(RepositoryId, RepoHome, Architecture);

        Assert.That(subject.Arguments, Is.EqualTo(new[] { $"{RepositoryId}.db.tar.gz" }));
    }

    [Test]
    public void Arguments_WithOnePackage_IsTheDatabaseFileNameThenThePackagePath()
    {
        const string packagePath = "/data/repositories/repo/my-tool-1.4.2-1-x86_64.pkg.tar.zst";

        var subject = new RepoAdd(RepositoryId, RepoHome, Architecture, packagePath);

        Assert.That(subject.Arguments, Is.EqualTo(new[] { $"{RepositoryId}.db.tar.gz", packagePath }));
    }

    [Test]
    public void Arguments_WithSeveralPackages_KeepsThePackageOrder()
    {
        const string first = "/data/repositories/repo/a-1.0-1-x86_64.pkg.tar.zst";
        const string second = "/data/repositories/repo/b-2.0-1-any.pkg.tar.zst";

        var subject = new RepoAdd(RepositoryId, RepoHome, Architecture, first, second);

        Assert.That(subject.Arguments, Is.EqualTo(new[] { $"{RepositoryId}.db.tar.gz", first, second }));
    }

    [Test]
    public void Arguments_AcceptsAnEnumerableOfPackagePaths()
    {
        var packages = new List<string>
        {
            "/data/repositories/repo/a-1.0-1-x86_64.pkg.tar.zst",
            "/data/repositories/repo/b-2.0-1-any.pkg.tar.zst"
        };

        var subject = new RepoAdd(RepositoryId, RepoHome, Architecture, packages);

        Assert.That(subject.Arguments, Is.EqualTo(new[] { $"{RepositoryId}.db.tar.gz", packages[0], packages[1] }));
    }

    [Test]
    public void Arguments_AreStableAcrossEnumerations()
    {
        var packages = new[] { "/data/repositories/repo/a-1.0-1-x86_64.pkg.tar.zst" };

        var subject = new RepoAdd(RepositoryId, RepoHome, Architecture, packages.Select(p => p));
        var first = subject.Arguments.ToArray();
        var second = subject.Arguments.ToArray();

        Assert.That(second, Is.EqualTo(first));
    }
}
