using Antlr4.Runtime;
using LibAlpmSharp.Config.Listeners;
using LibAlpmSharp.Config.TokenSources;
using LibAlpmSharp.Config.Visitors;
using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Config;

public class PacmanConfigReader(ILogger<PacmanConfigReader> logger)
{
    public PacmanConfig ReadConfig(string filePath)
    {
        logger.LogInformation("Reading Pacman configuration from {FilePath}", filePath);

        var tokenStream = new AntlrFileStream(filePath);
        var lexer = new PacmanConfLexer(tokenStream);
        var tokens = new CommonTokenStream(new PacmanConfigTokenSource(logger, lexer));
        var parser = new PacmanConfParser(tokens);
        parser.ErrorHandler = new BailErrorStrategy();
        var errorListener = new PacmanConfigErrorListener(logger);
        parser.AddErrorListener(errorListener);
        var parseTree = parser.pacmanConf();
        var visitor = new PacmanConfigVisitor(logger);
        return visitor.Visit(parseTree);
    }

    public PacmanConfig ReadConfigFromString(string configContent, string baseDirectory)
    {
        logger.LogInformation("Reading Pacman configuration from string with base directory {BaseDirectory}", baseDirectory);

        // Use the base directory as the "source name" for Include resolution
        var inputStream = new AntlrInputStream(configContent)
        {
            name = baseDirectory
        };
        var lexer = new PacmanConfLexer(inputStream);
        
        var tokenSource = new PacmanConfigTokenSource(logger, lexer);
        var tokens = new CommonTokenStream(tokenSource);
        var parser = new PacmanConfParser(tokens);
        parser.ErrorHandler = new BailErrorStrategy();
        var errorListener = new PacmanConfigErrorListener(logger);
        parser.AddErrorListener(errorListener);
        var parseTree = parser.pacmanConf();
        var visitor = new PacmanConfigVisitor(logger);
        return visitor.Visit(parseTree);
    }
}