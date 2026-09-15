using Lumen.Core.Tokens;

namespace Lumen.Core.Ast;

public interface IExpression
{
}

public interface IStatement
{
}

/// <summary>所有 AST node 的基底；Token 是該 node 的「代表 token」，供錯誤訊息取得 line / column。</summary>
public abstract record Node(Token Token);

/// <summary>整個原始檔的根。沒有單一代表 token，所以不繼承 <see cref="Node"/>。</summary>
public sealed record Program(IReadOnlyList<IStatement> Statements)
{
    public override string ToString() => string.Join("\n", Statements);
}
