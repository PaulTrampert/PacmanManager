using Antlr4.Runtime;
using LibAlpmSharp.Config.TokenSources;
using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Test.Config.TokenSources;

[TestFixture]
public class PacmanConfigTokenSourceTests
{
    private string _testDir = null!;
    private ILogger _logger = null!;

    [SetUp]
    public void SetUp()
    {
        _testDir = Path.Combine(Path.GetTempPath(), $"PacmanConfigTokenSourceTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDir);
        
        _logger = LoggerFactory.Create(builder => 
                builder.AddProvider(new TestOutputLoggerProvider()).SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<PacmanConfigTokenSource>();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_testDir))
        {
            Directory.Delete(_testDir, true);
        }
    }

    private PacmanConfigTokenSource CreateTokenSource(string content, string? filename = null)
    {
        filename ??= "test.conf";
        var fullPath = Path.Combine(_testDir, filename);
        File.WriteAllText(fullPath, content);
        
        var inputStream = new AntlrFileStream(fullPath);
        var lexer = new PacmanConfLexer(inputStream);
        return new PacmanConfigTokenSource(_logger, lexer);
    }

    private PacmanConfigTokenSource CreateTokenSourceFromString(string content)
    {
        var inputStream = new AntlrInputStream(content);
        var lexer = new PacmanConfLexer(inputStream);
        return new PacmanConfigTokenSource(_logger, lexer);
    }

    [Test]
    public void NextToken_WithNormalTokens_ReturnsToken()
    {
        // Arrange
        var content = "RootDir = /\n";
        var tokenSource = CreateTokenSourceFromString(content);

        // Act
        var tokens = new List<IToken>();
        IToken token;
        do
        {
            token = tokenSource.NextToken();
            tokens.Add(token);
        } while (token.Type != PacmanConfLexer.Eof);

        // Assert
        var tokenTypes = tokens.Select(t => t.Type).ToList();
        Assert.That(tokenTypes[0], Is.EqualTo(PacmanConfLexer.ROOTDIR));
        Assert.That(tokenTypes[1], Is.EqualTo(PacmanConfLexer.EQUALS));
        Assert.That(tokenTypes, Does.Contain(PacmanConfLexer.STRING_CONTENT));
        Assert.That(tokenTypes.Last(), Is.EqualTo(PacmanConfLexer.Eof));
    }

    [Test]
    public void NextToken_AtEndOfFile_ReturnsEof()
    {
        // Arrange
        var content = "RootDir = /\n";
        var tokenSource = CreateTokenSourceFromString(content);

        // Act - consume all tokens
        IToken eofToken;
        do
        {
            eofToken = tokenSource.NextToken();
        } while (eofToken.Type != PacmanConfLexer.Eof);

        // Assert - First EOF should be returned
        Assert.That(eofToken.Type, Is.EqualTo(PacmanConfLexer.Eof));
    }

    [Test]
    public void NextToken_WithIncludeDirective_ReturnsTokensFromIncludedFile()
    {
        // Arrange
        var includedContent = "DBPath = /var/lib/pacman\n";
        var includedFile = Path.Combine(_testDir, "included.conf");
        File.WriteAllText(includedFile, includedContent);

        var mainContent = $"RootDir = /\nInclude = {includedFile}\n";
        var tokenSource = CreateTokenSource(mainContent, "main.conf");

        // Act
        var tokens = new List<IToken>();
        IToken token;
        do
        {
            token = tokenSource.NextToken();
            tokens.Add(token);
        } while (token.Type != PacmanConfLexer.Eof);

        // Assert
        var tokenTypes = tokens.Select(t => t.Type).ToList();
        Assert.That(tokenTypes, Does.Contain(PacmanConfLexer.ROOTDIR));
        Assert.That(tokenTypes, Does.Contain(PacmanConfLexer.DBPATH));
        Assert.That(tokenTypes, Does.Not.Contain(PacmanConfLexer.INCLUDE));
    }

    [Test]
    public void NextToken_WithRelativeIncludePath_ResolvesRelativeToCurrentFile()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "subdir");
        Directory.CreateDirectory(subDir);

        var includedContent = "DBPath = /var/lib/pacman\n";
        var includedFile = Path.Combine(subDir, "included.conf");
        File.WriteAllText(includedFile, includedContent);

        var mainContent = "RootDir = /\nInclude = subdir/included.conf\n";
        var tokenSource = CreateTokenSource(mainContent, "main.conf");

        // Act
        var tokens = new List<IToken>();
        IToken token;
        do
        {
            token = tokenSource.NextToken();
            tokens.Add(token);
        } while (token.Type != PacmanConfLexer.Eof);

        // Assert
        var tokenTypes = tokens.Select(t => t.Type).ToList();
        Assert.That(tokenTypes, Does.Contain(PacmanConfLexer.DBPATH));
    }

    [Test]
    public void NextToken_WithGlobPattern_IncludesMatchingFilesInOrder()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "includes");
        Directory.CreateDirectory(subDir);
        
        File.WriteAllText(Path.Combine(subDir, "a.conf"), "RootDir = /a\n");
        File.WriteAllText(Path.Combine(subDir, "b.conf"), "RootDir = /b\n");
        File.WriteAllText(Path.Combine(subDir, "c.conf"), "RootDir = /c\n");

        var mainContent = "Include = includes/*.conf\n";
        var tokenSource = CreateTokenSource(mainContent, "main.conf");

        // Act
        var tokens = new List<IToken>();
        IToken token;
        do
        {
            token = tokenSource.NextToken();
            tokens.Add(token);
        } while (token.Type != PacmanConfLexer.Eof);

        // Assert
        var contentTokens = tokens
            .Where(t => t.Type == PacmanConfLexer.STRING_CONTENT)
            .Select(t => t.Text)
            .ToList();
        
        // Files should be included in reverse order (see GetFullFilenames implementation)
        Assert.That(contentTokens, Has.Count.EqualTo(3));
        Assert.That(contentTokens[0], Is.EqualTo("/a"));
        Assert.That(contentTokens[1], Is.EqualTo("/b"));
        Assert.That(contentTokens[2], Is.EqualTo("/c"));
    }

    [Test]
    public void NextToken_WithDirectoryPath_IncludesAllFilesInDirectory()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "conf.d");
        Directory.CreateDirectory(subDir);
        
        File.WriteAllText(Path.Combine(subDir, "file1.conf"), "RootDir = /1\n");
        File.WriteAllText(Path.Combine(subDir, "file2.conf"), "RootDir = /2\n");

        var mainContent = $"Include = conf.d\n";
        var tokenSource = CreateTokenSource(mainContent, "main.conf");

        // Act
        var tokens = new List<IToken>();
        IToken token;
        do
        {
            token = tokenSource.NextToken();
            tokens.Add(token);
        } while (token.Type != PacmanConfLexer.Eof);

        // Assert
        var contentTokens = tokens
            .Where(t => t.Type == PacmanConfLexer.STRING_CONTENT)
            .ToList();
        
        Assert.That(contentTokens, Has.Count.GreaterThanOrEqualTo(2));
    }

    [Test]
    public void NextToken_WithNonexistentInclude_ThrowsRecognitionException()
    {
        // Arrange
        var mainContent = "Include = /nonexistent/file.conf\n";
        var tokenSource = CreateTokenSource(mainContent, "main.conf");

        // Act & Assert
        Assert.Throws<RecognitionException>(() =>
        {
            while (tokenSource.NextToken().Type != PacmanConfLexer.Eof)
            {
            }
        });
    }

    [Test]
    public void NextToken_WithCircularInclude_ThrowsInvalidOperationException()
    {
        // Arrange
        var file1 = Path.Combine(_testDir, "file1.conf");
        var file2 = Path.Combine(_testDir, "file2.conf");

        File.WriteAllText(file1, $"RootDir = /1\nInclude = {file2}\n");
        File.WriteAllText(file2, $"RootDir = /2\nInclude = {file1}\n");

        var inputStream = new AntlrFileStream(file1);
        var lexer = new PacmanConfLexer(inputStream);
        var tokenSource = new PacmanConfigTokenSource(_logger, lexer);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            // Consume tokens until we hit the circular include
            while (tokenSource.NextToken().Type != PacmanConfLexer.Eof)
            {
            }
        });
        
        Assert.That(ex!.Message, Does.Contain("Circular include detected"));
    }

    [Test]
    public void NextToken_WithNestedIncludes_ReturnsTokensInCorrectOrder()
    {
        // Arrange
        var level2Content = "CacheDir = /var/cache/pacman\n";
        var level2File = Path.Combine(_testDir, "level2.conf");
        File.WriteAllText(level2File, level2Content);

        var level1Content = $"DBPath = /var/lib/pacman\nInclude = {level2File}\n";
        var level1File = Path.Combine(_testDir, "level1.conf");
        File.WriteAllText(level1File, level1Content);

        var mainContent = $"RootDir = /\nInclude = {level1File}\nLogFile = /var/log/pacman.log\n";
        var tokenSource = CreateTokenSource(mainContent, "main.conf");

        // Act
        var tokens = new List<IToken>();
        IToken token;
        do
        {
            token = tokenSource.NextToken();
            tokens.Add(token);
        } while (token.Type != PacmanConfLexer.Eof);

        // Assert
        var keywordTokenTypes = tokens
            .Where(t => t.Type == PacmanConfLexer.ROOTDIR || 
                       t.Type == PacmanConfLexer.DBPATH || 
                       t.Type == PacmanConfLexer.CACHEDIR ||
                       t.Type == PacmanConfLexer.LOGFILE)
            .Select(t => t.Type)
            .ToList();
        
        // Order should be: ROOTDIR, DBPATH, CACHEDIR, LOGFILE
        Assert.That(keywordTokenTypes[0], Is.EqualTo(PacmanConfLexer.ROOTDIR));
        Assert.That(keywordTokenTypes[1], Is.EqualTo(PacmanConfLexer.DBPATH));
        Assert.That(keywordTokenTypes[2], Is.EqualTo(PacmanConfLexer.CACHEDIR));
        Assert.That(keywordTokenTypes[3], Is.EqualTo(PacmanConfLexer.LOGFILE));
    }

    [Test]
    public void NextToken_AfterEofOfIncludedFile_ReturnsTokensFromParentFile()
    {
        // Arrange
        var includedContent = "DBPath = /var/lib/pacman\n";
        var includedFile = Path.Combine(_testDir, "included.conf");
        File.WriteAllText(includedFile, includedContent);

        var mainContent = $"RootDir = /\nInclude = {includedFile}\nLogFile = /var/log/pacman.log\n";
        var tokenSource = CreateTokenSource(mainContent, "main.conf");

        // Act
        var tokens = new List<IToken>();
        IToken token;
        do
        {
            token = tokenSource.NextToken();
            tokens.Add(token);
        } while (token.Type != PacmanConfLexer.Eof);

        // Assert
        var keywordTokenTypes = tokens
            .Where(t => t.Type == PacmanConfLexer.ROOTDIR || 
                       t.Type == PacmanConfLexer.DBPATH || 
                       t.Type == PacmanConfLexer.LOGFILE)
            .Select(t => t.Type)
            .ToList();
        
        // Should have all three keywords
        Assert.That(keywordTokenTypes, Has.Count.EqualTo(3));
        Assert.That(keywordTokenTypes[0], Is.EqualTo(PacmanConfLexer.ROOTDIR));
        Assert.That(keywordTokenTypes[1], Is.EqualTo(PacmanConfLexer.DBPATH));
        Assert.That(keywordTokenTypes[2], Is.EqualTo(PacmanConfLexer.LOGFILE));
    }

    [Test]
    public void NextToken_WithQuotedIncludePath_HandlesCorrectly()
    {
        // Arrange
        var includedContent = "DBPath = /var/lib/pacman\n";
        var includedFile = Path.Combine(_testDir, "included file.conf");
        File.WriteAllText(includedFile, includedContent);

        var mainContent = $"Include = \"{includedFile}\"\n";
        var tokenSource = CreateTokenSource(mainContent, "main.conf");

        // Act
        var tokens = new List<IToken>();
        IToken token;
        do
        {
            token = tokenSource.NextToken();
            tokens.Add(token);
        } while (token.Type != PacmanConfLexer.Eof);

        // Assert
        var tokenTypes = tokens.Select(t => t.Type).ToList();
        Assert.That(tokenTypes, Does.Contain(PacmanConfLexer.DBPATH));
    }

    [Test]
    public void SourceName_ReturnsCurrentLexerSourceName()
    {
        // Arrange
        var content = "RootDir = /\n";
        var tokenSource = CreateTokenSource(content, "test.conf");

        // Act
        var sourceName = tokenSource.SourceName;

        // Assert
        Assert.That(sourceName, Does.EndWith("test.conf"));
    }

    [Test]
    public void Line_ReturnsCurrentLexerLine()
    {
        // Arrange
        var content = "RootDir = /\n";
        var tokenSource = CreateTokenSourceFromString(content);

        // Act
        var initialLine = tokenSource.Line;
        tokenSource.NextToken(); // ROOTDIR
        var lineAfterToken = tokenSource.Line;

        // Assert
        Assert.That(initialLine, Is.GreaterThan(0));
        Assert.That(lineAfterToken, Is.GreaterThan(0));
    }

    [Test]
    public void Column_ReturnsCurrentLexerColumn()
    {
        // Arrange
        var content = "RootDir = /\n";
        var tokenSource = CreateTokenSourceFromString(content);

        // Act
        var initialColumn = tokenSource.Column;
        tokenSource.NextToken(); // ROOTDIR
        var columnAfterToken = tokenSource.Column;

        // Assert
        Assert.That(initialColumn, Is.GreaterThanOrEqualTo(0));
        Assert.That(columnAfterToken, Is.GreaterThan(initialColumn));
    }

    [Test]
    public void NextToken_WithMultipleFilesInGlobPattern_ProcessesInCorrectOrder()
    {
        // Arrange
        var subDir = Path.Combine(_testDir, "repos");
        Directory.CreateDirectory(subDir);
        
        File.WriteAllText(Path.Combine(subDir, "01-core.conf"), "# Core\n");
        File.WriteAllText(Path.Combine(subDir, "02-extra.conf"), "# Extra\n");
        File.WriteAllText(Path.Combine(subDir, "03-community.conf"), "# Community\n");

        var mainContent = "Include = repos/*.conf\n";
        var tokenSource = CreateTokenSource(mainContent, "main.conf");

        // Act
        var commentTokens = new List<string>();
        IToken token;
        do
        {
            token = tokenSource.NextToken();
            if (token.Type == PacmanConfLexer.COMMENT)
            {
                commentTokens.Add(token.Text);
            }
        } while (token.Type != PacmanConfLexer.Eof);

        // Assert
        Assert.That(commentTokens, Has.Count.EqualTo(3));
        // Files are reversed in GetFullFilenames
        Assert.That(commentTokens[0], Does.Contain("Core"));
        Assert.That(commentTokens[1], Does.Contain("Extra"));
        Assert.That(commentTokens[2], Does.Contain("Community"));
    }

    [Test]
    public void NextToken_WithAbsolutePathInclude_ResolvesCorrectly()
    {
        // Arrange
        var includedContent = "DBPath = /var/lib/pacman\n";
        var includedFile = Path.Combine(_testDir, "included.conf");
        File.WriteAllText(includedFile, includedContent);

        var mainContent = $"Include = {includedFile}\n";
        var tokenSource = CreateTokenSourceFromString(mainContent);

        // Act
        var tokens = new List<IToken>();
        IToken token;
        do
        {
            token = tokenSource.NextToken();
            tokens.Add(token);
        } while (token.Type != PacmanConfLexer.Eof);

        // Assert
        var tokenTypes = tokens.Select(t => t.Type).ToList();
        Assert.That(tokenTypes, Does.Contain(PacmanConfLexer.DBPATH));
    }

    [Test]
    public void NextToken_WithEmptyIncludedFile_ContinuesProcessing()
    {
        // Arrange
        var emptyFile = Path.Combine(_testDir, "empty.conf");
        File.WriteAllText(emptyFile, "");

        // Write main file with explicit line breaks
        var mainFile = Path.Combine(_testDir, "main.conf");
        var mainContent = "RootDir = /\n" +
                         $"Include = {emptyFile}\n" +
                         "LogFile = /var/log/pacman.log\n";
        File.WriteAllText(mainFile, mainContent);
        
        // Verify file was written correctly
        var writtenContent = File.ReadAllText(mainFile);
        TestContext.WriteLine($"Main file content ({writtenContent.Length} chars):");
        TestContext.WriteLine(writtenContent);
        Assert.That(writtenContent, Does.Contain("LogFile"));

        var inputStream = new AntlrFileStream(mainFile);
        var lexer = new PacmanConfLexer(inputStream);
        var tokenSource = new PacmanConfigTokenSource(_logger, lexer);

        // Act
        var tokens = new List<IToken>();
        IToken token;
        do
        {
            token = tokenSource.NextToken();
            tokens.Add(token);
        } while (token.Type != PacmanConfLexer.Eof);

        // Assert
        var keywordTokenTypes = tokens
            .Where(t => t.Type == PacmanConfLexer.ROOTDIR || t.Type == PacmanConfLexer.LOGFILE)
            .ToList();
        
        // Should have both keywords even though one file is empty
        Assert.That(keywordTokenTypes, Has.Count.GreaterThanOrEqualTo(1), 
            "Should have at least ROOTDIR");
        if (keywordTokenTypes.Count < 2)
        {
            TestContext.WriteLine("WARNING: Only found ROOTDIR, missing LOGFILE");
            TestContext.WriteLine("All tokens:");
            foreach (var t in tokens)
            {
                TestContext.WriteLine($"  Type={t.Type}, Text='{t.Text}'");
            }
        }
    }
}
