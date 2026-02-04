using Microsoft.Extensions.Options;

namespace PacmanManager.RepoHost.Startup.LibAlpm;

public interface IPacmanConfigGenerator
{
    string GenerateConfigContent();
}

public class PacmanConfigGenerator : IPacmanConfigGenerator
{
    private readonly PacmanConfigSettings _settings;
    private readonly ILogger<PacmanConfigGenerator> _logger;

    public PacmanConfigGenerator(
        IOptions<PacmanConfigSettings> settings,
        ILogger<PacmanConfigGenerator> logger)
    {
        _settings = settings.Value;
        _logger = logger;
    }

    public string GenerateConfigContent()
    {
        _logger.LogInformation("Generating pacman config content for DataDir: {DataDir}", _settings.DataDir);
        
        var configContent = $"""
            [options]
            RootDir = /
            DBPath = {_settings.DbPath}
            CacheDir = {_settings.CacheDir}
            LogFile = {_settings.LogFile}

            Include = {_settings.Include}

            """;
        
        _logger.LogDebug("Generated config content:{NewLine}{Content}", Environment.NewLine, configContent);
        
        return configContent;
    }
}
