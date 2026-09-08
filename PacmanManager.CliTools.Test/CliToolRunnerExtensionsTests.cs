using Moq;

namespace PacmanManager.CliTools.Test;

/// <summary>
/// Tests for the one thing <c>RunToolCheckedAsync</c> exists to do: notice that a tool which ran
/// and failed did so.
/// </summary>
/// <remarks>
/// Most of these run against a doubled <see cref="ICliToolRunner"/>, because what is under test is
/// what the extension does with an exit code and the output that came with it, not how a process
/// gets started. The last fixture runs a real one, so that the two halves are known to fit.
/// </remarks>
[TestFixture]
public class CliToolRunnerExtensionsTests
{
    private Mock<ICliToolRunner> _runner = null!;
    private EchoCliTool _tool = null!;

    [SetUp]
    public void SetUp()
    {
        _runner = new Mock<ICliToolRunner>();
        _tool = new EchoCliTool("test");
    }

    /// <summary>
    /// Arranges the runner to report <paramref name="exitCode"/>, having written
    /// <paramref name="stdOut"/> and <paramref name="stdErr"/> to every handler it was given.
    /// </summary>
    private void GivenToolRun(int exitCode, string stdOut = "", string stdErr = "")
    {
        _runner
            .Setup(c => c.RunToolAsync(It.IsAny<ICliTool>(), It.IsAny<ICliOutputHandler>(),
                It.IsAny<CancellationToken>()))
            .Returns((ICliTool tool, ICliOutputHandler handler, CancellationToken _)
                => WriteOutputAsync(tool, [handler], exitCode, stdOut, stdErr));

        _runner
            .Setup(c => c.RunToolAsync(It.IsAny<ICliTool>(), It.IsAny<IEnumerable<ICliOutputHandler>>(),
                It.IsAny<CancellationToken>()))
            .Returns((ICliTool tool, IEnumerable<ICliOutputHandler> handlers, CancellationToken _)
                => WriteOutputAsync(tool, handlers, exitCode, stdOut, stdErr));
    }

    private static async Task<int> WriteOutputAsync(
        ICliTool tool,
        IEnumerable<ICliOutputHandler> handlers,
        int exitCode,
        string stdOut,
        string stdErr)
    {
        foreach (var handler in handlers)
        {
            using var outReader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(stdOut)));
            using var errReader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(stdErr)));
            await handler.HandleOutputAsync(tool, outReader, errReader);
        }

        return exitCode;
    }

    [Test]
    public void RunToolCheckedAsync_Returns_WhenTheToolSucceeds()
    {
        // Arrange
        GivenToolRun(0, stdOut: "test");

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _runner.Object.RunToolCheckedAsync(_tool));
    }

    [Test]
    public void RunToolCheckedAsync_Throws_WhenTheToolExitsNonZero()
    {
        // Arrange
        GivenToolRun(1, stdErr: "ERROR: Failed to acquire lockfile");

        // Act
        var thrown = Assert.ThrowsAsync<CliToolFailedException>(
            async () => await _runner.Object.RunToolCheckedAsync(_tool));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(thrown!.Tool, Is.EqualTo("echo"));
            Assert.That(thrown.ExitCode, Is.EqualTo(1));
            Assert.That(thrown.Diagnostics, Is.EqualTo("ERROR: Failed to acquire lockfile"));
        });
    }

    /// <summary>
    /// Plenty of tools report their problems on standard output — <c>repo-add</c> among them — so
    /// the diagnostics fall back to it rather than carrying an empty message.
    /// </summary>
    [Test]
    public void RunToolCheckedAsync_FallsBackToStandardOutput_WhenTheToolSaidNothingOnStandardError()
    {
        // Arrange
        GivenToolRun(2, stdOut: "==> ERROR: 'foo.db.tar.gz' does not exist");

        // Act
        var thrown = Assert.ThrowsAsync<CliToolFailedException>(
            async () => await _runner.Object.RunToolCheckedAsync(_tool));

        // Assert
        Assert.That(thrown!.Diagnostics, Is.EqualTo("==> ERROR: 'foo.db.tar.gz' does not exist"));
    }

    /// <summary>
    /// Collecting the output for the exception is in addition to whatever the caller asked for, not
    /// instead of it.
    /// </summary>
    [Test]
    public async Task RunToolCheckedAsync_StillGivesTheCallersHandlersTheOutput()
    {
        // Arrange
        GivenToolRun(0, stdOut: "hello", stdErr: "a warning");
        var callersHandler = new CollectingCliOutputHandler();

        // Act
        await _runner.Object.RunToolCheckedAsync(_tool, callersHandler);

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(callersHandler.StdOut, Is.EqualTo("hello"));
            Assert.That(callersHandler.StdErr, Is.EqualTo("a warning"));
        });
    }

    /// <summary>
    /// The checking is opt-in: the plain overloads still hand back a non-zero code for a caller
    /// that wants to decide for itself what it means.
    /// </summary>
    [Test]
    public async Task RunToolAsync_StillReportsANonZeroExitAsAValue()
    {
        // Arrange
        GivenToolRun(1, stdErr: "no");

        // Act
        var exitCode = await _runner.Object.RunToolAsync(_tool, new CollectingCliOutputHandler());

        // Assert
        Assert.That(exitCode, Is.EqualTo(1));
    }

    /// <summary>
    /// The same thing against a real process, so that the extension is known to work over the
    /// runner it extends and not just over a double of it.
    /// </summary>
    [Test]
    public void RunToolCheckedAsync_Throws_ForARealProcessThatExitsNonZero()
    {
        // Arrange
        var registry = new Mock<ICliOutputHandlerRegistry>();
        registry.Setup(r => r.GetGlobalOutputHandlers()).Returns([]);
        var runner = new CliToolRunner(registry.Object);
        var tool = new ShellCliTool("failing-tool", "echo 'it went wrong' >&2; exit 3");

        // Act
        var thrown = Assert.ThrowsAsync<CliToolFailedException>(
            async () => await runner.RunToolCheckedAsync(tool));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(thrown!.Tool, Is.EqualTo("failing-tool"));
            Assert.That(thrown.ExitCode, Is.EqualTo(3));
            Assert.That(thrown.Diagnostics, Does.Contain("it went wrong"));
        });
    }
}
