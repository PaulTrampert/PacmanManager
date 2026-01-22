using Antlr4.Runtime;
using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Config.Listeners;

internal class PacmanConfigErrorListener(ILogger logger) : BaseErrorListener
{
    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine,
        string msg, RecognitionException e)
    {
        logger.LogError(e, "Syntax error at line {Line}, position {Position}: {Message}", line, charPositionInLine, msg);
    }
}