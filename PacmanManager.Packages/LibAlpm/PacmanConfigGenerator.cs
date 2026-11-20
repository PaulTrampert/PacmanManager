using LibAlpmSharp.Config;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PacmanManager.Packages.LibAlpm;

/// <summary>
/// Produces the text of the <c>pacman.conf</c> libalpm is initialized from.
/// </summary>
public interface IPacmanConfigGenerator
{
    /// <summary>
    /// Generates the configuration for the current <see cref="PacmanConfigSettings"/>.
    /// </summary>
    /// <returns>The content of a <c>pacman.conf</c>.</returns>
    string GenerateConfigContent();
}

/// <summary>
/// The default <see cref="IPacmanConfigGenerator"/>: a configuration whose database, cache and log
/// paths all lie under <see cref="PacmanConfigSettings.DataDir"/>.
/// </summary>
/// <param name="serializer">Writes the configuration out as text.</param>
/// <param name="settings">Where under <c>DATA_DIR</c> libalpm keeps its files.</param>
/// <param name="logger">Logs the configuration generated.</param>
public partial class PacmanConfigGenerator(
    IPacmanConfigSerializer serializer,
    IOptions<PacmanConfigSettings> settings, 
    ILogger<PacmanConfigGenerator> logger) : IPacmanConfigGenerator
{
    /// <inheritdoc />
    public string GenerateConfigContent()
    {
        var currentSettings = settings.Value;
        LogGeneratingPacmanConfigContentForDatadirDatadir(logger, currentSettings.DataDir);
        
        var baseConfig = new PacmanConfig
        {
            CacheDir = currentSettings.CacheDir,
            DBPath = currentSettings.DbPath,
            LogFile = currentSettings.LogFile,
        };
        
        var result = serializer.Serialize(baseConfig);
        LogGeneratedConfigContentNewlineContent(logger, result);
        
        return result;
    }

    [LoggerMessage(LogLevel.Debug, "Generating pacman config content for DataDir: {dataDir}")]
    static partial void LogGeneratingPacmanConfigContentForDatadirDatadir(ILogger<PacmanConfigGenerator> logger, string dataDir);

    [LoggerMessage(LogLevel.Information, "Generated config content:\n{content}")]
    static partial void LogGeneratedConfigContentNewlineContent(ILogger<PacmanConfigGenerator> logger, string content);
}
