namespace Lumen.Tests.Evaluation;

public class EvaluatorIfTests
{
    [Theory]
    [InlineData("if (true) { 10 }", 10L)]
    [InlineData("if (false) { 10 } else { 20 }", 20L)]
    [InlineData("if (1 < 2) { 10 } else { 20 }", 10L)]
    [InlineData("if (1 > 2) { 10 } else { 20 }", 20L)]
    [InlineData("if (true) { 1; 2; 3 }", 3L)]
    [InlineData("if (true) { if (false) { 1 } else { 2 } } else { 3 }", 2L)]
    [InlineData("1 + if (true) { 2 } else { 3 }", 3L)]
    public void Eval_IfExpression_ProducesBranchValue(string input, long expected)
    {
        EvalTestHelper.AssertInt(expected, input);
    }

    [Theory]
    [InlineData("if (false) { 10 }")]
    [InlineData("if (true) { }")]
    public void Eval_IfWithoutTakenBranchValue_ProducesNull(string input)
    {
        EvalTestHelper.AssertNull(input);
    }

    [Theory]
    [InlineData("if (1) { 10 }", "condition must be Bool, got Int")]
    [InlineData("if (null) { 10 }", "condition must be Bool, got Null")]
    [InlineData("if (\"\") { 10 }", "condition must be Bool, got String")]
    [InlineData("if ([]) { 10 }", "condition must be Bool, got Array")]
    public void Eval_IfWithNonBoolCondition_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Fact]
    public void Eval_IfConditionError_Propagates()
    {
        EvalTestHelper.AssertError("division by zero", "if (1 / 0 == 1) { 10 }");
    }

    [Fact]
    public void Eval_ErrorInsideBranch_StopsBranchAndPropagates()
    {
        EvalTestHelper.AssertError("division by zero", "if (true) { 1 / 0; 99 }");
    }
}
