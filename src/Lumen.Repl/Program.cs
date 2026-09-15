using Lumen.Core.Evaluation;
using Lumen.Core.Lexing;
using Lumen.Core.Objects;
using Lumen.Core.Parsing;
using AstProgram = Lumen.Core.Ast.Program;
using LumenEnv = Lumen.Core.Objects.Environment;

Evaluator evaluator = new(Builtins.CreateDefault(Console.Out));
LumenEnv env = new();

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

    LumenValue result = evaluator.Eval(program, env);
    if (result is not NullValue)
    {
        Console.WriteLine(result.Inspect());
    }
}
