using System.Diagnostics;
using Lumen.Core.Ast;
using Lumen.Core.Objects;
using Lumen.Core.Tokens;
using Environment = Lumen.Core.Objects.Environment;

namespace Lumen.Core.Evaluation;

/// <summary>
/// Tree-walking evaluator。行為全部用外部 switch + type pattern 放在這裡，AST 保持純資料。
/// 每個子運算式求值後只做泛型的 <c>is Signal</c> 檢查往上傳，不寫具名的 ErrorSignal 檢查。
/// </summary>
public sealed class Evaluator
{
    // .NET 的 StackOverflowException 抓不到，所以呼叫深度手動計數。
    private const int MaxCallDepth = 1000;

    private readonly BuiltinRegistry _builtins;
    private int _callDepth;

    public Evaluator(BuiltinRegistry builtins)
    {
        _builtins = builtins;
    }

    public LumenValue Eval(Program program, Environment env)
    {
        LumenValue result = NullValue.Instance;
        foreach (IStatement statement in program.Statements)
        {
            result = Eval(statement, env);
            if (result is Signal)
            {
                break;
            }
        }

        // 頂層的 return 直接拆成值；break / continue 到不了這裡（parser 已擋）。
        return result is ReturnSignal r ? r.Value : result;
    }

    // ------------------------------------------------------------------
    // Statements
    // ------------------------------------------------------------------

    private LumenValue Eval(IStatement statement, Environment env) => statement switch
    {
        ExpressionStatement s => Eval(s.Expression, env),
        BlockStatement s => EvalBlock(s, env),
        LetStatement s => EvalLet(s, env),
        AssignStatement s => EvalAssign(s, env),
        ReturnStatement s => EvalReturn(s, env),
        BreakStatement => BreakSignal.Instance,
        ContinueStatement => ContinueSignal.Instance,
        WhileStatement s => EvalWhile(s, env),
        ForStatement s => EvalFor(s, env),
        _ => throw new UnreachableException($"unsupported statement: {statement.GetType().Name}"),
    };

    private LumenValue EvalLet(LetStatement node, Environment env)
    {
        LumenValue value = Eval(node.Value, env);
        if (value is Signal)
        {
            return value;
        }

        env.Define(node.Name.Name, value);
        return NullValue.Instance;
    }

    private LumenValue EvalAssign(AssignStatement node, Environment env)
    {
        LumenValue value = Eval(node.Value, env);
        if (value is Signal)
        {
            return value;
        }

        return env.TryAssign(node.Name.Name, value)
            ? NullValue.Instance
            : Error.UndefinedVariable(node.Name.Token, node.Name.Name);
    }

    private LumenValue EvalReturn(ReturnStatement node, Environment env)
    {
        if (node.Value is null)
        {
            return new ReturnSignal(NullValue.Instance);
        }

        LumenValue value = Eval(node.Value, env);
        return value is Signal ? value : new ReturnSignal(value);
    }

    // loop 只攔 Break / Continue；Return / Error 原樣放行。
    private LumenValue EvalWhile(WhileStatement node, Environment env)
    {
        while (true)
        {
            LumenValue condition = Eval(node.Condition, env);
            if (condition is Signal)
            {
                return condition;
            }

            if (condition is not BoolValue b)
            {
                return Error.ConditionNotBool(node.Token, condition);
            }

            if (!b.Value)
            {
                return NullValue.Instance;
            }

            LumenValue result = EvalBlock(node.Body, env);
            if (result is BreakSignal)
            {
                return NullValue.Instance;
            }

            if (result is Signal and not ContinueSignal)
            {
                return result;
            }
        }
    }

    // 每輪 iteration 用新的 Environment 跑 body（closure 捕捉到的是那一輪的 binding），
    // 結束後把 loop 變數的值寫回 loopEnv，讓 condition / update 看得到 body 裡的修改。
    private LumenValue EvalFor(ForStatement node, Environment env)
    {
        Environment loopEnv = new(env);

        if (node.Init is not null)
        {
            LumenValue init = Eval(node.Init, loopEnv);
            if (init is Signal)
            {
                return init;
            }
        }

        while (true)
        {
            if (node.Condition is not null)
            {
                LumenValue condition = Eval(node.Condition, loopEnv);
                if (condition is Signal)
                {
                    return condition;
                }

                if (condition is not BoolValue b)
                {
                    return Error.ConditionNotBool(node.Token, condition);
                }

                if (!b.Value)
                {
                    return NullValue.Instance;
                }
            }

            Environment iterEnv = new(loopEnv);
            foreach ((string name, LumenValue value) in loopEnv.Bindings)
            {
                iterEnv.Define(name, value);
            }

            LumenValue result = EvalBlock(node.Body, iterEnv);

            Dictionary<string, LumenValue> iterBindings = iterEnv.Bindings.ToDictionary(b => b.Key, b => b.Value);
            foreach ((string name, _) in loopEnv.Bindings)
            {
                loopEnv.Define(name, iterBindings[name]);
            }

            if (result is BreakSignal)
            {
                return NullValue.Instance;
            }

            if (result is Signal and not ContinueSignal)
            {
                return result;
            }

            if (node.Update is not null)
            {
                LumenValue update = Eval(node.Update, loopEnv);
                if (update is Signal)
                {
                    return update;
                }
            }
        }
    }

    // block 本身不開新 scope；最後一句的值就是 block 的值，任何 Signal 立刻中斷。
    private LumenValue EvalBlock(BlockStatement block, Environment env)
    {
        LumenValue result = NullValue.Instance;
        foreach (IStatement statement in block.Statements)
        {
            result = Eval(statement, env);
            if (result is Signal)
            {
                return result;
            }
        }

        return result;
    }

    // ------------------------------------------------------------------
    // Expressions
    // ------------------------------------------------------------------

    private LumenValue Eval(IExpression expression, Environment env) => expression switch
    {
        IntegerLiteral n => new IntValue(n.Value),
        FloatLiteral n => new FloatValue(n.Value),
        StringLiteral n => new StringValue(n.Value),
        BooleanLiteral n => BoolValue.Of(n.Value),
        NullLiteral => NullValue.Instance,
        Identifier n => EvalIdentifier(n, env),
        PrefixExpression n => EvalPrefix(n, env),
        InfixExpression n => EvalInfix(n, env),
        LogicalExpression n => EvalLogical(n, env),
        IfExpression n => EvalIf(n, env),
        ArrayLiteral n => EvalArray(n, env),
        HashLiteral n => EvalHash(n, env),
        CallExpression n => EvalCall(n, env),
        FunctionLiteral n => new FunctionValue(n, env),
        _ => throw new UnreachableException($"unsupported expression: {expression.GetType().Name}"),
    };

    private LumenValue EvalIdentifier(Identifier node, Environment env)
    {
        if (env.TryGet(node.Name, out LumenValue value))
        {
            return value;
        }

        if (_builtins.TryGet(node.Name, out BuiltinValue builtin))
        {
            return builtin;
        }

        return Error.UndefinedVariable(node.Token, node.Name);
    }

    private LumenValue EvalPrefix(PrefixExpression node, Environment env)
    {
        LumenValue operand = Eval(node.Right, env);
        if (operand is Signal)
        {
            return operand;
        }

        return node.Operator switch
        {
            TokenType.Bang => operand is BoolValue b ? BoolValue.Of(!b.Value) : Error.UnknownPrefix(node.Token, operand),
            TokenType.Minus => Numeric.Negate(node.Token, operand),
            _ => Error.UnknownPrefix(node.Token, operand),
        };
    }

    private LumenValue EvalInfix(InfixExpression node, Environment env)
    {
        LumenValue left = Eval(node.Left, env);
        if (left is Signal)
        {
            return left;
        }

        LumenValue right = Eval(node.Right, env);
        if (right is Signal)
        {
            return right;
        }

        if (Numeric.IsNumeric(left) && Numeric.IsNumeric(right))
        {
            return Numeric.Infix(node.Token, left, right);
        }

        // 非數值的 == / != 用 value equality；型別不同就是 false，不報錯。
        if (node.Operator is TokenType.Eq or TokenType.NotEq)
        {
            bool equal = left.Equals(right);
            return BoolValue.Of(node.Operator == TokenType.Eq ? equal : !equal);
        }

        if (node.Operator == TokenType.Plus && left is StringValue a && right is StringValue b)
        {
            return new StringValue(a.Value + b.Value);
        }

        return Error.TypeMismatch(node.Token, left, right);
    }

    // short-circuit：左邊決定結果時右邊完全不求值。
    private LumenValue EvalLogical(LogicalExpression node, Environment env)
    {
        LumenValue left = Eval(node.Left, env);
        if (left is Signal)
        {
            return left;
        }

        if (left is not BoolValue leftBool)
        {
            return Error.LogicalOperandNotBool(node.Token, left);
        }

        bool shortCircuit = node.Operator == TokenType.And ? !leftBool.Value : leftBool.Value;
        if (shortCircuit)
        {
            return leftBool;
        }

        LumenValue right = Eval(node.Right, env);
        if (right is Signal)
        {
            return right;
        }

        return right is BoolValue ? right : Error.LogicalOperandNotBool(node.Token, right);
    }

    private LumenValue EvalIf(IfExpression node, Environment env)
    {
        LumenValue condition = Eval(node.Condition, env);
        if (condition is Signal)
        {
            return condition;
        }

        if (condition is not BoolValue b)
        {
            return Error.ConditionNotBool(node.Token, condition);
        }

        if (b.Value)
        {
            return EvalBlock(node.Consequence, env);
        }

        return node.Alternative is null ? NullValue.Instance : EvalBlock(node.Alternative, env);
    }

    private LumenValue EvalArray(ArrayLiteral node, Environment env)
    {
        List<LumenValue> elements = new(node.Elements.Count);
        foreach (IExpression element in node.Elements)
        {
            LumenValue value = Eval(element, env);
            if (value is Signal)
            {
                return value;
            }

            elements.Add(value);
        }

        return new ArrayValue(elements);
    }

    private LumenValue EvalHash(HashLiteral node, Environment env)
    {
        HashValue hash = new();
        foreach ((IExpression keyExpr, IExpression valueExpr) in node.Pairs)
        {
            LumenValue key = Eval(keyExpr, env);
            if (key is Signal)
            {
                return key;
            }

            if (!HashValue.IsValidKey(key))
            {
                return Error.UnusableHashKey(((Node)keyExpr).Token, key);
            }

            LumenValue value = Eval(valueExpr, env);
            if (value is Signal)
            {
                return value;
            }

            hash = hash.With(key, value);
        }

        return hash;
    }

    private LumenValue EvalCall(CallExpression node, Environment env)
    {
        LumenValue callee = Eval(node.Function, env);
        if (callee is Signal)
        {
            return callee;
        }

        List<LumenValue> arguments = new(node.Arguments.Count);
        foreach (IExpression argument in node.Arguments)
        {
            LumenValue value = Eval(argument, env);
            if (value is Signal)
            {
                return value;
            }

            arguments.Add(value);
        }

        return callee switch
        {
            BuiltinValue builtin => builtin.Fn(arguments),
            FunctionValue function => CallFunction(node, function, arguments),
            _ => Error.NotAFunction(node.Token, callee),
        };
    }

    private LumenValue CallFunction(CallExpression node, FunctionValue function, List<LumenValue> arguments)
    {
        IReadOnlyList<Identifier> parameters = function.Declaration.Parameters;
        if (arguments.Count != parameters.Count)
        {
            return Error.At(node.Token, $"wrong number of arguments: expected {parameters.Count}, got {arguments.Count}");
        }

        if (_callDepth >= MaxCallDepth)
        {
            return Error.At(node.Token, "call stack exceeded");
        }

        _callDepth++;
        try
        {
            Environment callEnv = new(function.Closure);
            for (int i = 0; i < parameters.Count; i++)
            {
                callEnv.Define(parameters[i].Name, arguments[i]);
            }

            LumenValue result = EvalBlock(function.Declaration.Body, callEnv);

            // function call 只攔 Return；Error 原樣放行。
            return result is ReturnSignal r ? r.Value : result;
        }
        finally
        {
            _callDepth--;
        }
    }
}
