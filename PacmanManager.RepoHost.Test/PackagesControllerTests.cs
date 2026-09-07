using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using PacmanManager.Entities;
using PacmanManager.RepoHost.Models;

namespace PacmanManager.RepoHost.Test;

/// <summary>
/// End-to-end tests for the package read routes. These run the application in a Docker container
/// and make real HTTP requests.
/// </summary>
/// <remarks>
/// Publishing does not exist yet, so the packages these routes serve are seeded straight into the
/// database the containerized API is talking to. Only the arrangement takes that shortcut: every
/// assertion is made against what the API itself returns.
/// </remarks>
[TestFixture]
public class PackagesControllerTests
{
    private const string PublicPackageName = "e2e-public-tool";
    private const string OtherPublicPackageName = "e2e-public-widget";
    private const string PrivatePackageName = "e2e-private-tool";

    private EndToEndTestFixture _fixture = null!;
    private HttpClient _client = null!;
    private Repository _publicRepository = null!;
    private Repository _privateRepository = null!;
    private Guid _publicPackageId;
    private Guid _privatePackageId;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _fixture = new EndToEndTestFixture();
        await _fixture.StartAsync();
        _client = _fixture.HttpClient;
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            await _fixture.AuthContainer!.GetBearerTokenAsync(_fixture.AuthContainer.DefaultCredentials));

        _publicRepository = await CreateRepositoryAsync("e2e-packages-public", isPublic: true);
        _privateRepository = await CreateRepositoryAsync("e2e-packages-private", isPublic: false);

        var publisherId = _publicRepository.Owner.Id;

        // A null base and description on the package the tests read back most often: they are the
        // columns the search filter has to guard, and the ones the null-omitting serializer has to
        // leave out of the response.
        var publicPackage = NewPackage(_publicRepository.Id, publisherId, PublicPackageName, installedSize: 4096);
        var otherPublicPackage = NewPackage(
            _publicRepository.Id, publisherId, OtherPublicPackageName, installedSize: 8192) with
        {
            Base = OtherPublicPackageName,
            Description = "A widget, for widgeting.",
        };
        var privatePackage = NewPackage(_privateRepository.Id, publisherId, PrivatePackageName, installedSize: 2048);

        await using var db = _fixture.CreateDbContext();
        db.PacmanPackages.AddRange(publicPackage, otherPublicPackage, privatePackage);
        await db.SaveChangesAsync();

        _publicPackageId = publicPackage.Id;
        _privatePackageId = privatePackage.Id;
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _fixture.DisposeAsync();
    }

    #region GET /api/v1/packages

    [Test]
    public async Task Get_ReturnsTheVisiblePackages()
    {
        // Act
        var response = await _client.GetAsync("/api/v1/packages?pageSize=500");
        var packages = await response.Content.ReadFromJsonAsync<PaginatedResponse<Package>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(packages!.Results.Select(p => p.Name), Is.SupersetOf(new[]
        {
            PublicPackageName, OtherPublicPackageName, PrivatePackageName
        }), "The owner sees their private repository's packages alongside the public ones.");
    }

    [Test]
    public async Task Get_WithoutAuthentication_ReturnsPackagesOfPublicRepositoriesOnly()
    {
        // Act
        var response = await AnonymousClient().GetAsync("/api/v1/packages?pageSize=500");
        var packages = await response.Content.ReadFromJsonAsync<PaginatedResponse<Package>>();

        // Assert
        var names = packages!.Results.Select(p => p.Name).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(names, Does.Contain(PublicPackageName));
            Assert.That(names, Does.Contain(OtherPublicPackageName));
            Assert.That(names, Does.Not.Contain(PrivatePackageName));
        });
    }

    [Test]
    public async Task Get_SortsByANonStringKey_InTheDatabase()
    {
        // ApplySort types every key selector as Expression<Func<PacmanPackage, object>>, so a value
        // type key carries a Convert node EF Core has to strip to order in SQL. An untranslatable
        // ordering throws rather than falling back to the client, so this is where the listing would
        // start returning 500; the name cases go through a reference type and would not catch it.
        const string query = "/api/v1/packages?nameContains=e2e-public-&pageSize=50&sortBy=InstalledSize";

        // Act
        var biggestFirst = await _client.GetFromJsonAsync<PaginatedResponse<Package>>(query);
        var smallestFirst = await _client.GetFromJsonAsync<PaginatedResponse<Package>>(
            $"{query}&direction=Ascending");

        // Assert
        var bySize = new[] { PublicPackageName, OtherPublicPackageName };
        Assert.Multiple(() =>
        {
            Assert.That(smallestFirst!.Results.Select(p => p.Name), Is.EqualTo(bySize));
            Assert.That(biggestFirst!.Results.Select(p => p.Name), Is.EqualTo(bySize.Reverse()),
                "InstalledSize defaults to biggest first.");
        });
    }

    [Test]
    public async Task Get_SerializesCamelCaseAndOmitsNulls()
    {
        // Act
        var json = await _client.GetStringAsync($"/api/v1/packages/{_publicPackageId}");

        // Assert
        using var document = JsonDocument.Parse(json);
        var names = document.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(names, Does.Contain("fileName"), "Properties are camelCase.");
            Assert.That(names, Does.Not.Contain("FileName"));
            Assert.That(names, Does.Not.Contain("description"),
                "A null description is omitted rather than serialized as null.");
            Assert.That(names, Does.Not.Contain("base"));
        });
    }

    #endregion

    #region GET /api/v1/packages/{packageId}

    [Test]
    public async Task GetById_ReturnsThePackage()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/packages/{_publicPackageId}");
        var package = await response.Content.ReadFromJsonAsync<Package>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.Multiple(() =>
        {
            Assert.That(package!.Id, Is.EqualTo(_publicPackageId));
            Assert.That(package.Name, Is.EqualTo(PublicPackageName));
            Assert.That(package.RepositoryId, Is.EqualTo(_publicRepository.Id));
            Assert.That(package.Publisher.Id, Is.EqualTo(_publicRepository.Owner.Id));
        });
    }

    [Test]
    public async Task GetById_WithNonExistentId_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/packages/{Guid.NewGuid()}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetById_InAPrivateRepository_IsFoundByItsOwnerAndMissingToEveryoneElse()
    {
        // Act
        var asOwner = await _client.GetAsync($"/api/v1/packages/{_privatePackageId}");
        var asStranger = await AnonymousClient().GetAsync($"/api/v1/packages/{_privatePackageId}");

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(asOwner.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(asStranger.StatusCode, Is.EqualTo(HttpStatusCode.NotFound),
                "A package nobody else may see is reported as absent, not as forbidden.");
        });
    }

    #endregion

    #region GET /api/v1/repositories/{repositoryId}/packages

    [Test]
    public async Task GetForRepository_ReturnsThatRepositorysPackagesOnly()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/repositories/{_publicRepository.Id}/packages?pageSize=500");
        var packages = await response.Content.ReadFromJsonAsync<PaginatedResponse<Package>>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(packages!.Results.Select(p => p.Name),
            Is.EquivalentTo(new[] { PublicPackageName, OtherPublicPackageName }));
    }

    [Test]
    public async Task GetForRepository_AppliesTheOtherFilters()
    {
        // The path segment supplies the repository; everything else still narrows the listing.
        // Act
        var packages = await _client.GetFromJsonAsync<PaginatedResponse<Package>>(
            $"/api/v1/repositories/{_publicRepository.Id}/packages?search=WIDGETING&pageSize=500");

        // Assert
        Assert.That(packages!.Results.Select(p => p.Name), Is.EqualTo(new[] { OtherPublicPackageName }));
    }

    [Test]
    public async Task GetForRepository_WithNonExistentRepository_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync($"/api/v1/repositories/{Guid.NewGuid()}/packages");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetForRepository_OnAPrivateRepository_IsListedByItsOwnerAndMissingToEveryoneElse()
    {
        // An empty page would tell a stranger the repository exists, so an invisible one is missing.
        // Act
        var asOwner = await _client.GetAsync($"/api/v1/repositories/{_privateRepository.Id}/packages");
        var asStranger = await AnonymousClient().GetAsync($"/api/v1/repositories/{_privateRepository.Id}/packages");
        var listed = await asOwner.Content.ReadFromJsonAsync<PaginatedResponse<Package>>();

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(asOwner.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(listed!.Results.Select(p => p.Name), Is.EqualTo(new[] { PrivatePackageName }));
            Assert.That(asStranger.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    [Test]
    public async Task GetForRepository_WithARepositoryFilter_ReturnsBadRequest()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/v1/repositories/{_publicRepository.Id}/packages?repositoryIds={_privateRepository.Id}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest),
            "A criterion the path already decides is rejected rather than silently ignored.");
    }

    [Test]
    public async Task GetForRepository_WithAnEmptyRepositoryFilter_ReturnsBadRequest()
    {
        // The binder maps an empty value to null, so only the raw query string can tell this apart
        // from omitting the parameter — which is why the check is made against the query string.
        // Act
        var response = await _client.GetAsync(
            $"/api/v1/repositories/{_publicRepository.Id}/packages?repositoryIds=");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    #endregion

    #region GET /api/v1/repositories/{repositoryId}/packages/{name}

    [Test]
    public async Task GetByName_ReturnsThePackage()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/v1/repositories/{_publicRepository.Id}/packages/{PublicPackageName}");
        var package = await response.Content.ReadFromJsonAsync<Package>();

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(package!.Id, Is.EqualTo(_publicPackageId),
            "The alias resolves to the same package the id form returns.");
    }

    [Test]
    public async Task GetByName_WithUnknownName_ReturnsNotFound()
    {
        // Act
        var response = await _client.GetAsync(
            $"/api/v1/repositories/{_publicRepository.Id}/packages/no-such-package");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetByName_InAnotherRepository_ReturnsNotFound()
    {
        // The name is real and so is the repository; the pair is not.
        // Act
        var response = await _client.GetAsync(
            $"/api/v1/repositories/{_publicRepository.Id}/packages/{PrivatePackageName}");

        // Assert
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetByName_InAPrivateRepository_IsFoundByItsOwnerAndMissingToEveryoneElse()
    {
        // Arrange
        var url = $"/api/v1/repositories/{_privateRepository.Id}/packages/{PrivatePackageName}";

        // Act
        var asOwner = await _client.GetAsync(url);
        var asStranger = await AnonymousClient().GetAsync(url);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(asOwner.StatusCode, Is.EqualTo(HttpStatusCode.OK));
            Assert.That(asStranger.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
        });
    }

    #endregion

    /// <summary>
    /// A client pointed at the same application but carrying no bearer token.
    /// </summary>
    private HttpClient AnonymousClient() => new() { BaseAddress = new Uri(_fixture.BaseUrl) };

    /// <summary>
    /// Creates a repository over the API, so that its owner is the authenticated test user.
    /// </summary>
    private async Task<Repository> CreateRepositoryAsync(string name, bool isPublic)
    {
        var response = await _client.PostAsJsonAsync("/api/v1/repositories", new WriteRepositoryRequest
        {
            Name = name,
            IsPublic = isPublic
        });
        Assert.That(response.IsSuccessStatusCode, Is.True, $"Failed to create the {name} repository.");
        return (await response.Content.ReadFromJsonAsync<Repository>())!;
    }

    /// <summary>
    /// A package row with every required column filled in and nothing else set, so that a test only
    /// has to say what it actually cares about.
    /// </summary>
    private static PacmanPackage NewPackage(Guid repositoryId, Guid publisherId, string name, long installedSize) =>
        new()
        {
            RepositoryId = repositoryId,
            PublisherId = publisherId,
            Name = name,
            Version = "1.0.0-1",
            Architecture = "x86_64",
            FileName = $"{name}-1.0.0-1-x86_64.pkg.tar.zst",
            CompressedSize = installedSize / 2,
            InstalledSize = installedSize,
            BuildDate = DateTimeOffset.UtcNow,
            Sha256Sum = new string('a', PackageValidationConstants.Sha256SumLength),
            Md5Sum = new string('b', PackageValidationConstants.Md5SumLength),
        };
}
