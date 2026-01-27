using System.Text.Json;

namespace PacmanManager.AurClient.Test;

[TestFixture]
public class AurResponseConverterTests
{
    private JsonSerializerOptions _jsonOptions;

    [SetUp]
    public void Setup()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    [Test]
    public void Read_InfoResponse_DeserializesCorrectly()
    {
        // Arrange
        var json = """
        {
            "version": 5,
            "type": "info",
            "resultcount": 1,
            "results": [
                {
                    "id": 123456,
                    "name": "test-package",
                    "packagebaseid": 123456,
                    "packagebase": "test-package",
                    "version": "1.0.0-1",
                    "description": "A test package",
                    "url": "https://example.com",
                    "numvotes": 10,
                    "popularity": 0.5,
                    "outofdate": null,
                    "maintainer": "testuser",
                    "firstsubmitted": 1609459200,
                    "lastmodified": 1609545600,
                    "urlpath": "/cgit/aur.git/snapshot/test-package.tar.gz",
                    "depends": ["dep1", "dep2"],
                    "makedepends": ["make-dep1"],
                    "optdepends": ["opt-dep1: for optional feature"],
                    "checkdepends": [],
                    "conflicts": [],
                    "provides": [],
                    "replaces": [],
                    "groups": [],
                    "license": ["MIT"],
                    "keywords": ["test", "package"]
                }
            ]
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<AurResponse>(json, _jsonOptions);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<AurResponse<AurFullPackageInfo>>());
        Assert.That(result.Version, Is.EqualTo(5));
        Assert.That(result.Type, Is.EqualTo(AurReturnType.Info));
        
        var typedResult = (AurResponse<AurFullPackageInfo>)result;
        Assert.That(typedResult.ResultCount, Is.EqualTo(1));
        Assert.That(typedResult.Results, Is.Not.Null);
        
        var package = typedResult.Results.FirstOrDefault();
        Assert.That(package, Is.Not.Null);
        Assert.That(package.Name, Is.EqualTo("test-package"));
        Assert.That(package.Version, Is.EqualTo("1.0.0-1"));
        Assert.That(package.Description, Is.EqualTo("A test package"));
        Assert.That(package.Depends, Is.EqualTo(new[] { "dep1", "dep2" }));
    }

    [Test]
    public void Read_MultiInfoResponse_DeserializesCorrectly()
    {
        // Arrange
        var json = """
        {
            "version": 5,
            "type": "multiinfo",
            "resultcount": 2,
            "results": [
                {
                    "id": 123456,
                    "name": "package1",
                    "packagebaseid": 123456,
                    "packagebase": "package1",
                    "version": "1.0.0-1",
                    "description": "First package",
                    "url": "https://example.com",
                    "numvotes": 10,
                    "popularity": 0.5,
                    "outofdate": null,
                    "maintainer": "testuser",
                    "firstsubmitted": 1609459200,
                    "lastmodified": 1609545600,
                    "urlpath": "/cgit/aur.git/snapshot/package1.tar.gz",
                    "depends": [],
                    "makedepends": [],
                    "optdepends": [],
                    "checkdepends": [],
                    "conflicts": [],
                    "provides": [],
                    "replaces": [],
                    "groups": [],
                    "license": ["MIT"],
                    "keywords": []
                },
                {
                    "id": 123457,
                    "name": "package2",
                    "packagebaseid": 123457,
                    "packagebase": "package2",
                    "version": "2.0.0-1",
                    "description": "Second package",
                    "url": "https://example.org",
                    "numvotes": 20,
                    "popularity": 1.5,
                    "outofdate": null,
                    "maintainer": "testuser2",
                    "firstsubmitted": 1609459200,
                    "lastmodified": 1609545600,
                    "urlpath": "/cgit/aur.git/snapshot/package2.tar.gz",
                    "depends": [],
                    "makedepends": [],
                    "optdepends": [],
                    "checkdepends": [],
                    "conflicts": [],
                    "provides": [],
                    "replaces": [],
                    "groups": [],
                    "license": ["GPL"],
                    "keywords": []
                }
            ]
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<AurResponse>(json, _jsonOptions);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<AurResponse<AurFullPackageInfo>>());
        Assert.That(result.Version, Is.EqualTo(5));
        Assert.That(result.Type, Is.EqualTo(AurReturnType.MultiInfo));
        
        var typedResult = (AurResponse<AurFullPackageInfo>)result;
        Assert.That(typedResult.ResultCount, Is.EqualTo(2));
        Assert.That(typedResult.Results.Count(), Is.EqualTo(2));
        
        var packages = typedResult.Results.ToList();
        Assert.That(packages[0].Name, Is.EqualTo("package1"));
        Assert.That(packages[1].Name, Is.EqualTo("package2"));
    }

    [Test]
    public void Read_SearchResponse_DeserializesCorrectly()
    {
        // Arrange
        var json = """
        {
            "version": 5,
            "type": "search",
            "resultcount": 1,
            "results": [
                {
                    "id": 123456,
                    "name": "search-result",
                    "packagebaseid": 123456,
                    "packagebase": "search-result",
                    "version": "1.0.0-1",
                    "description": "A search result package",
                    "url": "https://example.com",
                    "numvotes": 5,
                    "popularity": 0.25,
                    "outofdate": null,
                    "maintainer": "searchuser",
                    "firstsubmitted": 1609459200,
                    "lastmodified": 1609545600,
                    "urlpath": "/cgit/aur.git/snapshot/search-result.tar.gz"
                }
            ]
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<AurResponse>(json, _jsonOptions);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<AurResponse<AurBasicPackageInfo>>());
        Assert.That(result.Version, Is.EqualTo(5));
        Assert.That(result.Type, Is.EqualTo(AurReturnType.Search));
        
        var typedResult = (AurResponse<AurBasicPackageInfo>)result;
        Assert.That(typedResult.ResultCount, Is.EqualTo(1));
        Assert.That(typedResult.Results, Is.Not.Null);
        
        var package = typedResult.Results.FirstOrDefault();
        Assert.That(package, Is.Not.Null);
        Assert.That(package.Name, Is.EqualTo("search-result"));
        Assert.That(package.Version, Is.EqualTo("1.0.0-1"));
        Assert.That(package.NumVotes, Is.EqualTo(5));
    }

    [Test]
    public void Read_ErrorResponse_DeserializesCorrectly()
    {
        // Arrange
        var json = """
        {
            "version": 5,
            "type": "error",
            "error": "Invalid request: Too many package results."
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<AurResponse>(json, _jsonOptions);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<AurErrorResponse>());
        Assert.That(result.Version, Is.EqualTo(5));
        Assert.That(result.Type, Is.EqualTo(AurReturnType.Error));
        
        var errorResult = (AurErrorResponse)result;
        Assert.That(errorResult.Error, Is.EqualTo("Invalid request: Too many package results."));
    }

    [Test]
    public void Read_MissingTypeProperty_ThrowsJsonException()
    {
        // Arrange
        var json = """
        {
            "version": 5,
            "resultcount": 1,
            "results": []
        }
        """;

        // Act & Assert
        var ex = Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<AurResponse>(json, _jsonOptions));
        Assert.That(ex.Message, Does.Contain("Invalid or missing 'type' property"));
    }

    [Test]
    public void Read_InvalidTypeValue_ThrowsJsonException()
    {
        // Arrange
        var json = """
        {
            "version": 5,
            "type": "InvalidType",
            "resultcount": 1,
            "results": []
        }
        """;

        // Act & Assert
        var ex = Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<AurResponse>(json, _jsonOptions));
        Assert.That(ex.Message, Does.Contain("Invalid or missing 'type' property"));
    }

    [Test]
    public void Read_EmptyResults_DeserializesCorrectly()
    {
        // Arrange
        var json = """
        {
            "version": 5,
            "type": "search",
            "resultcount": 0,
            "results": []
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<AurResponse>(json, _jsonOptions);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.InstanceOf<AurResponse<AurBasicPackageInfo>>());
        
        var typedResult = (AurResponse<AurBasicPackageInfo>)result;
        Assert.That(typedResult.ResultCount, Is.EqualTo(0));
        Assert.That(typedResult.Results, Is.Empty);
    }

    [Test]
    public void Read_PackageWithOutOfDate_DeserializesCorrectly()
    {
        // Arrange
        var json = """
        {
            "version": 5,
            "type": "info",
            "resultcount": 1,
            "results": [
                {
                    "id": 123456,
                    "name": "outdated-package",
                    "packagebaseid": 123456,
                    "packagebase": "outdated-package",
                    "version": "0.9.0-1",
                    "description": "An outdated package",
                    "url": "https://example.com",
                    "numvotes": 1,
                    "popularity": 0.01,
                    "outofdate": 1640995200,
                    "maintainer": "testuser",
                    "firstsubmitted": 1609459200,
                    "lastmodified": 1609545600,
                    "urlpath": "/cgit/aur.git/snapshot/outdated-package.tar.gz",
                    "depends": [],
                    "makedepends": [],
                    "optdepends": [],
                    "checkdepends": [],
                    "conflicts": [],
                    "provides": [],
                    "replaces": [],
                    "groups": [],
                    "license": ["GPL"],
                    "keywords": []
                }
            ]
        }
        """;

        // Act
        var result = JsonSerializer.Deserialize<AurResponse>(json, _jsonOptions);

        // Assert
        Assert.That(result, Is.Not.Null);
        var typedResult = (AurResponse<AurFullPackageInfo>)result;
        var package = typedResult.Results.FirstOrDefault();
        Assert.That(package, Is.Not.Null);
        Assert.That(package.OutOfDate, Is.Not.Null);
    }

    [Test]
    public void Write_InfoResponse_SerializesCorrectly()
    {
        // Arrange
        var response = new AurResponse<AurFullPackageInfo>
        {
            Version = 5,
            Type = AurReturnType.Info,
            ResultCount = 1,
            Results = new[]
            {
                new AurFullPackageInfo
                {
                    Id = 123456,
                    Name = "test-package",
                    PackageBaseId = 123456,
                    PackageBase = "test-package",
                    Version = "1.0.0-1",
                    Description = "A test package",
                    Url = "https://example.com",
                    NumVotes = 10,
                    Popularity = 0.5m,
                    OutOfDate = null,
                    Maintainer = "testuser",
                    FirstSubmitted = DateTimeOffset.FromUnixTimeSeconds(1609459200),
                    LastModified = DateTimeOffset.FromUnixTimeSeconds(1609545600),
                    UrlPath = "/cgit/aur.git/snapshot/test-package.tar.gz",
                    Depends = new[] { "dep1", "dep2" },
                    MakeDepends = new[] { "make-dep1" },
                    OptDepends = new[] { "opt-dep1: for optional feature" },
                    CheckDepends = Array.Empty<string>(),
                    Conflicts = Array.Empty<string>(),
                    Provides = Array.Empty<string>(),
                    Replaces = Array.Empty<string>(),
                    Groups = Array.Empty<string>(),
                    License = new[] { "MIT" },
                    Keywords = new[] { "test", "package" }
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize<AurResponse>(response, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AurResponse>(json, _jsonOptions);

        // Assert
        Assert.That(json, Is.Not.Null.And.Not.Empty);
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized, Is.InstanceOf<AurResponse<AurFullPackageInfo>>());
        
        var typedResult = (AurResponse<AurFullPackageInfo>)deserialized;
        Assert.That(typedResult.Version, Is.EqualTo(5));
        Assert.That(typedResult.Type, Is.EqualTo(AurReturnType.Info));
        Assert.That(typedResult.ResultCount, Is.EqualTo(1));
        
        var package = typedResult.Results.FirstOrDefault();
        Assert.That(package, Is.Not.Null);
        Assert.That(package.Name, Is.EqualTo("test-package"));
    }

    [Test]
    public void Write_ErrorResponse_SerializesCorrectly()
    {
        // Arrange
        var response = new AurErrorResponse
        {
            Version = 5,
            Type = AurReturnType.Error,
            Error = "Test error message"
        };

        // Act
        var json = JsonSerializer.Serialize<AurResponse>(response, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AurResponse>(json, _jsonOptions);

        // Assert
        Assert.That(json, Is.Not.Null.And.Not.Empty);
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized, Is.InstanceOf<AurErrorResponse>());
        
        var errorResult = (AurErrorResponse)deserialized;
        Assert.That(errorResult.Version, Is.EqualTo(5));
        Assert.That(errorResult.Type, Is.EqualTo(AurReturnType.Error));
        Assert.That(errorResult.Error, Is.EqualTo("Test error message"));
    }

    [Test]
    public void Write_SearchResponse_SerializesCorrectly()
    {
        // Arrange
        var response = new AurResponse<AurBasicPackageInfo>
        {
            Version = 5,
            Type = AurReturnType.Search,
            ResultCount = 1,
            Results = new[]
            {
                new AurBasicPackageInfo
                {
                    Id = 123456,
                    Name = "search-package",
                    PackageBaseId = 123456,
                    PackageBase = "search-package",
                    Version = "2.0.0-1",
                    Description = "A search result",
                    Url = "https://example.com",
                    NumVotes = 15,
                    Popularity = 0.75m,
                    OutOfDate = null,
                    Maintainer = "maintainer",
                    FirstSubmitted = DateTimeOffset.FromUnixTimeSeconds(1609459200),
                    LastModified = DateTimeOffset.FromUnixTimeSeconds(1609545600),
                    UrlPath = "/cgit/aur.git/snapshot/search-package.tar.gz"
                }
            }
        };

        // Act
        var json = JsonSerializer.Serialize<AurResponse>(response, _jsonOptions);
        var deserialized = JsonSerializer.Deserialize<AurResponse>(json, _jsonOptions);

        // Assert
        Assert.That(json, Is.Not.Null.And.Not.Empty);
        Assert.That(deserialized, Is.Not.Null);
        Assert.That(deserialized, Is.InstanceOf<AurResponse<AurBasicPackageInfo>>());
        
        var typedResult = (AurResponse<AurBasicPackageInfo>)deserialized;
        Assert.That(typedResult.Version, Is.EqualTo(5));
        Assert.That(typedResult.Type, Is.EqualTo(AurReturnType.Search));
        Assert.That(typedResult.ResultCount, Is.EqualTo(1));
        
        var package = typedResult.Results.FirstOrDefault();
        Assert.That(package, Is.Not.Null);
        Assert.That(package.Name, Is.EqualTo("search-package"));
    }
}
