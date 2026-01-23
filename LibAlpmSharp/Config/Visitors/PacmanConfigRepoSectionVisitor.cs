using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Config.Visitors;

internal class PacmanConfigRepoSectionVisitor(ILogger logger) : PacmanConfParserBaseVisitor<PacmanRepositoryConfig>
{
    private PacmanRepositoryConfig _repositoryConfig = new();

    public override PacmanRepositoryConfig VisitSection(PacmanConfParser.SectionContext context)
    {
        base.VisitSection(context);
        return _repositoryConfig;
    }

    public override PacmanRepositoryConfig VisitSectionHeader(PacmanConfParser.SectionHeaderContext context)
    {
        _repositoryConfig = _repositoryConfig with
        {
            Name = context.REPO_ID().GetText()!
        };
        return _repositoryConfig;
    }

    public override PacmanRepositoryConfig VisitSetting(PacmanConfParser.SettingContext context)
    {
        var key = context.settingKey().GetText()!;
        var valueVisitor = new PacmanConfigValueVisitor(logger);
        var values = valueVisitor.VisitSettingValues(context.settingValues())?.ToList() ?? [];

        switch (key)
        {
            case nameof(PacmanRepositoryConfig.Server):
                _repositoryConfig = _repositoryConfig with { Server = _repositoryConfig.Server.Union(values) };
                break;
            case nameof(PacmanRepositoryConfig.SigLevel):
                _repositoryConfig = _repositoryConfig with { SigLevel = SigLevelLookup.LookupSigLevel(values) };
                break;
            case nameof(PacmanRepositoryConfig.CacheServer):
                _repositoryConfig = _repositoryConfig with { CacheServer = _repositoryConfig.CacheServer.Union(values) };
                break;
            case nameof(PacmanRepositoryConfig.Usage):
                _repositoryConfig = _repositoryConfig with { Usage = UsageLookup.LookupUsage(values) };
                break;
            default:
                throw new NotSupportedException($"Unsupported repository setting key: {key}");
        }
        
        return _repositoryConfig;
    }
}