namespace PacmanManager.AurClient.Test;

[TestFixture]
public class AurClientTests
{
    private HttpClient _httpClient;
    private AurClient _aurClient;

    [SetUp]
    public void Setup()
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri("https://aur.archlinux.org")
        };
        _aurClient = new AurClient(_httpClient);
    }

    [TearDown]
    public void TearDown()
    {
        _httpClient.Dispose();
    }

    [Test]
    public async Task Search_WithValidPackageName_ReturnsResults()
    {
        // Arrange - search for a well-known AUR package
        var searchTerm = "yay";
        var searchBy = AurSearchBy.Name;

        // Act
        var result = await _aurClient.Search(searchTerm, searchBy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Version, Is.EqualTo(5));
        Assert.That(result.Type, Is.EqualTo(AurReturnType.Search));
        Assert.That(result.ResultCount, Is.GreaterThan(0), "Should find at least one package matching 'yay'");
        Assert.That(result.Results, Is.Not.Null);
        
        var packages = result.Results.ToList();
        Assert.That(packages, Is.Not.Empty);
        
        // Verify the first result has the expected properties
        var firstPackage = packages.First();
        Assert.That(firstPackage.Name, Is.Not.Null.And.Not.Empty);
        Assert.That(firstPackage.Version, Is.Not.Null.And.Not.Empty);
        Assert.That(firstPackage.Description, Is.Not.Null.And.Not.Empty);
        
        await TestContext.Out.WriteLineAsync($"Found {result.ResultCount} packages matching '{searchTerm}'");
        await TestContext.Out.WriteLineAsync($"First result: {firstPackage.Name} ({firstPackage.Version})");
    }

    [Test]
    public async Task Search_WithNameDesc_ReturnsResults()
    {
        // Arrange - search by name and description
        var searchTerm = "helper";
        var searchBy = AurSearchBy.NameDesc;

        // Act
        var result = await _aurClient.Search(searchTerm, searchBy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Version, Is.EqualTo(5));
        Assert.That(result.Type, Is.EqualTo(AurReturnType.Search));
        Assert.That(result.ResultCount, Is.GreaterThan(0), "Should find packages with 'helper' in name or description");
        
        await TestContext.Out.WriteLineAsync($"Found {result.ResultCount} packages matching '{searchTerm}' by name or description");
    }

    [Test]
    public async Task Info_WithSinglePackage_ReturnsFullPackageInfo()
    {
        // Arrange - query info for a specific well-known package
        var packageName = "yay";

        // Act
        var result = await _aurClient.Info(new[] { packageName });

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Version, Is.EqualTo(5));
        Assert.That(result.Type, Is.EqualTo(AurReturnType.MultiInfo));
        Assert.That(result.ResultCount, Is.EqualTo(1), "Should find exactly one package");
        Assert.That(result.Results, Is.Not.Null);
        
        var package = result.Results.FirstOrDefault();
        Assert.That(package, Is.Not.Null, "Package should not be null");
        Assert.That(package.Name, Is.EqualTo(packageName));
        Assert.That(package.Version, Is.Not.Null.And.Not.Empty);
        Assert.That(package.Description, Is.Not.Null.And.Not.Empty);
        Assert.That(package.Url, Is.Not.Null.And.Not.Empty);
        Assert.That(package.Maintainer, Is.Not.Null.And.Not.Empty);
        Assert.That(package.PackageBase, Is.Not.Null.And.Not.Empty);
        
        // Check that full package info includes dependency information
        Assert.That(package.Depends, Is.Not.Null);
        Assert.That(package.MakeDepends, Is.Not.Null);
        Assert.That(package.License, Is.Not.Null);
        
        await TestContext.Out.WriteLineAsync($"Package: {package.Name} {package.Version}");
        await TestContext.Out.WriteLineAsync($"Description: {package.Description}");
        await TestContext.Out.WriteLineAsync($"Maintainer: {package.Maintainer}");
        await TestContext.Out.WriteLineAsync($"Votes: {package.NumVotes}, Popularity: {package.Popularity}");
    }

    [Test]
    public async Task Info_WithMultiplePackages_ReturnsAllPackages()
    {
        // Arrange - query info for multiple packages
        var packageNames = new[] { "yay", "paru" };

        // Act
        var result = await _aurClient.Info(packageNames);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Version, Is.EqualTo(5));
        Assert.That(result.Type, Is.EqualTo(AurReturnType.MultiInfo));
        Assert.That(result.ResultCount, Is.GreaterThanOrEqualTo(1), "Should find at least one package");
        
        var packages = result.Results.ToList();
        Assert.That(packages, Is.Not.Empty);
        
        // Verify each package has valid data
        foreach (var package in packages)
        {
            Assert.That(package.Name, Is.Not.Null.And.Not.Empty);
            Assert.That(package.Version, Is.Not.Null.And.Not.Empty);
            Assert.That(packageNames, Does.Contain(package.Name), $"Package name {package.Name} should be in requested list");
            
            await TestContext.Out.WriteLineAsync($"Package: {package.Name} {package.Version}");
        }
    }

    [Test]
    public async Task Info_WithNonExistentPackage_ReturnsEmptyResults()
    {
        // Arrange - query for a package that definitely doesn't exist
        var packageName = "this-package-definitely-does-not-exist-12345";

        // Act
        var result = await _aurClient.Info(new[] { packageName });

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Version, Is.EqualTo(5));
        Assert.That(result.Type, Is.EqualTo(AurReturnType.MultiInfo));
        Assert.That(result.ResultCount, Is.EqualTo(0), "Should find no packages");
        Assert.That(result.Results, Is.Empty);
        
        await TestContext.Out.WriteLineAsync("Correctly returned empty results for non-existent package");
    }

    [Test]
    public async Task Search_WithObscureTerm_MayReturnNoResults()
    {
        // Arrange - search for something unlikely to exist
        var searchTerm = "xyzabc123456789unlikely";
        var searchBy = AurSearchBy.Name;

        // Act
        var result = await _aurClient.Search(searchTerm, searchBy);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Version, Is.EqualTo(5));
        Assert.That(result.Type, Is.EqualTo(AurReturnType.Search));
        // ResultCount could be 0 or more, both are valid
        Assert.That(result.Results, Is.Not.Null);
        
        await TestContext.Out.WriteLineAsync($"Search for '{searchTerm}' returned {result.ResultCount} results");
    }
}
