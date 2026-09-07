using PacmanManager.CliTools;

namespace PacmanManager.RepoHost.CliTools;

/// <summary>
/// Base class for the pacman database tools (<see cref="RepoAdd"/> and <see cref="RepoRemove"/>) that operate on a
/// repository's <c>.db.tar.gz</c> file. Every such tool takes the database file name as its first argument and runs
/// in the <c>sync</c> directory of the libalpm database home, which is where registered sync databases live.
/// </summary>
/// <param name="name">Name of the repository database, without extension. This is the repository id.</param>
/// <param name="repoHome">The libalpm database home directory. The tool runs in its <c>sync</c> subdirectory.</param>
public abstract class RepoDbTool(string name, string repoHome) : ICliTool
{
    /// <summary>
    /// Extension shared by every repository database file.
    /// </summary>
    public const string DatabaseExtension = ".db.tar.gz";

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Executable { get; }

    /// <summary>
    /// File name of the repository database the tool operates on, relative to <see cref="WorkingDirectory"/>.
    /// </summary>
    public string DatabaseFileName => $"{name}{DatabaseExtension}";

    /// <summary>
    /// The database file name, followed by the tool specific <see cref="OperandArguments"/>.
    /// </summary>
    public IEnumerable<string> Arguments => [DatabaseFileName, ..OperandArguments];

    /// <inheritdoc />
    public string WorkingDirectory => $"{repoHome}/sync";

    /// <summary>
    /// Arguments that follow the database file name: package file paths for <see cref="RepoAdd"/>, package names for
    /// <see cref="RepoRemove"/>.
    /// </summary>
    protected abstract IEnumerable<string> OperandArguments { get; }
}
