namespace PacmanManager.TestUtils;

/// <summary>
/// Locates the committed pacman package fixtures under <c>test-fixtures/packages</c> and pins the
/// metadata they carry, so that every test that loads one asserts against the same values.
/// </summary>
public static class PackageFixtures
{
    /// <summary>
    /// The directory holding the committed package fixtures.
    /// </summary>
    public static string Directory =>
        Path.Combine(DirUtils.FindSolutionDirectory(), "test-fixtures", "packages");

    /// <summary>
    /// The file name of the minimal fixture package.
    /// </summary>
    public const string MinimalPackageFileName = "pacmanmanager-test-1.2.3-4-x86_64.pkg.tar.zst";

    /// <summary>
    /// The package name reported by the minimal fixture package.
    /// </summary>
    public const string MinimalPackageName = "pacmanmanager-test";

    /// <summary>
    /// The full version reported by the minimal fixture package.
    /// </summary>
    public const string MinimalPackageVersion = "1.2.3-4";

    /// <summary>
    /// The architecture reported by the minimal fixture package.
    /// </summary>
    public const string MinimalPackageArchitecture = "x86_64";

    /// <summary>
    /// The description reported by the minimal fixture package.
    /// </summary>
    public const string MinimalPackageDescription = "Fixture package used by PacmanManager tests";

    /// <summary>
    /// The <c>pkgbase</c> reported by the minimal fixture package.
    /// </summary>
    public const string MinimalPackageBase = "pacmanmanager-test";

    /// <summary>
    /// The upstream URL reported by the minimal fixture package.
    /// </summary>
    public const string MinimalPackageUrl = "https://github.com/PaulTrampert/PacmanManager";

    /// <summary>
    /// The packager reported by the minimal fixture package.
    /// </summary>
    public const string MinimalPackagePackager = "Paul Trampert <paul.trampert@gmail.com>";

    /// <summary>
    /// The installed size, in bytes, reported by the minimal fixture package.
    /// </summary>
    public const long MinimalPackageInstalledSize = 35;

    /// <summary>
    /// The build timestamp reported by the minimal fixture package, as seconds since the Unix epoch.
    /// The fixture is built with a fixed timestamp so that the archive is reproducible.
    /// </summary>
    public const long MinimalPackageBuildDateUnixSeconds = 1757116800;

    /// <summary>
    /// The single license declared by the minimal fixture package.
    /// </summary>
    public const string MinimalPackageLicense = "MIT";

    /// <summary>
    /// The single group the minimal fixture package belongs to.
    /// </summary>
    public const string MinimalPackageGroup = "pacmanmanager";

    /// <summary>
    /// The single dependency declared by the minimal fixture package.
    /// </summary>
    public const string MinimalPackageDepend = "glibc";

    /// <summary>
    /// The single optional dependency declared by the minimal fixture package, in pacman's
    /// <c>name: reason</c> spelling.
    /// </summary>
    public const string MinimalPackageOptDepend = "git: for the sync command";

    /// <summary>
    /// The single make dependency declared by the minimal fixture package.
    /// </summary>
    public const string MinimalPackageMakeDepend = "cmake";

    /// <summary>
    /// The single check dependency declared by the minimal fixture package.
    /// </summary>
    public const string MinimalPackageCheckDepend = "python";

    /// <summary>
    /// The single provision declared by the minimal fixture package.
    /// </summary>
    public const string MinimalPackageProvides = "pacmanmanager-fixture";

    /// <summary>
    /// The single conflict declared by the minimal fixture package.
    /// </summary>
    public const string MinimalPackageConflict = "pacmanmanager-test-old";

    /// <summary>
    /// The single package the minimal fixture package replaces.
    /// </summary>
    public const string MinimalPackageReplaces = "pacmanmanager-test-legacy";

    /// <summary>
    /// The absolute path to the minimal fixture package.
    /// </summary>
    public static string MinimalPackagePath => Path.Combine(Directory, MinimalPackageFileName);
}
