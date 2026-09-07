namespace LibAlpmSharp.Test;

/// <summary>
/// Tests for <see cref="AlpmVersion"/>, which is the ordering a pacman client applies to the same
/// two versions.
/// </summary>
[TestFixture]
public class AlpmVersionTests
{
    [TestCase("1.2.4-1", "1.2.3-4", Description = "A newer pkgver")]
    [TestCase("1.2.3-5", "1.2.3-4", Description = "A newer pkgrel")]
    [TestCase("1.10", "1.9", Description = "Version segments are numbers, not text")]
    [TestCase("1:1.0-1", "2.0-1", Description = "An epoch outranks everything to its right")]
    public void Compare_ReportsTheFirstVersionAsNewer(string newer, string older)
    {
        Assert.Multiple(() =>
        {
            Assert.That(AlpmVersion.Compare(newer, older), Is.Positive);
            Assert.That(AlpmVersion.Compare(older, newer), Is.Negative);
            Assert.That(AlpmVersion.IsNewerThan(newer, older), Is.True);
            Assert.That(AlpmVersion.IsNewerThan(older, newer), Is.False);
        });
    }

    [Test]
    public void Compare_ReportsIdenticalVersionsAsEqual()
    {
        Assert.Multiple(() =>
        {
            Assert.That(AlpmVersion.Compare("1.2.3-4", "1.2.3-4"), Is.Zero);
            Assert.That(AlpmVersion.IsNewerThan("1.2.3-4", "1.2.3-4"), Is.False,
                "The same version is not an upgrade of itself.");
        });
    }

    [Test]
    public void Compare_RejectsAVersionThatIsNotThere()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<ArgumentException>(() => AlpmVersion.Compare("", "1.2.3-4"));
            Assert.Throws<ArgumentNullException>(() => AlpmVersion.Compare("1.2.3-4", null!));
        });
    }
}
