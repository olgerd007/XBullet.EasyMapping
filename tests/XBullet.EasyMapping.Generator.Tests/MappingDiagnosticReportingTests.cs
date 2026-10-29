using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class MappingDiagnosticReportingTests
{
    [Fact]
    public void Missing_source_error_points_to_the_mapping_and_explains_how_to_fix_it()
    {
        const string source = """
            using XBullet.EasyMapping;
            public record Input(int Id);
            public class Output { public int Id { get; set; } public string Customer { get; set; } = ""; }
            [Mapper] public static partial class M {
                public static Output Map(Input source) => MapCommonProperties(source);
                private static partial Output MapCommonProperties(Input source);
            }
            """;

        var (driver, compilation) = GeneratorTestCompilation.Generate(source);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP003", error.Id);
        Assert.Contains("'Customer'", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'MapCommonProperties'", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("MapProperty", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("MapIgnoreTarget", error.GetMessage(), StringComparison.Ordinal);
        AssertMappingLocation(error, compilation, "MapCommonProperties");
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Conversion_error_names_the_mapping_member_and_types()
    {
        var (driver, compilation) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(long Amount);
            public record Output(int Amount);
            [Mapper] public static partial class M {
                public static partial Output ToRequest(Input source);
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP005", error.Id);
        Assert.Contains("'ToRequest'", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'Amount'", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'long'", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("'int'", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("handwritten converter", error.GetMessage(), StringComparison.Ordinal);
        AssertMappingLocation(error, compilation, "ToRequest");
    }

    [Fact]
    public void Approval_error_is_attached_to_the_mapper_source()
    {
        var (driver, compilation) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public record Output(int Id);
            [Mapper(RequireModelApproval = true)] public static partial class M {
                public static partial Output Map(Input source);
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP011", error.Id);
        AssertMappingLocation(error, compilation, "M");
    }

    [Fact]
    public void Cached_diagnostics_use_the_current_source_tree_after_an_unrelated_edit()
    {
        const string source = """
            using XBullet.EasyMapping;
            public record Input(int Id);
            public record Output(string Id);
            [Mapper] public static partial class M {
                public static partial Output Map(Input source);
            }
            """;
        var (driver, compilation) = GeneratorTestCompilation.Generate(source);
        var oldTree = Assert.Single(compilation.SyntaxTrees);
        var currentTree = CSharpSyntaxTree.ParseText(source + "\n// Unrelated edit.\n",
            (CSharpParseOptions)oldTree.Options, path: oldTree.FilePath);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation.ReplaceSyntaxTree(oldTree, currentTree), out var output, out _);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP005", error.Id);
        AssertMappingLocation(error, output, "Map");
        Assert.Same(currentTree, error.Location.SourceTree);
        var steps = Assert.Single(driver.GetRunResult().Results).TrackedSteps["MapperPlan"];
        Assert.All(steps.SelectMany(step => step.Outputs), value =>
            Assert.True(value.Reason is IncrementalStepRunReason.Unchanged or IncrementalStepRunReason.Cached));
    }

    private static void AssertMappingLocation(Diagnostic error, Compilation compilation, string identifier)
    {
        Assert.True(error.Location.IsInSource);
        Assert.Contains(error.Location.SourceTree, compilation.SyntaxTrees);
        Assert.Equal("TestInput.cs", error.Location.GetLineSpan().Path);
        Assert.Equal(identifier, error.Location.SourceTree!.GetText().ToString(error.Location.SourceSpan));
    }
}
