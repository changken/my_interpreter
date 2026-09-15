namespace Lumen.Tests.Evaluation;

public class EvaluatorLoopTests
{
    [Theory]
    [InlineData("let i := 0; while (i < 3) { i := i + 1; } i", 3L)]
    [InlineData("let i := 0; while (true) { i := i + 1; if (i == 5) { break; } } i", 5L)]
    [InlineData("let i := 0; let s := 0; while (i < 5) { i := i + 1; if (i % 2 == 0) { continue; } s := s + i; } s", 9L)]
    [InlineData("let c := 0; let i := 0; while (i < 3) { i := i + 1; while (true) { break; } c := c + 1; } c", 3L)]
    public void Eval_WhileLoop_RunsUntilConditionFalseOrBreak(string input, long expected)
    {
        EvalTestHelper.AssertInt(expected, input);
    }

    [Theory]
    [InlineData("while (false) { }")]
    [InlineData("let i := 0; while (i < 3) { i := i + 1; }")]
    [InlineData("while (true) { break; }")]
    public void Eval_WhileLoop_ProducesNull(string input)
    {
        EvalTestHelper.AssertNull(input);
    }

    [Theory]
    [InlineData("let s := 0; for (let i := 0; i < 5; i := i + 1) { s := s + i; } s", 10L)]
    [InlineData("let s := 0; for (let i := 0; i < 5; i := i + 1) { if (i == 2) { continue; } s := s + i; } s", 8L)]
    [InlineData("let n := 0; for (; ; ) { n := n + 1; if (n == 3) { break; } } n", 3L)]
    [InlineData("let n := 0; for (n := 1; n < 4; n := n + 1) { } n", 4L)]
    [InlineData("let c := 0; for (let i := 0; i < 10; i := i + 1) { i := i + 1; c := c + 1; } c", 5L)]
    [InlineData("let c := 0; for (let i := 0; i < 2; i := i + 1) { for (let j := 0; j < 3; j := j + 1) { if (j == 1) { break; } c := c + 1; } } c", 2L)]
    public void Eval_ForLoop_RunsClausesInOrder(string input, long expected)
    {
        EvalTestHelper.AssertInt(expected, input);
    }

    [Fact]
    public void Eval_ForLoopVariable_IsNotVisibleOutside()
    {
        EvalTestHelper.AssertError("undefined variable: i", "for (let i := 0; i < 1; i := i + 1) { } i");
    }

    [Fact]
    public void Eval_ForLoopVariable_ShadowsOuterWithoutChangingIt()
    {
        EvalTestHelper.AssertInt(99, "let i := 99; for (let i := 0; i < 3; i := i + 1) { } i");
    }

    [Fact]
    public void Eval_LetInsideForBody_DoesNotLeak()
    {
        EvalTestHelper.AssertError("undefined variable: tmp", "for (let i := 0; i < 1; i := i + 1) { let tmp := 1; } tmp");
    }

    [Theory]
    [InlineData("while (1) { }", "condition must be Bool, got Int")]
    [InlineData("for (; 1; ) { }", "condition must be Bool, got Int")]
    [InlineData("while (null) { }", "condition must be Bool, got Null")]
    public void Eval_LoopWithNonBoolCondition_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("while (true) { 1 / 0; }")]
    [InlineData("for (let i := 0; i / 0 == 0; ) { }")]
    [InlineData("for (let i := 1 / 0; ; ) { }")]
    [InlineData("for (let i := 0; true; i := i / 0) { }")]
    public void Eval_ErrorInsideLoop_Propagates(string input)
    {
        EvalTestHelper.AssertError("division by zero", input);
    }

    [Fact]
    public void Eval_WhileLoopError_CarriesPosition()
    {
        EvalTestHelper.AssertError("[line 1:43] division by zero", "let i := 0; while (i < 3) { i := i + 1; 1 / 0; }");
    }
}
