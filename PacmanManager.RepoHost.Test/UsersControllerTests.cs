using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests for the Users API's read routes.
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
