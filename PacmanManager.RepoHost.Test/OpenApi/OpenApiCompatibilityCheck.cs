using System.Text;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using DotNet.Testcontainers.Images;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.OpenApi;

/// <summary>
/// Compares two OpenAPI documents with <c>oasdiff breaking</c>, run in the <c>tufin/oasdiff</c>
/// image, and reports whether the second is still compatible with the first.
/// </summary>
/// <remarks>
/// What counts as breaking is exactly <c>oasdiff</c>'s <c>ERR</c> level; this class adds no rules of
/// its own. The image runs once per comparison and exits, so nothing is left running.
/// </remarks>
public static class OpenApiCompatibilityCheck
{
    /// <summary>
    /// The pinned <c>oasdiff</c> image. Bump it deliberately: a newer release may classify a change
    /// differently.
    /// </summary>
    public const string Image = "tufin/oasdiff:v1.33.0";

    private const string BasePath = "/specs/base.json";
    private const string RevisionPath = "/specs/revision.json";

    /// <summary>
    /// Compares <paramref name="revisionPath"/> against <paramref name="basePath"/>.
    /// </summary>
    /// <param name="basePath">The document clients were built against: the committed one.</param>
    /// <param name="revisionPath">The document the code produces now.</param>
    /// <returns>Whether the revision is compatible, and <c>oasdiff</c>'s report.</returns>
    /// <exception cref="InvalidOperationException"><c>oasdiff</c> failed rather than reporting a result.</exception>
    public static async Task<OpenApiCompatibilityResult> CompareAsync(string basePath, string revisionPath)
    {
        await using var container = new ContainerBuilder(new DockerImage(Image))
            .WithResourceMapping(await File.ReadAllBytesAsync(basePath), BasePath)
            .WithResourceMapping(await File.ReadAllBytesAsync(revisionPath), RevisionPath)
            .WithCommand("breaking", BasePath, RevisionPath, "--fail-on", "ERR", "--color", "never")
            .WithWaitStrategy(Wait.ForUnixContainer()
                .AddCustomWaitStrategy(new UntilExitWaitStrategy(), s => s.WithMode(WaitStrategyMode.OneShot)))
            .WithLogger(new TestOutputLogger(nameof(OpenApiCompatibilityCheck)))
            .WithCleanUp(true)
            .Build();

        await container.StartAsync();
        var exitCode = await container.GetExitCodeAsync();
        var (stdout, stderr) = await container.GetLogsAsync(timestampsEnabled: false);
        var report = new StringBuilder(stdout.Trim()).AppendLine().Append(stderr.Trim()).ToString().Trim();

        return exitCode switch
        {
            0 => new OpenApiCompatibilityResult(true, report),
            1 => new OpenApiCompatibilityResult(false, report),
            _ => throw new InvalidOperationException($"oasdiff exited with code {exitCode}:\n{report}"),
        };
    }
}

/// <summary>
/// The outcome of an <see cref="OpenApiCompatibilityCheck"/> comparison.
/// </summary>
/// <param name="IsCompatible">Whether the revision has no breaking difference from the base.</param>
/// <param name="Report"><c>oasdiff</c>'s report, naming each breaking difference it found.</param>
public record OpenApiCompatibilityResult(bool IsCompatible, string Report)
{
    /// <summary>
    /// What a failing test says: the breaking differences, and how to regenerate the document.
    /// </summary>
    public string FailureMessage =>
        $"""
         The API has a breaking difference from the committed OpenAPI document, {OpenApiDocumentGenerator.CommittedDocumentRelativePath}:

         {Report}

         If the break is intended, regenerate the document in this change, from the repository root:

             {OpenApiDocumentGenerator.RegenerationCommand}
         """;
}
