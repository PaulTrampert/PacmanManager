namespace PacmanManager.CliTools;

/// <summary>
/// Represents a registry for command-line interface (CLI) output handlers that should be used for all CLI tools.
/// </summary>
public interface ICliOutputHandlerRegistry
{
    /// <summary>
    /// Retrieves all global CLI output handlers.
    /// </summary>
    /// <returns>A list of <see cref="ICliOutputHandler"/></returns>
    IEnumerable<ICliOutputHandler> GetGlobalOutputHandlers();
}