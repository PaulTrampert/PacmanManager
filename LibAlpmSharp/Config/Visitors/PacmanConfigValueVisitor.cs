using System.Collections.ObjectModel;
using Antlr4.Runtime.Tree;
using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Config.Visitors;

internal class PacmanConfigValueVisitor(ILogger logger) : PacmanConfParserBaseVisitor<IEnumerable<string>>
{
    private readonly ReadOnlyDictionary<string, string> EscapeSequences = new(new Dictionary<string, string>
    {
        { "\\n", "\n" },
        { "\\b", "\b" },
        { "\\f", "\f" },
        { "\\t", "\t" },
        { "\\r", "\r" },
        { "\\\"", "\"" },
        { "\\'", "'" },
        { "\\\\", "\\" }
    });
        
    protected override IEnumerable<string> AggregateResult(IEnumerable<string> aggregate, IEnumerable<string> nextResult)
    {
        if (aggregate == null || nextResult == null)
            return aggregate ?? nextResult ?? [];
        return aggregate.Concat(nextResult);
    }

    public override IEnumerable<string> VisitTerminal(ITerminalNode node)
    {
        var nodeText = node.GetText();
        logger.LogDebug("Visiting terminal node of type {NodeType} with text: {Text}", node.Symbol.Type, nodeText);
        return node.Symbol.Type switch
        {
            PacmanConfLexer.STRING_ESCAPE_SEQUENCE 
                or PacmanConfLexer.DQUOTE_ESCAPE_SEQUENCE
                or PacmanConfLexer.SQUOTE_ESCAPE_SEQUENCE => [EscapeSequences[nodeText]],
            PacmanConfLexer.STRING_DQUOTE_OPEN
                or PacmanConfLexer.DQUOTE_CLOSE
                or PacmanConfLexer.STRING_SQUOTE_OPEN
                or PacmanConfLexer.SQUOTE_CLOSE
                or PacmanConfLexer.STRING_WS => [],
            _ => [nodeText]
        };
    }

    public override IEnumerable<string> VisitString(PacmanConfParser.StringContext context)
    {
        var childResults = VisitChildren(context) ?? [];
        var result = string.Join(string.Empty, childResults);
        logger.LogDebug("String node resolved to: {Result}", result);
        return [result];
    }

    public override IEnumerable<string> VisitQuotedString(PacmanConfParser.QuotedStringContext context)
    {
        var childResults = VisitChildren(context) ?? [];
        var result = string.Join(string.Empty, childResults);
        logger.LogDebug("Quoted string node resolved to: {Result}", result);
        return [result];
    }
}