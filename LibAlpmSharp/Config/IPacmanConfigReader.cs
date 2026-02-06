namespace LibAlpmSharp.Config;

public interface IPacmanConfigReader
{
    PacmanConfig ReadConfig(string filePath);
    PacmanConfig ReadConfigFromString(string configContent, string fileName);
}