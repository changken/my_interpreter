namespace Lumen.Core.Tokens;

public readonly record struct Token(TokenType Type, string Literal, int Line, int Column);
