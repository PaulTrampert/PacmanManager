using Antlr4.Runtime;
using LibAlpmSharp.Config.Listeners;
using LibAlpmSharp.Config.TokenSources;
using LibAlpmSharp.Config.Visitors;
using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Config;

public class PacmanConfigReader(ILogger<PacmanConfigReader> logger) : IPacmanConfigReader
{
    public PacmanConfig ReadConfig(string filePath)
    {
        logger.LogInformation("Reading Pacman configuration from {FilePath}", filePath);
        var fullPath = Path.GetFullPath(filePath);
        var tokenStream = new AntlrFileStream(fullPath);
        return ReadConfig(tokenStream);
    }

    public PacmanConfig ReadConfigFromString(string configContent, string fileName)
    {
        logger.LogInformation("Reading Pacman configuration from string using fake filename {FileName}", fileName);

        // Use the base directory as the "source name" for Include resolution
        var inputStream = new AntlrInputStream(configContent)
        {
            name = fileName
        };
        return ReadConfig(inputStream);
    }

    private PacmanConfig ReadConfig(ICharStream inputStream)
    {
        var lexer = new PacmanConfLexer(inputStream);
        var tokens = new CommonTokenStream(new PacmanConfigTokenSource(logger, lexer));
        var parser = new PacmanConfParser(tokens);
        parser.ErrorHandler = new BailErrorStrategy();
        var errorListener = new PacmanConfigErrorListener(logger);
        parser.AddErrorListener(errorListener);
        var parseTree = parser.pacmanConf();
        var visitor = new PacmanConfigVisitor(logger);
        return visitor.Visit(parseTree);
    }
}