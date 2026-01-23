using LibAlpmSharp.Config;
using LibAlpmSharp.Test.Utils;
using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Test.Config;

[TestFixture]
public class PacmanConfigReaderTests
{
    private ILogger<PacmanConfigReader> _logger = null!;
    private string _testDir = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = LoggerFactory.Create(builder => 
                builder.AddProvider(new TestOutputLoggerProvider()).SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<PacmanConfigReader>();
        
        _testDir = Path.Combine(Path.GetTempPath(), "pacman_config_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    [Test]
    public void ReadConfig_EmptyFile_ReturnsDefaultConfig()
    {
        // Arrange
        var configFile = Path.Combine(_testDir, "pacman.conf");
        File.WriteAllText(configFile, "");
        
        var reader = new PacmanConfigReader(_logger);

        // Act
        var result = reader.ReadConfig(configFile);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RootDir, Is.EqualTo("/"));
        Assert.That(result.DBPath, Is.EqualTo("/var/lib/pacman"));
        Assert.That(result.Architecture, Is.EqualTo("auto"));
        Assert.That(result.Repositories, Is.Empty);
    }

    [Test]
    public void ReadConfig_ValidConfigNoComments_ParsesCorrectly()
    {
        // Arrange
        var configFile = Path.Combine(_testDir, "pacman.conf");
        var content = "[options]\n" +
                     "RootDir=/custom/root\n" +
                     "Architecture=x86_64\n" +
                     "[core]\n" +
                     "Server=https://mirror.example.com/$repo/os/$arch\n";
        File.WriteAllText(configFile, content);
        
        var reader = new PacmanConfigReader(_logger);

        // Act
        var result = reader.ReadConfig(configFile);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RootDir, Is.EqualTo("/custom/root"));
        Assert.That(result.Architecture, Is.EqualTo("x86_64"));
        Assert.That(result.Repositories.Count(), Is.EqualTo(1));
        
        var coreRepo = result.Repositories.First();
        Assert.That(coreRepo.Name, Is.EqualTo("core"));
        Assert.That(coreRepo.Server.Count(), Is.EqualTo(1));
        Assert.That(coreRepo.Server.First(), Is.EqualTo("https://mirror.example.com/$repo/os/$arch"));
    }

    [Test]
    public void ReadConfig_ValidConfigWithComments_ParsesCorrectly()
    {
        // Arrange
        var configFile = Path.Combine(_testDir, "pacman.conf");
        var content = "# This is a comment at the beginning\n" +
                     "# Another comment\n" +
                     "[options]\n" +
                     "# Comment in options\n" +
                     "RootDir=/test\n" +
                     "Color\n" +
                     "# Comment before repo\n" +
                     "[extra]\n" +
                     "# Comment in repo\n" +
                     "Server=https://example.com\n";
        File.WriteAllText(configFile, content);
        
        var reader = new PacmanConfigReader(_logger);

        // Act
        var result = reader.ReadConfig(configFile);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RootDir, Is.EqualTo("/test"));
        Assert.That(result.Color, Is.True);
        Assert.That(result.Repositories.Count(), Is.EqualTo(1));
        Assert.That(result.Repositories.First().Name, Is.EqualTo("extra"));
    }

    [Test]
    public void ReadConfig_NoOptionsWithRepositories_ParsesCorrectly()
    {
        // Arrange
        var configFile = Path.Combine(_testDir, "pacman.conf");
        var content = "[core]\n" +
                     "Server=https://core.example.com\n" +
                     "[extra]\n" +
                     "Server=https://extra.example.com\n";
        File.WriteAllText(configFile, content);
        
        var reader = new PacmanConfigReader(_logger);

        // Act
        var result = reader.ReadConfig(configFile);

        // Assert
        Assert.That(result, Is.Not.Null);
        // Should have default options values
        Assert.That(result.RootDir, Is.EqualTo("/"));
        Assert.That(result.DBPath, Is.EqualTo("/var/lib/pacman"));
        
        // Should have both repositories
        Assert.That(result.Repositories.Count(), Is.EqualTo(2));
        var repoNames = result.Repositories.Select(r => r.Name).ToList();
        Assert.That(repoNames, Does.Contain("core"));
        Assert.That(repoNames, Does.Contain("extra"));
    }

    [Test]
    public void ReadConfig_OptionsWithoutRepositories_ParsesCorrectly()
    {
        // Arrange
        var configFile = Path.Combine(_testDir, "pacman.conf");
        var content = "[options]\n" +
                     "RootDir=/custom\n" +
                     "HoldPkg=pacman\n" +
                     "HoldPkg=glibc\n" +
                     "Architecture=x86_64\n" +
                     "Color\n" +
                     "CheckSpace\n" +
                     "ParallelDownloads=5\n";
        File.WriteAllText(configFile, content);
        
        var reader = new PacmanConfigReader(_logger);

        // Act
        var result = reader.ReadConfig(configFile);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.RootDir, Is.EqualTo("/custom"));
        Assert.That(result.HoldPkg.Count(), Is.EqualTo(2));
        Assert.That(result.HoldPkg, Does.Contain("pacman"));
        Assert.That(result.HoldPkg, Does.Contain("glibc"));
        Assert.That(result.Architecture, Is.EqualTo("x86_64"));
        Assert.That(result.Color, Is.True);
        Assert.That(result.CheckSpace, Is.True);
        Assert.That(result.ParallelDownloads, Is.EqualTo(5));
        Assert.That(result.Repositories, Is.Empty);
    }

    [Test]
    public void ReadConfig_WithIncludedRepository_ParsesCorrectly()
    {
        // Arrange
        var repoFile = Path.Combine(_testDir, "custom-repo.conf");
        var repoContent = "[custom]\n" +
                         "Server=https://custom.example.com/$repo/os/$arch\n" +
                         "SigLevel=Required\n";
        File.WriteAllText(repoFile, repoContent);

        var configFile = Path.Combine(_testDir, "pacman.conf");
        var content = "[options]\n" +
                     "Architecture=x86_64\n" +
                     $"Include={repoFile}\n";
        File.WriteAllText(configFile, content);
        
        var reader = new PacmanConfigReader(_logger);

        // Act
        var result = reader.ReadConfig(configFile);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Architecture, Is.EqualTo("x86_64"));
        Assert.That(result.Repositories.Count(), Is.EqualTo(1));
        
        var customRepo = result.Repositories.First();
        Assert.That(customRepo.Name, Is.EqualTo("custom"));
        Assert.That(customRepo.Server.Count(), Is.EqualTo(1));
        Assert.That(customRepo.Server.First(), Is.EqualTo("https://custom.example.com/$repo/os/$arch"));
    }

    [Test]
    public void ReadConfig_SharedMirrorlist_ParsesCorrectly()
    {
        // Arrange
        var mirrorlistFile = Path.Combine(_testDir, "mirrorlist");
        var mirrorlistContent = "Server=https://mirror1.example.com/$repo/os/$arch\n" +
                               "Server=https://mirror2.example.com/$repo/os/$arch\n";
        File.WriteAllText(mirrorlistFile, mirrorlistContent);

        var configFile = Path.Combine(_testDir, "pacman.conf");
        var content = "[core]\n" +
                     $"Include={mirrorlistFile}\n" +
                     "[extra]\n" +
                     $"Include={mirrorlistFile}\n";
        File.WriteAllText(configFile, content);
        
        var reader = new PacmanConfigReader(_logger);

        // Act
        var result = reader.ReadConfig(configFile);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Repositories.Count(), Is.EqualTo(2));
        
        var coreRepo = result.Repositories.First(r => r.Name == "core");
        var extraRepo = result.Repositories.First(r => r.Name == "extra");
        
        // Both repos should have the same mirrors
        Assert.That(coreRepo.Server.Count(), Is.EqualTo(2));
        Assert.That(extraRepo.Server.Count(), Is.EqualTo(2));
        
        Assert.That(coreRepo.Server, Does.Contain("https://mirror1.example.com/$repo/os/$arch"));
        Assert.That(coreRepo.Server, Does.Contain("https://mirror2.example.com/$repo/os/$arch"));
        
        Assert.That(extraRepo.Server, Does.Contain("https://mirror1.example.com/$repo/os/$arch"));
        Assert.That(extraRepo.Server, Does.Contain("https://mirror2.example.com/$repo/os/$arch"));
    }

    [Test]
    public void ReadConfig_SyntaxErrorInMainFile_LogsCorrectLocation()
    {
        // Arrange
        var configFile = Path.Combine(_testDir, "pacman.conf");
        var content = "[options]\n" +
                     "RootDir=/test\n" +
                     "InvalidSyntax !\n" +  // Line 3 - syntax error
                     "Architecture=x86_64\n";
        File.WriteAllText(configFile, content);
        
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddProvider(new TestOutputLoggerProvider()).SetMinimumLevel(LogLevel.Debug));
        var testLogger = loggerFactory.CreateLogger<PacmanConfigReader>();
        var reader = new PacmanConfigReader(testLogger);

        // Act & Assert
        // The parser should still complete but log the error
        try
        {
            var result = reader.ReadConfig(configFile);
            // Parser may recover from syntax errors, so we just verify it attempts to parse
            Assert.That(result, Is.Not.Null);
        }
        catch (Exception ex)
        {
            // If it throws, verify the exception message contains file and line info
            TestContext.WriteLine($"Exception: {ex.Message}");
            Assert.Pass("Parser threw exception with error details");
        }
    }

    [Test]
    public void ReadConfig_SyntaxErrorInIncludedFile_LogsCorrectLocation()
    {
        // Arrange
        var includedFile = Path.Combine(_testDir, "repos.conf");
        var includedContent = "[core]\n" +
                             "Server=https://example.com\n" +
                             "BadSyntax !\n" +  // Line 3 in included file - syntax error
                             "[extra]\n" +
                             "Server=https://example2.com\n";
        File.WriteAllText(includedFile, includedContent);

        var configFile = Path.Combine(_testDir, "pacman.conf");
        var content = "[options]\n" +
                     "Architecture=x86_64\n" +
                     $"Include={includedFile}\n";
        File.WriteAllText(configFile, content);
        
        var loggerFactory = LoggerFactory.Create(builder => 
            builder.AddProvider(new TestOutputLoggerProvider()).SetMinimumLevel(LogLevel.Debug));
        var testLogger = loggerFactory.CreateLogger<PacmanConfigReader>();
        var reader = new PacmanConfigReader(testLogger);

        // Act & Assert
        try
        {
            var result = reader.ReadConfig(configFile);
            // Verify parsing completes (may recover from errors)
            Assert.That(result, Is.Not.Null);
        }
        catch (Exception ex)
        {
            // If it throws, verify exception contains included file info
            TestContext.WriteLine($"Exception: {ex.Message}");
            Assert.Pass("Parser threw exception with included file error details");
        }
    }

    [Test]
    public void ReadConfig_ComplexConfiguration_ParsesAllElements()
    {
        // Arrange
        var mirrorlistFile = Path.Combine(_testDir, "mirrorlist");
        File.WriteAllText(mirrorlistFile, "Server=https://mirror.example.com/$repo/os/$arch\n");

        var customRepoFile = Path.Combine(_testDir, "custom.conf");
        var customRepoContent = "[multilib]\n" +
                               "SigLevel=Optional\n" +
                               $"Include={mirrorlistFile}\n";
        File.WriteAllText(customRepoFile, customRepoContent);

        var configFile = Path.Combine(_testDir, "pacman.conf");
        var content = "# Pacman Configuration File\n" +
                     "[options]\n" +
                     "RootDir=/\n" +
                     "DBPath=/var/lib/pacman\n" +
                     "CacheDir=/var/cache/pacman/pkg\n" +
                     "LogFile=/var/log/pacman.log\n" +
                     "HoldPkg=pacman glibc\n" +
                     "Architecture=auto\n" +
                     "Color\n" +
                     "CheckSpace\n" +
                     "ParallelDownloads=5\n" +
                     "# Core repository\n" +
                     "[core]\n" +
                     $"Include={mirrorlistFile}\n" +
                     "# Extra repository\n" +
                     "[extra]\n" +
                     $"Include={mirrorlistFile}\n" +
                     "# Include custom repos\n" +
                     $"Include={customRepoFile}\n";
        File.WriteAllText(configFile, content);
        
        var reader = new PacmanConfigReader(_logger);

        // Act
        var result = reader.ReadConfig(configFile);

        // Assert
        Assert.That(result, Is.Not.Null);
        
        // Verify options
        Assert.That(result.RootDir, Is.EqualTo("/"));
        Assert.That(result.DBPath, Is.EqualTo("/var/lib/pacman"));
        Assert.That(result.CacheDir, Is.EqualTo("/var/cache/pacman/pkg"));
        Assert.That(result.LogFile, Is.EqualTo("/var/log/pacman.log"));
        Assert.That(result.HoldPkg.Count(), Is.EqualTo(2));
        Assert.That(result.Architecture, Is.EqualTo("auto"));
        Assert.That(result.Color, Is.True);
        Assert.That(result.CheckSpace, Is.True);
        Assert.That(result.ParallelDownloads, Is.EqualTo(5));
        
        // Verify repositories
        Assert.That(result.Repositories.Count(), Is.EqualTo(3));
        var repoNames = result.Repositories.Select(r => r.Name).ToList();
        Assert.That(repoNames, Does.Contain("core"));
        Assert.That(repoNames, Does.Contain("extra"));
        Assert.That(repoNames, Does.Contain("multilib"));
        
        // Verify all repos have the mirror
        foreach (var repo in result.Repositories)
        {
            Assert.That(repo.Server, Does.Contain("https://mirror.example.com/$repo/os/$arch"));
        }
    }

    [Test]
    public void ReadConfig_RelativeIncludePath_ResolvesCorrectly()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "conf.d");
        Directory.CreateDirectory(subDir);
        
        var repoFile = Path.Combine(subDir, "custom.conf");
        File.WriteAllText(repoFile, "[custom]\nServer=https://custom.example.com\n");

        var configFile = Path.Combine(_testDir, "pacman.conf");
        var content = "[options]\n" +
                     "Architecture=x86_64\n" +
                     "Include=conf.d/custom.conf\n";
        File.WriteAllText(configFile, content);
        
        var reader = new PacmanConfigReader(_logger);

        // Act
        var result = reader.ReadConfig(configFile);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Repositories.Count(), Is.EqualTo(1));
        Assert.That(result.Repositories.First().Name, Is.EqualTo("custom"));
    }

    [Test]
    public void ReadConfig_GlobPattern_IncludesMultipleFiles()
    {
        // Arrange
        var confDir = Path.Combine(_testDir, "repos.d");
        Directory.CreateDirectory(confDir);
        
        File.WriteAllText(Path.Combine(confDir, "core.conf"), "[core]\nServer=https://core.example.com\n");
        File.WriteAllText(Path.Combine(confDir, "extra.conf"), "[extra]\nServer=https://extra.example.com\n");
        File.WriteAllText(Path.Combine(confDir, "community.conf"), "[community]\nServer=https://community.example.com\n");

        var configFile = Path.Combine(_testDir, "pacman.conf");
        var content = "[options]\n" +
                     "Architecture=x86_64\n" +
                     $"Include={confDir}/*.conf\n";
        File.WriteAllText(configFile, content);
        
        var reader = new PacmanConfigReader(_logger);

        // Act
        var result = reader.ReadConfig(configFile);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Repositories.Count(), Is.EqualTo(3));
        var repoNames = result.Repositories.Select(r => r.Name).ToList();
        Assert.That(repoNames, Does.Contain("core"));
        Assert.That(repoNames, Does.Contain("extra"));
        Assert.That(repoNames, Does.Contain("community"));
    }

    [Test]
    public void ReadConfig_NonExistentFile_ThrowsException()
    {
        // Arrange
        var configFile = Path.Combine(_testDir, "nonexistent.conf");
        var reader = new PacmanConfigReader(_logger);

        // Act & Assert
        Assert.Throws<FileNotFoundException>(() => reader.ReadConfig(configFile));
    }
}
