using System.Text;
using LibAlpmSharp.Interop;

namespace LibAlpmSharp.Config;

public class PacmanConfigSerializer : IPacmanConfigSerializer
{
    public string Serialize(PacmanConfig config)
    {
        var defaultConfig = PacmanConfig.Default;
        var sb = new StringBuilder();

        // Header
        sb.AppendLine("[options]");

        // Compare and write differences
        if (config.RootDir != defaultConfig.RootDir)
        {
            sb.AppendLine($"RootDir = {EscapeToken(config.RootDir)}");
        }

        if (config.DBPath != defaultConfig.DBPath)
        {
            sb.AppendLine($"DBPath = {EscapeToken(config.DBPath)}");
        }

        if (config.CacheDir != defaultConfig.CacheDir)
        {
            sb.AppendLine($"CacheDir = {EscapeToken(config.CacheDir)}");
        }

        if (config.HookDir != defaultConfig.HookDir)
        {
            sb.AppendLine($"HookDir = {EscapeToken(config.HookDir)}");
        }

        if (config.GPGDir != defaultConfig.GPGDir)
        {
            sb.AppendLine($"GPGDir = {EscapeToken(config.GPGDir)}");
        }

        if (config.LogFile != defaultConfig.LogFile)
        {
            sb.AppendLine($"LogFile = {EscapeToken(config.LogFile)}");
        }

        if (!config.HoldPkg.SequenceEqual(defaultConfig.HoldPkg))
        {
            if (config.HoldPkg.Any()) sb.AppendLine($"HoldPkg = {JoinValues(config.HoldPkg)}");
        }

        if (!config.IgnorePkg.SequenceEqual(defaultConfig.IgnorePkg))
        {
            if (config.IgnorePkg.Any()) sb.AppendLine($"IgnorePkg = {JoinValues(config.IgnorePkg)}");
        }

        if (!config.IgnoreGroup.SequenceEqual(defaultConfig.IgnoreGroup))
        {
            if (config.IgnoreGroup.Any()) sb.AppendLine($"IgnoreGroup = {JoinValues(config.IgnoreGroup)}");
        }

        if (config.Architecture != defaultConfig.Architecture)
        {
            sb.AppendLine($"Architecture = {EscapeToken(config.Architecture)}");
        }

        if (config.XferCommand != defaultConfig.XferCommand)
        {
            sb.AppendLine($"XferCommand = {EscapeToken(config.XferCommand)}");
        }

        if (!config.NoUpgrade.SequenceEqual(defaultConfig.NoUpgrade))
        {
            if (config.NoUpgrade.Any()) sb.AppendLine($"NoUpgrade = {JoinValues(config.NoUpgrade)}");
        }

        if (!config.NoExtract.SequenceEqual(defaultConfig.NoExtract))
        {
            if (config.NoExtract.Any()) sb.AppendLine($"NoExtract = {JoinValues(config.NoExtract)}");
        }

        if (config.CleanMethod != defaultConfig.CleanMethod)
        {
            sb.AppendLine($"CleanMethod = {EscapeToken(config.CleanMethod)}");
        }

        if (config.SigLevel != defaultConfig.SigLevel)
        {
            var s = Serialize(config.SigLevel);
            if (!string.IsNullOrEmpty(s)) sb.AppendLine($"SigLevel = {s}");
        }

        if (config.LocalFileSigLevel != defaultConfig.LocalFileSigLevel)
        {
            var s = Serialize(config.LocalFileSigLevel);
            if (!string.IsNullOrEmpty(s)) sb.AppendLine($"LocalFileSigLevel = {s}");
        }

        if (config.RemoteFileSigLevel != defaultConfig.RemoteFileSigLevel)
        {
            var s = Serialize(config.RemoteFileSigLevel);
            if (!string.IsNullOrEmpty(s)) sb.AppendLine($"RemoteFileSigLevel = {s}");
        }

        WriteBool(nameof(PacmanConfig.UseSyslog), config.UseSyslog);
        WriteBool(nameof(PacmanConfig.Color), config.Color);
        WriteBool(nameof(PacmanConfig.NoProgressBar), config.NoProgressBar);
        WriteBool(nameof(PacmanConfig.CheckSpace), config.CheckSpace);
        WriteBool(nameof(PacmanConfig.VerbosePkgLists), config.VerbosePkgLists);
        WriteBool(nameof(PacmanConfig.DisableDownloadTimeout), config.DisableDownloadTimeout);

        if (config.ParallelDownloads != defaultConfig.ParallelDownloads)
        {
            sb.AppendLine($"ParallelDownloads = {config.ParallelDownloads}");
        }

        if (config.DownloadUser != defaultConfig.DownloadUser)
        {
            sb.AppendLine($"DownloadUser = {EscapeToken(config.DownloadUser)}");
        }

        WriteBool(nameof(PacmanConfig.DisableSandbox), config.DisableSandbox);
        WriteBool(nameof(PacmanConfig.DisableSandboxFilesystem), config.DisableSandboxFilesystem);
        WriteBool(nameof(PacmanConfig.DisableSandboxSyscalls), config.DisableSandboxSyscalls);

        // Repositories
        foreach (var repo in config.Repositories)
        {
            sb.AppendLine();
            sb.Append(Serialize(repo));
        }

        return sb.ToString();

        // Helper local functions
        static string JoinValues(IEnumerable<string> values)
        {
            return string.Join(' ', values.Select(EscapeToken));
        }

        void WriteBool(string name, bool value)
        {
            if (value)
                sb.AppendLine(name);
        }

        static string EscapeToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return "";
            var mustQuote = token.Any(char.IsWhiteSpace) || token.Contains('"') || token.Contains('=');
            var sb = new StringBuilder();
            foreach (var ch in token)
            {
                switch (ch)
                {
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    case '"': sb.Append("\\\""); break;
                    default: sb.Append(ch); break;
                }
            }
            var escaped = sb.ToString();
            return mustQuote ? '"' + escaped + '"' : escaped;
        }
    }

    public string Serialize(PacmanRepositoryConfig config)
    {
        var defaultRepo = PacmanRepositoryConfig.Default;

        var stringBuilder = new StringBuilder();

        stringBuilder.AppendLine($"[{config.Name}]");
        foreach (var server in config.Server)
        {
            stringBuilder.AppendLine($"Server = {server}");
        }

        foreach (var cacheServer in config.CacheServer)
        {
            stringBuilder.AppendLine($"CacheServer = {cacheServer}");
        }
        
        if (config.SigLevel != defaultRepo.SigLevel)
        {
            var sig = Serialize(config.SigLevel);
            if (!string.IsNullOrEmpty(sig))
            {
                stringBuilder.AppendLine($"SigLevel = {sig}");
            }
        }

        if (config.Usage != defaultRepo.Usage)
        {
            var usage = Serialize(config.Usage);
            if (!string.IsNullOrEmpty(usage))
            {
                stringBuilder.AppendLine($"Usage = {usage}");
            }
        }
        
        return stringBuilder.ToString();
    }

    public string Serialize(AlpmDbUsage usage)
    {
        // "All" is the canonical value representing all usage flags
        if (usage == AlpmDbUsage.ALPM_DB_USAGE_ALL)
        {
            return "All";
        }

        // No usage specified -> nothing to serialize
        if (usage == 0)
        {
            return string.Empty;
        }

        var tokens = new List<string>();

        if ((usage & AlpmDbUsage.ALPM_DB_USAGE_SYNC) != 0)
        {
            tokens.Add("Sync");
        }
        if ((usage & AlpmDbUsage.ALPM_DB_USAGE_SEARCH) != 0)
        {
            tokens.Add("Search");
        }
        if ((usage & AlpmDbUsage.ALPM_DB_USAGE_INSTALL) != 0)
        {
            tokens.Add("Install");
        }
        if ((usage & AlpmDbUsage.ALPM_DB_USAGE_UPGRADE) != 0)
        {
            tokens.Add("Upgrade");
        }

        return string.Join(' ', tokens);
    }

    public string Serialize(AlpmSigLevel sigLevel)
    {
        // Special-case: use-default is represented by omission
        if ((sigLevel & AlpmSigLevel.ALPM_SIG_USE_DEFAULT) != 0)
        {
            return string.Empty;
        }

        // "Never" is represented by zero
        if (sigLevel == 0)
        {
            return "Never";
        }

        var tokens = new List<string>();

        var pkgReq = (sigLevel & AlpmSigLevel.ALPM_SIG_PACKAGE) != 0;
        var pkgOpt = (sigLevel & AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL) != 0;
        var pkgMarg = (sigLevel & AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK) != 0;

        var dbReq = (sigLevel & AlpmSigLevel.ALPM_SIG_DATABASE) != 0;
        var dbOpt = (sigLevel & AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL) != 0;
        var dbMarg = (sigLevel & AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK) != 0;

        // Prefer combined tokens when both package & database share the same setting
        if (pkgReq && dbReq && !pkgOpt && !dbOpt)
        {
            tokens.Add("Required");
        }
        else if (pkgOpt && dbOpt && !pkgReq && !dbReq)
        {
            tokens.Add("Optional");
        }
        else
        {
            if (pkgReq) tokens.Add("PackageRequired");
            else if (pkgOpt) tokens.Add("PackageOptional");

            if (dbReq) tokens.Add("DatabaseRequired");
            else if (dbOpt) tokens.Add("DatabaseOptional");
        }

        // Marginal/trust tokens
        if (pkgMarg && dbMarg)
        {
            tokens.Add("TrustAll");
        }
        else if (pkgMarg)
        {
            tokens.Add("PackageTrustAll");
        }
        else if (dbMarg)
        {
            tokens.Add("DatabaseTrustAll");
        }

        return string.Join(' ', tokens);
    }
}