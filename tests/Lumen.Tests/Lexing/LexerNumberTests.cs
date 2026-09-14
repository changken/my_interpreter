using System.Globalization;
using Lumen.Core.Lexing;
using Lumen.Core.Tokens;

namespace Lumen.Tests.Lexing;

public class LexerNumberTests
{
    [Theory]
    [InlineData("42", TokenType.Int, "42")]
    [InlineData("0", TokenType.Int, "0")]
    [InlineData("123456789", TokenType.Int, "123456789")]
    [InlineData("3.14", TokenType.Float, "3.14")]
    [InlineData("0.0", TokenType.Float, "0.0")]
    [InlineData("0.5", TokenType.Float, "0.5")]
    public void Tokenize_ValidNumberLiteral_ProducesExpectedTypeAndLiteral(
        string input, TokenType expectedType, string expectedLiteral)
    {
        List<Token> tokens = LexerTestHelper.Tokenize(input);

        Assert.Equal(2, tokens.Count);
        Assert.Equal(new Token(expectedType, expectedLiteral, 1, 1), tokens[0]);
        Assert.Equal(TokenType.Eof, tokens[1].Type);
    }

    [Theory]
    [InlineData("1.", "1.")]
    [InlineData(".5", ".5")]
    [InlineData("10L", "10L")]
    [InlineData("1.5f", "1.5f")]
    [InlineData("1_000", "1_000")]
    [InlineData("0x1F", "0x1F")]
    [InlineData("0b101", "0b101")]
    public void Tokenize_MalformedNumberLiteral_ProducesSingleIllegalToken(
        string input, string expectedLiteral)
    {
        List<Token> tokens = LexerTestHelper.Tokenize(input);

        Assert.Equal(2, tokens.Count);
        Assert.Equal(new Token(TokenType.Illegal, expectedLiteral, 1, 1), tokens[0]);
        Assert.Equal(TokenType.Eof, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_IntegerLiteralOverflowingLong_ProducesIllegal()
    {
        const string tooLarge = "99999999999999999999";
        LexerTestHelper.AssertToken(tooLarge, 0, new Token(TokenType.Illegal, tooLarge, 1, 1));
    }

    [Fact]
    public void Tokenize_FloatLiteral_UnaffectedByDeDECurrentCulture()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");

            LexerTestHelper.AssertToken("3.14", 0, new Token(TokenType.Float, "3.14", 1, 1));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void Tokenize_NumberFollowedByOperator_StopsAtOperator()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("5+3");

        Assert.Equal(TokenType.Int, tokens[0].Type);
        Assert.Equal("5", tokens[0].Literal);
        Assert.Equal(TokenType.Plus, tokens[1].Type);
        Assert.Equal(TokenType.Int, tokens[2].Type);
        Assert.Equal("3", tokens[2].Literal);
    }
}
