namespace PacmanManager.RepoHost.CliTools;

/// <summary>
/// Runs <c>repo-remove</c> against a repository's database, removing packages from it by name. This drops the packages'
/// entries from the database; it does not delete the package files themselves.
/// </summary>
public class RepoRemove : RepoDbTool
{
    private readonly IEnumerable<string> _packageNames;

    /// <summary>
    /// Creates the tool for the given repository database.
    /// </summary>
    /// <param name="name">Name of the repository database, without extension. This is the repository id.</param>
    /// <param name="repoHome">The libalpm database home directory. The tool runs in its <c>sync</c> subdirectory.</param>
    /// <param name="packageNames">Names of the packages to remove. At least one is required.</param>
    /// <exception cref="ArgumentException">Thrown when no package names are supplied.</exception>
    public RepoRemove(string name, string repoHome, params IEnumerable<string> packageNames) : base(name, repoHome)
    {
        _packageNames = packageNames.ToArray();
        if (!_packageNames.Any())
        {
            throw new ArgumentException("At least one package name is required.", nameof(packageNames));
        }
    }

    /// <inheritdoc />
    public override string Name => "repo-remove";

    /// <inheritdoc />
    public override string Executable => "repo-remove";

    /// <inheritdoc />
    protected override IEnumerable<string> OperandArguments => _packageNames;
}
