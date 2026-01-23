using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Config.Visitors;

internal class PacmanConfigVisitor(ILogger logger) : PacmanConfParserBaseVisitor<PacmanConfig>
{
    private PacmanConfig _config;
    
    public override PacmanConfig VisitPacmanConf(PacmanConfParser.PacmanConfContext context)
    {
        _config = new PacmanConfig();
        base.VisitPacmanConf(context);
        return _config;
    }

    public override PacmanConfig VisitSection(PacmanConfParser.SectionContext context)
    {
        var sectionHeader = context.sectionHeader();
        if (sectionHeader.REPO_ID() != null)
        {
            var repoConfig = new PacmanConfigRepoSectionVisitor(logger).Visit(context);
            _config = _config with
            {
                Repositories = _config.Repositories.Append(repoConfig)
            };
            return _config;
        }
        return base.VisitSection(context);
    }

    public override PacmanConfig VisitSetting(PacmanConfParser.SettingContext context)
    {
        var key = context.settingKey().GetText()!;
        var valueCtx = context.settingValues();
        var values = valueCtx != null 
            ? new PacmanConfigValueVisitor(logger).VisitSettingValues(valueCtx)?.ToList() ?? []
            : new List<string>();
        switch (key)
        {
            case nameof(PacmanConfig.RootDir):
                _config = _config with { RootDir = values.FirstOrDefault() ?? throw new ArgumentException($"{key} requires a value") };
                break;
            case nameof(PacmanConfig.DBPath):
                _config = _config with { DBPath = values.FirstOrDefault() ?? throw new ArgumentException($"{key} requires a value") };
                break;
            case nameof(PacmanConfig.CacheDir):
                _config = _config with { CacheDir = values.FirstOrDefault() ?? throw new ArgumentException($"{key} requires a value") };
                break;
            case nameof(PacmanConfig.HookDir):
                _config = _config with { HookDir = values.FirstOrDefault() ?? throw new ArgumentException($"{key} requires a value") };
                break;
            case nameof(PacmanConfig.GPGDir):
                _config = _config with { GPGDir = values.FirstOrDefault() ?? throw new ArgumentException($"{key} requires a value") };
                break;
            case nameof(PacmanConfig.LogFile):
                _config = _config with { LogFile = values.FirstOrDefault() ?? throw new ArgumentException($"{key} requires a value") };
                break;
            case nameof(PacmanConfig.HoldPkg):
                _config = _config with { HoldPkg = _config.HoldPkg.Union(values) };
                break;
            case nameof(PacmanConfig.IgnorePkg):
                _config = _config with { IgnorePkg = _config.IgnorePkg.Union(values) };
                break;
            case nameof(PacmanConfig.IgnoreGroup):
                _config = _config with { IgnoreGroup = _config.IgnoreGroup.Union(values) };
                break;
            case nameof(PacmanConfig.Architecture):
                _config = _config with { Architecture = values.FirstOrDefault() ?? throw new ArgumentException($"{key} requires a value") };
                break;
            case nameof(PacmanConfig.XferCommand):
                _config = _config with { XferCommand = values.FirstOrDefault() ?? throw new ArgumentException($"{key} requires a value") };
                break;
            case nameof(PacmanConfig.NoUpgrade):
                _config = _config with { NoUpgrade = _config.NoUpgrade.Union(values) };
                break;
            case nameof(PacmanConfig.NoExtract):
                _config = _config with { NoExtract = _config.NoExtract.Union(values) };
                break;
            case nameof(PacmanConfig.CleanMethod):
                _config = _config with { CleanMethod = values.FirstOrDefault() ?? throw new ArgumentException($"{key} requires a value") };
                break;
            case nameof(PacmanConfig.SigLevel):
                _config = _config with { SigLevel = SigLevelLookup.LookupSigLevel(values) };
                break;
            case nameof(PacmanConfig.LocalFileSigLevel):
                _config = _config with { LocalFileSigLevel = SigLevelLookup.LookupSigLevel(values) };
                break;
            case nameof(PacmanConfig.RemoteFileSigLevel):
                _config = _config with { RemoteFileSigLevel = SigLevelLookup.LookupSigLevel(values) };
                break;
            case nameof(PacmanConfig.UseSyslog):
                _config = _config with { UseSyslog = true };
                break;
            case nameof(PacmanConfig.Color):
                _config = _config with { Color = true };
                break;
            case nameof(PacmanConfig.NoProgressBar):
                _config = _config with { NoProgressBar = true };
                break;
            case nameof(PacmanConfig.CheckSpace):
                _config = _config with { CheckSpace = true };
                break;
            case nameof(PacmanConfig.VerbosePkgLists):
                _config = _config with { VerbosePkgLists = true };
                break;
            case nameof(PacmanConfig.DisableDownloadTimeout):
                _config = _config with { DisableDownloadTimeout = true };
                break;
            case nameof(PacmanConfig.ParallelDownloads):
                var parallelDownloadsValue = values.FirstOrDefault() ?? throw new ArgumentException($"{key} requires a value");
                if (!int.TryParse(parallelDownloadsValue, out var parallelDownloads))
                {
                    throw new ArgumentException($"{key} must be a valid integer");
                }
                _config = _config with { ParallelDownloads = parallelDownloads };
                break;
            case nameof(PacmanConfig.DownloadUser):
                _config = _config with { DownloadUser = values.FirstOrDefault() ?? throw new ArgumentException($"{key} requires a value") };
                break;
            case nameof(PacmanConfig.DisableSandbox):
                _config = _config with { DisableSandbox = true };
                break;
            case nameof(PacmanConfig.DisableSandboxFilesystem):
                _config = _config with { DisableSandboxFilesystem = true };
                break;
            case nameof(PacmanConfig.DisableSandboxSyscalls):
                _config = _config with { DisableSandboxSyscalls = true };
                break;
            default:
                throw new NotSupportedException($"Unsupported option: {key}");
        }
        return _config;
    }
}