using System.Text.RegularExpressions;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Services;

/// <summary>
/// Guards who may reach <c>PacmanAccessTokens</c>. <c>UserService</c> reaches it to verify a token,
/// before any actor exists; <c>AccessTokenService</c> reaches it to manage the actor's own tokens.
/// Nothing else may, and inside <c>AccessTokenService</c> every query starts from the chokepoint that
/// restricts the set to the actor's own tokens.
/// </summary>
/// <remarks>
/// If this test fails, route the new code through one of those two services rather than relaxing the
/// assertion.
/// </remarks>
[TestFixture]
public class AccessTokenServiceEnforcementTests
{
    private const string DbSetName = "PacmanAccessTokens";
    private const string ChokepointMethod = "OwnAsync";

    private static readonly string[] AllowedFiles =
    [
        Path.Combine("Services", "AccessTokenService.cs"),
        Path.Combine("Services", "UserService.cs"),
    ];

    private string _projectDirectory = null!;

    [OneTimeSetUp]
    public void OneTimeSetUp()
    {
        _projectDirectory = Path.Combine(DirUtils.FindSolutionDirectory(), "PacmanManager.RepoHost");
    }

    [Test]
    public void OnlyTheTwoTokenServicesNameTheAccessTokensDbSet()
    {
        var naming = Directory
            .EnumerateFiles(_projectDirectory, "*.cs", SearchOption.AllDirectories)
            .Select(file => Path.GetRelativePath(_projectDirectory, file))
            .Where(file => !IsBuildOutput(file))
            .Where(file => File.ReadAllText(Path.Combine(_projectDirectory, file)).Contains(DbSetName))
            .Order()
            .ToList();

        Assert.That(naming, Is.EquivalentTo(AllowedFiles));
    }

    [Test]
    public void AccessTokenServiceOnlyReachesTheDbSetThroughTheOwnershipChokepoint()
    {
        var source = File.ReadAllLines(Path.Combine(_projectDirectory, "Services", "AccessTokenService.cs"));
        var accesses = source
            .Select((line, index) => (Line: line, Number: index + 1))
            .Where(l => l.Line.Contains($"dbContext.{DbSetName}"))
            .ToList();

        Assert.That(accesses, Has.Count.EqualTo(1),
            $"Expected exactly one reference to dbContext.{DbSetName}, inside {ChokepointMethod}. Found: "
            + string.Join(", ", accesses.Select(a => $"line {a.Number}")));
        Assert.That(EnclosingMethodOf(source, accesses[0].Number), Is.EqualTo(ChokepointMethod));
    }

    private static bool IsBuildOutput(string relativePath)
    {
        var first = relativePath.Split(Path.DirectorySeparatorChar)[0];
        return first is "bin" or "obj";
    }

    /// <summary>
    /// Finds the name of the method whose declaration most recently precedes the given line.
    /// </summary>
    private static string? EnclosingMethodOf(string[] source, int lineNumber)
    {
        var declaration = new Regex(@"^\s{4}(?:private|protected|internal|public)\b.*?\s(?<name>\w+)\(");

        return source
            .Take(lineNumber)
            .Select(line => declaration.Match(line))
            .LastOrDefault(m => m.Success)
            ?.Groups["name"].Value;
    }
}
