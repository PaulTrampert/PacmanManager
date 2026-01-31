using Moq;

namespace PacmanManager.CliTools.Test;

[TestFixture]
public class CliToolRunnerTests
{
    private Mock<ICliOutputHandlerRegistry> _mockRegistry = null!;
    private CliToolRunner _runner = null!;

    [SetUp]
    public void SetUp()
    {
        _mockRegistry = new Mock<ICliOutputHandlerRegistry>();
        _mockRegistry.Setup(r => r.GetGlobalOutputHandlers())
            .Returns(Enumerable.Empty<ICliOutputHandler>());
        _runner = new CliToolRunner(_mockRegistry.Object);
    }

    [Test]
    public async Task RunToolAsync_EchoCliTool_ReturnsZero()
    {
        // Arrange
        var tool = new EchoCliTool("test");

        // Act
        var exitCode = await _runner.RunToolAsync(tool);

        // Assert
        Assert.That(exitCode, Is.EqualTo(0));
    }

    [Test]
    public async Task RunToolAsync_EchoCliTool_ShortString_GetsOutputToCollectingHandler()
    {
        // Arrange
        var testString = "Hello, World!";
        var tool = new EchoCliTool(testString);
        var handler = new CollectingCliOutputHandler();

        // Act
        var exitCode = await _runner.RunToolAsync(tool, [handler]);

        // Assert
        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(handler.StdOut, Is.EqualTo($"{testString}\n"));
        Assert.That(handler.StdErr, Is.Empty);
    }

    [Test]
    public async Task RunToolAsync_EchoCliTool_LongString_GetsFullOutputToCollectingHandler()
    {
        // Arrange
        // Generate a string longer than the forward buffer size (4096 characters)
        var longString = new string('A', 5000);
        var tool = new EchoCliTool(longString);
        var handler = new CollectingCliOutputHandler();

        // Act
        var exitCode = await _runner.RunToolAsync(tool, [handler]);

        // Assert
        Assert.That(exitCode, Is.EqualTo(0));
        Assert.That(handler.StdOut, Is.EqualTo($"{longString}\n"));
        Assert.That(handler.StdErr, Is.Empty);
    }

    [Test]
    public async Task RunToolAsync_MultipleCollectingHandlers_EachReceivesFullCopy()
    {
        // Arrange
        var testString = new string('B', 6000); // Longer than buffer size
        var tool = new EchoCliTool(testString);
        
        // Handler from registry
        var registryHandler = new CollectingCliOutputHandler();
        _mockRegistry.Setup(r => r.GetGlobalOutputHandlers())
            .Returns(new[] { registryHandler });
        
        // Handler passed as argument
        var argumentHandler = new CollectingCliOutputHandler();

        // Act
        var exitCode = await _runner.RunToolAsync(tool, [argumentHandler]);

        // Assert
        Assert.That(exitCode, Is.EqualTo(0));
        
        var expectedOutput = $"{testString}\n";
        
        // Verify registry handler received full output
        Assert.That(registryHandler.StdOut, Is.EqualTo(expectedOutput));
        Assert.That(registryHandler.StdErr, Is.Empty);
        
        // Verify argument handler received full output
        Assert.That(argumentHandler.StdOut, Is.EqualTo(expectedOutput));
        Assert.That(argumentHandler.StdErr, Is.Empty);
    }
}
