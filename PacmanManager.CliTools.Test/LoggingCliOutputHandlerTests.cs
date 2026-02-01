using Microsoft.Extensions.Logging;
using Moq;

namespace PacmanManager.CliTools.Test;

[TestFixture]
public class LoggingCliOutputHandlerTests
{
    private Mock<ILogger<LoggingCliOutputHandler>> _mockLogger = null!;
    private LoggingCliOutputHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _mockLogger = new Mock<ILogger<LoggingCliOutputHandler>>();
        
        // Setup IsEnabled to return true for all log levels
        _mockLogger.Setup(l => l.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        
        // Setup BeginScope to return a disposable
        _mockLogger.Setup(l => l.BeginScope(It.IsAny<It.IsAnyType>()))
            .Returns(Mock.Of<IDisposable>());
        
        _handler = new LoggingCliOutputHandler(_mockLogger.Object);
    }

    [Test]
    public async Task HandleOutputAsync_AllLogEventsWithinScope()
    {
        // Arrange
        var tool = new EchoCliTool("arg1", "arg2");
        var stdOut = CreateStreamReader("line1\nline2\n");
        var stdErr = CreateStreamReader("error1\n");

        // Act
        await _handler.HandleOutputAsync(tool, stdOut, stdErr);

        // Assert - Verify that BeginScope was called with the correct state
        _mockLogger.Verify(
            l => l.BeginScope(
                It.Is<It.IsAnyType>((state, type) => 
                    state.ToString()!.Contains("Tool: echo") && 
                    state.ToString()!.Contains("Executable: echo") && 
                    state.ToString()!.Contains("Arguments: arg1 arg2"))),
            Times.Once);
    }

    [Test]
    public async Task HandleOutputAsync_StdOutLines_LoggedAsInformation()
    {
        // Arrange
        var tool = new EchoCliTool("test");
        var stdOut = CreateStreamReader("line1\nline2\nline3\n");
        var stdErr = CreateStreamReader("");

        // Act
        await _handler.HandleOutputAsync(tool, stdOut, stdErr);

        // Assert
        VerifyLog(_mockLogger, LogLevel.Information, "line1", Times.Once());
        VerifyLog(_mockLogger, LogLevel.Information, "line2", Times.Once());
        VerifyLog(_mockLogger, LogLevel.Information, "line3", Times.Once());
    }

    [Test]
    public async Task HandleOutputAsync_StdErrLines_LoggedAsError()
    {
        // Arrange
        var tool = new EchoCliTool("test");
        var stdOut = CreateStreamReader("");
        var stdErr = CreateStreamReader("error1\nerror2\nerror3\n");

        // Act
        await _handler.HandleOutputAsync(tool, stdOut, stdErr);

        // Assert
        VerifyLog(_mockLogger, LogLevel.Error, "error1", Times.Once());
        VerifyLog(_mockLogger, LogLevel.Error, "error2", Times.Once());
        VerifyLog(_mockLogger, LogLevel.Error, "error3", Times.Once());
    }

    [Test]
    public async Task HandleOutputAsync_MixedOutput_LogsCorrectLevels()
    {
        // Arrange
        var tool = new EchoCliTool("test");
        var stdOut = CreateStreamReader("stdout1\nstdout2\n");
        var stdErr = CreateStreamReader("stderr1\nstderr2\n");

        // Act
        await _handler.HandleOutputAsync(tool, stdOut, stdErr);

        // Assert
        // Verify stdout lines logged as Information
        VerifyLog(_mockLogger, LogLevel.Information, "stdout1", Times.Once());
        VerifyLog(_mockLogger, LogLevel.Information, "stdout2", Times.Once());
        
        // Verify stderr lines logged as Error
        VerifyLog(_mockLogger, LogLevel.Error, "stderr1", Times.Once());
        VerifyLog(_mockLogger, LogLevel.Error, "stderr2", Times.Once());
    }

    [Test]
    public async Task HandleOutputAsync_EmptyStreams_NoLinesLogged()
    {
        // Arrange
        var tool = new EchoCliTool("test");
        var stdOut = CreateStreamReader("");
        var stdErr = CreateStreamReader("");

        // Act
        await _handler.HandleOutputAsync(tool, stdOut, stdErr);

        // Assert
        // Verify no log calls (except for scope creation)
        _mockLogger.Verify(
            l => l.Log(
                It.IsAny<LogLevel>(),
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Never);
    }

    private static StreamReader CreateStreamReader(string content)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(content);
        writer.Flush();
        stream.Position = 0;
        return new StreamReader(stream);
    }

    private static void VerifyLog(
        Mock<ILogger<LoggingCliOutputHandler>> mockLogger,
        LogLevel level,
        string message,
        Times times)
    {
        mockLogger.Verify(
            l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, type) => 
                    state.ToString()!.Contains(message)),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
    }
}
