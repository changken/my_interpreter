using System.Globalization;

namespace Lumen.Core.Objects;

// 所有非正常控制流都是 Signal，沿求值結果往上傳，不用 C# exception。
// Evaluator 的傳播檢查一律寫 `is Signal`，不要寫具名型別。
internal abstract record Signal : LumenValue;

internal sealed record ReturnSignal(LumenValue Value) : Signal
{
    public override string TypeName => "Return";

    public override string Inspect() => "<return>";
}

internal sealed record BreakSignal : Signal
{
    public static readonly BreakSignal Instance = new();

    public override string TypeName => "Break";

    public override string Inspect() => "<break>";
}

internal sealed record ContinueSignal : Signal
{
    public static readonly ContinueSignal Instance = new();

    public override string TypeName => "Continue";

    public override string Inspect() => "<continue>";
}

internal sealed record ErrorSignal(LumenValue Payload, int Line, int Column) : Signal
{
    public override string TypeName => "Error";

    public override string Inspect() =>
        string.Create(CultureInfo.InvariantCulture, $"[line {Line}:{Column}] {Payload.Inspect()}");
}
