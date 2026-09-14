using Lumen.Core.Lexing;

namespace Lumen.Tests.Lexing;

public class LexerStringTests
{
    [Fact]
    public void Tokenize_EmptyString_ProducesStringWithEmptyLiteral()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("\"\"");

        Assert.Equal(TokenType.String, tokens[0].Type);
        Assert.Equal(string.Empty, tokens[0].Literal);
    }

    [Fact]
    public void Tokenize_PlainString_ProducesDecodedLiteral()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("\"hello\"");

        Assert.Equal(TokenType.String, tokens[0].Type);
        Assert.Equal("hello", tokens[0].Literal);
    }

    [Theory]
    [InlineData("\"a\\nb\"", "a\nb")]
    [InlineData("\"a\\tb\"", "a\tb")]
    [InlineData("\"a\\\\b\"", "a\\b")]
    [InlineData("\"a\\\"b\"", "a\"b")]
    [InlineData("\"a\\0b\"", "a\0b")]
    public void Tokenize_StringWithKnownEscape_DecodesEscape(string input, string expectedLiteral)
    {
        List<Token> tokens = LexerTestHelper.Tokenize(input);

        Assert.Equal(TokenType.String, tokens[0].Type);
        Assert.Equal(expectedLiteral, tokens[0].Literal);
    }

    [Fact]
    public void Tokenize_StringWithUnknownEscape_ProducesIllegalWithContentSoFar()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("\"a\\qb\"");

        Assert.Equal(TokenType.Illegal, tokens[0].Type);
        Assert.Equal("a", tokens[0].Literal);
    }

    [Fact]
    public void Tokenize_UnterminatedStringAtNewline_ProducesIllegalWithContentSoFar()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("\"abc\ndef");

        Assert.Equal(TokenType.Illegal, tokens[0].Type);
        Assert.Equal("abc", tokens[0].Literal);
    }

    [Fact]
    public void Tokenize_UnterminatedStringAtEof_ProducesIllegalWithContentSoFar()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("\"abc");

        Assert.Equal(TokenType.Illegal, tokens[0].Type);
        Assert.Equal("abc", tokens[0].Literal);
        Assert.Equal(TokenType.Eof, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_UnterminatedStringAtNewline_ResumesLexingAfterNewline()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("\"abc\ndef");

        Assert.Equal(TokenType.Illegal, tokens[0].Type);
        Assert.Equal(TokenType.Ident, tokens[1].Type);
        Assert.Equal("def", tokens[1].Literal);
        Assert.Equal(TokenType.Eof, tokens[2].Type);
    }
}
