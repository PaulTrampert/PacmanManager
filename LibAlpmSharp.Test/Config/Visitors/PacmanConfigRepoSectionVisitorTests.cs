using Antlr4.Runtime;
using LibAlpmSharp.Config.Visitors;
using LibAlpmSharp.Interop;
using LibAlpmSharp.Test.Config.Listeners;
using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Test.Config.Visitors;

[TestFixture]
public class PacmanConfigRepoSectionVisitorTests
{
    private ILogger _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _logger = LoggerFactory.Create(builder =>
                builder.AddProvider(new TestOutputLoggerProvider()).SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<PacmanConfigRepoSectionVisitor>();
    }

    private PacmanConfParser.SectionContext GetSectionContext(string input)
    {
        using var reader = new StringReader(input);
        var inputStream = new AntlrInputStream(reader);
        var lexer = new PacmanConfLexer(inputStream);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new PacmanConfParser(tokenStream);
        parser.AddErrorListener(new TestFailingErrorListener());
        return parser.section();
    }

    [Test]
    public void VisitSection_WithSectionHeader_SetsNameProperty()
    {
        // Arrange
        var input = "[core]\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act
        var result = visitor.Visit(context);
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("core"));
    }

    [Test]
    public void VisitSection_WithMultipleServerSettings_AggregatesWithUnion()
    {
        // Arrange
        var input = "[extra]\n" +
                    "Server=https://mirror1.example.com/$repo/os/$arch\n" +
                    "Server=https://mirror2.example.com/$repo/os/$arch\n" +
                    "Server=https://mirror3.example.com/$repo/os/$arch\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act
        var result = visitor.Visit(context);
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("extra"));
        Assert.That(result.Server.Count(), Is.EqualTo(3));
        Assert.That(result.Server, Does.Contain("https://mirror1.example.com/$repo/os/$arch"));
        Assert.That(result.Server, Does.Contain("https://mirror2.example.com/$repo/os/$arch"));
        Assert.That(result.Server, Does.Contain("https://mirror3.example.com/$repo/os/$arch"));
    }

    [Test]
    public void VisitSection_WithDuplicateServerSettings_UnionPreventsDuplicates()
    {
        // Arrange
        var input = "[community]\n" +
                    "Server=https://mirror.example.com/$repo/os/$arch\n" +
                    "Server=https://mirror.example.com/$repo/os/$arch\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act
        var result = visitor.Visit(context);
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Server.Count(), Is.EqualTo(1));
        Assert.That(result.Server.First(), Is.EqualTo("https://mirror.example.com/$repo/os/$arch"));
    }

    [Test]
    public void VisitSection_WithMultipleSigLevelSettings_TakesLatest()
    {
        // Arrange
        var input = "[core]\n" +
                    "SigLevel=Required DatabaseOptional\n" +
                    "SigLevel=Optional\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act
        var result = visitor.Visit(context);
        // Assert

        Assert.That(result, Is.Not.Null);
        Assert.That(result.SigLevel,
            Is.EqualTo(AlpmSigLevel.ALPM_SIG_DATABASE_OPTIONAL | AlpmSigLevel.ALPM_SIG_PACKAGE_OPTIONAL));
    }

    [Test]
    public void VisitSection_WithSingleSigLevel_SetsSigLevelProperty()
    {
        // Arrange
        var input = "[extra]\n" +
                    "SigLevel=Required\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act
        var result = visitor.Visit(context);
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.SigLevel, Is.EqualTo(AlpmSigLevel.ALPM_SIG_DATABASE | AlpmSigLevel.ALPM_SIG_PACKAGE));
    }

    [Test]
    public void VisitSection_WithMultipleCacheServerSettings_AggregatesWithUnion()
    {
        // Arrange
        var input = "[multilib]\n" +
                    "CacheServer=https://cache1.example.com\n" +
                    "CacheServer=https://cache2.example.com\n" +
                    "CacheServer=https://cache3.example.com\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act
        var result = visitor.Visit(context);
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("multilib"));
        Assert.That(result.CacheServer.Count(), Is.EqualTo(3));
        Assert.That(result.CacheServer, Does.Contain("https://cache1.example.com"));
        Assert.That(result.CacheServer, Does.Contain("https://cache2.example.com"));
        Assert.That(result.CacheServer, Does.Contain("https://cache3.example.com"));
    }

    [Test]
    public void VisitSection_WithDuplicateCacheServerSettings_UnionPreventsDuplicates()
    {
        // Arrange
        var input = "[testing]\n" +
                    "CacheServer=https://cache.example.com\n" +
                    "CacheServer=https://cache.example.com\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act
        var result = visitor.Visit(context);
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.CacheServer.Count(), Is.EqualTo(1));
        Assert.That(result.CacheServer.First(), Is.EqualTo("https://cache.example.com"));
    }

    [Test]
    public void VisitSection_WithMultipleUsageSettings_TakesLatest()
    {
        // Arrange
        var input = "[core]\n" +
                    "Usage=Sync\n" +
                    "Usage=Search\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act
        var result = visitor.Visit(context);
        // Assert
        Assert.That(result, Is.Not.Null);
        // The latest Usage directive should be applied
        Assert.That(result.Usage, Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_SEARCH));
    }

    [Test]
    public void VisitSection_WithSingleUsage_SetsUsageProperty()
    {
        // Arrange
        var input = "[extra]\n" +
                    "Usage=Sync Search\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act
        var result = visitor.Visit(context);
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Usage, Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_SYNC | AlpmDbUsage.ALPM_DB_USAGE_SEARCH));
    }

    [Test]
    public void VisitSection_WithUnsupportedKey_ThrowsNotSupportedException()
    {
        // Arrange
        // RootDir is a valid setting key but not supported in repository sections
        var input = "[core]\n" +
                    "RootDir=/custom/root\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act & Assert
        var ex = Assert.Throws<NotSupportedException>(() => visitor.Visit(context));
        Assert.That(ex!.Message, Does.Contain("RootDir"));
    }

    [Test]
    public void VisitSection_WithMixedSettings_ProcessesAllCorrectly()
    {
        // Arrange
        var input = "[community]\n" +
                    "Server=https://mirror1.example.com/$repo/os/$arch\n" +
                    "SigLevel=Required\n" +
                    "Server=https://mirror2.example.com/$repo/os/$arch\n" +
                    "CacheServer=https://cache.example.com\n" +
                    "Usage=Sync Search\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act
        var result = visitor.Visit(context);
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("community"));
        Assert.That(result.Server.Count(), Is.EqualTo(2));
        Assert.That(result.CacheServer.Count(), Is.EqualTo(1));
        Assert.That(result.SigLevel, Is.EqualTo(AlpmSigLevel.ALPM_SIG_DATABASE | AlpmSigLevel.ALPM_SIG_PACKAGE));
        Assert.That(result.Usage, Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_SYNC | AlpmDbUsage.ALPM_DB_USAGE_SEARCH));
    }

    [Test]
    public void VisitSection_WithEmptySection_ReturnsConfigWithNameOnly()
    {
        // Arrange
        var input = "[minimal]\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act
        var result = visitor.Visit(context);
        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("minimal"));
        Assert.That(result.Server, Is.Empty);
        Assert.That(result.CacheServer, Is.Empty);
        Assert.That(result.SigLevel, Is.EqualTo(AlpmSigLevel.ALPM_SIG_USE_DEFAULT));
        Assert.That(result.Usage, Is.EqualTo(AlpmDbUsage.ALPM_DB_USAGE_ALL));
    }

    [Test]
    public void VisitSection_ServerAggregationPreservesOrder()
    {
        // Arrange
        var input = "[core]\n" +
                    "Server=https://first.example.com\n" +
                    "Server=https://second.example.com\n" +
                    "Server=https://third.example.com\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act
        var result = visitor.Visit(context);
        // Assert
        Assert.That(result, Is.Not.Null);
        var servers = result.Server.ToList();
        Assert.That(servers, Has.Count.EqualTo(3));
        Assert.That(servers[0], Is.EqualTo("https://first.example.com"));
        Assert.That(servers[1], Is.EqualTo("https://second.example.com"));
        Assert.That(servers[2], Is.EqualTo("https://third.example.com"));
    }

    [Test]
    public void VisitSection_CacheServerAggregationPreservesOrder()
    {
        // Arrange
        var input = "[extra]\n" +
                    "CacheServer=https://cache1.example.com\n" +
                    "CacheServer=https://cache2.example.com\n" +
                    "CacheServer=https://cache3.example.com\n";
        var context = GetSectionContext(input);
        var visitor = new PacmanConfigRepoSectionVisitor(_logger);
        // Act
        var result = visitor.Visit(context);
        // Assert
        Assert.That(result, Is.Not.Null);
        var cacheServers = result.CacheServer.ToList();
        Assert.That(cacheServers, Has.Count.EqualTo(3));
        Assert.That(cacheServers[0], Is.EqualTo("https://cache1.example.com"));
        Assert.That(cacheServers[1], Is.EqualTo("https://cache2.example.com"));
        Assert.That(cacheServers[2], Is.EqualTo("https://cache3.example.com"));
    }
}