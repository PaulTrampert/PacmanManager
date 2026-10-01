using System.Globalization;
using System.Net.Mime;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using PacmanManager.RepoHost.Authentication;
using PacmanManager.RepoHost.Models;
using PacmanManager.RepoHost.Services;

namespace PacmanManager.RepoHost.Controllers;

/// <summary>
/// Serves a hosted repository to a <c>pacman</c> client: its databases and its package files, under
/// the one base URL a <c>Server = …/pacman/$repo/$arch</c> line names.
/// </summary>
/// <remarks>
/// <para>
/// The route is outside <c>/api</c> and unversioned, because it lives in <c>pacman.conf</c> files on
/// machines nobody here administers. <see cref="ApiVersionNeutralAttribute"/> takes it out of
/// versioning altogether, and <see cref="ApiExplorerSettingsAttribute.IgnoreApi"/> keeps it out of
/// every Swagger document.
/// </para>
/// <para>
/// The action is <see cref="AllowAnonymousAttribute"/> and names no scheme: a public repository is
/// served to a client with no credentials, a private one to its owner's Basic credential, and the
/// <c>Authorization</c> header's prefix has already picked the handler before endpoint metadata is
/// consulted. A credential that was offered and refused is a <c>401</c> before this controller runs.
/// </para>
/// <para>
/// An anonymous request for a repository it cannot see — private, or not there at all — is
/// challenged with a <c>401</c> and <c>WWW-Authenticate: Basic</c> rather than answered <c>404</c>.
/// pacman sends the credentials in its <c>Server</c> URL only after such a challenge, so without it a
/// private repository could never be reached. The two cases get the same challenge, so a private
/// repository is still indistinguishable from a missing one. An authenticated caller is not
/// challenged: it has already presented its credential, and gets the <c>404</c>.
/// </para>
/// <para>
/// There is deliberately no <c>Cache-Control</c>; libalpm's <c>If-Modified-Since</c> answered with a
/// <c>304</c> is what keeps a sync cheap. <c>Vary: Authorization</c> is sent on every response,
/// since the same URL is a <c>401</c> anonymously and a <c>200</c> for the owner.
/// </para>
/// </remarks>
[ApiController]
[ApiVersionNeutral]
// A version-neutral action is described in every version's API Explorer group, so neutrality alone
// would put these routes into the v1 Swagger document. Ignoring them is what keeps them out of all.
[ApiExplorerSettings(IgnoreApi = true)]
[Route(Route)]
public class PacmanController(IPacmanRepoService pacmanRepoService, ILogger<PacmanController> logger) : ControllerBase
{
    /// <summary>
    /// The route template: pacman's <c>$repo</c> and <c>$arch</c>, then the requested file name.
    /// </summary>
    public const string Route = "/pacman/{repoName}/{repoArch}/{fileName}";

    /// <summary>
    /// Get a repository database or package file.
    /// </summary>
    /// <param name="repoName">The repository's name, pacman's <c>$repo</c>.</param>
    /// <param name="repoArch">The architecture, pacman's <c>$arch</c>.</param>
    /// <param name="fileName">
    /// <c>{repoName}.db</c>, <c>{repoName}.files</c>, either with <c>.tar.gz</c> appended, or the name
    /// of a package file.
    /// </param>
    /// <param name="ct">Cancellation Token</param>
    /// <returns>
    /// The file as <c>application/octet-stream</c>, a <c>304</c> or <c>206</c> where the request's
    /// preconditions or range call for one; a <c>401</c> Basic challenge when an anonymous caller asks
    /// for a repository it cannot see; or <c>404</c> when there is otherwise no such file the caller
    /// may see.
    /// </returns>
    [HttpGet]
    [HttpHead]
    [AllowAnonymous]
    public async Task<IActionResult> Get(string repoName, string repoArch, string fileName, CancellationToken ct = default)
    {
        logger.LogInformation("Resolving {FileName} in {RepoName}/{RepoArch}", fileName, repoName, repoArch);

        // Set before anything can answer, so that the 401 and the 404 carry it as well as the file.
        Response.Headers.Append(HeaderNames.Vary, HeaderNames.Authorization);

        var resolution = await pacmanRepoService.ResolveAsync(repoName, repoArch, fileName, ct);
        if (resolution.File is not { } file)
        {
            if (!resolution.RepositoryFound && User.Identity?.IsAuthenticated != true)
            {
                // pacman offers the credentials in its Server URL only when challenged.
                logger.LogInformation("Challenging an anonymous request for {RepoName}", repoName);
                return Challenge(AuthnConstants.BasicScheme);
            }

            return NotFound();
        }

        // The requested name goes out as Content-Disposition. pacman ignores it and names the file
        // from the URL, and a browser saves the file under that name.
        return File(
            file.Content,
            MediaTypeNames.Application.Octet,
            fileName,
            file.LastModified,
            CreateEntityTag(file),
            enableRangeProcessing: true);
    }

    /// <summary>
    /// Cheaply derives an entity tag from the file's modification time and length, which change
    /// whenever its content does.
    /// </summary>
    /// <remarks>
    /// The length needs a seekable stream, which range processing needs anyway and which
    /// <see cref="Infrastructure.IFileSystem.OpenRead"/> provides.
    /// </remarks>
    /// <param name="file">The resolved file.</param>
    /// <returns>A strong entity tag.</returns>
    internal static EntityTagHeaderValue CreateEntityTag(RepositoryFile file) =>
        new(string.Create(CultureInfo.InvariantCulture,
            $"\"{file.LastModified.UtcTicks:x}-{file.Content.Length:x}\""));
}
