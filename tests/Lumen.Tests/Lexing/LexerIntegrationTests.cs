using Lumen.Core.Lexing;
using Lumen.Core.Tokens;

namespace Lumen.Tests.Lexing;

public class LexerIntegrationTests
{
    [Fact]
    public void Tokenize_LetStatement_ProducesExpectedSequence()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("let x := 5;");

        TokenType[] expected =
        [
            TokenType.Let,
            TokenType.Ident,
            TokenType.Assign,
            TokenType.Int,
            TokenType.Semicolon,
            TokenType.Eof,
        ];
        Assert.Equal(expected, tokens.Select(t => t.Type));
        Assert.Equal("x", tokens[1].Literal);
        Assert.Equal("5", tokens[3].Literal);
    }

    [Fact]
    public void Tokenize_FunctionDeclaration_ProducesExpectedSequence()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("fn add(a, b) { return a + b; }");

        TokenType[] expected =
        [
            TokenType.Fn,
            TokenType.Ident,
            TokenType.LParen,
            TokenType.Ident,
            TokenType.Comma,
            TokenType.Ident,
            TokenType.RParen,
            TokenType.LBrace,
            TokenType.Return,
            TokenType.Ident,
            TokenType.Plus,
            TokenType.Ident,
            TokenType.Semicolon,
            TokenType.RBrace,
            TokenType.Eof,
        ];
        Assert.Equal(expected, tokens.Select(t => t.Type));
    }

    [Fact]
    public void Tokenize_ArrayAndHashLiterals_ProducesExpectedSequence()
    {
        List<Token> tokens = LexerTestHelper.Tokenize(
            """
            let arr := [1, 2, 3];
            let map := {"a": 1, "b": 2};
            """);

        TokenType[] expected =
        [
            TokenType.Let, TokenType.Ident, TokenType.Assign,
            TokenType.LBracket, TokenType.Int, TokenType.Comma,
            TokenType.Int, TokenType.Comma, TokenType.Int, TokenType.RBracket,
            TokenType.Semicolon,
            TokenType.Let, TokenType.Ident, TokenType.Assign,
            TokenType.LBrace,
            TokenType.String, TokenType.Colon, TokenType.Int, TokenType.Comma,
            TokenType.String, TokenType.Colon, TokenType.Int,
            TokenType.RBrace, TokenType.Semicolon,
            TokenType.Eof,
        ];
        Assert.Equal(expected, tokens.Select(t => t.Type));
    }

    [Fact]
    public void Tokenize_ControlFlowSnippet_ProducesExpectedSequence()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("""if (x > 3) { "big" } else { "small" }""");

        TokenType[] expected =
        [
            TokenType.If, TokenType.LParen, TokenType.Ident, TokenType.Gt, TokenType.Int, TokenType.RParen,
            TokenType.LBrace, TokenType.String, TokenType.RBrace,
            TokenType.Else,
            TokenType.LBrace, TokenType.String, TokenType.RBrace,
            TokenType.Eof,
        ];
        Assert.Equal(expected, tokens.Select(t => t.Type));
    }

    [Fact]
    public void Tokenize_AnyInput_EndsWithExactlyOneEofToken()
    {
        List<Token> tokens = LexerTestHelper.Tokenize("let x := 5; x + 1");

        Assert.Equal(TokenType.Eof, tokens[^1].Type);
        Assert.Single(tokens, t => t.Type == TokenType.Eof);
    }
}
