namespace Lumen.Tests.Parsing;

public class ParserPrecedenceTests
{
    [Theory]
    // 同層左結合
    [InlineData("a + b + c", "((a + b) + c)")]
    [InlineData("a + b - c", "((a + b) - c)")]
    [InlineData("a * b * c", "((a * b) * c)")]
    [InlineData("a * b / c", "((a * b) / c)")]
    [InlineData("a % b + c", "((a % b) + c)")]
    // 乘除高於加減
    [InlineData("a + b * c", "(a + (b * c))")]
    [InlineData("a + b / c", "(a + (b / c))")]
    [InlineData("a + b * c + d / e - f", "(((a + (b * c)) + (d / e)) - f)")]
    // 比較與相等
    [InlineData("5 > 4 == 3 < 4", "((5 > 4) == (3 < 4))")]
    [InlineData("5 < 4 != 3 > 4", "((5 < 4) != (3 > 4))")]
    [InlineData("a <= b >= c", "((a <= b) >= c)")]
    [InlineData("3 + 4 * 5 == 3 * 1 + 4 * 5", "((3 + (4 * 5)) == ((3 * 1) + (4 * 5)))")]
    // 邏輯運算：&& 高於 ||，兩者都低於比較
    [InlineData("a || b && c", "(a || (b && c))")]
    [InlineData("a && b || c", "((a && b) || c)")]
    [InlineData("a == b && c != d", "((a == b) && (c != d))")]
    [InlineData("!a && b", "((!a) && b)")]
    // ** 右結合，高於乘除，但低於 prefix
    [InlineData("2 ** 3 ** 2", "(2 ** (3 ** 2))")]
    [InlineData("a * b ** c", "(a * (b ** c))")]
    [InlineData("-a ** 2", "((-a) ** 2)")]
    // prefix
    [InlineData("-a * b", "((-a) * b)")]
    [InlineData("!-a", "(!(-a))")]
    [InlineData("!a == b", "((!a) == b)")]
    // 括號
    [InlineData("(a + b) * c", "((a + b) * c)")]
    [InlineData("a + (b + c)", "(a + (b + c))")]
    [InlineData("-(a + b)", "(-(a + b))")]
    [InlineData("!(a == b)", "(!(a == b))")]
    // call / index 最高
    [InlineData("f(a)(b)", "f(a)(b)")]
    [InlineData("arr[1][2]", "((arr[1])[2])")]
    [InlineData("a * [1, 2][0]", "(a * ([1, 2][0]))")]
    [InlineData("f(a + b, c * d)", "f((a + b), (c * d))")]
    [InlineData("a + f(b) * c", "(a + (f(b) * c))")]
    [InlineData("-f(a)", "(-f(a))")]
    [InlineData("f(a)[0] + 1", "((f(a)[0]) + 1)")]
    public void ParseProgram_OperatorPrecedence_ProducesExpectedTree(string input, string expected)
    {
        Assert.Equal(expected, ParserTestHelper.ParseExpression(input).ToString());
    }
}
