using System.Text;
using Lumen.Core.Ast;
using Lumen.Core.Evaluation;
using Lumen.Core.Lexing;
using Lumen.Core.Objects;
using Lumen.Core.Parsing;
using Lumen.Core.Tokens;
using AstProgram = Lumen.Core.Ast.Program;
using LumenEnv = Lumen.Core.Objects.Environment;

namespace Lumen.Repl;

/// <summary>互動式 REPL。Environment 由這裡持有，跨行保留；輸入輸出可注入，方便測試。</summary>
public sealed class Repl
{
    private const string Prompt = "lumen> ";
    private const string ContinuationPrompt = "... ";

    private readonly TextReader _input;
    private readonly TextWriter _output;
    private readonly Evaluator _evaluator;
    private LumenEnv _env = new();

    public Repl(TextReader input, TextWriter output)
    {
        _input = input;
        _output = output;
        _evaluator = new Evaluator(Builtins.CreateDefault(output));
    }

    public void Run()
    {
        StringBuilder buffer = new();

        while (true)
        {
            _output.Write(buffer.Length == 0 ? Prompt : ContinuationPrompt);
            string? line = _input.ReadLine();
            if (line is null)
            {
                return;
            }

            if (buffer.Length == 0)
            {
                string command = line.Trim();
                if (command.Length == 0)
                {
                    continue;
                }

                if (command == ".exit")
                {
                    return;
                }

                if (command == ".env")
                {
                    PrintEnvironment();
                    continue;
                }

                if (command == ".clear")
                {
                    _env = new LumenEnv();
                    continue;
                }
            }

            buffer.AppendLine(line);
            string source = buffer.ToString();
            if (HasUnclosedBrackets(source))
            {
                continue;
            }

            buffer.Clear();
            Execute(source.TrimEnd());
        }
    }

    private void Execute(string source)
    {
        Parser parser = new(new Lexer(source).Tokenize());
        AstProgram program = parser.ParseProgram();
        if (parser.Errors.Count > 0)
        {
            foreach (ParseError error in parser.Errors)
            {
                _output.WriteLine(error);
            }

            return;
        }

        LumenValue result = _evaluator.Eval(program, _env);
        if (result is ErrorSignal)
        {
            _output.WriteLine(result.Inspect());
            return;
        }

        // 只有 expression statement 的結果自動印出，且 null 不印（避免每個 puts 後面多一行）。
        if (program.Statements.Count > 0 && program.Statements[^1] is ExpressionStatement && result is not NullValue)
        {
            _output.WriteLine(result.Inspect());
        }
    }

    private void PrintEnvironment()
    {
        foreach ((string name, LumenValue value) in _env.Bindings)
        {
            _output.WriteLine($"{name} = {value.Inspect()}");
        }
    }

    // 用 token 而不是字元數括號，字串裡的括號才不會誤判。
    private static bool HasUnclosedBrackets(string source)
    {
        int depth = 0;
        foreach (Token token in new Lexer(source).Tokenize())
        {
            depth += token.Type switch
            {
                TokenType.LParen or TokenType.LBracket or TokenType.LBrace => 1,
                TokenType.RParen or TokenType.RBracket or TokenType.RBrace => -1,
                _ => 0,
            };
        }

        return depth > 0;
    }
}
