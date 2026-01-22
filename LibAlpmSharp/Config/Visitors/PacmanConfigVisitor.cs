using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Config.Visitors;

internal class PacmanConfigVisitor : PacmanConfParserBaseVisitor<PacmanConfig>
{
    private ILogger _logger;
    private PacmanConfig _config;
    private PacmanRepositoryConfig? _currentRepo;

    public PacmanConfigVisitor(ILogger logger, PacmanConfig? config = null, PacmanRepositoryConfig? currentRepo = null)
    {
        _logger = logger;
        _config = config ?? new PacmanConfig();
        _currentRepo = currentRepo;
    }

    public override PacmanConfig VisitSetting(PacmanConfParser.SettingContext context)
    {
        return base.VisitSetting(context);
    }
}