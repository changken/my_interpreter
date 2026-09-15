using Lumen.Core.Objects;

namespace Lumen.Tests.Evaluation;

public class ErrorCallStackTests
{
    [Fact]
    public void Error_ThroughNamedFunctions_ListsFramesInnermostFirst()
    {
        ErrorSignal error = EvalTestHelper.EvalError(
            "fn inner() { 1 / 0 }\nfn middle() { inner() }\nfn outer() { middle() }\nouter()");

        Assert.Equal(["inner", "middle", "outer"], error.CallStack);
        Assert.Equal("[line 1:16] division by zero\n  at inner\n  at middle\n  at outer", error.Inspect());
    }

    [Fact]
    public void Error_AtTopLevel_HasEmptyCallStack()
    {
        ErrorSignal error = EvalTestHelper.EvalError("1 / 0");

        Assert.Empty(error.CallStack);
        Assert.Equal("[line 1:3] division by zero", error.Inspect());
    }

    [Fact]
    public void Error_ThroughAnonymousFunction_UsesPlaceholderName()
    {
        ErrorSignal error = EvalTestHelper.EvalError("let f := fn() { 1 / 0 }; f()");

        Assert.Equal(["<anonymous>"], error.CallStack);
    }

    [Fact]
    public void Error_DeepRecursion_KeepsAtMostTenFrames()
    {
        ErrorSignal error = EvalTestHelper.EvalError("fn r(n) { if (n == 0) { 1 / 0 } else { r(n - 1) } } r(25)");

        Assert.Equal(10, error.CallStack.Count);
        Assert.All(error.CallStack, frame => Assert.Equal("r", frame));
    }

    [Fact]
    public void Error_OriginalPosition_IsPreservedThroughFrames()
    {
        ErrorSignal error = EvalTestHelper.EvalError("fn f() {\n  1 + \"a\"\n}\nfn g() { f() }\ng()");

        Assert.Equal(2, error.Line);
        Assert.Equal(5, error.Column);
    }

    [Fact]
    public void Error_FromBuiltinInsideFunction_HasFrameAndCallSitePosition()
    {
        ErrorSignal error = EvalTestHelper.EvalError("fn f() { len(1) } f()");

        Assert.Equal(["f"], error.CallStack);
        Assert.Equal("[line 1:13] argument to len must be String or Array, got Int\n  at f", error.Inspect());
    }

    [Fact]
    public void CallStackExceeded_ReportsTenFrames()
    {
        ErrorSignal error = EvalTestHelper.EvalError("fn f() { return f(); } f()");

        Assert.Contains("call stack exceeded", error.Inspect());
        Assert.Equal(10, error.CallStack.Count);
    }
}
