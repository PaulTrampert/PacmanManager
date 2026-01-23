using LibAlpmSharp.Interop;

namespace LibAlpmSharp.Config;

public class UsageLookup
{
    public static AlpmDbUsage LookupUsage(IEnumerable<string> usages)
    {
        AlpmDbUsage usage = AlpmDbUsage.ALPM_DB_USAGE_ALL;
        bool hasUsages = false;

        foreach (var usageStr in usages)
        {
            if (!hasUsages)
            {
                usage = 0;
                hasUsages = true;
            }
            
            switch (usageStr)
            {
                case "Sync":
                    usage |= AlpmDbUsage.ALPM_DB_USAGE_SYNC;
                    break;
                case "Search":
                    usage |= AlpmDbUsage.ALPM_DB_USAGE_SEARCH;
                    break;
                case "Install":
                    usage |= AlpmDbUsage.ALPM_DB_USAGE_INSTALL;
                    break;
                case "Upgrade":
                    usage |= AlpmDbUsage.ALPM_DB_USAGE_UPGRADE;
                    break;
                case "All":
                    usage |= AlpmDbUsage.ALPM_DB_USAGE_ALL;
                    break;
                default:
                    throw new ArgumentException($"Invalid usage type: {usageStr}");
            }
        }

        return usage;
    }
}