using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Validation;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Validation;

public class RegularExpressionsTests
{
    /// <summary>
    /// The repository-name cases from the fixture the web app's parity test reads too, so a name
    /// added there is checked against both the API's pattern and the web form's.
    /// </summary>
    private static IEnumerable<TestCaseData> RepositoryNameCases()
    {
        using var stream = File.OpenRead(ValidationFixtures.RepositoryNamesPath);
        var cases = JsonSerializer.Deserialize<RepositoryNameFixture>(stream)
            ?? throw new InvalidOperationException($"{ValidationFixtures.RepositoryNamesPath} is empty.");

        foreach (var name in cases.Accepted)
        {
            yield return new TestCaseData(name, true);
        }

        foreach (var name in cases.Rejected)
        {
            yield return new TestCaseData(name, false);
        }
    }

    private sealed record RepositoryNameFixture(
        [property: JsonPropertyName("accepted")] IEnumerable<string> Accepted,
        [property: JsonPropertyName("rejected")] IEnumerable<string> Rejected);

    [TestCaseSource(nameof(RepositoryNameCases))]
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

    [TestCase(Architectures.X86_64, true)]
    [TestCase(Architectures.Any, true)]
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
