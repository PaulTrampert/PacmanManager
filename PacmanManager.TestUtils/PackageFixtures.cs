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
    /// The absolute path to the minimal fixture package.
    /// </summary>
    public static string MinimalPackagePath => Path.Combine(Directory, MinimalPackageFileName);
}
