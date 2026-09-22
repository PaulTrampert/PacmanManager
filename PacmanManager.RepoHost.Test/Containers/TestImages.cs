using DotNet.Testcontainers.Builders;
using PacmanManager.TestUtils;

namespace PacmanManager.RepoHost.Test.Containers;

/// <summary>
/// The images built from this solution's Dockerfiles, shared by every fixture in the assembly.
/// </summary>
/// <remarks>
/// <para>
/// Each image is built once per test run rather than once per fixture. The build context is the
/// same for every fixture, so a rebuild was only ever a layer-cache hit -- but it still tarred the
/// whole solution root and shipped it to the daemon each time, and a cold or evicted cache turned
/// each of those into a full build.
/// </para>
/// <para>
/// The Dockerfile directory is the solution root rather than the project directory, and the
/// project directory rides along in the Dockerfile path instead. That is what makes the root
/// .dockerignore apply: Testcontainers looks for the ignore file next to the *Dockerfile
/// directory* -- "&lt;dockerfile&gt;.dockerignore", falling back to ".dockerignore" there -- and
/// never reads the context root's. Point the Dockerfile directory at a project and the root ignore
/// file is silently skipped, so the host's bin/ and obj/ are tarred into the context; obj/ records
/// absolute host paths (the sources ANTLR generates for LibAlpmSharp among them) and
/// <c>dotnet build</c> inside the image then fails with CS2001. That only bites once a local build
/// has populated obj/, which is always true in CI.
/// </para>
/// <para>
/// The images are not cleaned up with the fixture that happened to build them, since later
/// fixtures still need them.
/// </para>
/// </remarks>
public static class TestImages
{
    /// <summary>
    /// The PacmanManager.RepoHost API image.
    /// </summary>
    public static SharedImage RepoHost { get; } =
        FromDockerfile("PacmanManager.RepoHost/Dockerfile", "pacmanmanager-repohost-test:latest");

    /// <summary>
    /// The PacmanManager.Migrations image, which applies the migrations and exits.
    /// </summary>
    public static SharedImage Migrations { get; } =
        FromDockerfile("PacmanManager.Migrations/Dockerfile", "pacmanmanager-migrations-test:latest");

    private static SharedImage FromDockerfile(string dockerfile, string name) => new(() =>
    {
        var solutionDirectory = DirUtils.FindSolutionDirectory();
        return new ImageFromDockerfileBuilder()
            .WithContextDirectory(solutionDirectory)
            .WithDockerfileDirectory(solutionDirectory)
            .WithDockerfile(dockerfile)
            .WithName(name)
            .WithCleanUp(false)
            .WithLogger(new TestOutputLogger(nameof(TestImages)))
            .Build();
    });
}
