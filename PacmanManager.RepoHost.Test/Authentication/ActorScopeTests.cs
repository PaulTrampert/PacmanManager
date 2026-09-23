using Microsoft.Extensions.Logging;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.TestUtils;
using static PacmanManager.RepoHost.Authentication.ScopeValues;

namespace PacmanManager.RepoHost.Test.Authentication;

/// <summary>
/// Pins the parser of the <c>scope</c> claim and what the parsed scope permits. Absent means denied,
/// so a mistake here is silent: it shows up as a credential that can do more than it was issued.
/// </summary>
[TestFixture]
public class ActorScopeTests
{
    private TestOutputLogger<ActorScopeTests> _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = new TestOutputLogger<ActorScopeTests>();
    }

    private ActorScope Parse(string? claim) => ActorScope.Parse(claim, _logger);

    #region Values of ours

    [Test]
    public void Parse_AnEntityAndAnAction_PermitsExactlyThat()
    {
        var scope = Parse("pacman-manager:repositories:read");

        Assert.Multiple(() =>
        {
            Assert.That(scope.Permits(EntityNames.Repositories, ActionNames.Read), Is.True);
            Assert.That(scope.Permits(EntityNames.Repositories, ActionNames.Update), Is.False);
            Assert.That(scope.Permits(EntityNames.Packages, ActionNames.Read), Is.False);
        });
    }

    [Test]
    public void Parse_AWildcardEntity_PermitsTheActionOnEveryEntity()
    {
        var scope = Parse("pacman-manager:*:read");

        Assert.Multiple(() =>
        {
            foreach (var entity in ScopeValues.Entities.Keys)
            {
                Assert.That(scope.Permits(entity, ActionNames.Read), Is.True, entity);
                Assert.That(scope.Permits(entity, ActionNames.Delete), Is.False, entity);
            }
        });
    }

    [Test]
    public void Parse_AWildcardAction_PermitsEveryActionOnTheEntity()
    {
        var scope = Parse("pacman-manager:packages:*");

        Assert.Multiple(() =>
        {
            foreach (var action in ScopeValues.Entities[EntityNames.Packages])
            {
                Assert.That(scope.Permits(EntityNames.Packages, action), Is.True, action);
            }

            Assert.That(scope.Permits(EntityNames.Repositories, ActionNames.Read), Is.False);
        });
    }

    [Test]
    public void Parse_BothWildcards_PermitsEverything()
    {
        var scope = Parse(Everything);

        Assert.Multiple(() =>
        {
            Assert.That(scope, Is.EqualTo(ActorScope.Unrestricted));
            foreach (var (entity, actions) in ScopeValues.Entities)
            {
                foreach (var action in actions)
                {
                    Assert.That(scope.Permits(entity, action), Is.True, $"{entity}:{action}");
                }
            }
        });
    }

    [Test]
    public void Parse_SeveralValues_AreOrd()
    {
        var scope = Parse("pacman-manager:repositories:read pacman-manager:packages:create");

        Assert.Multiple(() =>
        {
            Assert.That(scope.Permits(EntityNames.Repositories, ActionNames.Read), Is.True);
            Assert.That(scope.Permits(EntityNames.Packages, ActionNames.Create), Is.True);
            Assert.That(scope.Permits(EntityNames.Packages, ActionNames.Read), Is.False);
        });
    }

    [Test]
    public void Parse_EveryValueTheGrammarAllows_IsKept()
    {
        foreach (var value in All)
        {
            Assert.That(Parse(value), Is.Not.EqualTo(ActorScope.Empty), value);
        }

        Assert.That(_logger.LogEvents, Is.Empty);
    }

    [Test]
    public void Parse_ExtraWhitespace_IsTolerated()
    {
        Assert.That(
            Parse("  pacman-manager:repositories:read   pacman-manager:packages:read "),
            Is.EqualTo(Parse("pacman-manager:repositories:read pacman-manager:packages:read")));
    }

    #endregion

    #region Malformed values of ours are dropped and logged

    [TestCase("pacman-manager:widgets:read", TestName = "Parse_AnUnknownEntity_IsDropped")]
    [TestCase("pacman-manager:repositories:write", TestName = "Parse_AnUnknownAction_IsDropped")]
    [TestCase("pacman-manager:repositories:publish", TestName = "Parse_ARetiredAction_IsDropped")]
    [TestCase("pacman-manager:users:delete", TestName = "Parse_UsersDelete_WhichUsersDoNotHave_IsDropped")]
    [TestCase("pacman-manager:users:create", TestName = "Parse_UsersCreate_WhichUsersDoNotHave_IsDropped")]
    [TestCase("pacman-manager:packages:update", TestName = "Parse_PackagesUpdate_WhichPackagesDoNotHave_IsDropped")]
    [TestCase("pacman-manager:tokens:update", TestName = "Parse_TokensUpdate_WhichTokensDoNotHave_IsDropped")]
    [TestCase("pacman-manager:*:write", TestName = "Parse_AWildcardEntityWithAnUnknownAction_IsDropped")]
    [TestCase("pacman-manager:repositories", TestName = "Parse_TooFewSegments_IsDropped")]
    [TestCase("pacman-manager:", TestName = "Parse_ThePrefixAlone_IsDropped")]
    [TestCase("pacman-manager:repositories:read:extra", TestName = "Parse_TooManySegments_IsDropped")]
    [TestCase("pacman-manager:Repositories:read", TestName = "Parse_TheWrongCase_IsDropped")]
    public void Parse_AMalformedValueOfOurs_IsDroppedAndLogged(string value)
    {
        var scope = Parse(value);

        Assert.Multiple(() =>
        {
            Assert.That(scope, Is.EqualTo(ActorScope.Empty));
            var logEvent = _logger.LogEvents.Single();
            Assert.That(logEvent.LogLevel, Is.EqualTo(LogLevel.Warning));
            Assert.That(logEvent.Message, Does.Contain(value));
        });
    }

    [Test]
    public void Parse_AMalformedValue_DoesNotTakeTheOthersWithIt()
    {
        var scope = Parse("pacman-manager:users:delete pacman-manager:users:read");

        Assert.Multiple(() =>
        {
            Assert.That(scope.Permits(EntityNames.Users, ActionNames.Read), Is.True);
            Assert.That(scope.Permits(EntityNames.Users, ActionNames.Delete), Is.False);
        });
    }

    #endregion

    #region Values that are not ours are ignored

    [TestCase("repositories:read", TestName = "Parse_AValueMissingThePrefix_IsIgnored")]
    [TestCase("other-api:repositories:read", TestName = "Parse_AValueWithTheWrongPrefix_IsIgnored")]
    [TestCase("pacman-managerx:repositories:read", TestName = "Parse_AValueWithALongerPrefix_IsIgnored")]
    [TestCase("pacman-manager", TestName = "Parse_TheBareAudience_IsIgnored")]
    [TestCase("openid", TestName = "Parse_OpenId_IsIgnored")]
    public void Parse_AValueThatIsNotOurs_IsIgnoredSilently(string value)
    {
        Assert.Multiple(() =>
        {
            Assert.That(Parse(value), Is.EqualTo(ActorScope.Empty));
            Assert.That(_logger.LogEvents, Is.Empty);
        });
    }

    [Test]
    public void Parse_AClaimCarryingNoneOfOurs_IsEmpty()
    {
        // The claim every token the realm issued before scopes were enforced carried. This is the
        // test that pins the direction of the default: none of ours means nothing permitted.
        var scope = Parse("openid profile email roles pacman-manager");

        Assert.Multiple(() =>
        {
            Assert.That(scope, Is.EqualTo(ActorScope.Empty));
            Assert.That(scope.PermitsAnyActionButRead, Is.False);
            foreach (var (entity, actions) in ScopeValues.Entities)
            {
                foreach (var action in actions)
                {
                    Assert.That(scope.Permits(entity, action), Is.False, $"{entity}:{action}");
                }
            }
        });
    }

    [Test]
    public void Parse_AClaimMixingOursWithSomebodyElses_KeepsOursAndIgnoresTheRest()
    {
        var scope = Parse("openid pacman-manager:repositories:read");

        Assert.Multiple(() =>
        {
            Assert.That(scope, Is.EqualTo(Parse("pacman-manager:repositories:read")));
            Assert.That(scope.Values, Is.EquivalentTo(new[] { "pacman-manager:repositories:read" }));
        });
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void Parse_NoClaim_IsEmpty(string? claim)
    {
        Assert.That(Parse(claim), Is.EqualTo(ActorScope.Empty));
    }

    #endregion

    #region What a scope permits

    [TestCase("pacman-manager:*:read", ExpectedResult = false)]
    [TestCase("pacman-manager:repositories:read pacman-manager:packages:read", ExpectedResult = false)]
    [TestCase("pacman-manager:packages:create", ExpectedResult = true)]
    [TestCase("pacman-manager:users:*", ExpectedResult = true)]
    [TestCase("pacman-manager:*:*", ExpectedResult = true)]
    public bool PermitsAnyActionButRead(string claim) => Parse(claim).PermitsAnyActionButRead;

    [Test]
    public void Empty_PermitsNothing()
    {
        Assert.Multiple(() =>
        {
            Assert.That(ActorScope.Empty.Permits(EntityNames.Repositories, ActionNames.Read), Is.False);
            Assert.That(ActorScope.Empty.PermitsAnyActionButRead, Is.False);
            Assert.That(ActorScope.Empty.Values, Is.Empty);
        });
    }

    [Test]
    public void Unrestricted_IsEverything()
    {
        Assert.That(ActorScope.Unrestricted.Values, Is.EqualTo(new[] { Everything }));
    }

    [Test]
    public void Equality_IgnoresTheOrderOfTheClaim()
    {
        var a = Parse("pacman-manager:repositories:read pacman-manager:packages:read");
        var b = Parse("pacman-manager:packages:read pacman-manager:repositories:read");

        Assert.Multiple(() =>
        {
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a.ToString(), Is.EqualTo(b.ToString()));
        });
    }

    #endregion
}
