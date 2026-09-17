using Lumen.Core.Ast;
using Lumen.Core.Tokens;

namespace Lumen.Tests.Parsing;

public class ParserStatementTests
{
    private static IStatement ParseSingle(string input) =>
        Assert.Single(ParserTestHelper.ParseValid(input).Statements);

    [Theory]
    [InlineData("let x := 5;", "x", "5")]
    [InlineData("let y := true;", "y", "true")]
    [InlineData("let foobar := y;", "foobar", "y")]
    [InlineData("let sum := a + b * c;", "sum", "(a + (b * c))")]
    public void ParseProgram_LetStatement_BindsNameToValue(string input, string name, string value)
    {
        LetStatement node = Assert.IsType<LetStatement>(ParseSingle(input));

        Assert.Equal(name, node.Name.Name);
        Assert.Equal(value, node.Value.ToString());
    }

    [Fact]
    public void ParseProgram_AssignStatement_ProducesAssignNode()
    {
        AssignStatement node = Assert.IsType<AssignStatement>(ParseSingle("x := x + 1;"));

        Assert.Equal("x", node.Name.Name);
        Assert.Equal("x := (x + 1);", node.ToString());
    }

    [Theory]
    [InlineData("x += 1;", "x := (x + 1);")]
    [InlineData("x -= 1;", "x := (x - 1);")]
    [InlineData("x *= 2;", "x := (x * 2);")]
    [InlineData("x /= 2;", "x := (x / 2);")]
    [InlineData("x += y * 2;", "x := (x + (y * 2));")]
    [InlineData("x -= -1;", "x := (x - (-1));")]
    [InlineData("x *= 1 + 2;", "x := (x * (1 + 2));")]
    [InlineData("x /= 2 * 2;", "x := (x / (2 * 2));")]
    [InlineData("x += a && b;", "x := (x + (a && b));")]
    public void ParseProgram_CompoundAssignStatement_DesugarsToAssignWithInfix(string input, string expected)
    {
        AssignStatement node = Assert.IsType<AssignStatement>(ParseSingle(input));

        Assert.Equal("x", node.Name.Name);
        InfixExpression infix = Assert.IsType<InfixExpression>(node.Value);
        Assert.Equal("x", Assert.IsType<Identifier>(infix.Left).Name);
        Assert.Equal(expected, node.ToString());
    }

    [Fact]
    public void ParseProgram_CompoundAssignStatement_SyntheticOperatorTokenKeepsPositionAndBareLiteral()
    {
        AssignStatement node = Assert.IsType<AssignStatement>(ParseSingle("x -= 1;"));

        InfixExpression infix = Assert.IsType<InfixExpression>(node.Value);
        Assert.Equal(TokenType.Minus, infix.Operator);
        Assert.Equal(new Token(TokenType.Minus, "-", 1, 3), infix.Token);
    }

    [Fact]
    public void ParseProgram_NamedFunctionDeclaration_DesugarsToLetWithNamedLiteral()
    {
        LetStatement node = Assert.IsType<LetStatement>(ParseSingle("fn add(a, b) { return a + b; }"));

        Assert.Equal("add", node.Name.Name);
        FunctionLiteral fn = Assert.IsType<FunctionLiteral>(node.Value);
        Assert.Equal("add", fn.Name);
        Assert.Equal(["a", "b"], fn.Parameters.Select(p => p.Name));
        Assert.Equal("fn add(a, b) { return (a + b); }", node.ToString());
    }

    [Fact]
    public void ParseProgram_NamedFunctionDeclaration_NeedsNoTerminator()
    {
        Program program = ParserTestHelper.ParseValid("fn f() { } 1");

        Assert.Equal(2, program.Statements.Count);
    }

    [Fact]
    public void ParseProgram_LetBoundAnonymousFunction_HasNoName()
    {
        LetStatement node = Assert.IsType<LetStatement>(ParseSingle("let double := fn(n) { n * 2 };"));

        Assert.Null(Assert.IsType<FunctionLiteral>(node.Value).Name);
        Assert.Equal("let double := fn(n) { (n * 2); };", node.ToString());
    }

    [Theory]
    [InlineData("return 5;", "5")]
    [InlineData("return x + y;", "(x + y)")]
    [InlineData("return fn() { 1 };", "fn() { 1; }")]
    public void ParseProgram_ReturnStatementWithValue_CarriesValue(string input, string value)
    {
        ReturnStatement node = Assert.IsType<ReturnStatement>(ParseSingle(input));

        Assert.Equal(value, node.Value?.ToString());
    }

    [Fact]
    public void ParseProgram_BareReturn_HasNullValue()
    {
        ReturnStatement node = Assert.IsType<ReturnStatement>(ParseSingle("return;"));

        Assert.Null(node.Value);
    }

    [Fact]
    public void ParseProgram_BlockTailExpressionWithoutSemicolon_IsExpressionStatement()
    {
        FunctionLiteral fn = Assert.IsType<FunctionLiteral>(ParserTestHelper.ParseExpression("fn() { let x := 1; x }"));

        Assert.Equal(2, fn.Body.Statements.Count);
        Assert.IsType<LetStatement>(fn.Body.Statements[0]);
        Assert.IsType<ExpressionStatement>(fn.Body.Statements[1]);
    }

    [Fact]
    public void ParseProgram_IfAtStatementLevel_IsExpressionStatementWithoutTerminator()
    {
        ExpressionStatement node = Assert.IsType<ExpressionStatement>(ParseSingle("if (x) { 1 } else { 2 }"));

        Assert.IsType<IfExpression>(node.Expression);
    }

    [Fact]
    public void ParseProgram_WhileStatement_ParsesConditionAndBody()
    {
        WhileStatement node = Assert.IsType<WhileStatement>(ParseSingle("while (x > 0) { x := x - 1; }"));

        Assert.Equal("(x > 0)", node.Condition.ToString());
        Assert.Equal("while ((x > 0)) { x := (x - 1); }", node.ToString());
    }

    [Theory]
    [InlineData("for (let i := 0; i < 10; i := i + 1) { puts(i); }", "for (let i := 0; (i < 10); i := (i + 1)) { puts(i); }")]
    [InlineData("for (; ; ) { }", "for (; ; ) { }")]
    [InlineData("for (;;) { }", "for (; ; ) { }")]
    [InlineData("for (; x < 3; ) { }", "for (; (x < 3); ) { }")]
    [InlineData("for (i := 0; ; i := i + 1) { break; }", "for (i := 0; ; i := (i + 1)) { break; }")]
    [InlineData("for (f(); c; g()) { }", "for (f(); c; g()) { }")]
    [InlineData("for (let i := 0; i < 3; i += 1) { }", "for (let i := 0; (i < 3); i := (i + 1)) { }")]
    [InlineData("for (i -= 1; i > 0; i /= 2) { }", "for (i := (i - 1); (i > 0); i := (i / 2)) { }")]
    public void ParseProgram_ForStatement_ClausesAreOptional(string input, string expected)
    {
        ForStatement node = Assert.IsType<ForStatement>(ParseSingle(input));

        Assert.Equal(expected, node.ToString());
    }

    [Fact]
    public void ParseProgram_ForStatement_ClausesHaveExpectedNodeTypes()
    {
        ForStatement node = Assert.IsType<ForStatement>(ParseSingle("for (let i := 0; i < 3; i := i + 1) { }"));

        Assert.IsType<LetStatement>(node.Init);
        Assert.IsType<InfixExpression>(node.Condition);
        Assert.IsType<AssignStatement>(node.Update);
    }

    [Fact]
    public void ParseProgram_ForStatementWithExpressionClauses_UsesExpressionStatements()
    {
        ForStatement node = Assert.IsType<ForStatement>(ParseSingle("for (a; ; f()) { }"));

        Assert.IsType<ExpressionStatement>(node.Init);
        Assert.Null(node.Condition);
        Assert.IsType<ExpressionStatement>(node.Update);
    }

    [Theory]
    [InlineData("while (true) { break; }")]
    [InlineData("while (true) { continue; }")]
    [InlineData("for (; ; ) { break; continue; }")]
    [InlineData("for (; ; ) { if (x) { break; } }")]
    [InlineData("while (a) { while (b) { break; } continue; }")]
    [InlineData("fn() { while (x) { break; } }")]
    public void ParseProgram_BreakAndContinueInsideLoop_AreValid(string input)
    {
        ParserTestHelper.ParseValid(input);
    }

    [Fact]
    public void ParseProgram_BreakAndContinue_ProduceDedicatedNodes()
    {
        WhileStatement node = Assert.IsType<WhileStatement>(ParseSingle("while (true) { break; continue; }"));

        Assert.IsType<BreakStatement>(node.Body.Statements[0]);
        Assert.IsType<ContinueStatement>(node.Body.Statements[1]);
    }

    [Fact]
    public void ParseProgram_MixedStatements_ProduceOneNodeEach()
    {
        Program program = ParserTestHelper.ParseValid("let x := 5; x := x + 1; x + 1");

        Assert.Equal(3, program.Statements.Count);
        Assert.Equal("let x := 5;\nx := (x + 1);\n(x + 1);", program.ToString());
    }
}
