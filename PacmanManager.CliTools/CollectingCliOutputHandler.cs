namespace PacmanManager.CliTools;

/// <summary>
/// An output handler that collects standard output and error into properties.
/// </summary>
public class CollectingCliOutputHandler : ICliOutputHandler
{
    /// <summary>
    /// Gets the collected standard output.
    /// </summary>
    public string StdOut { get; private set; } = string.Empty;
    
    /// <summary>
    /// Gets the collected standard error.
    /// </summary>
    public string StdErr { get; private set; } = string.Empty;
    
    /// <inheritdoc />
    public async Task HandleOutputAsync(ICliTool tool, StreamReader stdOut, StreamReader stdErr)
    {
        var stdOutTask = stdOut.ReadToEndAsync();
        var stdErrTask = stdErr.ReadToEndAsync();
        
        await Task.WhenAll(stdOutTask, stdErrTask);
        
        StdOut = stdOutTask.Result;
        StdErr = stdErrTask.Result;
    }
}