using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests for the access token routes under <c>/api/v1/users/me/tokens</c>, against the
/// containerized API.
/// </summary>
/// <remarks>
/// Every test names its tokens with a fresh marker and filters the listing by it, so tests sharing
/// the fixture's users never see each other's tokens.
/// </remarks>
[TestFixture]
public class AccessTokensControllerTests
{
    private const string TokensRoute = "/api/v1/users/me/tokens";

    private EndToEndTestFixture _fixture = null!;
    private HttpClient _client = null!;
    private HttpClient _otherUsersClient = null!;
    private HttpClient _anonymousClient = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _fixture = new EndToEndTestFixture();
        await _fixture.StartAsync();
        _client = _fixture.HttpClient;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _fixture.AuthContainer!.GetBearerTokenAsync(_fixture.AuthContainer.DefaultCredentials));

        _otherUsersClient = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
        _otherUsersClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _fixture.AuthContainer.GetBearerTokenAsync(_fixture.AuthContainer.SecondaryCredentials));

        _anonymousClient = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _otherUsersClient.Dispose();
        _anonymousClient.Dispose();
        await _fixture.DisposeAsync();
    }

    #region Create

    [Test]
    public async Task Create_ReturnsTheSecretOnce_AndTheListingNeverReturnsIt()
    {
        var name = NewName();

        var create = await _client.PostAsJsonAsync(TokensRoute, new { name });
        var createBody = await create.Content.ReadAsStringAsync();
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created), createBody);
        var created = JsonSerializer.Deserialize<CreatedAccessToken>(createBody, JsonSerializerOptions.Web)!;

        var list = await _client.GetAsync($"{TokensRoute}?nameContains={name}");
        var listBody = await list.Content.ReadAsStringAsync();
        var listed = JsonSerializer.Deserialize<PaginatedResponse<AccessToken>>(listBody, JsonSerializerOptions.Web)!;

        Assert.Multiple(() =>
        {
            Assert.That(created.Secret, Does.StartWith("pms_"));
            Assert.That(JsonProperties(createBody), Does.Contain("secret"));
            Assert.That(list.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(listed.Results.Select(t => t.Id), Is.EqualTo(new[] { created.Id }));
            Assert.That(listBody, Does.Not.Contain(created.Secret), "the secret's value");
            Assert.That(listBody, Does.Not.Contain(created.Secret["pms_".Length..]), "the secret without its prefix");
            Assert.That(listBody, Does.Not.Contain("\"secret\""), "a secret field");
        });
    }

    [Test]
    public async Task Create_TheListingShowsTheSameUsernameTheCreateReturned()
    {
        var created = await CreateTokenAsync(_client, NewName());

        var listed = await ListAsync(_client, created.Name);

        Assert.Multiple(() =>
        {
            Assert.That(created.Username, Does.StartWith("pmt_"));
            Assert.That(listed.Results.Single().Username, Is.EqualTo(created.Username));
        });
    }

    [Test]
    public async Task Create_TheReturnedUsernameAndSecretAuthenticate_UntilTheTokenIsDeleted()
    {
        var created = await CreateTokenAsync(_client, NewName());
        using var basic = BasicClient(created.Username, created.Secret);

        // The listing requires authentication, so a 200 here is the Basic credential being accepted.
        var before = await basic.GetAsync(TokensRoute);
        var delete = await _client.DeleteAsync($"{TokensRoute}/{created.Id}");
        var after = await basic.GetAsync(TokensRoute);

        Assert.Multiple(() =>
        {
            Assert.That(before.StatusCode, Is.EqualTo(HttpStatusCode.OK), "before the delete");
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent), "the delete");
            Assert.That(after.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "after the delete");
        });
    }

    [Test]
    public async Task Create_ASecondTokenWithTheSameName_IsConflict()
    {
        var name = NewName();
        await CreateTokenAsync(_client, name);

        var response = await _client.PostAsJsonAsync(TokensRoute, new { name });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Create_ASecondTokenWhoseNameDiffersOnlyInCase_IsConflict()
    {
        var name = NewName();
        await CreateTokenAsync(_client, name);

        var response = await _client.PostAsJsonAsync(TokensRoute, new { name = name.ToUpperInvariant() });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task Create_WithAPastExpiry_IsBadRequest()
    {
        var name = NewName();

        var response = await _client.PostAsJsonAsync(TokensRoute,
            new { name, expiresAt = DateTimeOffset.UtcNow.AddDays(-1) });
        var listed = await ListAsync(_client, name);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(listed.Total, Is.Zero, "no token was minted");
        });
    }

    [Test]
    public async Task Create_WithAFutureExpiry_KeepsIt()
    {
        var expiresAt = DateTimeOffset.UtcNow.AddDays(30);

        var created = await CreateTokenAsync(_client, NewName(), expiresAt);

        Assert.That(created.ExpiresAt, Is.EqualTo(expiresAt).Within(TimeSpan.FromMilliseconds(1)));
    }

    [Test]
    public async Task Create_WithNoExpiry_ProducesATokenThatNeverExpires()
    {
        var name = NewName();

        var create = await _client.PostAsJsonAsync(TokensRoute, new { name });
        var createBody = await create.Content.ReadAsStringAsync();
        var listed = await ListAsync(_client, name);

        Assert.Multiple(() =>
        {
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created), createBody);
            Assert.That(JsonProperties(createBody), Does.Not.Contain("expiresAt"));
            Assert.That(listed.Results.Single().ExpiresAt, Is.Null);
        });
    }

    /// <summary>
    /// A Basic credential carries only a read scope, so it cannot be used to mint another token.
    /// </summary>
    [Test]
    public async Task Create_WithABasicCredential_IsForbidden()
    {
        var created = await CreateTokenAsync(_client, NewName());
        using var basic = BasicClient(created.Username, created.Secret);
        var name = NewName();

        var response = await basic.PostAsJsonAsync(TokensRoute, new { name });
        var listed = await ListAsync(_client, name);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
            Assert.That(listed.Total, Is.Zero, "no token was minted");
        });
    }

    #endregion

    #region Delete

    [Test]
    public async Task Delete_AnotherUsersToken_IsNotFound_AndLeavesItInPlace()
    {
        var theirs = await CreateTokenAsync(_otherUsersClient, NewName());

        var response = await _client.DeleteAsync($"{TokensRoute}/{theirs.Id}");
        var listed = await ListAsync(_otherUsersClient, theirs.Name);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(listed.Results.Select(t => t.Id), Is.EqualTo(new[] { theirs.Id }), "still there");
        });
    }

    [Test]
    public async Task Delete_ATokenThatDoesNotExist_IsNotFound()
    {
        var response = await _client.DeleteAsync($"{TokensRoute}/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region List

    [Test]
    public async Task List_Pages()
    {
        var prefix = NewName();
        foreach (var suffix in new[] { "a", "b", "c" })
        {
            await CreateTokenAsync(_client, $"{prefix}-{suffix}");
        }

        var first = await ListAsync(_client, prefix, "&sortBy=Name&pageSize=2");
        var second = await ListAsync(_client, prefix, "&sortBy=Name&pageSize=2&offset=2");

        Assert.Multiple(() =>
        {
            Assert.That(first.Total, Is.EqualTo(3));
            Assert.That(first.Results.Select(t => t.Name), Is.EqualTo(new[] { $"{prefix}-a", $"{prefix}-b" }));
            Assert.That(second.Offset, Is.EqualTo(2));
            Assert.That(second.Results.Select(t => t.Name), Is.EqualTo(new[] { $"{prefix}-c" }));
        });
    }

    [Test]
    public async Task List_FiltersByNameIgnoringCase()
    {
        var prefix = NewName();
        await CreateTokenAsync(_client, $"{prefix}-Laptop");
        await CreateTokenAsync(_client, $"{prefix}-desktop");

        var listed = await ListAsync(_client, $"{prefix}-LAP");

        Assert.That(listed.Results.Select(t => t.Name), Is.EqualTo(new[] { $"{prefix}-Laptop" }));
    }

    [Test]
    public async Task List_SortsNewestFirstByDefault_AndByNameWhenAsked()
    {
        var prefix = NewName();
        foreach (var suffix in new[] { "b", "c", "a" })
        {
            await CreateTokenAsync(_client, $"{prefix}-{suffix}");
        }

        var defaulted = await ListAsync(_client, prefix);
        var byName = await ListAsync(_client, prefix, "&sortBy=Name");
        var byNameDescending = await ListAsync(_client, prefix, "&sortBy=Name&direction=Descending");

        Assert.Multiple(() =>
        {
            Assert.That(defaulted.Results.Select(t => t.Name),
                Is.EqualTo(new[] { $"{prefix}-a", $"{prefix}-c", $"{prefix}-b" }), "newest first");
            Assert.That(byName.Results.Select(t => t.Name),
                Is.EqualTo(new[] { $"{prefix}-a", $"{prefix}-b", $"{prefix}-c" }), "by name");
            Assert.That(byNameDescending.Results.Select(t => t.Name),
                Is.EqualTo(new[] { $"{prefix}-c", $"{prefix}-b", $"{prefix}-a" }), "by name, descending");
        });
    }

    [Test]
    public async Task List_NeverShowsAnotherUsersToken()
    {
        var name = NewName();
        var theirs = await CreateTokenAsync(_otherUsersClient, name);

        var filtered = await ListAsync(_client, name);
        var everything = await _client.GetFromJsonAsync<PaginatedResponse<AccessToken>>($"{TokensRoute}?pageSize=500");

        Assert.Multiple(() =>
        {
            Assert.That(filtered.Total, Is.Zero, "filtered by the other user's token name");
            Assert.That(everything!.Results.Select(t => t.Id), Does.Not.Contain(theirs.Id), "unfiltered");
        });
    }

    #endregion

    #region Unauthenticated

    [Test]
    public async Task List_Unauthenticated_IsUnauthorized()
    {
        var response = await _anonymousClient.GetAsync(TokensRoute);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Create_Unauthenticated_IsUnauthorized()
    {
        var name = NewName();

        var response = await _anonymousClient.PostAsJsonAsync(TokensRoute, new { name });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Delete_Unauthenticated_IsUnauthorized()
    {
        var mine = await CreateTokenAsync(_client, NewName());

        var response = await _anonymousClient.DeleteAsync($"{TokensRoute}/{mine.Id}");
        var listed = await ListAsync(_client, mine.Name);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(listed.Total, Is.EqualTo(1), "still there");
        });
    }

    #endregion

    #region Helpers

    /// <summary>
    /// A token name no other test uses, so a listing filtered by it sees only this test's tokens.
    /// </summary>
    private static string NewName() => $"tok-{Guid.NewGuid():N}"[..20];

    private static async Task<CreatedAccessToken> CreateTokenAsync(
        HttpClient client,
        string name,
        DateTimeOffset? expiresAt = null)
    {
        var response = await client.PostAsJsonAsync(TokensRoute, new CreateAccessTokenRequest { Name = name, ExpiresAt = expiresAt });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created), await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CreatedAccessToken>())!;
    }

    private static async Task<PaginatedResponse<AccessToken>> ListAsync(HttpClient client, string nameContains, string query = "")
    {
        var response = await client.GetAsync($"{TokensRoute}?nameContains={Uri.EscapeDataString(nameContains)}{query}");
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<PaginatedResponse<AccessToken>>())!;
    }

    /// <summary>
    /// The names of the top-level properties of a JSON object, as the API wrote them.
    /// </summary>
    private static IEnumerable<string> JsonProperties(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.EnumerateObject().Select(p => p.Name).ToList();
    }

    private HttpClient BasicClient(string username, string password)
    {
        var client = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        return client;
    }

    #endregion
}
