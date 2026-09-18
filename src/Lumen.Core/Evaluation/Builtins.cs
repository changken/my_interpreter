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

        registry.Register("upper", args => args switch
        {
            [StringValue s] => new StringValue(s.Value.ToUpperInvariant()),
            [LumenValue other] => Error.FromBuiltin($"argument to upper must be String, got {other.TypeName}"),
            _ => Error.WrongArity(1, args.Count),
        });

        registry.Register("lower", args => args switch
        {
            [StringValue s] => new StringValue(s.Value.ToLowerInvariant()),
            [LumenValue other] => Error.FromBuiltin($"argument to lower must be String, got {other.TypeName}"),
            _ => Error.WrongArity(1, args.Count),
        });

        registry.Register("trim", args => args switch
        {
            [StringValue s] => new StringValue(s.Value.Trim()),
            [LumenValue other] => Error.FromBuiltin($"argument to trim must be String, got {other.TypeName}"),
            _ => Error.WrongArity(1, args.Count),
        });

        registry.Register("split", args => args switch
        {
            [StringValue, StringValue { Value.Length: 0 }] => Error.FromBuiltin("second argument to split must not be empty"),
            [StringValue s, StringValue sep] => new ArrayValue([.. s.Value.Split(sep.Value).Select(part => (LumenValue)new StringValue(part))]),
            [StringValue, LumenValue other] => Error.FromBuiltin($"second argument to split must be String, got {other.TypeName}"),
            [LumenValue other, _] => Error.FromBuiltin($"first argument to split must be String, got {other.TypeName}"),
            _ => Error.WrongArity(2, args.Count),
        });

        registry.Register("join", args => args switch
        {
            [ArrayValue a, StringValue sep] => JoinArray(a, sep),
            [ArrayValue, LumenValue other] => Error.FromBuiltin($"second argument to join must be String, got {other.TypeName}"),
            [LumenValue other, _] => Error.FromBuiltin($"first argument to join must be Array, got {other.TypeName}"),
            _ => Error.WrongArity(2, args.Count),
        });

        registry.Register("contains", args => args switch
        {
            [StringValue s, StringValue needle] => BoolValue.Of(s.Value.Contains(needle.Value, StringComparison.Ordinal)),
            [StringValue, LumenValue other] => Error.FromBuiltin($"second argument to contains must be String, got {other.TypeName}"),
            [ArrayValue a, LumenValue item] => BoolValue.Of(a.Elements.Any(item.Equals)),
            [LumenValue other, _] => Error.FromBuiltin($"first argument to contains must be String or Array, got {other.TypeName}"),
            _ => Error.WrongArity(2, args.Count),
        });

        registry.Register("reverse", args => args switch
        {
            [StringValue s] => new StringValue(new string([.. s.Value.Reverse()])),
            [ArrayValue a] => new ArrayValue([.. a.Elements.Reverse()]),
            [LumenValue other] => Error.FromBuiltin($"argument to reverse must be String or Array, got {other.TypeName}"),
            _ => Error.WrongArity(1, args.Count),
        });

        registry.Register("last", args => args switch
        {
            [ArrayValue a] => a.Elements.Count == 0 ? NullValue.Instance : a.Elements[^1],
            [StringValue s] => s.Value.Length == 0 ? NullValue.Instance : new CharValue(s.Value[^1]),
            [LumenValue other] => Error.FromBuiltin($"argument to last must be String or Array, got {other.TypeName}"),
            _ => Error.WrongArity(1, args.Count),
        });

        return registry;
    }

    // join 的元素檢查需要在中途回報「哪個元素」出錯，list-pattern switch 表達不了，拆成獨立方法。
    private static LumenValue JoinArray(ArrayValue array, StringValue separator)
    {
        string[] parts = new string[array.Elements.Count];
        for (int i = 0; i < array.Elements.Count; i++)
        {
            if (array.Elements[i] is not StringValue element)
            {
                return Error.FromBuiltin($"argument to join: array element must be String, got {array.Elements[i].TypeName}");
            }

            parts[i] = element.Value;
        }

        return new StringValue(string.Join(separator.Value, parts));
    }
}
