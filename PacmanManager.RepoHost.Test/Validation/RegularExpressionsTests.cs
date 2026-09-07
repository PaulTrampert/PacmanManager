using System.Text.RegularExpressions;
using PacmanManager.RepoHost.Validation;

namespace PacmanManager.RepoHost.Test.Validation;

public class RegularExpressionsTests
{
    [TestCase("a", true)]
    [TestCase("A", true)]
    [TestCase("1", true)]
    [TestCase("@", true)]
    [TestCase("_", true)]
    [TestCase("+", true)]
    [TestCase("-", false)]
    [TestCase(".", false)]
    [TestCase("aa", true)]
    [TestCase("aA", true)]
    [TestCase("a1", true)]
    [TestCase("a@", true)]
    [TestCase("a_", true)]
    [TestCase("a+", true)]
    [TestCase("a-", false)]
    [TestCase("a.", false)]
    [TestCase("aa1", true)]
    [TestCase("aA1", true)]
    [TestCase("a11", true)]
    [TestCase("a@1", true)]
    [TestCase("a_1", true)]
    [TestCase("a+1", true)]
    [TestCase("a-1", true)]
    [TestCase("a.1", true)]
    [TestCase("a\n", false)]
    [TestCase("a\nb", false)]
    [TestCase("a\n/etc/passwd", false)]
    public void RepositoryName_MatchesRegularExpression(string repositoryName, bool isMatchExpected)
    {
        Assert.That(Regex.IsMatch(repositoryName, RegularExpressions.RepositoryName), Is.EqualTo(isMatchExpected));
    }

    [TestCase("1.0", true)]
    [TestCase("1.0-1", true)]
    [TestCase("1.4.2-1", true)]
    [TestCase("2:1.4.2-1", true)]
    [TestCase("0:1.0-1", true)]
    [TestCase("1.0-1.1", true)]
    [TestCase("1.0_rc1-1", true)]
    [TestCase("1.0+git20260101-1", true)]
    [TestCase("r123.abc0def-1", true)]
    [TestCase("1", true)]
    [TestCase("", false)]
    [TestCase("-1", false)]
    [TestCase(".1-1", false)]
    [TestCase("1.0-", false)]
    [TestCase("1.0-a", false)]
    [TestCase("1.0-1-1", false)]
    [TestCase(":1.0-1", false)]
    [TestCase("a:1.0-1", false)]
    [TestCase("1:2:1.0-1", false)]
    [TestCase("1.0 -1", false)]
    [TestCase("1.0/1-1", false)]
    [TestCase("1.0-1\n", false)]
    public void PackageVersion_MatchesRegularExpression(string version, bool isMatchExpected)
    {
        Assert.That(Regex.IsMatch(version, RegularExpressions.PackageVersion), Is.EqualTo(isMatchExpected));
    }

    [TestCase("x86_64", true)]
    [TestCase("any", true)]
    [TestCase("aarch64", true)]
    [TestCase("armv7h", true)]
    [TestCase("i686", true)]
    [TestCase("", false)]
    [TestCase("_x86_64", false)]
    [TestCase("x86 64", false)]
    [TestCase("x86_64/..", false)]
    [TestCase("x86_64\\..", false)]
    [TestCase("../x86_64", false)]
    [TestCase("x86_64\n", false)]
    public void PackageArchitecture_MatchesRegularExpression(string architecture, bool isMatchExpected)
    {
        Assert.That(Regex.IsMatch(architecture, RegularExpressions.PackageArchitecture), Is.EqualTo(isMatchExpected));
    }
}
