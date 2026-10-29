using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

internal static class GeneratorTestCompilation
{
    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.CSharp12);
    private static readonly ImmutableArray<MetadataReference> References =
        ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
        .Append(typeof(MapperAttribute).Assembly.Location).Distinct(StringComparer.OrdinalIgnoreCase)
        .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToImmutableArray();

    public static (GeneratorDriver Driver, Compilation Output) Generate(string source, params AdditionalText[] additionalTexts) =>
        Generate(source, OutputKind.DynamicallyLinkedLibrary, additionalTexts);

    public static (GeneratorDriver Driver, Compilation Output) Generate(string source, OutputKind outputKind, params AdditionalText[] additionalTexts)
    {
        var compilation = CSharpCompilation.Create("MapperTest_" + Guid.NewGuid().ToString("N"),
            [CSharpSyntaxTree.ParseText(source, ParseOptions, path: "TestInput.cs")], References,
            new CSharpCompilationOptions(outputKind, nullableContextOptions: NullableContextOptions.Enable));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new MappingGenerator().AsSourceGenerator()],
            additionalTexts: additionalTexts, parseOptions: ParseOptions,
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        Assert.Null(Assert.Single(driver.GetRunResult().Results).Exception);
        return (driver, output);
    }

    public static void VerifyRuntime(string source)
    {
        var (driver, compilation) = Generate(source);
        Assert.Empty(driver.GetRunResult().Diagnostics);
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        using var assemblyStream = new MemoryStream();
        var emit = compilation.Emit(assemblyStream);
        Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
        var assembly = Assembly.Load(assemblyStream.ToArray());
        var verify = assembly.GetType("Scenario")!.GetMethod("Verify")!;
        Assert.True((bool)verify.Invoke(null, null)!);
    }
}
