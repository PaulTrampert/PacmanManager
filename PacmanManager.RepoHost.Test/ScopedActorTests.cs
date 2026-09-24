using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Test.Containers;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests that the <c>scope</c> claim on a bearer token narrows what its user may do.
/// Narrow tokens come from the realm's <c>pacman-manager-scoped</c> client, which issues exactly the
/// values asked for; everything is arranged through a token carrying <c>pacman-manager:*:*</c>, as the
/// interactive clients' do, for the same user.
/// </summary>
[TestFixture]
public class ScopedActorTests
{
    private EndToEndTestFixture _fixture = null!;
    private HttpClient _fullClient = null!;
    private byte[] _packageBytes = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _fixture = new EndToEndTestFixture();
        await _fixture.StartAsync();

        _fullClient = _fixture.HttpClient;
        _fullClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _fixture.AuthContainer!.GetBearerTokenAsync(_fixture.AuthContainer.DefaultCredentials));

        _packageBytes = await File.ReadAllBytesAsync(PackageFixtures.MinimalPackagePath);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _fixture.DisposeAsync();
    }

    [Test]
    public async Task ReadScope_ChangesAreForbidden_AndTheCallersOwnPrivateRepositoryIsListed()
    {
        // Arrange
        using var client = await ScopedClientAsync("pacman-manager:*:read");
        var repository = await CreateAsync(_fullClient, "scoped-read-private", isPublic: false);
        var package = await PublishAsync(_fullClient, repository.Id);

        // Act
        var listed = await ListAsync(client, "scoped-read-");
        var create = await client.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = "scoped-read-created", SupportedArchitectures = [Architectures.X86_64] });
        var update = await client.PutAsJsonAsync($"/api/v1/repositories/{repository.Id}",
            new WriteRepositoryRequest { Name = "scoped-read-renamed", SupportedArchitectures = [Architectures.X86_64] });
        var delete = await client.DeleteAsync($"/api/v1/repositories/{repository.Id}");
        var publish = await PostPackageAsync(client, repository.Id);
        var deletePackage = await client.DeleteAsync($"/api/v1/packages/{package.Id}");
        var readPackage = await client.GetAsync($"/api/v1/packages/{package.Id}");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(listed, Does.Contain(repository.Id), "own private repository is listed");
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "create repository");
            Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "update repository");
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "delete repository");
            Assert.That(publish.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "publish");
            Assert.That(deletePackage.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "delete package");
            Assert.That(readPackage.StatusCode, Is.EqualTo(HttpStatusCode.OK), "read package");
        });
    }

    [Test]
    public async Task PackagesScopeWithRepositoriesRead_PublishingSucceeds_AndCreatingARepositoryIsForbidden()
    {
        // Arrange
        using var client = await ScopedClientAsync("pacman-manager:packages:* pacman-manager:repositories:read");
        var repository = await CreateAsync(_fullClient, "scoped-packages-private", isPublic: false);

        // Act
        var publish = await PostPackageAsync(client, repository.Id);
        var create = await client.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = "scoped-packages-created", SupportedArchitectures = [Architectures.X86_64] });

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(publish.StatusCode, Is.EqualTo(HttpStatusCode.Created), "publish");
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "create repository");
        });
    }

    [Test]
    public async Task RepositoriesScope_RepositoryChangesSucceed_AndPublishingIsForbidden()
    {
        // Arrange
        using var client = await ScopedClientAsync("pacman-manager:repositories:*");

        // Act
        var create = await client.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = "scoped-repositories-created", SupportedArchitectures = [Architectures.X86_64], IsPublic = true });
        var created = (await create.Content.ReadFromJsonAsync<Repository>())!;
        var update = await client.PutAsJsonAsync($"/api/v1/repositories/{created.Id}",
            new WriteRepositoryRequest { Name = "scoped-repositories-renamed", SupportedArchitectures = [Architectures.X86_64], IsPublic = true });

        // Public, so the repository is found without packages:read and the verdict is what refuses.
        var publish = await PostPackageAsync(client, created.Id);
        var delete = await client.DeleteAsync($"/api/v1/repositories/{created.Id}");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Created), "create repository");
            Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.OK), "update repository");
            Assert.That(publish.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "publish");
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NoContent), "delete repository");
        });
    }

    [Test]
    public async Task RepositoriesDeleteAlone_DeletingTheCallersOwnPrivateRepositoryIsNotFound_AndOnlyPublicRepositoriesAreListed()
    {
        // Arrange
        using var client = await ScopedClientAsync("pacman-manager:repositories:delete");
        var privateRepository = await CreateAsync(_fullClient, "scoped-delete-private", isPublic: false);
        var publicRepository = await CreateAsync(_fullClient, "scoped-delete-public", isPublic: true);

        // Act
        var delete = await client.DeleteAsync($"/api/v1/repositories/{privateRepository.Id}");
        var listed = await ListAsync(client, "scoped-delete-");
        var stillThere = await _fullClient.GetAsync($"/api/v1/repositories/{privateRepository.Id}");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.NotFound), "delete own private repository");
            Assert.That(listed, Is.EqualTo(new[] { publicRepository.Id }), "listing");
            Assert.That(stillThere.StatusCode, Is.EqualTo(HttpStatusCode.OK), "the repository was not deleted");
        });
    }

    [Test]
    public async Task NoneOfOurs_EveryChangeIsForbidden_AndTheListingIsExactlyTheAnonymousOne()
    {
        // Arrange
        using var client = await ScopedClientAsync(null);
        using var anonymous = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
        var publicRepository = await CreateAsync(_fullClient, "scoped-none-public", isPublic: true);
        var privateRepository = await CreateAsync(_fullClient, "scoped-none-private", isPublic: false);
        var package = await PublishAsync(_fullClient, publicRepository.Id);

        // Act
        var create = await client.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = "scoped-none-created", SupportedArchitectures = [Architectures.X86_64] });
        var update = await client.PutAsJsonAsync($"/api/v1/repositories/{publicRepository.Id}",
            new WriteRepositoryRequest { Name = "scoped-none-renamed", SupportedArchitectures = [Architectures.X86_64], IsPublic = true });
        var publish = await PostPackageAsync(client, publicRepository.Id);
        var deletePackage = await client.DeleteAsync($"/api/v1/packages/{package.Id}");
        var delete = await client.DeleteAsync($"/api/v1/repositories/{publicRepository.Id}");
        var listed = await ListAsync(client, "scoped-");
        var listedAnonymously = await ListAsync(anonymous, "scoped-");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(create.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "create repository");
            Assert.That(update.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "update repository");
            Assert.That(publish.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "publish");
            Assert.That(deletePackage.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "delete package");
            Assert.That(delete.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden), "delete repository");
            Assert.That(listed, Is.EqualTo(listedAnonymously), "listing");
            Assert.That(listed, Does.Contain(publicRepository.Id).And.Not.Contain(privateRepository.Id), "listing");
        });
    }

    #region Helpers

    /// <summary>
    /// A client carrying a token from <c>pacman-manager-scoped</c> for the default user, issued the
    /// given values of ours and nothing else of ours.
    /// </summary>
    private async Task<HttpClient> ScopedClientAsync(string? values)
    {
        var token = await _fixture.AuthContainer!.GetBearerTokenAsync(
            _fixture.AuthContainer.DefaultCredentials,
            KeycloakContainer.ScopedClientId,
            values is null ? "openid" : $"openid {values}");

        var client = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task<Repository> CreateAsync(HttpClient client, string name, bool isPublic)
    {
        var response = await client.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = name, SupportedArchitectures = [Architectures.X86_64], IsPublic = isPublic });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            $"Arranging '{name}' failed: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<Repository>())!;
    }

    private Task<HttpResponseMessage> PostPackageAsync(HttpClient client, Guid repositoryId)
    {
        var body = new ByteArrayContent(_packageBytes);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return client.PostAsync($"/api/v1/repositories/{repositoryId}/packages", body);
    }

    private async Task<Package> PublishAsync(HttpClient client, Guid repositoryId)
    {
        var response = await PostPackageAsync(client, repositoryId);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            $"Publishing failed: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<Package>())!;
    }

    private static async Task<IReadOnlyList<Guid>> ListAsync(HttpClient client, string nameContains)
    {
        var page = await client.GetFromJsonAsync<PaginatedResponse<Repository>>(
            $"/api/v1/repositories?nameContains={nameContains}&pageSize=100");
        return page!.Results.Select(r => r.Id).ToList();
    }

    #endregion
}
