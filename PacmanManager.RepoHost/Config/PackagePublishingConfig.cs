namespace PacmanManager.RepoHost.Config;

/// <summary>
/// Configurable limits on publishing a package.
/// </summary>
public class PackagePublishingConfig
{
    /// <summary>
    /// Config section to find this object in.
    /// </summary>
    public const string Section = "PackagePublishing";

    /// <summary>
    /// The default upload ceiling, in bytes. One gibibyte, which is well above any real package and
    /// well below anything that would fill a disk on its own.
    /// </summary>
    public const long DefaultMaxUploadBytes = 1024L * 1024 * 1024;

    /// <summary>
    /// The largest package file the publish route will accept, in bytes.
    /// </summary>
    /// <remarks>
    /// Kestrel's own default is 30 MB, which a package file routinely exceeds, so the publish route
    /// raises the per request limit to this value. It is a configurable ceiling rather than
    /// <c>DisableRequestSizeLimit</c> on purpose: an oversized upload has to fail as a <c>413</c>
    /// that names a limit, not as an out of disk somewhere further down.
    /// </remarks>
    public long MaxUploadBytes { get; set; } = DefaultMaxUploadBytes;
}
