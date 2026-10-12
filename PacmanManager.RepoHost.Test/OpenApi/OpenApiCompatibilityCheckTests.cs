using System.Text.Json.Nodes;

namespace PacmanManager.RepoHost.Test.OpenApi;

/// <summary>
/// Exercises <see cref="OpenApiCompatibilityCheck"/> against fixture documents, so that what the
/// check accepts and rejects is pinned down independently of RepoHost's current routes. Needs
/// Docker, for the <c>oasdiff</c> image.
/// </summary>
[TestFixture]
public class OpenApiCompatibilityCheckTests
{
    private static readonly string BaseFixturePath =
        Path.Combine(AppContext.BaseDirectory, "OpenApi", "Fixtures", "base.json");

    private string _directory = null!;

    [SetUp]
    public void SetUp()
    {
        _directory = Path.Combine(Path.GetTempPath(), $"pacmanmanager-openapi-{Guid.CreateVersion7()}");
        Directory.CreateDirectory(_directory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Test]
    public async Task IdenticalDocuments_AreCompatible()
    {
        var result = await OpenApiCompatibilityCheck.CompareAsync(BaseFixturePath, BaseFixturePath);

        Assert.That(result.IsCompatible, Is.True, result.Report);
    }

    [Test]
    public async Task AnAddedRoute_IsCompatible()
    {
        var revision = await WriteRevisionAsync(doc =>
            doc["paths"]!["/api/v1/widgets"] = JsonNode.Parse(
                """{ "get": { "operationId": "listWidgets", "responses": { "200": { "description": "OK" } } } }"""));

        var result = await OpenApiCompatibilityCheck.CompareAsync(BaseFixturePath, revision);

        Assert.That(result.IsCompatible, Is.True, result.Report);
    }

    [Test]
    public async Task AnAddedOptionalProperty_IsCompatible()
    {
        var revision = await WriteRevisionAsync(doc =>
        {
            Properties(doc, "Thing")["colour"] = new JsonObject { ["type"] = "string" };
            Properties(doc, "WriteThing")["colour"] = new JsonObject { ["type"] = "string" };
        });

        var result = await OpenApiCompatibilityCheck.CompareAsync(BaseFixturePath, revision);

        Assert.That(result.IsCompatible, Is.True, result.Report);
    }

    [Test]
    public async Task ARemovedProperty_IsBreaking_AndTheMessageNamesItAndTheRegenerationCommand()
    {
        var revision = await WriteRevisionAsync(doc =>
        {
            Properties(doc, "Thing").Remove("name");
            Schema(doc, "Thing")["required"] = new JsonArray("id");
        });

        var result = await OpenApiCompatibilityCheck.CompareAsync(BaseFixturePath, revision);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsCompatible, Is.False, result.Report);
            Assert.That(result.FailureMessage, Does.Contain("response-required-property-removed"));
            Assert.That(result.FailureMessage, Does.Contain("`name`"));
            Assert.That(result.FailureMessage, Does.Contain(OpenApiDocumentGenerator.RegenerationCommand));
        }
    }

    [Test]
    public async Task APropertyThatBecameRequired_IsBreaking_AndTheMessageNamesItAndTheRegenerationCommand()
    {
        var revision = await WriteRevisionAsync(doc =>
            Schema(doc, "WriteThing")["required"]!.AsArray().Add("description"));

        var result = await OpenApiCompatibilityCheck.CompareAsync(BaseFixturePath, revision);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.IsCompatible, Is.False, result.Report);
            Assert.That(result.FailureMessage, Does.Contain("request-property-became-required"));
            Assert.That(result.FailureMessage, Does.Contain("`description`"));
            Assert.That(result.FailureMessage, Does.Contain(OpenApiDocumentGenerator.RegenerationCommand));
        }
    }

    [Test]
    public async Task ABreakingChange_IsCompatibleOnceTheCommittedDocumentIsRegenerated()
    {
        var revision = await WriteRevisionAsync(doc =>
            Schema(doc, "WriteThing")["required"]!.AsArray().Add("description"));
        Assume.That((await OpenApiCompatibilityCheck.CompareAsync(BaseFixturePath, revision)).IsCompatible, Is.False);

        // Regenerating makes the committed document the one the code now produces.
        var regenerated = Path.Combine(_directory, "regenerated.json");
        File.Copy(revision, regenerated);
        var result = await OpenApiCompatibilityCheck.CompareAsync(regenerated, revision);

        Assert.That(result.IsCompatible, Is.True, result.Report);
    }

    private async Task<string> WriteRevisionAsync(Action<JsonNode> change)
    {
        var doc = JsonNode.Parse(await File.ReadAllTextAsync(BaseFixturePath))!;
        change(doc);
        var path = Path.Combine(_directory, "revision.json");
        await File.WriteAllTextAsync(path, doc.ToJsonString());
        return path;
    }

    private static JsonObject Schema(JsonNode doc, string name) =>
        doc["components"]!["schemas"]![name]!.AsObject();

    private static JsonObject Properties(JsonNode doc, string name) =>
        Schema(doc, name)["properties"]!.AsObject();
}
