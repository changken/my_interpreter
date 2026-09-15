using Lumen.Core.Ast;
using Lumen.Core.Parsing;
using Lumen.Core.Tokens;

namespace Lumen.Tests.Parsing;

public class ParserLiteralTests
{
    [Theory]
    [InlineData("5", 5L)]
    [InlineData("0", 0L)]
    [InlineData("9223372036854775807", long.MaxValue)]
    public void ParseProgram_IntegerLiteral_ProducesIntegerNodeWithValue(string input, long expected)
    {
        IntegerLiteral node = Assert.IsType<IntegerLiteral>(ParserTestHelper.ParseExpression(input));

        Assert.Equal(expected, node.Value);
        Assert.Equal(input, node.ToString());
    }

    [Theory]
    [InlineData("3.14", 3.14)]
    [InlineData("1.0", 1.0)]
    [InlineData("0.5", 0.5)]
    public void ParseProgram_FloatLiteral_ProducesFloatNodeWithInvariantValue(string input, double expected)
    {
        FloatLiteral node = Assert.IsType<FloatLiteral>(ParserTestHelper.ParseExpression(input));

        Assert.Equal(expected, node.Value);
        Assert.Equal(input, node.ToString());
    }

    [Fact]
    public void ParseProgram_StringLiteral_CarriesDecodedValue()
    {
        StringLiteral node = Assert.IsType<StringLiteral>(ParserTestHelper.ParseExpression("\"hi\\n\""));

        Assert.Equal("hi\n", node.Value);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    public void ParseProgram_BooleanLiteral_ProducesBooleanNode(string input, bool expected)
    {
        BooleanLiteral node = Assert.IsType<BooleanLiteral>(ParserTestHelper.ParseExpression(input));

        Assert.Equal(expected, node.Value);
    }

    [Fact]
    public void ParseProgram_NullLiteral_ProducesNullNode()
    {
        Assert.IsType<NullLiteral>(ParserTestHelper.ParseExpression("null"));
    }

    [Fact]
    public void ParseProgram_Identifier_ProducesIdentifierWithName()
    {
        Identifier node = Assert.IsType<Identifier>(ParserTestHelper.ParseExpression("foobar"));

        Assert.Equal("foobar", node.Name);
        Assert.Equal(new Token(TokenType.Ident, "foobar", 1, 1), node.Token);
    }

    [Fact]
    public void ParseProgram_IntegerTokenOutOfRange_RecordsErrorInsteadOfThrowing()
    {
        Parser parser = new(
        [
            new Token(TokenType.Int, "99999999999999999999", 1, 1),
            new Token(TokenType.Eof, string.Empty, 1, 21),
        ]);

        parser.ParseProgram();

        ParseError error = Assert.Single(parser.Errors);
        Assert.Contains("integer literal", error.Message);
        Assert.Equal((1, 1), (error.Line, error.Column));
    }

    [Fact]
    public void ParseProgram_TokenStreamWithoutEof_StillTerminates()
    {
        Parser parser = new([new Token(TokenType.Int, "1", 1, 1)]);

        Program program = parser.ParseProgram();

        Assert.Empty(parser.Errors);
        Assert.Equal("1;", program.ToString());
    }

    [Fact]
    public void ParseProgram_EmptyTokenStream_ProducesEmptyProgram()
    {
        Parser parser = new([]);

        Program program = parser.ParseProgram();

        Assert.Empty(parser.Errors);
        Assert.Empty(program.Statements);
    }

    [Fact]
    public void ParseProgram_EmptyInput_ProducesEmptyProgram()
    {
        Program program = ParserTestHelper.ParseValid("   // only a comment\n");

        Assert.Empty(program.Statements);
    }
}
