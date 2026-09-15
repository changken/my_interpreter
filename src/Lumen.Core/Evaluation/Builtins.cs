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

        return registry;
    }
}
