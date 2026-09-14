namespace Lumen.Core.Tokens;

public enum TokenType
{
    Illegal,
    Eof,

    // Identifiers & literals
    Ident,
    Int,
    Float,
    String,

    // Keywords
    Let,
    Fn,
    Return,
    If,
    Else,
    While,
    For,
    Break,
    Continue,
    True,
    False,
    Null,

    // Operators
    Assign,     // :=
    Plus,       // +
    Minus,      // -
    Star,       // *
    Slash,      // /
    Percent,    // %
    StarStar,   // **
    Bang,       // !
    Eq,         // ==
    NotEq,      // !=
    Lt,         // <
    Gt,         // >
    LtEq,       // <=
    GtEq,       // >=
    And,        // &&
    Or,         // ||

    // Delimiters
    Comma,      // ,
    Semicolon,  // ;
    Colon,      // :
    LParen,     // (
    RParen,     // )
    LBrace,     // {
    RBrace,     // }
    LBracket,   // [
    RBracket,   // ]
}
