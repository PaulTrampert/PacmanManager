namespace PacmanManager.RepoHost.Models;

/// <summary>
/// A file a repository serves to a <c>pacman</c> client, together with when it was last written.
/// </summary>
/// <remarks>
/// <para>
/// The modification time is what lets the route answer a conditional request: libalpm sends
/// <c>If-Modified-Since</c> on every <c>pacman -Sy</c>, and without it every sync would re-download
/// every database in full. It comes from <see cref="Infrastructure.IFileSystem.GetLastWriteTimeUtc"/>,
/// not from the stream, so a mocked file system is enough to exercise that path.
/// </para>
/// <para>
/// The stream is open and owned by the caller, who is expected to dispose it. In the API that is
/// <see cref="Microsoft.AspNetCore.Mvc.FileStreamResult"/>, which streams it to the client and
/// disposes it afterwards.
/// </para>
/// </remarks>
/// <param name="Content">An open, readable stream over the file.</param>
/// <param name="LastModified">When the file was last written.</param>
public record RepositoryFile(Stream Content, DateTimeOffset LastModified);
