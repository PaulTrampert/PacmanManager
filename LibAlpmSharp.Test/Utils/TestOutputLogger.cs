using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Test.Utils;

public record LogEvent
{
    public LogLevel LogLevel { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public Exception? Exception { get; init; }
}

// ILogger implementation
public class TestOutputLogger : ILogger
{
    private readonly string _categoryName;

    public IEnumerable<LogEvent> LogEvents { get; private set; } = [];
    
    public TestOutputLogger(string categoryName)
    {
        _categoryName = categoryName;
    }

    public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception exception,
        Func<TState, Exception, string> formatter)
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

// ILoggerProvider implementation
public class TestOutputLoggerProvider : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName)
    {
        return new TestOutputLogger(categoryName);
    }

    public void Dispose() { }
}