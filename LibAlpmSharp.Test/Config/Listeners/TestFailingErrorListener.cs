using Antlr4.Runtime;

namespace LibAlpmSharp.Test.Config.Listeners;

public class TestFailingErrorListener : BaseErrorListener
{
    public override void SyntaxError(TextWriter output, IRecognizer recognizer, IToken offendingSymbol, int line, int charPositionInLine,
        string msg, RecognitionException e)
    {
        Assert.Fail($"Syntax error at line {line}, position {charPositionInLine}: {msg}\n" +
                    $"Offending Token Type: {PacmanConfLexer.ruleNames[offendingSymbol.Type]}",
            e);
    }
}