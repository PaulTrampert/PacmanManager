namespace PacmanManager.CliTools;

/// <summary>
/// Interface for handling the output of CLI tools. Implementations should define how to process
/// the standard output and error streams of a CLI tool.
/// </summary>
public interface ICliOutputHandler
{
    /// <summary>
    /// Handles the output from the specified CLI tool. The returned task should not complete until stdOut and stdErr
    /// have been fully read and processed.
    /// </summary>
    /// <param name="tool">The CLI tool whose output is being handled.</param>
    /// <param name="stdOut">StreamReader for the standard output of the tool.</param>
    /// <param name="stdErr">StreamReader for the standard error of the tool.</param>
    /// <returns>A Task that completes once the output has been handled.</returns>
    Task HandleOutputAsync(ICliTool tool, StreamReader stdOut, StreamReader stdErr);
}