using DotNet.Testcontainers.Images;
using Moq;

namespace PacmanManager.RepoHost.Test.Containers;

[TestFixture]
public class SharedImageTests
{
    [Test]
    public async Task GetAsync_BuildsTheImageOnce_AcrossConcurrentCallers()
    {
        var image = new Mock<IFutureDockerImage>();
        var release = new TaskCompletionSource();
        image.Setup(i => i.CreateAsync(It.IsAny<CancellationToken>())).Returns(release.Task);
        var factoryCalls = 0;
        var subject = new SharedImage(() =>
        {
            Interlocked.Increment(ref factoryCalls);
            return image.Object;
        });

        var callers = Enumerable.Range(0, 8).Select(_ => Task.Run(subject.GetAsync)).ToArray();
        release.SetResult();
        var results = await Task.WhenAll(callers);

        Assert.Multiple(() =>
        {
            Assert.That(factoryCalls, Is.EqualTo(1));
            image.Verify(i => i.CreateAsync(It.IsAny<CancellationToken>()), Times.Once);
            Assert.That(results, Is.All.SameAs(image.Object));
        });
    }

    [Test]
    public async Task GetAsync_DoesNotRebuild_OnceBuilt()
    {
        var image = new Mock<IFutureDockerImage>();
        image.Setup(i => i.CreateAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        var subject = new SharedImage(() => image.Object);

        await subject.GetAsync();
        await subject.GetAsync();

        image.Verify(i => i.CreateAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public void GetAsync_ReportsTheSameFailure_WithoutRetrying()
    {
        var image = new Mock<IFutureDockerImage>();
        var failure = new InvalidOperationException("build failed");
        image.Setup(i => i.CreateAsync(It.IsAny<CancellationToken>())).ThrowsAsync(failure);
        var subject = new SharedImage(() => image.Object);

        Assert.Multiple(() =>
        {
            Assert.That(subject.GetAsync, Throws.Exception.SameAs(failure));
            Assert.That(subject.GetAsync, Throws.Exception.SameAs(failure));
            image.Verify(i => i.CreateAsync(It.IsAny<CancellationToken>()), Times.Once);
        });
    }
}
