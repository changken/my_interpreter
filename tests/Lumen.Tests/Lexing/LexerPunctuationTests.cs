using Lumen.Core.Lexing;
using Lumen.Core.Tokens;

namespace Lumen.Tests.Lexing;

public class LexerPunctuationTests
{
    [Theory]
    [InlineData(":", TokenType.Colon, ":")]
    [InlineData(":=", TokenType.Assign, ":=")]
    [InlineData("=", TokenType.Illegal, "=")]
    [InlineData("==", TokenType.Eq, "==")]
    [InlineData("!", TokenType.Bang, "!")]
    [InlineData("!=", TokenType.NotEq, "!=")]
    [InlineData("<", TokenType.Lt, "<")]
    [InlineData("<=", TokenType.LtEq, "<=")]
    [InlineData(">", TokenType.Gt, ">")]
    [InlineData(">=", TokenType.GtEq, ">=")]
    [InlineData("&", TokenType.Illegal, "&")]
    [InlineData("&&", TokenType.And, "&&")]
    [InlineData("|", TokenType.Illegal, "|")]
    [InlineData("||", TokenType.Or, "||")]
    [InlineData("*", TokenType.Star, "*")]
    [InlineData("**", TokenType.StarStar, "**")]
    [InlineData("/", TokenType.Slash, "/")]
    [InlineData("+", TokenType.Plus, "+")]
    [InlineData("-", TokenType.Minus, "-")]
    [InlineData("%", TokenType.Percent, "%")]
    [InlineData(",", TokenType.Comma, ",")]
    [InlineData(";", TokenType.Semicolon, ";")]
    [InlineData("(", TokenType.LParen, "(")]
    [InlineData(")", TokenType.RParen, ")")]
    [InlineData("{", TokenType.LBrace, "{")]
    [InlineData("}", TokenType.RBrace, "}")]
    [InlineData("[", TokenType.LBracket, "[")]
    [InlineData("]", TokenType.RBracket, "]")]
    [InlineData("@", TokenType.Illegal, "@")]
    [InlineData("#", TokenType.Illegal, "#")]
    [InlineData("$", TokenType.Illegal, "$")]
    [InlineData("^", TokenType.Illegal, "^")]
    [InlineData("~", TokenType.Illegal, "~")]
    public void Tokenize_SinglePunctuation_ProducesExpectedTypeAndLiteral(
        string input, TokenType expectedType, string expectedLiteral)
    {
        List<Token> tokens = LexerTestHelper.Tokenize(input);

        Assert.Equal(2, tokens.Count);
        Assert.Equal(new Token(expectedType, expectedLiteral, 1, 1), tokens[0]);
        Assert.Equal(TokenType.Eof, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_AdjacentTwoCharOperators_DoNotMerge()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("<=>=");

        Assert.Equal(TokenType.LtEq, tokens[0].Type);
        Assert.Equal(TokenType.GtEq, tokens[1].Type);
        Assert.Equal(TokenType.Eof, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_StarStarStar_ProducesStarStarThenStar()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("***");

        Assert.Equal(TokenType.StarStar, tokens[0].Type);
        Assert.Equal(TokenType.Star, tokens[1].Type);
        Assert.Equal(TokenType.Eof, tokens[2].Type);
    }
}
