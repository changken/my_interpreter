namespace Lumen.Tests.Evaluation;

public class EvaluatorArchitectureTests
{
    private const string InterceptionMarker = "// interception point";

    [Fact]
    public void EvaluationSources_NeverPropagateErrorsByNamedTypeCheck()
    {
        string evaluationDir = Path.Combine(FindRepoRoot(), "src", "Lumen.Core", "Evaluation");
        List<string> violations = [];

        foreach (string file in Directory.GetFiles(evaluationDir, "*.cs"))
        {
            string[] lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("is ErrorSignal") && !lines[i].Contains(InterceptionMarker))
                {
                    violations.Add($"{Path.GetFileName(file)}:{i + 1}: {lines[i].Trim()}");
                }
            }
        }

        Assert.Empty(violations);
    }

    private static string FindRepoRoot()
    {
        DirectoryInfo? dir = new(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Lumen.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Lumen.slnx not found above test directory");
    }
}
