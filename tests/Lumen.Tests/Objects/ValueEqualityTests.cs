using Lumen.Core.Ast;
using Lumen.Core.Objects;
using Lumen.Tests.Parsing;
using LumenEnv = Lumen.Core.Objects.Environment;

namespace Lumen.Tests.Objects;

public class ValueEqualityTests
{
    private static ArrayValue Arr(params LumenValue[] items) => new(items);

    private static FunctionValue Fn(LumenEnv env) =>
        new(Assert.IsType<FunctionLiteral>(ParserTestHelper.ParseExpression("fn() { }")), env);

    [Fact]
    public void Equals_ScalarValues_UseValueEquality()
    {
        Assert.Equal(new IntValue(1), new IntValue(1));
        Assert.NotEqual(new IntValue(1), new IntValue(2));
        Assert.Equal(new StringValue("a"), new StringValue("a"));
        Assert.Equal(new FloatValue(1.5), new FloatValue(1.5));
        Assert.Equal(BoolValue.True, new BoolValue(true));
        Assert.Equal(NullValue.Instance, NullValue.Instance);
    }

    [Fact]
    public void Equals_IntAndFloat_AreDifferentValues()
    {
        // 數值層的 1 == 1.0 是 Evaluator 的比較語意，不是 record 相等。
        Assert.NotEqual<LumenValue>(new IntValue(1), new FloatValue(1.0));
    }

    [Fact]
    public void Equals_ArraysWithSameElements_AreEqual()
    {
        Assert.Equal(Arr(new IntValue(1), new IntValue(2)), Arr(new IntValue(1), new IntValue(2)));
        Assert.Equal(Arr(new IntValue(1), Arr(new IntValue(2))), Arr(new IntValue(1), Arr(new IntValue(2))));
        Assert.Equal(Arr(), Arr());
    }

    [Fact]
    public void Equals_ArraysWithDifferentOrderOrLength_AreNotEqual()
    {
        Assert.NotEqual(Arr(new IntValue(1), new IntValue(2)), Arr(new IntValue(2), new IntValue(1)));
        Assert.NotEqual(Arr(new IntValue(1)), Arr(new IntValue(1), new IntValue(1)));
    }

    [Fact]
    public void GetHashCode_EqualArrays_Collapse()
    {
        HashSet<LumenValue> set = [Arr(new IntValue(1), new IntValue(2)), Arr(new IntValue(1), new IntValue(2))];

        Assert.Single(set);
    }

    [Fact]
    public void Equals_HashesWithSamePairsInDifferentOrder_AreEqual()
    {
        HashValue a = new HashValue().With(new StringValue("x"), new IntValue(1)).With(new StringValue("y"), new IntValue(2));
        HashValue b = new HashValue().With(new StringValue("y"), new IntValue(2)).With(new StringValue("x"), new IntValue(1));

        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_HashesWithDifferentValues_AreNotEqual()
    {
        HashValue a = new HashValue().With(new StringValue("x"), new IntValue(1));
        HashValue b = new HashValue().With(new StringValue("x"), new IntValue(2));
        HashValue c = new HashValue().With(new StringValue("x"), new IntValue(1)).With(new StringValue("y"), new IntValue(1));

        Assert.NotEqual(a, b);
        Assert.NotEqual(a, c);
    }

    [Fact]
    public void Equals_SameFunctionValue_IsEqualToItself()
    {
        FunctionValue fn = Fn(new LumenEnv());

        Assert.Equal(fn, fn);
        Assert.True(fn.Equals((LumenValue)fn));
    }

    [Fact]
    public void Equals_TwoFunctionValuesWithIdenticalSource_AreNotEqual()
    {
        LumenEnv env = new();

        Assert.NotEqual(Fn(env), Fn(env));
    }

    [Fact]
    public void Equals_SelfReferencingClosure_DoesNotOverflow()
    {
        LumenEnv env = new();
        FunctionValue fn = Fn(env);
        env.Define("f", fn);

        Assert.Equal(fn, fn);
        Assert.NotEqual(fn, Fn(env));
        _ = fn.GetHashCode();
    }

    [Fact]
    public void Equals_BuiltinValues_UseReferenceEquality()
    {
        BuiltinValue a = new("len", static _ => NullValue.Instance);
        BuiltinValue b = new("len", static _ => NullValue.Instance);

        Assert.Equal(a, a);
        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Equals_ArrayContainingFunction_ComparesFunctionByReference()
    {
        FunctionValue fn = Fn(new LumenEnv());

        Assert.Equal(Arr(fn), Arr(fn));
        Assert.NotEqual(Arr(fn), Arr(Fn(new LumenEnv())));
    }
}
