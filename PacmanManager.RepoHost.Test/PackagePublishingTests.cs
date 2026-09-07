using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using PacmanManager.RepoHost.Models;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests for publishing a package. These run the application in a Docker container, so
/// the package is uploaded over real HTTP, read by real libalpm and added by the real
/// <c>repo-add</c>.
/// </summary>
[TestFixture]
public class PackagePublishingTests
{
    private EndToEndTestFixture _fixture = null!;
    private HttpClient _client = null!;
    private HttpClient _otherUsersClient = null!;

    private byte[] _packageBytes = null!;
    private string _expectedSha256 = null!;
    private string _expectedMd5 = null!;

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

        _packageBytes = await File.ReadAllBytesAsync(PackageFixtures.MinimalPackagePath);
        _expectedSha256 = Convert.ToHexStringLower(SHA256.HashData(_packageBytes));
        _expectedMd5 = Convert.ToHexStringLower(MD5.HashData(_packageBytes));
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _otherUsersClient.Dispose();
        await _fixture.DisposeAsync();
    }

    #region Publishing

    [Test]
    public async Task Publish_ANewPackage_ReturnsCreated()
    {
        // Arrange
        var repository = await GivenRepositoryAsync("publish-created");

        // Act
        var response = await PublishAsync(_client, repository.Id, _packageBytes);
        var package = await response.Content.ReadFromJsonAsync<Package>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
            Assert.That(package, Is.Not.Null);
            Assert.That(package!.Name, Is.EqualTo(PackageFixtures.MinimalPackageName));
            Assert.That(package.Version, Is.EqualTo(PackageFixtures.MinimalPackageVersion));
            Assert.That(package.Architecture, Is.EqualTo(PackageFixtures.MinimalPackageArchitecture));
            Assert.That(package.FileName, Is.EqualTo(PackageFixtures.MinimalPackageFileName));
            Assert.That(package.RepositoryId, Is.EqualTo(repository.Id));
            Assert.That(package.CompressedSize, Is.EqualTo(_packageBytes.Length));
        });
    }

    [Test]
    public async Task Publish_ANewPackage_ReturnsALocationHeaderNamingIt()
    {
        // Arrange
        var repository = await GivenRepositoryAsync("publish-location");

        // Act
        var response = await PublishAsync(_client, repository.Id, _packageBytes);

        // Assert
        Assert.That(response.Headers.Location, Is.Not.Null);
        Assert.That(response.Headers.Location!.ToString(),
            Is.EqualTo($"/api/v1/repositories/{repository.Id}/packages/{PackageFixtures.MinimalPackageName}"));
    }

    [Test]
    public async Task Publish_ReadsEveryFieldOutOfTheUploadedFile()
    {
        // Nothing here was asserted by the caller: the request body was the package and nothing
        // else.
        // Arrange
        var repository = await GivenRepositoryAsync("publish-metadata");

        // Act
        var package = await PublishAndReadAsync(_client, repository.Id);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(package.Description, Is.EqualTo(PackageFixtures.MinimalPackageDescription));
            Assert.That(package.Base, Is.EqualTo(PackageFixtures.MinimalPackageBase));
            Assert.That(package.Url, Is.EqualTo(PackageFixtures.MinimalPackageUrl));
            Assert.That(package.Packager, Is.EqualTo(PackageFixtures.MinimalPackagePackager));
            Assert.That(package.InstalledSize, Is.EqualTo(PackageFixtures.MinimalPackageInstalledSize));
            Assert.That(package.BuildDate.ToUnixTimeSeconds(),
                Is.EqualTo(PackageFixtures.MinimalPackageBuildDateUnixSeconds));
            Assert.That(package.Licenses, Is.EqualTo(new[] { PackageFixtures.MinimalPackageLicense }));
            Assert.That(package.Groups, Is.EqualTo(new[] { PackageFixtures.MinimalPackageGroup }));
            Assert.That(package.Depends, Is.EqualTo(new[] { PackageFixtures.MinimalPackageDepend }));
            Assert.That(package.OptDepends, Is.EqualTo(new[] { PackageFixtures.MinimalPackageOptDepend }));
            Assert.That(package.MakeDepends, Is.EqualTo(new[] { PackageFixtures.MinimalPackageMakeDepend }));
            Assert.That(package.CheckDepends, Is.EqualTo(new[] { PackageFixtures.MinimalPackageCheckDepend }));
            Assert.That(package.Provides, Is.EqualTo(new[] { PackageFixtures.MinimalPackageProvides }));
            Assert.That(package.Conflicts, Is.EqualTo(new[] { PackageFixtures.MinimalPackageConflict }));
            Assert.That(package.Replaces, Is.EqualTo(new[] { PackageFixtures.MinimalPackageReplaces }));
        });
    }

    [Test]
    public async Task Publish_StoresChecksumsThatAgreeWithTheBytesAndWithRepoAdd()
    {
        // The API and the database a pacman client reads have to say the same thing about the same
        // file, which is why these are computed over the upload rather than read back from libalpm.
        // Arrange
        var repository = await GivenRepositoryAsync("publish-checksums");

        // Act
        var package = await PublishAndReadAsync(_client, repository.Id);
        var databaseEntry = await ReadRepositoryDatabaseAsync(repository.Id);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(package.Sha256Sum, Is.EqualTo(_expectedSha256), "The API's checksum is of the bytes sent.");
            Assert.That(FieldOf(databaseEntry, "%SHA256SUM%"), Is.EqualTo(_expectedSha256),
                "And repo-add recorded the same one for the same file.");
            Assert.That(FieldOf(databaseEntry, "%FILENAME%"), Is.EqualTo(package.FileName));

            // repo-add stopped writing %MD5SUM% at pacman 7, so this only holds it to account where
            // it is still recorded. The column exists because the database format has the field.
            Assert.That(FieldOf(databaseEntry, "%MD5SUM%"), Is.EqualTo(_expectedMd5).Or.Null);
        });
    }

    [Test]
    public async Task Publish_AddsThePackageToTheRepositoryDatabase()
    {
        // Arrange
        var repository = await GivenRepositoryAsync("publish-repo-add");

        // Act
        await PublishAsync(_client, repository.Id, _packageBytes);
        var databaseEntry = await ReadRepositoryDatabaseAsync(repository.Id);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(FieldOf(databaseEntry, "%NAME%"), Is.EqualTo(PackageFixtures.MinimalPackageName));
            Assert.That(FieldOf(databaseEntry, "%VERSION%"), Is.EqualTo(PackageFixtures.MinimalPackageVersion));
        });
    }

    [Test]
    public async Task Publish_TheSamePackageAgain_ReturnsOkAndKeepsTheIdentity()
    {
        // A repository holds exactly one version of a package, so replacing one is the normal path.
        // Arrange
        var repository = await GivenRepositoryAsync("publish-replace");
        var first = await PublishAndReadAsync(_client, repository.Id);

        // Act
        var response = await PublishAsync(_client, repository.Id, _packageBytes);
        var second = await response.Content.ReadFromJsonAsync<Package>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), "A replacement is a 200, not a 201.");
            Assert.That(second!.Id, Is.EqualTo(first.Id));

            // Within a microsecond rather than exactly: the 201 reported the timestamp the row was
            // built with, and the 200 reports it as Postgres stores it.
            Assert.That(second.CreatedAt, Is.EqualTo(first.CreatedAt).Within(TimeSpan.FromMicroseconds(1)));
            Assert.That(second.UpdatedAt, Is.GreaterThan(first.CreatedAt));
        });
    }

    #endregion

    #region Rejected uploads

    [Test]
    public async Task Publish_AFileThatIsNotAPackage_ReturnsBadRequest()
    {
        // Arrange
        var repository = await GivenRepositoryAsync("publish-not-a-package");

        // Act
        var response = await PublishAsync(_client, repository.Id, "this is not a package"u8.ToArray());

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Publish_APackageForAnArchitectureTheRepositoryDoesNotServe_ReturnsBadRequest()
    {
        // An 'any' repository serves packages that contain nothing architecture specific, so an
        // x86_64 build does not belong in one.
        // Arrange
        var repository = await GivenRepositoryAsync("publish-wrong-arch", architecture: "any");

        // Act
        var response = await PublishAsync(_client, repository.Id, _packageBytes);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Publish_APackageOverTheConfiguredCeiling_ReturnsPayloadTooLarge()
    {
        // The limit is a configurable maximum rather than no limit at all, so that too large a
        // package is a 413 that names a limit and not an out of disk.
        // Arrange
        var repository = await GivenRepositoryAsync("publish-too-large");
        var oversized = new byte[EndToEndTestFixture.MaxUploadBytes + 1];

        // Its own client, because refusing the body without draining it costs the connection, and a
        // pooled one would take the next test's request down with it.
        using var client = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
        client.DefaultRequestHeaders.Authorization = _client.DefaultRequestHeaders.Authorization;

        // Act
        var response = await PublishAsync(client, repository.Id, oversized);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.RequestEntityTooLarge));
    }

    #endregion

    #region Authorization

    [Test]
    public async Task Publish_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var repository = await GivenRepositoryAsync("publish-anonymous", isPublic: true);
        using var anonymous = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };

        // Act
        var response = await PublishAsync(anonymous, repository.Id, _packageBytes);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Publish_ToSomebodyElsesPublicRepository_ReturnsForbidden()
    {
        // The repository is public, so its existence is already known and refusing the publish
        // leaks nothing.
        // Arrange
        var repository = await GivenRepositoryAsync("publish-theirs-public", isPublic: true,
            client: _otherUsersClient);

        // Act
        var response = await PublishAsync(_client, repository.Id, _packageBytes);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
    }

    [Test]
    public async Task Publish_ToSomebodyElsesPrivateRepository_ReturnsNotFound()
    {
        // A private repository has to stay indistinguishable from one that is not there.
        // Arrange
        var repository = await GivenRepositoryAsync("publish-theirs-private", isPublic: false,
            client: _otherUsersClient);

        // Act
        var response = await PublishAsync(_client, repository.Id, _packageBytes);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Publish_ToARepositoryThatDoesNotExist_ReturnsNotFound()
    {
        // Act
        var response = await PublishAsync(_client, Guid.NewGuid(), _packageBytes);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region Helpers

    private async Task<AuthenticationHeaderValue> BearerFor(Containers.KeycloakCredentials credentials) =>
        new("Bearer", await _fixture.AuthContainer!.GetBearerTokenAsync(credentials));

    private async Task<Repository> GivenRepositoryAsync(
        string name,
        bool isPublic = false,
        string architecture = "x86_64",
        HttpClient? client = null)
    {
        var response = await (client ?? _client).PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = name,
            Architecture = architecture,
            IsPublic = isPublic,
        });

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            $"Arranging '{name}' failed: {await response.Content.ReadAsStringAsync()}");

        return (await response.Content.ReadFromJsonAsync<Repository>())!;
    }

    private static Task<HttpResponseMessage> PublishAsync(HttpClient client, Guid repositoryId, byte[] content)
    {
        var body = new ByteArrayContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return client.PostAsync($"/api/v1/repositories/{repositoryId}/packages", body);
    }

    private async Task<Package> PublishAndReadAsync(HttpClient client, Guid repositoryId)
    {
        var response = await PublishAsync(client, repositoryId, _packageBytes);
        Assert.That(response.IsSuccessStatusCode, Is.True,
            $"Publishing failed: {await response.Content.ReadAsStringAsync()}");

        return (await response.Content.ReadFromJsonAsync<Package>())!;
    }

    /// <summary>
    /// Reads the <c>desc</c> entries out of a repository's <c>.db.tar.gz</c>, which is what
    /// <c>repo-add</c> wrote and what a pacman client would sync.
    /// </summary>
    private Task<string> ReadRepositoryDatabaseAsync(Guid repositoryId) =>
        _fixture.ExecInApiContainerAsync("tar", "-xOzf", $"/data/libalpm/sync/{repositoryId}.db.tar.gz");

    /// <summary>
    /// Pulls one field out of a pacman database entry, whose format is a <c>%KEY%</c> line followed
    /// by the value on the next line.
    /// </summary>
    private static string? FieldOf(string entry, string key)
    {
        var lines = entry.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var keyIndex = Array.IndexOf(lines, key);
        return keyIndex >= 0 && keyIndex + 1 < lines.Length ? lines[keyIndex + 1] : null;
    }

    #endregion
}
