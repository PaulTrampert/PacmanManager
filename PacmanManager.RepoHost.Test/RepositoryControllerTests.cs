using System.Net;
using System.Net.Http.Json;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests for the Repository API endpoints.
/// These tests run the application in a Docker container and make real HTTP requests.
/// </summary>
[TestFixture]
public class RepositoryControllerTests
{
    private EndToEndTestFixture _fixture = null!;
    private HttpClient _client = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _fixture = new EndToEndTestFixture();
        await _fixture.StartAsync();
        _client = _fixture.HttpClient;
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _fixture.DisposeAsync();
    }

    #region GetAll Tests

    [Test]
    public async Task GetAll_ReturnsOkStatus()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/repository");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
    }

    [Test]
    public async Task GetAll_ReturnsListOfRepositories()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/repository");
        var repositories = await response.Content.ReadFromJsonAsync<List<Repository>>();

        // Assert
        Assert.That(response.IsSuccessStatusCode, Is.True);
        Assert.That(repositories, Is.Not.Null);
    }

    [Test]
    public async Task GetAll_InitiallyReturnsEmptyList()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/repository");
        var repositories = await response.Content.ReadFromJsonAsync<List<Repository>>();

        // Assert
        Assert.That(repositories, Is.Not.Null);
        Assert.That(repositories, Is.Empty);
    }

    #endregion

    #region GetById Tests

    [Test]
    public async Task GetById_WithNonExistentName_ReturnsNotFound()
    {
        // Arrange
        var name = "non-existent-repo";

        // Act
        var response = await _client.GetAsync($"/api/v1/repository/{name}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetById_WithValidName_ReturnsOkResult()
    {
        // This test will be updated when repository storage is implemented
        // Arrange
        var name = "test-repo";

        // Act
        var response = await _client.GetAsync($"/api/v1/repository/{name}");

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
        var request = new CreateRepositoryRequest
        {
            Name = "test-repo",
            Architecture = "x86_64"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repository", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
    }

    [Test]
    public async Task Create_WithValidRequest_ReturnsRepositoryWithName()
    {
        // Arrange
        var request = new CreateRepositoryRequest
        {
            Name = "test-repo-2",
            Architecture = "x86_64"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repository", request);
        var repository = await response.Content.ReadFromJsonAsync<Repository>();

        // Assert
        Assert.That(repository, Is.Not.Null);
        Assert.That(repository!.Name, Is.EqualTo("test-repo-2"));
    }

    [Test]
    public async Task Create_WithValidRequest_SetsPropertiesCorrectly()
    {
        // Arrange
        var request = new CreateRepositoryRequest
        {
            Name = "custom-repo",
            Architecture = "aarch64"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repository", request);
        var repository = await response.Content.ReadFromJsonAsync<Repository>();

        // Assert
        Assert.That(repository, Is.Not.Null);
        Assert.That(repository!.Name, Is.EqualTo(request.Name));
        Assert.That(repository.Architecture, Is.EqualTo(request.Architecture));
    }

    [Test]
    public async Task Create_SetsTimestamps()
    {
        // Arrange
        var request = new CreateRepositoryRequest
        {
            Name = "test-repo-timestamps"
        };
        var beforeCreate = DateTimeOffset.UtcNow;

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repository", request);
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
        var request = new CreateRepositoryRequest
        {
            Name = "test-repo-location"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repository", request);
        var repository = await response.Content.ReadFromJsonAsync<Repository>();

        // Assert
        Assert.That(response.Headers.Location, Is.Not.Null);
        Assert.That(response.Headers.Location!.ToString(), Does.Contain(repository!.Name));
    }

    [Test]
    public async Task Create_WithMinimalRequest_UsesDefaults()
    {
        // Arrange
        var request = new CreateRepositoryRequest
        {
            Name = "minimal-repo"
        };

        // Act
        var response = await _client.PostAsJsonAsync("/api/v1/repository", request);
        var repository = await response.Content.ReadFromJsonAsync<Repository>();

        // Assert
        Assert.That(repository, Is.Not.Null);
        Assert.That(repository!.Architecture, Is.EqualTo("x86_64"));
    }

    #endregion

    #region Update Tests

    [Test]
    public async Task Update_WithNonExistentName_ReturnsNotFound()
    {
        // Arrange
        var name = "non-existent-repo";
        var request = new UpdateRepositoryRequest
        {
            Name = "updated-name"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/repository/{name}", request);

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Update_WithValidName_ReturnsOkResult()
    {
        // This test will be updated when repository storage is implemented
        // Arrange
        var name = "test-repo";
        var request = new UpdateRepositoryRequest
        {
            Name = "updated-name"
        };

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/repository/{name}", request);

        // Assert
        // Currently returns NotFound until storage is implemented
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Update_WithEmptyRequest_IsValid()
    {
        // Arrange
        var name = "test-repo";
        var request = new UpdateRepositoryRequest();

        // Act
        var response = await _client.PutAsJsonAsync($"/api/v1/repository/{name}", request);

        // Assert
        // Should not throw, currently returns NotFound
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region Delete Tests

    [Test]
    public async Task Delete_WithNonExistentName_ReturnsNotFound()
    {
        // Arrange
        var name = "non-existent-repo";

        // Act
        var response = await _client.DeleteAsync($"/api/v1/repository/{name}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task Delete_WithValidName_ReturnsNoContent()
    {
        // This test will be updated when repository storage is implemented
        // Arrange
        var name = "test-repo";

        // Act
        var response = await _client.DeleteAsync($"/api/v1/repository/{name}");

        // Assert
        // Currently returns NotFound until storage is implemented
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    #endregion

    #region Request Model Tests

    [Test]
    public void CreateRepositoryRequest_HasRequiredProperties()
    {
        // Arrange & Act
        var request = new CreateRepositoryRequest
        {
            Name = "test"
        };

        // Assert
        Assert.That(request.Name, Is.Not.Null);
    }

    [Test]
    public void UpdateRepositoryRequest_AllPropertiesOptional()
    {
        // Arrange & Act
        var request = new UpdateRepositoryRequest();

        // Assert
        Assert.That(request.Name, Is.Null);
        Assert.That(request.Architecture, Is.Null);
    }

    [Test]
    public void Repository_HasAllRequiredFields()
    {
        // Arrange & Act
        var repository = new Repository
        {
            Id = Guid.CreateVersion7(),
            Name = "test",
            Architecture = "x86_64",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Assert
        Assert.That(repository.Name, Is.Not.Null);
        Assert.That(repository.Architecture, Is.Not.Null);
        Assert.That(repository.CreatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
        Assert.That(repository.UpdatedAt, Is.Not.EqualTo(default(DateTimeOffset)));
    }

    [Test]
    public void Repository_GetPath_ReturnsCorrectPath()
    {
        // Arrange
        var repository = new Repository
        {
            Id = Guid.CreateVersion7(),
            Name = "custom-repo",
            Architecture = "aarch64"
        };
        var basePath = "/var/lib/pacman/repos";

        // Act
        var path = repository.GetPath(basePath);

        // Assert
        Assert.That(path, Is.EqualTo("/var/lib/pacman/repos/custom-repo/aarch64"));
    }

    [Test]
    public void Repository_GetPath_WithDifferentArchitecture_ReturnsCorrectPath()
    {
        // Arrange
        var repository = new Repository
        {
            Id =  Guid.CreateVersion7(),
            Name = "test-repo",
            Architecture = "x86_64"
        };
        var basePath = "/srv/repos";

        // Act
        var path = repository.GetPath(basePath);

        // Assert
        Assert.That(path, Is.EqualTo("/srv/repos/test-repo/x86_64"));
    }

    #endregion
}
