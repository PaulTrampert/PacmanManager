namespace PacmanManager.CliTools;

/// <summary>
/// Interface for running CLI tools from code.
/// </summary>
/// <remarks>
/// <para>
/// Every method here reports a tool that ran and <i>failed</i> through its return value alone. An
/// exception means the process could not be started at all; a process that started, ran and exited
/// non-zero — a missing file, a lock it could not take, a directory it could not write — completes
/// the returned task normally, carrying that exit code. So the returned code is the only failure
/// signal there is for a tool that ran, and discarding it discards the failure with it.
/// </para>
/// <para>
/// That is deliberate: a runner generic over every tool cannot know which non-zero codes are
/// failures, since plenty of tools use them to answer a question. It does mean the burden sits with
/// the caller, and that ignoring a failure looks exactly like not caring about the result. A caller
/// that does care is better served by a small wrapper of its own that checks the code once and
/// throws — <c>PacmanManager.RepoHost</c>'s <c>IRepositoryDatabaseToolRunner</c> is one — than by
/// repeating the check at every call site and eventually forgetting one.
/// </para>
/// </remarks>
public interface ICliToolRunner
{
    /// <summary>
    /// Run the given CLI tool. Returns the exit code of the tool.
    /// </summary>
    /// <param name="tool">Descriptor of the cli tool to run.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>
    /// Exit code of the tool. This is the only signal that the tool failed: a non-zero code is
    /// reported here rather than thrown, so a caller that discards the result has silently accepted
    /// whatever went wrong.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the tool could not be executed. A tool that <i>ran</i> and failed does not throw.
    /// </exception>
    Task<int> RunToolAsync(ICliTool tool, CancellationToken ct = default);

    /// <summary>
    /// Run the given CLI tool with specified output handlers. Returns the exit code of the tool. Upon return,
    /// all output handlers have completed processing.
    /// </summary>
    /// <param name="tool">Descriptor of the cli tool to run.</param>
    /// <param name="outputHandler">Handler for the output of the tool.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>
    /// Exit code of the tool. This is the only signal that the tool failed: a non-zero code is
    /// reported here rather than thrown, so a caller that discards the result has silently accepted
    /// whatever went wrong. Whatever the tool wrote about it reached
    /// <paramref name="outputHandler"/>, not this task.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the tool could not be executed. A tool that <i>ran</i> and failed does not throw.
    /// </exception>
    Task<int> RunToolAsync(ICliTool tool, ICliOutputHandler outputHandler, CancellationToken ct = default);

    /// <summary>
    /// Run the given CLI tool with specified output handlers. Returns the exit code of the tool. Upon return,
    /// all output handlers have completed processing.
    /// </summary>
    /// <param name="tool">Descriptor of the cli tool to run.</param>
    /// <param name="outputHandlers">List of output handlers for this specific tool run.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>
    /// Exit code of the tool. This is the only signal that the tool failed: a non-zero code is
    /// reported here rather than thrown, so a caller that discards the result has silently accepted
    /// whatever went wrong. Whatever the tool wrote about it reached
    /// <paramref name="outputHandlers"/>, not this task.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the tool could not be executed. A tool that <i>ran</i> and failed does not throw.
    /// </exception>
    Task<int> RunToolAsync(
        ICliTool tool, 
        IEnumerable<ICliOutputHandler> outputHandlers,
        CancellationToken ct = default
    );
}
