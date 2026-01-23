using Antlr4.Runtime;
using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Config.Listeners;

internal class PacmanConfigErrorListener(ILogger logger) : BaseErrorListener
{
    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine,
        string msg, RecognitionException e)
    {
        logger.LogError(
            e, 
            "Syntax error in {File} at line {Line}, position {Position}: {Message}", 
            offendingSymbol.TokenSource.SourceName, 
            line, 
            charPositionInLine, 
            msg
        );
    }
}