using Lumen.Core.Objects;

namespace Lumen.Core.Evaluation;

public static class Builtins
{
    /// <summary>預設 builtin 集合；輸出目標由 caller 決定（REPL 用 Console，測試用 StringWriter）。</summary>
    public static BuiltinRegistry CreateDefault(TextWriter output)
    {
        BuiltinRegistry registry = new();

        registry.Register("puts", args =>
        {
            foreach (LumenValue arg in args)
            {
                output.WriteLine(arg.Inspect());
            }

            return NullValue.Instance;
        });

        registry.Register("len", args => args switch
        {
            [StringValue s] => new IntValue(s.Value.Length),   // UTF-16 code unit 數（已知簡化）
            [ArrayValue a] => new IntValue(a.Elements.Count),
            [LumenValue other] => Error.FromBuiltin($"argument to len must be String or Array, got {other.TypeName}"),
            _ => Error.WrongArity(1, args.Count),
        });

        registry.Register("first", args => args switch
        {
            [ArrayValue a] => a.Elements.Count == 0 ? NullValue.Instance : a.Elements[0],
            [LumenValue other] => Error.FromBuiltin($"argument to first must be Array, got {other.TypeName}"),
            _ => Error.WrongArity(1, args.Count),
        });

        registry.Register("rest", args => args switch
        {
            [ArrayValue a] => a.Elements.Count == 0 ? NullValue.Instance : new ArrayValue(a.Elements.Skip(1).ToList()),
            [LumenValue other] => Error.FromBuiltin($"argument to rest must be Array, got {other.TypeName}"),
            _ => Error.WrongArity(1, args.Count),
        });

        registry.Register("push", args => args switch
        {
            [ArrayValue a, LumenValue item] => new ArrayValue([.. a.Elements, item]),
            [LumenValue other, _] => Error.FromBuiltin($"first argument to push must be Array, got {other.TypeName}"),
            _ => Error.WrongArity(2, args.Count),
        });

        return registry;
    }
}
