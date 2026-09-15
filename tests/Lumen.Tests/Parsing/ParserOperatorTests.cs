using Lumen.Core.Ast;
using Lumen.Core.Tokens;

namespace Lumen.Tests.Parsing;

public class ParserOperatorTests
{
    [Theory]
    [InlineData("!x", TokenType.Bang, "(!x)")]
    [InlineData("-5", TokenType.Minus, "(-5)")]
    [InlineData("!true", TokenType.Bang, "(!true)")]
    public void ParseProgram_PrefixOperator_ProducesPrefixExpression(string input, TokenType op, string expected)
    {
        PrefixExpression node = Assert.IsType<PrefixExpression>(ParserTestHelper.ParseExpression(input));

        Assert.Equal(op, node.Operator);
        Assert.Equal(expected, node.ToString());
    }

    [Theory]
    [InlineData("a + b", TokenType.Plus)]
    [InlineData("a - b", TokenType.Minus)]
    [InlineData("a * b", TokenType.Star)]
    [InlineData("a / b", TokenType.Slash)]
    [InlineData("a % b", TokenType.Percent)]
    [InlineData("a ** b", TokenType.StarStar)]
    [InlineData("a == b", TokenType.Eq)]
    [InlineData("a != b", TokenType.NotEq)]
    [InlineData("a < b", TokenType.Lt)]
    [InlineData("a > b", TokenType.Gt)]
    [InlineData("a <= b", TokenType.LtEq)]
    [InlineData("a >= b", TokenType.GtEq)]
    public void ParseProgram_InfixOperator_ProducesInfixExpression(string input, TokenType op)
    {
        InfixExpression node = Assert.IsType<InfixExpression>(ParserTestHelper.ParseExpression(input));

        Assert.Equal(op, node.Operator);
        Assert.Equal("a", Assert.IsType<Identifier>(node.Left).Name);
        Assert.Equal("b", Assert.IsType<Identifier>(node.Right).Name);
        Assert.Equal($"({input})", node.ToString());
    }

    [Theory]
    [InlineData("a && b", TokenType.And)]
    [InlineData("a || b", TokenType.Or)]
    public void ParseProgram_LogicalOperator_ProducesLogicalExpressionNotInfix(string input, TokenType op)
    {
        IExpression expression = ParserTestHelper.ParseExpression(input);

        Assert.IsNotType<InfixExpression>(expression);
        LogicalExpression node = Assert.IsType<LogicalExpression>(expression);
        Assert.Equal(op, node.Operator);
        Assert.Equal($"({input})", node.ToString());
    }

    [Fact]
    public void ParseProgram_InfixExpression_TokenIsOperatorToken()
    {
        InfixExpression node = Assert.IsType<InfixExpression>(ParserTestHelper.ParseExpression("a + b"));

        Assert.Equal(new Token(TokenType.Plus, "+", 1, 3), node.Token);
    }
}
