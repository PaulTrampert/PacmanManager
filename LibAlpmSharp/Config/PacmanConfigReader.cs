using Antlr4.Runtime;
using Antlr4.Runtime.Atn;
using Antlr4.Runtime.Dfa;
using Antlr4.Runtime.Sharpen;
using LibAlpmSharp.Config.Listeners;
using LibAlpmSharp.Config.Visitors;
using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Config;

public class PacmanConfigReader(ILogger<PacmanConfigReader> logger)
{
    private readonly IParserErrorListener _errorListener = new PacmanConfigErrorListener(logger);
    
    public PacmanConfig ReadConfig(string filePath)
    {
        logger.LogInformation("Reading Pacman configuration from {FilePath}", filePath);

        using var configReader = File.OpenText(filePath);
        var tokenStream = new AntlrInputStream(configReader);
        var lexer = new PacmanConfLexer(tokenStream);
        var tokens = new CommonTokenStream(lexer);
        var parser = new PacmanConfParser(tokens);
        parser.AddErrorListener(_errorListener);
        var parseTree = parser.pacmanConf();
        var visitor = new PacmanConfigVisitor(logger);
        return visitor.Visit(parseTree);
    }
}