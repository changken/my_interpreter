using Lumen.Core.Ast;
using Lumen.Core.Objects;
using Lumen.Tests.Parsing;

namespace Lumen.Tests.Objects;

public class InspectTests
{
    [Theory]
    [InlineData(0L, "0")]
    [InlineData(42L, "42")]
    [InlineData(-7L, "-7")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    public void Inspect_IntValue_PrintsInvariantDigits(long value, string expected)
    {
        Assert.Equal(expected, new IntValue(value).Inspect());
    }

    [Theory]
    [InlineData(3.14, "3.14")]
    [InlineData(1.0, "1.0")]
    [InlineData(-2.0, "-2.0")]
    [InlineData(0.5, "0.5")]
    [InlineData(1e16, "10000000000000000.0")]
    [InlineData(1e300, "1E+300")]
    [InlineData(1.5e-7, "1.5E-07")]
    [InlineData(0.1, "0.1")]
    public void Inspect_FloatValue_AlwaysDistinguishableFromInt(double value, string expected)
    {
        Assert.Equal(expected, new FloatValue(value).Inspect());
    }

    [Fact]
    public void Inspect_BoolValue_PrintsKeyword()
    {
        Assert.Equal("true", BoolValue.True.Inspect());
        Assert.Equal("false", BoolValue.False.Inspect());
    }

    [Fact]
    public void Inspect_NullValue_PrintsNull()
    {
        Assert.Equal("null", NullValue.Instance.Inspect());
    }

    [Fact]
    public void Inspect_StringValue_PrintsRawTextWithoutQuotes()
    {
        Assert.Equal("hello", new StringValue("hello").Inspect());
    }

    [Fact]
    public void Inspect_ArrayValue_QuotesNestedStrings()
    {
        ArrayValue array = new([new IntValue(1), new StringValue("a"), NullValue.Instance, new FloatValue(2.0)]);

        Assert.Equal("[1, \"a\", null, 2.0]", array.Inspect());
    }

    [Fact]
    public void Inspect_NestedStringWithEscapes_ReEscapes()
    {
        ArrayValue array = new([new StringValue("a\"b\n")]);

        Assert.Equal("[\"a\\\"b\\n\"]", array.Inspect());
    }

    [Fact]
    public void Inspect_EmptyArray_PrintsBrackets()
    {
        Assert.Equal("[]", new ArrayValue([]).Inspect());
    }

    [Fact]
    public void Inspect_HashValue_PreservesInsertionOrder()
    {
        HashValue hash = new HashValue()
            .With(new StringValue("b"), new IntValue(2))
            .With(new StringValue("a"), new IntValue(1))
            .With(new IntValue(3), BoolValue.True);

        Assert.Equal("{\"b\": 2, \"a\": 1, 3: true}", hash.Inspect());
    }

    [Fact]
    public void Inspect_EmptyHash_PrintsBraces()
    {
        Assert.Equal("{}", new HashValue().Inspect());
    }

    [Fact]
    public void Inspect_FunctionValue_PrintsCanonicalSource()
    {
        FunctionLiteral literal = Assert.IsType<FunctionLiteral>(ParserTestHelper.ParseExpression("fn(a, b) { a + b }"));
        FunctionValue fn = new(literal, new Lumen.Core.Objects.Environment());

        Assert.Equal("fn(a, b) { (a + b); }", fn.Inspect());
    }

    [Fact]
    public void Inspect_BuiltinValue_PrintsName()
    {
        BuiltinValue builtin = new("len", static _ => NullValue.Instance);

        Assert.Equal("<builtin len>", builtin.Inspect());
    }

    [Theory]
    [InlineData(typeof(IntValue), "Int")]
    [InlineData(typeof(FloatValue), "Float")]
    [InlineData(typeof(BoolValue), "Bool")]
    [InlineData(typeof(StringValue), "String")]
    [InlineData(typeof(NullValue), "Null")]
    [InlineData(typeof(ArrayValue), "Array")]
    [InlineData(typeof(HashValue), "Hash")]
    [InlineData(typeof(FunctionValue), "Function")]
    [InlineData(typeof(BuiltinValue), "Builtin")]
    public void TypeName_EveryValueType_MatchesSpecName(Type type, string expected)
    {
        LumenValue value = type.Name switch
        {
            nameof(IntValue) => new IntValue(1),
            nameof(FloatValue) => new FloatValue(1),
            nameof(BoolValue) => BoolValue.True,
            nameof(StringValue) => new StringValue(""),
            nameof(NullValue) => NullValue.Instance,
            nameof(ArrayValue) => new ArrayValue([]),
            nameof(HashValue) => new HashValue(),
            nameof(FunctionValue) => new FunctionValue(
                Assert.IsType<FunctionLiteral>(ParserTestHelper.ParseExpression("fn() { }")),
                new Lumen.Core.Objects.Environment()),
            nameof(BuiltinValue) => new BuiltinValue("x", static _ => NullValue.Instance),
            _ => throw new InvalidOperationException(type.Name),
        };

        Assert.Equal(expected, value.TypeName);
    }

    [Fact]
    public void Inspect_ErrorSignal_UsesLineColumnPrefix()
    {
        ErrorSignal error = new(new StringValue("type mismatch: Int + String"), 3, 12);

        Assert.Equal("[line 3:12] type mismatch: Int + String", error.Inspect());
    }
}
