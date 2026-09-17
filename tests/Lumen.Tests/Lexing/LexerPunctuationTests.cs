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
    [InlineData("+=", TokenType.PlusEq, "+=")]
    [InlineData("-=", TokenType.MinusEq, "-=")]
    [InlineData("*=", TokenType.StarEq, "*=")]
    [InlineData("/=", TokenType.SlashEq, "/=")]
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

    [Fact]
    public void Tokenize_StarStarEquals_DoesNotFormCompoundToken()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("**=");

        Assert.Equal(TokenType.StarStar, tokens[0].Type);
        Assert.Equal(TokenType.Illegal, tokens[1].Type);
        Assert.Equal(TokenType.Eof, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_CompoundAssignWithoutSpaces_SplitsAroundOperator()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("x+=1");

        Assert.Equal(new Token(TokenType.Ident, "x", 1, 1), tokens[0]);
        Assert.Equal(new Token(TokenType.PlusEq, "+=", 1, 2), tokens[1]);
        Assert.Equal(new Token(TokenType.Int, "1", 1, 4), tokens[2]);
        Assert.Equal(TokenType.Eof, tokens[3].Type);
    }

    [Fact]
    public void Tokenize_SlashEqBeforeComment_ProducesSlashEqAndSkipsComment()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("x /= 2 // halve");

        TokenType[] expected = [TokenType.Ident, TokenType.SlashEq, TokenType.Int, TokenType.Eof];
        Assert.Equal(expected, tokens.Select(t => t.Type));
    }
}
