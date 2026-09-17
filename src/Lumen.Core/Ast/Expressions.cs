using System.Text;
using Lumen.Core.Tokens;

namespace Lumen.Core.Ast;

// ToString() 一律輸出 canonical source：prefix / infix / logical / index 都加括號，
// 讓 precedence test 直接比對字串就能驗證樹的結構。

public sealed record Identifier(Token Token, string Name) : Node(Token), IExpression
{
    public override string ToString() => Name;
}

public sealed record IntegerLiteral(Token Token, long Value) : Node(Token), IExpression
{
    public override string ToString() => Token.Literal;
}

// 直接印原始文字而不是 Value：避開 culture 與 "R" 格式在 1.0 / 1E+16 之類邊界的差異。
public sealed record FloatLiteral(Token Token, double Value) : Node(Token), IExpression
{
    public override string ToString() => Token.Literal;
}

public sealed record StringLiteral(Token Token, string Value) : Node(Token), IExpression
{
    public override string ToString()
    {
        StringBuilder sb = new(Value.Length + 2);
        sb.Append('"');
        foreach (char c in Value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\0': sb.Append("\\0"); break;
                default: sb.Append(c); break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }
}

public sealed record CharLiteral(Token Token, char Value) : Node(Token), IExpression
{
    public override string ToString()
    {
        StringBuilder sb = new(3);
        sb.Append('\'');
        switch (Value)
        {
            case '\\': sb.Append("\\\\"); break;
            case '\'': sb.Append("\\'"); break;
            case '\n': sb.Append("\\n"); break;
            case '\t': sb.Append("\\t"); break;
            case '\0': sb.Append("\\0"); break;
            default: sb.Append(Value); break;
        }

        sb.Append('\'');
        return sb.ToString();
    }
}

public sealed record BooleanLiteral(Token Token, bool Value) : Node(Token), IExpression
{
    public override string ToString() => Value ? "true" : "false";
}

public sealed record NullLiteral(Token Token) : Node(Token), IExpression
{
    public override string ToString() => "null";
}

public sealed record PrefixExpression(Token Token, TokenType Operator, IExpression Right) : Node(Token), IExpression
{
    public override string ToString() => $"({Token.Literal}{Right})";
}

public sealed record InfixExpression(Token Token, IExpression Left, TokenType Operator, IExpression Right)
    : Node(Token), IExpression
{
    public override string ToString() => $"({Left} {Token.Literal} {Right})";
}

// && / || 獨立成一種 node：evaluator 要 short-circuit，不能跟 InfixExpression 走同一條路。
public sealed record LogicalExpression(Token Token, IExpression Left, TokenType Operator, IExpression Right)
    : Node(Token), IExpression
{
    public override string ToString() => $"({Left} {Token.Literal} {Right})";
}

public sealed record IfExpression(
    Token Token,
    IExpression Condition,
    BlockStatement Consequence,
    BlockStatement? Alternative) : Node(Token), IExpression
{
    public override string ToString() =>
        Alternative is null
            ? $"if ({Condition}) {Consequence}"
            : $"if ({Condition}) {Consequence} else {Alternative}";
}

// Name 只是 metadata（具名宣告 desugar 時填入，供之後 call stack 顯示），
// 不參與 ToString：宣告語法由 LetStatement 負責印，這裡永遠是匿名形式。
public sealed record FunctionLiteral(
    Token Token,
    IReadOnlyList<Identifier> Parameters,
    BlockStatement Body,
    string? Name) : Node(Token), IExpression
{
    internal string ParameterList => string.Join(", ", Parameters);

    public override string ToString() => $"fn({ParameterList}) {Body}";
}

public sealed record CallExpression(Token Token, IExpression Function, IReadOnlyList<IExpression> Arguments)
    : Node(Token), IExpression
{
    public override string ToString() => $"{Function}({string.Join(", ", Arguments)})";
}

public sealed record ArrayLiteral(Token Token, IReadOnlyList<IExpression> Elements) : Node(Token), IExpression
{
    public override string ToString() => $"[{string.Join(", ", Elements)}]";
}

// 用 list 而不是 dictionary：保住 insertion order，也讓之後能偵測重複 key。
public sealed record HashLiteral(Token Token, IReadOnlyList<KeyValuePair<IExpression, IExpression>> Pairs)
    : Node(Token), IExpression
{
    public override string ToString() =>
        $"{{{string.Join(", ", Pairs.Select(p => $"{p.Key}: {p.Value}"))}}}";
}

public sealed record IndexExpression(Token Token, IExpression Left, IExpression Index) : Node(Token), IExpression
{
    public override string ToString() => $"({Left}[{Index}])";
}
