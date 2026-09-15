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
    private readonly BuiltinRegistry _builtins;

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

        return result;
    }

    // ------------------------------------------------------------------
    // Statements
    // ------------------------------------------------------------------

    private LumenValue Eval(IStatement statement, Environment env) => statement switch
    {
        ExpressionStatement s => Eval(s.Expression, env),
        BlockStatement s => EvalBlock(s, env),
        _ => throw new UnreachableException($"unsupported statement: {statement.GetType().Name}"),
    };

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
            _ => Error.NotAFunction(node.Token, callee),
        };
    }
}
