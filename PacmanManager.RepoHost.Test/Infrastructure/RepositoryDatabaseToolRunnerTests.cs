using Moq;
using PacmanManager.CliTools;
using PacmanManager.RepoHost.CliTools;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Infrastructure;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Infrastructure;

/// <summary>
/// Tests for the one thing <c>RepositoryDatabaseToolRunner</c> exists to do: notice that a tool
/// which ran and failed did so.
/// </summary>
/// <remarks>
/// The runner underneath is a double, because the point of each test is what this class does with
/// the exit code it is handed, not what <c>repo-add</c> does with a real database.
/// </remarks>
[TestFixture]
public class RepositoryDatabaseToolRunnerTests
{
    private Mock<ICliToolRunner> _cliRunner = null!;
    private RepositoryDatabaseToolRunner _runner = null!;
    private RepoAdd _tool = null!;
    private Guid _repositoryId;

    [SetUp]
    public void SetUp()
    {
        _cliRunner = new Mock<ICliToolRunner>();
        _runner = new RepositoryDatabaseToolRunner(
            _cliRunner.Object,
            new TestOutputLogger<RepositoryDatabaseToolRunner>());
        _repositoryId = Guid.CreateVersion7();
        _tool = new RepoAdd(_repositoryId.ToString(), "/tmp/pacman/libalpm");
    }

    /// <summary>
    /// Arranges the underlying runner to report <paramref name="exitCode"/>, having written
    /// <paramref name="stdOut"/> and <paramref name="stdErr"/> to the collecting handler it is
    /// given.
    /// </summary>
    private void GivenToolRun(int exitCode, string stdOut = "", string stdErr = "")
    {
        _cliRunner
            .Setup(c => c.RunToolAsync(It.IsAny<ICliTool>(), It.IsAny<ICliOutputHandler>(),
                It.IsAny<CancellationToken>()))
            .Returns(async (ICliTool tool, ICliOutputHandler handler, CancellationToken _) =>
            {
                using var outReader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(stdOut)));
                using var errReader = new StreamReader(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(stdErr)));
                await handler.HandleOutputAsync(tool, outReader, errReader);
                return exitCode;
            });
    }

    [Test]
    public void RunAsync_Returns_WhenTheToolSucceeds()
    {
        // Arrange
        GivenToolRun(0, stdOut: "Creating updated database file");

        // Act & Assert
        Assert.DoesNotThrowAsync(async () => await _runner.RunAsync(_tool, _repositoryId));
    }

    [Test]
    public void RunAsync_Throws_WhenTheToolExitsNonZero()
    {
        // Arrange
        GivenToolRun(1, stdErr: "ERROR: Failed to acquire lockfile");

        // Act
        var thrown = Assert.ThrowsAsync<RepositoryDatabaseToolException>(
            async () => await _runner.RunAsync(_tool, _repositoryId));

        // Assert
        Assert.Multiple(() =>
        {
            Assert.That(thrown!.Tool, Is.EqualTo("repo-add"));
            Assert.That(thrown.ExitCode, Is.EqualTo(1));
            Assert.That(thrown.StandardError, Is.EqualTo("ERROR: Failed to acquire lockfile"));
        });
    }

    /// <summary>
    /// A lock file conflict is not special. The tools say so only in prose, so every non-zero exit
    /// is the same exception and the same 500 — sniffing the message for a reason to answer 409
    /// with would be right only some of the time.
    /// </summary>
    [Test]
    public void RunAsync_TreatsALockFileFailureLikeAnyOther()
    {
        // Arrange
        GivenToolRun(1, stdErr: "ERROR: Failed to acquire lockfile: File exists.");

        // Act & Assert
        Assert.ThrowsAsync<RepositoryDatabaseToolException>(
            async () => await _runner.RunAsync(_tool, _repositoryId));
    }

    /// <summary>
    /// <c>repo-add</c> reports plenty of its problems on standard output, so the diagnostics fall
    /// back to it rather than carrying an empty message.
    /// </summary>
    [Test]
    public void RunAsync_FallsBackToStandardOutput_WhenTheToolSaidNothingOnStandardError()
    {
        // Arrange
        GivenToolRun(2, stdOut: "==> ERROR: 'foo.db.tar.gz' does not exist");

        // Act
        var thrown = Assert.ThrowsAsync<RepositoryDatabaseToolException>(
            async () => await _runner.RunAsync(_tool, _repositoryId));

        // Assert
        Assert.That(thrown!.StandardError, Is.EqualTo("==> ERROR: 'foo.db.tar.gz' does not exist"));
    }
}
