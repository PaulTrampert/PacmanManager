using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using Microsoft.EntityFrameworkCore;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests of the <c>Basic</c> scheme against the containerized API. Tokens are arranged
/// directly in the database, since no route mints one yet; the user they belong to is the realm's
/// default user, provisioned by a <c>Bearer</c> request first.
/// </summary>
[TestFixture]
public class BasicAuthenticationTests
{
    private EndToEndTestFixture _fixture = null!;
    private HttpClient _bearerClient = null!;
    private Repository _privateRepository = null!;
    private string _username = null!;
    private string _password = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _fixture = new EndToEndTestFixture();
        await _fixture.StartAsync();

        _bearerClient = _fixture.HttpClient;
        _bearerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _fixture.AuthContainer!.GetBearerTokenAsync(_fixture.AuthContainer.DefaultCredentials));

        var create = await _bearerClient.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = "basic-private", SupportedArchitectures = [Architectures.X86_64], IsPublic = false });
        Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created), await create.Content.ReadAsStringAsync());
        _privateRepository = (await create.Content.ReadFromJsonAsync<Repository>())!;

        await using var db = _fixture.CreateDbContext();
        var ownerId = await db.PacmanRepositories
            .Where(r => r.Id == _privateRepository.Id)
            .Select(r => r.OwnerId)
            .SingleAsync();
        _password = AccessTokenFormat.GenerateSecret();
        var token = new PacmanAccessToken
        {
            UserId = ownerId,
            Name = "laptop",
            NormalizedName = "laptop",
            TokenHash = AccessTokenFormat.HashSecret(_password),
        };
        db.PacmanAccessTokens.Add(token);
        await db.SaveChangesAsync();
        _username = AccessTokenFormat.FormatUsername(token.Id);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _fixture.DisposeAsync();
    }

    /// <summary>
    /// The restriction is a property of the credential, not of the route: <c>POST</c> is a route the
    /// token's owner may use, and it is refused because the token carries only a read scope.
    /// </summary>
    [Test]
    public async Task BasicAuthenticatedPostToRepositories_IsForbidden()
    {
        using var client = BasicClient(_username, _password);

        var response = await client.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = "basic-created", SupportedArchitectures = [Architectures.X86_64] });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task BasicAuthenticatedListing_ShowsTheOwnersPrivateRepository()
    {
        using var client = BasicClient(_username, _password);

        var page = await client.GetFromJsonAsync<PaginatedResponse<Repository>>(
            "/api/v1/repositories?nameContains=basic-private");

        Assert.That(page!.Results.Select(r => r.Id), Does.Contain(_privateRepository.Id));
    }

    /// <summary>
    /// The repository listing is <c>[AllowAnonymous]</c>: a bad token is still a <c>401</c> there, not
    /// the anonymous listing.
    /// </summary>
    [Test]
    public async Task InvalidBasicCredential_OnAnAllowAnonymousRoute_Is401WithABasicChallenge()
    {
        using var client = BasicClient(_username, AccessTokenFormat.GenerateSecret());

        var response = await client.GetAsync("/api/v1/repositories");

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(response.Headers.WwwAuthenticate.ToString(), Is.EqualTo("Basic realm=\"pacman\""));
        });
    }

    [Test]
    public async Task BasicAuthenticatedRequest_TheTokenDoesNotAppearInTheLog()
    {
        using var client = BasicClient(_username, _password);
        var marker = $"basic-log-{Guid.NewGuid():N}";

        var response = await client.GetAsync($"/api/v1/repositories?nameContains={marker}");
        var logs = await WaitForLogContainingAsync(marker);

        var header = client.DefaultRequestHeaders.Authorization!.Parameter!;
        var secretBody = _password[AccessTokenFormat.SecretPrefix.Length..];
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(logs, Does.Contain(marker), "the request was logged");
            Assert.That(logs, Does.Not.Contain(secretBody[..8]), "the secret");
            Assert.That(logs, Does.Not.Contain(header[..16]), "the Authorization header");
        });
    }

    [Test]
    public async Task BasicAuthenticatedRequest_CreatesNoExternalProviderUserMapping()
    {
        using var client = BasicClient(_username, _password);
        int users, mappings;
        await using (var before = _fixture.CreateDbContext())
        {
            users = await before.Users.CountAsync();
            mappings = await before.UserMappings.CountAsync();
        }

        var response = await client.GetAsync("/api/v1/repositories");

        await using var after = _fixture.CreateDbContext();
        var usersAfter = await after.Users.CountAsync();
        var mappingsAfter = await after.UserMappings.CountAsync();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(usersAfter, Is.EqualTo(users), "users");
            Assert.That(mappingsAfter, Is.EqualTo(mappings), "external provider user mappings");
        });
    }

    #region Helpers

    private HttpClient BasicClient(string username, string password)
    {
        var client = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        return client;
    }

    /// <summary>
    /// Reads the API's log until it contains <paramref name="marker"/>, since the request log line is
    /// written after the response is sent.
    /// </summary>
    private async Task<string> WaitForLogContainingAsync(string marker)
    {
        var logs = string.Empty;
        for (var attempt = 0; attempt < 50 && !logs.Contains(marker); attempt++)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            logs = await _fixture.GetApiLogsAsync();
        }

        return logs;
    }

    #endregion
}
