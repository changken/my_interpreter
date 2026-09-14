using Lumen.Core.Lexing;

namespace Lumen.Tests.Lexing;

public class LexerPositionTests
{
    [Fact]
    public void Tokenize_SingleCharOnFirstLine_HasLineOneColumnOne()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("x");

        Assert.Equal(1, tokens[0].Line);
        Assert.Equal(1, tokens[0].Column);
    }

    [Fact]
    public void Tokenize_TokenAfterLeadingSpaces_HasCorrectColumn()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("  x");

        Assert.Equal(1, tokens[0].Line);
        Assert.Equal(3, tokens[0].Column);
    }

    [Fact]
    public void Tokenize_TokenAfterLeadingTab_HasColumnAdvancedByOne()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("\tx");

        Assert.Equal(1, tokens[0].Line);
        Assert.Equal(2, tokens[0].Column);
    }

    [Fact]
    public void Tokenize_TokensOnDifferentLines_TrackLineAndResetColumn()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("x\ny");

        Assert.Equal(1, tokens[0].Line);
        Assert.Equal(1, tokens[0].Column);
        Assert.Equal(2, tokens[1].Line);
        Assert.Equal(1, tokens[1].Column);
    }

    [Fact]
    public void Tokenize_IdentifierAfterKeyword_HasColumnAtItsOwnStart()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("let x");

        Assert.Equal(1, tokens[0].Column);
        Assert.Equal(5, tokens[1].Column);
    }

    [Fact]
    public void Tokenize_EofAfterTrailingNewlines_HasLineAdvancedPastLastContent()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("x\ny\n");

        Token eof = tokens[^1];
        Assert.Equal(TokenType.Eof, eof.Type);
        Assert.Equal(3, eof.Line);
        Assert.Equal(1, eof.Column);
    }

    [Fact]
    public void Tokenize_UnterminatedStringError_ReportsLineOfOpeningQuote()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("x\n\"abc\ndef");

        Token illegal = tokens[1];
        Assert.Equal(TokenType.Illegal, illegal.Type);
        Assert.Equal(2, illegal.Line);
    }
}
