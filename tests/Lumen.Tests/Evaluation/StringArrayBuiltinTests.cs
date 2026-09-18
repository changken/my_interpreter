using Lumen.Core.Objects;

namespace Lumen.Tests.Evaluation;

public class StringArrayBuiltinTests
{
    [Theory]
    [InlineData("upper(\"hello\")", "HELLO")]
    [InlineData("upper(\"HeLLo\")", "HELLO")]
    [InlineData("upper(\"\")", "")]
    [InlineData("lower(\"WORLD\")", "world")]
    [InlineData("lower(\"WoRLd\")", "world")]
    [InlineData("lower(\"\")", "")]
    [InlineData("trim(\"  hi  \")", "hi")]
    [InlineData("trim(\"\\thi\\n\")", "hi")]
    [InlineData("trim(\"none\")", "none")]
    public void StringCaseAndTrim_ReturnExpectedString(string input, string expected)
    {
        EvalTestHelper.AssertString(expected, input);
    }

    [Theory]
    [InlineData("upper(1)", "argument to upper must be String, got Int")]
    [InlineData("lower([1])", "argument to lower must be String, got Array")]
    [InlineData("trim(null)", "argument to trim must be String, got Null")]
    [InlineData("upper()", "wrong number of arguments: expected 1, got 0")]
    [InlineData("lower(\"a\", \"b\")", "wrong number of arguments: expected 1, got 2")]
    public void StringCaseAndTrim_WithBadArguments_ProduceError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("split(\"a,b,c\", \",\")", "[\"a\", \"b\", \"c\"]")]
    [InlineData("split(\"a\", \",\")", "[\"a\"]")]
    [InlineData("split(\"\", \",\")", "[\"\"]")]
    [InlineData("split(\"a--b\", \"--\")", "[\"a\", \"b\"]")]
    public void Split_ReturnsArrayOfParts(string input, string expected)
    {
        Assert.Equal(expected, EvalTestHelper.Eval(input).Inspect());
    }

    [Theory]
    [InlineData("split(\"a\", \"\")", "second argument to split must not be empty")]
    [InlineData("split(1, \",\")", "first argument to split must be String, got Int")]
    [InlineData("split(\"a\", 1)", "second argument to split must be String, got Int")]
    [InlineData("split(\"a\")", "wrong number of arguments: expected 2, got 1")]
    public void Split_WithBadArguments_ProduceError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("join([\"a\", \"b\", \"c\"], \"-\")", "a-b-c")]
    [InlineData("join([], \"-\")", "")]
    [InlineData("join([\"only\"], \"-\")", "only")]
    public void Join_ReturnsJoinedString(string input, string expected)
    {
        EvalTestHelper.AssertString(expected, input);
    }

    [Theory]
    [InlineData("join(1, \"-\")", "first argument to join must be Array, got Int")]
    [InlineData("join([], 1)", "second argument to join must be String, got Int")]
    [InlineData("join([1, 2], \"-\")", "argument to join: array element must be String, got Int")]
    [InlineData("join([\"a\"])", "wrong number of arguments: expected 2, got 1")]
    public void Join_WithBadArguments_ProduceError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("contains(\"hello\", \"ell\")", true)]
    [InlineData("contains(\"hello\", \"xyz\")", false)]
    [InlineData("contains(\"hello\", \"\")", true)]
    [InlineData("contains([1, 2, 3], 2)", true)]
    [InlineData("contains([1, 2, 3], 9)", false)]
    [InlineData("contains([\"a\", \"b\"], \"a\")", true)]
    [InlineData("contains([], 1)", false)]
    public void Contains_ReturnsBool(string input, bool expected)
    {
        EvalTestHelper.AssertBool(expected, input);
    }

    [Theory]
    [InlineData("contains(\"hello\", 1)", "second argument to contains must be String, got Int")]
    [InlineData("contains(1, \"a\")", "first argument to contains must be String or Array, got Int")]
    [InlineData("contains(\"a\")", "wrong number of arguments: expected 2, got 1")]
    public void Contains_WithBadArguments_ProduceError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("reverse(\"abc\")", "cba")]
    [InlineData("reverse(\"\")", "")]
    [InlineData("reverse(\"a\")", "a")]
    public void Reverse_OnString_ReturnsReversedString(string input, string expected)
    {
        EvalTestHelper.AssertString(expected, input);
    }

    [Theory]
    [InlineData("reverse([1, 2, 3])", "[3, 2, 1]")]
    [InlineData("reverse([])", "[]")]
    public void Reverse_OnArray_ReturnsReversedArray(string input, string expected)
    {
        Assert.Equal(expected, EvalTestHelper.Eval(input).Inspect());
    }

    [Fact]
    public void Reverse_DoesNotMutateOriginalArray()
    {
        ArrayValue result = Assert.IsType<ArrayValue>(EvalTestHelper.Eval("let a := [1, 2]; let b := reverse(a); [a, b]"));

        Assert.Equal("[[1, 2], [2, 1]]", result.Inspect());
    }

    [Theory]
    [InlineData("reverse(1)", "argument to reverse must be String or Array, got Int")]
    [InlineData("reverse()", "wrong number of arguments: expected 1, got 0")]
    public void Reverse_WithBadArguments_ProduceError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("last([1, 2, 3])", "3")]
    [InlineData("last([\"x\"])", "x")]
    public void Last_OnArray_ReturnsLastElement(string input, string expected)
    {
        Assert.Equal(expected, EvalTestHelper.Eval(input).Inspect());
    }

    [Fact]
    public void Last_OnEmptyArray_ProducesNull()
    {
        EvalTestHelper.AssertNull("last([])");
    }

    [Fact]
    public void Last_OnString_ReturnsLastChar()
    {
        Assert.Equal(new CharValue('c'), EvalTestHelper.Eval("last(\"abc\")"));
    }

    [Fact]
    public void Last_OnEmptyString_ProducesNull()
    {
        EvalTestHelper.AssertNull("last(\"\")");
    }

    [Theory]
    [InlineData("last(1)", "argument to last must be String or Array, got Int")]
    [InlineData("last()", "wrong number of arguments: expected 1, got 0")]
    public void Last_WithBadArguments_ProduceError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }
}
