using System.Text;
using LibAlpmSharp.Config;
using Microsoft.Extensions.Options;

namespace PacmanManager.RepoHost.Startup.LibAlpm;

public interface IPacmanConfigGenerator
{
    string GenerateConfigContent();
}

public partial class PacmanConfigGenerator(
    IPacmanConfigSerializer serializer,
    IOptions<PacmanConfigSettings> settings, 
    ILogger<PacmanConfigGenerator> logger) : IPacmanConfigGenerator
{
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
        
        var configContent = new StringBuilder(serializer.Serialize(baseConfig));
        configContent.AppendLine($"Include = {currentSettings.Include}");
        
        var result = configContent.ToString();
        LogGeneratedConfigContentNewlineContent(logger, result);
        
        return result;
    }

    [LoggerMessage(LogLevel.Debug, "Generating pacman config content for DataDir: {dataDir}")]
    static partial void LogGeneratingPacmanConfigContentForDatadirDatadir(ILogger<PacmanConfigGenerator> logger, string dataDir);

    [LoggerMessage(LogLevel.Information, "Generated config content:\n{content}")]
    static partial void LogGeneratedConfigContentNewlineContent(ILogger<PacmanConfigGenerator> logger, string content);
}
