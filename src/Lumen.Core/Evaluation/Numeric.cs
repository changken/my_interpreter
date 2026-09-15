using Lumen.Core.Objects;
using Lumen.Core.Tokens;

namespace Lumen.Core.Evaluation;

// 數值運算只有兩條路：Int op Int 走 checked long；其餘任一邊是 Float 就 promote 成 double。
// 不要為 (型別, 型別, operator) 的組合加個別 case。
internal static class Numeric
{
    public static bool IsNumeric(LumenValue value) => value is IntValue or FloatValue;

    public static LumenValue Infix(Token op, LumenValue left, LumenValue right) => (left, right) switch
    {
        (IntValue a, IntValue b) => IntInfix(op, a.Value, b.Value),
        _ when IsNumeric(left) && IsNumeric(right) => FloatInfix(op, ToDouble(left), ToDouble(right)),
        _ => Error.TypeMismatch(op, left, right),
    };

    public static LumenValue Negate(Token op, LumenValue operand) => operand switch
    {
        IntValue { Value: long.MinValue } => Error.IntegerOverflow(op),
        IntValue i => new IntValue(-i.Value),
        FloatValue f => new FloatValue(-f.Value),
        _ => Error.UnknownPrefix(op, operand),
    };

    private static double ToDouble(LumenValue value) => value switch
    {
        IntValue i => i.Value,
        FloatValue f => f.Value,
        _ => throw new ArgumentException($"not numeric: {value.TypeName}", nameof(value)),
    };

    private static LumenValue IntInfix(Token op, long a, long b)
    {
        try
        {
            return op.Type switch
            {
                TokenType.Plus => new IntValue(checked(a + b)),
                TokenType.Minus => new IntValue(checked(a - b)),
                TokenType.Star => new IntValue(checked(a * b)),
                TokenType.Slash => b == 0 ? Error.DivisionByZero(op) : new IntValue(checked(a / b)),
                // x % -1 永遠是 0；先擋掉，避免 long.MinValue % -1 在 .NET 丟 OverflowException。
                TokenType.Percent => b switch
                {
                    0 => Error.DivisionByZero(op),
                    -1 => new IntValue(0),
                    _ => new IntValue(a % b),
                },
                TokenType.StarStar => IntPower(op, a, b),
                TokenType.Lt => BoolValue.Of(a < b),
                TokenType.Gt => BoolValue.Of(a > b),
                TokenType.LtEq => BoolValue.Of(a <= b),
                TokenType.GtEq => BoolValue.Of(a >= b),
                TokenType.Eq => BoolValue.Of(a == b),
                TokenType.NotEq => BoolValue.Of(a != b),
                _ => Error.TypeMismatch(op, new IntValue(a), new IntValue(b)),
            };
        }
        catch (OverflowException)
        {
            return Error.IntegerOverflow(op);
        }
    }

    // 指數 >= 0 用平方求冪保持 Int（每一步 checked）；負指數退到 Float 路徑。
    private static LumenValue IntPower(Token op, long @base, long exponent)
    {
        if (exponent < 0)
        {
            return FloatInfix(op, @base, exponent);
        }

        long result = 1;
        long factor = @base;
        checked
        {
            while (exponent > 0)
            {
                if ((exponent & 1) == 1)
                {
                    result *= factor;
                }

                exponent >>= 1;
                if (exponent > 0)
                {
                    factor *= factor;
                }
            }
        }

        return new IntValue(result);
    }

    private static LumenValue FloatInfix(Token op, double a, double b)
    {
        switch (op.Type)
        {
            case TokenType.Lt: return BoolValue.Of(a < b);
            case TokenType.Gt: return BoolValue.Of(a > b);
            case TokenType.LtEq: return BoolValue.Of(a <= b);
            case TokenType.GtEq: return BoolValue.Of(a >= b);
            case TokenType.Eq: return BoolValue.Of(a == b);
            case TokenType.NotEq: return BoolValue.Of(a != b);
            case TokenType.Percent: return Error.FloatModulo(op);
            case TokenType.Slash when b == 0: return Error.DivisionByZero(op);
        }

        double result = op.Type switch
        {
            TokenType.Plus => a + b,
            TokenType.Minus => a - b,
            TokenType.Star => a * b,
            TokenType.Slash => a / b,
            TokenType.StarStar => Math.Pow(a, b),
            _ => double.NaN,
        };

        // v0 不暴露 NaN / Infinity：任何非有限結果都是錯誤。
        return double.IsFinite(result) ? new FloatValue(result) : Error.NotFinite(op);
    }
}
