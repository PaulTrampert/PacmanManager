using LibAlpmSharp;
using Moq;
using PacmanManager.Packages.Exceptions;
using PacmanManager.Packages.LibAlpm;

namespace PacmanManager.Packages.Test.LibAlpm;

/// <summary>
/// Tests for <see cref="PackageFileReader"/>.
/// </summary>
[TestFixture]
public class PackageFileReaderTests
{
    [Test]
    public void Constructing_DoesNotInitializeLibAlpm()
    {
        // Arrange
        var libAlpm = new Lazy<ILibAlpm>(() =>
            throw new InvalidOperationException("Only reading a package may initialize libalpm."));

        // Act
        _ = new PackageFileReader(libAlpm);

        // Assert
        Assert.That(libAlpm.IsValueCreated, Is.False);
    }

    [Test]
    public void Read_ReturnsThePackageLibalpmLoaded()
    {
        // Arrange
        var package = Mock.Of<IPackage>();
        var libAlpm = new Mock<ILibAlpm>();
        libAlpm.Setup(a => a.LoadPackageFile("/data/tmp/upload", It.IsAny<bool>(), It.IsAny<int>())).Returns(package);
        var reader = new PackageFileReader(new Lazy<ILibAlpm>(() => libAlpm.Object));

        // Act
        var result = reader.Read("/data/tmp/upload");

        // Assert
        Assert.That(result, Is.SameAs(package));
    }

    [Test]
    public void Read_RestatesALibalpmFailureAsAnUnreadablePackage()
    {
        // A file libalpm cannot open is a rejected upload, not a server error.
        // Arrange
        var failure = new AlpmException("not a package");
        var libAlpm = new Mock<ILibAlpm>();
        libAlpm.Setup(a => a.LoadPackageFile(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<int>())).Throws(failure);
        var reader = new PackageFileReader(new Lazy<ILibAlpm>(() => libAlpm.Object));

        // Act & Assert
        var thrown = Assert.Throws<UnreadablePackageException>(() => reader.Read("/data/tmp/upload"));
        Assert.That(thrown!.InnerException, Is.SameAs(failure));
    }
}
