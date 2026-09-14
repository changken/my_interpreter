using System.Globalization;
using System.Text;

namespace Lumen.Core.Lexing;

public sealed class Lexer
{
    private static readonly Dictionary<string, TokenType> Keywords = new()
    {
        ["let"] = TokenType.Let,
        ["fn"] = TokenType.Fn,
        ["return"] = TokenType.Return,
        ["if"] = TokenType.If,
        ["else"] = TokenType.Else,
        ["while"] = TokenType.While,
        ["for"] = TokenType.For,
        ["break"] = TokenType.Break,
        ["continue"] = TokenType.Continue,
        ["true"] = TokenType.True,
        ["false"] = TokenType.False,
        ["null"] = TokenType.Null,
    };

    private readonly string _input;
    private int _position;
    private int _line = 1;
    private int _column = 1;

    public Lexer(string input)
    {
        _input = input;
    }

    public IEnumerable<Token> Tokenize()
    {
        Token token;
        do
        {
            token = NextToken();
            yield return token;
        } while (token.Type != TokenType.Eof);
    }

    private Token NextToken()
    {
        SkipTrivia();

        int startLine = _line;
        int startColumn = _column;

        if (IsAtEnd())
        {
            return new Token(TokenType.Eof, string.Empty, startLine, startColumn);
        }

        char c = Peek();

        if (IsIdentifierStart(c))
        {
            return ScanIdentifierOrKeyword(startLine, startColumn);
        }

        if (IsAsciiDigit(c))
        {
            return ScanNumber(startLine, startColumn);
        }

        if (c == '"')
        {
            return ScanString(startLine, startColumn);
        }

        return ScanOperatorOrDelimiter(startLine, startColumn);
    }

    private void SkipTrivia()
    {
        while (true)
        {
            while (!IsAtEnd() && IsWhitespace(Peek()))
            {
                Advance();
            }

            if (!IsAtEnd() && Peek() == '/' && PeekNext() == '/')
            {
                Advance();
                Advance();
                while (!IsAtEnd() && Peek() != '\n')
                {
                    Advance();
                }

                continue;
            }

            break;
        }
    }

    private Token ScanIdentifierOrKeyword(int startLine, int startColumn)
    {
        int start = _position;
        while (!IsAtEnd() && IsIdentifierPart(Peek()))
        {
            Advance();
        }

        string text = _input[start.._position];
        TokenType type = Keywords.GetValueOrDefault(text, TokenType.Ident);
        return new Token(type, text, startLine, startColumn);
    }

    private Token ScanNumber(int startLine, int startColumn)
    {
        int start = _position;
        bool isFloat = false;

        while (!IsAtEnd() && IsAsciiDigit(Peek()))
        {
            Advance();
        }

        if (!IsAtEnd() && Peek() == '.' && IsAsciiDigit(PeekNext()))
        {
            isFloat = true;
            Advance();
            while (!IsAtEnd() && IsAsciiDigit(Peek()))
            {
                Advance();
            }
        }
        else if (!IsAtEnd() && Peek() == '.')
        {
            // Trailing dot with no fractional digits (e.g. "1."): fold into one
            // malformed-literal Illegal token instead of leaving a dangling '.'.
            Advance();
            return IllegalFromMalformedNumberTail(start, startLine, startColumn);
        }

        if (!IsAtEnd() && (IsIdentifierStart(Peek()) || IsAsciiDigit(Peek())))
        {
            // Suffix / underscore-separator / accidental hex-prefix, e.g. "10L", "1_000", "0x1F".
            return IllegalFromMalformedNumberTail(start, startLine, startColumn);
        }

        string text = _input[start.._position];

        try
        {
            if (isFloat)
            {
                double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                return new Token(TokenType.Float, text, startLine, startColumn);
            }

            long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return new Token(TokenType.Int, text, startLine, startColumn);
        }
        catch (OverflowException)
        {
            return new Token(TokenType.Illegal, text, startLine, startColumn);
        }
    }

    private Token IllegalFromMalformedNumberTail(int start, int startLine, int startColumn)
    {
        while (!IsAtEnd() && IsIdentifierPart(Peek()))
        {
            Advance();
        }

        string text = _input[start.._position];
        return new Token(TokenType.Illegal, text, startLine, startColumn);
    }

    private Token ScanLeadingDotNumber(int startLine, int startColumn)
    {
        int start = _position;
        Advance(); // consume '.'

        if (!IsAtEnd() && IsAsciiDigit(Peek()))
        {
            while (!IsAtEnd() && IsAsciiDigit(Peek()))
            {
                Advance();
            }
        }

        string text = _input[start.._position];
        return new Token(TokenType.Illegal, text, startLine, startColumn);
    }

    private Token ScanString(int startLine, int startColumn)
    {
        Advance(); // consume opening quote
        StringBuilder builder = new();

        while (true)
        {
            if (IsAtEnd() || Peek() == '\n')
            {
                return new Token(TokenType.Illegal, builder.ToString(), startLine, startColumn);
            }

            char c = Advance();
            if (c == '"')
            {
                return new Token(TokenType.String, builder.ToString(), startLine, startColumn);
            }

            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }

            if (IsAtEnd() || Peek() == '\n')
            {
                return new Token(TokenType.Illegal, builder.ToString(), startLine, startColumn);
            }

            char escaped = Advance();
            char? decoded = escaped switch
            {
                'n' => '\n',
                't' => '\t',
                '\\' => '\\',
                '"' => '"',
                '0' => '\0',
                _ => null,
            };

            if (decoded is null)
            {
                return new Token(TokenType.Illegal, builder.ToString(), startLine, startColumn);
            }

            builder.Append(decoded.Value);
        }
    }

    private Token ScanOperatorOrDelimiter(int startLine, int startColumn)
    {
        if (Peek() == '.')
        {
            return ScanLeadingDotNumber(startLine, startColumn);
        }

        int start = _position;
        char c = Advance();

        TokenType type = c switch
        {
            ':' => Match('=') ? TokenType.Assign : TokenType.Colon,
            '=' => Match('=') ? TokenType.Eq : TokenType.Illegal,
            '!' => Match('=') ? TokenType.NotEq : TokenType.Bang,
            '<' => Match('=') ? TokenType.LtEq : TokenType.Lt,
            '>' => Match('=') ? TokenType.GtEq : TokenType.Gt,
            '&' => Match('&') ? TokenType.And : TokenType.Illegal,
            '|' => Match('|') ? TokenType.Or : TokenType.Illegal,
            '*' => Match('*') ? TokenType.StarStar : TokenType.Star,
            '/' => TokenType.Slash,
            '+' => TokenType.Plus,
            '-' => TokenType.Minus,
            '%' => TokenType.Percent,
            ',' => TokenType.Comma,
            ';' => TokenType.Semicolon,
            '(' => TokenType.LParen,
            ')' => TokenType.RParen,
            '{' => TokenType.LBrace,
            '}' => TokenType.RBrace,
            '[' => TokenType.LBracket,
            ']' => TokenType.RBracket,
            _ => TokenType.Illegal,
        };

        string literal = _input[start.._position];
        return new Token(type, literal, startLine, startColumn);
    }

    private bool IsAtEnd() => _position >= _input.Length;

    private char Peek() => _input[_position];

    private char PeekNext() => _position + 1 < _input.Length ? _input[_position + 1] : '\0';

    private char Advance()
    {
        char c = _input[_position];
        _position++;
        if (c == '\n')
        {
            _line++;
            _column = 1;
        }
        else
        {
            _column++;
        }

        return c;
    }

    private bool Match(char expected)
    {
        if (IsAtEnd() || _input[_position] != expected)
        {
            return false;
        }

        Advance();
        return true;
    }

    private static bool IsWhitespace(char c) => c is ' ' or '\t' or '\r' or '\n';

    private static bool IsAsciiDigit(char c) => c is >= '0' and <= '9';

    private static bool IsIdentifierStart(char c) =>
        c == '_' || (c is >= 'a' and <= 'z') || (c is >= 'A' and <= 'Z');

    private static bool IsIdentifierPart(char c) => IsIdentifierStart(c) || IsAsciiDigit(c);
}
