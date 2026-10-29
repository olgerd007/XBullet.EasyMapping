using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using XBullet.EasyMapping.Generator;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class ModelApprovalMetadataTests
{
    [Theory]
    [InlineData("mscorlib", false)]
    [InlineData("netstandard", false)]
    [InlineData("System", false)]
    [InlineData("System.Orders", false)]
    [InlineData("OrderContracts", true)]
    public void Imported_application_models_are_inspected_while_framework_assembly_leaves_stay_opaque(string assemblyName, bool includeNestedModel)
    {
        var (_, template) = GeneratorTestCompilation.Generate("public class Order {}");
        var options = new CSharpParseOptions(LanguageVersion.CSharp12);
        var contracts = template.RemoveAllSyntaxTrees().WithAssemblyName(assemblyName).AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
            namespace Contracts;
            public class Customer { public int Id { get; set; } }
            """, options));
        using var stream = new MemoryStream();
        var result = contracts.Emit(stream);
        Assert.True(result.Success, string.Join(Environment.NewLine, result.Diagnostics));
        var consumer = template.RemoveAllSyntaxTrees().AddReferences(MetadataReference.CreateFromImage(stream.ToArray()))
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
                using XBullet.EasyMapping;
                public record OrderInput(int Id, Contracts.Customer Customer);
                public record OrderDto(int Id);
                [Mapper(RequireModelApproval = true)] public static partial class OrderMapper {
                    public static partial OrderDto Map(OrderInput source);
                }
                """, options, path: "Orders.cs"));
        GeneratorDriver driver = CSharpGeneratorDriver.Create([new MappingGenerator().AsSourceGenerator()], parseOptions: options);
        driver = driver.RunGeneratorsAndUpdateCompilation(consumer, out var output, out _);

        Assert.Equal("EMAP011", Assert.Single(driver.GetRunResult().Diagnostics).Id);
        Assert.Empty(output.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        var snapshot = Assert.Single(Assert.Single(driver.GetRunResult().Results).GeneratedSources.Where(source =>
            source.HintName.EndsWith(".ModelApproval.g.cs", StringComparison.Ordinal))).SourceText.ToString();
        Assert.Equal(includeNestedModel, snapshot.Contains("model global::Contracts.Customer", StringComparison.Ordinal));
    }

    [Fact]
    public void Ignored_unsafe_source_members_are_recorded_without_recursing_into_pointer_types()
    {
        var (_, template) = GeneratorTestCompilation.Generate("public class Order {}");
        var input = ((CSharpCompilation)template).RemoveAllSyntaxTrees()
            .WithOptions(((CSharpCompilationOptions)template.Options).WithAllowUnsafe(true))
            .AddSyntaxTrees(CSharpSyntaxTree.ParseText("""
                using XBullet.EasyMapping;
                public unsafe class OrderInput {
                    public int Id { get; set; }
                    public int* Buffer { get; set; }
                    public int Version;
                }
                public record OrderDto(int Id);
                [Mapper(RequireModelApproval = true)] public static partial class OrderMapper {
                    public static partial OrderDto Map(OrderInput source);
                }
                """, new CSharpParseOptions(LanguageVersion.CSharp12), path: "Orders.cs"));
        var mapper = input.GetTypeByMetadataName("OrderMapper")!;
        var method = Assert.Single(mapper.GetMembers("Map").OfType<IMethodSymbol>());

        var snapshot = ModelShapeBuilder.Create(mapper, [method], CancellationToken.None);

        Assert.NotNull(snapshot);
        Assert.Contains("Buffer : int*", snapshot.Content, StringComparison.Ordinal);
        Assert.Contains("field Version : int readonly=False static=False", snapshot.Content, StringComparison.Ordinal);
    }
}
