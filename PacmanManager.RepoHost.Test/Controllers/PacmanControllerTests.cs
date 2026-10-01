using System.Reflection;
using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Controllers;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Test.Controllers;

/// <summary>
/// Unit tests of the result <see cref="PacmanController"/> builds. What the result then does on the
/// wire — the <c>304</c>, the <c>206</c>, <c>Content-Length</c> — is ASP.NET's, and is asserted
/// end to end in <c>PacmanRoutesTests</c>.
/// </summary>
[TestFixture]
public class PacmanControllerTests
{
    private static readonly DateTimeOffset LastModified = new(2026, 9, 30, 12, 34, 56, 789, TimeSpan.Zero);

    private Mock<IPacmanRepoService> _service = null!;
    private PacmanController _subject = null!;

    [SetUp]
    public void SetUp()
    {
        _service = new Mock<IPacmanRepoService>();
        GivenAResolution(PacmanResolution.FileNotFound);
        _subject = new PacmanController(_service.Object, NullLogger<PacmanController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() },
        };
    }

    [Test]
    public async Task Get_ReturnsNotFound_ForAMissingFileInAVisibleRepository_EvenAnonymously()
    {
        var result = await _subject.Get("repo", "x86_64", "repo.db");

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    /// <summary>
    /// pacman sends the credentials in its <c>Server</c> URL only once challenged, so an anonymous
    /// request for a repository it cannot see — private or missing, which resolve alike — has to be
    /// a Basic challenge rather than a <c>404</c>.
    /// </summary>
    [Test]
    public async Task Get_ChallengesWithBasic_WhenAnAnonymousCallerCannotSeeTheRepository()
    {
        GivenAResolution(PacmanResolution.RepositoryNotFound);

        var result = await _subject.Get("repo", "x86_64", "repo.db");

        Assert.That(result, Is.InstanceOf<ChallengeResult>());
        Assert.That(((ChallengeResult)result).AuthenticationSchemes, Is.EqualTo(new[] { AuthnConstants.BasicScheme }));
    }

    /// <summary>
    /// A caller that has already authenticated has nothing more to offer, so a repository it may not
    /// see stays a <c>404</c>.
    /// </summary>
    [TestCase(AuthnConstants.BasicScheme)]
    [TestCase("Bearer")]
    public async Task Get_ReturnsNotFound_WhenAnAuthenticatedCallerCannotSeeTheRepository(string scheme)
    {
        GivenAResolution(PacmanResolution.RepositoryNotFound);
        _subject.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(AuthnConstants.AppUserIdClaimType, Guid.NewGuid().ToString())], scheme));

        var result = await _subject.Get("repo", "x86_64", "repo.db");

        Assert.That(result, Is.InstanceOf<NotFoundResult>());
    }

    [Test]
    public async Task Get_SetsVaryAuthorization_OnANotFound()
    {
        await _subject.Get("repo", "x86_64", "repo.db");

        Assert.That(_subject.Response.Headers.Vary.ToString(), Is.EqualTo("Authorization"));
    }

    [Test]
    public async Task Get_SetsVaryAuthorization_OnAChallenge()
    {
        GivenAResolution(PacmanResolution.RepositoryNotFound);

        await _subject.Get("repo", "x86_64", "repo.db");

        Assert.That(_subject.Response.Headers.Vary.ToString(), Is.EqualTo("Authorization"));
    }

    [Test]
    public async Task Get_SetsVaryAuthorization_OnAFile()
    {
        GivenAFile(new byte[] { 1, 2, 3 });

        await _subject.Get("repo", "x86_64", "repo.db");

        Assert.That(_subject.Response.Headers.Vary.ToString(), Is.EqualTo("Authorization"));
    }

    [Test]
    public async Task Get_PassesTheRequestedSegmentsToTheService()
    {
        await _subject.Get("repo", "x86_64", "pkg-1-1-any.pkg.tar.zst");

        _service.Verify(s => s.ResolveAsync("repo", "x86_64", "pkg-1-1-any.pkg.tar.zst", It.IsAny<CancellationToken>()));
    }

    [Test]
    public async Task Get_ServesTheResolvedStream_AsAnOctetStreamWithRangesAndTheRequestedNameAsDownloadName()
    {
        var content = GivenAFile(new byte[] { 1, 2, 3 });

        var result = await _subject.Get("repo", "x86_64", "repo.db");

        Assert.That(result, Is.InstanceOf<FileStreamResult>());
        var file = (FileStreamResult)result;
        Assert.Multiple(() =>
        {
            Assert.That(file.FileStream, Is.SameAs(content));
            Assert.That(file.ContentType, Is.EqualTo("application/octet-stream"));
            Assert.That(file.LastModified, Is.EqualTo(LastModified));
            Assert.That(file.EntityTag, Is.Not.Null);
            Assert.That(file.EnableRangeProcessing, Is.True);
            Assert.That(file.FileDownloadName, Is.EqualTo("repo.db"));
        });
    }

    [Test]
    public void CreateEntityTag_IsStrongAndQuoted()
    {
        var tag = PacmanController.CreateEntityTag(new RepositoryFile(new MemoryStream([1, 2, 3]), LastModified));

        Assert.Multiple(() =>
        {
            Assert.That(tag.IsWeak, Is.False);
            Assert.That(tag.Tag.ToString(), Does.StartWith("\"").And.EndWith("\""));
        });
    }

    [Test]
    public void CreateEntityTag_IsStableForTheSameTimeAndLength()
    {
        var first = PacmanController.CreateEntityTag(new RepositoryFile(new MemoryStream([1, 2, 3]), LastModified));
        var second = PacmanController.CreateEntityTag(new RepositoryFile(new MemoryStream([4, 5, 6]), LastModified));

        Assert.That(first.Tag, Is.EqualTo(second.Tag));
    }

    [Test]
    public void CreateEntityTag_ChangesWithTheModificationTime()
    {
        var first = PacmanController.CreateEntityTag(new RepositoryFile(new MemoryStream([1, 2, 3]), LastModified));
        var second = PacmanController.CreateEntityTag(
            new RepositoryFile(new MemoryStream([1, 2, 3]), LastModified.AddMilliseconds(1)));

        Assert.That(first.Tag, Is.Not.EqualTo(second.Tag));
    }

    [Test]
    public void CreateEntityTag_ChangesWithTheLength()
    {
        var first = PacmanController.CreateEntityTag(new RepositoryFile(new MemoryStream([1, 2, 3]), LastModified));
        var second = PacmanController.CreateEntityTag(new RepositoryFile(new MemoryStream([1, 2, 3, 4]), LastModified));

        Assert.That(first.Tag, Is.Not.EqualTo(second.Tag));
    }

    [Test]
    public void Controller_IsVersionNeutral_AndIgnoredByTheApiExplorer()
    {
        var type = typeof(PacmanController);

        Assert.Multiple(() =>
        {
            Assert.That(type.GetCustomAttribute<ApiVersionNeutralAttribute>(), Is.Not.Null);
            Assert.That(type.GetCustomAttribute<ApiExplorerSettingsAttribute>()?.IgnoreApi, Is.True);
        });
    }

    [Test]
    public void Get_IsAnonymous_AndNamesNoScheme()
    {
        var method = typeof(PacmanController).GetMethod(nameof(PacmanController.Get))!;

        Assert.Multiple(() =>
        {
            Assert.That(method.GetCustomAttribute<AllowAnonymousAttribute>(), Is.Not.Null);
            Assert.That(method.GetCustomAttributes<AuthorizeAttribute>(), Is.Empty);
            Assert.That(typeof(PacmanController).GetCustomAttributes<AuthorizeAttribute>(), Is.Empty);
        });
    }

    private Stream GivenAFile(byte[] bytes)
    {
        var content = new MemoryStream(bytes);
        GivenAResolution(PacmanResolution.Of(new RepositoryFile(content, LastModified)));
        return content;
    }

    private void GivenAResolution(PacmanResolution resolution) =>
        _service
            .Setup(s => s.ResolveAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(resolution);
}
