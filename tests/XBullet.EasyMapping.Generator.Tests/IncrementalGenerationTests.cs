using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class IncrementalGenerationTests
{
    private const string Source = """
        using XBullet.EasyMapping;
        public record Input(int Id);
        public record Output(int Id);
        [Mapper] public static partial class M { public static partial Output Map(Input input); }
        """;

    [Fact]
    public void Unrelated_edits_leave_the_mapping_plan_unchanged()
    {
        var (driver, output) = GeneratorTestCompilation.Generate(Source);
        var input = output.RemoveSyntaxTrees(driver.GetRunResult().GeneratedTrees);
        input = input.AddSyntaxTrees(CSharpSyntaxTree.ParseText("public class Unrelated { public int Value { get; set; } }",
            new CSharpParseOptions(LanguageVersion.CSharp12)));

        driver = driver.RunGenerators(input);

        var steps = Assert.Single(driver.GetRunResult().Results).TrackedSteps["MapperPlan"];
        Assert.All(steps.SelectMany(step => step.Outputs), value =>
            Assert.True(value.Reason is IncrementalStepRunReason.Unchanged or IncrementalStepRunReason.Cached));
    }

    [Fact]
    public void Changes_to_a_referenced_dto_invalidate_the_mapping_plan()
    {
        var (driver, output) = GeneratorTestCompilation.Generate(Source);
        var input = output.RemoveSyntaxTrees(driver.GetRunResult().GeneratedTrees);
        var oldTree = Assert.Single(input.SyntaxTrees);
        var changedTree = CSharpSyntaxTree.ParseText(Source.Replace("Input(int Id)", "Input(string Id)", StringComparison.Ordinal),
            new CSharpParseOptions(LanguageVersion.CSharp12), path: "TestInput.cs");

        driver = driver.RunGenerators(input.ReplaceSyntaxTree(oldTree, changedTree));

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP005");
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Generated_source_is_deterministic_and_contains_no_runtime_mapper_calls()
    {
        var first = GeneratorTestCompilation.Generate(Source).Driver.GetRunResult();
        var second = GeneratorTestCompilation.Generate(Source).Driver.GetRunResult();
        var firstSource = Assert.Single(Assert.Single(first.Results).GeneratedSources);
        var secondSource = Assert.Single(Assert.Single(second.Results).GeneratedSources);

        Assert.Equal(firstSource.HintName, secondSource.HintName);
        Assert.Equal(firstSource.SourceText.ToString(), secondSource.SourceText.ToString());
        Assert.DoesNotContain("System.Reflection", firstSource.SourceText.ToString());
        Assert.DoesNotContain("XBullet.EasyMapping", firstSource.SourceText.ToString());
    }
}
