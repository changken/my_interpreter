using Lumen.Core.Objects;
using LumenEnv = Lumen.Core.Objects.Environment;

namespace Lumen.Tests.Objects;

public class EnvironmentTests
{
    [Fact]
    public void TryGet_DefinedName_ReturnsValue()
    {
        LumenEnv env = new();
        env.Define("x", new IntValue(1));

        Assert.True(env.TryGet("x", out LumenValue? value));
        Assert.Equal(new IntValue(1), value);
    }

    [Fact]
    public void TryGet_UndefinedName_ReturnsFalse()
    {
        Assert.False(new LumenEnv().TryGet("nope", out _));
    }

    [Fact]
    public void TryGet_EnclosedScope_FindsOuterBinding()
    {
        LumenEnv outer = new();
        outer.Define("x", new IntValue(1));
        LumenEnv inner = new(outer);

        Assert.True(inner.TryGet("x", out LumenValue? value));
        Assert.Equal(new IntValue(1), value);
    }

    [Fact]
    public void Define_InInnerScope_ShadowsWithoutTouchingOuter()
    {
        LumenEnv outer = new();
        outer.Define("x", new IntValue(1));
        LumenEnv inner = new(outer);

        inner.Define("x", new IntValue(2));

        Assert.True(inner.TryGet("x", out LumenValue? innerValue));
        Assert.True(outer.TryGet("x", out LumenValue? outerValue));
        Assert.Equal(new IntValue(2), innerValue);
        Assert.Equal(new IntValue(1), outerValue);
    }

    [Fact]
    public void Define_SameNameTwice_Overwrites()
    {
        LumenEnv env = new();
        env.Define("x", new IntValue(1));
        env.Define("x", new IntValue(2));

        Assert.True(env.TryGet("x", out LumenValue? value));
        Assert.Equal(new IntValue(2), value);
        Assert.Single(env.Bindings);
    }

    [Fact]
    public void TryAssign_ExistingOuterBinding_UpdatesOuter()
    {
        LumenEnv outer = new();
        outer.Define("n", new IntValue(0));
        LumenEnv inner = new(outer);

        Assert.True(inner.TryAssign("n", new IntValue(5)));

        Assert.True(outer.TryGet("n", out LumenValue? value));
        Assert.Equal(new IntValue(5), value);
        Assert.Empty(inner.Bindings);
    }

    [Fact]
    public void TryAssign_UndefinedName_ReturnsFalseAndDefinesNothing()
    {
        LumenEnv env = new();

        Assert.False(env.TryAssign("ghost", new IntValue(1)));
        Assert.False(env.TryGet("ghost", out _));
    }

    [Fact]
    public void Bindings_ListsOnlyLocalScopeInInsertionOrder()
    {
        LumenEnv outer = new();
        outer.Define("a", new IntValue(1));
        LumenEnv inner = new(outer);
        inner.Define("z", new IntValue(26));
        inner.Define("b", new IntValue(2));

        Assert.Equal(["z", "b"], inner.Bindings.Select(b => b.Key));
        Assert.Equal([new IntValue(26), new IntValue(2)], inner.Bindings.Select(b => b.Value));
    }

    [Fact]
    public void Outer_ExposesEnclosingScope()
    {
        LumenEnv outer = new();
        LumenEnv inner = new(outer);

        Assert.Same(outer, inner.Outer);
        Assert.Null(outer.Outer);
    }
}
