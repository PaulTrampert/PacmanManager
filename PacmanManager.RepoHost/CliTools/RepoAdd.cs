using PacmanManager.CliTools;

namespace PacmanManager.RepoHost.CliTools;

/// <summary>
/// Runs <c>repo-add</c> against a repository's database, adding package files to it. Invoked with no package files it
/// simply creates an empty database, which is how a new repository is initialised.
/// </summary>
public class RepoAdd : ICliTool
{
    private readonly RepositoryDatabase _database;
    private readonly IEnumerable<string> _packageFilePaths;

    /// <summary>
    /// Creates the tool for the given repository database.
    /// </summary>
    /// <param name="database">The database to change. The tool runs in its <see cref="RepositoryDatabase.DatabaseDirectory"/>.</param>
    /// <param name="packageFilePaths">
    /// Absolute paths of the package files to add. Package files live outside the working directory, so relative paths
    /// are not meaningful here. When empty, <c>repo-add</c> creates an empty database.
    /// </param>
    public RepoAdd(RepositoryDatabase database, params IEnumerable<string> packageFilePaths)
    {
        _database = database;
        _packageFilePaths = packageFilePaths.ToArray();
    }

    /// <inheritdoc />
    public string Name => "repo-add";

    /// <inheritdoc />
    public string Executable => "repo-add";

    /// <inheritdoc />
    public IEnumerable<string> Arguments => [_database.FileName, .._packageFilePaths];

    /// <inheritdoc />
    public string WorkingDirectory => _database.DatabaseDirectory;
}
