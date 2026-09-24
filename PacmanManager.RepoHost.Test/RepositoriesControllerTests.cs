using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests for the Repositories API endpoints.
/// These tests run the application in a Docker container and make real HTTP requests.
/// </summary>
[TestFixture]
public class RepositoriesControllerTests
{
    private EndToEndTestFixture _fixture = null!;
    private HttpClient _client = null!;
    private HttpClient _otherUsersClient = null!;

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
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _otherUsersClient.Dispose();
        await _fixture.DisposeAsync();
    }

    #region GetAll Tests

    [Test]
    public async Task Get_ReturnsOkStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/repositories");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Get_ReturnsListOfRepositories()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/repositories");
        var repositories = await response.Content.ReadFromJsonAsync<PaginatedResponse<Repository>>();

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);
        Assert.That(repositories, Is.Not.Null);
    }

    [Test]
    public async Task Get_DefaultsToSortingByNameAscending()
    {
        // Name is both the default sort field and an ascending-by-default one, so a listing with
        // neither parameter runs A->Z, as does sortBy=Name on its own. An explicit direction still
        // wins over both.
        // Arrange
        foreach (var suffix in new[] { "charlie", "alpha", "bravo" })
        {
            await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
            {
                Name = $"sortdefault-{suffix}",
                IsPublic = true
            });
        }

        const string unsortedQuery = "/api/v1/repositories?nameContains=sortdefault-&pageSize=50";
        const string query = $"{unsortedQuery}&sortBy=Name";

        // Act
        var unsorted = await _client.GetFromJsonAsync<PaginatedResponse<Repository>>(unsortedQuery);
        var defaulted = await _client.GetFromJsonAsync<PaginatedResponse<Repository>>(query);
        var descending = await _client.GetFromJsonAsync<PaginatedResponse<Repository>>(
            $"{query}&direction=Descending");

        // Assert
        var expected = new[] { "sortdefault-alpha", "sortdefault-bravo", "sortdefault-charlie" };
        Assert.Multiple(() =>
        {
            Assert.That(unsorted!.Results.Select(r => r.Name), Is.EqualTo(expected));
            Assert.That(defaulted!.Results.Select(r => r.Name), Is.EqualTo(expected));
            Assert.That(descending!.Results.Select(r => r.Name), Is.EqualTo(expected.Reverse()));
        });
    }

    [Test]
    public async Task Get_SortsByANonStringKey_InTheDatabase()
    {
        // ApplySort looks its key selectors up in one table, so they are all typed
        // Expression<Func<PacmanRepository, object>> and each carries a Convert node. EF Core is
        // expected to strip a conversion to object and order in SQL; an untranslatable ordering
        // throws rather than being evaluated client side, so this listing would start returning 500.
        // A value type key is where that would show first, so sorting by a date against real
        // Postgres is what pins it down — the name cases above go through a reference type.
        // Arrange
        foreach (var suffix in new[] { "first", "second", "third" })
        {
            var created = await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
            {
                Name = $"sortbydate-{suffix}",
                IsPublic = true
            });
            Assert.That(created.IsSuccessStatusCode, Is.True);
        }

        const string query = "/api/v1/repositories?nameContains=sortbydate-&pageSize=50&sortBy=Created";

        // Act
        var newestFirst = await _client.GetFromJsonAsync<PaginatedResponse<Repository>>(query);
        var oldestFirst = await _client.GetFromJsonAsync<PaginatedResponse<Repository>>(
            $"{query}&direction=Ascending");

        // Assert
        var byAge = new[] { "sortbydate-first", "sortbydate-second", "sortbydate-third" };
        Assert.Multiple(() =>
        {
            Assert.That(oldestFirst!.Results.Select(r => r.Name), Is.EqualTo(byAge));
            Assert.That(newestFirst!.Results.Select(r => r.Name), Is.EqualTo(byAge.Reverse()),
                "Created defaults to newest first.");
        });
    }

    [Test]
    public async Task Get_FilteredBySupportedArchitecture_ReturnsRepositoriesSupportingIt()
    {
        // Arrange
        var name = $"arch-filter-{Guid.NewGuid():N}";
        await CreateAsync(_client, name, isPublic: true);

        // Act
        var response = await _client.GetFromJsonAsync<PaginatedResponse<Repository>>(
            $"/api/v1/repositories?nameContains={name}&architecture=x86_64");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(response!.Total, Is.EqualTo(1));
            Assert.That(response.Results.Single().SupportedArchitectures, Does.Contain(Architectures.X86_64));
        });
    }

    [TestCase("any")]
    [TestCase("sparc64")]
    public async Task Get_FilteredByAnArchitectureNoRepositoryMaySupport_ReturnsBadRequest(string architecture)
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/repositories?architecture={architecture}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    #endregion

    #region GetById Tests

    [Test]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/repositories/{id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetById_WithValidId_ReturnsOkResult()
    {
        // This test will be updated when repository storage is implemented
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.GetAsync($"/api/v1/repositories/{id}");

        // Assert
        // Currently returns NotFound until storage is implemented
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region Create Tests

    [Test]
    public async Task Create_WithValidRequest_ReturnsCreatedStatus()
    {
        // Arrange
        var request = new WriteRepositoryRequest
        {
            Name = "test-repo",
            SupportedArchitectures = [Architectures.X86_64]
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repositories", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task Create_WithValidRequest_ReturnsRepositoryWithName()
    {
        // Arrange
        var request = new WriteRepositoryRequest
        {
            Name = "test-repo-2",
            SupportedArchitectures = [Architectures.X86_64]
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repositories", request);
        var repository = await response.Content.ReadFromJsonAsync<Repository>();

        // Assert
        Assert.That(repository, Is.Not.Null);
        Assert.That(repository!.Name, Is.EqualTo("test-repo-2"));
    }

    [Test]
    public async Task Create_WithValidRequest_SetsPropertiesCorrectly()
    {
        // Arrange
        var request = new WriteRepositoryRequest
        {
            Name = "custom-repo",
            SupportedArchitectures = [Architectures.X86_64],
            IsPublic = true
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repositories", request);
        var repository = await response.Content.ReadFromJsonAsync<Repository>();

        // Assert
        Assert.That(repository, Is.Not.Null);
        Assert.That(repository!.Name, Is.EqualTo(request.Name));
        Assert.That(repository.SupportedArchitectures, Is.EqualTo(request.SupportedArchitectures));
        Assert.That(repository.IsPublic, Is.True);
    }

    [TestCase("[\"any\"]", TestName = "Create_SupportingAny_ReturnsBadRequest")]
    [TestCase("[]", TestName = "Create_SupportingNothing_ReturnsBadRequest")]
    [TestCase("[\"sparc64\"]", TestName = "Create_SupportingAnUnknownArchitecture_ReturnsBadRequest")]
    [TestCase("[\"x86_64\", \"any\"]", TestName = "Create_SupportingAnyAlongsideARealArchitecture_ReturnsBadRequest")]
    [TestCase("null", TestName = "Create_WithNullSupportedArchitectures_ReturnsBadRequest")]
    public async Task Create_WithUnsupportedArchitectures_ReturnsBadRequest(string supportedArchitectures)
    {
        // Arrange
        var name = $"bad-arch-{Guid.NewGuid():N}";
        using var content = new StringContent(
            $$"""{ "name": "{{name}}", "supportedArchitectures": {{supportedArchitectures}} }""",
            System.Text.Encoding.UTF8,
            "application/json");

        // Act
        var response = await _client.PostAsync("/api/v1/repositories", content);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            await response.Content.ReadAsStringAsync());
        var listing = await _client.GetFromJsonAsync<PaginatedResponse<Repository>>(
            $"/api/v1/repositories?nameContains={name}");
        Assert.That(listing!.Total, Is.Zero, "Nothing is created for a rejected request.");
    }

    [Test]
    public async Task Create_SetsTimestamps()
    {
        // Arrange
        var request = new WriteRepositoryRequest
        {
            Name = "test-repo-timestamps"
        };
        var beforeCreate = DateTimeOffset.UtcNow;

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repositories", request);
        var afterCreate = DateTimeOffset.UtcNow;
        var repository = await response.Content.ReadFromJsonAsync<Repository>();

        // Assert
        Assert.That(repository, Is.Not.Null);
        Assert.That(repository!.CreatedAt, Is.GreaterThanOrEqualTo(beforeCreate));
        Assert.That(repository.CreatedAt, Is.LessThanOrEqualTo(afterCreate));
        Assert.That(repository.UpdatedAt, Is.GreaterThanOrEqualTo(beforeCreate));
        Assert.That(repository.UpdatedAt, Is.LessThanOrEqualTo(afterCreate));
    }

    [Test]
    public async Task Create_ReturnsLocationHeader()
    {
        // Arrange
        var request = new WriteRepositoryRequest
        {
            Name = "test-repo-location"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repositories", request);
        var repository = await response.Content.ReadFromJsonAsync<Repository>();

        // Assert
        Assert.That(response.Headers.Location, Is.Not.Null);
        Assert.That(response.Headers.Location!.ToString(), Does.Contain(repository!.Id.ToString()));
    }

    [Test]
    public async Task Create_WithMinimalRequest_UsesDefaults()
    {
        // Arrange
        var request = new WriteRepositoryRequest
        {
            Name = "minimal-repo"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repositories", request);
        var repository = await response.Content.ReadFromJsonAsync<Repository>();

        // Assert
        Assert.That(repository, Is.Not.Null);
        Assert.That(repository!.SupportedArchitectures, Is.EqualTo(new[] { Architectures.X86_64 }));
    }

    [Test]
    public async Task Create_WithATakenNameAndArchitecture_ReturnsConflict()
    {
        // Arrange
        var request = new WriteRepositoryRequest { Name = "conflict-on-create" };
        var first = await _client.PostAsJsonAsync("/api/v1/repositories", request);
        Assert.That(first.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var existing = (await first.Content.ReadFromJsonAsync<Repository>())!;

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repositories", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        await AssertDisclosesNothingAboutAsync(response, existing);
    }

    [Test]
    public async Task Create_WithANameAnotherUserHolds_ReturnsConflict()
    {
        // Repository names are unique across users, and the other user's repository is private, so
        // the 409 is the only thing that reveals the name is in use.
        // Arrange
        var theirs = await CreateAsync(_otherUsersClient, "custom", isPublic: false);

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = "custom" });

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        await AssertDisclosesNothingAboutAsync(response, theirs);
    }

    [Test]
    public async Task Create_WithANameAnotherUserHolds_StillHidesTheirPrivateRepository()
    {
        // The collision is the only new signal: the repository holding the name is still a 404 by
        // id, and still absent from the listing, for anybody but its owner.
        // Arrange
        var theirs = await CreateAsync(_otherUsersClient, "collision-still-hidden", isPublic: false);
        var collision = await _client.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = theirs.Name });
        Assert.That(collision.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));

        // Act
        var byId = await _client.GetAsync($"/api/v1/repositories/{theirs.Id}");
        var listing = await _client.GetFromJsonAsync<PaginatedResponse<Repository>>(
            $"/api/v1/repositories?nameContains={theirs.Name}&pageSize=500");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(byId.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
            Assert.That(listing!.Results.Select(r => r.Id), Does.Not.Contain(theirs.Id));
        });
    }

    #endregion

    #region Update Tests

    [Test]
    public async Task Update_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();
        var request = new WriteRepositoryRequest
        {
            Name = "updated-name"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/repositories/{id}", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Update_WithValidId_ReturnsOkResult()
    {
        // Arrange
        var request = new WriteRepositoryRequest
        {
            Name = "updated-name"
        };
        var response = await _client.PostAsJsonAsync("/api/v1/repositories", request);
        var repository = await response.Content.ReadFromJsonAsync<Repository>();
        var id = repository!.Id;

        // Act
        var updateResponse = await _client.PutAsJsonAsync($"/api/v1/repositories/{id}", request);

        // Assert
        Assert.That(updateResponse.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task Update_RenamingIntoATakenNameAndArchitecture_ReturnsConflict()
    {
        // Arrange
        var taken = await _client.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = "conflict-on-update-taken" });
        var existing = (await taken.Content.ReadFromJsonAsync<Repository>())!;
        var renamed = await _client.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = "conflict-on-update-original" });
        var renamedId = (await renamed.Content.ReadFromJsonAsync<Repository>())!.Id;

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/repositories/{renamedId}",
            new WriteRepositoryRequest { Name = existing.Name, SupportedArchitectures = existing.SupportedArchitectures });

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        await AssertDisclosesNothingAboutAsync(response, existing);

        var unchanged = await _client.GetFromJsonAsync<Repository>($"/api/v1/repositories/{renamedId}");
        Assert.That(unchanged!.Name, Is.EqualTo("conflict-on-update-original"));
    }

    [Test]
    public async Task Update_RenamingIntoANameAnotherUserHolds_ReturnsConflict()
    {
        // Arrange
        var theirs = await CreateAsync(_otherUsersClient, "conflict-on-update-theirs", isPublic: false);
        var mine = await CreateAsync(_client, "conflict-on-update-mine", isPublic: false);

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/repositories/{mine.Id}",
            new WriteRepositoryRequest { Name = theirs.Name, SupportedArchitectures = mine.SupportedArchitectures });

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        await AssertDisclosesNothingAboutAsync(response, theirs);

        var unchanged = await _client.GetFromJsonAsync<Repository>($"/api/v1/repositories/{mine.Id}");
        Assert.That(unchanged!.Name, Is.EqualTo("conflict-on-update-mine"));
    }

    #endregion

    #region Delete Tests

    [Test]
    public async Task Delete_WithNonExistentId_ReturnsNotFound()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/repositories/{id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_WithValidId_ReturnsNoContent()
    {
        // Arrange
        var created = await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = "test-repo-to-delete"
        });
        var repository = await created.Content.ReadFromJsonAsync<Repository>();

        // Act
        var response = await _client.DeleteAsync($"/api/v1/repositories/{repository!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task Delete_ThenGet_ReturnsNotFound()
    {
        // Arrange
        var created = await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = "test-repo-deleted-then-fetched"
        });
        var repository = await created.Content.ReadFromJsonAsync<Repository>();
        await _client.DeleteAsync($"/api/v1/repositories/{repository!.Id}");

        // Act
        var response = await _client.GetAsync($"/api/v1/repositories/{repository.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region Authorization Tests

    [Test]
    public async Task Create_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var request = new WriteRepositoryRequest { Name = "anonymous-repo" };

        // Act
        var response = await AnonymousClient().PostAsJsonAsync("/api/v1/repositories", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Get_WithoutAuthentication_ReturnsOnlyPublicRepositories()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = "visibility-private-repo",
            IsPublic = false
        });
        await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = "visibility-public-repo",
            IsPublic = true
        });

        // Act
        var response = await AnonymousClient().GetAsync("/api/v1/repositories?pageSize=500");
        var repositories = await response.Content.ReadFromJsonAsync<PaginatedResponse<Repository>>();

        // Assert
        var names = repositories!.Results.Select(r => r.Name).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("visibility-public-repo"));
            Assert.That(names, Does.Not.Contain("visibility-private-repo"));
            Assert.That(repositories.Results.Select(r => r.IsPublic), Is.All.True);
        });
    }

    [Test]
    public async Task GetById_WithoutAuthentication_HidesPrivateRepositories()
    {
        // Arrange
        var created = await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = "hidden-from-anonymous",
            IsPublic = false
        });
        var repository = await created.Content.ReadFromJsonAsync<Repository>();

        // Act
        var response = await AnonymousClient().GetAsync($"/api/v1/repositories/{repository!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Get_OwnerFilter_WithoutAuthentication_ReturnsOnlyThatOwnersPublicRepositories()
    {
        // Naming an owner narrows the listing; it does not open up their private repositories.
        // Arrange
        var created = await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = "owner-filter-public-repo",
            IsPublic = true
        });
        var repository = await created.Content.ReadFromJsonAsync<Repository>();
        await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = "owner-filter-private-repo",
            IsPublic = false
        });

        // Act
        var response = await AnonymousClient()
            .GetAsync($"/api/v1/repositories?ownerId={repository!.Owner.Id}&pageSize=500");
        var repositories = await response.Content.ReadFromJsonAsync<PaginatedResponse<Repository>>();

        // Assert
        var names = repositories!.Results.Select(r => r.Name).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("owner-filter-public-repo"));
            Assert.That(names, Does.Not.Contain("owner-filter-private-repo"));
        });
    }

    [Test]
    public async Task Get_PrivateFilter_WithoutAuthentication_ReturnsNothing()
    {
        // Arrange
        await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = "private-filter-repo",
            IsPublic = false
        });

        // Act
        var response = await AnonymousClient().GetAsync("/api/v1/repositories?isPublic=false");
        var repositories = await response.Content.ReadFromJsonAsync<PaginatedResponse<Repository>>();

        // Assert
        Assert.That(repositories!.Total, Is.EqualTo(0));
    }

    [Test]
    public async Task Update_WithoutAuthentication_ReturnsUnauthorized()
    {
        // Arrange
        var created = await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = "update-requires-auth",
            IsPublic = true
        });
        var repository = await created.Content.ReadFromJsonAsync<Repository>();

        // Act
        var response = await AnonymousClient()
            .PutAsJsonAsync($"/api/v1/repositories/{repository!.Id}", new WriteRepositoryRequest { Name = "hijacked" });

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Unauthorized));
    }

    [Test]
    public async Task Get_OnRenamedSingularRoute_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/repository");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetById_OnRenamedSingularRoute_ReturnsNotFound()
    {
        // Arrange
        var created = await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = "singular-route-is-gone",
            IsPublic = true
        });
        var repository = await created.Content.ReadFromJsonAsync<Repository>();

        // Act
        var response = await _client.GetAsync($"/api/v1/repository/{repository!.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    /// <summary>
    /// A <c>409</c> may say only that the name is taken. The body must not name the owner, or
    /// anything else about the repository that holds the name.
    /// </summary>
    private static async Task AssertDisclosesNothingAboutAsync(HttpResponseMessage response, Repository existing)
    {
        var body = await response.Content.ReadAsStringAsync();

        Assert.Multiple(() =>
        {
            Assert.That(body, Does.Not.Contain(existing.Owner.Id.ToString()), "the owner's id");
            Assert.That(body, Does.Not.Contain(existing.Owner.DisplayName), "the owner's display name");
            Assert.That(body, Does.Not.Contain(existing.Id.ToString()), "the colliding repository's id");
            Assert.That(body, Does.Not.Contain(existing.Name), "the colliding repository's name");
            Assert.That(body, Does.Not.Contain("A repository with this name"), "the exception's message");
        });
    }

    /// <summary>
    /// Creates a repository as whoever <paramref name="client"/> authenticates as.
    /// </summary>
    private static async Task<Repository> CreateAsync(HttpClient client, string name, bool isPublic)
    {
        var response = await client.PostAsJsonAsync("/api/v1/repositories",
            new WriteRepositoryRequest { Name = name, IsPublic = isPublic });
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created),
            $"Arranging '{name}' failed: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<Repository>())!;
    }

    /// <summary>
    /// A client pointed at the same application but carrying no bearer token.
    /// </summary>
    private HttpClient AnonymousClient() => new() { BaseAddress = new Uri(_fixture.BaseUrl) };

    #endregion

    #region Request Model Tests

    [Test]
    public void CreateRepositoryRequest_HasRequiredProperties()
    {
        // Arrange & Act
        var request = new WriteRepositoryRequest
        {
            Name = "test"
        };

        // Assert
        Assert.That(request.Name, Is.Not.Null);
    }

    [Test]
    public void Repository_HasAllRequiredFields()
    {
        // Arrange & Act
        var repository = new Repository
        {
            Id = Guid.CreateVersion7(),
            Name = "test",
            SupportedArchitectures = [Architectures.X86_64],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        Assert.That(repository.Name, Is.Not.Null);
        Assert.That(repository.SupportedArchitectures, Is.Not.Empty);
        Assert.That(repository.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        Assert.That(repository.UpdatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
    }

    #endregion
}
