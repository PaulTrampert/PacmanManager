using System.Text.RegularExpressions;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Guards the structural property that makes <c>PackageService</c> safe: authorization is not
/// something each method remembers to do, it is the only way each method can reach the data.
/// </summary>
/// <remarks>
/// If this test fails, a method has started querying packages without going through
/// <c>VisibleAsync</c>, which means it is no longer applying the visibility rules. Route the new
/// query through <c>VisibleAsync</c> rather than relaxing the assertion. This mirrors
/// <see cref="RepositoryServiceEnforcementTests"/>.
/// </remarks>
[TestFixture]
public class PackageServiceEnforcementTests
{
    private const string ChokepointMethod = "VisibleAsync";
    private const string DbSetAccess = "dbContext.PacmanPackages";

    private string[] _source = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        var path = Path.Combine(
            DirUtils.FindSolutionDirectory(),
            "PacmanManager.RepoHost",
            "Services",
            "PackageService.cs");
        _source = File.ReadAllLines(path);
    }

    [Test]
    public void ThePackagesDbSetIsOnlyReachedThroughTheVisibilityChokepoint()
    {
        var accesses = _source
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(l => l.Line.Contains(DbSetAccess))
            .ToList();

        Assert.That(accesses, Has.Count.EqualTo(1),
            $"Expected exactly one reference to {DbSetAccess}, inside {ChokepointMethod}. Found: "
            + string.Join(", ", accesses.Select(a => $"line {a.Number}")));

        Assert.That(EnclosingMethodOf(accesses[0].Number), Is.EqualTo(ChokepointMethod));
    }

    /// <summary>
    /// Finds the name of the method whose declaration most recently precedes the given line.
    /// </summary>
    private string? EnclosingMethodOf(int lineNumber)
    {
        var declaration = new Regex(@"^\s{4}(?:private|protected|internal|public)\b.*?\s(?<name>\w+)\(");

        return _source
            .Take(lineNumber)
            .Select(line => declaration.Match(line))
            .LastOrDefault(m => m.Success)
            ?.Groups["name"].Value;
    }
}
