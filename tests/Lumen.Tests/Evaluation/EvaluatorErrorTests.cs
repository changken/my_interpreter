using Lumen.Core.Objects;

namespace Lumen.Tests.Evaluation;

public class EvaluatorErrorTests
{
    [Fact]
    public void Eval_ErrorSignal_InspectUsesLineColumnFormat()
    {
        ErrorSignal error = EvalTestHelper.EvalError("1 + \"a\"");

        Assert.Equal("[line 1:3] type mismatch: Int + String", error.Inspect());
    }

    [Fact]
    public void Eval_ErrorOnThirdLine_ReportsLineThree()
    {
        ErrorSignal error = EvalTestHelper.EvalError("1;\n2;\n3 + \"a\"");

        Assert.Equal(3, error.Line);
        Assert.StartsWith("[line 3:3]", error.Inspect());
    }

    [Fact]
    public void Eval_DeeplyNestedError_BubblesUpWithOriginalPosition()
    {
        ErrorSignal error = EvalTestHelper.EvalError("1 + (2 * (3 + \"a\"))");

        Assert.Equal("[line 1:13] type mismatch: Int + String", error.Inspect());
    }

    [Fact]
    public void Eval_UndefinedVariable_ProducesError()
    {
        EvalTestHelper.AssertError("undefined variable: foobar", "foobar");
    }

    [Fact]
    public void Eval_AfterError_LaterStatementsDoNotRun()
    {
        StringWriter output = new();

        LumenValue result = EvalTestHelper.Eval("puts(1); 1 / 0; puts(2);", output);

        Assert.IsType<ErrorSignal>(result);
        Assert.Equal("1", output.ToString().Trim());
    }

    [Fact]
    public void Eval_ErrorPayload_IsStringValue()
    {
        ErrorSignal error = EvalTestHelper.EvalError("1 / 0");

        Assert.Equal(new StringValue("division by zero"), error.Payload);
    }
}
