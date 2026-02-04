using Antlr4.Runtime;
using LibAlpmSharp.Config.Visitors;
using LibAlpmSharp.Interop;
using LibAlpmSharp.Test.Config.Listeners;
using PacmanManager.TestUtils;
using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Test.Config.Visitors;

[TestFixture]
public class PacmanConfigVisitorTests
{
    private ILogger _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = LoggerFactory.Create(builder => 
                builder.AddProvider(new TestOutputLoggerProvider()).SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<PacmanConfigVisitor>();
    }

    private PacmanConfParser.PacmanConfContext GetPacmanConfContext(string input)
    {
        using var reader = new StringReader(input);
        var inputStream = new AntlrInputStream(reader);
        var lexer = new PacmanConfLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new PacmanConfParser(tokenStream);
        parser.AddErrorListener(new TestFailingErrorListener());
        return parser.pacmanConf();
    }

    #region Single-Value String Properties

    [Test]
    public void VisitSetting_RootDir_SetsProperty()
    {
        // Arrange
        var input = "[options]\nRootDir=/custom/root\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RootDir, Is.EqualTo("/custom/root"));
    }

    [Test]
    public void VisitSetting_DbPath_SetsProperty()
    {
        // Arrange
        var input = "[options]\nDBPath=/custom/db\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.DBPath, Is.EqualTo("/custom/db"));
    }

    [Test]
    public void VisitSetting_CacheDir_SetsProperty()
    {
        // Arrange
        var input = "[options]\nCacheDir=/custom/cache\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.CacheDir, Is.EqualTo("/custom/cache"));
    }

    [Test]
    public void VisitSetting_HookDir_SetsProperty()
    {
        // Arrange
        var input = "[options]\nHookDir=/custom/hooks\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.HookDir, Is.EqualTo("/custom/hooks"));
    }

    [Test]
    public void VisitSetting_GPGDir_SetsProperty()
    {
        // Arrange
        var input = "[options]\nGPGDir=/custom/gpg\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.GPGDir, Is.EqualTo("/custom/gpg"));
    }

    [Test]
    public void VisitSetting_LogFile_SetsProperty()
    {
        // Arrange
        var input = "[options]\nLogFile=/custom/log\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.LogFile, Is.EqualTo("/custom/log"));
    }

    [Test]
    public void VisitSetting_Architecture_SetsProperty()
    {
        // Arrange
        var input = "[options]\nArchitecture=x86_64\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Architecture, Is.EqualTo("x86_64"));
    }

    [Test]
    public void VisitSetting_XferCommand_SetsProperty()
    {
        // Arrange
        var input = "[options]\nXferCommand=/usr/bin/curl -L -C - -f -o %o %u\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.XferCommand, Is.EqualTo("/usr/bin/curl"));
    }

    [Test]
    public void VisitSetting_CleanMethod_SetsProperty()
    {
        // Arrange
        var input = "[options]\nCleanMethod=KeepCurrent\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.CleanMethod, Is.EqualTo("KeepCurrent"));
    }

    [Test]
    public void VisitSetting_DownloadUser_SetsProperty()
    {
        // Arrange
        var input = "[options]\nDownloadUser=alpm\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.DownloadUser, Is.EqualTo("alpm"));
    }

    #endregion

    #region Single-Value Properties - Latest Wins

    [Test]
    public void VisitSetting_RootDir_LatestWins()
    {
        // Arrange
        var input = "[options]\nRootDir=/first\nRootDir=/second\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RootDir, Is.EqualTo("/second"));
    }

    [Test]
    public void VisitSetting_Architecture_LatestWins()
    {
        // Arrange
        var input = "[options]\nArchitecture=i686\nArchitecture=x86_64\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Architecture, Is.EqualTo("x86_64"));
    }

    #endregion

    #region Multi-Value Properties - Union

    [Test]
    public void VisitSetting_HoldPkg_AggregatesWithUnion()
    {
        // Arrange
        var input = "[options]\nHoldPkg=pacman\nHoldPkg=glibc\nHoldPkg=bash\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.HoldPkg.Count(), Is.EqualTo(3));
        Assert.That(result.HoldPkg, Does.Contain("pacman"));
        Assert.That(result.HoldPkg, Does.Contain("glibc"));
        Assert.That(result.HoldPkg, Does.Contain("bash"));
    }

    [Test]
    public void VisitSetting_HoldPkg_UnionPreventsDuplicates()
    {
        // Arrange
        var input = "[options]\nHoldPkg=pacman\nHoldPkg=pacman\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.HoldPkg.Count(), Is.EqualTo(1));
        Assert.That(result.HoldPkg.First(), Is.EqualTo("pacman"));
    }

    [Test]
    public void VisitSetting_IgnorePkg_AggregatesWithUnion()
    {
        // Arrange
        var input = "[options]\nIgnorePkg=pkg1\nIgnorePkg=pkg2\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IgnorePkg.Count(), Is.EqualTo(2));
        Assert.That(result.IgnorePkg, Does.Contain("pkg1"));
        Assert.That(result.IgnorePkg, Does.Contain("pkg2"));
    }

    [Test]
    public void VisitSetting_IgnoreGroup_AggregatesWithUnion()
    {
        // Arrange
        var input = "[options]\nIgnoreGroup=base-devel\nIgnoreGroup=kde\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.IgnoreGroup.Count(), Is.EqualTo(2));
        Assert.That(result.IgnoreGroup, Does.Contain("base-devel"));
        Assert.That(result.IgnoreGroup, Does.Contain("kde"));
    }

    [Test]
    public void VisitSetting_NoUpgrade_AggregatesWithUnion()
    {
        // Arrange
        var input = "[options]\nNoUpgrade=etc/pacman.conf\nNoUpgrade=etc/fstab\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.NoUpgrade.Count(), Is.EqualTo(2));
        Assert.That(result.NoUpgrade, Does.Contain("etc/pacman.conf"));
        Assert.That(result.NoUpgrade, Does.Contain("etc/fstab"));
    }

    [Test]
    public void VisitSetting_NoExtract_AggregatesWithUnion()
    {
        // Arrange
        var input = "[options]\nNoExtract=usr/share/man/*\nNoExtract=usr/share/doc/*\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.NoExtract.Count(), Is.EqualTo(2));
        Assert.That(result.NoExtract, Does.Contain("usr/share/man/*"));
        Assert.That(result.NoExtract, Does.Contain("usr/share/doc/*"));
    }

    #endregion

    #region SigLevel Properties

    [Test]
    public void VisitSetting_SigLevel_SetsProperty()
    {
        // Arrange
        var input = "[options]\nSigLevel=Required\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.SigLevel, Is.Not.EqualTo(AlpmSigLevel.ALPM_SIG_USE_DEFAULT));
    }

    [Test]
    public void VisitSetting_SigLevel_LatestWins()
    {
        // Arrange
        var input = "[options]\nSigLevel=Required\nSigLevel=Never\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That((int)result.SigLevel, Is.EqualTo(0)); // Never = 0
    }

    [Test]
    public void VisitSetting_LocalFileSigLevel_SetsProperty()
    {
        // Arrange
        var input = "[options]\nLocalFileSigLevel=Optional\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.LocalFileSigLevel, Is.Not.EqualTo(AlpmSigLevel.ALPM_SIG_USE_DEFAULT));
    }

    [Test]
    public void VisitSetting_RemoteFileSigLevel_SetsProperty()
    {
        // Arrange
        var input = "[options]\nRemoteFileSigLevel=Required\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RemoteFileSigLevel, Is.Not.EqualTo(AlpmSigLevel.ALPM_SIG_USE_DEFAULT));
    }

    #endregion

    #region Boolean Flag Properties

    [Test]
    public void VisitSetting_UseSyslog_SetsToTrue()
    {
        // Arrange
        var input = "[options]\nUseSyslog\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.UseSyslog, Is.True);
    }

    [Test]
    public void VisitSetting_Color_SetsToTrue()
    {
        // Arrange
        var input = "[options]\nColor\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Color, Is.True);
    }

    [Test]
    public void VisitSetting_NoProgressBar_SetsToTrue()
    {
        // Arrange
        var input = "[options]\nNoProgressBar\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.NoProgressBar, Is.True);
    }

    [Test]
    public void VisitSetting_CheckSpace_SetsToTrue()
    {
        // Arrange
        var input = "[options]\nCheckSpace\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.CheckSpace, Is.True);
    }

    [Test]
    public void VisitSetting_VerbosePkgLists_SetsToTrue()
    {
        // Arrange
        var input = "[options]\nVerbosePkgLists\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.VerbosePkgLists, Is.True);
    }

    [Test]
    public void VisitSetting_DisableDownloadTimeout_SetsToTrue()
    {
        // Arrange
        var input = "[options]\nDisableDownloadTimeout\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.DisableDownloadTimeout, Is.True);
    }

    [Test]
    public void VisitSetting_DisableSandbox_SetsToTrue()
    {
        // Arrange
        var input = "[options]\nDisableSandbox\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.DisableSandbox, Is.True);
    }

    [Test]
    public void VisitSetting_DisableSandboxFilesystem_SetsToTrue()
    {
        // Arrange
        var input = "[options]\nDisableSandboxFilesystem\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.DisableSandboxFilesystem, Is.True);
    }

    [Test]
    public void VisitSetting_DisableSandboxSyscalls_SetsToTrue()
    {
        // Arrange
        var input = "[options]\nDisableSandboxSyscalls\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.DisableSandboxSyscalls, Is.True);
    }

    #endregion

    #region Numeric Properties

    [Test]
    public void VisitSetting_ParallelDownloads_SetsProperty()
    {
        // Arrange
        var input = "[options]\nParallelDownloads=5\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ParallelDownloads, Is.EqualTo(5));
    }

    [Test]
    public void VisitSetting_ParallelDownloads_LatestWins()
    {
        // Arrange
        var input = "[options]\nParallelDownloads=3\nParallelDownloads=7\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.ParallelDownloads, Is.EqualTo(7));
    }

    [Test]
    public void VisitSetting_ParallelDownloads_InvalidValue_ThrowsArgumentException()
    {
        // Arrange
        var input = "[options]\nParallelDownloads=invalid\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => visitor.Visit(context));
        Assert.That(ex!.Message, Does.Contain("must be a valid integer"));
    }

    #endregion

    #region Missing Required Values

    [Test]
    public void VisitSetting_RootDir_MissingValue_ThrowsArgumentException()
    {
        // Arrange
        var input = "[options]\nRootDir\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => visitor.Visit(context));
        Assert.That(ex!.Message, Does.Contain("requires a value"));
    }

    [Test]
    public void VisitSetting_ParallelDownloads_MissingValue_ThrowsArgumentException()
    {
        // Arrange
        var input = "[options]\nParallelDownloads\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => visitor.Visit(context));
        Assert.That(ex!.Message, Does.Contain("requires a value"));
    }

    #endregion

    #region Unsupported Options

    [Test]
    public void VisitSetting_UnsupportedOption_ThrowsNotSupportedException()
    {
        // Arrange
        // Use a valid keyword that exists in the grammar but not supported in options section
        var input = "[options]\nServer=https://example.com\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act & Assert
        var ex = Assert.Throws<NotSupportedException>(() => visitor.Visit(context));
        Assert.That(ex!.Message, Does.Contain("Unsupported option"));
    }

    #endregion

    #region Repository Sections

    [Test]
    public void VisitSection_WithRepositorySection_AddsToRepositories()
    {
        // Arrange
        var input = "[core]\nServer=https://mirror.example.com/$repo/os/$arch\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Repositories.Count(), Is.EqualTo(1));
        var repo = result.Repositories.First();
        Assert.That(repo.Name, Is.EqualTo("core"));
        Assert.That(repo.Server.Count(), Is.EqualTo(1));
        Assert.That(repo.Server.First(), Is.EqualTo("https://mirror.example.com/$repo/os/$arch"));
    }

    [Test]
    public void VisitSection_WithMultipleRepositorySections_AddsAllToRepositories()
    {
        // Arrange
        var input = "[core]\nServer=https://core.example.com\n" +
                   "[extra]\nServer=https://extra.example.com\n" +
                   "[community]\nServer=https://community.example.com\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Repositories.Count(), Is.EqualTo(3));
        
        var repoNames = result.Repositories.Select(r => r.Name).ToList();
        Assert.That(repoNames, Does.Contain("core"));
        Assert.That(repoNames, Does.Contain("extra"));
        Assert.That(repoNames, Does.Contain("community"));
    }

    [Test]
    public void VisitSection_MixedOptionsAndRepositories_ProcessesBothCorrectly()
    {
        // Arrange
        var input = "[options]\n" +
                   "RootDir=/custom\n" +
                   "Color\n" +
                   "[core]\n" +
                   "Server=https://core.example.com\n" +
                   "[extra]\n" +
                   "Server=https://extra.example.com\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RootDir, Is.EqualTo("/custom"));
        Assert.That(result.Color, Is.True);
        Assert.That(result.Repositories.Count(), Is.EqualTo(2));
    }

    #endregion

    #region Complex Scenarios

    [Test]
    public void VisitSetting_MultipleOptionsOfDifferentTypes_AllSetCorrectly()
    {
        // Arrange
        var input = "[options]\n" +
                   "RootDir=/custom\n" +
                   "HoldPkg=pacman\n" +
                   "HoldPkg=glibc\n" +
                   "Color\n" +
                   "ParallelDownloads=5\n" +
                   "Architecture=x86_64\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RootDir, Is.EqualTo("/custom"));
        Assert.That(result.HoldPkg.Count(), Is.EqualTo(2));
        Assert.That(result.Color, Is.True);
        Assert.That(result.ParallelDownloads, Is.EqualTo(5));
        Assert.That(result.Architecture, Is.EqualTo("x86_64"));
    }

    [Test]
    public void VisitSetting_EmptyOptionsSection_ReturnsDefaultConfig()
    {
        // Arrange
        var input = "[options]\n";
        var context = GetPacmanConfContext(input);
        var visitor = new PacmanConfigVisitor(_logger);

        // Act
        var result = visitor.Visit(context);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Should have default values
        Assert.That(result.RootDir, Is.EqualTo("/"));
        Assert.That(result.DBPath, Is.EqualTo("/var/lib/pacman"));
        Assert.That(result.Architecture, Is.EqualTo("auto"));
    }

    #endregion
}
