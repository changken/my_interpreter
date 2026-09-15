using Lumen.Core.Ast;

namespace Lumen.Tests.Parsing;

public class ParserCompositeTests
{
    [Fact]
    public void ParseProgram_GroupedExpression_UnwrapsToInnerNode()
    {
        IntegerLiteral node = Assert.IsType<IntegerLiteral>(ParserTestHelper.ParseExpression("(5)"));

        Assert.Equal(5, node.Value);
    }

    [Fact]
    public void ParseProgram_IfWithoutElse_HasNullAlternative()
    {
        IfExpression node = Assert.IsType<IfExpression>(ParserTestHelper.ParseExpression("if (x < y) { x }"));

        Assert.Null(node.Alternative);
        Assert.Equal("(x < y)", node.Condition.ToString());
        IStatement only = Assert.Single(node.Consequence.Statements);
        Assert.Equal("x", Assert.IsType<ExpressionStatement>(only).Expression.ToString());
        Assert.Equal("if ((x < y)) { x; }", node.ToString());
    }

    [Fact]
    public void ParseProgram_IfWithElse_HasBothBranches()
    {
        IfExpression node = Assert.IsType<IfExpression>(
            ParserTestHelper.ParseExpression("if (x < y) { x } else { y }"));

        Assert.NotNull(node.Alternative);
        Assert.Equal("if ((x < y)) { x; } else { y; }", node.ToString());
    }

    [Fact]
    public void ParseProgram_IfAsSubExpression_NestsInsideInfix()
    {
        Assert.Equal(
            "(1 + if (c) { 2; } else { 3; })",
            ParserTestHelper.ParseExpression("1 + if (c) { 2 } else { 3 }").ToString());
    }

    [Theory]
    [InlineData("fn() { }", new string[0])]
    [InlineData("fn(x) { }", new[] { "x" })]
    [InlineData("fn(x, y, z) { }", new[] { "x", "y", "z" })]
    public void ParseProgram_FunctionLiteral_CollectsParameters(string input, string[] expected)
    {
        FunctionLiteral node = Assert.IsType<FunctionLiteral>(ParserTestHelper.ParseExpression(input));

        Assert.Equal(expected, node.Parameters.Select(p => p.Name));
        Assert.Null(node.Name);
        Assert.Empty(node.Body.Statements);
    }

    [Fact]
    public void ParseProgram_FunctionLiteralBody_ParsesStatements()
    {
        FunctionLiteral node = Assert.IsType<FunctionLiteral>(
            ParserTestHelper.ParseExpression("fn(x, y) { x + y }"));

        Assert.Equal("fn(x, y) { (x + y); }", node.ToString());
    }

    [Theory]
    [InlineData("f()", "f()")]
    [InlineData("f(1)", "f(1)")]
    [InlineData("add(1, 2 * 3, 4 + 5)", "add(1, (2 * 3), (4 + 5))")]
    public void ParseProgram_CallExpression_CollectsArguments(string input, string expected)
    {
        CallExpression node = Assert.IsType<CallExpression>(ParserTestHelper.ParseExpression(input));

        Assert.Equal(expected, node.ToString());
    }

    [Fact]
    public void ParseProgram_CallOnFunctionLiteral_UsesLiteralAsCallee()
    {
        CallExpression node = Assert.IsType<CallExpression>(
            ParserTestHelper.ParseExpression("fn(x) { x }(5)"));

        Assert.IsType<FunctionLiteral>(node.Function);
        Assert.Equal("fn(x) { x; }(5)", node.ToString());
    }

    [Theory]
    [InlineData("[]", 0, "[]")]
    [InlineData("[1]", 1, "[1]")]
    [InlineData("[1, 2 * 2, 3 + 3]", 3, "[1, (2 * 2), (3 + 3)]")]
    public void ParseProgram_ArrayLiteral_CollectsElements(string input, int count, string expected)
    {
        ArrayLiteral node = Assert.IsType<ArrayLiteral>(ParserTestHelper.ParseExpression(input));

        Assert.Equal(count, node.Elements.Count);
        Assert.Equal(expected, node.ToString());
    }

    [Fact]
    public void ParseProgram_EmptyHashLiteral_HasNoPairs()
    {
        HashLiteral node = Assert.IsType<HashLiteral>(ParserTestHelper.ParseExpression("{}"));

        Assert.Empty(node.Pairs);
    }

    [Fact]
    public void ParseProgram_HashLiteral_PreservesInsertionOrder()
    {
        HashLiteral node = Assert.IsType<HashLiteral>(
            ParserTestHelper.ParseExpression("{\"one\": 1, \"two\": 2, \"three\": 3}"));

        Assert.Equal(["one", "two", "three"], node.Pairs.Select(p => Assert.IsType<StringLiteral>(p.Key).Value));
        Assert.Equal([1L, 2L, 3L], node.Pairs.Select(p => Assert.IsType<IntegerLiteral>(p.Value).Value));
    }

    [Fact]
    public void ParseProgram_HashLiteral_AllowsExpressionKeysAndValues()
    {
        Assert.Equal(
            "{(a + 1): (b * 2), true: f(x)}",
            ParserTestHelper.ParseExpression("{a + 1: b * 2, true: f(x)}").ToString());
    }

    [Fact]
    public void ParseProgram_IndexExpression_ParsesIndexAsFullExpression()
    {
        IndexExpression node = Assert.IsType<IndexExpression>(ParserTestHelper.ParseExpression("myArray[1 + 1]"));

        Assert.Equal("myArray", node.Left.ToString());
        Assert.Equal("(1 + 1)", node.Index.ToString());
    }

    [Fact]
    public void ParseProgram_NestedFunctionAndIf_ParsesRecursively()
    {
        Assert.Equal(
            "fn(x) { if (x) { fn() { 1; }; } else { 2; }; }",
            ParserTestHelper.ParseExpression("fn(x) { if (x) { fn() { 1 } } else { 2 } }").ToString());
    }

    [Theory]
    [InlineData("5;")]
    [InlineData("5")]
    public void ParseProgram_ExpressionStatement_SemicolonIsOptional(string input)
    {
        Program program = ParserTestHelper.ParseValid(input);

        Assert.Single(program.Statements);
    }

    [Fact]
    public void ParseProgram_MultipleExpressionStatements_ProduceOneNodeEach()
    {
        Program program = ParserTestHelper.ParseValid("1; 2; 3");

        Assert.Equal(3, program.Statements.Count);
        Assert.Equal("1;\n2;\n3;", program.ToString());
    }
}
