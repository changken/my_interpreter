using Lumen.Core.Ast;
using Lumen.Core.Evaluation;
using Lumen.Core.Objects;
using Lumen.Tests.Parsing;
using LumenEnv = Lumen.Core.Objects.Environment;

namespace Lumen.Tests.Evaluation;

internal static class EvalTestHelper
{
    public static LumenValue Eval(string input, TextWriter? output = null, LumenEnv? env = null)
    {
        Program program = ParserTestHelper.ParseValid(input);
        Evaluator evaluator = new(Builtins.CreateDefault(output ?? TextWriter.Null));
        return evaluator.Eval(program, env ?? new LumenEnv());
    }

    public static ErrorSignal EvalError(string input)
    {
        LumenValue result = Eval(input);
        return Assert.IsType<ErrorSignal>(result);
    }

    public static void AssertInt(long expected, string input) => Assert.Equal(new IntValue(expected), Eval(input));

    public static void AssertFloat(double expected, string input) => Assert.Equal(new FloatValue(expected), Eval(input));

    public static void AssertBool(bool expected, string input) => Assert.Equal(BoolValue.Of(expected), Eval(input));

    public static void AssertString(string expected, string input) => Assert.Equal(new StringValue(expected), Eval(input));

    public static void AssertNull(string input) => Assert.Same(NullValue.Instance, Eval(input));

    public static void AssertError(string expectedFragment, string input)
    {
        ErrorSignal error = EvalError(input);
        Assert.Contains(expectedFragment, error.Inspect());
    }
}
