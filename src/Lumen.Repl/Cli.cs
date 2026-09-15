using Lumen.Core.Evaluation;
using Lumen.Core.Lexing;
using Lumen.Core.Objects;
using Lumen.Core.Parsing;
using AstProgram = Lumen.Core.Ast.Program;
using LumenEnv = Lumen.Core.Objects.Environment;

namespace Lumen.Repl;

/// <summary>非互動模式：<c>lumen script.lumen</c> 與 <c>lumen -e "expr"</c>。REPL 模式由 Program.cs 直接建 <see cref="Repl"/>。</summary>
public static class Cli
{
    public const int ExitOk = 0;
    public const int ExitUsage = 64;
    public const int ExitParseError = 65;
    public const int ExitNoInput = 66;
    public const int ExitRuntimeError = 70;
    public const int ExitInterrupted = 130;

    private const string Usage = "usage: lumen [<script.lumen> | -e <source>]";

    public static int Run(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        switch (args)
        {
            case ["-e", string inline]:
                return Execute(inline, printResult: true, output, error, cancellationToken);

            case [string path] when !path.StartsWith('-'):
                string source;
                try
                {
                    source = File.ReadAllText(path);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    error.WriteLine($"cannot read file: {path}");
                    return ExitNoInput;
                }

                return Execute(source, printResult: false, output, error, cancellationToken);

            default:
                error.WriteLine(Usage);
                return ExitUsage;
        }
    }

    private static int Execute(string source, bool printResult, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        Parser parser = new(new Lexer(source).Tokenize());
        AstProgram program = parser.ParseProgram();
        if (parser.Errors.Count > 0)
        {
            foreach (ParseError parseError in parser.Errors)
            {
                error.WriteLine(parseError);
            }

            return ExitParseError;
        }

        LumenValue result;
        try
        {
            result = new Evaluator(Builtins.CreateDefault(output), cancellationToken).Eval(program, new LumenEnv());
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("interrupted");
            return ExitInterrupted;
        }

        if (result is ErrorSignal)
        {
            error.WriteLine(result.Inspect());
            return ExitRuntimeError;
        }

        if (printResult && result is not NullValue)
        {
            output.WriteLine(result.Inspect());
        }

        return ExitOk;
    }
}
