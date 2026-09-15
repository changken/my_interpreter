using Lumen.Core.Ast;
using Lumen.Core.Parsing;

namespace Lumen.Tests.Parsing;

public class ParserIntegrationTests
{
    // spec 範例程式；spec 裡示意用的頂層 break / continue 改放進 loop，否則依規則是 parse error。
    private const string SpecProgram = """
        // 單行註解

        let x := 5;
        let pi := 3.14;
        let name := "ken";
        x := x + 1;                        // 賦值

        fn add(a, b) { return a + b; }
        let double := fn(n) { n * 2 };     // 無 return 時，最後一個 expression 為回傳值

        if (x > 3) { "big" } else { "small" }

        while (x > 0) { x := x - 1; }
        for (let i := 0; i < 10; i := i + 1) { puts(i); }
        while (true) { if (x > 100) { break; } continue; }

        let arr := [1, 2, 3];
        let map := {"a": 1, "b": 2};
        arr[0];  map["a"];

        puts(len(arr));
        """;

    [Fact]
    public void ParseProgram_SpecSampleProgram_ParsesWithoutErrors()
    {
        Program program = ParserTestHelper.ParseValid(SpecProgram);

        Assert.Equal(15, program.Statements.Count);
    }

    [Theory]
    [InlineData(SpecProgram)]
    [InlineData("a;\n-b")]
    [InlineData("fn add(a, b) { return a + b; }")]
    [InlineData("let add := fn(a, b) { a + b };")]
    [InlineData("for (let i := 0; i < 3; i := i + 1) { for (; ; ) { break; } }")]
    [InlineData("for (; ; ) { }")]
    [InlineData("{\"k\": [1, {\"n\": fn() { null }}], \"s\": \"a\\\"b\\n\\t\\\\\\0\"}")]
    [InlineData("if (a && !b || c) { 1 } else { if (d) { 2 } }")]
    [InlineData("-a ** 2 ** -b + f(x)[0](y)")]
    [InlineData("return;")]
    public void ParseProgram_CanonicalOutput_RoundTripsToItself(string input)
    {
        Program first = ParserTestHelper.ParseValid(input);
        string canonical = first.ToString();

        (Program second, IReadOnlyList<ParseError> errors) = ParserTestHelper.Parse(canonical);

        Assert.True(errors.Count == 0, $"re-parse of canonical output failed:\n{canonical}\n{string.Join("\n", errors)}");
        Assert.Equal(canonical, second.ToString());
    }

    [Fact]
    public void ParseProgram_ReplStyleSeparateLines_EachParseIndependently()
    {
        Assert.Equal("let x := 1;", ParserTestHelper.ParseValid("let x := 1;").ToString());
        Assert.Equal("(x + 1);", ParserTestHelper.ParseValid("x + 1").ToString());
    }
}
