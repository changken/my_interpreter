using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using Lumen.Core.Ast;

namespace Lumen.Core.Objects;

public delegate LumenValue BuiltinFn(IReadOnlyList<LumenValue> args);

/// <summary>
/// 所有 runtime 值的基底。Inspect() 是唯一放在型別上的 operation（多型）；
/// 求值邏輯一律在 Evaluator 用外部 pattern matching，不要往這裡加。
/// </summary>
public abstract record LumenValue
{
    public abstract string TypeName { get; }

    public abstract string Inspect();

    // 放在 Array / Hash 裡面時的顯示方式：只有字串需要加引號，其餘與 Inspect 相同。
    internal virtual string InspectNested() => Inspect();
}

public sealed record IntValue(long Value) : LumenValue
{
    public override string TypeName => "Int";

    public override string Inspect() => Value.ToString(CultureInfo.InvariantCulture);
}

public sealed record FloatValue(double Value) : LumenValue
{
    public override string TypeName => "Float";

    // 一定要跟 Int 分得出來：純數字（可帶負號）的輸出補上 ".0"。
    public override string Inspect()
    {
        string text = Value.ToString("R", CultureInfo.InvariantCulture);
        return text.All(c => char.IsAsciiDigit(c) || c == '-') ? text + ".0" : text;
    }
}

public sealed record BoolValue(bool Value) : LumenValue
{
    public static readonly BoolValue True = new(true);
    public static readonly BoolValue False = new(false);

    public static BoolValue Of(bool value) => value ? True : False;

    public override string TypeName => "Bool";

    public override string Inspect() => Value ? "true" : "false";
}

public sealed record NullValue : LumenValue
{
    public static readonly NullValue Instance = new();

    private NullValue()
    {
    }

    public override string TypeName => "Null";

    public override string Inspect() => "null";
}

public sealed record StringValue(string Value) : LumenValue
{
    public override string TypeName => "String";

    public override string Inspect() => Value;

    internal override string InspectNested()
    {
        StringBuilder sb = new(Value.Length + 2);
        sb.Append('"');
        foreach (char c in Value)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append("\\n"); break;
                case '\t': sb.Append("\\t"); break;
                case '\0': sb.Append("\\0"); break;
                default: sb.Append(c); break;
            }
        }

        sb.Append('"');
        return sb.ToString();
    }
}

// record 對 IReadOnlyList 只會做 reference 比較，所以手動改成逐元素 structural 比較。
public sealed record ArrayValue(IReadOnlyList<LumenValue> Elements) : LumenValue
{
    public override string TypeName => "Array";

    public override string Inspect() => $"[{string.Join(", ", Elements.Select(e => e.InspectNested()))}]";

    public bool Equals(ArrayValue? other) => other is not null && Elements.SequenceEqual(other.Elements);

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (LumenValue element in Elements)
        {
            hash.Add(element);
        }

        return hash.ToHashCode();
    }
}

// 不可變、保 insertion order。key 只能是 Int / Float / Bool / String，且整數值的 Float 會正規化成 Int，
// 讓 hash 查找與 `1 == 1.0` 為 true 的比較語意一致。
public sealed record HashValue : LumenValue
{
    private readonly Dictionary<LumenValue, LumenValue> _map;
    private readonly List<LumenValue> _keys;

    public HashValue()
    {
        _map = [];
        _keys = [];
    }

    private HashValue(Dictionary<LumenValue, LumenValue> map, List<LumenValue> keys)
    {
        _map = map;
        _keys = keys;
    }

    public override string TypeName => "Hash";

    public int Count => _keys.Count;

    public IEnumerable<KeyValuePair<LumenValue, LumenValue>> Pairs =>
        _keys.Select(k => new KeyValuePair<LumenValue, LumenValue>(k, _map[k]));

    public static bool IsValidKey(LumenValue key) => key is IntValue or FloatValue or BoolValue or StringValue;

    public bool TryGet(LumenValue key, out LumenValue value) => _map.TryGetValue(NormalizeKey(key), out value!);

    public HashValue With(LumenValue key, LumenValue value)
    {
        LumenValue normalized = NormalizeKey(key);
        Dictionary<LumenValue, LumenValue> map = new(_map);
        List<LumenValue> keys = new(_keys);
        if (map.TryAdd(normalized, value))
        {
            keys.Add(normalized);
        }
        else
        {
            map[normalized] = value;
        }

        return new HashValue(map, keys);
    }

    public override string Inspect() =>
        $"{{{string.Join(", ", Pairs.Select(p => $"{p.Key.InspectNested()}: {p.Value.InspectNested()}"))}}}";

    public bool Equals(HashValue? other)
    {
        if (other is null || other.Count != Count)
        {
            return false;
        }

        foreach ((LumenValue key, LumenValue value) in _map)
        {
            if (!other._map.TryGetValue(key, out LumenValue? otherValue) || !value.Equals(otherValue))
            {
                return false;
            }
        }

        return true;
    }

    // 順序不影響相等，所以 hash 也必須與順序無關：用可交換的加總。
    public override int GetHashCode()
    {
        int hash = Count;
        foreach ((LumenValue key, LumenValue value) in _map)
        {
            hash += HashCode.Combine(key, value);
        }

        return hash;
    }

    private static LumenValue NormalizeKey(LumenValue key) =>
        key is FloatValue { Value: var d } && double.IsInteger(d) && Math.Abs(d) < 9.2e18
            ? new IntValue((long)d)
            : key;
}

// 一定是 reference equality：Environment 若參與比較，closure 自我引用會無限遞迴。
public sealed record FunctionValue(FunctionLiteral Declaration, Environment Closure) : LumenValue
{
    public override string TypeName => "Function";

    public override string Inspect() => Declaration.ToString();

    public bool Equals(FunctionValue? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}

public sealed record BuiltinValue(string Name, BuiltinFn Fn) : LumenValue
{
    public override string TypeName => "Builtin";

    public override string Inspect() => $"<builtin {Name}>";

    public bool Equals(BuiltinValue? other) => ReferenceEquals(this, other);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(this);
}
