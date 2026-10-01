using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using DotNet.Testcontainers.Images;
using PacmanManager.RepoHost.Models;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests of renaming a repository: the old name's <c>307</c> to the new one while the
/// redirect window lasts, its <c>410</c> afterwards, and the hold on the old name in between, against
/// the containerized API and a real <c>pacman</c> client.
/// </summary>
/// <remarks>
/// The API runs with a redirect window short enough to wait out, so this fixture has a stack of its
/// own rather than sharing one whose configuration other fixtures rely on. Every test renames a
/// repository of its own, and asserts on the redirect within moments of the rename.
/// </remarks>
[TestFixture]
public class RepositoryRenameTests
{
    /// <summary>
    /// Long enough for a test to fetch through the redirect right after renaming, short enough to wait
    /// out for the <c>410</c>.
    /// </summary>
    private static readonly TimeSpan RedirectWindow = TimeSpan.FromSeconds(20);

    private const string Arch = PackageFixtures.MinimalPackageArchitecture;

    private EndToEndTestFixture _fixture = null!;
    private HttpClient _ownerClient = null!;
    private HttpClient _otherUsersClient = null!;
    private HttpClient _anonymousClient = null!;
    private IContainer _pacmanClient = null!;
    private byte[] _packageBytes = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _fixture = new EndToEndTestFixture(new Dictionary<string, string>
        {
            ["RepositoryRenames__RedirectWindow"] = RedirectWindow.ToString("c"),
        });
        await _fixture.StartAsync();

        _ownerClient = _fixture.HttpClient;
        _ownerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _fixture.AuthContainer!.GetBearerTokenAsync(_fixture.AuthContainer.DefaultCredentials));

        _otherUsersClient = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
        _otherUsersClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _fixture.AuthContainer.GetBearerTokenAsync(_fixture.AuthContainer.SecondaryCredentials));

        // Redirects are asserted, not followed, unless a test follows one itself.
        _anonymousClient = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = new Uri(_fixture.BaseUrl)
        };

        _packageBytes = await File.ReadAllBytesAsync(PackageFixtures.MinimalPackagePath);

        // The same image the API is built on, so it is already present, and it carries pacman. Started
        // up front so that a test's rename is not waiting on it.
        _pacmanClient = new ContainerBuilder(new DockerImage("archlinux/archlinux"))
            .WithNetwork(_fixture.Network)
            .WithCommand("sleep", "infinity")
            .WithCleanUp(true)
            .Build();
        await _pacmanClient.StartAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _pacmanClient.DisposeAsync();
        _otherUsersClient.Dispose();
        _anonymousClient.Dispose();
        await _fixture.DisposeAsync();
    }

    [Test]
    public async Task OldDatabaseUrl_RedirectsTemporarilyToTheNewOne_AndTheRedirectLeadsToTheRightBytes()
    {
        await GivenAPublishedRepositoryAsync("rename-redirect-old", "rename-redirect-new");

        var redirect = await _anonymousClient.GetAsync(Url("rename-redirect-old", "rename-redirect-old.db"));
        var expected = await _anonymousClient.GetByteArrayAsync(Url("rename-redirect-new", "rename-redirect-new.db"));

        Assert.Multiple(() =>
        {
            Assert.That(redirect.StatusCode, Is.EqualTo(HttpStatusCode.TemporaryRedirect));
            Assert.That(redirect.Headers.Location?.OriginalString,
                Does.EndWith(Url("rename-redirect-new", "rename-redirect-new.db")));
            Assert.That(redirect.Headers.CacheControl?.NoStore, Is.True, "Cache-Control: no-store");
            Assert.That(redirect.Headers.Vary, Does.Contain("Authorization"), "Vary");
        });

        // Followed by a client that does follow redirects, as libcurl does for pacman.
        using var following = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
        var followed = await following.GetAsync(Url("rename-redirect-old", "rename-redirect-old.db"));
        var bytes = await followed.Content.ReadAsByteArrayAsync();

        Assert.Multiple(() =>
        {
            Assert.That(followed.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(followed.RequestMessage?.RequestUri?.AbsolutePath, Is.EqualTo(Url("rename-redirect-new", "rename-redirect-new.db")));
            Assert.That(bytes, Is.EqualTo(expected));
        });
    }

    [Test]
    public async Task OldPackageUrl_IsFollowedToTheSamePackage()
    {
        await GivenAPublishedRepositoryAsync("rename-package-old", "rename-package-new");

        using var following = new HttpClient { BaseAddress = new Uri(_fixture.BaseUrl) };
        var bytes = await following.GetByteArrayAsync(Url("rename-package-old", PackageFixtures.MinimalPackageFileName));

        Assert.That(bytes, Is.EqualTo(_packageBytes));
    }

    [Test]
    public async Task OldDatabaseUrl_IsGone_OnceTheRedirectWindowHasPassed()
    {
        var renamedAt = Stopwatch.StartNew();
        await GivenAPublishedRepositoryAsync("rename-gone-old", "rename-gone-new");

        var deadline = RedirectWindow + TimeSpan.FromSeconds(30);
        HttpResponseMessage response;
        do
        {
            await Task.Delay(TimeSpan.FromSeconds(1));
            response = await _anonymousClient.GetAsync(Url("rename-gone-old", "rename-gone-old.db"));
        } while (response.StatusCode == HttpStatusCode.TemporaryRedirect && renamedAt.Elapsed < deadline);

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Gone));
            Assert.That(renamedAt.Elapsed, Is.GreaterThanOrEqualTo(RedirectWindow), "not before the window had passed");
            Assert.That(response.Headers.CacheControl, Is.Null, "Cache-Control");
            Assert.That(response.Headers.Vary, Does.Contain("Authorization"), "Vary");
        });
    }

    [Test]
    public async Task OldName_CannotBeClaimedByAnotherUser_AndTheConflictIsTheSameAsForALiveName()
    {
        await GivenAPublishedRepositoryAsync("rename-held-old", "rename-held-new");

        var byRetirement = await _otherUsersClient.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = "rename-held-old" });
        var byRepository = await _otherUsersClient.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = "rename-held-new" });

        Assert.Multiple(async () =>
        {
            Assert.That(byRetirement.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(byRepository.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
            Assert.That(await ComparableBodyAsync(byRetirement), Is.EqualTo(await ComparableBodyAsync(byRepository)));
        });
    }

    [Test]
    public async Task OldName_CanBeTakenBackByTheRepositoryThatRetiredIt()
    {
        var repository = await GivenAPublishedRepositoryAsync("rename-back-old", "rename-back-new");

        var response = await RenameAsync(repository, "rename-back-old");
        var served = await _anonymousClient.GetAsync(Url("rename-back-old", "rename-back-old.db"));

        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(served.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        });
    }

    /// <summary>
    /// What makes the redirect safe: <c>pacman -Sy</c> follows it silently and still writes the local
    /// database under the section name it was configured with, so the client keeps working unchanged.
    /// </summary>
    [Test]
    public async Task Pacman_SyncsThroughTheRedirect_AndKeepsTheOldSectionName()
    {
        const string oldName = "rename-pacman-old";
        await GivenAPublishedRepositoryAsync(oldName, "rename-pacman-new");

        var conf = $"""
            [options]
            Architecture = {Arch}
            SigLevel = Optional TrustAll

            [{oldName}]
            Server = http://{EndToEndTestFixture.ApiHostname}:8080/pacman/$repo/$arch
            SigLevel = Optional TrustAll
            """;
        await _pacmanClient.CopyAsync(Encoding.UTF8.GetBytes(conf), "/tmp/rename.conf");
        await ExecInPacmanClientAsync("mkdir", "-p", "/tmp/rename-db");

        await ExecInPacmanClientAsync("pacman", "--config", "/tmp/rename.conf", "--dbpath", "/tmp/rename-db", "-Sy");
        var listing = await ExecInPacmanClientAsync(
            "pacman", "--config", "/tmp/rename.conf", "--dbpath", "/tmp/rename-db", "-Sl", oldName);
        var syncDir = await ExecInPacmanClientAsync("ls", "/tmp/rename-db/sync");

        Assert.Multiple(() =>
        {
            Assert.That(syncDir.Split('\n', StringSplitOptions.RemoveEmptyEntries), Is.EqualTo(new[] { $"{oldName}.db" }));
            Assert.That(listing, Does.Contain($"{oldName} {PackageFixtures.MinimalPackageName} {PackageFixtures.MinimalPackageVersion}"));
        });
    }

    #region Helpers

    private static string Url(string repoName, string fileName) => $"/pacman/{repoName}/{Arch}/{fileName}";

    /// <summary>
    /// A problem details body without the members that differ between any two requests.
    /// </summary>
    private static async Task<string> ComparableBodyAsync(HttpResponseMessage response)
    {
        var body = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
        body.Remove("traceId");
        return body.ToJsonString();
    }

    private async Task<string> ExecInPacmanClientAsync(params string[] command)
    {
        var result = await _pacmanClient.ExecAsync(command);
        Assert.That(result.ExitCode, Is.Zero,
            $"'{string.Join(' ', command)}' exited with {result.ExitCode}: {result.Stdout}{result.Stderr}");
        return result.Stdout;
    }

    private async Task<HttpResponseMessage> RenameAsync(Repository repository, string newName) =>
        await _ownerClient.PutAsJsonAsync($"/api/v1/repositories/{repository.Id}", new WriteRepositoryRequest
        {
            Name = newName,
            SupportedArchitectures = repository.SupportedArchitectures,
            IsPublic = repository.IsPublic,
        });

    /// <summary>
    /// Creates a public repository, publishes the fixture package into it so that its files are the
    /// ones the application itself wrote, and renames it.
    /// </summary>
    private async Task<Repository> GivenAPublishedRepositoryAsync(string oldName, string newName)
    {
        var created = await _ownerClient.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = oldName,
            SupportedArchitectures = [Arch],
            IsPublic = true,
        });
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            $"Arranging '{oldName}' failed: {await created.Content.ReadAsStringAsync()}");
        var repository = (await created.Content.ReadFromJsonAsync<Repository>())!;

        var body = new ByteArrayContent(_packageBytes);
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var published = await _ownerClient.PostAsync($"/api/v1/repositories/{repository.Id}/packages", body);
        Assert.That(published.IsSuccessStatusCode, Is.True,
            $"Publishing to '{oldName}' failed: {await published.Content.ReadAsStringAsync()}");

        var renamed = await RenameAsync(repository, newName);
        Assert.That(renamed.StatusCode, Is.EqualTo(HttpStatusCode.OK),
            $"Renaming '{oldName}' failed: {await renamed.Content.ReadAsStringAsync()}");

        return (await renamed.Content.ReadFromJsonAsync<Repository>())!;
    }

    #endregion
}
