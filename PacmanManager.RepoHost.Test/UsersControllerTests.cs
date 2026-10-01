using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests for the Users API: its read routes, and the caller's own write routes.
/// These tests run the application in a Docker container and make real HTTP requests.
/// </summary>
/// <remarks>
/// The anonymous routes are checked against the raw response body, not a deserialised model: a
/// <see cref="PublicUserInfo"/> would silently drop an email property the server had started sending,
/// which is exactly the regression those assertions are there to catch.
/// </remarks>
[TestFixture]
public class UsersControllerTests
{
    private EndToEndTestFixture _fixture = null!;
    private HttpClient _client = null!;
    private HttpClient _otherUsersClient = null!;
    private HttpClient _anonymousClient = null!;

    /// <summary>
    /// Marks the users this fixture seeds, so the listing tests can filter down to them alone.
    /// Mixed case, so that the filter test can search with a term whose case differs.
    /// </summary>
    private const string SeedMarker = "SeededUser";

    /// <summary>The seeded users' display names, in the order an ascending sort returns them.</summary>
    private static readonly string[] SeededNamesAscending =
    [
        $"{SeedMarker} Alpha",
        $"{SeedMarker} Bravo",
        $"{SeedMarker} Charlie",
    ];

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

        // Seeded out of order, so that a listing returning insertion order cannot pass for a sorted one.
        await using var db = _fixture.CreateDbContext();
        foreach (var name in new[] { SeededNamesAscending[2], SeededNamesAscending[0], SeededNamesAscending[1] })
        {
            db.Users.Add(new User
            {
                DisplayName = name,
                NormalizedDisplayName = name.ToLowerInvariant(),
                Email = $"{name.Replace(' ', '.').ToLowerInvariant()}@example.com",
            });
        }

        await db.SaveChangesAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _anonymousClient.Dispose();
        _otherUsersClient.Dispose();
        await _fixture.DisposeAsync();
    }

    #region Me

    [Test]
    public async Task GetMe_ReturnsTheAuthenticatedUser()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/users/me");
        var me = await response.Content.ReadFromJsonAsync<CurrentUser>();
        var other = await _otherUsersClient.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(me!.Email, Is.EqualTo("test@example.com"));
            Assert.That(me.Id, Is.Not.EqualTo(Guid.Empty));
            Assert.That(me.DisplayName, Is.Not.Empty);
            Assert.That(other!.Email, Is.EqualTo("other@example.com"), "each caller gets their own record");
            Assert.That(other.Id, Is.Not.EqualTo(me.Id));
        });
    }

    [Test]
    public async Task GetMe_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await _anonymousClient.GetAsync("/api/v1/users/me");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    #endregion

    #region PatchMe

    [Test]
    public async Task PatchMe_WithDisplayName_ChangesIt_VisiblyFromGetById()
    {
        // Arrange
        var before = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");
        var newName = $"Renamed {Guid.NewGuid():N}";

        // Act
        var response = await PatchMeAsync(_client, $$"""{"displayName": "{{newName}}"}""");
        var patched = await response.Content.ReadFromJsonAsync<CurrentUser>();
        var publicView = await _anonymousClient.GetFromJsonAsync<PublicUserInfo>($"/api/v1/users/{before!.Id}");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(patched, Is.EqualTo(before with { DisplayName = newName }),
                "only the display name changes");
            Assert.That(publicView!.DisplayName, Is.EqualTo(newName));
        });
    }

    [Test]
    public async Task PatchMe_WithDisplayName_IsFoundByTheCaseInsensitiveFilter()
    {
        // Arrange
        var marker = $"Findable{Guid.NewGuid():N}";

        // Act
        var response = await PatchMeAsync(_client, $$"""{"displayName": "{{marker}}"}""");
        var page = await _anonymousClient.GetFromJsonAsync<PaginatedResponse<PublicUserInfo>>(
            $"/api/v1/users?displayNameContains={marker.ToUpperInvariant()}");

        // Assert: the normalized copy was written alongside the display name.
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(page!.Results.Select(u => u.DisplayName), Is.EqualTo(new[] { marker }));
        });
    }

    [Test]
    public async Task PatchMe_WithEmptyBody_ReturnsOkAndChangesNothing()
    {
        // Arrange
        var before = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Act
        var response = await PatchMeAsync(_client, "{}");
        var patched = await response.Content.ReadFromJsonAsync<CurrentUser>();
        var after = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(patched, Is.EqualTo(before));
            Assert.That(after, Is.EqualTo(before));
        });
    }

    [Test]
    public async Task PatchMe_WithExplicitNullDisplayName_ReturnsBadRequestAndKeepsTheName()
    {
        // Arrange
        var before = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Act
        var response = await PatchMeAsync(_client, """{"displayName": null}""");
        var body = await response.Content.ReadAsStringAsync();
        var after = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(body, Does.Contain("required").IgnoreCase, "rejected by [Required], not by the parser");
            Assert.That(after, Is.EqualTo(before));
        });
    }

    [Test]
    public async Task PatchMe_WithOverLongDisplayName_ReturnsBadRequestAndKeepsTheName()
    {
        // Arrange
        var before = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");
        var tooLong = new string('a', UserValidationConstants.DisplayNameMaxLength + 1);

        // Act
        var response = await PatchMeAsync(_client, $$"""{"displayName": "{{tooLong}}"}""");
        var after = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(after, Is.EqualTo(before));
        });
    }

    [Test]
    public async Task PatchMe_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await PatchMeAsync(_anonymousClient, """{"displayName": "Anonymous"}""");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PatchById_IsNotARoute_EvenForTheCallersOwnId()
    {
        // Arrange
        var me = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");
        var other = await _otherUsersClient.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Act
        var otherResponse = await _client.PatchAsync($"/api/v1/users/{other!.Id}", JsonBody("""{"displayName": "Hijacked"}"""));
        var otherBody = await otherResponse.Content.ReadAsStringAsync();
        var ownResponse = await _client.PatchAsync($"/api/v1/users/{me!.Id}", JsonBody("""{"displayName": "Hijacked"}"""));
        var ownBody = await ownResponse.Content.ReadAsStringAsync();
        var otherAfter = await _otherUsersClient.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Assert: routing answers 405, since GET {userId:guid} matches the path but no endpoint takes
        // PATCH there. It carries no body, where a response from an action would carry ProblemDetails.
        Assert.Multiple(() =>
        {
            Assert.That(otherResponse.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            Assert.That(otherBody, Is.Empty, "not handled by an action");
            Assert.That(ownResponse.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            Assert.That(ownBody, Is.Empty, "not handled by an action");
            Assert.That(otherAfter, Is.EqualTo(other));
        });
    }

    [Test]
    public async Task Swagger_DocumentsPatchMeBodyWithAnOptionalDisplayName()
    {
        // Act
        var document = await _anonymousClient.GetFromJsonAsync<JsonObject>("/swagger/v1/swagger.json");

        // Assert
        var paths = document!["paths"]!.AsObject();
        var schema = paths["/api/v1/users/me"]!["patch"]!["requestBody"]!["content"]!["application/json"]!["schema"]!;
        if (schema["$ref"] is { } reference)
        {
            var id = reference.GetValue<string>().Split('/').Last();
            schema = document["components"]!["schemas"]![id]!;
        }

        var required = schema["required"]?.AsArray().Select(n => n!.GetValue<string>()) ?? [];
        Assert.Multiple(() =>
        {
            Assert.That(schema["properties"]?.AsObject().Select(p => p.Key), Does.Contain("displayName"));
            Assert.That(required, Does.Not.Contain("displayName"), "every property of a patch body is optional");
            Assert.That(paths["/api/v1/users/{userId}"]?["patch"], Is.Null, "there is no PATCH by id");
        });
    }

    private static Task<HttpResponseMessage> PatchMeAsync(HttpClient client, string json) =>
        client.PatchAsync("/api/v1/users/me", JsonBody(json));

    private static StringContent JsonBody(string json) => new(json, Encoding.UTF8, "application/json");

    #endregion

    #region PostMe

    [Test]
    public async Task PostMe_WithDisplayName_ChangesIt_VisiblyFromGetById()
    {
        // Arrange
        var before = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");
        var newName = $"Posted {Guid.NewGuid():N}";

        // Act
        var response = await PostMeAsync(_client, $$"""{"displayName": "{{newName}}"}""");
        var posted = await response.Content.ReadFromJsonAsync<CurrentUser>();
        var publicView = await _anonymousClient.GetFromJsonAsync<PublicUserInfo>($"/api/v1/users/{before!.Id}");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(posted, Is.EqualTo(before with { DisplayName = newName }),
                "only the display name changes");
            Assert.That(publicView!.DisplayName, Is.EqualTo(newName));
        });
    }

    [Test]
    public async Task PostMe_WithDisplayName_IsFoundByTheCaseInsensitiveFilter()
    {
        // Arrange
        var marker = $"PostFindable{Guid.NewGuid():N}";

        // Act
        var response = await PostMeAsync(_client, $$"""{"displayName": "{{marker}}"}""");
        var page = await _anonymousClient.GetFromJsonAsync<PaginatedResponse<PublicUserInfo>>(
            $"/api/v1/users?displayNameContains={marker.ToUpperInvariant()}");

        // Assert: the normalized copy was written alongside the display name.
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(page!.Results.Select(u => u.DisplayName), Is.EqualTo(new[] { marker }));
        });
    }

    [TestCase("{}", TestName = "PostMe_WithEmptyBody_ReturnsBadRequestAndKeepsTheName")]
    [TestCase("""{"displayName": null}""", TestName = "PostMe_WithExplicitNullDisplayName_ReturnsBadRequestAndKeepsTheName")]
    public async Task PostMe_WithoutADisplayName_ReturnsBadRequestAndKeepsTheName(string json)
    {
        // Arrange
        var before = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Act: unlike PATCH, POST replaces the whole writable surface, so an omitted property is missing.
        var response = await PostMeAsync(_client, json);
        var after = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(after, Is.EqualTo(before));
        });
    }

    [Test]
    public async Task PostMe_WithOverLongDisplayName_ReturnsBadRequestAndKeepsTheName()
    {
        // Arrange
        var before = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");
        var tooLong = new string('a', UserValidationConstants.DisplayNameMaxLength + 1);

        // Act
        var response = await PostMeAsync(_client, $$"""{"displayName": "{{tooLong}}"}""");
        var after = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
            Assert.That(after, Is.EqualTo(before));
        });
    }

    [Test]
    public async Task PostMe_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Act
        var response = await PostMeAsync(_anonymousClient, """{"displayName": "Anonymous"}""");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task PostById_IsNotARoute_EvenForTheCallersOwnId()
    {
        // Arrange
        var me = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");
        var other = await _otherUsersClient.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Act
        var otherResponse = await _client.PostAsync($"/api/v1/users/{other!.Id}", JsonBody("""{"displayName": "Hijacked"}"""));
        var otherBody = await otherResponse.Content.ReadAsStringAsync();
        var ownResponse = await _client.PostAsync($"/api/v1/users/{me!.Id}", JsonBody("""{"displayName": "Hijacked"}"""));
        var ownBody = await ownResponse.Content.ReadAsStringAsync();
        var otherAfter = await _otherUsersClient.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Assert: as for PATCH, routing answers 405 without reaching an action.
        Assert.Multiple(() =>
        {
            Assert.That(otherResponse.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            Assert.That(otherBody, Is.Empty, "not handled by an action");
            Assert.That(ownResponse.StatusCode, Is.EqualTo(HttpStatusCode.MethodNotAllowed));
            Assert.That(ownBody, Is.Empty, "not handled by an action");
            Assert.That(otherAfter, Is.EqualTo(other));
        });
    }

    [Test]
    public async Task Swagger_DocumentsPostMeBodyWithARequiredDisplayName()
    {
        // Act
        var document = await _anonymousClient.GetFromJsonAsync<JsonObject>("/swagger/v1/swagger.json");

        // Assert
        var paths = document!["paths"]!.AsObject();
        var schema = paths["/api/v1/users/me"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!;
        if (schema["$ref"] is { } reference)
        {
            var id = reference.GetValue<string>().Split('/').Last();
            schema = document["components"]!["schemas"]![id]!;
        }

        var required = schema["required"]?.AsArray().Select(n => n!.GetValue<string>()) ?? [];
        Assert.Multiple(() =>
        {
            Assert.That(schema["properties"]?.AsObject().Select(p => p.Key), Does.Contain("displayName"));
            Assert.That(required, Does.Contain("displayName"), "a POST body replaces every property");
            Assert.That(paths["/api/v1/users/{userId}"]?["post"], Is.Null, "there is no POST by id");
        });
    }

    private static Task<HttpResponseMessage> PostMeAsync(HttpClient client, string json) =>
        client.PostAsync("/api/v1/users/me", JsonBody(json));

    #endregion

    #region GetById

    [Test]
    public async Task GetById_Anonymously_ReturnsTheUserWithoutTheirEmail()
    {
        // Arrange
        var me = await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me");

        // Act
        var response = await _anonymousClient.GetAsync($"/api/v1/users/{me!.Id}");
        var body = await response.Content.ReadAsStringAsync();
        var user = await response.Content.ReadFromJsonAsync<PublicUserInfo>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(user!.Id, Is.EqualTo(me.Id));
            Assert.That(user.DisplayName, Is.EqualTo(me.DisplayName));
            AssertCarriesNoEmail(body);
        });
    }

    [Test]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _anonymousClient.GetAsync($"/api/v1/users/{Guid.NewGuid()}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region List

    [Test]
    public async Task Get_Anonymously_ReturnsUsersWithoutTheirEmails()
    {
        // Arrange: both realm users exist once they have made an authenticated request.
        await _client.GetAsync("/api/v1/users/me");
        await _otherUsersClient.GetAsync("/api/v1/users/me");

        // Act
        var response = await _anonymousClient.GetAsync("/api/v1/users?pageSize=500");
        var body = await response.Content.ReadAsStringAsync();
        var page = await response.Content.ReadFromJsonAsync<PaginatedResponse<PublicUserInfo>>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(page!.Total, Is.GreaterThanOrEqualTo(SeededNamesAscending.Length + 2));
            AssertCarriesNoEmail(body);
        });
    }

    [Test]
    public async Task Get_FilteredByDisplayName_IgnoresCase()
    {
        // Act
        var page = await _anonymousClient.GetFromJsonAsync<PaginatedResponse<PublicUserInfo>>(
            $"/api/v1/users?displayNameContains={SeedMarker.ToUpperInvariant()}");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(page!.Total, Is.EqualTo(SeededNamesAscending.Length));
            Assert.That(page.Results.Select(u => u.DisplayName), Is.EquivalentTo(SeededNamesAscending));
        });
    }

    [Test]
    public async Task Get_DefaultsToAlphabeticalByDisplayName_AndHonoursDirection()
    {
        // Arrange
        const string query = $"/api/v1/users?displayNameContains={SeedMarker}";

        // Act
        var unsorted = await _anonymousClient.GetFromJsonAsync<PaginatedResponse<PublicUserInfo>>(query);
        var sortedBy = await _anonymousClient.GetFromJsonAsync<PaginatedResponse<PublicUserInfo>>(
            $"{query}&sortBy=DisplayName");
        var descending = await _anonymousClient.GetFromJsonAsync<PaginatedResponse<PublicUserInfo>>(
            $"{query}&sortBy=DisplayName&direction=Descending");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(unsorted!.Results.Select(u => u.DisplayName), Is.EqualTo(SeededNamesAscending));
            Assert.That(sortedBy!.Results.Select(u => u.DisplayName), Is.EqualTo(SeededNamesAscending));
            Assert.That(descending!.Results.Select(u => u.DisplayName), Is.EqualTo(SeededNamesAscending.Reverse()));
        });
    }

    [Test]
    public async Task Get_Pages()
    {
        // Arrange
        const string query = $"/api/v1/users?displayNameContains={SeedMarker}&pageSize=2";

        // Act
        var first = await _anonymousClient.GetFromJsonAsync<PaginatedResponse<PublicUserInfo>>($"{query}&offset=0");
        var second = await _anonymousClient.GetFromJsonAsync<PaginatedResponse<PublicUserInfo>>($"{query}&offset=2");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(first!.Total, Is.EqualTo(SeededNamesAscending.Length));
            Assert.That(first.Offset, Is.EqualTo(0));
            Assert.That(first.Results.Select(u => u.DisplayName), Is.EqualTo(SeededNamesAscending.Take(2)));
            Assert.That(second!.Total, Is.EqualTo(SeededNamesAscending.Length));
            Assert.That(second.Offset, Is.EqualTo(2));
            Assert.That(second.Results.Select(u => u.DisplayName), Is.EqualTo(SeededNamesAscending.Skip(2)));
        });
    }

    #endregion

    /// <summary>
    /// Asserts that a raw response body names no email property and holds no email address. Every
    /// address in this fixture's database, realm users and seeded ones alike, contains an <c>@</c>, and
    /// no display name does.
    /// </summary>
    private static void AssertCarriesNoEmail(string body)
    {
        Assert.That(body, Does.Not.Contain("email").IgnoreCase, "no email property");
        Assert.That(body, Does.Not.Contain("@"), "no email address");
    }
}
