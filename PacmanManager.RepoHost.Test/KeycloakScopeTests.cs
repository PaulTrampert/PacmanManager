using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Networks;
using Microsoft.IdentityModel.JsonWebTokens;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Test.Containers;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests that the <c>localdev</c> realm issues this API's <c>scope</c> values, asserted by
/// decoding the tokens it mints. Only Keycloak is started: the subject is the realm, not the API.
/// </summary>
[TestFixture]
public class KeycloakScopeTests
{
    private INetwork _network = null!;
    private KeycloakContainer _keycloak = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _network = new NetworkBuilder()
            .WithName($"pacmanmanager-test-network-{Guid.NewGuid():N}")
            .WithCleanUp(true)
            .Build();
        _keycloak = new KeycloakContainer(_network, DirUtils.FindSolutionDirectory());
        await _keycloak.StartAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _keycloak.DisposeAsync();
        await _network.DisposeAsync();
    }

    [Test]
    public async Task InteractiveClient_TokenCarriesEverything_WithoutAskingForIt()
    {
        // Act: the same request the E2E fixtures make, which names no value of ours.
        var scope = ScopeOf(await _keycloak.GetBearerTokenAsync(_keycloak.DefaultCredentials));

        // Assert
        Assert.That(scope, Does.Contain(ScopeValues.Everything));
        Assert.That(scope, Does.Contain(ScopeValues.Audience));
    }

    [Test]
    public async Task InteractiveClient_TokenCarriesEverything_WhenOnlyOpenIdIsRequested()
    {
        var scope = ScopeOf(await _keycloak.GetBearerTokenAsync(
            _keycloak.DefaultCredentials, KeycloakContainer.SwaggerClientId, "openid"));

        Assert.That(scope, Does.Contain(ScopeValues.Everything));
    }

    [Test]
    public async Task ScopedClient_TokenCarriesNoneOfOurs_WhenNoneAreRequested()
    {
        var token = await _keycloak.GetBearerTokenAsync(
            _keycloak.DefaultCredentials, KeycloakContainer.ScopedClientId, "openid");
        var scope = ScopeOf(token);

        Assert.Multiple(() =>
        {
            Assert.That(scope.Where(v => v.StartsWith($"{ScopeValues.Audience}:")), Is.Empty);
            Assert.That(new JsonWebToken(token).Audiences, Does.Contain(ScopeValues.Audience));
        });
    }

    [Test]
    public async Task ScopedClient_TokenCarriesExactlyTheValuesRequested()
    {
        var requested = new[]
        {
            "pacman-manager:*:read",
            "pacman-manager:packages:create",
            "pacman-manager:repositories:*",
        };

        var scope = ScopeOf(await _keycloak.GetBearerTokenAsync(
            _keycloak.DefaultCredentials, KeycloakContainer.ScopedClientId, $"openid {string.Join(' ', requested)}"));

        Assert.That(scope.Where(v => v.StartsWith($"{ScopeValues.Audience}:")), Is.EquivalentTo(requested));
    }

    [Test]
    public async Task ScopedClient_CanBeIssuedEveryNarrowerValue()
    {
        var narrower = ScopeValues.All.Except([ScopeValues.Everything]).ToList();

        var scope = ScopeOf(await _keycloak.GetBearerTokenAsync(
            _keycloak.DefaultCredentials, KeycloakContainer.ScopedClientId, $"openid {string.Join(' ', narrower)}"));

        Assert.That(scope.Where(v => v.StartsWith($"{ScopeValues.Audience}:")), Is.EquivalentTo(narrower));
    }

    [Test]
    public void ScopedClient_CannotBeIssuedEverything()
    {
        Assert.That(
            () => _keycloak.GetBearerTokenAsync(
                _keycloak.DefaultCredentials, KeycloakContainer.ScopedClientId, $"openid {ScopeValues.Everything}"),
            Throws.Exception.With.Message.Contains("invalid_scope"));
    }

    private static IReadOnlyList<string> ScopeOf(string token) =>
        new JsonWebToken(token).GetClaim("scope").Value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
}
