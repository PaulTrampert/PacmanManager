namespace PacmanManager.RepoHost.Test.OpenApi;

/// <summary>
/// Checks that the committed OpenAPI document, the contract the web app is built against, is still
/// compatible with the document RepoHost generates now. Additions pass; a breaking difference fails
/// until the document is regenerated. Needs Docker, for the <c>oasdiff</c> image, and
/// <c>dotnet tool restore</c>, for the Swashbuckle CLI.
/// </summary>
[TestFixture]
public class OpenApiDocumentTests
{
    private string _directory = null!;
    private string _generatedPath = null!;
    private OpenApiGenerationResult _generation = null!;

    [OneTimeSetUp]
    public async Task GenerateTheDocument()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"pacmanmanager-openapi-{Guid.CreateVersion7()}");
        Directory.CreateDirectory(_directory);
        _generatedPath = Path.Combine(_directory, "v1.json");
        _generation = await OpenApiDocumentGenerator.GenerateAsync(_generatedPath);
        await TestContext.Out.WriteLineAsync(_generation.Output);
    }

    [OneTimeTearDown]
    public void RemoveTheDocument()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public void Generation_SucceedsWithNoDataDirOrDatabase_AndLogsNothingFatal()
    {
        using (Assert.EnterMultipleScope())
        {
            Assert.That(_generation.ExitCode, Is.Zero, _generation.Output);
            Assert.That(File.Exists(_generatedPath), Is.True, _generation.Output);
            Assert.That(_generation.Output, Does.Not.Contain(" FTL]"));
            Assert.That(_generation.Output, Does.Not.Contain("Unhandled exception"));
        }
    }

    [Test]
    public async Task CommittedDocument_HasNoBreakingDifferenceFromTheGeneratedOne()
    {
        Assume.That(_generation.ExitCode, Is.Zero, _generation.Output);

        var result = await OpenApiCompatibilityCheck.CompareAsync(
            OpenApiDocumentGenerator.CommittedDocumentPath, _generatedPath);

        Assert.That(result.IsCompatible, Is.True, result.FailureMessage);
    }
}
