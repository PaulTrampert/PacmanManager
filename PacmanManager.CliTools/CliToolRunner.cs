using System.Diagnostics;
using System.IO.Pipelines;

namespace PacmanManager.CliTools;

/// <summary>
/// Class that runs CLI tools and manages their output handling.
/// </summary>
/// <param name="outputHandlerRegistry">Registry of global output handlers.</param>
public class CliToolRunner(ICliOutputHandlerRegistry outputHandlerRegistry)
{
    private const int FwdBufferSize = 4096;

    /// <summary>
    /// Run the given CLI tool with specified output handlers. Returns the exit code of the tool. Upon return,
    /// all output handlers have completed processing.
    /// </summary>
    /// <param name="tool">Descriptor of the cli tool to run.</param>
    /// <param name="outputHandlers">List of output handlers for this specific tool run.</param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>Exit code of the tool</returns>
    /// <exception cref="InvalidOperationException">Thrown if the tool could not be executed.</exception>
    public async Task<int> RunToolAsync(
        ICliTool tool, 
        IEnumerable<ICliOutputHandler>? outputHandlers = null,
        CancellationToken ct = default
    )
    {
        var startInfo = new ProcessStartInfo(tool.Executable, tool.Arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        using var process = Process.Start(startInfo);

        if (process == null)
        {
            throw new InvalidOperationException($"Failed to start process for tool {tool.Name}");
        }

        var allOutputHandlers = outputHandlerRegistry
            .GetGlobalOutputHandlers()
            .Concat(outputHandlers ?? [])
            .ToArray();

        var outputs = allOutputHandlers
            .Select(_ =>
                (
                    new Pipe(), 
                    new Pipe()
                ))
            .ToArray();
        
        var outputHandlingTasks = allOutputHandlers
            .Select((handler, idx) => 
                HandleOutputAsync(tool, outputs[idx].Item1, outputs[idx].Item2, handler))
            .ToArray();

        Task[] streamFwdTasks =
        [
            ForwardStreamAsync(process.StandardOutput,
                outputs.Select(o => o.Item1).ToArray(), ct),
            ForwardStreamAsync(process.StandardError,
                outputs.Select(o => o.Item2).ToArray(), ct)
        ];

        await process.WaitForExitAsync(ct);

        await Task.WhenAll(streamFwdTasks);

        await Task.WhenAll(outputHandlingTasks);

        return process.ExitCode;
    }

    private async Task ForwardStreamAsync(StreamReader src, Pipe[] dest, CancellationToken ct)
    {
        try
        {
            var buffer = new char[FwdBufferSize];
            var read = 0;
            var writers = dest.Select(d => new StreamWriter(d.Writer.AsStream()))
                .ToArray();
            while ((read = await src.ReadAsync(buffer, 0, FwdBufferSize)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                var read1 = read;
                var writeTasks = writers.Select(d => d.WriteAsync(buffer, 0, read1));
                await Task.WhenAll(writeTasks);
            }
            await Task.WhenAll(writers.Select(d => d.FlushAsync(ct)));
        }
        finally
        {
            await Task.WhenAll(dest.Select(d => d.Writer.CompleteAsync().AsTask()));
        }
    }

    private async Task HandleOutputAsync(ICliTool tool, Pipe stdOutPipe, Pipe stdErrPipe, ICliOutputHandler handler)
    {
        var stdOutReader = new StreamReader(stdOutPipe.Reader.AsStream());
        var stdErrReader = new StreamReader(stdErrPipe.Reader.AsStream());
        
        await handler.HandleOutputAsync(tool, stdOutReader, stdErrReader);
        
        await stdOutPipe.Reader.CompleteAsync();
        await stdErrPipe.Reader.CompleteAsync();
    }
}