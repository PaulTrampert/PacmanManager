using System.Text.RegularExpressions;
using PacmanManager.RepoHost.Authentication;

namespace PacmanManager.RepoHost.Test.Authentication;

/// <summary>
/// Pins the vocabulary of the <c>scope</c> grammar: every value it allows, and nothing else.
/// </summary>
[TestFixture]
public class ScopeValuesTests
{
    private static readonly Regex Grammar = new(
        "^pacman-manager:(repositories|packages|users|tokens|\\*):(read|create|write|delete|publish|\\*)$");

    [Test]
    public void All_HasEveryCombinationOfEntityAndAction_IncludingWildcards()
    {
        // Assert: (4 entities + wildcard) x (5 actions + wildcard).
        Assert.That(ScopeValues.All, Has.Count.EqualTo(30));
        Assert.That(ScopeValues.All, Is.Unique);
    }

    [Test]
    public void All_MatchesTheGrammar()
    {
        Assert.That(ScopeValues.All, Has.All.Matches(Grammar));
    }

    [Test]
    public void All_StartsWithEverything()
    {
        Assert.That(ScopeValues.All[0], Is.EqualTo("pacman-manager:*:*"));
        Assert.That(ScopeValues.Everything, Is.EqualTo("pacman-manager:*:*"));
    }

    [TestCase("repositories", "read", "pacman-manager:repositories:read")]
    [TestCase("packages", "publish", "pacman-manager:packages:publish")]
    [TestCase("repositories", "*", "pacman-manager:repositories:*")]
    [TestCase("*", "read", "pacman-manager:*:read")]
    public void For_ComposesTheValue(string entity, string action, string expected)
    {
        Assert.That(ScopeValues.For(entity, action), Is.EqualTo(expected));
        Assert.That(ScopeValues.All, Does.Contain(expected));
    }
}
