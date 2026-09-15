using Lumen.Core.Objects;

namespace Lumen.Core.Evaluation;

/// <summary>Builtin function 的 plugin 註冊表；Evaluator 只透過它查名字，不認識任何具體 builtin。</summary>
public sealed class BuiltinRegistry
{
    private readonly Dictionary<string, BuiltinValue> _builtins = [];

    public IEnumerable<string> Names => _builtins.Keys;

    public void Register(string name, BuiltinFn fn) => _builtins[name] = new BuiltinValue(name, fn);

    public bool TryGet(string name, out BuiltinValue value) => _builtins.TryGetValue(name, out value!);
}
