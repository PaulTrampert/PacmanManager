namespace PacmanManager.CliTools;

/// <summary>
/// The checking half of <see cref="ICliToolRunner"/>: run a tool and treat a non-zero exit as a
/// failure rather than as a value to inspect.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ICliToolRunner.RunToolAsync(ICliTool,CancellationToken)"/> cannot throw on a non-zero
/// exit, because a runner generic over every tool does not know which codes are failures — plenty
/// of tools use them to answer a question. That is the right default and the wrong one for most
/// callers, since ignoring a failure is spelled exactly like not caring about the result. So the
/// check is offered here instead, opt-in per call: a caller that wants it says
/// <c>RunToolCheckedAsync</c> and cannot then forget to look at what it got back.
/// </para>
/// <para>
/// The tool's output is collected as well as passed to whatever handlers the caller and the
/// registry supply, so that <see cref="CliToolFailedException"/> can carry what the tool said about
/// failing. A stack trace reporting that something exited 1 and nothing else is a poor thing to
/// find in a log.
/// </para>
/// </remarks>
public static class CliToolRunnerExtensions
{
    /// <summary>
    /// Runs <paramref name="tool"/> to completion, succeeding only on a zero exit code.
    /// </summary>
    /// <param name="runner">The runner to execute the tool with.</param>
    /// <param name="tool">Descriptor of the cli tool to run.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <exception cref="CliToolFailedException">The tool ran and exited non-zero.</exception>
    /// <exception cref="InvalidOperationException">The tool could not be executed at all.</exception>
    public static Task RunToolCheckedAsync(this ICliToolRunner runner, ICliTool tool, CancellationToken ct = default)
        => runner.RunToolCheckedAsync(tool, [], ct);

    /// <summary>
    /// Runs <paramref name="tool"/> to completion with the given output handler, succeeding only on
    /// a zero exit code.
    /// </summary>
    /// <param name="runner">The runner to execute the tool with.</param>
    /// <param name="tool">Descriptor of the cli tool to run.</param>
    /// <param name="outputHandler">Handler for the output of the tool.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <exception cref="CliToolFailedException">The tool ran and exited non-zero.</exception>
    /// <exception cref="InvalidOperationException">The tool could not be executed at all.</exception>
    public static Task RunToolCheckedAsync(
        this ICliToolRunner runner,
        ICliTool tool,
        ICliOutputHandler outputHandler,
        CancellationToken ct = default
    ) => runner.RunToolCheckedAsync(tool, [outputHandler], ct);

    /// <summary>
    /// Runs <paramref name="tool"/> to completion with the given output handlers, succeeding only
    /// on a zero exit code.
    /// </summary>
    /// <param name="runner">The runner to execute the tool with.</param>
    /// <param name="tool">Descriptor of the cli tool to run.</param>
    /// <param name="outputHandlers">List of output handlers for this specific tool run.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <exception cref="CliToolFailedException">The tool ran and exited non-zero.</exception>
    /// <exception cref="InvalidOperationException">The tool could not be executed at all.</exception>
    public static async Task RunToolCheckedAsync(
        this ICliToolRunner runner,
        ICliTool tool,
        IEnumerable<ICliOutputHandler> outputHandlers,
        CancellationToken ct = default
    )
    {
        var collected = new CollectingCliOutputHandler();
        var handlers = outputHandlers as ICliOutputHandler[] ?? outputHandlers.ToArray();
        var exitCode = handlers.Length == 0
            ? await runner.RunToolAsync(tool, collected, ct)
            : await runner.RunToolAsync(tool, [..handlers, collected], ct);

        if (exitCode == 0)
        {
            return;
        }

        // Not every tool says what went wrong on the stream it is supposed to: repo-add reports
        // plenty of its problems on standard output, so an empty standard error is no reason to
        // throw a message that says nothing.
        var diagnostics = string.IsNullOrWhiteSpace(collected.StdErr) ? collected.StdOut : collected.StdErr;

        throw new CliToolFailedException(tool.Name, exitCode, diagnostics);
    }
}
