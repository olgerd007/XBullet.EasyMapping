using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class DocumentationDiagnosticTests
{
    [Theory]
    [InlineData("/// <summary>Maps orders.</summary>\n", false)]
    [InlineData("/// <summary>\n/// Maps orders.\n/// </summary>\n", false)]
    [InlineData("/// <summary>Maps an order.</summary>\n", true)]
    public void Adding_and_removing_summary_comments_keeps_conversion_errors_on_the_current_method(string comment, bool documentMethod)
    {
        const string source = """
            using XBullet.EasyMapping;
            public record OrderInput(long Total);
            public record OrderDto(int Total);
            [Mapper] public static partial class OrderMapper {
                public static partial OrderDto Map(OrderInput source);
            }
            """;
        var marker = documentMethod ? "public static partial OrderDto Map" : "[Mapper]";
        var documented = source.Replace(marker, comment + marker, StringComparison.Ordinal);
        var (driver, compilation) = GeneratorTestCompilation.Generate(source);

        foreach (var text in new[] { documented, source, documented + "\n// Another edit.\n" })
        {
            var oldTree = Assert.Single(compilation.SyntaxTrees);
            var tree = CSharpSyntaxTree.ParseText(text, (CSharpParseOptions)oldTree.Options, path: oldTree.FilePath);
            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation.ReplaceSyntaxTree(oldTree, tree), out compilation, out _);

            AssertDiagnostic(driver, tree, "EMAP005", "Map");
        }
    }

    [Fact]
    public void Adding_class_documentation_keeps_approval_errors_on_the_current_class()
    {
        const string source = """
            using XBullet.EasyMapping;
            public record OrderInput(int Id);
            public record OrderDto(int Id);
            [Mapper(RequireModelApproval = true)] public static partial class OrderMapper {
                public static partial OrderDto Map(OrderInput source);
            }
            """;
        var (driver, output) = GeneratorTestCompilation.Generate(source);
        var input = output.RemoveSyntaxTrees(driver.GetRunResult().GeneratedTrees);
        var oldTree = Assert.Single(input.SyntaxTrees);
        var documented = source.Replace("[Mapper", "/// <summary>Maps orders.</summary>\n[Mapper", StringComparison.Ordinal);
        var tree = CSharpSyntaxTree.ParseText(documented, (CSharpParseOptions)oldTree.Options, path: oldTree.FilePath);

        driver = driver.RunGeneratorsAndUpdateCompilation(input.ReplaceSyntaxTree(oldTree, tree), out _, out _);

        AssertDiagnostic(driver, tree, "EMAP011", "OrderMapper");
    }

    [Theory]
    [InlineData("/// <summary>Maps orders.</summary>\n", "EMAP005", false)]
    [InlineData("/// <summary>\n/// Maps orders.\n/// </summary>\n", "EMAP005", false)]
    [InlineData("/// <summary>Maps an order.</summary>\n", "EMAP005", true)]
    [InlineData("/// <summary>Maps orders.</summary>\n", "EMAP011", false)]
    public void Incremental_documentation_edits_keep_diagnostics_attached_to_the_current_declaration(
        string comment, string expectedId, bool documentMethod)
    {
        var source = $$"""
            using XBullet.EasyMapping;
            public record OrderInput({{(expectedId == "EMAP005" ? "long" : "int")}} Total);
            public record OrderDto(int Total);
            [Mapper(RequireModelApproval = true)] public static partial class OrderMapper {
                public static partial OrderDto Map(OrderInput source);
            }
            """;
        var marker = documentMethod ? "public static partial OrderDto Map" : "[Mapper";
        var (driver, output) = GeneratorTestCompilation.Generate(source);
        var input = output.RemoveSyntaxTrees(driver.GetRunResult().GeneratedTrees);
        var insertion = source.IndexOf(marker, StringComparison.Ordinal);

        foreach (var change in new[]
        {
            new TextChange(new TextSpan(insertion, 0), comment),
            new TextChange(new TextSpan(insertion, comment.Length), ""),
            new TextChange(new TextSpan(insertion, 0), comment)
        })
        {
            var oldTree = Assert.Single(input.SyntaxTrees);
            var tree = oldTree.WithChangedText(oldTree.GetText().WithChanges(change));
            input = input.ReplaceSyntaxTree(oldTree, tree);
            driver = driver.RunGeneratorsAndUpdateCompilation(input, out _, out _);

            AssertDiagnostic(driver, tree, expectedId, expectedId == "EMAP011" ? "OrderMapper" : "Map");
        }
    }

    private static void AssertDiagnostic(GeneratorDriver driver, SyntaxTree tree, string id, string identifier)
    {
        Assert.Null(Assert.Single(driver.GetRunResult().Results).Exception);
        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal(id, error.Id);
        Assert.True(error.Location.IsInSource);
        Assert.Same(tree, error.Location.SourceTree);
        Assert.Equal(identifier, tree.GetText().ToString(error.Location.SourceSpan));
    }
}
