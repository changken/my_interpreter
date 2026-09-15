using Lumen.Core.Ast;
using Lumen.Core.Evaluation;
using Lumen.Core.Objects;
using Lumen.Tests.Parsing;
using LumenEnv = Lumen.Core.Objects.Environment;

namespace Lumen.Tests.Evaluation;

public class BuiltinRegistryTests
{
    [Fact]
    public void TryGet_RegisteredName_ReturnsBuiltinValue()
    {
        BuiltinRegistry registry = new();
        registry.Register("answer", static _ => new IntValue(42));

        Assert.True(registry.TryGet("answer", out BuiltinValue? value));
        Assert.Equal("answer", value.Name);
        Assert.Equal(new IntValue(42), value.Fn([]));
    }

    [Fact]
    public void TryGet_UnknownName_ReturnsFalse()
    {
        Assert.False(new BuiltinRegistry().TryGet("nope", out _));
    }

    [Fact]
    public void Register_SameNameTwice_ReplacesEarlier()
    {
        BuiltinRegistry registry = new();
        registry.Register("x", static _ => new IntValue(1));
        registry.Register("x", static _ => new IntValue(2));

        Assert.True(registry.TryGet("x", out BuiltinValue? value));
        Assert.Equal(new IntValue(2), value.Fn([]));
        Assert.Single(registry.Names);
    }

    [Fact]
    public void Puts_WritesEachArgumentOnItsOwnLine()
    {
        StringWriter output = new();

        LumenValue result = EvalTestHelper.Eval("puts(1, \"two\", [3, \"four\"], 5.0)", output);

        Assert.Same(NullValue.Instance, result);
        Assert.Equal(["1", "two", "[3, \"four\"]", "5.0"], output.ToString().Split(System.Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void Puts_WithNoArguments_WritesNothing()
    {
        StringWriter output = new();

        EvalTestHelper.Eval("puts()", output);

        Assert.Equal(string.Empty, output.ToString());
    }

    [Fact]
    public void Eval_Identifier_ResolvesBuiltinWhenNotInEnvironment()
    {
        Assert.IsType<BuiltinValue>(EvalTestHelper.Eval("puts"));
    }

    [Fact]
    public void Eval_EnvironmentBinding_ShadowsBuiltin()
    {
        LumenEnv env = new();
        env.Define("puts", new IntValue(1));

        Assert.Equal(new IntValue(1), EvalTestHelper.Eval("puts", env: env));
    }

    [Fact]
    public void Eval_NewBuiltin_IsCallableWithoutEvaluatorChanges()
    {
        BuiltinRegistry registry = new();
        registry.Register("twice", static args => new IntValue(((IntValue)args[0]).Value * 2));
        Program program = ParserTestHelper.ParseValid("twice(21)");

        LumenValue result = new Evaluator(registry).Eval(program, new LumenEnv());

        Assert.Equal(new IntValue(42), result);
    }

    [Theory]
    [InlineData("1(2)", "not a function: Int")]
    [InlineData("\"f\"()", "not a function: String")]
    [InlineData("null()", "not a function: Null")]
    public void Eval_CallOnNonFunction_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Fact]
    public void Eval_CallArgumentError_Propagates()
    {
        EvalTestHelper.AssertError("division by zero", "puts(1 / 0)");
    }
}
