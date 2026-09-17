using Lumen.Repl;

namespace Lumen.Tests.Repl;

public class ReplTests
{
    private static string RunRaw(string input)
    {
        StringWriter output = new();
        new Lumen.Repl.Repl(new StringReader(input), output).Run();
        return output.ToString();
    }

    private static string[] RunLines(string input) =>
        RunRaw(input)
            .Replace("lumen> ", string.Empty)
            .Replace("... ", string.Empty)
            .Split(System.Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    [Fact]
    public void Run_EnvironmentPersistsAcrossLines()
    {
        Assert.Equal(["2"], RunLines("let x := 1;\nx + 1\n"));
    }

    [Fact]
    public void Run_ExpressionStatement_AutoPrintsResult()
    {
        Assert.Equal(["7", "abc"], RunLines("1 + 2 * 3\n\"abc\"\n"));
    }

    [Fact]
    public void Run_NonExpressionStatements_PrintNothing()
    {
        Assert.Empty(RunLines("let x := 1;\nx := 2;\nx += 1;\nwhile (false) { }\n"));
    }

    [Fact]
    public void Run_CompoundAssign_UpdatesBindingAcrossLines()
    {
        Assert.Equal(["3"], RunLines("let x := 1;\nx += 2;\nx\n"));
    }

    [Fact]
    public void Run_NullResult_IsNotPrinted()
    {
        Assert.Equal(["5"], RunLines("puts(5)\nnull\n"));
    }

    [Fact]
    public void Run_ParseError_IsPrintedAndReplContinues()
    {
        Assert.Equal(["[line 1:9] unexpected token end of input", "2"], RunLines("let x :=\n1 + 1\n"));
    }

    [Fact]
    public void Run_RuntimeError_IsPrintedAndReplContinues()
    {
        Assert.Equal(["[line 1:3] division by zero", "3"], RunLines("1 / 0\n1 + 2\n"));
    }

    [Fact]
    public void Run_UnclosedBrackets_ContinueOnNextLine()
    {
        string raw = RunRaw("let f := fn(a) {\n  a + 1\n};\nf(1)\n");

        Assert.Contains("... ", raw);
        Assert.Equal(["2"], RunLines("let f := fn(a) {\n  a + 1\n};\nf(1)\n"));
    }

    [Fact]
    public void Run_BracketsInsideStrings_DoNotTriggerContinuation()
    {
        Assert.Equal(["("], RunLines("\"(\"\n"));
    }

    [Fact]
    public void Run_ExitCommand_StopsReading()
    {
        Assert.Empty(RunLines(".exit\n1 + 1\n"));
    }

    [Fact]
    public void Run_EnvCommand_ListsBindingsInOrder()
    {
        Assert.Equal(["b = 2", "a = [1]"], RunLines("let b := 2; let a := [1];\n.env\n"));
    }

    [Fact]
    public void Run_ClearCommand_ResetsEnvironment()
    {
        Assert.Equal(["[line 1:1] undefined variable: x"], RunLines("let x := 1;\n.clear\nx\n"));
    }

    [Fact]
    public void Run_EmptyLine_IsIgnored()
    {
        Assert.Equal(["1"], RunLines("\n\n1\n"));
    }

    [Fact]
    public void Run_EndOfInput_Terminates()
    {
        Assert.Empty(RunLines(string.Empty));
    }

    [Fact]
    public void Interrupt_DuringEvaluation_ReturnsToPromptWithoutExiting()
    {
        StringWriter output = new();
        Lumen.Repl.Repl repl = new(new StringReader("while (true) { }\n1 + 1\n"), output);
        Thread thread = new(repl.Run);

        thread.Start();
        Thread.Sleep(150);
        repl.Interrupt();
        Assert.True(thread.Join(TimeSpan.FromSeconds(5)), "REPL did not return to the prompt after Interrupt()");

        string[] lines = output.ToString().Replace("lumen> ", string.Empty).Split(System.Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(["interrupted", "2"], lines);
    }
}
