using Lumen.Core.Lexing;
using Lumen.Core.Parsing;
using AstProgram = Lumen.Core.Ast.Program;

while (true)
{
    Console.Write("lumen> ");
    string? line = Console.ReadLine();
    if (string.IsNullOrEmpty(line))
    {
        break;
    }

    Parser parser = new(new Lexer(line).Tokenize());
    AstProgram program = parser.ParseProgram();

    if (parser.Errors.Count > 0)
    {
        foreach (ParseError error in parser.Errors)
        {
            Console.WriteLine(error);
        }

        continue;
    }

    Console.WriteLine(program);
}
