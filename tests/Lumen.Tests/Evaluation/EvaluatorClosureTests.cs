using Lumen.Core.Objects;

namespace Lumen.Tests.Evaluation;

public class EvaluatorClosureTests
{
    private const string Counter = """
        let counter := fn() {
            let n := 0;
            fn() { n := n + 1; return n; }
        };
        """;

    [Fact]
    public void Eval_CounterClosures_KeepIndependentState()
    {
        ArrayValue result = Assert.IsType<ArrayValue>(
            EvalTestHelper.Eval(Counter + "let c1 := counter(); let c2 := counter(); c1(); c1(); [c1(), c2()]"));

        Assert.Equal("[3, 1]", result.Inspect());
    }

    [Fact]
    public void Eval_Closure_CapturesBindingNotSnapshot()
    {
        EvalTestHelper.AssertInt(2, "let x := 1; let f := fn() { x }; x := 2; f()");
    }

    [Fact]
    public void Eval_Closure_CanMutateCapturedBinding()
    {
        EvalTestHelper.AssertInt(11, "let x := 1; let f := fn() { x := x + 10; }; f(); x");
    }

    [Fact]
    public void Eval_ClosureInForLoop_CapturesPerIterationBinding()
    {
        const string source = """
            let f0 := null; let f1 := null; let f2 := null;
            for (let i := 0; i < 3; i := i + 1) {
                if (i == 0) { f0 := fn() { i }; }
                if (i == 1) { f1 := fn() { i }; }
                if (i == 2) { f2 := fn() { i }; }
            }
            [f0(), f1(), f2()]
            """;

        ArrayValue result = Assert.IsType<ArrayValue>(EvalTestHelper.Eval(source));

        Assert.Equal("[0, 1, 2]", result.Inspect());
    }

    [Fact]
    public void Eval_ClosureInWhileLoop_SharesSingleBinding()
    {
        const string source = """
            let i := 0; let f := null;
            while (i < 3) {
                if (i == 0) { f := fn() { i }; }
                i := i + 1;
            }
            f()
            """;

        EvalTestHelper.AssertInt(3, source);
    }

    [Fact]
    public void Eval_ClosureMutatingForVariable_AffectsThatIterationOnly()
    {
        const string source = """
            let bump := null;
            let seen := 0;
            for (let i := 0; i < 3; i := i + 1) {
                if (i == 0) { bump := fn() { i := i + 100; i }; }
                seen := seen + i;
            }
            [bump(), seen]
            """;

        ArrayValue result = Assert.IsType<ArrayValue>(EvalTestHelper.Eval(source));

        Assert.Equal("[100, 3]", result.Inspect());
    }

    [Fact]
    public void Eval_NestedClosures_ResolveThroughScopeChain()
    {
        EvalTestHelper.AssertInt(6, "let a := 1; let f := fn() { let b := 2; fn() { let c := 3; a + b + c } }; f()()");
    }
}
