using Antlr4.Runtime;
using LibAlpmSharp.Config.Visitors;
using LibAlpmSharp.Test.Config.Listeners;
using LibAlpmSharp.Test.Utils;
using Microsoft.Extensions.Logging;

namespace LibAlpmSharp.Test.Config.Visitors;

public class PacmanConfigValueVisitorTests
{
    private PacmanConfParser.SettingValuesContext GetContext(string input)
    {
        using var reader = new StringReader(input);
        var inputStream = new AntlrInputStream(reader);
        var lexer = new PacmanConfLexer(inputStream);
        lexer.PushMode(PacmanConfLexer.STRING);
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new PacmanConfParser(tokenStream);
        parser.AddErrorListener(new TestFailingErrorListener());
        return parser.settingValues();
    }

    public static IEnumerable<object[]> GetSettingValuesTestData()
    {
        yield return ["simpleValue", new[] { "simpleValue" }];
        yield return ["\"quoted value\"", new[] { "quoted value" }];
        yield return ["'single quoted value'", new[] { "single quoted value" }];
        yield return ["\"value with \\n newline\"", new[] { "value with \n newline" }];
        yield return ["'value with \\' single quote'", new[] { "value with ' single quote" }];
        yield return ["\"mixed 'quotes' and \\t tab\"", new[] { "mixed 'quotes' and \t tab" }];
        yield return ["value1 value2 'value 3' \"value 4\"", new[] { "value1", "value2", "value 3", "value 4" }];
        yield return ["escaped\\\\with multi-value", new[] { "escaped\\with", "multi-value" }];
        yield return [@"backslash chaos \\\\\\\\", new[] { "backslash", "chaos", @"\\\\" }];
    }
    
    [TestCaseSource(nameof(GetSettingValuesTestData))]
    public void VisitSettingValues_ReturnsExpectedValues(string input, IEnumerable<string> expectedValues)
    {
        // Arrange
        var context = GetContext(input);
        var logger = LoggerFactory.Create(builder => 
            builder
                .AddProvider(new TestOutputLoggerProvider()).SetMinimumLevel(LogLevel.Debug))
            .CreateLogger<PacmanConfigValueVisitor>();
        var visitor = new PacmanConfigValueVisitor(logger);

        // Act
        var result = visitor.VisitSettingValues(context);

        // Assert
        Assert.That(result, Is.EquivalentTo(expectedValues));
    }
}