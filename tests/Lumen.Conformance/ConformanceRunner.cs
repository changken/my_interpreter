using System.Text.RegularExpressions;
using Lumen.Core.Ast;
using Lumen.Core.Evaluation;
using Lumen.Core.Lexing;
using Lumen.Core.Objects;
using Lumen.Core.Parsing;
using LumenEnv = Lumen.Core.Objects.Environment;

namespace Lumen.Conformance;

/// <summary>
/// 端到端測試：每個 scripts/*.lumen 跑一次，蒐集 puts 輸出，逐行比對檔案裡的 <c>// expect:</c> 標記。
/// 檔尾可放一個 <c>// expect-error: 片段</c>，表示腳本最後必須以含該片段的 ErrorSignal 結束。
/// </summary>
public partial class ConformanceRunner
{
    private static readonly string ScriptsDirectory = Path.Combine(AppContext.BaseDirectory, "scripts");

    public static TheoryData<string> Scripts
    {
        get
        {
            TheoryData<string> data = [];
            foreach (string path in Directory.GetFiles(ScriptsDirectory, "*.lumen").Order(StringComparer.Ordinal))
            {
                data.Add(Path.GetFileName(path));
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(Scripts))]
    public void Script_ProducesExpectedOutput(string scriptName)
    {
        string source = File.ReadAllText(Path.Combine(ScriptsDirectory, scriptName));
        List<string> expectedLines = ExpectPattern().Matches(source).Select(m => m.Groups[1].Value.TrimEnd()).ToList();
        Match errorMatch = ExpectErrorPattern().Match(source);

        Parser parser = new(new Lexer(source).Tokenize());
        Program program = parser.ParseProgram();
        Assert.True(parser.Errors.Count == 0, $"parse errors:\n{string.Join("\n", parser.Errors)}");

        StringWriter output = new();
        LumenValue result = new Evaluator(Builtins.CreateDefault(output)).Eval(program, new LumenEnv());

        string[] actualLines = output.ToString().Split(System.Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(expectedLines, actualLines);

        if (errorMatch.Success)
        {
            ErrorSignal error = Assert.IsType<ErrorSignal>(result);
            Assert.Contains(errorMatch.Groups[1].Value.TrimEnd(), error.Inspect());
        }
        else
        {
            Assert.False(result is ErrorSignal, $"unexpected runtime error: {result.Inspect()}");
        }
    }

    [GeneratedRegex(@"//\s*expect:\s?(.*)$", RegexOptions.Multiline)]
    private static partial Regex ExpectPattern();

    [GeneratedRegex(@"//\s*expect-error:\s?(.*)$", RegexOptions.Multiline)]
    private static partial Regex ExpectErrorPattern();
}
