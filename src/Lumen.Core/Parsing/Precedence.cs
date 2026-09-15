using Lumen.Core.Tokens;

namespace Lumen.Core.Parsing;

internal enum Precedence
{
    Lowest = 1,
    LogicalOr,      // ||
    LogicalAnd,     // &&
    Equality,       // == !=
    Comparison,     // < > <= >=
    Sum,            // + -
    Product,        // * / %
    Power,          // **
    Prefix,         // ! -
    Call,           // ()
    Index,          // []
}

internal static class PrecedenceTable
{
    private static readonly Dictionary<TokenType, Precedence> Table = new()
    {
        [TokenType.Or] = Precedence.LogicalOr,
        [TokenType.And] = Precedence.LogicalAnd,
        [TokenType.Eq] = Precedence.Equality,
        [TokenType.NotEq] = Precedence.Equality,
        [TokenType.Lt] = Precedence.Comparison,
        [TokenType.Gt] = Precedence.Comparison,
        [TokenType.LtEq] = Precedence.Comparison,
        [TokenType.GtEq] = Precedence.Comparison,
        [TokenType.Plus] = Precedence.Sum,
        [TokenType.Minus] = Precedence.Sum,
        [TokenType.Star] = Precedence.Product,
        [TokenType.Slash] = Precedence.Product,
        [TokenType.Percent] = Precedence.Product,
        [TokenType.StarStar] = Precedence.Power,
        [TokenType.LParen] = Precedence.Call,
        [TokenType.LBracket] = Precedence.Index,
    };

    public static Precedence Of(TokenType type) => Table.GetValueOrDefault(type, Precedence.Lowest);
}
