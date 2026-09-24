using PacmanManager.Entities;
using PacmanManager.RepoHost.CliTools;

namespace PacmanManager.RepoHost.Test.CliTools;

public class RepositoryDatabaseTests
{
    private const string RepositoryId = "0199a1b2-c3d4-7e5f-8a9b-0c1d2e3f4a5b";
    private const string RepositoryDirectory = $"/data/repositories/{RepositoryId}";
    private const string Aarch64 = "aarch64";

    [Test]
    public void FileName_IsTheNameWithTheDatabaseExtension()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Architectures.X86_64);

        Assert.That(subject.FileName, Is.EqualTo($"{RepositoryId}.db.tar.gz"));
    }

    [Test]
    public void FilesFileName_IsTheNameWithTheFilesDatabaseExtension()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Architectures.X86_64);

        Assert.That(subject.FilesFileName, Is.EqualTo($"{RepositoryId}.files.tar.gz"));
    }

    [Test]
    public void DatabaseDirectory_IsTheArchitecturesDirectoryUnderTheRepositorysDbDirectory()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Architectures.X86_64);

        Assert.That(subject.DatabaseDirectory, Is.EqualTo($"/data/repositories/{RepositoryId}/db/x86_64"));
    }

    [Test]
    public void FilePath_IsTheFileNameInsideTheDatabaseDirectory()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Architectures.X86_64);

        Assert.That(subject.FilePath,
            Is.EqualTo($"/data/repositories/{RepositoryId}/db/x86_64/{RepositoryId}.db.tar.gz"));
    }

    [Test]
    public void FilesFilePath_IsTheFilesFileNameInsideTheDatabaseDirectory()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Architectures.X86_64);

        Assert.That(subject.FilesFilePath,
            Is.EqualTo($"/data/repositories/{RepositoryId}/db/x86_64/{RepositoryId}.files.tar.gz"));
    }

    /// <summary>
    /// The six files one <c>repo-add</c> run leaves behind, as observed against pacman 7.1.0: both
    /// databases, the symlink <c>repo-add</c> points at each, and the backup it rotates each into.
    /// </summary>
    [Test]
    public void FileNames_AreEveryFileRepoAddWrites()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Architectures.X86_64);

        Assert.That(subject.FileNames, Is.EquivalentTo(new[]
        {
            $"{RepositoryId}.db.tar.gz",
            $"{RepositoryId}.files.tar.gz",
            $"{RepositoryId}.db",
            $"{RepositoryId}.files",
            $"{RepositoryId}.db.tar.gz.old",
            $"{RepositoryId}.files.tar.gz.old"
        }));
    }

    [Test]
    public void FilePaths_AreTheFileNamesInsideTheDatabaseDirectory()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Architectures.X86_64);

        Assert.That(subject.FilePaths,
            Is.EqualTo(subject.FileNames.Select(f => $"/data/repositories/{RepositoryId}/db/x86_64/{f}")));
    }

    [Test]
    public void FilePaths_IncludeBothDatabases()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Architectures.X86_64);

        Assert.That(subject.FilePaths, Is.SupersetOf(new[] { subject.FilePath, subject.FilesFilePath }));
    }

    [Test]
    public void EveryFile_LivesUnderTheRepositoryDirectory()
    {
        // Deleting a repository is deleting its directory, which only works while nothing the
        // database tools write lands outside it.
        var subject = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Architectures.X86_64);

        Assert.That(subject.FilePaths, Has.All.StartWith($"{RepositoryDirectory}/"));
    }

    [Test]
    public void EachArchitecture_HasItsOwnDatabase_UnderTheSameFileNames()
    {
        var x86 = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Architectures.X86_64);
        var arm = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Aarch64);

        Assert.Multiple(() =>
        {
            Assert.That(arm.FileNames, Is.EqualTo(x86.FileNames), "The files keep the repository's id as their name.");
            Assert.That(arm.DatabaseDirectory, Is.EqualTo($"/data/repositories/{RepositoryId}/db/aarch64"));
            Assert.That(arm.FilePath, Is.EqualTo($"{arm.DatabaseDirectory}/{RepositoryId}.db.tar.gz"));
            Assert.That(arm.FilesFilePath, Is.EqualTo($"{arm.DatabaseDirectory}/{RepositoryId}.files.tar.gz"));
        });
    }

    [Test]
    public void TwoArchitectures_ShareNoFile_AndKeepEveryFileUnderTheRepositoryDirectory()
    {
        var x86 = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Architectures.X86_64);
        var arm = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Aarch64);

        var all = x86.FilePaths.Concat(arm.FilePaths).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(all, Has.Count.EqualTo(12));
            Assert.That(all, Is.Unique);
            Assert.That(all, Has.All.StartWith($"{RepositoryDirectory}/db/"));
        });
    }

    [TestCase(RepositoryDatabaseKind.Sync, $"{RepositoryId}.db.tar.gz")]
    [TestCase(RepositoryDatabaseKind.Files, $"{RepositoryId}.files.tar.gz")]
    public void FilePathOf_IsThePathOfTheNamedDatabase(RepositoryDatabaseKind kind, string fileName)
    {
        var subject = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Aarch64);

        Assert.That(subject.FilePathOf(kind), Is.EqualTo($"{RepositoryDirectory}/db/aarch64/{fileName}"));
    }

    [Test]
    public void FilePathOf_RejectsAnUndefinedKind()
    {
        var subject = new RepositoryDatabase(RepositoryId, RepositoryDirectory, Architectures.X86_64);

        Assert.Throws<ArgumentOutOfRangeException>(() => subject.FilePathOf((RepositoryDatabaseKind)42));
    }
}
