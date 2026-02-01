namespace PacmanManager.CliTools;

/// <summary>
/// Interface for running CLI tools from code.
/// </summary>
public interface ICliToolRunner
{
    /// <summary>
    /// Run the given CLI tool. Returns the exit code of the tool.
    /// </summary>
    /// <param name="tool">Descriptor of the cli tool to run.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>Exit code of the tool</returns>
    /// <exception cref="InvalidOperationException">Thrown if the tool could not be executed.</exception>
    Task<int> RunToolAsync(ICliTool tool, CancellationToken ct = default);

    /// <summary>
    /// Run the given CLI tool with specified output handlers. Returns the exit code of the tool. Upon return,
    /// all output handlers have completed processing.
    /// </summary>
    /// <param name="tool">Descriptor of the cli tool to run.</param>
    /// <param name="outputHandler">Handler for the output of the tool.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>Exit code of the tool</returns>
    /// <exception cref="InvalidOperationException">Thrown if the tool could not be executed.</exception>
    Task<int> RunToolAsync(ICliTool tool, ICliOutputHandler outputHandler, CancellationToken ct = default);

    /// <summary>
    /// Run the given CLI tool with specified output handlers. Returns the exit code of the tool. Upon return,
    /// all output handlers have completed processing.
    /// </summary>
    /// <param name="tool">Descriptor of the cli tool to run.</param>
    /// <param name="outputHandlers">List of output handlers for this specific tool run.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>Exit code of the tool</returns>
    /// <exception cref="InvalidOperationException">Thrown if the tool could not be executed.</exception>
    Task<int> RunToolAsync(
        ICliTool tool, 
        IEnumerable<ICliOutputHandler> outputHandlers,
        CancellationToken ct = default
    );
}