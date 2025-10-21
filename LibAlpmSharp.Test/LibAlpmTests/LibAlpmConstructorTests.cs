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
}