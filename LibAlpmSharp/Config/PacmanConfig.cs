using LibAlpmSharp.Interop;

namespace LibAlpmSharp.Config;

public record PacmanConfig
{
    public static PacmanConfig Default => new();
    
    public string RootDir { get; init; } = "/";
    public string DBPath { get; init; } = "/var/lib/pacman";
    public string CacheDir { get; init; } = "/var/cache/pacman/pkg";
    public string HookDir { get; init; } = "/etc/pacman.d/hooks";
    public string GPGDir { get; init; } = "/etc/pacman.d/gnupg";
    public string LogFile { get; init; } = "/var/log/pacman.log";
    public IEnumerable<string> HoldPkg { get; init; } = [];
    public IEnumerable<string> IgnorePkg { get; init; } = [];
    public IEnumerable<string> IgnoreGroup { get; init; } = [];
    public string Architecture { get; init; } = "auto";
    public string XferCommand { get; init; } = string.Empty;
    public IEnumerable<string> NoUpgrade { get; init; } = [];
    public IEnumerable<string> NoExtract { get; init; } = [];
    public string CleanMethod { get; init; } = "KeepInstalled";
    public AlpmSigLevel SigLevel { get; init; } = AlpmSigLevel.ALPM_SIG_USE_DEFAULT;
    public AlpmSigLevel LocalFileSigLevel { get; init; }
    public AlpmSigLevel RemoteFileSigLevel { get; init; }
    public bool UseSyslog { get; init; }
    public bool Color { get; init; }
    public bool NoProgressBar { get; init; }
    public bool CheckSpace { get; init; }
    public bool VerbosePkgLists { get; init; }
    public bool DisableDownloadTimeout { get; init; }
    public int ParallelDownloads { get; init; } = 1;
    public string DownloadUser { get; init; } = string.Empty;
    public bool DisableSandbox { get; init; }
    public bool DisableSandboxFilesystem { get; init; }
    public bool DisableSandboxSyscalls { get; init; }
    
    public IEnumerable<PacmanRepositoryConfig> Repositories { get; init; } = [];
}