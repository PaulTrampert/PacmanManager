using PacmanManager.CliTools;

namespace PacmanManager.RepoHost.CliTools;

/// <summary>
/// Runs <c>repo-remove</c> against a repository's database, removing packages from it by name. This drops the packages'
/// entries from the database; it does not delete the package files themselves.
/// </summary>
public class RepoRemove : ICliTool
{
    private readonly RepositoryDatabase _database;
    private readonly IEnumerable<string> _packageNames;

    /// <summary>
    /// Creates the tool for the given repository database.
    /// </summary>
    /// <param name="database">The database to change. The tool runs in its <see cref="RepositoryDatabase.DatabaseDirectory"/>.</param>
    /// <param name="packageNames">Names of the packages to remove. At least one is required.</param>
    /// <exception cref="ArgumentException">Thrown when no package names are supplied.</exception>
    public RepoRemove(RepositoryDatabase database, params IEnumerable<string> packageNames)
    {
        _database = database;
        _packageNames = packageNames.ToArray();
        if (!_packageNames.Any())
        {
            throw new ArgumentException("At least one package name is required.", nameof(packageNames));
        }
    }

    /// <inheritdoc />
    public string Name => "repo-remove";

    /// <inheritdoc />
    public string Executable => "repo-remove";

    /// <inheritdoc />
    public IEnumerable<string> Arguments => [_database.FileName, .._packageNames];

    /// <inheritdoc />
    public string WorkingDirectory => _database.DatabaseDirectory;
}
