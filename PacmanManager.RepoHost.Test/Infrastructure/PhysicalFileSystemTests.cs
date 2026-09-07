using PacmanManager.RepoHost.Infrastructure;

namespace PacmanManager.RepoHost.Test.Infrastructure;

/// <summary>
/// Exercises the write side of <see cref="PhysicalFileSystem"/> against a real temporary directory.
/// The interface exists so that services can be tested without a disk; these tests are what say the
/// one implementation that does touch the disk behaves the way those services assume.
/// </summary>
[TestFixture]
public class PhysicalFileSystemTests
{
    private string _root = null!;
    private PhysicalFileSystem _subject = null!;

    [SetUp]
    public void SetUp()
    {
        _root = Path.Combine(Path.GetTempPath(), $"pacmanmanager-fs-{Guid.CreateVersion7()}");
        Directory.CreateDirectory(_root);
        _subject = new PhysicalFileSystem();
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
    public void OpenWrite_CreatesAFileThatOpenReadReadsBack()
    {
        var path = Path.Combine(_root, "package.pkg.tar.zst");

        using (var stream = _subject.OpenWrite(path))
        {
            stream.Write("contents"u8);
        }

        using var reader = new StreamReader(_subject.OpenRead(path));

        Assert.That(_subject.Exists(path), Is.True);
        Assert.That(reader.ReadToEnd(), Is.EqualTo("contents"));
    }

    [Test]
    public void OpenWrite_TruncatesAnExistingFile()
    {
        var path = Path.Combine(_root, "package.pkg.tar.zst");
        File.WriteAllText(path, "a much longer previous version");

        using (var stream = _subject.OpenWrite(path))
        {
            stream.Write("short"u8);
        }

        Assert.That(File.ReadAllText(path), Is.EqualTo("short"));
    }

    [Test]
    public void Move_RelocatesTheFile()
    {
        var source = Path.Combine(_root, "upload.tmp");
        var destination = Path.Combine(_root, "package.pkg.tar.zst");
        File.WriteAllText(source, "contents");

        _subject.Move(source, destination);

        Assert.Multiple(() =>
        {
            Assert.That(_subject.Exists(source), Is.False);
            Assert.That(File.ReadAllText(destination), Is.EqualTo("contents"));
        });
    }

    [Test]
    public void Move_WithoutOverwrite_RefusesToReplaceAnExistingFile()
    {
        var source = Path.Combine(_root, "upload.tmp");
        var destination = Path.Combine(_root, "package.pkg.tar.zst");
        File.WriteAllText(source, "new");
        File.WriteAllText(destination, "existing");

        Assert.Throws<IOException>(() => _subject.Move(source, destination));
        Assert.That(File.ReadAllText(destination), Is.EqualTo("existing"));
    }

    [Test]
    public void Move_WithOverwrite_ReplacesAnExistingFile()
    {
        var source = Path.Combine(_root, "upload.tmp");
        var destination = Path.Combine(_root, "package.pkg.tar.zst");
        File.WriteAllText(source, "new");
        File.WriteAllText(destination, "existing");

        _subject.Move(source, destination, overwrite: true);

        Assert.That(File.ReadAllText(destination), Is.EqualTo("new"));
    }

    [Test]
    public void CreateDirectory_CreatesMissingParents()
    {
        var path = Path.Combine(_root, "repositories", Guid.CreateVersion7().ToString());

        _subject.CreateDirectory(path);

        Assert.That(_subject.DirectoryExists(path), Is.True);
    }

    [Test]
    public void CreateDirectory_IsIdempotent()
    {
        var path = Path.Combine(_root, "repositories");
        _subject.CreateDirectory(path);

        Assert.DoesNotThrow(() => _subject.CreateDirectory(path));
        Assert.That(_subject.DirectoryExists(path), Is.True);
    }

    [Test]
    public void DirectoryExists_IsFalseForAFile()
    {
        var path = Path.Combine(_root, "package.pkg.tar.zst");
        File.WriteAllText(path, "contents");

        Assert.Multiple(() =>
        {
            Assert.That(_subject.DirectoryExists(path), Is.False);
            Assert.That(_subject.Exists(path), Is.True);
        });
    }
}
