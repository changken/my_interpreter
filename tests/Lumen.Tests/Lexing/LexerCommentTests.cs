using Lumen.Core.Lexing;
using Lumen.Core.Tokens;

namespace Lumen.Tests.Lexing;

public class LexerCommentTests
{
    [Fact]
    public void Tokenize_LineConsistingOnlyOfComment_ProducesOnlyEof()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("// this is a comment");

        Assert.Single(tokens);
        Assert.Equal(TokenType.Eof, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_EmptyComment_ProducesOnlyEof()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("//");

        Assert.Single(tokens);
        Assert.Equal(TokenType.Eof, tokens[0].Type);
    }

    [Fact]
    public void Tokenize_CodeFollowedByTrailingComment_IgnoresComment()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("let x := 1; // comment\nx");

        Assert.Equal(TokenType.Let, tokens[0].Type);
        Assert.Equal(TokenType.Ident, tokens[1].Type);
        Assert.Equal(TokenType.Assign, tokens[2].Type);
        Assert.Equal(TokenType.Int, tokens[3].Type);
        Assert.Equal(TokenType.Semicolon, tokens[4].Type);
        Assert.Equal(TokenType.Ident, tokens[5].Type);
        Assert.Equal("x", tokens[5].Literal);
        Assert.Equal(2, tokens[5].Line);
        Assert.Equal(TokenType.Eof, tokens[6].Type);
    }

    [Fact]
    public void Tokenize_SingleSlash_ProducesDivisionNotComment()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("1 / 2");

        Assert.Equal(TokenType.Int, tokens[0].Type);
        Assert.Equal(TokenType.Slash, tokens[1].Type);
        Assert.Equal(TokenType.Int, tokens[2].Type);
    }

    [Fact]
    public void Tokenize_ConsecutiveCommentLines_AreBothSkipped()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("// first\n// second\nx");

        Assert.Equal(2, tokens.Count);
        Assert.Equal(new Token(TokenType.Ident, "x", 3, 1), tokens[0]);
        Assert.Equal(TokenType.Eof, tokens[1].Type);
    }
}
