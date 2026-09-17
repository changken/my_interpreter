using Lumen.Core.Objects;

namespace Lumen.Tests.Evaluation;

public class EvaluatorIndexTests
{
    [Theory]
    [InlineData("[1, 2, 3][0]", 1L)]
    [InlineData("[1, 2, 3][2]", 3L)]
    [InlineData("[1, 2, 3][1 + 1]", 3L)]
    [InlineData("let a := [1, 2, 3]; a[1]", 2L)]
    [InlineData("[[1, 2], [3, 4]][1][0]", 3L)]
    [InlineData("let i := 0; [10, 20][i]", 10L)]
    public void Eval_ArrayIndex_ReturnsElement(string input, long expected)
    {
        EvalTestHelper.AssertInt(expected, input);
    }

    [Theory]
    [InlineData("[1, 2, 3][3]", "index out of range: 3")]
    [InlineData("[1, 2, 3][-1]", "index out of range: -1")]
    [InlineData("[][0]", "index out of range: 0")]
    public void Eval_ArrayIndexOutOfRange_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("[1, 2][\"a\"]", "array index must be Int, got String")]
    [InlineData("[1, 2][1.0]", "array index must be Int, got Float")]
    [InlineData("[1, 2][true]", "array index must be Int, got Bool")]
    public void Eval_ArrayIndexWithNonInt_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("{\"foo\": 5}[\"foo\"]", 5L)]
    [InlineData("let k := \"foo\"; {\"foo\": 5}[k]", 5L)]
    [InlineData("{5: 5}[5]", 5L)]
    [InlineData("{5: 5}[5.0]", 5L)]
    [InlineData("{1.0: 7}[1]", 7L)]
    [InlineData("{true: 5}[true]", 5L)]
    [InlineData("{false: 5}[false]", 5L)]
    [InlineData("{\"a\": [1, 2]}[\"a\"][1]", 2L)]
    public void Eval_HashIndex_ReturnsValue(string input, long expected)
    {
        EvalTestHelper.AssertInt(expected, input);
    }

    [Theory]
    [InlineData("{\"foo\": 5}[\"bar\"]")]
    [InlineData("{}[\"foo\"]")]
    [InlineData("{1: 1}[1.5]")]
    public void Eval_HashIndexMissingKey_ProducesNull(string input)
    {
        EvalTestHelper.AssertNull(input);
    }

    [Theory]
    [InlineData("{}[[1]]", "unusable as hash key: Array")]
    [InlineData("{}[{}]", "unusable as hash key: Hash")]
    [InlineData("{}[null]", "unusable as hash key: Null")]
    [InlineData("{}[fn() { }]", "unusable as hash key: Function")]
    public void Eval_HashIndexWithInvalidKey_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("1[0]", "index operator not supported: Int")]
    [InlineData("null[0]", "index operator not supported: Null")]
    [InlineData("fn() { }[0]", "index operator not supported: Function")]
    public void Eval_IndexOnUnsupportedType_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("\"abc\"[0]", 'a')]
    [InlineData("\"abc\"[2]", 'c')]
    [InlineData("let s := \"abc\"; s[1]", 'b')]
    public void Eval_StringIndex_ReturnsChar(string input, char expected)
    {
        Assert.Equal(new CharValue(expected), EvalTestHelper.Eval(input));
    }

    [Theory]
    [InlineData("\"abc\"[3]", "index out of range: 3")]
    [InlineData("\"abc\"[-1]", "index out of range: -1")]
    [InlineData("\"\"[0]", "index out of range: 0")]
    public void Eval_StringIndexOutOfRange_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("\"abc\"[\"a\"]", "string index must be Int, got String")]
    [InlineData("\"abc\"[1.0]", "string index must be Int, got Float")]
    [InlineData("\"abc\"[true]", "string index must be Int, got Bool")]
    public void Eval_StringIndexWithNonInt_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Fact]
    public void Eval_IndexError_CarriesPositionOfBracket()
    {
        ErrorSignal error = EvalTestHelper.EvalError("let a := [1];\na[9]");

        Assert.Equal("[line 2:2] index out of range: 9", error.Inspect());
    }

    [Theory]
    [InlineData("[1 / 0][0]")]
    [InlineData("[1][1 / 0]")]
    public void Eval_ErrorInIndexOperands_Propagates(string input)
    {
        EvalTestHelper.AssertError("division by zero", input);
    }
}
