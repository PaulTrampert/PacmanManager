using System.Globalization;
using System.Text;

namespace PacmanManager.TestUtils;

/// <summary>
/// A pacman local package database seeded under a temporary root, so that a test reading installed
/// packages asserts against known fixture metadata rather than against whatever the host happens to
/// have installed.
/// </summary>
/// <remarks>
/// <para>
/// Without this, a test wanting an installed package has to read the host's own database at
/// <c>/var/lib/pacman</c>. That only carries anything on an Arch machine, and even there it pins the
/// assertions to whichever build of a system package the host is currently running. Seeding makes
/// the same tests deterministic and host independent, and gives libalpm a writable root, which it
/// needs before it will hand out a local database at all.
/// </para>
/// <para>
/// The entries are written directly rather than by shelling out to <c>pacman -U</c>, so the tests
/// need nothing beyond libalpm itself -- the same reason the package fixtures are committed rather
/// than built during the test run. A local database is a <c>local</c> directory under the database
/// path holding an <c>ALPM_DB_VERSION</c> file and one directory per installed package, named
/// <c>&lt;name&gt;-&lt;version&gt;</c> and holding a <c>desc</c> file of <c>%FIELD%</c> blocks. That
/// is the whole of the format this needs, and it has been stable across pacman 6 and 7.
/// </para>
/// <para>
/// Two packages are seeded: the committed fixture package, whose metadata
/// <see cref="PackageFixtures"/> pins, and a second package that depends on it, so that the reverse
/// dependency members have something to report.
/// </para>
/// </remarks>
public sealed class LocalPackageDatabase : IDisposable
{
    /// <summary>
    /// The on-disk format version written to <c>ALPM_DB_VERSION</c>. libalpm refuses to read a
    /// local database whose version it does not recognise, and 9 is what pacman 6 and 7 both write.
    /// </summary>
    public const string FormatVersion = "9";

    /// <summary>
    /// The install date recorded for every seeded package, as seconds since the Unix epoch. It is a
    /// fixed point after <see cref="PackageFixtures.MinimalPackageBuildDateUnixSeconds"/> -- one day
    /// on -- rather than the time the database was seeded, so that a test asserting on it is
    /// asserting on a constant.
    /// </summary>
    public const long InstallDateUnixSeconds =
        PackageFixtures.MinimalPackageBuildDateUnixSeconds + 24 * 60 * 60;

    /// <summary>
    /// The name of the second seeded package, which exists so that the fixture package is required
    /// by, and optional for, something.
    /// </summary>
    public const string DependentPackageName = "pacmanmanager-test-dependent";

    /// <summary>
    /// The full version of the second seeded package.
    /// </summary>
    public const string DependentPackageVersion = "2.0-1";

    /// <summary>
    /// The reason the second seeded package gives for optionally depending on the fixture package.
    /// </summary>
    public const string DependentPackageOptDependReason = "for the fixture metadata";

    private readonly string _directory;

    private LocalPackageDatabase(string directory, string root, string dbPath)
    {
        _directory = directory;
        Root = root;
        DbPath = dbPath;
    }

    /// <summary>
    /// The filesystem root to hand <see cref="M:LibAlpmSharp.LibAlpm.Initialize(System.String,System.String)"/>.
    /// </summary>
    public string Root { get; }

    /// <summary>
    /// The database path to hand <see cref="M:LibAlpmSharp.LibAlpm.Initialize(System.String,System.String)"/>.
    /// </summary>
    public string DbPath { get; }

    /// <summary>
    /// The names of the seeded packages.
    /// </summary>
    public static IReadOnlyList<string> PackageNames { get; } =
        [PackageFixtures.MinimalPackageName, DependentPackageName];

    /// <summary>
    /// Creates a temporary root and seeds a local database under it.
    /// </summary>
    /// <returns>The seeded database. Dispose it to delete the temporary root.</returns>
    public static LocalPackageDatabase Seed()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"pacmanmanager-localdb-{Guid.NewGuid():N}");
        var root = Path.Combine(directory, "root");
        var dbPath = Path.Combine(directory, "db");
        var localPath = Path.Combine(dbPath, "local");

        Directory.CreateDirectory(root);
        Directory.CreateDirectory(localPath);
        File.WriteAllText(Path.Combine(localPath, "ALPM_DB_VERSION"), $"{FormatVersion}\n");

        WriteEntry(
            localPath,
            PackageFixtures.MinimalPackageName,
            PackageFixtures.MinimalPackageVersion,
            [
                ("BASE", [PackageFixtures.MinimalPackageBase]),
                ("DESC", [PackageFixtures.MinimalPackageDescription]),
                ("URL", [PackageFixtures.MinimalPackageUrl]),
                ("ARCH", [PackageFixtures.MinimalPackageArchitecture]),
                ("BUILDDATE", [Number(PackageFixtures.MinimalPackageBuildDateUnixSeconds)]),
                ("INSTALLDATE", [Number(InstallDateUnixSeconds)]),
                ("PACKAGER", [PackageFixtures.MinimalPackagePackager]),
                ("SIZE", [Number(PackageFixtures.MinimalPackageInstalledSize)]),
                ("REASON", ["0"]),
                ("VALIDATION", ["none"]),
                ("LICENSE", [PackageFixtures.MinimalPackageLicense]),
                ("GROUPS", [PackageFixtures.MinimalPackageGroup]),
                ("DEPENDS", [PackageFixtures.MinimalPackageDepend]),
                ("OPTDEPENDS", [PackageFixtures.MinimalPackageOptDepend]),
                ("MAKEDEPENDS", [PackageFixtures.MinimalPackageMakeDepend]),
                ("CHECKDEPENDS", [PackageFixtures.MinimalPackageCheckDepend]),
                ("PROVIDES", [PackageFixtures.MinimalPackageProvides]),
                ("CONFLICTS", [PackageFixtures.MinimalPackageConflict]),
                ("REPLACES", [PackageFixtures.MinimalPackageReplaces]),
            ]);

        WriteEntry(
            localPath,
            DependentPackageName,
            DependentPackageVersion,
            [
                ("BASE", [DependentPackageName]),
                ("DESC", ["Fixture package that depends on the fixture package"]),
                ("ARCH", [PackageFixtures.MinimalPackageArchitecture]),
                ("BUILDDATE", [Number(PackageFixtures.MinimalPackageBuildDateUnixSeconds)]),
                ("INSTALLDATE", [Number(InstallDateUnixSeconds)]),
                ("SIZE", [Number(PackageFixtures.MinimalPackageInstalledSize)]),
                // Installed as a dependency, and depending on the fixture package both ways round,
                // which is what makes GetRequiredBy and GetOptionalFor report anything.
                ("REASON", ["1"]),
                ("VALIDATION", ["none"]),
                ("DEPENDS", [PackageFixtures.MinimalPackageName]),
                ("OPTDEPENDS",
                    [$"{PackageFixtures.MinimalPackageName}: {DependentPackageOptDependReason}"]),
            ]);

        return new LocalPackageDatabase(directory, root, dbPath);
    }

    /// <summary>
    /// Deletes the temporary root and everything seeded under it.
    /// </summary>
    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Writes one local database entry. <c>%NAME%</c> and <c>%VERSION%</c> always come first and
    /// always match the directory name, so they are taken from the arguments rather than repeated
    /// in every caller's field list.
    /// </summary>
    private static void WriteEntry(
        string localPath,
        string name,
        string version,
        (string Field, string[] Values)[] fields)
    {
        var entryPath = Path.Combine(localPath, $"{name}-{version}");
        Directory.CreateDirectory(entryPath);

        var desc = new StringBuilder();
        foreach (var (field, values) in
                 new[] { ("NAME", new[] { name }), ("VERSION", new[] { version }) }.Concat(fields))
        {
            // A field with no values is left out entirely, which is what pacman writes and what its
            // parser expects: a %FIELD% header with nothing under it would read as a blank value.
            if (values.Length == 0)
                continue;

            desc.Append('%').Append(field).Append("%\n");
            foreach (var value in values)
                desc.Append(value).Append('\n');
            desc.Append('\n');
        }

        File.WriteAllText(Path.Combine(entryPath, "desc"), desc.ToString());
    }
}
