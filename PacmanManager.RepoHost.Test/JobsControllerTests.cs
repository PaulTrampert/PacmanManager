using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;
using JobEntity = PacmanManager.Entities.Job;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests for <c>GET /api/v1/Jobs</c> and <c>GET /api/v1/Jobs/{id}</c>, against the
/// containerized API.
/// </summary>
/// <remarks>
/// Nothing schedules a job over HTTP yet, so each test seeds the job it reads straight into the
/// database, for the user the fixture's default credentials sign in as. The tests share one database,
/// so a listing test asserts on the jobs it seeded rather than on the whole listing.
/// </remarks>
[TestFixture]
public class JobsControllerTests
{
    private const string JobsRoute = "/api/v1/Jobs";

    private EndToEndTestFixture _fixture = null!;
    private HttpClient _client = null!;
    private HttpClient _otherUsersClient = null!;
    private HttpClient _anonymousClient = null!;
    private Guid _userId;

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

        // Signing in creates the user, so the caller's id is known only once it has made a request.
        _userId = (await _client.GetFromJsonAsync<CurrentUser>("/api/v1/users/me"))!.Id;
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _otherUsersClient.Dispose();
        _anonymousClient.Dispose();
        await _fixture.DisposeAsync();
    }

    [Test]
    public async Task GetById_TheSchedulersCompletedJob_ShowsItsAffectedEntities()
    {
        var repositoryId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var job = await SeedAsync(_userId, JobState.Completed, null,
            (JobEntityType.Package, packageId),
            (JobEntityType.Repository, repositoryId));

        var response = await _client.GetAsync($"{JobsRoute}/{job.Id}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);

        var json = JsonDocument.Parse(body).RootElement;
        var affected = json.GetProperty("affectedEntities").EnumerateArray()
            .Select(e => (e.GetProperty("type").GetString(), e.GetProperty("id").GetGuid()))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(json.GetProperty("id").GetGuid(), Is.EqualTo(job.Id));
            Assert.That(json.GetProperty("state").GetString(), Is.EqualTo("Completed"));
            Assert.That(json.GetProperty("createdAt").GetDateTimeOffset(), Is.EqualTo(job.CreatedAt).Within(TimeSpan.FromMilliseconds(1)));
            Assert.That(affected, Is.EqualTo(new[] { ("Repository", repositoryId), ("Package", packageId) }));
            Assert.That(json.TryGetProperty("error", out _), Is.False, "a completed job has no error");
        });
    }

    [Test]
    public async Task GetById_AFailedJob_CarriesItsError()
    {
        var job = await SeedAsync(_userId, JobState.Failed, "The repository does not support aarch64.",
            (JobEntityType.Repository, Guid.NewGuid()));

        var response = await _client.GetAsync($"{JobsRoute}/{job.Id}");
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);

        var json = JsonDocument.Parse(body).RootElement;
        Assert.Multiple(() =>
        {
            Assert.That(json.GetProperty("state").GetString(), Is.EqualTo("Failed"));
            Assert.That(json.GetProperty("error").GetString(), Is.EqualTo("The repository does not support aarch64."));
        });
    }

    [Test]
    public async Task GetById_AnotherUsersJob_IsNotFound()
    {
        var job = await SeedAsync(_userId, JobState.Pending, null);

        var response = await _otherUsersClient.GetAsync($"{JobsRoute}/{job.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetById_NoSuchJob_IsNotFound()
    {
        var response = await _client.GetAsync($"{JobsRoute}/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetById_Anonymous_IsUnauthorized()
    {
        var job = await SeedAsync(_userId, JobState.Pending, null);

        var existing = await _anonymousClient.GetAsync($"{JobsRoute}/{job.Id}");
        var missing = await _anonymousClient.GetAsync($"{JobsRoute}/{Guid.NewGuid()}");

        Assert.Multiple(() =>
        {
            Assert.That(existing.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "an existing job");
            Assert.That(missing.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized), "no such job");
        });
    }

    [Test]
    public async Task List_ReturnsTheCallersOwnJobs_NewestFirst_WithTheirAffectedEntities()
    {
        // Scheduled in the future, so that they head the newest-first listing whatever else the other
        // tests have seeded.
        var now = DateTimeOffset.UtcNow;
        var repositoryId = Guid.NewGuid();
        var packageId = Guid.NewGuid();
        var older = await SeedAtAsync(now.AddHours(1), _userId, JobState.Completed, null,
            (JobEntityType.Package, packageId),
            (JobEntityType.Repository, repositoryId));
        var newer = await SeedAtAsync(now.AddHours(2), _userId, JobState.Pending, null);
        var othersJob = await SeedAtAsync(now.AddHours(3), null, JobState.Pending, null);

        var response = await _client.GetAsync(JobsRoute);
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);

        var json = JsonDocument.Parse(body).RootElement;
        var results = json.GetProperty("results").EnumerateArray().ToList();
        var ids = results.Select(r => r.GetProperty("id").GetGuid()).ToList();
        var olderJson = results.Single(r => r.GetProperty("id").GetGuid() == older.Id);
        var affected = olderJson.GetProperty("affectedEntities").EnumerateArray()
            .Select(e => (e.GetProperty("type").GetString(), e.GetProperty("id").GetGuid()))
            .ToList();

        Assert.Multiple(() =>
        {
            Assert.That(ids.Take(2), Is.EqualTo(new[] { newer.Id, older.Id }), "newest first");
            Assert.That(ids, Does.Not.Contain(othersJob.Id), "a job the caller did not schedule");
            Assert.That(olderJson.GetProperty("state").GetString(), Is.EqualTo("Completed"));
            Assert.That(affected, Is.EqualTo(new[] { ("Repository", repositoryId), ("Package", packageId) }));
            Assert.That(json.GetProperty("total").GetInt32(), Is.GreaterThanOrEqualTo(2));
            Assert.That(json.GetProperty("sort").GetProperty("sortBy").GetString(), Is.EqualTo("CreatedAt"));
            Assert.That(json.GetProperty("sort").GetProperty("direction").GetString(), Is.EqualTo("Descending"));
        });
    }

    [Test]
    public async Task List_AnotherUser_DoesNotSeeTheCallersJobs()
    {
        var job = await SeedAsync(_userId, JobState.Failed, "nope");

        var response = await _otherUsersClient.GetAsync($"{JobsRoute}?state=Failed&pageSize=500");
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);

        var ids = JsonDocument.Parse(body).RootElement.GetProperty("results").EnumerateArray()
            .Select(r => r.GetProperty("id").GetGuid());
        Assert.That(ids, Does.Not.Contain(job.Id));
    }

    [Test]
    public async Task List_StateFilter_ReturnsOnlyTheCallersJobsInThatState()
    {
        var failed = await SeedAsync(_userId, JobState.Failed, "The repository does not support aarch64.");
        var running = await SeedAsync(_userId, JobState.Running, null);
        var systemsFailed = await SeedAsync(null, JobState.Failed, "nope");

        var response = await _client.GetAsync($"{JobsRoute}?state=Failed&pageSize=500");
        var body = await response.Content.ReadAsStringAsync();
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK), body);

        var results = JsonDocument.Parse(body).RootElement.GetProperty("results").EnumerateArray().ToList();
        var ids = results.Select(r => r.GetProperty("id").GetGuid()).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(ids, Does.Contain(failed.Id));
            Assert.That(ids, Does.Not.Contain(running.Id), "a job in another state");
            Assert.That(ids, Does.Not.Contain(systemsFailed.Id), "a job the caller did not schedule");
            Assert.That(results.Select(r => r.GetProperty("state").GetString()), Is.All.EqualTo("Failed"));
        });
    }

    [Test]
    public async Task List_Anonymous_IsUnauthorized()
    {
        var response = await _anonymousClient.GetAsync(JobsRoute);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    private Task<JobEntity> SeedAsync(
        Guid? userId,
        JobState state,
        string? error,
        params (JobEntityType Type, Guid Id)[] affected) =>
        SeedAtAsync(DateTimeOffset.UtcNow, userId, state, error, affected);

    private async Task<JobEntity> SeedAtAsync(
        DateTimeOffset createdAt,
        Guid? userId,
        JobState state,
        string? error,
        params (JobEntityType Type, Guid Id)[] affected)
    {
        var job = new JobEntity
        {
            CreatedAt = createdAt,
            UserId = userId,
            State = state,
            Error = error,
            MessageType = "PacmanManager.RepoHost.Test.JobsControllerTests.Message",
            Message = "{}",
        };
        foreach (var (type, id) in affected)
        {
            job.AffectedEntities.Add(new JobAffectedEntity { JobId = job.Id, EntityType = type, EntityId = id });
        }

        await using var db = _fixture.CreateDbContext();
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }
}
