using Lumen.Core.Evaluation;
using Lumen.Tests.Parsing;
using LumenEnv = Lumen.Core.Objects.Environment;

namespace Lumen.Tests.Evaluation;

public class CancellationTests
{
    [Theory]
    [InlineData("while (true) { }")]
    [InlineData("for (; ; ) { }")]
    [InlineData("fn f() { while (true) { } } f()")]
    [InlineData("let i := 0; while (true) { i := i + 1; }")]
    public void Eval_CancelledDuringInfiniteLoop_ThrowsOperationCanceled(string input)
    {
        using CancellationTokenSource cts = new(TimeSpan.FromMilliseconds(100));
        Evaluator evaluator = new(Builtins.CreateDefault(TextWriter.Null), cts.Token);

        Assert.Throws<OperationCanceledException>(() =>
            evaluator.Eval(ParserTestHelper.ParseValid(input), new LumenEnv()));
    }

    [Fact]
    public void Eval_WithoutCancellation_RunsNormally()
    {
        Evaluator evaluator = new(Builtins.CreateDefault(TextWriter.Null), CancellationToken.None);

        Assert.Equal("3", evaluator.Eval(ParserTestHelper.ParseValid("1 + 2"), new LumenEnv()).Inspect());
    }
}
