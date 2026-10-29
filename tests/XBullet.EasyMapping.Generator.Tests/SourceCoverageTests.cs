using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class SourceCoverageTests
{
    [Theory]
    [InlineData("")]
    [InlineData("(Coverage = MappingCoverage.Target)")]
    public void Target_coverage_keeps_unused_source_properties_compatible(string configuration)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            public record Input(int Id, string Extra);
            public record Output(int Id);
            [Mapper{{configuration}}] public static partial class M { public static partial Output Map(Input input); }
            public static class Scenario { public static bool Verify() => M.Map(new Input(7, "unused")).Id == 7; }
            """);
    }

    [Fact]
    public void Both_reports_each_unaccounted_source_property_as_a_source_located_error()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int Id, string Extra, string Phone);
            public record Output(int Id);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                public static partial Output Map(Input input);
            }
            """);

        var errors = driver.GetRunResult().Diagnostics.Where(diagnostic => diagnostic.Id == "EMAP010").ToArray();
        Assert.Equal(2, errors.Length);
        Assert.Contains(errors, error => error.GetMessage().Contains("'Extra'", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.GetMessage().Contains("'Phone'", StringComparison.Ordinal));
        Assert.All(errors, error =>
        {
            Assert.Equal(DiagnosticSeverity.Error, error.Severity);
            Assert.Equal("TestInput.cs", error.Location.GetLineSpan().Path);
        });
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Renames_constructor_arguments_and_explicit_source_ignores_satisfy_both_coverage()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int Id, string Name, string InternalNote);
            public record Output(int Id, string DisplayName);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty(nameof(Input.Name), nameof(Output.DisplayName))]
                [MapIgnoreSource(nameof(Input.InternalNote))]
                public static partial Output Map(Input input);
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(7, "Ada", "secret")) == new Output(7, "Ada");
            }
            """);
    }

    [Fact]
    public void A_property_used_only_by_a_constructor_is_accounted_for()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public class Output {
                private readonly int _id;
                public Output(int id) { _id = id; }
                public int ReadId() => _id;
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M { public static partial Output Map(Input input); }
            public static class Scenario { public static bool Verify() => M.Map(new Input(7)).ReadId() == 7; }
            """);
    }

    [Fact]
    public void Unselected_constructor_parameters_do_not_account_for_unused_source_properties()
    {
        AssertDiagnostic("""
            using XBullet.EasyMapping;
            public record Input(int Id, string Name);
            public class Output { public Output() {} public Output(string name) {} public int Id { get; set; } }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M { public static partial Output Map(Input input); }
            """, "EMAP010");
    }

    [Fact]
    public void Ignoring_a_target_does_not_implicitly_ignore_its_source()
    {
        AssertDiagnostic("""
            using XBullet.EasyMapping;
            public record Input(int Id, string Name);
            public class Output { public int Id { get; set; } public string Name { get; set; } = ""; }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapIgnoreTarget(nameof(Output.Name))] public static partial Output Map(Input input);
            }
            """, "EMAP010");
    }

    [Fact]
    public void Explicit_ignores_on_both_sides_leave_the_target_value_unchanged()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int Id, string Name);
            public class Output { public int Id { get; set; } public string Name { get; set; } = "default"; }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapIgnoreSource(nameof(Input.Name)), MapIgnoreTarget(nameof(Output.Name))]
                public static partial void Update(Input input, Output output);
            }
            public static class Scenario {
                public static bool Verify() {
                    var output = new Output(); M.Update(new Input(7, "secret"), output);
                    return output.Id == 7 && output.Name == "default";
                }
            }
            """);
    }

    [Theory]
    [InlineData("")]
    [InlineData("(Coverage = MappingCoverage.Both)")]
    public void A_source_cannot_be_both_ignored_and_mapped_even_in_target_mode(string configuration)
    {
        AssertDiagnostic($$"""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public record Output(int Id);
            [Mapper{{configuration}}] public static partial class M {
                [MapIgnoreSource(nameof(Input.Id))] public static partial Output Map(Input input);
            }
            """, "EMAP006");
    }

    [Theory]
    [InlineData("[MapIgnoreSource(\"Absent\")]")]
    [InlineData("[MapIgnoreSource(\"extra\")]")]
    [InlineData("[MapIgnoreSource(null!)]")]
    [InlineData("[MapIgnoreSource()]")]
    [InlineData("[MapIgnoreSource(\"Extra\"), MapIgnoreSource(\"Extra\")]")]
    public void Invalid_source_ignores_fail_instead_of_silently_acknowledging_a_member(string configuration)
    {
        AssertDiagnostic($$"""
            using XBullet.EasyMapping;
            public record Input(int Id, string Extra);
            public record Output(int Id);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                {{configuration}} public static partial Output Map(Input input);
            }
            """, "EMAP006");
    }

    [Fact]
    public void Invalid_coverage_values_are_configuration_errors()
    {
        AssertDiagnostic("""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public record Output(int Id);
            [Mapper(Coverage = (MappingCoverage)42)] public static partial class M { public static partial Output Map(Input input); }
            """, "EMAP006");
    }

    [Fact]
    public void Source_properties_used_by_handwritten_conversions_are_accounted_for()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int Value);
            public record Output(string Value);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                public static partial Output Map(Input input);
                private static string Format(int value) => "converted";
            }
            public static class Scenario { public static bool Verify() => M.Map(new Input(7)).Value == "converted"; }
            """);
    }

    [Fact]
    public void A_source_property_may_supply_multiple_targets()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(string Name);
            public record Output(string First, string Second);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty(nameof(Input.Name), nameof(Output.First)), MapProperty(nameof(Input.Name), nameof(Output.Second))]
                public static partial Output Map(Input input);
            }
            public static class Scenario { public static bool Verify() => M.Map(new Input("value")) == new Output("value", "value"); }
            """);
    }

    [Fact]
    public void Inherited_readable_properties_require_a_mapping_or_ignore()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public class BaseInput { public int Id { get; init; } }
            public class Input : BaseInput { public string Name { get; init; } = "Ada"; }
            public record Output(string Name);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapIgnoreSource(nameof(Input.Id))] public static partial Output Map(Input input);
            }
            public static class Scenario { public static bool Verify() => M.Map(new Input()).Name == "Ada"; }
            """);
    }

    [Fact]
    public void Both_applies_to_every_generated_nested_mapping()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record ChildInput(int Id, string Extra);
            public record ChildDto(int Id);
            public record Input(ChildInput Child);
            public record Output(ChildDto Child);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                public static partial Output Map(Input input);
                private static partial ChildDto MapChild(ChildInput child);
            }
            """);
        var error = Assert.Single(driver.GetRunResult().Diagnostics.Where(diagnostic => diagnostic.Id == "EMAP010"));
        Assert.Contains("MapChild", error.GetMessage());
    }

    [Fact]
    public void Source_fields_cannot_silently_escape_both_coverage()
    {
        AssertDiagnostic("""
            using XBullet.EasyMapping;
            public class Input { public int Id { get; init; } public int Extra; }
            public record Output(int Id);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M { public static partial Output Map(Input input); }
            """, "EMAP009");
    }

    [Theory]
    [InlineData("source_added", "EMAP010")]
    [InlineData("ignored_source_removed", "EMAP006")]
    [InlineData("target_removed", "EMAP010")]
    [InlineData("coverage_enabled", "EMAP010")]
    public void Model_and_option_edits_invalidate_existing_mapping_plans(string change, string expectedDiagnostic)
    {
        var source = """
            using XBullet.EasyMapping;
            public record Input(int Id, string Extra);
            public record Output(int Id);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapIgnoreSource("Extra")] public static partial Output Map(Input input);
            }
            """;
        var initial = change switch
        {
            "source_added" or "target_removed" => source.Replace("[MapIgnoreSource(\"Extra\")]", "", StringComparison.Ordinal),
            "coverage_enabled" => source.Replace("(Coverage = MappingCoverage.Both)", "", StringComparison.Ordinal)
                .Replace("[MapIgnoreSource(\"Extra\")]", "", StringComparison.Ordinal),
            _ => source
        };
        if (change == "source_added")
        {
            initial = initial.Replace("Input(int Id, string Extra)", "Input(int Id)", StringComparison.Ordinal);
        }

        if (change == "target_removed")
        {
            initial = initial.Replace("Output(int Id)", "Output(int Id, string Extra)", StringComparison.Ordinal);
        }

        var (driver, output) = GeneratorTestCompilation.Generate(initial);
        Assert.Empty(driver.GetRunResult().Diagnostics);
        var input = output.RemoveSyntaxTrees(driver.GetRunResult().GeneratedTrees);
        var changed = change switch
        {
            "ignored_source_removed" => initial.Replace("Input(int Id, string Extra)", "Input(int Id)", StringComparison.Ordinal),
            "coverage_enabled" => initial.Replace("[Mapper]", "[Mapper(Coverage = MappingCoverage.Both)]", StringComparison.Ordinal),
            _ => source.Replace("[MapIgnoreSource(\"Extra\")]", "", StringComparison.Ordinal)
        };
        var tree = CSharpSyntaxTree.ParseText(changed, new CSharpParseOptions(LanguageVersion.CSharp12), path: "TestInput.cs");

        driver = driver.RunGenerators(input.ReplaceSyntaxTree(Assert.Single(input.SyntaxTrees), tree));

        Assert.Null(Assert.Single(driver.GetRunResult().Results).Exception);
        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == expectedDiagnostic);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void A_source_edit_in_another_file_revalidates_an_unchanged_mapper_declaration()
    {
        const string mapper = """
            using XBullet.EasyMapping;
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                public static partial Output Map(Input input);
            }
            """;
        const string models = "public record Input(int Id); public record Output(int Id);";
        var (driver, output) = GeneratorTestCompilation.Generate(mapper + "\n" + models);
        Assert.Empty(driver.GetRunResult().Diagnostics);
        var input = output.RemoveSyntaxTrees(driver.GetRunResult().GeneratedTrees);
        var options = new CSharpParseOptions(LanguageVersion.CSharp12);
        var mapperTree = CSharpSyntaxTree.ParseText(mapper, options, path: "Mapper.cs");
        var modelTree = CSharpSyntaxTree.ParseText(models, options, path: "Models.cs");
        input = input.ReplaceSyntaxTree(Assert.Single(input.SyntaxTrees), mapperTree).AddSyntaxTrees(modelTree);
        driver = driver.RunGenerators(input);
        Assert.Empty(driver.GetRunResult().Diagnostics);

        var changedModel = CSharpSyntaxTree.ParseText(models.Replace("Input(int Id)", "Input(int Id, string Phone)", StringComparison.Ordinal),
            options, path: "Models.cs");
        driver = driver.RunGenerators(input.ReplaceSyntaxTree(modelTree, changedModel));

        Assert.Null(Assert.Single(driver.GetRunResult().Results).Exception);
        var error = Assert.Single(driver.GetRunResult().Diagnostics.Where(diagnostic => diagnostic.Id == "EMAP010"));
        Assert.Contains("'Phone'", error.GetMessage());
        Assert.Equal("Mapper.cs", error.Location.GetLineSpan().Path);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    private static void AssertDiagnostic(string source, string expectedId)
    {
        var (driver, _) = GeneratorTestCompilation.Generate(source);
        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == expectedId);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }
}
