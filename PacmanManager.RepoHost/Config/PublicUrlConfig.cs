namespace PacmanManager.RepoHost.Config;

/// <summary>
/// The absolute URL a <c>pacman</c> client reaches RepoHost at, from which every repository's
/// <see cref="Models.Repository.ServerUrl"/> is built.
/// </summary>
/// <remarks>
/// Bound from the top-level <c>PublicUrl</c> key rather than a section of its own. Absent or empty, the
/// current request's root is used instead; anything that is not an absolute <c>http</c> or
/// <c>https</c> URL fails startup.
/// </remarks>
public class PublicUrlConfig
{
    /// <summary>
    /// The public URL, such as <c>https://packages.example.com/</c>, or empty to use the request's
    /// root.
    /// </summary>
    public string? PublicUrl { get; set; }

    /// <summary>
    /// Whether <see cref="PublicUrl"/> is set, as opposed to absent or empty.
    /// </summary>
    public bool IsConfigured => !string.IsNullOrEmpty(PublicUrl);

    /// <summary>
    /// Whether <paramref name="value"/> may be used as a <see cref="PublicUrl"/>: an absolute
    /// <c>http</c> or <c>https</c> URL.
    /// </summary>
    /// <param name="value">The configured value.</param>
    /// <returns><c>true</c> if the value is an absolute <c>http</c> or <c>https</c> URL.</returns>
    public static bool IsValidPublicUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
