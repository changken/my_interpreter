using Lumen.Core.Tokens;

namespace Lumen.Core.Ast;

// 每個 statement 的 ToString() 都自帶結尾（`;` 或 `}`），所以 Program / Block 只要用空白或換行接起來，
// 相鄰 statement 之間永遠有分隔符，重新 parse 時不會黏成另一個運算式。
//
// for 的 init / update 子句不能帶 `;`，所以能出現在子句裡的三種 statement 各自提供
// RenderWithoutTerminator()，由 ForStatement 呼叫；禁止用字串 trim 去掉分號。

public sealed record LetStatement(Token Token, Identifier Name, IExpression Value) : Node(Token), IStatement
{
    internal string RenderWithoutTerminator() => $"let {Name} := {Value}";

    public override string ToString() =>
        Value is FunctionLiteral fn && fn.Name == Name.Name
            ? $"fn {Name}({fn.ParameterList}) {fn.Body}"
            : RenderWithoutTerminator() + ";";
}

public sealed record AssignStatement(Token Token, Identifier Name, IExpression Value) : Node(Token), IStatement
{
    internal string RenderWithoutTerminator() => $"{Name} := {Value}";

    public override string ToString() => RenderWithoutTerminator() + ";";
}

public sealed record ReturnStatement(Token Token, IExpression? Value) : Node(Token), IStatement
{
    public override string ToString() => Value is null ? "return;" : $"return {Value};";
}

public sealed record ExpressionStatement(Token Token, IExpression Expression) : Node(Token), IStatement
{
    internal string RenderWithoutTerminator() => Expression.ToString()!;

    public override string ToString() => RenderWithoutTerminator() + ";";
}

public sealed record BlockStatement(Token Token, IReadOnlyList<IStatement> Statements) : Node(Token), IStatement
{
    public override string ToString() =>
        Statements.Count == 0 ? "{ }" : $"{{ {string.Join(" ", Statements)} }}";
}

public sealed record WhileStatement(Token Token, IExpression Condition, BlockStatement Body) : Node(Token), IStatement
{
    public override string ToString() => $"while ({Condition}) {Body}";
}

public sealed record ForStatement(
    Token Token,
    IStatement? Init,
    IExpression? Condition,
    IStatement? Update,
    BlockStatement Body) : Node(Token), IStatement
{
    public override string ToString() =>
        $"for ({RenderClause(Init)}; {Condition?.ToString()}; {RenderClause(Update)}) {Body}";

    private static string RenderClause(IStatement? clause) => clause switch
    {
        null => string.Empty,
        LetStatement let => let.RenderWithoutTerminator(),
        AssignStatement assign => assign.RenderWithoutTerminator(),
        ExpressionStatement expr => expr.RenderWithoutTerminator(),
        _ => throw new InvalidOperationException($"for clause cannot be {clause.GetType().Name}"),
    };
}

public sealed record BreakStatement(Token Token) : Node(Token), IStatement
{
    public override string ToString() => "break;";
}

public sealed record ContinueStatement(Token Token) : Node(Token), IStatement
{
    public override string ToString() => "continue;";
}
