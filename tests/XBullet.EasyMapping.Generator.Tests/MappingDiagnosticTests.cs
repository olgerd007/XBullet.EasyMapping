using Microsoft.CodeAnalysis;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class MappingDiagnosticTests
{
    [Theory]
    [InlineData("EMAP001", "public class Outer { [Mapper] public static partial class Nested {} }")]
    [InlineData("EMAP001", "[Mapper] public partial class Generic<T> {}")]
    [InlineData("EMAP001", "[Mapper] file static partial class M { public static partial Output Map(Input source); }")]
    [InlineData("EMAP002", "[Mapper] public static partial class M { public static partial Output Map<T>(Input input); }")]
    [InlineData("EMAP003", "[Mapper] public static partial class M { public static partial Output Map(Input input); }")]
    [InlineData("EMAP006", "[Mapper] public static partial class M { [MapProperty(\"Absent\", \"Missing\")] public static partial Output Map(Input input); }")]
    [InlineData("EMAP006", "[Mapper] public static partial class M { [MapIgnoreTarget(\"Absent\")] public static partial Output Map(Input input); }")]
    public void Invalid_declarations_report_source_located_errors(string expectedId, string declaration)
    {
        var source = "using XBullet.EasyMapping; public record Input(int Id); public class Output { public int Id {get;set;} public string Missing {get;set;} = \"\"; } " + declaration;
        var (driver, _) = GeneratorTestCompilation.Generate(source);

        var error = Assert.Single(driver.GetRunResult().Diagnostics.Where(diagnostic => diagnostic.Id == expectedId));
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Equal("TestInput.cs", error.Location.GetLineSpan().Path);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("string?", "string", "nullable source")]
    [InlineData("long", "int", "declare a mapping")]
    [InlineData("System.DateTime", "string", "declare a mapping")]
    public void Unsafe_conversions_are_errors(string sourceType, string targetType, string message)
    {
        var source = $$"""
            using XBullet.EasyMapping;
            public record Input({{sourceType}} Value);
            public record Output({{targetType}} Value);
            [Mapper] public static partial class M { public static partial Output Map(Input input); }
            """;
        var (driver, _) = GeneratorTestCompilation.Generate(source);
        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic =>
            diagnostic.Id == "EMAP005" && diagnostic.GetMessage().Contains(message, StringComparison.Ordinal));
    }

    [Fact]
    public void Multiple_mappable_constructors_require_an_explicit_handwritten_mapping()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int Id, string Name);
            public class Output { public Output(int id) {} public Output(string name) {} }
            [Mapper] public static partial class M { public static partial Output Map(Input input); }
            """);
        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP004");
    }

    [Fact]
    public void Multiple_converters_are_rejected_instead_of_picking_the_first()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int Value);
            public record Output(string Value);
            [Mapper] public static partial class M {
                public static partial Output Map(Input input);
                private static string First(int value) => value.ToString();
                private static string Second(int value) => value.ToString();
            }
            """);
        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP007" &&
            diagnostic.GetMessage().Contains("MapProperty.Use", StringComparison.Ordinal));
    }

    [Fact]
    public void Recursive_partial_mappings_are_rejected_before_runtime()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(Input? Next);
            public record Output(Output? Next);
            [Mapper] public static partial class M { public static partial Output Map(Input input); }
            """);
        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP008");
    }

    [Fact]
    public void Required_targets_cannot_be_ignored()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public class Output { public required int Id { get; init; } }
            [Mapper] public static partial class M {
                [MapIgnoreTarget(nameof(Output.Id))] public static partial Output Map(Input input);
            }
            """);
        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP006");
    }

    [Fact]
    public void Updates_cannot_assign_init_only_members()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public record Output(int Id);
            [Mapper] public static partial class M { public static partial void Map(Input input, Output target); }
            """);
        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP009");
    }

    [Fact]
    public void Inherited_public_fields_are_rejected_instead_of_silently_left_unmapped()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public class BaseOutput { public int Id; }
            public class Output : BaseOutput { }
            [Mapper] public static partial class M { public static partial Output Map(Input input); }
            """);
        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP009");
    }

    [Fact]
    public void Configuration_for_an_unselected_constructor_is_not_silently_ignored()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public class Output { public Output() {} public Output(int unused) {} public int Id { get; set; } }
            [Mapper] public static partial class M {
                [MapProperty(nameof(Input.Id), "unused")] public static partial Output Map(Input input);
            }
            """);
        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP006");
    }

    [Fact]
    public void Malformed_attribute_arguments_do_not_crash_the_generator()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public record Output(int Id);
            [Mapper] public static partial class M { [MapProperty()] public static partial Output Map(Input input); }
            """);
        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP006");
    }

    [Fact]
    public void Mutable_reference_members_require_a_declared_mapping_instead_of_shallow_assignment()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public class Mutable { public string Name { get; set; } = ""; }
            public record Input(Mutable Value);
            public record Output(Mutable Value);
            [Mapper] public static partial class M { public static partial Output Map(Input input); }
            """);
        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP005");
    }

    [Fact]
    public void Case_ambiguous_property_names_produce_a_diagnostic_not_a_generator_exception()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int ID, int Id);
            public record Output(int id);
            [Mapper] public static partial class M { public static partial Output Map(Input input); }
            """);
        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP006");
    }
}
