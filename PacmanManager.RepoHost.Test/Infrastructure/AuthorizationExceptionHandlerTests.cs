using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PacmanManager.RepoHost.Exceptions;
using PacmanManager.RepoHost.Infrastructure;

namespace PacmanManager.RepoHost.Test.Infrastructure;

/// <summary>
/// Covers the mapping from the authorization exceptions the service layer raises to HTTP status
/// codes. The handler switches on exception type and leaves everything else unhandled, so an
/// exception without an arm would fall through to a 500.
/// </summary>
[TestFixture]
public class AuthorizationExceptionHandlerTests
{
    private Mock<IProblemDetailsService> _problemDetailsService = null!;
    private AuthorizationExceptionHandler _subject = null!;
    private ProblemDetailsContext? _written;

    [SetUp]
    public void SetUp()
    {
        _written = null;
        _problemDetailsService = new Mock<IProblemDetailsService>();
        _problemDetailsService
            .Setup(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()))
            .Callback<ProblemDetailsContext>(context => _written = context)
            .ReturnsAsync(true);
        _subject = new AuthorizationExceptionHandler(_problemDetailsService.Object);
    }

    [Test]
    public async Task TryHandleAsync_PackageForbiddenException_Writes403()
    {
        var httpContext = new DefaultHttpContext();
        var repositoryId = Guid.CreateVersion7();

        var handled = await _subject.TryHandleAsync(
            httpContext,
            new PackageForbiddenException(repositoryId),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(httpContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(_written?.ProblemDetails.Status, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(_written?.ProblemDetails.Title, Is.EqualTo("You may not publish to this repository."));
        });
    }

    [Test]
    public async Task TryHandleAsync_RepositoryForbiddenException_Writes403()
    {
        var httpContext = new DefaultHttpContext();

        var handled = await _subject.TryHandleAsync(
            httpContext,
            new RepositoryForbiddenException(Guid.CreateVersion7()),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(httpContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status403Forbidden));
            Assert.That(_written?.ProblemDetails.Title, Is.EqualTo("You do not own this repository."));
        });
    }

    [Test]
    public async Task TryHandleAsync_NoCurrentUserException_Writes401()
    {
        var httpContext = new DefaultHttpContext();

        var handled = await _subject.TryHandleAsync(httpContext, new NoCurrentUserException(), CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.True);
            Assert.That(httpContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status401Unauthorized));
        });
    }

    [Test]
    public async Task TryHandleAsync_UnrelatedException_IsLeftUnhandled()
    {
        var httpContext = new DefaultHttpContext();

        var handled = await _subject.TryHandleAsync(
            httpContext,
            new InvalidOperationException("something else"),
            CancellationToken.None);

        Assert.Multiple(() =>
        {
            Assert.That(handled, Is.False);
            Assert.That(httpContext.Response.StatusCode, Is.EqualTo(StatusCodes.Status200OK));
        });
        _problemDetailsService.Verify(s => s.TryWriteAsync(It.IsAny<ProblemDetailsContext>()), Times.Never);
    }
}
