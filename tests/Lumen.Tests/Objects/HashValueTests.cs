using Lumen.Core.Objects;

namespace Lumen.Tests.Objects;

public class HashValueTests
{
    [Fact]
    public void With_ReturnsNewHashAndLeavesOriginalUntouched()
    {
        HashValue original = new HashValue().With(new StringValue("a"), new IntValue(1));

        HashValue extended = original.With(new StringValue("b"), new IntValue(2));

        Assert.Equal(1, original.Count);
        Assert.Equal(2, extended.Count);
    }

    [Fact]
    public void With_ExistingKey_ReplacesValueKeepingPosition()
    {
        HashValue hash = new HashValue()
            .With(new StringValue("a"), new IntValue(1))
            .With(new StringValue("b"), new IntValue(2))
            .With(new StringValue("a"), new IntValue(9));

        Assert.Equal("{\"a\": 9, \"b\": 2}", hash.Inspect());
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        HashValue hash = new HashValue().With(new StringValue("a"), new IntValue(1));

        Assert.False(hash.TryGet(new StringValue("zzz"), out _));
    }

    [Fact]
    public void TryGet_IntegralFloatKey_FindsIntKey()
    {
        HashValue hash = new HashValue().With(new IntValue(1), new StringValue("one"));

        Assert.True(hash.TryGet(new FloatValue(1.0), out LumenValue? value));
        Assert.Equal(new StringValue("one"), value);
    }

    [Fact]
    public void With_IntegralFloatKey_IsStoredAsIntKey()
    {
        HashValue hash = new HashValue().With(new FloatValue(2.0), new StringValue("two"));

        Assert.True(hash.TryGet(new IntValue(2), out _));
        Assert.Equal("{2: two}".Replace("two", "\"two\""), hash.Inspect());
    }

    [Fact]
    public void TryGet_NonIntegralFloatKey_IsDistinctFromInt()
    {
        HashValue hash = new HashValue().With(new IntValue(1), new StringValue("one"));

        Assert.False(hash.TryGet(new FloatValue(1.5), out _));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TryGet_BoolKey_Works(bool key)
    {
        HashValue hash = new HashValue().With(new BoolValue(key), new IntValue(1));

        Assert.True(hash.TryGet(new BoolValue(key), out _));
        Assert.False(hash.TryGet(new BoolValue(!key), out _));
    }

    [Fact]
    public void IsValidKey_OnlyScalarsQualify()
    {
        Assert.True(HashValue.IsValidKey(new IntValue(1)));
        Assert.True(HashValue.IsValidKey(new FloatValue(1.5)));
        Assert.True(HashValue.IsValidKey(BoolValue.True));
        Assert.True(HashValue.IsValidKey(new StringValue("s")));

        Assert.False(HashValue.IsValidKey(NullValue.Instance));
        Assert.False(HashValue.IsValidKey(new ArrayValue([])));
        Assert.False(HashValue.IsValidKey(new HashValue()));
        Assert.False(HashValue.IsValidKey(new BuiltinValue("x", static _ => NullValue.Instance)));
    }

    [Fact]
    public void Pairs_EnumerateInInsertionOrder()
    {
        HashValue hash = new HashValue()
            .With(new StringValue("z"), new IntValue(1))
            .With(new StringValue("a"), new IntValue(2))
            .With(new IntValue(0), new IntValue(3));

        Assert.Equal(["z", "a", "0"], hash.Pairs.Select(p => p.Key.Inspect()));
    }
}
