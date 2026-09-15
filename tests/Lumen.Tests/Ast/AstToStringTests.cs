using Lumen.Core.Ast;
using Lumen.Core.Tokens;

namespace Lumen.Tests.Ast;

public class AstToStringTests
{
    private static Token Tok(TokenType type, string literal) => new(type, literal, 1, 1);

    private static Identifier Ident(string name) => new(Tok(TokenType.Ident, name), name);

    private static IntegerLiteral Int(long value) =>
        new(Tok(TokenType.Int, value.ToString(System.Globalization.CultureInfo.InvariantCulture)), value);

    private static InfixExpression Infix(IExpression left, TokenType op, string symbol, IExpression right) =>
        new(Tok(op, symbol), left, op, right);

    private static BlockStatement Block(params IStatement[] statements) =>
        new(Tok(TokenType.LBrace, "{"), statements);

    private static ExpressionStatement ExprStmt(IExpression expression) =>
        new(expression is Node n ? n.Token : Tok(TokenType.Illegal, ""), expression);

    // ------------------------------------------------------------------
    // Literals
    // ------------------------------------------------------------------

    [Fact]
    public void ToString_Identifier_PrintsName()
    {
        Assert.Equal("x", Ident("x").ToString());
    }

    [Fact]
    public void ToString_IntegerLiteral_PrintsLiteralText()
    {
        Assert.Equal("42", Int(42).ToString());
    }

    [Fact]
    public void ToString_FloatLiteral_PrintsOriginalLiteralText()
    {
        FloatLiteral node = new(Tok(TokenType.Float, "3.14"), 3.14);

        Assert.Equal("3.14", node.ToString());
    }

    [Theory]
    [InlineData("hello", "\"hello\"")]
    [InlineData("a\"b", "\"a\\\"b\"")]
    [InlineData("line\nbreak", "\"line\\nbreak\"")]
    [InlineData("tab\there", "\"tab\\there\"")]
    [InlineData("back\\slash", "\"back\\\\slash\"")]
    [InlineData("nul\0char", "\"nul\\0char\"")]
    public void ToString_StringLiteral_ReEscapesValue(string value, string expected)
    {
        StringLiteral node = new(Tok(TokenType.String, value), value);

        Assert.Equal(expected, node.ToString());
    }

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void ToString_BooleanLiteral_PrintsKeyword(bool value, string expected)
    {
        BooleanLiteral node = new(Tok(value ? TokenType.True : TokenType.False, expected), value);

        Assert.Equal(expected, node.ToString());
    }

    [Fact]
    public void ToString_NullLiteral_PrintsNull()
    {
        NullLiteral node = new(Tok(TokenType.Null, "null"));

        Assert.Equal("null", node.ToString());
    }

    // ------------------------------------------------------------------
    // Operators
    // ------------------------------------------------------------------

    [Theory]
    [InlineData(TokenType.Minus, "-", "(-x)")]
    [InlineData(TokenType.Bang, "!", "(!x)")]
    public void ToString_PrefixExpression_WrapsInParentheses(TokenType op, string symbol, string expected)
    {
        PrefixExpression node = new(Tok(op, symbol), op, Ident("x"));

        Assert.Equal(expected, node.ToString());
    }

    [Fact]
    public void ToString_NestedInfixExpression_ParenthesizesEveryLevel()
    {
        InfixExpression inner = Infix(Ident("a"), TokenType.Plus, "+", Ident("b"));
        InfixExpression outer = Infix(inner, TokenType.Star, "*", Ident("c"));

        Assert.Equal("((a + b) * c)", outer.ToString());
    }

    [Theory]
    [InlineData(TokenType.And, "&&", "(a && b)")]
    [InlineData(TokenType.Or, "||", "(a || b)")]
    public void ToString_LogicalExpression_WrapsInParentheses(TokenType op, string symbol, string expected)
    {
        LogicalExpression node = new(Tok(op, symbol), Ident("a"), op, Ident("b"));

        Assert.Equal(expected, node.ToString());
    }

    [Fact]
    public void ToString_IndexExpression_WrapsInParentheses()
    {
        IndexExpression node = new(Tok(TokenType.LBracket, "["), Ident("arr"), Int(0));

        Assert.Equal("(arr[0])", node.ToString());
    }

    // ------------------------------------------------------------------
    // Composite expressions
    // ------------------------------------------------------------------

    [Fact]
    public void ToString_IfExpressionWithoutElse_OmitsElseClause()
    {
        IfExpression node = new(Tok(TokenType.If, "if"), Ident("c"), Block(ExprStmt(Ident("y"))), null);

        Assert.Equal("if (c) { y; }", node.ToString());
    }

    [Fact]
    public void ToString_IfExpressionWithElse_PrintsBothBranches()
    {
        IfExpression node = new(
            Tok(TokenType.If, "if"),
            Ident("c"),
            Block(ExprStmt(Ident("y"))),
            Block(ExprStmt(Ident("z"))));

        Assert.Equal("if (c) { y; } else { z; }", node.ToString());
    }

    [Fact]
    public void ToString_FunctionLiteral_PrintsAnonymousForm()
    {
        FunctionLiteral node = new(
            Tok(TokenType.Fn, "fn"),
            [Ident("a"), Ident("b")],
            Block(new ReturnStatement(Tok(TokenType.Return, "return"), Infix(Ident("a"), TokenType.Plus, "+", Ident("b")))),
            Name: null);

        Assert.Equal("fn(a, b) { return (a + b); }", node.ToString());
    }

    [Fact]
    public void ToString_FunctionLiteralWithName_StillPrintsAnonymousForm()
    {
        FunctionLiteral node = new(Tok(TokenType.Fn, "fn"), [], Block(), Name: "add");

        Assert.Equal("fn() { }", node.ToString());
    }

    [Theory]
    [InlineData(0, "f()")]
    [InlineData(1, "f(1)")]
    [InlineData(3, "f(1, 2, 3)")]
    public void ToString_CallExpression_JoinsArgumentsWithCommas(int argumentCount, string expected)
    {
        IExpression[] arguments = Enumerable.Range(1, argumentCount).Select(i => (IExpression)Int(i)).ToArray();
        CallExpression node = new(Tok(TokenType.LParen, "("), Ident("f"), arguments);

        Assert.Equal(expected, node.ToString());
    }

    [Theory]
    [InlineData(0, "[]")]
    [InlineData(2, "[1, 2]")]
    public void ToString_ArrayLiteral_JoinsElementsWithCommas(int elementCount, string expected)
    {
        IExpression[] elements = Enumerable.Range(1, elementCount).Select(i => (IExpression)Int(i)).ToArray();
        ArrayLiteral node = new(Tok(TokenType.LBracket, "["), elements);

        Assert.Equal(expected, node.ToString());
    }

    [Fact]
    public void ToString_HashLiteral_PreservesInsertionOrder()
    {
        HashLiteral node = new(
            Tok(TokenType.LBrace, "{"),
            [
                new KeyValuePair<IExpression, IExpression>(new StringLiteral(Tok(TokenType.String, "b"), "b"), Int(2)),
                new KeyValuePair<IExpression, IExpression>(new StringLiteral(Tok(TokenType.String, "a"), "a"), Int(1)),
            ]);

        Assert.Equal("{\"b\": 2, \"a\": 1}", node.ToString());
    }

    [Fact]
    public void ToString_EmptyHashLiteral_PrintsBraces()
    {
        HashLiteral node = new(Tok(TokenType.LBrace, "{"), []);

        Assert.Equal("{}", node.ToString());
    }

    // ------------------------------------------------------------------
    // Statements
    // ------------------------------------------------------------------

    [Fact]
    public void ToString_LetStatement_PrintsWithTerminator()
    {
        LetStatement node = new(Tok(TokenType.Let, "let"), Ident("x"), Int(5));

        Assert.Equal("let x := 5;", node.ToString());
    }

    [Fact]
    public void ToString_LetStatementBindingSameNamedFunction_PrintsDeclarationSyntax()
    {
        FunctionLiteral fn = new(Tok(TokenType.Fn, "fn"), [Ident("a"), Ident("b")], Block(), Name: "add");
        LetStatement node = new(Tok(TokenType.Fn, "fn"), Ident("add"), fn);

        Assert.Equal("fn add(a, b) { }", node.ToString());
    }

    [Fact]
    public void ToString_LetStatementBindingAnonymousFunction_PrintsLetSyntax()
    {
        FunctionLiteral fn = new(Tok(TokenType.Fn, "fn"), [Ident("n")], Block(), Name: null);
        LetStatement node = new(Tok(TokenType.Let, "let"), Ident("double"), fn);

        Assert.Equal("let double := fn(n) { };", node.ToString());
    }

    [Fact]
    public void ToString_AssignStatement_PrintsWithTerminator()
    {
        AssignStatement node = new(Tok(TokenType.Ident, "x"), Ident("x"), Int(5));

        Assert.Equal("x := 5;", node.ToString());
    }

    [Fact]
    public void ToString_ReturnStatementWithValue_PrintsValue()
    {
        ReturnStatement node = new(Tok(TokenType.Return, "return"), Ident("x"));

        Assert.Equal("return x;", node.ToString());
    }

    [Fact]
    public void ToString_ReturnStatementWithoutValue_PrintsBareReturn()
    {
        ReturnStatement node = new(Tok(TokenType.Return, "return"), null);

        Assert.Equal("return;", node.ToString());
    }

    [Fact]
    public void ToString_ExpressionStatement_AlwaysPrintsTerminator()
    {
        ExpressionStatement node = ExprStmt(Infix(Ident("x"), TokenType.Plus, "+", Int(1)));

        Assert.Equal("(x + 1);", node.ToString());
    }

    [Fact]
    public void ToString_EmptyBlockStatement_PrintsBracesWithSpace()
    {
        Assert.Equal("{ }", Block().ToString());
    }

    [Fact]
    public void ToString_BlockStatement_SeparatesStatementsWithSpaces()
    {
        BlockStatement node = Block(
            new LetStatement(Tok(TokenType.Let, "let"), Ident("x"), Int(5)),
            ExprStmt(Ident("x")));

        Assert.Equal("{ let x := 5; x; }", node.ToString());
    }

    [Fact]
    public void ToString_WhileStatement_PrintsConditionAndBody()
    {
        WhileStatement node = new(
            Tok(TokenType.While, "while"),
            Infix(Ident("x"), TokenType.Gt, ">", Int(0)),
            Block(new AssignStatement(Tok(TokenType.Ident, "x"), Ident("x"), Infix(Ident("x"), TokenType.Minus, "-", Int(1)))));

        Assert.Equal("while ((x > 0)) { x := (x - 1); }", node.ToString());
    }

    [Fact]
    public void ToString_ForStatementWithAllClauses_PrintsClausesWithoutTerminators()
    {
        ForStatement node = new(
            Tok(TokenType.For, "for"),
            new LetStatement(Tok(TokenType.Let, "let"), Ident("i"), Int(0)),
            Infix(Ident("i"), TokenType.Lt, "<", Int(3)),
            new AssignStatement(Tok(TokenType.Ident, "i"), Ident("i"), Infix(Ident("i"), TokenType.Plus, "+", Int(1))),
            Block(new BreakStatement(Tok(TokenType.Break, "break"))));

        Assert.Equal("for (let i := 0; (i < 3); i := (i + 1)) { break; }", node.ToString());
    }

    [Fact]
    public void ToString_ForStatementWithExpressionClauses_PrintsClausesWithoutTerminators()
    {
        ForStatement node = new(
            Tok(TokenType.For, "for"),
            ExprStmt(Ident("a")),
            null,
            ExprStmt(new CallExpression(Tok(TokenType.LParen, "("), Ident("f"), [])),
            Block());

        Assert.Equal("for (a; ; f()) { }", node.ToString());
    }

    [Fact]
    public void ToString_ForStatementWithNoClauses_PrintsEmptyClauses()
    {
        ForStatement node = new(Tok(TokenType.For, "for"), null, null, null, Block());

        Assert.Equal("for (; ; ) { }", node.ToString());
    }

    [Fact]
    public void ToString_BreakAndContinue_PrintKeywordWithTerminator()
    {
        Assert.Equal("break;", new BreakStatement(Tok(TokenType.Break, "break")).ToString());
        Assert.Equal("continue;", new ContinueStatement(Tok(TokenType.Continue, "continue")).ToString());
    }

    // ------------------------------------------------------------------
    // Program
    // ------------------------------------------------------------------

    [Fact]
    public void ToString_Program_JoinsStatementsWithNewlines()
    {
        Program program = new(
        [
            new LetStatement(Tok(TokenType.Let, "let"), Ident("x"), Int(5)),
            ExprStmt(Infix(Ident("x"), TokenType.Plus, "+", Int(1))),
        ]);

        Assert.Equal("let x := 5;\n(x + 1);", program.ToString());
    }

    [Fact]
    public void ToString_EmptyProgram_PrintsEmptyString()
    {
        Assert.Equal(string.Empty, new Program([]).ToString());
    }
}
