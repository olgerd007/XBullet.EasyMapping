using Microsoft.CodeAnalysis;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class DocumentationExampleTests
{
    [Fact]
    public void Published_quickstart_compiles_as_a_complete_console_program()
    {
        var guide = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Documentation", "easy-mapping.md"))
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        const string opening = "```csharp\n";
        var start = guide.IndexOf(opening, StringComparison.Ordinal);
        Assert.True(start >= 0, "The installation guide must contain a complete C# example.");
        start += opening.Length;
        var end = guide.IndexOf("\n```", start, StringComparison.Ordinal);
        Assert.True(end > start, "The C# example must have a closing code fence.");

        var (driver, compilation) = GeneratorTestCompilation.Generate(guide[start..end], OutputKind.ConsoleApplication);

        Assert.Empty(driver.GetRunResult().Diagnostics);
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        using var assembly = new MemoryStream();
        var result = compilation.Emit(assembly);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
    }
}
