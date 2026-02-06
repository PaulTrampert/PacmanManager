using LibAlpmSharp.Config;
using LibAlpmSharp.Interop;

namespace LibAlpmSharp.Test.Config;

[TestFixture]
public class PacmanConfigSerializerTests
{
    private PacmanConfigSerializer _serializer = null!;

    [SetUp]
    public void SetUp()
    {
        _serializer = new PacmanConfigSerializer();
    }

    #region Serialize(PacmanConfig) Tests

    [Test]
    public void Serialize_DefaultConfig_ReturnsOnlyOptionsHeader()
    {
        // Arrange
        var config = PacmanConfig.Default;

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("[options]"));
        // Default config should not output most properties
        Assert.That(result, Does.Not.Contain("RootDir"));
        Assert.That(result, Does.Not.Contain("DBPath"));
    }

    [Test]
    public void Serialize_CustomRootDir_SerializesRootDir()
    {
        // Arrange
        var config = PacmanConfig.Default with { RootDir = "/custom/root" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("RootDir = /custom/root"));
    }

    [Test]
    public void Serialize_CustomDBPath_SerializesDBPath()
    {
        // Arrange
        var config = PacmanConfig.Default with { DBPath = "/custom/db" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("DBPath = /custom/db"));
    }

    [Test]
    public void Serialize_CustomCacheDir_SerializesCacheDir()
    {
        // Arrange
        var config = PacmanConfig.Default with { CacheDir = "/custom/cache" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("CacheDir = /custom/cache"));
    }

    [Test]
    public void Serialize_CustomHookDir_SerializesHookDir()
    {
        // Arrange
        var config = PacmanConfig.Default with { HookDir = "/custom/hooks" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("HookDir = /custom/hooks"));
    }

    [Test]
    public void Serialize_CustomGPGDir_SerializesGPGDir()
    {
        // Arrange
        var config = PacmanConfig.Default with { GPGDir = "/custom/gpg" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("GPGDir = /custom/gpg"));
    }

    [Test]
    public void Serialize_CustomLogFile_SerializesLogFile()
    {
        // Arrange
        var config = PacmanConfig.Default with { LogFile = "/custom/log" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("LogFile = /custom/log"));
    }

    [Test]
    public void Serialize_HoldPkg_SerializesHoldPkg()
    {
        // Arrange
        var config = PacmanConfig.Default with { HoldPkg = new[] { "pkg1", "pkg2" } };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("HoldPkg = pkg1 pkg2"));
    }

    [Test]
    public void Serialize_IgnorePkg_SerializesIgnorePkg()
    {
        // Arrange
        var config = PacmanConfig.Default with { IgnorePkg = new[] { "pkg1", "pkg2" } };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("IgnorePkg = pkg1 pkg2"));
    }

    [Test]
    public void Serialize_IgnoreGroup_SerializesIgnoreGroup()
    {
        // Arrange
        var config = PacmanConfig.Default with { IgnoreGroup = new[] { "group1", "group2" } };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("IgnoreGroup = group1 group2"));
    }

    [Test]
    public void Serialize_CustomArchitecture_SerializesArchitecture()
    {
        // Arrange
        var config = PacmanConfig.Default with { Architecture = "x86_64" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("Architecture = x86_64"));
    }

    [Test]
    public void Serialize_CustomXferCommand_SerializesXferCommand()
    {
        // Arrange
        var config = PacmanConfig.Default with { XferCommand = "/usr/bin/curl -C - -f %u > %o" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("XferCommand = \"/usr/bin/curl -C - -f %u > %o\""));
    }

    [Test]
    public void Serialize_NoUpgrade_SerializesNoUpgrade()
    {
        // Arrange
        var config = PacmanConfig.Default with { NoUpgrade = new[] { "file1", "file2" } };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("NoUpgrade = file1 file2"));
    }

    [Test]
    public void Serialize_NoExtract_SerializesNoExtract()
    {
        // Arrange
        var config = PacmanConfig.Default with { NoExtract = new[] { "file1", "file2" } };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("NoExtract = file1 file2"));
    }

    [Test]
    public void Serialize_CustomCleanMethod_SerializesCleanMethod()
    {
        // Arrange
        var config = PacmanConfig.Default with { CleanMethod = "KeepCurrent" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("CleanMethod = KeepCurrent"));
    }

    [Test]
    public void Serialize_CustomSigLevel_SerializesSigLevel()
    {
        // Arrange
        var config = PacmanConfig.Default with 
        { 
            SigLevel = AlpmSigLevel.ALPM_SIG_DATABASE | AlpmSigLevel.ALPM_SIG_PACKAGE 
        };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("SigLevel = Required"));
    }

    [Test]
    public void Serialize_CustomLocalFileSigLevel_SerializesLocalFileSigLevel()
    {
        // Arrange
        var config = PacmanConfig.Default with 
        { 
            LocalFileSigLevel = AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL | AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL 
        };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("LocalFileSigLevel = Optional"));
    }

    [Test]
    public void Serialize_CustomRemoteFileSigLevel_SerializesRemoteFileSigLevel()
    {
        // Arrange
        var config = PacmanConfig.Default with 
        { 
            RemoteFileSigLevel = AlpmSigLevel.ALPM_SIG_DATABASE | AlpmSigLevel.ALPM_SIG_PACKAGE 
        };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("RemoteFileSigLevel = Required"));
    }

    [Test]
    public void Serialize_UseSyslogTrue_SerializesUseSyslog()
    {
        // Arrange
        var config = PacmanConfig.Default with { UseSyslog = true };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("UseSyslog"));
        Assert.That(result, Does.Not.Contain("UseSyslog ="));
    }

    [Test]
    public void Serialize_ColorTrue_SerializesColor()
    {
        // Arrange
        var config = PacmanConfig.Default with { Color = true };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("Color"));
        Assert.That(result, Does.Not.Contain("Color ="));
    }

    [Test]
    public void Serialize_NoProgressBarTrue_SerializesNoProgressBar()
    {
        // Arrange
        var config = PacmanConfig.Default with { NoProgressBar = true };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("NoProgressBar"));
    }

    [Test]
    public void Serialize_CheckSpaceTrue_SerializesCheckSpace()
    {
        // Arrange
        var config = PacmanConfig.Default with { CheckSpace = true };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("CheckSpace"));
    }

    [Test]
    public void Serialize_VerbosePkgListsTrue_SerializesVerbosePkgLists()
    {
        // Arrange
        var config = PacmanConfig.Default with { VerbosePkgLists = true };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("VerbosePkgLists"));
    }

    [Test]
    public void Serialize_DisableDownloadTimeoutTrue_SerializesDisableDownloadTimeout()
    {
        // Arrange
        var config = PacmanConfig.Default with { DisableDownloadTimeout = true };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("DisableDownloadTimeout"));
    }

    [Test]
    public void Serialize_CustomParallelDownloads_SerializesParallelDownloads()
    {
        // Arrange
        var config = PacmanConfig.Default with { ParallelDownloads = 5 };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("ParallelDownloads = 5"));
    }

    [Test]
    public void Serialize_CustomDownloadUser_SerializesDownloadUser()
    {
        // Arrange
        var config = PacmanConfig.Default with { DownloadUser = "alpm" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("DownloadUser = alpm"));
    }

    [Test]
    public void Serialize_DisableSandboxTrue_SerializesDisableSandbox()
    {
        // Arrange
        var config = PacmanConfig.Default with { DisableSandbox = true };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("DisableSandbox"));
    }

    [Test]
    public void Serialize_DisableSandboxFilesystemTrue_SerializesDisableSandboxFilesystem()
    {
        // Arrange
        var config = PacmanConfig.Default with { DisableSandboxFilesystem = true };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("DisableSandboxFilesystem"));
    }

    [Test]
    public void Serialize_DisableSandboxSyscallsTrue_SerializesDisableSandboxSyscalls()
    {
        // Arrange
        var config = PacmanConfig.Default with { DisableSandboxSyscalls = true };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("DisableSandboxSyscalls"));
    }

    [Test]
    public void Serialize_WithRepositories_SerializesRepositories()
    {
        // Arrange
        var repo = new PacmanRepositoryConfig
        {
            Name = "core",
            Server = new[] { "https://mirror.example.com/$repo/os/$arch" }
        };
        var config = PacmanConfig.Default with { Repositories = new[] { repo } };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("[core]"));
        Assert.That(result, Does.Contain("Server = https://mirror.example.com/$repo/os/$arch"));
    }

    [Test]
    public void Serialize_WithMultipleRepositories_SerializesAllRepositories()
    {
        // Arrange
        var repo1 = new PacmanRepositoryConfig
        {
            Name = "core",
            Server = new[] { "https://mirror1.example.com/$repo/os/$arch" }
        };
        var repo2 = new PacmanRepositoryConfig
        {
            Name = "extra",
            Server = new[] { "https://mirror2.example.com/$repo/os/$arch" }
        };
        var config = PacmanConfig.Default with { Repositories = new[] { repo1, repo2 } };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("[core]"));
        Assert.That(result, Does.Contain("[extra]"));
        Assert.That(result, Does.Contain("https://mirror1.example.com/$repo/os/$arch"));
        Assert.That(result, Does.Contain("https://mirror2.example.com/$repo/os/$arch"));
    }

    [Test]
    public void Serialize_TokenWithWhitespace_EscapesWithQuotes()
    {
        // Arrange
        var config = PacmanConfig.Default with { RootDir = "/path with spaces" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("RootDir = \"/path with spaces\""));
    }

    [Test]
    public void Serialize_TokenWithBackslash_EscapesBackslash()
    {
        // Arrange
        var config = PacmanConfig.Default with { RootDir = "/path\\with\\backslash" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("/path\\\\with\\\\backslash"));
    }

    [Test]
    public void Serialize_TokenWithNewline_EscapesNewline()
    {
        // Arrange
        var config = PacmanConfig.Default with { RootDir = "/path\nwith\nnewline" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("\\n"));
    }

    [Test]
    public void Serialize_TokenWithTab_EscapesTab()
    {
        // Arrange
        var config = PacmanConfig.Default with { RootDir = "/path\twith\ttab" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("\\t"));
    }

    [Test]
    public void Serialize_TokenWithQuote_EscapesQuote()
    {
        // Arrange
        var config = PacmanConfig.Default with { RootDir = "/path\"with\"quote" };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("\\\""));
    }

    [Test]
    public void Serialize_ComplexConfig_SerializesAllProperties()
    {
        // Arrange
        var repo = new PacmanRepositoryConfig
        {
            Name = "core",
            Server = new[] { "https://mirror.example.com/$repo/os/$arch" }
        };
        var config = new PacmanConfig
        {
            RootDir = "/custom/root",
            DBPath = "/custom/db",
            Architecture = "x86_64",
            HoldPkg = new[] { "pacman", "glibc" },
            IgnorePkg = new[] { "linux" },
            Color = true,
            CheckSpace = true,
            ParallelDownloads = 5,
            Repositories = new[] { repo }
        };

        // Act
        var result = _serializer.Serialize(config);

        // Assert
        Assert.That(result, Does.Contain("[options]"));
        Assert.That(result, Does.Contain("RootDir = /custom/root"));
        Assert.That(result, Does.Contain("DBPath = /custom/db"));
        Assert.That(result, Does.Contain("Architecture = x86_64"));
        Assert.That(result, Does.Contain("HoldPkg = pacman glibc"));
        Assert.That(result, Does.Contain("IgnorePkg = linux"));
        Assert.That(result, Does.Contain("Color"));
        Assert.That(result, Does.Contain("CheckSpace"));
        Assert.That(result, Does.Contain("ParallelDownloads = 5"));
        Assert.That(result, Does.Contain("[core]"));
    }

    #endregion

    #region Serialize(PacmanRepositoryConfig) Tests

    [Test]
    public void Serialize_Repository_SerializesName()
    {
        // Arrange
        var repo = new PacmanRepositoryConfig { Name = "core" };

        // Act
        var result = _serializer.Serialize(repo);

        // Assert
        Assert.That(result, Does.Contain("[core]"));
    }

    [Test]
    public void Serialize_RepositoryWithSingleServer_SerializesServer()
    {
        // Arrange
        var repo = new PacmanRepositoryConfig
        {
            Name = "core",
            Server = new[] { "https://mirror.example.com/$repo/os/$arch" }
        };

        // Act
        var result = _serializer.Serialize(repo);

        // Assert
        Assert.That(result, Does.Contain("Server = https://mirror.example.com/$repo/os/$arch"));
    }

    [Test]
    public void Serialize_RepositoryWithMultipleServers_SerializesAllServers()
    {
        // Arrange
        var repo = new PacmanRepositoryConfig
        {
            Name = "core",
            Server = new[] 
            { 
                "https://mirror1.example.com/$repo/os/$arch",
                "https://mirror2.example.com/$repo/os/$arch"
            }
        };

        // Act
        var result = _serializer.Serialize(repo);

        // Assert
        Assert.That(result, Does.Contain("Server = https://mirror1.example.com/$repo/os/$arch"));
        Assert.That(result, Does.Contain("Server = https://mirror2.example.com/$repo/os/$arch"));
    }

    [Test]
    public void Serialize_RepositoryWithCacheServer_SerializesCacheServer()
    {
        // Arrange
        var repo = new PacmanRepositoryConfig
        {
            Name = "core",
            CacheServer = new[] { "https://cache.example.com/$repo/os/$arch" }
        };

        // Act
        var result = _serializer.Serialize(repo);

        // Assert
        Assert.That(result, Does.Contain("CacheServer = https://cache.example.com/$repo/os/$arch"));
    }

    [Test]
    public void Serialize_RepositoryWithMultipleCacheServers_SerializesAllCacheServers()
    {
        // Arrange
        var repo = new PacmanRepositoryConfig
        {
            Name = "core",
            CacheServer = new[] 
            { 
                "https://cache1.example.com/$repo/os/$arch",
                "https://cache2.example.com/$repo/os/$arch"
            }
        };

        // Act
        var result = _serializer.Serialize(repo);

        // Assert
        Assert.That(result, Does.Contain("CacheServer = https://cache1.example.com/$repo/os/$arch"));
        Assert.That(result, Does.Contain("CacheServer = https://cache2.example.com/$repo/os/$arch"));
    }

    [Test]
    public void Serialize_RepositoryWithCustomSigLevel_SerializesSigLevel()
    {
        // Arrange
        var repo = new PacmanRepositoryConfig
        {
            Name = "core",
            SigLevel = AlpmSigLevel.ALPM_SIG_DATABASE | AlpmSigLevel.ALPM_SIG_PACKAGE
        };

        // Act
        var result = _serializer.Serialize(repo);

        // Assert
        Assert.That(result, Does.Contain("SigLevel = Required"));
    }

    [Test]
    public void Serialize_RepositoryWithDefaultSigLevel_DoesNotSerializeSigLevel()
    {
        // Arrange
        var repo = new PacmanRepositoryConfig
        {
            Name = "core",
            SigLevel = AlpmSigLevel.ALPM_SIG_USE_DEFAULT
        };

        // Act
        var result = _serializer.Serialize(repo);

        // Assert
        Assert.That(result, Does.Not.Contain("SigLevel"));
    }

    [Test]
    public void Serialize_RepositoryWithCustomUsage_SerializesUsage()
    {
        // Arrange
        var repo = new PacmanRepositoryConfig
        {
            Name = "core",
            Usage = AlpmDbUsage.ALPM_DB_USAGE_SYNC | AlpmDbUsage.ALPM_DB_USAGE_SEARCH
        };

        // Act
        var result = _serializer.Serialize(repo);

        // Assert
        Assert.That(result, Does.Contain("Usage = Sync Search"));
    }

    [Test]
    public void Serialize_RepositoryWithDefaultUsage_DoesNotSerializeUsage()
    {
        // Arrange
        var repo = new PacmanRepositoryConfig
        {
            Name = "core",
            Usage = AlpmDbUsage.ALPM_DB_USAGE_ALL
        };

        // Act
        var result = _serializer.Serialize(repo);

        // Assert
        // Usage should be serialized as "All" but due to default check it won't serialize
        // Actually, looking at the code again, default All doesn't serialize
        Assert.That(result, Does.Not.Contain("Usage"));
    }

    #endregion

    #region Serialize(AlpmDbUsage) Tests

    [Test]
    public void Serialize_UsageAll_ReturnsAll()
    {
        // Arrange
        var usage = AlpmDbUsage.ALPM_DB_USAGE_ALL;

        // Act
        var result = _serializer.Serialize(usage);

        // Assert
        Assert.That(result, Is.EqualTo("All"));
    }

    [Test]
    public void Serialize_UsageZero_ReturnsEmptyString()
    {
        // Arrange
        var usage = (AlpmDbUsage)0;

        // Act
        var result = _serializer.Serialize(usage);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Serialize_UsageSync_ReturnsSync()
    {
        // Arrange
        var usage = AlpmDbUsage.ALPM_DB_USAGE_SYNC;

        // Act
        var result = _serializer.Serialize(usage);

        // Assert
        Assert.That(result, Is.EqualTo("Sync"));
    }

    [Test]
    public void Serialize_UsageSearch_ReturnsSearch()
    {
        // Arrange
        var usage = AlpmDbUsage.ALPM_DB_USAGE_SEARCH;

        // Act
        var result = _serializer.Serialize(usage);

        // Assert
        Assert.That(result, Is.EqualTo("Search"));
    }

    [Test]
    public void Serialize_UsageInstall_ReturnsInstall()
    {
        // Arrange
        var usage = AlpmDbUsage.ALPM_DB_USAGE_INSTALL;

        // Act
        var result = _serializer.Serialize(usage);

        // Assert
        Assert.That(result, Is.EqualTo("Install"));
    }

    [Test]
    public void Serialize_UsageUpgrade_ReturnsUpgrade()
    {
        // Arrange
        var usage = AlpmDbUsage.ALPM_DB_USAGE_UPGRADE;

        // Act
        var result = _serializer.Serialize(usage);

        // Assert
        Assert.That(result, Is.EqualTo("Upgrade"));
    }

    [Test]
    public void Serialize_UsageSyncAndSearch_ReturnsSyncSearch()
    {
        // Arrange
        var usage = AlpmDbUsage.ALPM_DB_USAGE_SYNC | AlpmDbUsage.ALPM_DB_USAGE_SEARCH;

        // Act
        var result = _serializer.Serialize(usage);

        // Assert
        Assert.That(result, Is.EqualTo("Sync Search"));
    }

    [Test]
    public void Serialize_UsageInstallAndUpgrade_ReturnsInstallUpgrade()
    {
        // Arrange
        var usage = AlpmDbUsage.ALPM_DB_USAGE_INSTALL | AlpmDbUsage.ALPM_DB_USAGE_UPGRADE;

        // Act
        var result = _serializer.Serialize(usage);

        // Assert
        Assert.That(result, Is.EqualTo("Install Upgrade"));
    }

    [Test]
    public void Serialize_UsageMultipleFlags_ReturnsAllInOrder()
    {
        // Arrange
        var usage = AlpmDbUsage.ALPM_DB_USAGE_SYNC | AlpmDbUsage.ALPM_DB_USAGE_SEARCH | 
                     AlpmDbUsage.ALPM_DB_USAGE_INSTALL | AlpmDbUsage.ALPM_DB_USAGE_UPGRADE;

        // Act
        var result = _serializer.Serialize(usage);

        // Assert - When all flags are set, it equals ALPM_DB_USAGE_ALL
        Assert.That(result, Is.EqualTo("All"));
    }

    #endregion

    #region Serialize(AlpmSigLevel) Tests

    [Test]
    public void Serialize_SigLevelDefault_ReturnsEmptyString()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_USE_DEFAULT;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.Empty);
    }

    [Test]
    public void Serialize_SigLevelZero_ReturnsNever()
    {
        // Arrange
        var sigLevel = (AlpmSigLevel)0;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("Never"));
    }

    [Test]
    public void Serialize_SigLevelRequired_ReturnsRequired()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_DATABASE | AlpmSigLevel.ALPM_SIG_PACKAGE;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("Required"));
    }

    [Test]
    public void Serialize_SigLevelOptional_ReturnsOptional()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL | AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("Optional"));
    }

    [Test]
    public void Serialize_SigLevelPackageRequired_ReturnsPackageRequired()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_PACKAGE;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("PackageRequired"));
    }

    [Test]
    public void Serialize_SigLevelPackageOptional_ReturnsPackageOptional()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("PackageOptional"));
    }

    [Test]
    public void Serialize_SigLevelDatabaseRequired_ReturnsDatabaseRequired()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_DATABASE;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("DatabaseRequired"));
    }

    [Test]
    public void Serialize_SigLevelDatabaseOptional_ReturnsDatabaseOptional()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("DatabaseOptional"));
    }

    [Test]
    public void Serialize_SigLevelRequiredWithTrustAll_ReturnsRequiredTrustAll()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_DATABASE | AlpmSigLevel.ALPM_SIG_PACKAGE |
                       AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK | AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("Required TrustAll"));
    }

    [Test]
    public void Serialize_SigLevelPackageRequiredWithPackageTrustAll_ReturnsPackageRequiredPackageTrustAll()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_PACKAGE | AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("PackageRequired PackageTrustAll"));
    }

    [Test]
    public void Serialize_SigLevelDatabaseRequiredWithDatabaseTrustAll_ReturnsDatabaseRequiredDatabaseTrustAll()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_DATABASE | AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("DatabaseRequired DatabaseTrustAll"));
    }

    [Test]
    public void Serialize_SigLevelPackageRequiredDatabaseOptional_ReturnsPackageRequiredDatabaseOptional()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_PACKAGE | AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("PackageRequired DatabaseOptional"));
    }

    [Test]
    public void Serialize_SigLevelPackageOptionalDatabaseRequired_ReturnsPackageOptionalDatabaseRequired()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL | AlpmSigLevel.ALPM_SIG_DATABASE;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("PackageOptional DatabaseRequired"));
    }

    [Test]
    public void Serialize_SigLevelOptionalWithTrustAll_ReturnsOptionalTrustAll()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL | AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL |
                       AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK | AlpmSigLevel.ALPM_SIG_PACKAGE_MARGINAL_OK;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("Optional TrustAll"));
    }

    [Test]
    public void Serialize_SigLevelPackageOptionalWithDatabaseTrustAll_ReturnsPackageOptionalDatabaseTrustAll()
    {
        // Arrange
        var sigLevel = AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL | AlpmSigLevel.ALPM_SIG_DATABASE_MARGINAL_OK;

        // Act
        var result = _serializer.Serialize(sigLevel);

        // Assert
        Assert.That(result, Is.EqualTo("PackageOptional DatabaseTrustAll"));
    }

    #endregion
}


