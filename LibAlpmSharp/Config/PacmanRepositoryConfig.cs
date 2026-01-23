using LibAlpmSharp.Interop;

namespace LibAlpmSharp.Config;

public record PacmanRepositoryConfig
{
    public string Name { get; init; } = string.Empty;
    public IEnumerable<string> Server { get; init; } = [];
    public IEnumerable<string> CacheServer { get; init; } = [];
    public AlpmSigLevel SigLevel { get; init; } = AlpmSigLevel.ALPM_SIG_USE_DEFAULT;
    public AlpmDbUsage Usage { get; init; } = AlpmDbUsage.ALPM_DB_USAGE_ALL;
}