namespace PacmanManager.CliTools.Test;

/// <summary>
/// A tool that runs a shell fragment, so a test can ask for a particular exit code and a
/// particular thing written about it.
/// </summary>
public class ShellCliTool(string name, string script) : ICliTool
{
    public string Name => name;
    public string Executable => "sh";
    public IEnumerable<string> Arguments => ["-c", script];
    public string WorkingDirectory => string.Empty;
}
