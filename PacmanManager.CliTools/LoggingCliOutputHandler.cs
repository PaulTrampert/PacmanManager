using Microsoft.Extensions.Logging;

namespace PacmanManager.CliTools;

/// <summary>
/// <see cref="ICliOutputHandler"/> that logs CLI tool output using the provided <see cref="ILogger"/>. Stdout lines are logged at Information level,
/// while stderr lines are logged at Error level.
/// </summary>
/// <param name="logger">The logger to forward output to.</param>
public partial class LoggingCliOutputHandler(ILogger<LoggingCliOutputHandler> logger) : ICliOutputHandler
{
    /// <inheritdoc />
    public Task HandleOutputAsync(ICliTool tool, StreamReader stdOut, StreamReader stdErr)
    {
        using var scope = logger.BeginScope("Tool: {ToolName}, Executable: {Executable}, Arguments: {Arguments}",
            tool.Name,
            tool.Executable,
            string.Join(' ', tool.Arguments));
        
        var stdOutTask = LogStreamByLinesAsync(stdOut, LogLine);
        var stdErrTask = LogStreamByLinesAsync(stdErr, LogErrorLine);
        
        return Task.WhenAll(stdOutTask, stdErrTask);
    }
    
    private async Task LogStreamByLinesAsync(StreamReader stream, Action<ILogger<LoggingCliOutputHandler>, string> logLineAction)
    {
        while (await stream.ReadLineAsync() is { } line)
        {
            logLineAction(logger, line);
        }
    }
    
    [LoggerMessage(LogLevel.Error, "{line}")]
    static partial void LogErrorLine(ILogger<LoggingCliOutputHandler> logger, string line);

    [LoggerMessage(LogLevel.Information, "{line}")]
    static partial void LogLine(ILogger<LoggingCliOutputHandler> logger, string line);
}