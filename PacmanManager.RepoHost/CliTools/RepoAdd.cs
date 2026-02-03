using PacmanManager.CliTools;

namespace PacmanManager.RepoHost.CliTools;

public class RepoAdd(string name, string repoHome) : ICliTool
{
    public string Name => "repo-add";
    public string Executable => "repo-add";
    public IEnumerable<string> Arguments => [$"{name}.db.tar.gz"];
    public string WorkingDirectory => $"{repoHome}/sync";
}