using Lumen.Core.Objects;
using Lumen.Core.Tokens;

namespace Lumen.Core.Evaluation;

// 所有 runtime 錯誤都從這裡產生，訊息文字集中管理；位置一律取自觸發錯誤的 token。
internal static class Error
{
    public static ErrorSignal At(Token token, string message) =>
        new(new StringValue(message), token.Line, token.Column);

    public static ErrorSignal TypeMismatch(Token op, LumenValue left, LumenValue right) =>
        At(op, $"type mismatch: {left.TypeName} {op.Literal} {right.TypeName}");

    public static ErrorSignal UnknownPrefix(Token op, LumenValue operand) =>
        At(op, $"unknown operator: {op.Literal}{operand.TypeName}");

    public static ErrorSignal UndefinedVariable(Token at, string name) =>
        At(at, $"undefined variable: {name}");

    public static ErrorSignal DivisionByZero(Token op) => At(op, "division by zero");

    public static ErrorSignal IntegerOverflow(Token op) => At(op, "integer overflow");

    public static ErrorSignal NotFinite(Token op) => At(op, "not a finite number");

    public static ErrorSignal FloatModulo(Token op) => At(op, "operator % not supported for Float");

    public static ErrorSignal ConditionNotBool(Token at, LumenValue condition) =>
        At(at, $"condition must be Bool, got {condition.TypeName}");

    public static ErrorSignal LogicalOperandNotBool(Token op, LumenValue operand) =>
        At(op, $"operator {op.Literal} requires Bool, got {operand.TypeName}");

    public static ErrorSignal UnusableHashKey(Token at, LumenValue key) =>
        At(at, $"unusable as hash key: {key.TypeName}");

    public static ErrorSignal NotAFunction(Token at, LumenValue callee) =>
        At(at, $"not a function: {callee.TypeName}");
}
