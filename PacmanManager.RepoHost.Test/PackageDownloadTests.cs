using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PacmanManager.RepoHost.Models;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests for downloading a package's stored file. These run the application in a Docker
/// container, so every package downloaded here was first uploaded over real HTTP and written to
/// disk by the real publish path.
/// </summary>
/// <remarks>
/// The assertion worth having is byte equality: the file that comes back has to be the file that
/// went in, unaltered, because a client will checksum it against what <c>repo-add</c> recorded.
/// Seeding a row would not test that — the point is the round trip.
/// </remarks>
[TestFixture]
public class PackageDownloadTests
{
    private EndToEndTestFixture _fixture = null!;
    private HttpClient _client = null!;
    private HttpClient _otherUsersClient = null!;
    private HttpClient _anonymousClient = null!;

    private byte[] _packageBytes = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _fixture = new EndToEndTestFixture();
        await _fixture.StartAsync();

        _client = _fixture.HttpClient;
        _client.DefaultRequestHeaders.Authorization = await BearerFor(_fixture.AuthContainer!.DefaultCredentials);

        _otherUsersClient = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
        _otherUsersClient.DefaultRequestHeaders.Authorization =
            await BearerFor(_fixture.AuthContainer.SecondaryCredentials);

        _anonymousClient = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };

        _packageBytes = await File.ReadAllBytesAsync(PackageFixtures.MinimalPackagePath);
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _otherUsersClient.Dispose();
        _anonymousClient.Dispose();
        await _fixture.DisposeAsync();
    }

    #region The round trip

    [Test]
    public async Task GetContentById_ReturnsExactlyTheBytesThatWerePublished()
    {
        // Arrange
        var package = await GivenAPublishedPackageAsync("download-by-id");

        // Act
        var response = await _client.GetAsync($"/api/v1/packages/{package.Id}/content");
        var downloaded = await response.Content.ReadAsByteArrayAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(downloaded, Is.EqualTo(_packageBytes),
            "A downloaded package has to be byte for byte what was uploaded; a client will checksum it.");
    }

    [Test]
    public async Task GetContentByName_ReturnsExactlyTheBytesThatWerePublished()
    {
        // The alias is what a build script can address: it knows the repository it published to and
        // the package name, and has never seen a package id.
        // Arrange
        var package = await GivenAPublishedPackageAsync("download-by-name");

        // Act
        var response = await _client.GetAsync(ContentByNameUrl(package.RepositoryId, package.Name));
        var downloaded = await response.Content.ReadAsByteArrayAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(downloaded, Is.EqualTo(_packageBytes));
    }

    [Test]
    public async Task GetContent_NamesTheStoredFileAndServesItAsAnOctetStream()
    {
        // The name in the disposition is the basename repo-add recorded, so a client that saves the
        // response ends up with the file name pacman expects rather than a package id.
        // Arrange
        var package = await GivenAPublishedPackageAsync("download-headers");

        // Act
        var response = await _client.GetAsync($"/api/v1/packages/{package.Id}/content");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/octet-stream"));
            Assert.That(response.Content.Headers.ContentDisposition, Is.Not.Null);
            Assert.That(response.Content.Headers.ContentDisposition!.FileName?.Trim('"'),
                Is.EqualTo(PackageFixtures.MinimalPackageFileName));
        });
    }

    [Test]
    public async Task GetContent_ByEitherRoute_ReturnsTheSameFile()
    {
        // Arrange
        var package = await GivenAPublishedPackageAsync("download-both-routes");

        // Act
        var byId = await _client.GetByteArrayAsync($"/api/v1/packages/{package.Id}/content");
        var byName = await _client.GetByteArrayAsync(ContentByNameUrl(package.RepositoryId, package.Name));

        // Assert
        Assert.That(byId, Is.EqualTo(byName));
    }

    #endregion

    #region Visibility

    [Test]
    public async Task GetContentById_ByAnAnonymousCaller_ReturnsTheFile_WhenTheRepositoryIsPublic()
    {
        // Content is exactly as visible as metadata, and the metadata routes are anonymous.
        // Arrange
        var package = await GivenAPublishedPackageAsync("download-public", isPublic: true);

        // Act
        var response = await _anonymousClient.GetAsync($"/api/v1/packages/{package.Id}/content");
        var downloaded = await response.Content.ReadAsByteArrayAsync();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(downloaded, Is.EqualTo(_packageBytes));
    }

    [Test]
    public async Task GetContentByName_ByAnAnonymousCaller_ReturnsTheFile_WhenTheRepositoryIsPublic()
    {
        // Arrange
        var package = await GivenAPublishedPackageAsync("download-public-alias", isPublic: true);

        // Act
        var response = await _anonymousClient.GetAsync(ContentByNameUrl(package.RepositoryId, package.Name));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GetContentById_BySomebodyElse_ReturnsNotFound_WhenTheRepositoryIsPrivate()
    {
        // A private repository has to stay indistinguishable from one that is not there, so the
        // download says nothing the metadata route would not.
        // Arrange
        var package = await GivenAPublishedPackageAsync("download-theirs-private");

        // Act
        var response = await _otherUsersClient.GetAsync($"/api/v1/packages/{package.Id}/content");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetContentByName_BySomebodyElse_ReturnsNotFound_WhenTheRepositoryIsPrivate()
    {
        // Arrange
        var package = await GivenAPublishedPackageAsync("download-theirs-private-alias");

        // Act
        var response = await _otherUsersClient.GetAsync(ContentByNameUrl(package.RepositoryId, package.Name));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetContentById_ByAnAnonymousCaller_ReturnsNotFound_WhenTheRepositoryIsPrivate()
    {
        // Arrange
        var package = await GivenAPublishedPackageAsync("download-anonymous-private");

        // Act
        var response = await _anonymousClient.GetAsync($"/api/v1/packages/{package.Id}/content");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetContentById_ReturnsNotFound_ForAPackageThatDoesNotExist()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/packages/{Guid.NewGuid()}/content");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetContentByName_ReturnsNotFound_WhenTheRepositoryHoldsNoSuchPackage()
    {
        // Arrange
        var package = await GivenAPublishedPackageAsync("download-unknown-name");

        // Act
        var response = await _client.GetAsync(ContentByNameUrl(package.RepositoryId, "not-published-here"));

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region Helpers

    private async Task<AuthenticationHeaderValue> BearerFor(Containers.KeycloakCredentials credentials) =>
        new("Bearer", await _fixture.AuthContainer!.GetBearerTokenAsync(credentials));

    private static string ContentByNameUrl(Guid repositoryId, string name) =>
        $"/api/v1/repositories/{repositoryId}/packages/{Uri.EscapeDataString(name)}/content";

    /// <summary>
    /// Creates a repository and publishes the fixture package into it, so that every download runs
    /// against a file the application itself wrote.
    /// </summary>
    private async Task<Package> GivenAPublishedPackageAsync(string repositoryName, bool isPublic = false)
    {
        var created = await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = repositoryName,
            Architecture = PackageFixtures.MinimalPackageArchitecture,
            IsPublic = isPublic,
        });

        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            $"Arranging '{repositoryName}' failed: {await created.Content.ReadAsStringAsync()}");

        var repository = (await created.Content.ReadFromJsonAsync<Repository>())!;

        var body = new ByteArrayContent(_packageBytes);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var published = await _client.PostAsync($"/api/v1/repositories/{repository.Id}/packages", body);

        Assert.That(published.IsSuccessStatusCode, Is.True,
            $"Publishing to '{repositoryName}' failed: {await published.Content.ReadAsStringAsync()}");

        return (await published.Content.ReadFromJsonAsync<Package>())!;
    }

    #endregion
}
