using Lumen.Core.Ast;
using Lumen.Core.Evaluation;
using Lumen.Core.Objects;
using Lumen.Tests.Parsing;
using LumenEnv = Lumen.Core.Objects.Environment;

namespace Lumen.Tests.Evaluation;

public class EvaluatorFunctionTests
{
    [Theory]
    [InlineData("let identity := fn(x) { x }; identity(5)", 5L)]
    [InlineData("let double := fn(x) { x * 2 }; double(5)", 10L)]
    [InlineData("let add := fn(x, y) { x + y }; add(5, 5)", 10L)]
    [InlineData("let add := fn(x, y) { x + y }; add(5 + 5, add(5, 5))", 20L)]
    [InlineData("fn(x) { x }(5)", 5L)]
    [InlineData("let f := fn(x) { return x * 2; 99 }; f(2)", 4L)]
    [InlineData("fn add(a, b) { return a + b; } add(1, 2)", 3L)]
    [InlineData("let f := fn() { }; if (f() == null) { 1 } else { 0 }", 1L)]
    [InlineData("let x := 1; let f := fn(x) { x }; f(2)", 2L)]
    [InlineData("let x := 1; let f := fn(x) { x }; f(2); x", 1L)]
    public void Eval_FunctionCall_BindsArgumentsAndReturnsBodyValue(string input, long expected)
    {
        EvalTestHelper.AssertInt(expected, input);
    }

    [Theory]
    [InlineData("fn f() { while (true) { for (; ; ) { return 7; } } } f()", 7L)]
    [InlineData("fn f() { let i := 0; while (true) { i := i + 1; if (i == 3) { return i; } } } f() + 1", 4L)]
    [InlineData("fn f() { for (let i := 0; i < 10; i := i + 1) { if (i == 4) { return i; } } return -1; } f()", 4L)]
    public void Eval_ReturnInsideLoop_ExitsWholeFunction(string input, long expected)
    {
        EvalTestHelper.AssertInt(expected, input);
    }

    [Fact]
    public void Eval_FunctionLiteral_ProducesFunctionValueCapturingEnv()
    {
        FunctionValue fn = Assert.IsType<FunctionValue>(EvalTestHelper.Eval("let f := fn(x) { x }; f"));

        Assert.Equal("fn(x) { x; }", fn.Inspect());
        Assert.Equal(["x"], fn.Declaration.Parameters.Select(p => p.Name));
    }

    [Theory]
    [InlineData("let f := fn(a) { a }; f()", "wrong number of arguments: expected 1, got 0")]
    [InlineData("let f := fn(a) { a }; f(1, 2)", "wrong number of arguments: expected 1, got 2")]
    [InlineData("fn() { }(1)", "wrong number of arguments: expected 0, got 1")]
    public void Eval_ArityMismatch_ProducesError(string input, string expected)
    {
        EvalTestHelper.AssertError(expected, input);
    }

    [Fact]
    public void Eval_CallOnNonFunctionValue_ProducesError()
    {
        EvalTestHelper.AssertError("not a function: Int", "let x := 1; x()");
    }

    [Fact]
    public void Eval_Recursion_Works()
    {
        EvalTestHelper.AssertInt(55, "fn fib(n) { if (n < 2) { n } else { fib(n - 1) + fib(n - 2) } } fib(10)");
    }

    [Fact]
    public void Eval_InfiniteRecursion_ProducesErrorWithoutCrashing()
    {
        EvalTestHelper.AssertError("call stack exceeded", "fn f() { return f(); } f()");
    }

    [Fact]
    public void Eval_InfiniteRecursionWithExpression_ProducesErrorWithoutCrashing()
    {
        EvalTestHelper.AssertError("call stack exceeded", "fn f(n) { return 1 + f(n + 1) * 2; } f(0)");
    }

    [Fact]
    public void Eval_CallDepth_IsRestoredAfterError()
    {
        Evaluator evaluator = new(Builtins.CreateDefault(TextWriter.Null));
        LumenEnv env = new();

        LumenValue first = evaluator.Eval(ParserTestHelper.ParseValid("fn f() { return f(); } f()"), env);
        LumenValue second = evaluator.Eval(ParserTestHelper.ParseValid("fn g() { 1 } g()"), env);

        Assert.IsType<ErrorSignal>(first);
        Assert.Equal(new IntValue(1), second);
    }

    [Fact]
    public void Eval_DeepButBoundedRecursion_Succeeds()
    {
        EvalTestHelper.AssertInt(500, "fn count(n) { if (n == 0) { 0 } else { 1 + count(n - 1) } } count(500)");
    }

    [Theory]
    [InlineData("let apply := fn(f, x) { f(x) }; apply(fn(x) { x * 3 }, 4)", 12L)]
    [InlineData("let adder := fn(a) { fn(b) { a + b } }; adder(2)(3)", 5L)]
    [InlineData("let compose := fn(f, g) { fn(x) { f(g(x)) } }; compose(fn(x) { x + 1 }, fn(x) { x * 2 })(5)", 11L)]
    public void Eval_HigherOrderFunctions_Work(string input, long expected)
    {
        EvalTestHelper.AssertInt(expected, input);
    }

    [Fact]
    public void Eval_BuiltinPassedAsValue_IsCallable()
    {
        StringWriter output = new();

        EvalTestHelper.Eval("let p := puts; p(42)", output);

        Assert.Equal("42", output.ToString().Trim());
    }

    [Fact]
    public void Eval_ArgumentError_PropagatesBeforeCall()
    {
        EvalTestHelper.AssertError("division by zero", "let f := fn(a, b) { a }; f(1 / 0, 2)");
    }

    [Fact]
    public void Eval_ErrorInsideBody_CarriesOriginalPosition()
    {
        ErrorSignal error = EvalTestHelper.EvalError("let f := fn() {\n  1 + \"a\"\n};\nf()");

        Assert.Equal("[line 2:5] type mismatch: Int + String", error.Inspect());
    }

    [Fact]
    public void Eval_LetInsideBody_DoesNotLeak()
    {
        EvalTestHelper.AssertError("undefined variable: inner", "let f := fn() { let inner := 1; inner }; f(); inner");
    }

    [Fact]
    public void Eval_ParameterShadowsBuiltin()
    {
        EvalTestHelper.AssertInt(7, "let f := fn(puts) { puts }; f(7)");
    }
}
