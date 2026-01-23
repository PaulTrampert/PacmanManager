using Antlr4.Runtime;
using LibAlpmSharp.Config.Listeners;
using LibAlpmSharp.Config.Visitors;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Config.TokenSources;

internal class PacmanConfigTokenSource : ITokenSource
{
    private readonly ILogger _logger;
    private readonly Stack<PacmanConfLexer> _lexerStack = new();
    private readonly HashSet<string> _currentFiles = new();
    private PacmanConfLexer? CurrentLexer => _lexerStack.Count > 0 ? _lexerStack.Peek() : null;

    public PacmanConfigTokenSource(ILogger logger, PacmanConfLexer initialLexer)
    {
        _logger = logger;
        _lexerStack.Push(initialLexer);
        _currentFiles.Add(initialLexer.SourceName);
    }
    
    public IToken NextToken()
    {
        var token = CurrentLexer.NextToken();

        while (token.Type == PacmanConfLexer.Eof)
        {
            var lexerName = CurrentLexer.SourceName;
            _logger.LogDebug("Reached end of file for {File}", lexerName);
            _lexerStack.Pop();
            _currentFiles.Remove(lexerName);
            if (CurrentLexer == null)
            {
                break;
            }
            token = CurrentLexer.NextToken();
        }
        
        if (token.Type == PacmanConfLexer.INCLUDE)
        {
            token = HandleInclude();
        }

        return token;
    }
    
    private IToken HandleInclude()
    {
        var filename = ExtractFilename();
        var baseDir = GetBaseDirectory(filename);
        _logger.LogDebug("Processing include {File} relative to {BaseDir}", filename, baseDir);

        var fullFilenames = GetFullFilenames(baseDir, filename);
        foreach(var file in fullFilenames)
        {
            PushLexerForFile(file);
        }
        return NextToken();
    }
    
    private void PushLexerForFile(string fullFilename)
    {
        if (!_currentFiles.Add(fullFilename))        
        {
            var stackState = string.Join(Environment.NewLine, _lexerStack.Select(l => $"* {l.SourceName}"));
            throw new InvalidOperationException($"Circular include detected for file '{fullFilename}'{Environment.NewLine}" +
                                                $"Current include stack:{Environment.NewLine}{stackState}");
        }
        _logger.LogDebug("Including file {File}", fullFilename);
        var fileStream = new AntlrFileStream(fullFilename);
        var lexer = new PacmanConfLexer(fileStream);
        _lexerStack.Push(lexer);
    }

    private IEnumerable<string> GetFullFilenames(string baseDir, string fileName)
    {
        if (IsGlobPattern(fileName))
        {
            var matcher = new Matcher();
            matcher.AddInclude(fileName);
            var result = matcher.GetResultsInFullPath(baseDir);
            return result.OrderByDescending(f => f);
        }
        var fullPath = Path.IsPathFullyQualified(fileName)
            ? fileName
            : Path.GetFullPath(Path.Combine(baseDir, fileName));

        if (Directory.Exists(fullPath))
        {
            return Directory.EnumerateFiles(fullPath).OrderByDescending(f => f);
        }

        if (File.Exists(fullPath))
        {
            return [fullPath];
        }
        
        throw new RecognitionException(
            $"Included file or directory '{fileName}' not found relative to '{baseDir}'",
            CurrentLexer,
            InputStream,
            null
        );
    }
    
    private bool IsGlobPattern(string filename)
    {
        return filename.Contains("*") || filename.Contains("?") || filename.Contains("[");
    }

    private string GetBaseDirectory(string filename)
    {
        if (Path.IsPathFullyQualified(filename))
        {
            return "/";
        }
        
        return CurrentLexer.SourceName == IntStreamConstants.UnknownSourceName
            ? Directory.GetCurrentDirectory()
            : Path.GetDirectoryName(CurrentLexer.SourceName) ?? Directory.GetCurrentDirectory();
    }

    private string ExtractFilename()
    {
        var eqToken = CurrentLexer.NextToken();
        if (eqToken.Type != PacmanConfLexer.EQUALS)
        {
            throw new InvalidOperationException("Expected '=' token for include directive.");
        }
        var filenameTokens = new List<IToken>();
        var leadingWsSkipped = false;
        while (CurrentLexer.CurrentMode != PacmanConfLexer.DEFAULT_MODE)
        {
            var valueToken = CurrentLexer.NextToken();
            if (!leadingWsSkipped && valueToken.Type == PacmanConfLexer.STRING_WS)
            {
                continue;
            }
            leadingWsSkipped = true;
            filenameTokens.Add(valueToken);
        }
        
        var valueTokenSource = new ListTokenSource(filenameTokens);
        var valueTokenStream = new CommonTokenStream(valueTokenSource);
        var valueParser = new PacmanConfParser(valueTokenStream);
        valueParser.AddErrorListener(new PacmanConfigErrorListener(_logger));
        var valueContext = valueParser.settingValue();
        var valueVisitor = new PacmanConfigValueVisitor(_logger);
        var filename = valueVisitor.VisitSettingValue(valueContext).Single();
        return filename;
    }

    public int Line => CurrentLexer.Line;
    public int Column => CurrentLexer.Column;
    public ICharStream InputStream => CurrentLexer.InputStream as ICharStream;
    public string SourceName => CurrentLexer.SourceName;

    public ITokenFactory TokenFactory
    {
        get => CurrentLexer.TokenFactory;
        set => CurrentLexer.TokenFactory = value;
    }
}