using Lumen.Core.Objects;

namespace Lumen.Tests.Evaluation;

public class EvaluatorStatementTests
{
    [Theory]
    [InlineData("let x := 5; x", 5L)]
    [InlineData("let x := 5 * 5; x", 25L)]
    [InlineData("let a := 5; let b := a; b", 5L)]
    [InlineData("let a := 5; let b := a; let c := a + b + 5; c", 15L)]
    [InlineData("let x := 1; let x := 2; x", 2L)]
    [InlineData("let x := 1; x := x + 1; x", 2L)]
    [InlineData("let x := 1; x := 10; x := x * 2; x", 20L)]
    public void Eval_LetAndAssign_BindAndUpdate(string input, long expected)
    {
        EvalTestHelper.AssertInt(expected, input);
    }

    [Theory]
    [InlineData("let x := 1;")]
    [InlineData("let x := 1; x := 2;")]
    public void Eval_LetAndAssignStatements_ProduceNull(string input)
    {
        EvalTestHelper.AssertNull(input);
    }

    [Fact]
    public void Eval_AssignToUndefined_ProducesError()
    {
        EvalTestHelper.AssertError("undefined variable: y", "y := 1;");
    }

    [Fact]
    public void Eval_AssignToUndefined_DoesNotDefineIt()
    {
        LumenValue result = EvalTestHelper.Eval("y := 1;");

        Assert.IsType<ErrorSignal>(result);
        EvalTestHelper.AssertError("undefined variable: y", "y");
    }

    [Theory]
    [InlineData("let x := 1 / 0; x")]
    [InlineData("let x := 1; x := 1 / 0; x")]
    public void Eval_ErrorInBoundValue_Propagates(string input)
    {
        EvalTestHelper.AssertError("division by zero", input);
    }

    [Fact]
    public void Eval_TopLevelReturn_UnwrapsValueAndStops()
    {
        EvalTestHelper.AssertInt(5, "return 5; 9");
    }

    [Fact]
    public void Eval_TopLevelBareReturn_ProducesNull()
    {
        EvalTestHelper.AssertNull("return; 9");
    }

    [Fact]
    public void Eval_IfBlockValue_IsLastStatement()
    {
        EvalTestHelper.AssertInt(6, "if (true) { let y := 2; y * 3 }");
    }

    [Fact]
    public void Eval_LetInsideIfBlock_LeaksToEnclosingScope()
    {
        // v0 只有 function 與 for 建立新 scope；if / while 的 block 與外層共用。
        EvalTestHelper.AssertInt(2, "if (true) { let y := 2; } y");
    }

    [Fact]
    public void Eval_ErrorInStatementList_StopsAtError()
    {
        StringWriter output = new();

        EvalTestHelper.Eval("let x := 1; puts(x); x := 1 / 0; puts(x);", output);

        Assert.Equal("1", output.ToString().Trim());
    }
}
