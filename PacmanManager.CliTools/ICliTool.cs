namespace PacmanManager.CliTools;

/// <summary>
/// Interface representing a command-line tool with its name, executable, and arguments.
/// </summary>
public interface ICliTool
{
    /// <summary>
    /// Friendly name of the CLI tool.
    /// </summary>
    string Name { get; }
    
    /// <summary>
    /// Executable path of the CLI tool.
    /// </summary>
    string Executable { get; }
    
    /// <summary>
    /// Arguments to be passed to the CLI tool.
    /// </summary>
    IEnumerable<string> Arguments { get; }
    
    /// <summary>
    /// Gets the working directory for the CLI tool execution. If empty, the current directory is used.
    /// </summary>
    string WorkingDirectory { get; }
}