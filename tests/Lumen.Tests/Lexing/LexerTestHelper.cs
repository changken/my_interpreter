using Lumen.Core.Lexing;
using Lumen.Core.Tokens;

namespace Lumen.Tests.Lexing;

internal static class LexerTestHelper
{
    public static List<Token> Tokenize(string input) => new Lexer(input).Tokenize().ToList();

    /// <summary>
    /// 對「輸入只產生一個有意義 token」這種最常見的測試案例，一行斷言 Type/Literal/Line/Column。
    /// 靠 Token 是 record struct 的內建 value equality，失敗時 xUnit 會印出兩個完整 record 的差異。
    /// </summary>
    public static void AssertToken(string input, int index, Token expected) =>
        Assert.Equal(expected, Tokenize(input)[index]);
}
