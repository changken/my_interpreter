using Lumen.Core.Lexing;
using Lumen.Core.Tokens;

while (true)
{
    Console.Write("lumen> ");
    string? line = Console.ReadLine();
    if (string.IsNullOrEmpty(line))
    {
        break;
    }

    foreach (Token token in new Lexer(line).Tokenize())
    {
        Console.WriteLine($"{token.Type,-12} {token.Literal,-15} line {token.Line}, col {token.Column}");
    }
}
