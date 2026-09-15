using System.Reflection;
using Lumen.Core.Ast;

namespace Lumen.Tests.Ast;

public class AstDependencyTests
{
    private const string AstNamespace = "Lumen.Core.Ast";

    private const BindingFlags AllDeclared =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void AstTypes_ReferenceOnlyAstTokensAndSystemTypes()
    {
        List<Type> astTypes = typeof(Node).Assembly.GetTypes()
            .Where(t => t.Namespace == AstNamespace)
            .ToList();
        Assert.NotEmpty(astTypes);

        HashSet<Type> visited = [];
        List<string> violations = [];

        foreach (Type astType in astTypes)
        {
            foreach (Type referenced in ReferencedTypes(astType))
            {
                foreach (Type leaf in Leaves(referenced, visited))
                {
                    if (!IsAllowed(leaf))
                    {
                        violations.Add($"{astType.Name} -> {leaf.FullName}");
                    }
                }
            }
        }

        Assert.Empty(violations);
    }

    private static bool IsAllowed(Type type)
    {
        if (type.IsGenericParameter)
        {
            return true;
        }

        string ns = type.Namespace ?? string.Empty;
        return ns == AstNamespace
            || ns == "Lumen.Core.Tokens"
            || ns == "System"
            || ns.StartsWith("System.", StringComparison.Ordinal);
    }

    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        if (type.BaseType is not null)
        {
            yield return type.BaseType;
        }

        foreach (Type i in type.GetInterfaces())
        {
            yield return i;
        }

        foreach (FieldInfo f in type.GetFields(AllDeclared))
        {
            yield return f.FieldType;
        }

        foreach (PropertyInfo p in type.GetProperties(AllDeclared))
        {
            yield return p.PropertyType;
        }

        foreach (MethodInfo m in type.GetMethods(AllDeclared))
        {
            yield return m.ReturnType;
            foreach (ParameterInfo param in m.GetParameters())
            {
                yield return param.ParameterType;
            }
        }

        foreach (ConstructorInfo c in type.GetConstructors(AllDeclared))
        {
            foreach (ParameterInfo param in c.GetParameters())
            {
                yield return param.ParameterType;
            }
        }
    }

    // 拆到最底層：generic 引數、陣列元素、byref/pointer 目標，讓 IReadOnlyList<Objects.X> 這種包在 System 型別裡的引用也逃不掉。
    private static IEnumerable<Type> Leaves(Type type, HashSet<Type> visited)
    {
        if (!visited.Add(type))
        {
            yield break;
        }

        if (type.HasElementType)
        {
            foreach (Type leaf in Leaves(type.GetElementType()!, visited))
            {
                yield return leaf;
            }

            yield break;
        }

        if (type.IsGenericType)
        {
            yield return type.GetGenericTypeDefinition();
            foreach (Type arg in type.GetGenericArguments())
            {
                foreach (Type leaf in Leaves(arg, visited))
                {
                    yield return leaf;
                }
            }

            yield break;
        }

        yield return type;
    }
}
