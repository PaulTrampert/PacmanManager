namespace PacmanManager.Entities;

/// <summary>
/// Explicit names for the machine architectures pacman knows about, so that code needing to name
/// one directly -- not just validate against the allowed set -- has a single place to get it from.
/// </summary>
public static class Architectures
{
    /// <summary>
    /// The 64-bit x86 architecture.
    /// </summary>
    public const string X86_64 = "x86_64";

    /// <summary>
    /// The architecture-independent marker pacman uses for a package built with nothing
    /// architecture specific in it. Describes a package, never a repository.
    /// </summary>
    public const string Any = "any";

    /// <summary>
    /// Every architecture name this codebase knows about, <see cref="Any"/> included.
    /// </summary>
    public static readonly IEnumerable<string> All = [X86_64, Any];
}
