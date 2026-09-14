using Lumen.Core.Lexing;

namespace Lumen.Tests.Lexing;

public class LexerKeywordAndIdentifierTests
{
    [Theory]
    [InlineData("let", TokenType.Let)]
    [InlineData("fn", TokenType.Fn)]
    [InlineData("return", TokenType.Return)]
    [InlineData("if", TokenType.If)]
    [InlineData("else", TokenType.Else)]
    [InlineData("while", TokenType.While)]
    [InlineData("for", TokenType.For)]
    [InlineData("break", TokenType.Break)]
    [InlineData("continue", TokenType.Continue)]
    [InlineData("true", TokenType.True)]
    [InlineData("false", TokenType.False)]
    [InlineData("null", TokenType.Null)]
    public void Tokenize_Keyword_ProducesExpectedTokenType(string input, TokenType expectedType)
    {
        List<Token> tokens = LexerTestHelper.Tokenize(input);

        Assert.Equal(expectedType, tokens[0].Type);
        Assert.Equal(input, tokens[0].Literal);
        Assert.Equal(TokenType.Eof, tokens[1].Type);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("_foo")]
    [InlineData("myVar123")]
    [InlineData("lettuce")]
    [InlineData("iffy")]
    [InlineData("forever")]
    [InlineData("nullable")]
    [InlineData("a_b_c")]
    [InlineData("_")]
    public void Tokenize_NonKeywordIdentifier_ProducesIdent(string input)
    {
        List<Token> tokens = LexerTestHelper.Tokenize(input);

        Assert.Equal(TokenType.Ident, tokens[0].Type);
        Assert.Equal(input, tokens[0].Literal);
        Assert.Equal(TokenType.Eof, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_MultipleIdentifiersSeparatedBySpace_ProducesSeparateTokens()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("let letx x");

        Assert.Equal(TokenType.Let, tokens[0].Type);
        Assert.Equal(TokenType.Ident, tokens[1].Type);
        Assert.Equal("letx", tokens[1].Literal);
        Assert.Equal(TokenType.Ident, tokens[2].Type);
        Assert.Equal("x", tokens[2].Literal);
        Assert.Equal(TokenType.Eof, tokens[3].Type);
    }
}
