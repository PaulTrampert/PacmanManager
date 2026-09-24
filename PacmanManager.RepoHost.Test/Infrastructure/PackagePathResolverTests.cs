using Microsoft.Extensions.Options;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.RepoHost.Startup.LibAlpm;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Infrastructure;

/// <summary>
/// Covers the two rules the resolver owns: where a repository's package files live, and what a
/// stored package file is called. The derivation is the security boundary — the metadata it
/// formats comes out of a file the caller chose — so most of these cases are rejections.
/// </summary>
[TestFixture]
public class PackagePathResolverTests
{
    private const string DataDir = "/data";

    private static readonly Guid RepositoryId = Guid.Parse("0199a2c0-0000-7000-8000-000000000001");

    private PackagePathResolver _subject = null!;

    [SetUp]
    public void SetUp()
    {
        _subject = new PackagePathResolver(
            Options.Create(new PacmanConfigSettings { DataDir = DataDir }));
    }

    #region Path resolution

    [Test]
    public void GetRepositoryDirectory_IsUnderRepositoriesNamedByRepositoryId()
    {
        Assert.That(_subject.GetRepositoryDirectory(RepositoryId),
            Is.EqualTo(Path.Combine(DataDir, "repositories", RepositoryId.ToString())));
    }

    [Test]
    public void GetRepositoryDirectory_FollowsTheConfiguredDataDir()
    {
        var subject = new PackagePathResolver(Options.Create(new PacmanConfigSettings { DataDir = "/srv/pacman" }));

        Assert.That(subject.GetRepositoryDirectory(RepositoryId),
            Is.EqualTo(Path.Combine("/srv/pacman", "repositories", RepositoryId.ToString())));
    }

    [Test]
    public void GetRepositoryDirectory_IsNotTheSyncDirectory()
    {
        // repo-add records only the basename of a package file, so package files may not share the
        // sync directory with the .db.tar.gz files: two repositories would collide.
        var settings = new PacmanConfigSettings { DataDir = DataDir };

        Assert.That(_subject.GetRepositoryDirectory(RepositoryId),
            Does.Not.StartWith(Path.Combine(settings.DbPath, "sync")));
    }

    [Test]
    public void GetRepositoryDirectory_DiffersPerRepository()
    {
        var other = Guid.Parse("0199a2c0-0000-7000-8000-000000000002");

        Assert.That(_subject.GetRepositoryDirectory(RepositoryId),
            Is.Not.EqualTo(_subject.GetRepositoryDirectory(other)));
    }

    [Test]
    public void GetPackageFilePath_IsTheFileNameInsideTheRepositoryDirectory()
    {
        const string fileName = "my-tool-1.4.2-1-x86_64.pkg.tar.zst";

        Assert.That(_subject.GetPackageFilePath(RepositoryId, fileName),
            Is.EqualTo(Path.Combine(DataDir, "repositories", RepositoryId.ToString(), fileName)));
    }

    [TestCase("../escape.pkg.tar.zst")]
    [TestCase("nested/my-tool.pkg.tar.zst")]
    [TestCase("/etc/passwd")]
    [TestCase("..\\escape.pkg.tar.zst")]
    [TestCase("..")]
    [TestCase(".")]
    public void GetPackageFilePath_RejectsAnythingThatIsNotABasename(string fileName)
    {
        Assert.Throws<ArgumentException>(() => _subject.GetPackageFilePath(RepositoryId, fileName));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void GetPackageFilePath_RejectsAnEmptyFileName(string fileName)
    {
        Assert.Throws<ArgumentException>(() => _subject.GetPackageFilePath(RepositoryId, fileName));
    }

    #endregion

    #region Compression sniffing

    private static readonly byte[] ZstdMagic = [0x28, 0xB5, 0x2F, 0xFD];
    private static readonly byte[] XzMagic = [0xFD, 0x37, 0x7A, 0x58, 0x5A, 0x00];
    private static readonly byte[] GzipMagic = [0x1F, 0x8B];
    private static readonly byte[] Bzip2Magic = [0x42, 0x5A, 0x68];

    private static IEnumerable<TestCaseData> AllowedCompressions()
    {
        yield return new TestCaseData(ZstdMagic, PackageCompression.Zstandard, "zst").SetName("zstd");
        yield return new TestCaseData(XzMagic, PackageCompression.Xz, "xz").SetName("xz");
        yield return new TestCaseData(GzipMagic, PackageCompression.Gzip, "gz").SetName("gzip");
        yield return new TestCaseData(Bzip2Magic, PackageCompression.Bzip2, "bz2").SetName("bzip2");
    }

    [TestCaseSource(nameof(AllowedCompressions))]
    public void DetectCompression_RecognisesAllowedFormats(byte[] magic, PackageCompression expected, string extension)
    {
        using var content = StreamOf(magic);

        Assert.That(_subject.DetectCompression(content), Is.EqualTo(expected));
        Assert.That(expected.ToFileExtension(), Is.EqualTo(extension));
    }

    [TestCaseSource(nameof(AllowedCompressions))]
    public void DeriveFileName_UsesTheSniffedExtension(byte[] magic, PackageCompression expected, string extension)
    {
        using var content = StreamOf(magic);

        Assert.That(_subject.DeriveFileName("my-tool", "1.4.2-1", Architectures.X86_64, content),
            Is.EqualTo($"my-tool-1.4.2-1-x86_64.pkg.tar.{extension}"));
    }

    [Test]
    public void DetectCompression_RejectsAnUnrecognisedCompression()
    {
        // A zip file: a real archive, and not one of the four allowed formats.
        using var content = StreamOf([0x50, 0x4B, 0x03, 0x04, 0x00, 0x00]);

        Assert.Throws<UnsupportedPackageCompressionException>(() => _subject.DetectCompression(content));
    }

    [Test]
    public void DeriveFileName_RejectsAnUnrecognisedCompression()
    {
        using var content = StreamOf("this is not a package"u8.ToArray());

        var ex = Assert.Throws<UnsupportedPackageCompressionException>(
            () => _subject.DeriveFileName("my-tool", "1.4.2-1", Architectures.X86_64, content));

        Assert.That(ex, Is.InstanceOf<InvalidPackageException>());
    }

    [Test]
    public void DetectCompression_RejectsAFileTooShortToClassify()
    {
        using var content = StreamOf([0x28]);

        Assert.Throws<UnsupportedPackageCompressionException>(() => _subject.DetectCompression(content));
    }

    [Test]
    public void DetectCompression_RejectsAnEmptyFile()
    {
        using var content = StreamOf([]);

        Assert.Throws<UnsupportedPackageCompressionException>(() => _subject.DetectCompression(content));
    }

    [Test]
    public void DetectCompression_SniffsFromTheStartAndRestoresThePosition()
    {
        using var content = StreamOf([.. GzipMagic, 1, 2, 3, 4]);
        content.Position = 3;

        Assert.That(_subject.DetectCompression(content), Is.EqualTo(PackageCompression.Gzip));
        Assert.That(content.Position, Is.EqualTo(3));
    }

    [Test]
    public void DetectCompression_RejectsAStreamItCannotSeek()
    {
        using var content = new NonSeekableStream(ZstdMagic);

        Assert.Throws<ArgumentException>(() => _subject.DetectCompression(content));
    }

    [Test]
    public void DeriveFileName_MatchesTheNameOfTheCommittedFixturePackage()
    {
        // The strongest available check that the rule matches what makepkg produces.
        using var content = File.OpenRead(
            Path.Combine(PackageFixtures.Directory, PackageFixtures.MinimalPackageFileName));

        Assert.That(
            _subject.DeriveFileName(
                PackageFixtures.MinimalPackageName,
                PackageFixtures.MinimalPackageVersion,
                PackageFixtures.MinimalPackageArchitecture,
                content),
            Is.EqualTo(PackageFixtures.MinimalPackageFileName));
    }

    #endregion

    #region Name derivation

    [TestCase("my-tool", "1.4.2-1", Architectures.X86_64, "my-tool-1.4.2-1-x86_64.pkg.tar.zst")]
    [TestCase("my-tool", "2:1.4.2-1", Architectures.X86_64, "my-tool-2:1.4.2-1-x86_64.pkg.tar.zst")]
    [TestCase("lib32-foo+bar", "1.0", Architectures.Any, "lib32-foo+bar-1.0-any.pkg.tar.zst")]
    [TestCase("a", "1", "aarch64", "a-1-aarch64.pkg.tar.zst")]
    public void DeriveFileName_FormatsNameVersionAndArchitecture(
        string name, string version, string architecture, string expected)
    {
        Assert.That(_subject.DeriveFileName(name, version, architecture, PackageCompression.Zstandard),
            Is.EqualTo(expected));
    }

    [Test]
    public void DeriveFileName_NeverProducesAPathSeparator()
    {
        var fileName = _subject.DeriveFileName("my-tool", "1.4.2-1", Architectures.X86_64, PackageCompression.Zstandard);

        Assert.Multiple(() =>
        {
            Assert.That(fileName, Does.Not.Contain("/"));
            Assert.That(fileName, Does.Not.Contain("\\"));
            Assert.That(Path.GetFileName(fileName), Is.EqualTo(fileName));
        });
    }

    [TestCase("../../etc/passwd", TestName = "name traverses upwards")]
    [TestCase("nested/my-tool", TestName = "name contains a forward slash")]
    [TestCase("nested\\my-tool", TestName = "name contains a backslash")]
    [TestCase("/etc/passwd", TestName = "name is an absolute path")]
    [TestCase("..", TestName = "name is the parent directory")]
    [TestCase(".", TestName = "name is the current directory")]
    [TestCase("my-tool\n", TestName = "name has a trailing newline the ^/$ anchors would allow")]
    [TestCase("my-tool\nrm -rf /", TestName = "name embeds a newline")]
    [TestCase("my tool", TestName = "name contains a space")]
    [TestCase("", TestName = "name is empty")]
    public void DeriveFileName_RejectsANameThatWouldEscapeTheRepositoryDirectory(string name)
    {
        Assert.Throws<InvalidPackageMetadataException>(
            () => _subject.DeriveFileName(name, "1.4.2-1", Architectures.X86_64, PackageCompression.Zstandard));
    }

    [TestCase("1.0/../../etc/passwd", TestName = "version contains a forward slash")]
    [TestCase("..", TestName = "version is the parent directory")]
    [TestCase("1.0\\1", TestName = "version contains a backslash")]
    [TestCase("1.0-1\n", TestName = "version has a trailing newline")]
    [TestCase("1.0-", TestName = "version has an empty pkgrel")]
    [TestCase("", TestName = "version is empty")]
    public void DeriveFileName_RejectsAVersionThatWouldEscapeTheRepositoryDirectory(string version)
    {
        Assert.Throws<InvalidPackageMetadataException>(
            () => _subject.DeriveFileName("my-tool", version, Architectures.X86_64, PackageCompression.Zstandard));
    }

    [TestCase("../../x86_64", TestName = "architecture traverses upwards")]
    [TestCase("x86_64/..", TestName = "architecture contains a forward slash")]
    [TestCase("x86_64\\..", TestName = "architecture contains a backslash")]
    [TestCase("x86_64\n", TestName = "architecture has a trailing newline")]
    [TestCase("", TestName = "architecture is empty")]
    public void DeriveFileName_RejectsAnArchitectureThatWouldEscapeTheRepositoryDirectory(string architecture)
    {
        Assert.Throws<InvalidPackageMetadataException>(
            () => _subject.DeriveFileName("my-tool", "1.4.2-1", architecture, PackageCompression.Zstandard));
    }

    [Test]
    public void DeriveFileName_RejectsANameLongerThanTheColumnAllows()
    {
        var name = new string('a', PackageValidationConstants.NameMaxLength + 1);

        Assert.Throws<InvalidPackageMetadataException>(
            () => _subject.DeriveFileName(name, "1.4.2-1", Architectures.X86_64, PackageCompression.Zstandard));
    }

    [Test]
    public void DeriveFileName_NamesTheFieldItRejected()
    {
        var ex = Assert.Throws<InvalidPackageMetadataException>(
            () => _subject.DeriveFileName("my-tool", "not/a/version", Architectures.X86_64, PackageCompression.Zstandard));

        Assert.That(ex.Field, Is.EqualTo("version"));
        Assert.That(ex, Is.InstanceOf<InvalidPackageException>());
    }

    [Test]
    public void DeriveFileName_FitsTheStoredFileNameColumn()
    {
        var name = new string('a', PackageValidationConstants.NameMaxLength);
        var architecture = new string('b', PackageValidationConstants.ArchitectureMaxLength);

        // Longest values each column allows still have to fit FileNameMaxLength, or the derivation
        // has to say so rather than the database failing on insert.
        Assert.Throws<InvalidPackageMetadataException>(
            () => _subject.DeriveFileName(name, "1.4.2-1", architecture, PackageCompression.Zstandard));
    }

    #endregion

    private static MemoryStream StreamOf(byte[] content) => new(content, writable: false);

    /// <summary>
    /// A readable stream that cannot seek, standing in for a request body.
    /// </summary>
    private sealed class NonSeekableStream(byte[] content) : MemoryStream(content, writable: false)
    {
        public override bool CanSeek => false;
    }

    #region File name parsing

    [TestCase("my-tool-1.4.2-1-x86_64.pkg.tar.zst", "my-tool", "1.4.2-1", Architectures.X86_64, PackageCompression.Zstandard)]
    [TestCase("my-tool-2:1.4.2-1-x86_64.pkg.tar.xz", "my-tool", "2:1.4.2-1", Architectures.X86_64, PackageCompression.Xz)]
    [TestCase("lib32-foo+bar-1.0-2.1-any.pkg.tar.gz", "lib32-foo+bar", "1.0-2.1", Architectures.Any, PackageCompression.Gzip)]
    [TestCase("a-1-1-aarch64.pkg.tar.bz2", "a", "1-1", "aarch64", PackageCompression.Bzip2)]
    [TestCase("a-1-aarch64.pkg.tar.zst", "a", "1", "aarch64", PackageCompression.Zstandard)]
    public void TryParseFileName_ReadsBackEveryPart(
        string fileName, string name, string version, string architecture, PackageCompression compression)
    {
        Assert.Multiple(() =>
        {
            Assert.That(_subject.TryParseFileName(fileName, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(new PackageFileName(name, version, architecture, compression)));
        });
    }

    [TestCase("my-tool", "1.4.2-1", Architectures.X86_64, PackageCompression.Zstandard)]
    [TestCase("my-tool", "2:1.4.2-1", Architectures.X86_64, PackageCompression.Xz)]
    [TestCase("lib32-foo+bar", "1.0-1", Architectures.Any, PackageCompression.Gzip)]
    [TestCase("a", "1", "aarch64", PackageCompression.Bzip2)]
    public void TryParseFileName_IsTheInverseOfDeriveFileName(
        string name, string version, string architecture, PackageCompression compression)
    {
        var fileName = _subject.DeriveFileName(name, version, architecture, compression);

        Assert.Multiple(() =>
        {
            Assert.That(_subject.TryParseFileName(fileName, out var parsed), Is.True);
            Assert.That(parsed, Is.EqualTo(new PackageFileName(name, version, architecture, compression)));
        });
    }

    [Test]
    public void TryParseFileName_ReadsTheCommittedFixturePackage()
    {
        Assert.Multiple(() =>
        {
            Assert.That(_subject.TryParseFileName(PackageFixtures.MinimalPackageFileName, out var parsed), Is.True);
            Assert.That(parsed!.Name, Is.EqualTo(PackageFixtures.MinimalPackageName));
            Assert.That(parsed.Version, Is.EqualTo(PackageFixtures.MinimalPackageVersion));
            Assert.That(parsed.Architecture, Is.EqualTo(PackageFixtures.MinimalPackageArchitecture));
        });
    }

    [TestCase("my-tool-1.4.2-1-x86_64.pkg.tar.zst.sig", TestName = "a package signature")]
    [TestCase("my-tool-1.4.2-1-x86_64.pkg.tar", TestName = "no compression extension")]
    [TestCase("my-tool-1.4.2-1-x86_64.pkg.tar.lz4", TestName = "an unrecognised compression extension")]
    [TestCase("my-tool-1.4.2-1-x86_64.tar.zst", TestName = "no .pkg.tar. suffix")]
    [TestCase("my-tool-1.4.2-1-x86_64", TestName = "no suffix at all")]
    [TestCase("my-tool-1.4.2-1-x86/64.pkg.tar.zst", TestName = "a bad architecture token")]
    [TestCase("my-tool-1.4.2-1-.pkg.tar.zst", TestName = "an empty architecture token")]
    [TestCase("my-tool-1.4.2-1-_x86_64.pkg.tar.zst", TestName = "an architecture that does not start alphanumerically")]
    [TestCase("my-tool-:1.4.2-x86_64.pkg.tar.zst", TestName = "a bad version")]
    [TestCase("x86_64.pkg.tar.zst", TestName = "no name or version")]
    [TestCase("-1.4.2-1-x86_64.pkg.tar.zst", TestName = "an empty name")]
    [TestCase("custom.db", TestName = "a sync database")]
    [TestCase("custom.db.tar.gz", TestName = "a sync database under its real name")]
    [TestCase("custom.db.sig", TestName = "a sync database signature")]
    [TestCase("custom.files", TestName = "a files database")]
    [TestCase("db", TestName = "the db subdirectory")]
    [TestCase("", TestName = "an empty file name")]
    [TestCase(".", TestName = "the current directory")]
    [TestCase("..", TestName = "the parent directory")]
    [TestCase("../my-tool-1.4.2-1-x86_64.pkg.tar.zst", TestName = "traversal with a slash")]
    [TestCase("..%2Fmy-tool-1.4.2-1-x86_64.pkg.tar.zst", TestName = "traversal with an encoded slash")]
    [TestCase("..\\my-tool-1.4.2-1-x86_64.pkg.tar.zst", TestName = "traversal with a backslash")]
    [TestCase("my-tool-1.4.2-1-x86_64.pkg.tar.zst\n", TestName = "a trailing newline")]
    [TestCase("my-tool\0-1.4.2-1-x86_64.pkg.tar.zst", TestName = "an embedded NUL")]
    [TestCase("my%00tool-1.4.2-1-x86_64.pkg.tar.zst", TestName = "an encoded NUL")]
    public void TryParseFileName_RejectsAnythingThatIsNotAPackageFileName(string fileName)
    {
        Assert.Multiple(() =>
        {
            Assert.That(_subject.TryParseFileName(fileName, out var parsed), Is.False);
            Assert.That(parsed, Is.Null);
        });
    }

    [Test]
    public void TryParseFileName_RejectsANameLongerThanTheStoredFileNameColumn()
    {
        var fileName = $"{new string('a', PackageValidationConstants.FileNameMaxLength)}-1.0-1-x86_64.pkg.tar.zst";

        Assert.That(_subject.TryParseFileName(fileName, out _), Is.False);
    }

    #endregion
}
