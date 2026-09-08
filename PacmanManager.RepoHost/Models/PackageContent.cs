namespace PacmanManager.RepoHost.Models;

/// <summary>
/// A package's stored bytes, together with the name they are stored under.
/// </summary>
/// <remarks>
/// <para>
/// The stream alone is not enough to answer a download: the response has to carry a
/// <c>Content-Disposition</c> naming the file, and that name is the basename the publish derived
/// and recorded — <c>{name}-{version}-{architecture}.pkg.tar.{ext}</c> — not anything the request
/// spelled. Pairing the two here keeps the controller from having to look the package up a second
/// time to learn what to call it.
/// </para>
/// <para>
/// The stream is open and owned by the caller, who is expected to dispose it. In the API that is
/// <see cref="Microsoft.AspNetCore.Mvc.FileStreamResult"/>, which streams it to the client and
/// disposes it afterwards, so a package of any size is never buffered.
/// </para>
/// </remarks>
/// <param name="Content">An open, readable stream over the stored package file.</param>
/// <param name="FileName">The basename the package is stored under.</param>
public record PackageContent(Stream Content, string FileName);
