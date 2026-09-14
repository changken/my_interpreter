using System.Globalization;
using System.Text;
using Lumen.Core.Tokens;

namespace Lumen.Core.Lexing;

/// <summary>手刻 char-by-char scanner，把 Lumen 原始碼字串轉成 <see cref="Token"/> 序列。</summary>
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

    // ------------------------------------------------------------------
    // 對外 API
    // ------------------------------------------------------------------

    /// <summary>依序吐出整段輸入的 token，最後一定以恰好一個 <see cref="TokenType.Eof"/> 結尾。</summary>
    public IEnumerable<Token> Tokenize()
    {
        Token token;
        do
        {
            token = NextToken();
            yield return token;
        } while (token.Type != TokenType.Eof);
    }

    // ------------------------------------------------------------------
    // token 分派
    // ------------------------------------------------------------------

    private Token NextToken()
    {
        SkipTrivia();

        // 起始位置要在消費任何字元之前記下來，讓多字元 token 回報第一個字元的 Line/Column。
        int line = _line;
        int column = _column;

        char? current = Peek();
        if (current is null)
        {
            return new Token(TokenType.Eof, string.Empty, line, column);
        }

        char c = current.Value;

        if (IsIdentifierStart(c))
        {
            return ScanIdentifierOrKeyword(line, column);
        }

        if (IsAsciiDigit(c) || c == '.')
        {
            return ScanNumberOrDot(line, column);
        }

        if (c == '"')
        {
            return ScanString(line, column);
        }

        return ScanOperatorOrDelimiter(line, column);
    }

    // 空白跟註解可能交錯（例如空白行接註解行），用 loop 反覆跳到兩者都不成立才停。
    private void SkipTrivia()
    {
        while (true)
        {
            while (IsWhitespace(Peek()))
            {
                Advance();
            }

            // 單一 '/' 是除法，兩個 '/' 才是註解——這是唯一需要多看一格才能判斷的情況。
            if (Peek() == '/' && PeekNext() == '/')
            {
                Advance();
                Advance();

                // 吃到行尾但不吃掉 '\n' 本身，讓上面的空白迴圈下一輪處理換行、順便把行號加一。
                while (Peek() is not (null or '\n'))
                {
                    Advance();
                }

                continue;
            }

            break;
        }
    }

    // ------------------------------------------------------------------
    // 各類 token 掃描
    // ------------------------------------------------------------------

    private Token ScanIdentifierOrKeyword(int line, int column)
    {
        int start = _position;
        while (IsIdentifierPart(Peek()))
        {
            Advance();
        }

        string text = _input[start.._position];
        TokenType type = Keywords.GetValueOrDefault(text, TokenType.Ident);
        return TokenFrom(start, type, line, column);
    }

    // 數字/小數點相關的規則全部集中在這一個方法：合法 Int/Float、開頭是 '.' 的 ".5"、
    // 結尾懸空的 "1."、suffix（"10L"）、底線分隔（"1_000"）都在這裡判斷，不要分散到別處。
    private Token ScanNumberOrDot(int line, int column)
    {
        int start = _position;

        if (Peek() == '.')
        {
            // 開頭就是 '.'：Lumen 沒有前導小數點也沒有成員存取，不管後面接不接數字都是 Illegal，
            // 只是把後面的數字一起吃掉比較方便閱讀錯誤內容。
            Advance();
            while (IsAsciiDigit(Peek()))
            {
                Advance();
            }

            return IllegalToken(start, line, column);
        }

        while (IsAsciiDigit(Peek()))
        {
            Advance();
        }

        bool isFloat = false;

        if (Peek() == '.')
        {
            Advance();

            if (!IsAsciiDigit(Peek()))
            {
                // 小數點後面沒有數字，例如 "1."：不合法，且不留下懸空的 '.' 給下一輪誤判。
                ConsumeMalformedNumberSuffix();
                return IllegalToken(start, line, column);
            }

            isFloat = true;
            while (IsAsciiDigit(Peek()))
            {
                Advance();
            }
        }

        if (IsIdentifierPart(Peek()))
        {
            // 數字後面緊接字母/底線/數字：suffix、底線分隔、誤寫的 hex 前綴都落在這裡，
            // 統一當成單一 Illegal token 回報。
            ConsumeMalformedNumberSuffix();
            return IllegalToken(start, line, column);
        }

        return ParseNumberLiteral(start, isFloat, line, column);
    }

    private void ConsumeMalformedNumberSuffix()
    {
        while (IsIdentifierPart(Peek()))
        {
            Advance();
        }
    }

    // 呼叫 Parse 純粹是借用 BCL 的 overflow 檢查，一律走 InvariantCulture；
    // 數值本身不存進 Token，Ch3 Parser 建 AST 時會用同樣規則再 parse 一次。
    private Token ParseNumberLiteral(int start, bool isFloat, int line, int column)
    {
        string text = _input[start.._position];

        try
        {
            if (isFloat)
            {
                double.Parse(text, NumberStyles.Float, CultureInfo.InvariantCulture);
                return new Token(TokenType.Float, text, line, column);
            }

            long.Parse(text, NumberStyles.Integer, CultureInfo.InvariantCulture);
            return new Token(TokenType.Int, text, line, column);
        }
        catch (OverflowException)
        {
            return new Token(TokenType.Illegal, text, line, column);
        }
    }

    private Token ScanString(int line, int column)
    {
        Advance(); // 吃掉開頭的 '"'
        StringBuilder decoded = new();

        while (true)
        {
            // 未閉合：EOF 或換行（含 CRLF 的 '\r'）都視為 lexer error，字串不支援跨行。
            if (Peek() is null or '\r' or '\n')
            {
                return new Token(TokenType.Illegal, decoded.ToString(), line, column);
            }

            char c = Advance();
            if (c == '"')
            {
                return new Token(TokenType.String, decoded.ToString(), line, column);
            }

            if (c == '\\')
            {
                if (!TryScanEscape(decoded))
                {
                    return new Token(TokenType.Illegal, decoded.ToString(), line, column);
                }

                continue;
            }

            decoded.Append(c);
        }
    }

    // 未知 escape、或 '\' 後面直接是換行/EOF，都不得靜默放行：回傳 false 讓 ScanString 中止整段掃描。
    private bool TryScanEscape(StringBuilder decoded)
    {
        if (Peek() is null or '\r' or '\n')
        {
            return false;
        }

        char escaped = Advance();
        char? mapped = escaped switch
        {
            'n' => '\n',
            't' => '\t',
            '\\' => '\\',
            '"' => '"',
            '0' => '\0',
            _ => null,
        };

        if (mapped is null)
        {
            return false;
        }

        decoded.Append(mapped.Value);
        return true;
    }

    private Token ScanOperatorOrDelimiter(int line, int column)
    {
        int start = _position;
        char c = Advance();

        // 每個可能開頭雙字元 token 的字元都用 Match() 往前多看一格：
        // 看到就一起吃掉組成雙字元 token，看不到就退回單字元 token（或 Illegal）。
        TokenType type = c switch
        {
            ':' => Match('=') ? TokenType.Assign : TokenType.Colon,
            '=' => Match('=') ? TokenType.Eq : TokenType.Illegal,       // Lumen 沒有單一 '='
            '!' => Match('=') ? TokenType.NotEq : TokenType.Bang,
            '<' => Match('=') ? TokenType.LtEq : TokenType.Lt,
            '>' => Match('=') ? TokenType.GtEq : TokenType.Gt,
            '&' => Match('&') ? TokenType.And : TokenType.Illegal,      // 沒有位元運算，落單的 '&' 不合法
            '|' => Match('|') ? TokenType.Or : TokenType.Illegal,       // 同上
            '*' => Match('*') ? TokenType.StarStar : TokenType.Star,
            '/' => TokenType.Slash, // `//` 已經在 SkipTrivia() 被當成註解處理掉了，走到這裡一定是除法
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

        return TokenFrom(start, type, line, column);
    }

    private Token TokenFrom(int start, TokenType type, int line, int column) =>
        new(type, _input[start.._position], line, column);

    private Token IllegalToken(int start, int line, int column) =>
        TokenFrom(start, TokenType.Illegal, line, column);

    // ------------------------------------------------------------------
    // 游標與字元判斷
    // ------------------------------------------------------------------

    // 看目前字元但不消費；到達結尾回傳 null。用 char? 而不是 '\0' 之類的哨兵字元，
    // 是因為字串裡的 \0 escape 會解碼成真正的 NUL 字元——用哨兵字元會讓兩者無法區分，
    // 用 null 就沒有這個問題，所有呼叫端都可以直接用 `Peek() is null` 判斷是否結束。
    private char? Peek() => _position < _input.Length ? _input[_position] : null;

    private char? PeekNext() => _position + 1 < _input.Length ? _input[_position + 1] : null;

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

    /// <summary>若下一個字元等於 expected 就消費並回傳 true；否則什麼都不做回傳 false。</summary>
    private bool Match(char expected)
    {
        if (Peek() != expected)
        {
            return false;
        }

        Advance();
        return true;
    }

    // 全部限定 ASCII，不支援 Unicode 識別字。
    private static bool IsWhitespace(char? c) => c is ' ' or '\t' or '\r' or '\n';

    private static bool IsAsciiDigit(char? c) => c is >= '0' and <= '9';

    private static bool IsIdentifierStart(char? c) =>
        c is '_' or (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    private static bool IsIdentifierPart(char? c) => IsIdentifierStart(c) || IsAsciiDigit(c);
}
