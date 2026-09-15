using System.Globalization;
using Lumen.Core.Ast;
using Lumen.Core.Tokens;

namespace Lumen.Core.Parsing;

/// <summary>
/// Pratt parser：token 流 → <see cref="Program"/>。所有錯誤累積在 <see cref="Errors"/>，不丟 exception。
/// 任何回傳 null 的解析方法都代表「已經記過錯誤」，呼叫端只要往上傳 null，不要再記一次。
/// </summary>
public sealed class Parser
{
    private delegate IExpression? PrefixParseFn();

    private delegate IExpression? InfixParseFn(IExpression left);

    // .NET 的 StackOverflowException 抓不到，所以遞迴深度必須手動計數。
    private const int MaxNestingDepth = 200;
    private const int MaxErrors = 100;

    private readonly List<Token> _tokens;
    private readonly List<ParseError> _errors = [];
    private readonly Dictionary<TokenType, PrefixParseFn> _prefixFns;
    private readonly Dictionary<TokenType, InfixParseFn> _infixFns;

    private int _position;
    private int _nestingDepth;

    public Parser(IEnumerable<Token> tokens)
    {
        _tokens = tokens.ToList();
        EnsureTrailingEof();

        _prefixFns = new Dictionary<TokenType, PrefixParseFn>
        {
            [TokenType.Ident] = ParseIdentifier,
            [TokenType.Int] = ParseIntegerLiteral,
            [TokenType.Float] = ParseFloatLiteral,
            [TokenType.String] = ParseStringLiteral,
            [TokenType.True] = ParseBooleanLiteral,
            [TokenType.False] = ParseBooleanLiteral,
            [TokenType.Null] = ParseNullLiteral,
            [TokenType.Bang] = ParsePrefixExpression,
            [TokenType.Minus] = ParsePrefixExpression,
            [TokenType.LParen] = ParseGroupedExpression,
            [TokenType.If] = ParseIfExpression,
            [TokenType.Fn] = ParseAnonymousFunction,
            [TokenType.LBracket] = ParseArrayLiteral,
            [TokenType.LBrace] = ParseHashLiteral,
        };

        _infixFns = new Dictionary<TokenType, InfixParseFn>
        {
            [TokenType.Plus] = ParseInfixExpression,
            [TokenType.Minus] = ParseInfixExpression,
            [TokenType.Star] = ParseInfixExpression,
            [TokenType.Slash] = ParseInfixExpression,
            [TokenType.Percent] = ParseInfixExpression,
            [TokenType.Eq] = ParseInfixExpression,
            [TokenType.NotEq] = ParseInfixExpression,
            [TokenType.Lt] = ParseInfixExpression,
            [TokenType.Gt] = ParseInfixExpression,
            [TokenType.LtEq] = ParseInfixExpression,
            [TokenType.GtEq] = ParseInfixExpression,
            [TokenType.StarStar] = ParsePowerExpression,
            [TokenType.And] = ParseLogicalExpression,
            [TokenType.Or] = ParseLogicalExpression,
            [TokenType.LParen] = ParseCallExpression,
            [TokenType.LBracket] = ParseIndexExpression,
        };
    }

    public IReadOnlyList<ParseError> Errors => _errors;

    // ------------------------------------------------------------------
    // 對外 API
    // ------------------------------------------------------------------

    public Program ParseProgram() => new(ParseStatementsUntil(TokenType.Eof));

    // Parser 是公開 API，caller 不一定是 Lexer；缺 Eof 就補一個，座標接在最後一個 token 後面。
    private void EnsureTrailingEof()
    {
        if (_tokens.Count > 0 && _tokens[^1].Type == TokenType.Eof)
        {
            return;
        }

        Token eof = _tokens.Count == 0
            ? new Token(TokenType.Eof, string.Empty, 1, 1)
            : new Token(TokenType.Eof, string.Empty, _tokens[^1].Line, _tokens[^1].Column + _tokens[^1].Literal.Length);
        _tokens.Add(eof);
    }

    // ------------------------------------------------------------------
    // Statements
    // ------------------------------------------------------------------

    // Program 與 Block 共用的主迴圈。每一輪保證 _position 前進：statement 解析失敗就 Synchronize，
    // Synchronize 本身在沒進展時會強制吃掉一個 token；最後的 internal error 只是防呆，正常不該觸發。
    private List<IStatement> ParseStatementsUntil(TokenType terminator)
    {
        List<IStatement> statements = [];

        while (Current.Type != terminator && Current.Type != TokenType.Eof && _errors.Count < MaxErrors)
        {
            int start = _position;

            IStatement? statement = ParseStatement();
            if (statement is not null)
            {
                statements.Add(statement);
            }
            else
            {
                Synchronize(start);
            }

            if (_position == start)
            {
                AddError("internal error: parser made no progress", Current);
                Advance();
            }
        }

        return statements;
    }

    private IStatement? ParseStatement() => ParseExpressionStatement();

    private ExpressionStatement? ParseExpressionStatement()
    {
        Token start = Current;
        IExpression? expression = ParseExpression(Precedence.Lowest);
        if (expression is null)
        {
            return null;
        }

        // expression statement 的分號可省略。
        Match(TokenType.Semicolon);
        return new ExpressionStatement(start, expression);
    }

    private BlockStatement? ParseBlockStatement()
    {
        if (Current.Type != TokenType.LBrace)
        {
            AddError($"expected '{{' but found {Display(Current)}", Current);
            return null;
        }

        if (!EnterNesting())
        {
            return null;
        }

        try
        {
            Token lbrace = Advance();
            List<IStatement> statements = ParseStatementsUntil(TokenType.RBrace);
            return Expect(TokenType.RBrace) ? new BlockStatement(lbrace, statements) : null;
        }
        finally
        {
            _nestingDepth--;
        }
    }

    // panic-mode 同步：吃到分號之後，或停在下一個 statement 的起點 / block 結尾 / EOF。
    // `}` 不消費，留給 ParseBlockStatement 關 block，否則 recovery 會跨過 block 邊界。
    // 若失敗的 statement 完全沒消費 token，先強制吃掉一個，避免在同一個 token 上原地打轉。
    private void Synchronize(int statementStart)
    {
        if (_position == statementStart && Current.Type != TokenType.Eof)
        {
            Advance();
        }

        while (Current.Type != TokenType.Eof)
        {
            if (IsSynchronizationPoint(Current.Type))
            {
                return;
            }

            if (Advance().Type == TokenType.Semicolon)
            {
                return;
            }
        }
    }

    private static bool IsSynchronizationPoint(TokenType type) => type is
        TokenType.Let or TokenType.Fn or TokenType.If or TokenType.While or TokenType.For or TokenType.Return
        or TokenType.RBrace;

    // ------------------------------------------------------------------
    // Pratt 核心
    // ------------------------------------------------------------------

    private IExpression? ParseExpression(Precedence precedence)
    {
        if (!EnterNesting())
        {
            return null;
        }

        try
        {
            if (Current.Type == TokenType.Illegal)
            {
                AddError($"illegal token {Display(Current)}", Current);
                return null;
            }

            if (!_prefixFns.TryGetValue(Current.Type, out PrefixParseFn? prefix))
            {
                AddError($"unexpected token {Display(Current)}", Current);
                return null;
            }

            IExpression? left = prefix();

            while (left is not null && precedence < PrecedenceTable.Of(Current.Type))
            {
                if (!_infixFns.TryGetValue(Current.Type, out InfixParseFn? infix))
                {
                    return left;
                }

                left = infix(left);
            }

            return left;
        }
        finally
        {
            _nestingDepth--;
        }
    }

    // 超過上限只記一筆錯誤並回 false；上層看到 null 直接往上傳，不會重複記錄。
    private bool EnterNesting()
    {
        if (_nestingDepth >= MaxNestingDepth)
        {
            AddError("nesting too deep", Current);
            return false;
        }

        _nestingDepth++;
        return true;
    }

    // ------------------------------------------------------------------
    // Prefix
    // ------------------------------------------------------------------

    private IExpression ParseIdentifier()
    {
        Token token = Advance();
        return new Identifier(token, token.Literal);
    }

    private IExpression? ParseIntegerLiteral()
    {
        Token token = Advance();
        if (!long.TryParse(token.Literal, NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
        {
            AddError($"integer literal out of range: {token.Literal}", token);
            return null;
        }

        return new IntegerLiteral(token, value);
    }

    private IExpression? ParseFloatLiteral()
    {
        Token token = Advance();
        if (!double.TryParse(token.Literal, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
        {
            AddError($"invalid float literal: {token.Literal}", token);
            return null;
        }

        return new FloatLiteral(token, value);
    }

    private IExpression ParseStringLiteral()
    {
        Token token = Advance();
        return new StringLiteral(token, token.Literal);
    }

    private IExpression ParseBooleanLiteral()
    {
        Token token = Advance();
        return new BooleanLiteral(token, token.Type == TokenType.True);
    }

    private IExpression ParseNullLiteral() => new NullLiteral(Advance());

    private IExpression? ParsePrefixExpression()
    {
        Token op = Advance();
        IExpression? right = ParseExpression(Precedence.Prefix);
        return right is null ? null : new PrefixExpression(op, op.Type, right);
    }

    private IExpression? ParseGroupedExpression()
    {
        Advance();
        IExpression? inner = ParseExpression(Precedence.Lowest);
        if (inner is null)
        {
            return null;
        }

        return Expect(TokenType.RParen) ? inner : null;
    }

    private IExpression? ParseIfExpression()
    {
        Token token = Advance();
        if (!Expect(TokenType.LParen))
        {
            return null;
        }

        IExpression? condition = ParseExpression(Precedence.Lowest);
        if (condition is null || !Expect(TokenType.RParen))
        {
            return null;
        }

        BlockStatement? consequence = ParseBlockStatement();
        if (consequence is null)
        {
            return null;
        }

        BlockStatement? alternative = null;
        if (Match(TokenType.Else))
        {
            alternative = ParseBlockStatement();
            if (alternative is null)
            {
                return null;
            }
        }

        return new IfExpression(token, condition, consequence, alternative);
    }

    private IExpression? ParseAnonymousFunction() => ParseFunctionLiteral(name: null);

    // name 由呼叫端決定：expression 位置一律匿名；statement 位置的 `fn add(...)` 會傳入名稱。
    private FunctionLiteral? ParseFunctionLiteral(string? name)
    {
        Token token = Advance();
        if (!Expect(TokenType.LParen))
        {
            return null;
        }

        List<Identifier>? parameters = ParseParameterList();
        if (parameters is null)
        {
            return null;
        }

        BlockStatement? body = ParseBlockStatement();
        return body is null ? null : new FunctionLiteral(token, parameters, body, name);
    }

    private List<Identifier>? ParseParameterList()
    {
        List<Identifier> parameters = [];
        if (Match(TokenType.RParen))
        {
            return parameters;
        }

        while (true)
        {
            if (Current.Type != TokenType.Ident)
            {
                AddError($"expected identifier but found {Display(Current)}", Current);
                return null;
            }

            Token token = Advance();
            parameters.Add(new Identifier(token, token.Literal));

            if (!Match(TokenType.Comma))
            {
                break;
            }
        }

        return Expect(TokenType.RParen) ? parameters : null;
    }

    private IExpression? ParseArrayLiteral()
    {
        Token token = Advance();
        List<IExpression>? elements = ParseExpressionList(TokenType.RBracket);
        return elements is null ? null : new ArrayLiteral(token, elements);
    }

    private IExpression? ParseHashLiteral()
    {
        Token token = Advance();
        List<KeyValuePair<IExpression, IExpression>> pairs = [];

        if (Match(TokenType.RBrace))
        {
            return new HashLiteral(token, pairs);
        }

        while (true)
        {
            IExpression? key = ParseExpression(Precedence.Lowest);
            if (key is null || !Expect(TokenType.Colon))
            {
                return null;
            }

            IExpression? value = ParseExpression(Precedence.Lowest);
            if (value is null)
            {
                return null;
            }

            pairs.Add(new KeyValuePair<IExpression, IExpression>(key, value));

            if (!Match(TokenType.Comma))
            {
                break;
            }
        }

        return Expect(TokenType.RBrace) ? new HashLiteral(token, pairs) : null;
    }

    // 逗號分隔的運算式清單（call 引數 / array 元素），不接受尾隨逗號。
    private List<IExpression>? ParseExpressionList(TokenType end)
    {
        List<IExpression> items = [];
        if (Match(end))
        {
            return items;
        }

        while (true)
        {
            IExpression? item = ParseExpression(Precedence.Lowest);
            if (item is null)
            {
                return null;
            }

            items.Add(item);

            if (!Match(TokenType.Comma))
            {
                break;
            }
        }

        return Expect(end) ? items : null;
    }

    // ------------------------------------------------------------------
    // Infix
    // ------------------------------------------------------------------

    private IExpression? ParseInfixExpression(IExpression left)
    {
        Token op = Advance();
        IExpression? right = ParseExpression(PrecedenceTable.Of(op.Type));
        return right is null ? null : new InfixExpression(op, left, op.Type, right);
    }

    // ** 是右結合：右邊用「低一級」的 precedence 去解析，讓 2 ** 3 ** 2 變成 2 ** (3 ** 2)。
    private IExpression? ParsePowerExpression(IExpression left)
    {
        Token op = Advance();
        IExpression? right = ParseExpression(Precedence.Power - 1);
        return right is null ? null : new InfixExpression(op, left, op.Type, right);
    }

    private IExpression? ParseLogicalExpression(IExpression left)
    {
        Token op = Advance();
        IExpression? right = ParseExpression(PrecedenceTable.Of(op.Type));
        return right is null ? null : new LogicalExpression(op, left, op.Type, right);
    }

    private IExpression? ParseCallExpression(IExpression function)
    {
        Token token = Advance();
        List<IExpression>? arguments = ParseExpressionList(TokenType.RParen);
        return arguments is null ? null : new CallExpression(token, function, arguments);
    }

    private IExpression? ParseIndexExpression(IExpression left)
    {
        Token token = Advance();
        IExpression? index = ParseExpression(Precedence.Lowest);
        if (index is null)
        {
            return null;
        }

        return Expect(TokenType.RBracket) ? new IndexExpression(token, left, index) : null;
    }

    // ------------------------------------------------------------------
    // 游標與錯誤
    // ------------------------------------------------------------------

    private Token Current => _tokens[_position];

    // 永遠不會越過最後的 Eof：到了 Eof 之後再 Advance 仍停在原地。
    private Token Advance()
    {
        Token token = Current;
        if (_position < _tokens.Count - 1)
        {
            _position++;
        }

        return token;
    }

    private bool Match(TokenType type)
    {
        if (Current.Type != type)
        {
            return false;
        }

        Advance();
        return true;
    }

    private bool Expect(TokenType type)
    {
        if (Match(type))
        {
            return true;
        }

        AddError($"expected {Describe(type)} but found {Display(Current)}", Current);
        return false;
    }

    private void AddError(string message, Token at)
    {
        if (_errors.Count < MaxErrors)
        {
            _errors.Add(new ParseError(message, at.Line, at.Column));
        }
    }

    // 錯誤訊息裡「實際看到的 token」的統一顯示方式。
    private static string Display(Token token) => token.Type switch
    {
        TokenType.Eof => "end of input",
        TokenType.String => $"\"{token.Literal}\"",
        _ => $"'{token.Literal}'",
    };

    // 錯誤訊息裡「期望的 token 種類」的統一顯示方式。
    private static string Describe(TokenType type) => type switch
    {
        TokenType.Ident => "identifier",
        TokenType.Eof => "end of input",
        TokenType.Assign => "':='",
        TokenType.Comma => "','",
        TokenType.Semicolon => "';'",
        TokenType.Colon => "':'",
        TokenType.LParen => "'('",
        TokenType.RParen => "')'",
        TokenType.LBrace => "'{'",
        TokenType.RBrace => "'}'",
        TokenType.LBracket => "'['",
        TokenType.RBracket => "']'",
        _ => type.ToString().ToLowerInvariant(),
    };
}
