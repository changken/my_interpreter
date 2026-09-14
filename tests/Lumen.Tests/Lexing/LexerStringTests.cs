using Lumen.Core.Lexing;
using Lumen.Core.Tokens;

namespace Lumen.Tests.Lexing;

public class LexerStringTests
{
    [Fact]
    public void Tokenize_EmptyString_ProducesStringWithEmptyLiteral() =>
        LexerTestHelper.AssertToken("\"\"", 0, new Token(TokenType.String, string.Empty, 1, 1));

    [Fact]
    public void Tokenize_PlainString_ProducesDecodedLiteral() =>
        LexerTestHelper.AssertToken(@"""hello""", 0, new Token(TokenType.String, "hello", 1, 1));

    // verbatim string（@"..."）：反斜線不會被當跳脫，"" 代表一個字面上的 "。
    // 第一個參數是 Lumen 原始碼，不用再把 \n、\\ 這些 escape 序列雙重跳脫成 C# 語法。
    [Theory]
    [InlineData(@"""a\nb""", "a\nb")]
    [InlineData(@"""a\tb""", "a\tb")]
    [InlineData(@"""a\\b""", "a\\b")]
    [InlineData(@"""a\""b""", "a\"b")]
    [InlineData(@"""a\0b""", "a\0b")]
    public void Tokenize_StringWithKnownEscape_DecodesEscape(string input, string expectedLiteral) =>
        LexerTestHelper.AssertToken(input, 0, new Token(TokenType.String, expectedLiteral, 1, 1));

    [Fact]
    public void Tokenize_StringWithUnknownEscape_ProducesIllegalWithContentSoFar() =>
        LexerTestHelper.AssertToken(@"""a\qb""", 0, new Token(TokenType.Illegal, "a", 1, 1));

    [Fact]
    public void Tokenize_UnterminatedStringAtNewline_ProducesIllegalWithContentSoFar() =>
        LexerTestHelper.AssertToken("\"abc\ndef", 0, new Token(TokenType.Illegal, "abc", 1, 1));

    [Fact]
    public void Tokenize_UnterminatedStringAtCarriageReturn_ProducesIllegalWithContentSoFar() =>
        // CRLF 檔案裡，字串沒閉合就先遇到 '\r'：不該把 '\r' 當成字串內容的一部分。
        LexerTestHelper.AssertToken("\"abc\r\ndef", 0, new Token(TokenType.Illegal, "abc", 1, 1));

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
