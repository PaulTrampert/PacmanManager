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
        "^pacman-manager:(repositories|packages|users|tokens|\\*):(read|create|update|delete|\\*)$");

    /// <summary>
    /// The 21 values listed in <c>docs/basic-auth.md</c>, "Configuring another identity provider",
    /// in order.
    /// </summary>
    private static readonly string[] Expected =
    [
        "pacman-manager:*:*",
        "pacman-manager:repositories:read",
        "pacman-manager:repositories:create",
        "pacman-manager:repositories:update",
        "pacman-manager:repositories:delete",
        "pacman-manager:packages:read",
        "pacman-manager:packages:create",
        "pacman-manager:packages:delete",
        "pacman-manager:users:read",
        "pacman-manager:users:update",
        "pacman-manager:tokens:read",
        "pacman-manager:tokens:create",
        "pacman-manager:tokens:delete",
        "pacman-manager:repositories:*",
        "pacman-manager:packages:*",
        "pacman-manager:users:*",
        "pacman-manager:tokens:*",
        "pacman-manager:*:read",
        "pacman-manager:*:create",
        "pacman-manager:*:update",
        "pacman-manager:*:delete",
    ];

    [Test]
    public void All_IsExactlyTheDocumentedValues_InOrder()
    {
        Assert.That(ScopeValues.All, Is.EqualTo(Expected));
        Assert.That(ScopeValues.All, Has.Count.EqualTo(21));
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

    [Test]
    public void Entities_MapsEachEntityToItsActions()
    {
        Assert.That(ScopeValues.Entities.Keys, Is.EqualTo(new[] { "repositories", "packages", "users", "tokens" }));
        Assert.Multiple(() =>
        {
            Assert.That(ScopeValues.Entities["repositories"], Is.EqualTo(new[] { "read", "create", "update", "delete" }));
            Assert.That(ScopeValues.Entities["packages"], Is.EqualTo(new[] { "read", "create", "delete" }));
            Assert.That(ScopeValues.Entities["users"], Is.EqualTo(new[] { "read", "update" }));
            Assert.That(ScopeValues.Entities["tokens"], Is.EqualTo(new[] { "read", "create", "delete" }));
        });
    }

    [Test]
    public void Actions_IsTheUnionOfEveryEntitysActions()
    {
        Assert.That(ScopeValues.Actions, Is.EqualTo(new[] { "read", "create", "update", "delete" }));
    }

    [Test]
    public void All_NamesNoActionItsEntityLacks()
    {
        Assert.Multiple(() =>
        {
            foreach (var value in ScopeValues.All)
            {
                var (entity, action) = EntityAndAction(value);
                if (entity == ScopeValues.Wildcard || action == ScopeValues.Wildcard)
                {
                    continue;
                }

                Assert.That(ScopeValues.Entities[entity], Does.Contain(action), value);
            }
        });
    }

    [TestCase("pacman-manager:packages:update")]
    [TestCase("pacman-manager:users:create")]
    [TestCase("pacman-manager:users:delete")]
    [TestCase("pacman-manager:tokens:update")]
    [TestCase("pacman-manager:repositories:write")]
    [TestCase("pacman-manager:packages:publish")]
    [TestCase("pacman-manager:*:write")]
    [TestCase("pacman-manager:*:publish")]
    public void All_DoesNotContainAValueOutsideTheVocabulary(string value)
    {
        Assert.That(ScopeValues.All, Does.Not.Contain(value));
    }

    [TestCase("repositories", "read", "pacman-manager:repositories:read")]
    [TestCase("packages", "create", "pacman-manager:packages:create")]
    [TestCase("repositories", "*", "pacman-manager:repositories:*")]
    [TestCase("*", "read", "pacman-manager:*:read")]
    public void For_ComposesTheValue(string entity, string action, string expected)
    {
        Assert.That(ScopeValues.For(entity, action), Is.EqualTo(expected));
        Assert.That(ScopeValues.All, Does.Contain(expected));
    }

    private static (string Entity, string Action) EntityAndAction(string value)
    {
        var parts = value.Split(':');
        return (parts[1], parts[2]);
    }
}
