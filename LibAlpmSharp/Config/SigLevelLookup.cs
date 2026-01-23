using LibAlpmSharp.Interop;

namespace LibAlpmSharp.Config;

public static class SigLevelLookup
{
    public static AlpmSigLevel LookupSigLevel(IEnumerable<string> sigLevels)
    {
        AlpmSigLevel level = AlpmSigLevel.ALPM_SIG_USE_DEFAULT;

        foreach (var sigLevel in sigLevels)
        {
            if ((level & AlpmSigLevel.ALPM_SIG_USE_DEFAULT) > 0) 
            {
                level &= ~AlpmSigLevel.ALPM_SIG_USE_DEFAULT;
            }
            switch (sigLevel)
            {
                case "Never":
                    level = 0;
                    break;
                case "Optional":
                    level |= AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL | AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL;
                    level &= ~(AlpmSigLevel.ALPM_SIG_DATABASE | AlpmSigLevel.ALPM_SIG_PACKAGE);
                    break;
                case "Required":
                    level |= AlpmSigLevel.ALPM_SIG_DATABASE | AlpmSigLevel.ALPM_SIG_PACKAGE;
                    level &= ~(AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL | AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL);
                    break;
                case "TrustedOnly":
                    level &= ~(AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK | AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK);
                    break;
                case "TrustAll":
                    level |= AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK | AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK;
                    break;
                case "PackageOptional":
                    level |= AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL;
                    level &= ~AlpmSigLevel.ALPM_SIG_PACKAGE;
                    break;
                case "PackageRequired":
                    level |= AlpmSigLevel.ALPM_SIG_PACKAGE;
                    level &= ~AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL;
                    break;
                case "PackageTrustedOnly":
                    level &= ~AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK;
                    break;
                case "PackageTrustAll":
                    level |= AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK;
                    break;
                case "DatabaseOptional":
                    level |= AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL;
                    level &= ~AlpmSigLevel.ALPM_SIG_DATABASE;
                    break;
                case "DatabaseRequired":
                    level |= AlpmSigLevel.ALPM_SIG_DATABASE;;
                    level &= ~AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL;
                    break;
                case "DatabaseTrustedOnly":
                    level &= ~AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK;
                    break;
                case "DatabaseTrustAll":
                    level |= AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK;
                    break;
                default:
                    throw new ArgumentException($"Unknown SigLevel value: {sigLevel}");
            }
        }

        return level;
    }
}