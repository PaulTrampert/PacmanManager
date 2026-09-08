namespace PacmanManager.CliTools;

/// <summary>
/// Thrown when a tool run with
/// <see cref="CliToolRunnerExtensions.RunToolCheckedAsync(ICliToolRunner,ICliTool,CancellationToken)"/>
/// ran to completion and exited non-zero.
/// </summary>
/// <remarks>
/// This says only that the tool failed, never why: a runner generic over every tool has no way to
/// tell one non-zero code from another, and the tools themselves distinguish their reasons only in
/// prose. A caller that can act on a particular reason is the one that knows how to recognise it,
/// and can catch this and read <see cref="Diagnostics"/> to do so.
/// </remarks>
/// <param name="tool">The <see cref="ICliTool.Name"/> of the tool that failed.</param>
/// <param name="exitCode">The exit code it reported.</param>
/// <param name="diagnostics">What it wrote about the failure.</param>
public class CliToolFailedException(string tool, int exitCode, string diagnostics)
    : Exception($"'{tool}' exited with code {exitCode}: {diagnostics}")
{
    /// <summary>
    /// The <see cref="ICliTool.Name"/> of the tool that failed.
    /// </summary>
    public string Tool { get; } = tool;

    /// <summary>
    /// The exit code the tool reported.
    /// </summary>
    public int ExitCode { get; } = exitCode;

    /// <summary>
    /// What the tool wrote about the failure: its standard error, or its standard output when it
    /// said nothing on standard error.
    /// </summary>
    public string Diagnostics { get; } = diagnostics;
}
