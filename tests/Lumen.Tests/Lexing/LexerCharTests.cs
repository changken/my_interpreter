using Lumen.Core.Lexing;
using Lumen.Core.Tokens;

namespace Lumen.Tests.Lexing;

public class LexerCharTests
{
    [Fact]
    public void Tokenize_PlainChar_ProducesCharToken() =>
        LexerTestHelper.AssertToken("'a'", 0, new Token(TokenType.Char, "a", 1, 1));

    [Fact]
    public void Tokenize_EmptyChar_ProducesIllegal() =>
        LexerTestHelper.AssertToken("''", 0, new Token(TokenType.Illegal, string.Empty, 1, 1));

    // verbatim string（@"..."）：反斜線不會被當跳脫，直接寫 Lumen 原始碼，不用雙重跳脫。
    [Theory]
    [InlineData(@"'\n'", "\n")]
    [InlineData(@"'\t'", "\t")]
    [InlineData(@"'\\'", "\\")]
    [InlineData(@"'\''", "'")]
    [InlineData(@"'\0'", "\0")]
    public void Tokenize_CharWithKnownEscape_DecodesEscape(string input, string expectedLiteral) =>
        LexerTestHelper.AssertToken(input, 0, new Token(TokenType.Char, expectedLiteral, 1, 1));

    [Fact]
    public void Tokenize_CharWithUnknownEscape_ProducesIllegalWithContentSoFar() =>
        LexerTestHelper.AssertToken(@"'\q'", 0, new Token(TokenType.Illegal, string.Empty, 1, 1));

    [Fact]
    public void Tokenize_MultiCharLiteral_ProducesIllegalWithFullContent() =>
        LexerTestHelper.AssertToken("'ab'", 0, new Token(TokenType.Illegal, "ab", 1, 1));

    [Fact]
    public void Tokenize_UnterminatedCharAtNewline_ProducesIllegalWithContentSoFar() =>
        LexerTestHelper.AssertToken("'a\ndef", 0, new Token(TokenType.Illegal, "a", 1, 1));

    [Fact]
    public void Tokenize_UnterminatedCharAtCarriageReturn_ProducesIllegalWithContentSoFar() =>
        // CRLF 檔案裡，char 沒閉合就先遇到 '\r'：不該把 '\r' 當成內容的一部分。
        LexerTestHelper.AssertToken("'a\r\ndef", 0, new Token(TokenType.Illegal, "a", 1, 1));

    [Fact]
    public void Tokenize_UnterminatedCharAtEof_ProducesIllegalWithContentSoFar()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("'a");

        Assert.Equal(TokenType.Illegal, tokens[0].Type);
        Assert.Equal("a", tokens[0].Literal);
        Assert.Equal(TokenType.Eof, tokens[1].Type);
    }

    [Fact]
    public void Tokenize_UnterminatedCharAtNewline_ResumesLexingAfterNewline()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("'a\ndef");

        Assert.Equal(TokenType.Illegal, tokens[0].Type);
        Assert.Equal(TokenType.Ident, tokens[1].Type);
        Assert.Equal("def", tokens[1].Literal);
        Assert.Equal(TokenType.Eof, tokens[2].Type);
    }
}
