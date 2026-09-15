using Lumen.Core.Objects;

namespace Lumen.Tests.Evaluation;

public class EvaluatorNumericTests
{
    [Theory]
    [InlineData("1 + 2", 3L)]
    [InlineData("10 - 4", 6L)]
    [InlineData("3 * 4", 12L)]
    [InlineData("5 / 2", 2L)]
    [InlineData("-7 / 2", -3L)]
    [InlineData("7 % 3", 1L)]
    [InlineData("-7 % 3", -1L)]
    [InlineData("2 ** 10", 1024L)]
    [InlineData("2 ** 3 ** 2", 512L)]
    [InlineData("2 ** 0", 1L)]
    [InlineData("0 ** 0", 1L)]
    [InlineData("(-2) ** 3", -8L)]
    [InlineData("1 + 2 * 3", 7L)]
    [InlineData("(1 + 2) * 3", 9L)]
    [InlineData("-5", -5L)]
    [InlineData("--5", 5L)]
    [InlineData("9223372036854775807 % -1", 0L)]
    public void Eval_IntArithmetic_ProducesInt(string input, long expected)
    {
        EvalTestHelper.AssertInt(expected, input);
    }

    [Theory]
    [InlineData("5.0 / 2", 2.5)]
    [InlineData("1.5 + 1", 2.5)]
    [InlineData("1 + 1.5", 2.5)]
    [InlineData("2.0 * 3", 6.0)]
    [InlineData("2 ** -1", 0.5)]
    [InlineData("4 ** 0.5", 2.0)]
    [InlineData("2.0 ** 3", 8.0)]
    [InlineData("-3.5", -3.5)]
    [InlineData("0.1 + 0.2", 0.30000000000000004)]
    public void Eval_FloatArithmetic_ProducesFloat(string input, double expected)
    {
        EvalTestHelper.AssertFloat(expected, input);
    }

    [Fact]
    public void Eval_IntPower_StaysInt()
    {
        Assert.IsType<IntValue>(EvalTestHelper.Eval("2 ** 10"));
    }

    [Fact]
    public void Eval_IntDivision_StaysInt()
    {
        Assert.IsType<IntValue>(EvalTestHelper.Eval("6 / 3"));
    }

    [Theory]
    [InlineData("9223372036854775807 + 1")]
    [InlineData("-9223372036854775807 - 2")]
    [InlineData("9223372036854775807 * 2")]
    [InlineData("2 ** 64")]
    [InlineData("(-9223372036854775807 - 1) / -1")]
    [InlineData("-(-9223372036854775807 - 1)")]
    public void Eval_IntOverflow_ProducesErrorNotWrap(string input)
    {
        EvalTestHelper.AssertError("integer overflow", input);
    }

    [Theory]
    [InlineData("1 / 0")]
    [InlineData("1 % 0")]
    [InlineData("1.0 / 0.0")]
    [InlineData("1 / 0.0")]
    [InlineData("0.0 / 0.0")]
    public void Eval_DivisionByZero_ProducesError(string input)
    {
        EvalTestHelper.AssertError("division by zero", input);
    }

    [Fact]
    public void Eval_FloatModulo_ProducesError()
    {
        EvalTestHelper.AssertError("Float", "3.14 % 2");
    }

    [Theory]
    [InlineData("(10.0 ** 308) * 10")]
    [InlineData("10.0 ** 400")]
    [InlineData("(-8.0) ** 0.5")]
    public void Eval_NonFiniteFloatResult_ProducesError(string input)
    {
        EvalTestHelper.AssertError("not a finite number", input);
    }

    [Theory]
    [InlineData("1 + \"a\"", "type mismatch: Int + String")]
    [InlineData("\"a\" - 1", "type mismatch: String - Int")]
    [InlineData("true + 1", "type mismatch: Bool + Int")]
    [InlineData("[1] * 2", "type mismatch: Array * Int")]
    [InlineData("null / 1", "type mismatch: Null / Int")]
    public void Eval_ArithmeticOnMismatchedTypes_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }
}
