using LibAlpmSharp.Interop;

namespace LibAlpmSharp.Config;

public record PacmanConfig
{
    public string RootDir { get; init; } = "/";
    public string DbPath { get; init; } = "/var/lib/pacman";
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
    public bool UseSyslog { get; init; } = false;
    public bool Color { get; init; } = false;
    public bool NoProgressBar { get; init; } = false;
    public bool CheckSpace { get; init; } = true;
    public bool VerbosePkgLists { get; init; } = false;
    public bool DisableDownloadTimeout { get; init; } = false;
    public int ParallelDownloads { get; init; } = 1;
    public string DownloadUser { get; init; } = string.Empty;
    public bool DisableSandbox { get; init; } = false;
    public bool DisableSandboxFilesystem { get; init; } = false;
    public bool DisableSandboxSyscalls { get; init; } = false;
    
    public IEnumerable<PacmanRepositoryConfig> Repositories { get; init; } = [];
}