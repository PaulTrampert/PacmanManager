namespace PacmanManager.RepoHost.CliTools;

/// <summary>
/// Runs <c>repo-add</c> against a repository's database, adding package files to it. Invoked with no package files it
/// simply creates an empty database, which is how a new repository is initialised.
/// </summary>
public class RepoAdd : RepoDbTool
{
    private readonly IEnumerable<string> _packageFilePaths;

    /// <summary>
    /// Creates the tool for the given repository database.
    /// </summary>
    /// <param name="name">Name of the repository database, without extension. This is the repository id.</param>
    /// <param name="repoHome">The libalpm database home directory. The tool runs in its <c>sync</c> subdirectory.</param>
    /// <param name="packageFilePaths">
    /// Absolute paths of the package files to add. Package files live outside the working directory, so relative paths
    /// are not meaningful here. When empty, <c>repo-add</c> creates an empty database.
    /// </param>
    public RepoAdd(string name, string repoHome, params IEnumerable<string> packageFilePaths) : base(name, repoHome)
    {
        _packageFilePaths = packageFilePaths.ToArray();
    }

    /// <inheritdoc />
    public override string Name => "repo-add";

    /// <inheritdoc />
    public override string Executable => "repo-add";

    /// <inheritdoc />
    protected override IEnumerable<string> OperandArguments => _packageFilePaths;
}
