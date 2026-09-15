using Lumen.Repl;

namespace Lumen.Tests.Repl;

public class CliTests
{
    private static (int Code, string Out, string Err) Run(CancellationToken token, params string[] args)
    {
        StringWriter output = new();
        StringWriter error = new();
        int code = Cli.Run(args, output, error, token);
        return (code, output.ToString().Trim(), error.ToString().Trim());
    }

    private static (int Code, string Out, string Err) Run(params string[] args) => Run(CancellationToken.None, args);

    private static string WriteScript(string source)
    {
        string path = Path.Combine(Path.GetTempPath(), $"lumen-cli-{Guid.NewGuid():N}.lumen");
        File.WriteAllText(path, source);
        return path;
    }

    [Fact]
    public void Run_EvalFlag_PrintsResultAndExitsZero()
    {
        Assert.Equal((Cli.ExitOk, "2", ""), Run("-e", "1 + 1"));
    }

    [Fact]
    public void Run_EvalFlagWithNullResult_PrintsNothing()
    {
        Assert.Equal((Cli.ExitOk, "", ""), Run("-e", "let x := 1;"));
    }

    [Fact]
    public void Run_EvalFlagWithParseError_Exits65()
    {
        (int code, string _, string err) = Run("-e", "1 +");

        Assert.Equal(Cli.ExitParseError, code);
        Assert.Contains("unexpected token", err);
    }

    [Fact]
    public void Run_EvalFlagWithRuntimeError_Exits70()
    {
        (int code, string _, string err) = Run("-e", "1 / 0");

        Assert.Equal(Cli.ExitRuntimeError, code);
        Assert.Equal("[line 1:3] division by zero", err);
    }

    [Fact]
    public void Run_ScriptFile_ExecutesAndExitsZero()
    {
        string path = WriteScript("let x := 20;\nputs(x + 22);\nx");

        (int code, string output, string err) = Run(path);

        Assert.Equal((Cli.ExitOk, "42", ""), (code, output, err));
    }

    [Fact]
    public void Run_ScriptWithParseError_Exits65AndReportsAllErrors()
    {
        string path = WriteScript("let := 1;\nlet y := ;");

        (int code, string _, string err) = Run(path);

        Assert.Equal(Cli.ExitParseError, code);
        Assert.Contains("[line 1:5]", err);
        Assert.Contains("[line 2:10]", err);
    }

    [Fact]
    public void Run_ScriptWithRuntimeError_Exits70WithCallStack()
    {
        string path = WriteScript("puts(\"start\");\nfn boom() { 1 / 0 }\nboom();\nputs(\"never\");");

        (int code, string output, string err) = Run(path);

        Assert.Equal(Cli.ExitRuntimeError, code);
        Assert.Equal("start", output);
        Assert.Equal("[line 2:15] division by zero\n  at boom", err);
    }

    [Fact]
    public void Run_MissingScriptFile_Exits66()
    {
        (int code, string _, string err) = Run(Path.Combine(Path.GetTempPath(), "does-not-exist.lumen"));

        Assert.Equal(Cli.ExitNoInput, code);
        Assert.Contains("cannot read file", err);
    }

    [Theory]
    [InlineData("-e")]
    [InlineData("-x", "1")]
    [InlineData("a.lumen", "b.lumen")]
    public void Run_BadUsage_Exits64(params string[] args)
    {
        (int code, string _, string err) = Run(args);

        Assert.Equal(Cli.ExitUsage, code);
        Assert.Contains("usage:", err);
    }

    [Fact]
    public void Run_CancelledEvaluation_Exits130()
    {
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(100));

        (int code, string _, string err) = Run(cts.Token, "-e", "while (true) { }");

        Assert.Equal(Cli.ExitInterrupted, code);
        Assert.Contains("interrupted", err);
    }
}
