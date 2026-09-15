using Lumen.Core.Ast;
using Lumen.Core.Lexing;
using Lumen.Core.Parsing;

namespace Lumen.Tests.Parsing;

internal static class ParserTestHelper
{
    public static (Program Program, IReadOnlyList<ParseError> Errors) Parse(string input)
    {
        Parser parser = new(new Lexer(input).Tokenize());
        Program program = parser.ParseProgram();
        return (program, parser.Errors);
    }

    /// <summary>解析並斷言零錯誤；失敗時把所有錯誤訊息列出來，比 Assert.Empty 的預設輸出好讀。</summary>
    public static Program ParseValid(string input)
    {
        (Program program, IReadOnlyList<ParseError> errors) = Parse(input);
        Assert.True(errors.Count == 0, $"unexpected parse errors:\n{string.Join("\n", errors)}");
        return program;
    }

    public static IReadOnlyList<ParseError> ParseErrors(string input)
    {
        IReadOnlyList<ParseError> errors = Parse(input).Errors;
        Assert.NotEmpty(errors);
        return errors;
    }

    /// <summary>輸入必須剛好是一個 expression statement，回傳其中的運算式。</summary>
    public static IExpression ParseExpression(string input)
    {
        Program program = ParseValid(input);
        IStatement statement = Assert.Single(program.Statements);
        return Assert.IsType<ExpressionStatement>(statement).Expression;
    }
}
