using Lumen.Core.Objects;

namespace Lumen.Tests.Evaluation;

public class EvaluatorLiteralTests
{
    [Theory]
    [InlineData("5", 5L)]
    [InlineData("0", 0L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    public void Eval_IntegerLiteral_ProducesIntValue(string input, long expected)
    {
        EvalTestHelper.AssertInt(expected, input);
    }

    [Theory]
    [InlineData("3.14", 3.14)]
    [InlineData("1.0", 1.0)]
    public void Eval_FloatLiteral_ProducesFloatValue(string input, double expected)
    {
        EvalTestHelper.AssertFloat(expected, input);
    }

    [Fact]
    public void Eval_StringLiteral_ProducesDecodedString()
    {
        EvalTestHelper.AssertString("hi\n", "\"hi\\n\"");
    }

    [Theory]
    [InlineData("'a'", 'a')]
    [InlineData(@"'\n'", '\n')]
    public void Eval_CharLiteral_ProducesCharValue(string input, char expected)
    {
        Assert.Equal(new CharValue(expected), EvalTestHelper.Eval(input));
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void Eval_BooleanLiteral_ProducesSharedBoolValue(string input, bool expected)
    {
        Assert.Same(BoolValue.Of(expected), EvalTestHelper.Eval(input));
    }

    [Fact]
    public void Eval_NullLiteral_ProducesNullSingleton()
    {
        EvalTestHelper.AssertNull("null");
    }

    [Fact]
    public void Eval_ArrayLiteral_EvaluatesEachElement()
    {
        ArrayValue array = Assert.IsType<ArrayValue>(EvalTestHelper.Eval("[1 + 1, \"a\", [true]]"));

        Assert.Equal("[2, \"a\", [true]]", array.Inspect());
    }

    [Fact]
    public void Eval_ArrayLiteralWithErrorElement_PropagatesError()
    {
        EvalTestHelper.AssertError("division by zero", "[1, 2 / 0, 3]");
    }

    [Fact]
    public void Eval_HashLiteral_EvaluatesKeysAndValuesInOrder()
    {
        HashValue hash = Assert.IsType<HashValue>(EvalTestHelper.Eval("{\"a\" + \"b\": 1 + 1, 2: true, 1.0: \"x\"}"));

        Assert.Equal("{\"ab\": 2, 2: true, 1: \"x\"}", hash.Inspect());
    }

    [Theory]
    [InlineData("{[1]: 1}", "unusable as hash key: Array")]
    [InlineData("{{}: 1}", "unusable as hash key: Hash")]
    [InlineData("{null: 1}", "unusable as hash key: Null")]
    [InlineData("{'a': 1}", "unusable as hash key: Char")]
    public void Eval_HashLiteralWithInvalidKey_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Fact]
    public void Eval_EmptyProgram_ProducesNull()
    {
        EvalTestHelper.AssertNull("");
    }

    [Fact]
    public void Eval_MultipleStatements_ProducesLastValue()
    {
        EvalTestHelper.AssertInt(3, "1; 2; 3");
    }
}
