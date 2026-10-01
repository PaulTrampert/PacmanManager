using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Asp.Versioning;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Models;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests of the <c>/pacman/{repoName}/{repoArch}/{fileName}</c> routes against the
/// containerized API: what a <c>pacman</c> client is served, who it is served to, and the HTTP
/// behaviour that keeps a sync cheap and a download resumable.
/// </summary>
/// <remarks>
/// Every repository here is created and published to over the real API, so each file served was
/// written by the real publish path. The private repository is read with a token minted over the real
/// API too, the way a <c>pacman.conf</c> would carry it.
/// </remarks>
[TestFixture]
public class PacmanRoutesTests
{
    private const string PublicRepo = "pacman-routes-public";
    private const string PrivateRepo = "pacman-routes-private";
    private const string Arch = PackageFixtures.MinimalPackageArchitecture;

    private EndToEndTestFixture _fixture = null!;
    private HttpClient _ownerClient = null!;
    private HttpClient _anonymousClient = null!;
    private HttpClient _basicClient = null!;
    private HttpClient _mistypedBasicClient = null!;

    private byte[] _packageBytes = null!;
    private Repository _publicRepository = null!;
    private Repository _privateRepository = null!;

    /// <summary>
    /// Every kind of file a repository serves, by the name a client asks for it under. The package
    /// is the fixture's, which <c>repo-add</c> records under its own basename.
    /// </summary>
    private static IEnumerable<string> FileNameTemplates() =>
    [
        "{0}.db",
        "{0}.db.tar.gz",
        "{0}.files",
        "{0}.files.tar.gz",
        PackageFixtures.MinimalPackageFileName,
    ];

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _fixture = new EndToEndTestFixture();
        await _fixture.StartAsync();

        _ownerClient = _fixture.HttpClient;
        _ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _fixture.AuthContainer!.GetBearerTokenAsync(_fixture.AuthContainer.DefaultCredentials));

        _anonymousClient = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };

        _packageBytes = await File.ReadAllBytesAsync(PackageFixtures.MinimalPackagePath);
        _publicRepository = await GivenAPublishedRepositoryAsync(PublicRepo, isPublic: true);
        _privateRepository = await GivenAPublishedRepositoryAsync(PrivateRepo, isPublic: false);

        var mint = await _ownerClient.PostAsJsonAsync("/api/v1/users/me/tokens",
            new CreateAccessTokenRequest { Name = "pacman-routes" });
        var mintBody = await mint.Content.ReadAsStringAsync();
        Assert.That(mint.StatusCode, Is.EqualTo(HttpStatusCode.Created), mintBody);
        var token = JsonSerializer.Deserialize<CreatedAccessToken>(mintBody, JsonSerializerOptions.Web)!;

        _basicClient = BasicClient(token.Username, token.Secret);
        _mistypedBasicClient = BasicClient(token.Username, AccessTokenFormat.GenerateSecret());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _anonymousClient.Dispose();
        _basicClient.Dispose();
        _mistypedBasicClient.Dispose();
        await _fixture.DisposeAsync();
    }

    #region Who is served what

    [TestCaseSource(nameof(FileNameTemplates))]
    public async Task PublicRepository_ServesEveryFile_ToAnAnonymousClient(string template)
    {
        var response = await _anonymousClient.GetAsync(Url(PublicRepo, template));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        AssertPacmanHeaders(response);
    }

    [TestCaseSource(nameof(FileNameTemplates))]
    public async Task PrivateRepository_ServesEveryFile_ToItsOwnersToken(string template)
    {
        var response = await _basicClient.GetAsync(Url(PrivateRepo, template));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        AssertPacmanHeaders(response);
    }

    [TestCaseSource(nameof(FileNameTemplates))]
    public async Task PrivateRepository_IsNotFound_WithoutAToken(string template)
    {
        var response = await _anonymousClient.GetAsync(Url(PrivateRepo, template));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        AssertPacmanHeaders(response);
    }

    /// <summary>
    /// The <c>[AllowAnonymous]</c> trap: a refused credential must not fall through to anonymous,
    /// where the owner's own private repository would be a <c>404</c> and the typo would be hidden.
    /// </summary>
    [Test]
    public async Task PrivateRepository_IsUnauthorizedRatherThanNotFound_WithAMistypedToken()
    {
        var response = await _mistypedBasicClient.GetAsync(Url(PrivateRepo, "{0}.db"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
            Assert.That(response.Headers.WwwAuthenticate.ToString(), Does.StartWith("Basic"));
        });
    }

    [Test]
    public async Task PublicRepository_IsUnauthorized_WithAMistypedToken()
    {
        var response = await _mistypedBasicClient.GetAsync(Url(PublicRepo, "{0}.db"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task SignatureProbe_IsNotFound()
    {
        // pacman -Sy against an unsigned repository still asks for the database's signature.
        var response = await _anonymousClient.GetAsync(Url(PublicRepo, "{0}.db.sig"));

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region What is served

    [Test]
    public async Task Package_IsExactlyTheBytesThatWerePublished()
    {
        var bytes = await _anonymousClient.GetByteArrayAsync(Url(PublicRepo, PackageFixtures.MinimalPackageFileName));

        Assert.That(bytes, Is.EqualTo(_packageBytes));
    }

    [TestCase("{0}.db", "db.tar.gz")]
    [TestCase("{0}.db.tar.gz", "db.tar.gz")]
    [TestCase("{0}.files", "files.tar.gz")]
    [TestCase("{0}.files.tar.gz", "files.tar.gz")]
    public async Task Database_IsExactlyTheFileRepoAddWrote(string template, string storedExtension)
    {
        var id = _publicRepository.Id;
        var stored = $"/data/repositories/{id}/db/{Arch}/{id}.{storedExtension}";

        var bytes = await _anonymousClient.GetByteArrayAsync(Url(PublicRepo, template));
        var storedHash = (await _fixture.ExecInApiContainerAsync("sha256sum", stored)).Split(' ')[0];

        Assert.That(Convert.ToHexStringLower(SHA256.HashData(bytes)), Is.EqualTo(storedHash));
    }

    [Test]
    public async Task Response_IsAnOctetStreamWithAContentLengthAndNoDisposition()
    {
        var response = await _anonymousClient.GetAsync(Url(PublicRepo, PackageFixtures.MinimalPackageFileName));

        Assert.Multiple(() =>
        {
            Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/octet-stream"));
            Assert.That(response.Content.Headers.ContentLength, Is.EqualTo(_packageBytes.Length));
            Assert.That(response.Headers.TransferEncodingChunked, Is.Not.True, "never chunked");
            Assert.That(response.Content.Headers.ContentDisposition, Is.Null, "pacman names the file from its URL");
            Assert.That(response.Content.Headers.LastModified, Is.Not.Null);
            Assert.That(response.Headers.ETag, Is.Not.Null);
        });
    }

    [Test]
    public async Task Head_ReportsTheLengthWithoutABody()
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, Url(PublicRepo, PackageFixtures.MinimalPackageFileName));

        var response = await _anonymousClient.SendAsync(request);
        var body = await response.Content.ReadAsByteArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(response.Content.Headers.ContentLength, Is.EqualTo(_packageBytes.Length));
            Assert.That(body, Is.Empty);
        });
        AssertPacmanHeaders(response);
    }

    #endregion

    #region Conditional and range requests

    /// <summary>
    /// libalpm sends <c>If-Modified-Since</c> from its local copy's modification time on every
    /// <c>pacman -Sy</c>; without a <c>304</c> every sync re-downloads every database.
    /// </summary>
    [Test]
    public async Task IfModifiedSince_TheLastModifiedServed_IsNotModified()
    {
        var first = await _anonymousClient.GetAsync(Url(PublicRepo, "{0}.db"));
        using var request = new HttpRequestMessage(HttpMethod.Get, Url(PublicRepo, "{0}.db"));
        request.Headers.IfModifiedSince = first.Content.Headers.LastModified;

        var response = await _anonymousClient.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
        AssertPacmanHeaders(response);
    }

    [Test]
    public async Task IfNoneMatch_TheETagServed_IsNotModified()
    {
        var first = await _basicClient.GetAsync(Url(PrivateRepo, "{0}.db"));
        using var request = new HttpRequestMessage(HttpMethod.Get, Url(PrivateRepo, "{0}.db"));
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);

        var response = await _basicClient.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotModified));
        AssertPacmanHeaders(response);
    }

    [Test]
    public async Task IfModifiedSince_LongAgo_ServesTheFile()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, Url(PublicRepo, "{0}.db"));
        request.Headers.IfModifiedSince = DateTimeOffset.UnixEpoch;

        var response = await _anonymousClient.SendAsync(request);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    /// <summary>
    /// libalpm resumes an interrupted download with <c>Range: bytes=N-</c> and expects a <c>206</c>.
    /// </summary>
    [Test]
    public async Task Range_FromAnOffset_IsPartialContentWithTheRemainingBytes()
    {
        const int offset = 10;
        using var request = new HttpRequestMessage(HttpMethod.Get, Url(PublicRepo, PackageFixtures.MinimalPackageFileName));
        request.Headers.Range = new RangeHeaderValue(offset, null);

        var response = await _anonymousClient.SendAsync(request);
        var bytes = await response.Content.ReadAsByteArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.PartialContent));
            Assert.That(bytes, Is.EqualTo(_packageBytes[offset..]));
            Assert.That(response.Content.Headers.ContentLength, Is.EqualTo(_packageBytes.Length - offset));
            Assert.That(response.Content.Headers.ContentRange?.From, Is.EqualTo(offset));
            Assert.That(response.Content.Headers.ContentRange?.Length, Is.EqualTo(_packageBytes.Length));
        });
        AssertPacmanHeaders(response);
    }

    #endregion

    #region Swagger

    /// <summary>
    /// The routes belong to <c>pacman.conf</c>, not to the API a human explores. Each Swagger
    /// document is named after an API version the API reports, so the reported versions are what
    /// "every document" is enumerated from.
    /// </summary>
    [Test]
    public async Task Routes_AreAbsentFromEverySwaggerDocument()
    {
        var health = await _anonymousClient.GetAsync("/api/v1/healthcheck");
        var versions = health.Headers.GetValues("api-supported-versions")
            .SelectMany(v => v.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            .Select(v => ApiVersionParser.Default.Parse(v))
            .ToList();
        Assert.That(versions, Is.Not.Empty);

        foreach (var version in versions)
        {
            // The group name format Program.cs gives the API Explorer, which names each document.
            var group = version.ToString("'v'V", CultureInfo.InvariantCulture);
            var document = await _anonymousClient.GetAsync($"/swagger/{group}/swagger.json");
            var body = await document.Content.ReadAsStringAsync();
            using var json = JsonDocument.Parse(body);
            var paths = json.RootElement.GetProperty("paths").EnumerateObject().Select(p => p.Name).ToList();

            Assert.Multiple(() =>
            {
                Assert.That(document.StatusCode, Is.EqualTo(HttpStatusCode.OK), group);
                Assert.That(paths, Is.Not.Empty, $"{group} describes the API at all");
                Assert.That(paths, Has.None.StartsWith("/pacman"), group);
            });
        }
    }

    #endregion

    #region Helpers

    private static string Url(string repoName, string template) =>
        $"/pacman/{repoName}/{Arch}/{string.Format(template, repoName)}";

    /// <summary>
    /// <c>Vary: Authorization</c> on every response, since the same URL is a <c>404</c> anonymously and
    /// a <c>200</c> for its owner; and no <c>Cache-Control</c>, by decision rather than by omission.
    /// </summary>
    private static void AssertPacmanHeaders(HttpResponseMessage response)
    {
        Assert.Multiple(() =>
        {
            Assert.That(response.Headers.Vary, Does.Contain("Authorization"), "Vary");
            Assert.That(response.Headers.CacheControl, Is.Null, "Cache-Control");
        });
    }

    private HttpClient BasicClient(string username, string password)
    {
        var client = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}")));
        return client;
    }

    /// <summary>
    /// Creates a repository and publishes the fixture package into it, so that its databases and
    /// package file are the ones the application itself wrote.
    /// </summary>
    private async Task<Repository> GivenAPublishedRepositoryAsync(string name, bool isPublic)
    {
        var created = await _ownerClient.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = name,
            SupportedArchitectures = [Arch],
            IsPublic = isPublic,
        });
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            $"Arranging '{name}' failed: {await created.Content.ReadAsStringAsync()}");
        var repository = (await created.Content.ReadFromJsonAsync<Repository>())!;

        var body = new ByteArrayContent(_packageBytes);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var published = await _ownerClient.PostAsync($"/api/v1/repositories/{repository.Id}/packages", body);
        Assert.That(published.IsSuccessStatusCode, Is.True,
            $"Publishing to '{name}' failed: {await published.Content.ReadAsStringAsync()}");

        return repository;
    }

    #endregion
}
