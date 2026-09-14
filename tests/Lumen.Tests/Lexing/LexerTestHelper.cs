using Lumen.Core.Lexing;

namespace Lumen.Tests.Lexing;

internal static class LexerTestHelper
{
    public static List<Token> Tokenize(string input) => new Lexer(input).Tokenize().ToList();
}
