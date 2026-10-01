using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using DotNet.Testcontainers.Containers;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Test.Containers;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests in which a real <c>pacman</c>, in a stock Arch Linux container on the test
/// network, syncs from the containerized API and installs the fixture package from it.
/// </summary>
/// <remarks>
/// <para>
/// Every other test of the <c>/pacman</c> routes asserts a part; these assert that the parts add up to
/// something <c>pacman</c> can use. The client is configured the way the documentation tells a user to
/// configure it: one section per repository, named after it, with a single
/// <c>Server = …/pacman/$repo/$arch</c> line that <c>pacman</c> itself expands, credentials in the
/// URL's userinfo for a private repository, and <c>SigLevel = Optional TrustAll</c>, since nothing is
/// signed.
/// </para>
/// <para>
/// Each test gets a client container of its own, so that one test's synced database or installed
/// package is never another's starting point. The API, and the repositories published into it, are
/// shared by the whole fixture.
/// </para>
/// </remarks>
[TestFixture]
public class PacmanClientTests
{
    private const string PublicRepo = "pacman-client-public";
    private const string PrivateRepo = "pacman-client-private";

    private EndToEndTestFixture _fixture = null!;
    private CreatedAccessToken _token = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _fixture = new EndToEndTestFixture();
        await _fixture.StartAsync();

        var owner = _fixture.HttpClient;
        owner.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _fixture.AuthContainer!.GetBearerTokenAsync(_fixture.AuthContainer.DefaultCredentials));

        await GivenAPublishedRepositoryAsync(owner, PublicRepo, isPublic: true);
        await GivenAPublishedRepositoryAsync(owner, PrivateRepo, isPublic: false);

        var mint = await owner.PostAsJsonAsync("/api/v1/users/me/tokens",
            new CreateAccessTokenRequest { Name = "pacman-client" });
        var mintBody = await mint.Content.ReadAsStringAsync();
        Assert.That(mint.StatusCode, Is.EqualTo(HttpStatusCode.Created), mintBody);
        _token = JsonSerializer.Deserialize<CreatedAccessToken>(mintBody, JsonSerializerOptions.Web)!;
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _fixture.DisposeAsync();
    }

    /// <summary>
    /// The repositories a client installs from: the public one with no credentials configured at
    /// all, and the private one with its owner's token.
    /// </summary>
    private static IEnumerable<TestCaseData> Repositories()
    {
        yield return new TestCaseData(PublicRepo, false).SetArgDisplayNames("public, anonymously");
        yield return new TestCaseData(PrivateRepo, true).SetArgDisplayNames("private, with Basic credentials");
    }

    [TestCaseSource(nameof(Repositories))]
    public async Task Pacman_SyncsAndInstallsThePackage(string repoName, bool withCredentials)
    {
        await using var client = await GivenAClientOfAsync(repoName, withCredentials);

        var sync = await SyncAsync(client);
        var install = await RunAsync(client, "pacman", "-S", "--noconfirm", PackageFixtures.MinimalPackageName);
        var installed = await RunAsync(client, "pacman", "-Q", PackageFixtures.MinimalPackageName);
        var readme = await client.ExecAsync("test", "-f", $"/usr/share/doc/{PackageFixtures.MinimalPackageName}/README");

        Assert.Multiple(() =>
        {
            Assert.That(sync.Stdout, Does.Not.Contain("is up to date"), "the first sync downloads the database");
            Assert.That(install.Stdout, Does.Contain(PackageFixtures.MinimalPackageName), "the install names the package");
            Assert.That(installed.Stdout.Trim(),
                Is.EqualTo($"{PackageFixtures.MinimalPackageName} {PackageFixtures.MinimalPackageVersion}"));
            Assert.That(readme.ExitCode, Is.Zero, "the package's file is on disk");
        });
    }

    /// <summary>
    /// libalpm sends <c>If-Modified-Since</c> from the local database's modification time, which it
    /// set from the <c>Last-Modified</c> it was served. A <c>304</c> is what <c>pacman</c> reports as
    /// up to date, so this is the conditional-GET path proved end to end rather than by headers.
    /// </summary>
    [TestCaseSource(nameof(Repositories))]
    public async Task Pacman_SecondSyncWithNoInterveningPublish_IsUpToDate(string repoName, bool withCredentials)
    {
        await using var client = await GivenAClientOfAsync(repoName, withCredentials);

        var first = await SyncAsync(client);
        var second = await SyncAsync(client);

        Assert.Multiple(() =>
        {
            Assert.That(first.Stdout, Does.Not.Contain($"{repoName} is up to date"));
            Assert.That(second.Stdout, Does.Contain($"{repoName} is up to date"));
        });
    }

    #region Helpers

    /// <summary>
    /// Starts a client container whose <c>pacman.conf</c> carries a single section for
    /// <paramref name="repoName"/>.
    /// </summary>
    /// <remarks>
    /// The configuration is written from scratch rather than appended to the image's own, which would
    /// send every <c>pacman -Sy</c> to the Arch mirrors as well. It also leaves out the stock
    /// <c>DownloadUser</c>, whose download sandbox depends on what the container runtime permits and
    /// says nothing about the server. The <c>$repo</c> and <c>$arch</c> in the server line are
    /// literal: expanding them is <c>pacman</c>'s job, and is part of what is under test.
    /// </remarks>
    private async Task<PacmanClientContainer> GivenAClientOfAsync(string repoName, bool withCredentials)
    {
        var server = new UriBuilder(EndToEndTestFixture.NetworkBaseUrl);
        if (withCredentials)
        {
            server.UserName = _token.Username;
            server.Password = _token.Secret;
        }

        var client = new PacmanClientContainer(_fixture.Network);
        await client.StartAsync();
        await client.WritePacmanConfAsync($"""
            [options]
            Architecture = {PackageFixtures.MinimalPackageArchitecture}

            [{repoName}]
            SigLevel = Optional TrustAll
            Server = {server.Uri.ToString().TrimEnd('/')}/pacman/$repo/$arch

            """);
        return client;
    }

    /// <summary>
    /// Runs <c>pacman -Sy</c> in the client, on a terminal.
    /// </summary>
    /// <remarks>
    /// <c>pacman</c> reports a database as up to date only through its progress display, which it
    /// draws only on a terminal; without one, a <c>304</c> prints "downloading..." exactly as a
    /// download does. <c>script</c> runs the command on a pseudo-terminal and, with <c>-e</c>, exits
    /// with its exit code. The output then carries the display's escape sequences, which the
    /// assertions here do not mind, since they look for a substring.
    /// </remarks>
    private static Task<ExecResult> SyncAsync(PacmanClientContainer client) =>
        RunAsync(client, "script", "--quiet", "--return", "--command", "pacman -Sy --noconfirm", "/dev/null");

    /// <summary>
    /// Runs a command in the client and fails the test, with everything the command wrote, if it does
    /// not succeed.
    /// </summary>
    private static async Task<ExecResult> RunAsync(PacmanClientContainer client, params string[] command)
    {
        var result = await client.ExecAsync(command);
        Assert.That(result.ExitCode, Is.Zero,
            $"'{string.Join(' ', command)}' exited with {result.ExitCode}.\nstdout:\n{result.Stdout}\nstderr:\n{result.Stderr}");
        return result;
    }

    /// <summary>
    /// Creates a repository and publishes the fixture package into it over the real API.
    /// </summary>
    private static async Task GivenAPublishedRepositoryAsync(HttpClient owner, string name, bool isPublic)
    {
        var created = await owner.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = name,
            SupportedArchitectures = [PackageFixtures.MinimalPackageArchitecture],
            IsPublic = isPublic,
        });
        Assert.That(created.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            $"Arranging '{name}' failed: {await created.Content.ReadAsStringAsync()}");
        var repository = (await created.Content.ReadFromJsonAsync<Repository>())!;

        var body = new ByteArrayContent(await File.ReadAllBytesAsync(PackageFixtures.MinimalPackagePath));
        body.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        var published = await owner.PostAsync($"/api/v1/repositories/{repository.Id}/packages", body);
        Assert.That(published.IsSuccessStatusCode, Is.True,
            $"Publishing to '{name}' failed: {await published.Content.ReadAsStringAsync()}");
    }

    #endregion
}
