using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Config.Visitors;

internal class PacmanConfigVisitor(ILogger logger) : PacmanConfParserBaseVisitor<PacmanConfig>
{
    private PacmanConfig _config;
    
    public override PacmanConfig VisitPacmanConf(PacmanConfParser.PacmanConfContext context)
    {
        _config = new PacmanConfig();
        return base.VisitPacmanConf(context);
    }
}