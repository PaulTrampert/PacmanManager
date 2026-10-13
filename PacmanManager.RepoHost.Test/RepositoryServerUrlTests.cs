using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests of <c>serverUrl</c> against a host started with <c>PublicUrl</c> set. The
/// request-root form, with it unset, is asserted by
/// <see cref="RepositoriesControllerTests.EveryRepositoryResponse_CarriesTheRequestRootServerUrl_WhenPublicUrlIsUnset"/>
/// against that fixture's default host.
/// </summary>
[TestFixture]
public class RepositoryServerUrlTests
{
    /// <summary>
    /// Deliberately unlike the address the tests reach the host at, so that a value built from the
    /// request instead could not pass.
    /// </summary>
    private const string PublicUrl = "https://packages.example.com/pm";

    private EndToEndTestFixture _fixture = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _fixture = new EndToEndTestFixture(new Dictionary<string, string> { ["PublicUrl"] = PublicUrl });
        await _fixture.StartAsync();
        _client = _fixture.HttpClient;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _fixture.AuthContainer!.GetBearerTokenAsync(_fixture.AuthContainer.DefaultCredentials));
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _fixture.DisposeAsync();
    }

    [Test]
    public async Task EveryRepositoryResponse_CarriesTheServerUrlBuiltFromPublicUrl()
    {
        await AssertEveryResponseCarriesAsync(_client, $"{PublicUrl}/pacman/$repo/$arch");
    }

    /// <summary>
    /// Creates, updates, fetches and lists a repository through <paramref name="client"/>, and asserts
    /// that the raw JSON of each response carries <paramref name="expected"/> as <c>serverUrl</c>.
    /// </summary>
    internal static async Task AssertEveryResponseCarriesAsync(HttpClient client, string expected)
    {
        var name = $"server-url-{Guid.NewGuid():N}";

        var created = await client.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = name, IsPublic = true });
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            await created.Content.ReadAsStringAsync());
        var createdJson = await ReadJsonAsync(created);
        var id = createdJson.GetProperty("id").GetGuid();

        var updated = await client.PutAsJsonAsync($"/api/v1/repositories/{id}",
            new WriteRepositoryRequest { Name = $"{name}-renamed", IsPublic = true });
        Assert.That(updated.StatusCode, Is.EqualTo(HttpStatusCode.OK), await updated.Content.ReadAsStringAsync());

        var fetched = await client.GetAsync($"/api/v1/repositories/{id}");
        Assert.That(fetched.StatusCode, Is.EqualTo(HttpStatusCode.OK));

        var listed = await client.GetAsync($"/api/v1/repositories?nameContains={name}");
        Assert.That(listed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var listedJson = await ReadJsonAsync(listed);

        var updatedJson = await ReadJsonAsync(updated);
        var fetchedJson = await ReadJsonAsync(fetched);
        Assert.Multiple(() =>
        {
            Assert.That(createdJson.GetProperty("serverUrl").GetString(), Is.EqualTo(expected), "POST");
            Assert.That(updatedJson.GetProperty("serverUrl").GetString(), Is.EqualTo(expected), "PUT");
            Assert.That(fetchedJson.GetProperty("serverUrl").GetString(), Is.EqualTo(expected), "GET by id");
            Assert.That(
                listedJson.GetProperty("results").EnumerateArray().Select(r => r.GetProperty("serverUrl").GetString()),
                Is.EqualTo(new[] { expected }), "listing");
        });
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>());
}
