using System.Text.Json.Nodes;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Authentication;

/// <summary>
/// Checks the registrations in <c>keycloak/localdev.json</c> that make the realm emit this API's
/// <c>scope</c> values. <see cref="KeycloakScopeTests"/> asserts the tokens themselves; this fixture
/// fails fast, without a container, when the file and <see cref="ScopeValues"/> drift apart.
/// </summary>
[TestFixture]
public class LocalDevRealmTests
{
    private JsonNode _realm = null!;

    [OneTimeSetUp]
    public void LoadRealm()
    {
        var path = Path.Combine(DirUtils.FindSolutionDirectory(), "keycloak", "localdev.json");
        _realm = JsonNode.Parse(File.ReadAllText(path))!;
    }

    [Test]
    public void EveryScopeValue_IsRegisteredAsAClientScope_IncludedInTheTokenScope()
    {
        var clientScopes = _realm["clientScopes"]!.AsArray()
            .ToDictionary(s => s!["name"]!.GetValue<string>(), s => s!);

        Assert.Multiple(() =>
        {
            foreach (var value in ScopeValues.All)
            {
                Assert.That(clientScopes, Does.ContainKey(value));
                if (clientScopes.TryGetValue(value, out var scope))
                {
                    Assert.That(scope["attributes"]?["include.in.token.scope"]?.GetValue<string>(),
                        Is.EqualTo("true"), value);
                }
            }
        });
    }

    [Test]
    public void NoClientScopeOfOurs_IsOutsideTheVocabulary()
    {
        var ours = _realm["clientScopes"]!.AsArray()
            .Select(s => s!["name"]!.GetValue<string>())
            .Where(name => name.StartsWith($"{ScopeValues.Audience}:"));

        Assert.That(ours, Is.SubsetOf(ScopeValues.All));
    }

    [Test]
    public void NoScopeListOfOurs_NamesAValueOutsideTheVocabulary()
    {
        var lists = new Dictionary<string, JsonNode>
        {
            ["defaultDefaultClientScopes"] = _realm["defaultDefaultClientScopes"]!,
            ["defaultOptionalClientScopes"] = _realm["defaultOptionalClientScopes"]!,
        };
        foreach (var client in _realm["clients"]!.AsArray())
        {
            var clientId = client!["clientId"]!.GetValue<string>();
            foreach (var key in new[] { "defaultClientScopes", "optionalClientScopes" })
            {
                if (client[key] is { } list)
                {
                    lists[$"{clientId}.{key}"] = list;
                }
            }
        }

        Assert.Multiple(() =>
        {
            foreach (var (where, list) in lists)
            {
                Assert.That(Strings(list).Where(name => name.StartsWith($"{ScopeValues.Audience}:")),
                    Is.SubsetOf(ScopeValues.All), where);
            }
        });
    }

    [Test]
    public void Everything_IsADefaultScope_OnTheInteractiveClientsAndTheRealm()
    {
        Assert.Multiple(() =>
        {
            Assert.That(DefaultScopes("pacman-manager-web"), Does.Contain(ScopeValues.Everything));
            Assert.That(DefaultScopes("pacman-manager-swagger"), Does.Contain(ScopeValues.Everything));
            Assert.That(Strings(_realm["defaultDefaultClientScopes"]!), Does.Contain(ScopeValues.Everything));
        });
    }

    [Test]
    public void EveryNarrowerValue_IsARealmDefaultOptionalScope()
    {
        Assert.That(Strings(_realm["defaultOptionalClientScopes"]!),
            Is.SupersetOf(ScopeValues.All.Except([ScopeValues.Everything])));
    }

    [Test]
    public void TheScopedClient_GetsTheAudienceButNotEverything()
    {
        var client = Client("pacman-manager-scoped");

        Assert.Multiple(() =>
        {
            Assert.That(Strings(client["defaultClientScopes"]!), Does.Contain(ScopeValues.Audience));
            Assert.That(Strings(client["defaultClientScopes"]!), Does.Not.Contain(ScopeValues.Everything));
            Assert.That(Strings(client["optionalClientScopes"]!), Does.Not.Contain(ScopeValues.Everything));
            Assert.That(Strings(client["optionalClientScopes"]!),
                Is.SupersetOf(ScopeValues.All.Except([ScopeValues.Everything])));
        });
    }

    private JsonNode Client(string clientId) => _realm["clients"]!.AsArray()
        .Single(c => c!["clientId"]!.GetValue<string>() == clientId)!;

    private IEnumerable<string> DefaultScopes(string clientId) => Strings(Client(clientId)["defaultClientScopes"]!);

    private static IEnumerable<string> Strings(JsonNode array) =>
        array.AsArray().Select(n => n!.GetValue<string>()).ToList();
}
