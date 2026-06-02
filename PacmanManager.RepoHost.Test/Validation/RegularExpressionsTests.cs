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
    public void RepositoryName_MatchesRegularExpression(string repositoryName, bool isMatchExpected)
    {
        Assert.That(Regex.IsMatch(repositoryName, RegularExpressions.RepositoryName), Is.EqualTo(isMatchExpected));
    }
}