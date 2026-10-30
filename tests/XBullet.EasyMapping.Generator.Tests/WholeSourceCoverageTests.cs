using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class WholeSourceCoverageTests
{
    [Fact]
    public void Declared_sources_can_supply_multiple_helpers_and_conventional_assignments()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int Id, string First, string Last, string Audit);
            public record Output(int Id, string Name, string Label);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty(nameof(Output.Name), Use = nameof(BuildName),
                    Sources = new[] { nameof(Input.First), nameof(Input.Last) })]
                [MapProperty(nameof(Output.Label), Use = nameof(BuildLabel),
                    Sources = new[] { nameof(Input.Id), nameof(Input.First) })]
                [MapIgnoreSource(nameof(Input.Audit))]
                public static partial Output Map(Input source);
                private static string BuildName(Input source) => source.First + " " + source.Last;
                private static string BuildLabel(Input source) => source.Id + ":" + source.First;
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(7, "Ada", "Lovelace", "internal")) ==
                    new Output(7, "Ada Lovelace", "7:Ada");
            }
            """);
    }

    [Theory]
    [InlineData("new[] { nameof(Input.First) }", 1)]
    [InlineData("new string[] { }", 2)]
    public void Undeclared_properties_still_require_source_coverage(string sources, int expectedErrors)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record Input(string First, string Last);
            public record Output(string Name);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty(nameof(Output.Name), Use = nameof(Format), Sources = {{sources}})]
                public static partial Output Map(Input source);
                private static string Format(Input source) => source.First + " " + source.Last;
            }
            """);

        var errors = driver.GetRunResult().Diagnostics;
        Assert.Equal(expectedErrors, errors.Length);
        Assert.All(errors, error => Assert.Equal("EMAP010", error.Id));
        Assert.Contains(errors, error => error.GetMessage().Contains("Last", StringComparison.Ordinal));
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Declared_sources_do_not_hide_unmapped_target_members()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(string Name);
            public class Output {
                public string Label { get; set; } = "";
                public string Missing { get; set; } = "";
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty(nameof(Output.Label), Use = nameof(Format), Sources = new[] { nameof(Input.Name) })]
                public static partial Output Map(Input source);
                private static string Format(Input source) => source.Name;
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP003" &&
            error.GetMessage().Contains("Missing", StringComparison.Ordinal));
        Assert.DoesNotContain(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP010");
    }

    [Fact]
    public void Inherited_sources_and_nested_object_roots_can_be_declared_without_generated_getter_reads()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public class BaseInput { public string First => "Ada"; }
            public record Child(string Last);
            public class Input : BaseInput {
                public Child Child => new("Lovelace");
                public int AccessCount => throw new System.InvalidOperationException("Must not be read by coverage");
            }
            public record Output(string Name);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty(nameof(Output.Name), Use = nameof(Format),
                    Sources = new[] { nameof(Input.First), nameof(Input.Child), nameof(Input.AccessCount) })]
                public static partial Output Map(Input source);
                private static string Format(Input source) => source.First + " " + source.Child.Last;
            }
            public static class Scenario { public static bool Verify() => M.Map(new Input()).Name == "Ada Lovelace"; }
            """);
    }

    [Theory]
    [InlineData("")]
    [InlineData("(Coverage = MappingCoverage.Both)")]
    public void Declared_sources_cannot_also_be_ignored_in_either_coverage_mode(string coverage)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record Input(string Name);
            public record Output(string Label);
            [Mapper{{coverage}}] public static partial class M {
                [MapProperty(nameof(Output.Label), Use = nameof(Format), Sources = new[] { nameof(Input.Name) })]
                [MapIgnoreSource(nameof(Input.Name))]
                public static partial Output Map(Input source);
                private static string Format(Input source) => source.Name;
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP006", error.Id);
        Assert.Contains("both mapped and ignored", error.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("new string[] { null! }")]
    [InlineData("new[] { \"\" }")]
    [InlineData("new[] { \" \" }")]
    [InlineData("new[] { \"Missing\" }")]
    [InlineData("new[] { \"name\" }")]
    [InlineData("new[] { nameof(Input.Name), nameof(Input.Name) }")]
    [InlineData("new[] { \"Child.Name\" }")]
    [InlineData("new[] { \"Shared\" }")]
    [InlineData("new[] { \"WriteOnly\" }")]
    [InlineData("new[] { \"PrivateGetter\" }")]
    [InlineData("new[] { \"Field\" }")]
    [InlineData("new[] { \"Item\" }")]
    public void Invalid_source_declarations_are_configuration_errors_even_in_target_mode(string sources)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public class Input {
                public string Name => "Ada";
                public Input Child => this;
                public static string Shared => "shared";
                public string WriteOnly { set {} }
                public string PrivateGetter { private get; set; } = "hidden";
                public string Field = "field";
                public string this[int index] => "index";
            }
            public record Output(string Label);
            [Mapper] public static partial class M {
                [MapProperty(nameof(Output.Label), Use = nameof(Format), Sources = {{sources}})]
                public static partial Output Map(Input source);
                private static string Format(Input source) => source.Name;
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP006" &&
            error.GetMessage().Contains("MapProperty.Sources", StringComparison.Ordinal));
        Assert.All(driver.GetRunResult().Diagnostics, error =>
            Assert.Equal("TestInput.cs", error.Location.GetLineSpan().Path));
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("new[] { nameof(Input.Name) }")]
    [InlineData("new string[] { }")]
    [InlineData("null")]
    public void Source_declarations_require_the_target_only_overload(string sources)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record Input(string Name);
            public record Output(string Name);
            [Mapper] public static partial class M {
                [MapProperty(nameof(Input.Name), nameof(Output.Name), Use = nameof(Format), Sources = {{sources}})]
                public static partial Output Map(Input source);
                private static string Format(string value) => value;
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP006" &&
            error.GetMessage().Contains("target-only", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("new[] { nameof(Input.Name), nameof(Input.Note) }")]
    [InlineData("new[] { nameof(Input.Name) }")]
    public void Changing_declared_sources_requires_model_shape_approval(string sources)
    {
        const string source = """
            using XBullet.EasyMapping;
            public record Input(string Name, string Note);
            public record Output(string Label);
            [Mapper(RequireModelApproval = true)] public static partial class M {
                [MapProperty(nameof(Output.Label), Use = nameof(Format), Sources = new[] { nameof(Input.Note) })]
                public static partial Output Map(Input source);
                private static string Format(Input source) => source.Name;
            }
            """;
        var (initial, _) = GeneratorTestCompilation.Generate(source);
        var candidate = Assert.Single(Assert.Single(initial.GetRunResult().Results).GeneratedSources.Where(
            item => item.HintName.EndsWith(".ModelApproval.g.cs", StringComparison.Ordinal)));
        var lines = candidate.SourceText.ToString().Split('\n');
        var start = Array.IndexOf(lines, "// <MapperModelApproval>");
        var end = Array.IndexOf(lines, "// </MapperModelApproval>");
        Assert.True(start >= 0 && end > start);
        var approval = new MemoryApproval("M.mapper.approved.txt",
            string.Join("\n", lines[(start + 1)..end].Select(line => line[3..])) + "\n");
        Assert.Contains("Sources=[\"Note\"]", approval.GetText().ToString(), StringComparison.Ordinal);
        var (approved, _) = GeneratorTestCompilation.Generate(source, approval);
        Assert.Empty(approved.GetRunResult().Diagnostics);

        var (changed, _) = GeneratorTestCompilation.Generate(source.Replace(
            "new[] { nameof(Input.Note) }", sources, StringComparison.Ordinal), approval);

        Assert.Equal("EMAP011", Assert.Single(changed.GetRunResult().Diagnostics).Id);
    }

    private sealed class MemoryApproval(string path, string content) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
    }
}
