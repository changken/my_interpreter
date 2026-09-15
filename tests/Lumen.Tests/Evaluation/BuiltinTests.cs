using Lumen.Core.Objects;

namespace Lumen.Tests.Evaluation;

public class BuiltinTests
{
    [Theory]
    [InlineData("len(\"\")", 0L)]
    [InlineData("len(\"four\")", 4L)]
    [InlineData("len(\"hello world\")", 11L)]
    [InlineData("len(\"héllo\")", 5L)]
    [InlineData("len(\"👍\")", 2L)]
    [InlineData("len([])", 0L)]
    [InlineData("len([1, 2, 3])", 3L)]
    [InlineData("len([[1, 2], []])", 2L)]
    [InlineData("len(\"a\" + \"bc\")", 3L)]
    public void Len_ReturnsCodeUnitOrElementCount(string input, long expected)
    {
        EvalTestHelper.AssertInt(expected, input);
    }

    [Theory]
    [InlineData("len(1)", "argument to len must be String or Array, got Int")]
    [InlineData("len({})", "argument to len must be String or Array, got Hash")]
    [InlineData("len()", "wrong number of arguments: expected 1, got 0")]
    [InlineData("len(\"a\", \"b\")", "wrong number of arguments: expected 1, got 2")]
    public void Len_WithBadArguments_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Theory]
    [InlineData("first([1, 2, 3])", "1")]
    [InlineData("first([\"a\"])", "a")]
    [InlineData("first([[1], 2])", "[1]")]
    [InlineData("rest([1, 2, 3])", "[2, 3]")]
    [InlineData("rest([1])", "[]")]
    [InlineData("push([], 1)", "[1]")]
    [InlineData("push([1, 2], [3])", "[1, 2, [3]]")]
    [InlineData("push([1], \"s\")", "[1, \"s\"]")]
    public void ArrayBuiltins_ReturnExpectedValue(string input, string expected)
    {
        Assert.Equal(expected, EvalTestHelper.Eval(input).Inspect());
    }

    [Theory]
    [InlineData("first([])")]
    [InlineData("rest([])")]
    public void FirstAndRest_OnEmptyArray_ProduceNull(string input)
    {
        EvalTestHelper.AssertNull(input);
    }

    [Theory]
    [InlineData("first(1)", "argument to first must be Array, got Int")]
    [InlineData("first(\"abc\")", "argument to first must be Array, got String")]
    [InlineData("rest(null)", "argument to rest must be Array, got Null")]
    [InlineData("push(1, 2)", "first argument to push must be Array, got Int")]
    [InlineData("push([1])", "wrong number of arguments: expected 2, got 1")]
    [InlineData("first()", "wrong number of arguments: expected 1, got 0")]
    [InlineData("rest([1], [2])", "wrong number of arguments: expected 1, got 2")]
    public void ArrayBuiltins_WithBadArguments_ProduceError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Fact]
    public void Push_DoesNotMutateOriginalArray()
    {
        ArrayValue result = Assert.IsType<ArrayValue>(EvalTestHelper.Eval("let a := [1]; let b := push(a, 2); [a, b]"));

        Assert.Equal("[[1], [1, 2]]", result.Inspect());
    }

    [Fact]
    public void Rest_DoesNotMutateOriginalArray()
    {
        ArrayValue result = Assert.IsType<ArrayValue>(EvalTestHelper.Eval("let a := [1, 2]; let b := rest(a); [a, b]"));

        Assert.Equal("[[1, 2], [2]]", result.Inspect());
    }

    [Fact]
    public void BuiltinError_CarriesPositionOfCallSite()
    {
        ErrorSignal error = EvalTestHelper.EvalError("let x := 1;\n  len(x)");

        Assert.Equal("[line 2:6] argument to len must be String or Array, got Int", error.Inspect());
    }

    [Fact]
    public void Builtins_ComposeIntoRecursiveMap()
    {
        const string source = """
            fn map(arr, f) {
              fn iter(rest_, acc) {
                if (len(rest_) == 0) { acc } else { iter(rest(rest_), push(acc, f(first(rest_)))) }
              }
              iter(arr, [])
            }
            map([1, 2, 3], fn(x) { x * 2 })
            """;

        Assert.Equal("[2, 4, 6]", EvalTestHelper.Eval(source).Inspect());
    }

    [Fact]
    public void DefaultRegistry_ContainsExactlySpecBuiltins()
    {
        Lumen.Core.Evaluation.BuiltinRegistry registry = Lumen.Core.Evaluation.Builtins.CreateDefault(TextWriter.Null);

        Assert.Equal(["first", "len", "push", "puts", "rest"], registry.Names.Order(StringComparer.Ordinal));
    }
}
