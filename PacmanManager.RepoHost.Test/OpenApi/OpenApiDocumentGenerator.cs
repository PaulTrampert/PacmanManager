using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.OpenApi;

/// <summary>
/// Generates RepoHost's <c>v1</c> OpenAPI document the way <c>scripts/generate-openapi.sh</c> does,
/// but into a file of the caller's choosing.
/// </summary>
/// <remarks>
/// The environment is <c>Development</c>, since the document embeds
/// <c>Swagger:OpenIdConnectUrl</c> and only the development settings define it, and the content root
/// is RepoHost's output directory, where those settings are copied. The output directory is the one
/// built in this test assembly's own configuration, so a Release build tests the Release output.
/// <c>DATA_DIR</c> and the connection string are removed from the environment: generation must need
/// neither.
/// </remarks>
public static class OpenApiDocumentGenerator
{
    /// <summary>
    /// The committed document, relative to the repository root.
    /// </summary>
    public const string CommittedDocumentRelativePath = "pacmanmanager-web/openapi/v1.json";

    /// <summary>
    /// The command that regenerates the committed document, run from the repository root.
    /// </summary>
    public const string RegenerationCommand =
        "dotnet tool restore && dotnet build PacmanManager.RepoHost && scripts/generate-openapi.sh";

    private static readonly TimeSpan Timeout = TimeSpan.FromMinutes(2);

    /// <summary>
    /// The committed document's absolute path.
    /// </summary>
    public static string CommittedDocumentPath =>
        Path.Combine(DirUtils.FindSolutionDirectory(), CommittedDocumentRelativePath);

    /// <summary>
    /// The build configuration this test assembly was compiled in, and so the one RepoHost was
    /// built in alongside it.
    /// </summary>
    public static string Configuration =>
        typeof(OpenApiDocumentGenerator).Assembly.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration
        ?? "Debug";

    /// <summary>
    /// Generates the document into <paramref name="outputPath"/>.
    /// </summary>
    /// <param name="outputPath">Where to write the document.</param>
    /// <returns>The CLI's exit code and everything it and the host wrote.</returns>
    public static async Task<OpenApiGenerationResult> GenerateAsync(string outputPath)
    {
        var solutionDirectory = DirUtils.FindSolutionDirectory();
        var bin = Path.Combine(solutionDirectory, "PacmanManager.RepoHost", "bin", Configuration, "net10.0");
        var startInfo = new ProcessStartInfo("dotnet",
            ["swagger", "tofile", "--output", outputPath, Path.Combine(bin, "PacmanManager.RepoHost.dll"), "v1"])
        {
            WorkingDirectory = solutionDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        startInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        startInfo.Environment["ASPNETCORE_CONTENTROOT"] = bin;
        startInfo.Environment.Remove("DATA_DIR");
        startInfo.Environment.Remove("ConnectionStrings__pacmanmanager");

        Process process;
        try
        {
            process = Process.Start(startInfo)
                      ?? throw new InvalidOperationException("dotnet swagger did not start.");
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException("dotnet is not on the PATH.", ex);
        }

        using (process)
        {
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(Timeout);
            try
            {
                await process.WaitForExitAsync(timeout.Token);
            }
            catch (OperationCanceledException)
            {
                process.Kill(entireProcessTree: true);
                throw new TimeoutException($"dotnet swagger tofile did not finish within {Timeout}.");
            }

            return new OpenApiGenerationResult(process.ExitCode, await stdout + await stderr);
        }
    }
}

/// <summary>
/// The outcome of <see cref="OpenApiDocumentGenerator.GenerateAsync"/>.
/// </summary>
/// <param name="ExitCode">The CLI's exit code.</param>
/// <param name="Output">Its standard output followed by its standard error.</param>
public record OpenApiGenerationResult(int ExitCode, string Output);
