namespace Lumen.Core.Objects;

/// <summary>
/// Scope chain 的一層。由 caller 持有並注入 Evaluator（REPL 靠這點跨行保留變數）。
/// </summary>
public sealed class Environment
{
    private readonly Dictionary<string, LumenValue> _values = [];
    private readonly List<string> _order = [];

    public Environment()
    {
    }

    public Environment(Environment outer)
    {
        Outer = outer;
    }

    public Environment? Outer { get; }

    /// <summary>只有本層的 binding，依定義順序。</summary>
    public IReadOnlyList<KeyValuePair<string, LumenValue>> Bindings =>
        _order.Select(name => new KeyValuePair<string, LumenValue>(name, _values[name])).ToList();

    /// <summary>在本層宣告；同名重複宣告直接覆蓋。</summary>
    public void Define(string name, LumenValue value)
    {
        if (_values.TryAdd(name, value))
        {
            _order.Add(name);
        }
        else
        {
            _values[name] = value;
        }
    }

    public bool TryGet(string name, out LumenValue value)
    {
        for (Environment? scope = this; scope is not null; scope = scope.Outer)
        {
            if (scope._values.TryGetValue(name, out value!))
            {
                return true;
            }
        }

        value = null!;
        return false;
    }

    /// <summary>沿 scope chain 找到既有 binding 就改它；找不到回 false，不會偷偷宣告。</summary>
    public bool TryAssign(string name, LumenValue value)
    {
        for (Environment? scope = this; scope is not null; scope = scope.Outer)
        {
            if (scope._values.ContainsKey(name))
            {
                scope._values[name] = value;
                return true;
            }
        }

        return false;
    }
}
