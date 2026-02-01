namespace PacmanManager.CliTools.Test;

public class EchoCliTool(params string[] arguments) : ICliTool
{
    public string Name => "echo";
    public string Executable => "echo";
    public IEnumerable<string> Arguments => arguments;
    public string WorkingDirectory => string.Empty;
}