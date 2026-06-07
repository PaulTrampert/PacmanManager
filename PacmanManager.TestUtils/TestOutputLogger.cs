using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace PacmanManager.TestUtils;

/// <summary>
/// Represents a log event captured during testing.
/// </summary>
public record LogEvent
{
    /// <summary>
    /// The log level of the event.
    /// </summary>
    public LogLevel LogLevel { get; init; }
    
    /// <summary>
    /// The category name (typically the logger name).
    /// </summary>
    public string Category { get; init; } = string.Empty;
    
    /// <summary>
    /// The log message.
    /// </summary>
    public string Message { get; init; } = string.Empty;
    
    /// <summary>
    /// The exception associated with this log event, if any.
    /// </summary>
    public Exception? Exception { get; init; }
}

/// <summary>
/// An ILogger implementation that writes to NUnit's TestContext and captures log events for assertions.
/// </summary>
public class TestOutputLogger : ILogger
{
    private readonly string _categoryName;

    /// <summary>
    /// Gets all log events that have been captured by this logger.
    /// </summary>
    public IEnumerable<LogEvent> LogEvents { get; private set; } = [];
    
    /// <summary>
    /// Creates a new instance of TestOutputLogger.
    /// </summary>
    /// <param name="categoryName">The category name for this logger.</param>
    public TestOutputLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    /// <inheritdoc />
    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => true;

    /// <inheritdoc />
    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
        {
            return;
        }

        var message = formatter(state, exception);
        if (string.IsNullOrEmpty(message))
        {
            return;
        }
        
        LogEvents = [
            ..LogEvents,
            new LogEvent
            {
                LogLevel = logLevel,
                Category = _categoryName,
                Message = message,
                Exception = exception
            }
        ];

        // Use TestContext.Out.WriteLine to write to the test output
        // TestContext.Progress.WriteLine can be used for immediate output (useful for long-running tests)
        TestContext.Out.WriteLine($"[{logLevel}][{_categoryName}] {message}");

        if (exception != null)
        {
            TestContext.Out.WriteLine(exception.ToString());
        }
    }

    private class NullScope : IDisposable
    {
        public static NullScope Instance { get; } = new NullScope();
        public void Dispose() { }
    }
}

public class TestOutputLogger<T>() : TestOutputLogger(typeof(T).FullName!), ILogger<T>;

/// <summary>
/// An ILoggerProvider implementation that creates TestOutputLogger instances.
/// </summary>
public class TestOutputLoggerProvider : ILoggerProvider
{
    /// <inheritdoc />
    public ILogger CreateLogger(string categoryName)
    {
        return new TestOutputLogger(categoryName);
    }

    /// <inheritdoc />
    public void Dispose() { }
}
