using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Config.Visitors;

internal class PacmanConfigRepoSectionVisitor(ILogger logger) : PacmanConfParserBaseVisitor<PacmanRepositoryConfig>
{
    PacmanRepositoryConfig repositoryConfig = new();

    public override PacmanRepositoryConfig VisitSectionHeader(PacmanConfParser.SectionHeaderContext context)
    {
        repositoryConfig = repositoryConfig with
        {
            Name = context.REPO_ID().GetText()!
        };
        return repositoryConfig;
    }

    public override PacmanRepositoryConfig VisitSetting(PacmanConfParser.SettingContext context)
    {
        var key = context.settingKey().GetText()!;
        var valueVisitor = new PacmanConfigValueVisitor(logger);
        var values = valueVisitor.VisitSettingValues(context.settingValues())?.ToList() ?? [];

        switch (key)
        {
            case nameof(PacmanRepositoryConfig.Server):
                repositoryConfig = repositoryConfig with { Server = repositoryConfig.Server.Union(values) };
                break;
            case nameof(PacmanRepositoryConfig.SigLevel):
                repositoryConfig = repositoryConfig with { SigLevel = SigLevelLookup.LookupSigLevel(values) };
                break;
            case nameof(PacmanRepositoryConfig.CacheServer):
                repositoryConfig = repositoryConfig with { CacheServer = repositoryConfig.CacheServer.Union(values) };
                break;
            case nameof(PacmanRepositoryConfig.Usage):
                repositoryConfig = repositoryConfig with { Usage = UsageLookup.LookupUsage(values) };
                break;
            default:
                throw new NotSupportedException($"Unsupported repository setting key: {key}");
        }
        
        return repositoryConfig;
    }
}