using LibAlpmSharp.Marshall;

namespace LibAlpmSharp.Test.LibAlpmTests;

public class LibAlpmConstructorTests
{
    [Test]
    public void Constructor_ValidParameters_DoesNotThrow()
    {
        // Arrange
        var root = "/";
        var dbpath = "/var/lib/pacman/";

        // Act & Assert
        Assert.DoesNotThrow(() =>
        {
            using var alpm = new LibAlpm(root, dbpath);
        });
    }
    
    [Test]
    public void Constructor_InvalidParameters_ThrowsLibAlpmException()
    {
        // Arrange
        var root = "/invalid/root";
        var dbpath = "/invalid/dbpath";

        // Act & Assert
        Assert.Multiple(() =>
        {
            var ex = Assert.Throws<LibAlpmException>(() =>
            {
                using var alpm = new LibAlpm(root, dbpath);
            });
            
            Assert.That(ex!.ErrorCode, Is.Not.EqualTo(AlpmErrno.ALPM_ERR_OK));
            Assert.That(ex!.Message, Is.EqualTo("could not find or read directory"));
        });
    }
}