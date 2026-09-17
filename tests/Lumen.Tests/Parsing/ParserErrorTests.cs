using Lumen.Core.Ast;
using Lumen.Core.Parsing;

namespace Lumen.Tests.Parsing;

public class ParserErrorTests
{
    [Fact]
    public void ParseError_ToString_UsesLineColumnPrefix()
    {
        ParseError error = new("boom", 3, 12);

        Assert.Equal("[line 3:12] boom", error.ToString());
    }

    [Theory]
    [InlineData("+ 5", "unexpected token '+'")]
    [InlineData("(1 + 2", "expected ')' but found end of input")]
    [InlineData("[1, 2", "expected ']' but found end of input")]
    [InlineData("{\"a\": 1", "expected '}' but found end of input")]
    [InlineData("{\"a\" 1}", "expected ':' but found '1'")]
    [InlineData("f(1, 2", "expected ')' but found end of input")]
    [InlineData("arr[1", "expected ']' but found end of input")]
    [InlineData("if x { 1 }", "expected '(' but found 'x'")]
    [InlineData("if (x) 1", "expected '{' but found '1'")]
    [InlineData("fn(a b) { }", "expected ')' but found 'b'")]
    [InlineData("fn(1) { }", "expected identifier but found '1'")]
    [InlineData("[1, 2,]", "unexpected token ']'")]
    [InlineData("1 +", "unexpected token end of input")]
    [InlineData("@", "illegal token '@'")]
    [InlineData("\"unterminated", "illegal token")]
    public void ParseProgram_MalformedExpression_RecordsDescriptiveError(string input, string expectedFragment)
    {
        IReadOnlyList<ParseError> errors = ParserTestHelper.ParseErrors(input);

        Assert.Contains(expectedFragment, errors[0].Message);
    }

    [Fact]
    public void ParseProgram_ErrorOnThirdLine_ReportsLineAndColumn()
    {
        IReadOnlyList<ParseError> errors = ParserTestHelper.ParseErrors("1\n2\n3 + @");

        Assert.StartsWith("[line 3:5] ", errors[0].ToString());
    }

    [Fact]
    public void ParseProgram_DeeplyNestedParentheses_RecordsSingleNestingErrorWithoutCrashing()
    {
        string input = new string('(', 250) + "1" + new string(')', 250);

        IReadOnlyList<ParseError> errors = ParserTestHelper.ParseErrors(input);

        Assert.Contains(errors, e => e.Message.Contains("nesting too deep"));
        Assert.Single(errors, e => e.Message.Contains("nesting too deep"));
    }

    [Fact]
    public void ParseProgram_DeeplyNestedArrays_RecordsNestingErrorWithoutCrashing()
    {
        string input = new string('[', 250) + new string(']', 250);

        IReadOnlyList<ParseError> errors = ParserTestHelper.ParseErrors(input);

        Assert.Contains(errors, e => e.Message.Contains("nesting too deep"));
    }

    [Fact]
    public void ParseProgram_NestingWithinLimit_ParsesSuccessfully()
    {
        string input = new string('(', 100) + "1" + new string(')', 100);

        Assert.Equal("1;", ParserTestHelper.ParseValid(input).ToString());
    }

    [Fact]
    public void ParseProgram_PureGarbage_TerminatesWithErrors()
    {
        IReadOnlyList<ParseError> errors = ParserTestHelper.ParseErrors("@@@@@ ### $$$ )))) }}}} ]]]]");

        Assert.NotEmpty(errors);
    }

    [Fact]
    public void ParseProgram_MoreThanHundredErrors_StopsAtCap()
    {
        string input = string.Join(" ", Enumerable.Repeat("@;", 150));

        IReadOnlyList<ParseError> errors = ParserTestHelper.ParseErrors(input);

        Assert.Equal(100, errors.Count);
    }

    [Fact]
    public void ParseProgram_GarbageWithoutSeparators_SynchronizesToSingleError()
    {
        string input = string.Join(" ", Enumerable.Repeat("@", 150));

        IReadOnlyList<ParseError> errors = ParserTestHelper.ParseErrors(input);

        Assert.Single(errors);
    }

    [Theory]
    [InlineData("let x := 5", "expected ';' but found end of input")]
    [InlineData("let := 5;", "expected identifier but found ':='")]
    [InlineData("let x 5;", "expected ':=' but found '5'")]
    [InlineData("let x := ;", "unexpected token ';'")]
    [InlineData("x := 5", "expected ';' but found end of input")]
    [InlineData("return 5", "expected ';' but found end of input")]
    [InlineData("while (x) { break }", "expected ';' but found '}'")]
    [InlineData("break;", "'break' outside of loop")]
    [InlineData("continue;", "'continue' outside of loop")]
    [InlineData("while (true) { fn() { break; } }", "'break' outside of loop")]
    [InlineData("if (x) { continue; }", "'continue' outside of loop")]
    [InlineData("arr[0] := 1;", "invalid assignment target")]
    [InlineData("a + b := 1;", "invalid assignment target")]
    [InlineData("arr[0] += 1;", "invalid assignment target")]
    [InlineData("a + b -= 1;", "invalid assignment target")]
    [InlineData("1 *= 2;", "invalid assignment target")]
    [InlineData("f() /= 2;", "invalid assignment target")]
    [InlineData("x += ;", "unexpected token ';'")]
    [InlineData("x += 1", "expected ';' but found end of input")]
    [InlineData("x %= 2;", "illegal token '='")]
    [InlineData("x **= 2;", "illegal token '='")]
    [InlineData("while (x) x := 1;", "expected '{' but found 'x'")]
    [InlineData("while x { }", "expected '(' but found 'x'")]
    [InlineData("for (let i := 0 i < 3; ) { }", "expected ';' but found 'i'")]
    [InlineData("for (let i := 0; i < 3) { }", "expected ';' but found ')'")]
    [InlineData("for (; ; let i := 0) { }", "unexpected token 'let'")]
    [InlineData("fn add(a, b) 1", "expected '{' but found '1'")]
    public void ParseProgram_MalformedStatement_RecordsDescriptiveError(string input, string expectedFragment)
    {
        IReadOnlyList<ParseError> errors = ParserTestHelper.ParseErrors(input);

        Assert.Contains(expectedFragment, errors[0].Message);
    }

    [Fact]
    public void ParseProgram_StatementErrorOnThirdLine_ReportsLine()
    {
        IReadOnlyList<ParseError> errors = ParserTestHelper.ParseErrors("let x := 1;\nlet y := 2;\nlet z := ;");

        Assert.StartsWith("[line 3:10] ", errors[0].ToString());
    }

    [Fact]
    public void ParseProgram_MultipleBrokenStatements_ReportsEachAndRecoversAtNextLet()
    {
        (Program program, IReadOnlyList<ParseError> errors) = ParserTestHelper.Parse("let := ;;; @@@ let x := 1;");

        Assert.True(errors.Count >= 3, $"expected several errors, got:\n{string.Join("\n", errors)}");
        IStatement recovered = Assert.Single(program.Statements);
        Assert.Equal("let x := 1;", recovered.ToString());
    }

    [Fact]
    public void ParseProgram_MissingTerminatorBeforeNextStatement_DoesNotSwallowNextStatement()
    {
        (Program program, IReadOnlyList<ParseError> errors) = ParserTestHelper.Parse("let x := 5 let y := 6;");

        Assert.Single(errors);
        Assert.Equal("let y := 6;", Assert.Single(program.Statements).ToString());
    }

    [Fact]
    public void ParseProgram_MalformedCompoundAssign_RecordsOneErrorAndRecoversAtNextStatement()
    {
        (Program program, IReadOnlyList<ParseError> errors) = ParserTestHelper.Parse("x += ; let y := 1;");

        Assert.Single(errors);
        Assert.Equal("let y := 1;", Assert.Single(program.Statements).ToString());
    }

    [Fact]
    public void ParseProgram_ErrorInsideBlock_RecoversWithinBlockAndClosesIt()
    {
        (Program program, IReadOnlyList<ParseError> errors) = ParserTestHelper.Parse("fn() { let x := ; 1 }");

        Assert.Single(errors);
        Assert.Equal("fn() { 1; };", Assert.Single(program.Statements).ToString());
    }

    [Fact]
    public void ParseProgram_DeeplyNestedBlocks_RecordsNestingErrorWithoutCrashing()
    {
        string input = string.Concat(Enumerable.Repeat("while (true) { ", 250)) + string.Concat(Enumerable.Repeat("}", 250));

        IReadOnlyList<ParseError> errors = ParserTestHelper.ParseErrors(input);

        Assert.Contains(errors, e => e.Message.Contains("nesting too deep"));
    }

    [Fact]
    public void ParseProgram_ErrorInFirstStatement_StillParsesLaterStatements()
    {
        (Program program, IReadOnlyList<ParseError> errors) = ParserTestHelper.Parse("(1 + ; 2 + 3");

        Assert.NotEmpty(errors);
        IStatement recovered = Assert.Single(program.Statements);
        Assert.Equal("(2 + 3);", recovered.ToString());
    }
}
