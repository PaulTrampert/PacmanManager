using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Config.Visitors;

internal class PacmanConfigVisitor(ILogger logger) : PacmanConfParserBaseVisitor<PacmanConfig>
{
}