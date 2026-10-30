using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class WholeSourceHelperTests
{
    [Fact]
    public void Separate_helpers_construct_nested_objects_from_multiple_source_members()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(bool PrimaryEnabled, string PrimaryText, int PrimaryRank,
                bool SecondaryEnabled, string SecondaryText, int SecondaryRank);
            public record ValueGroup(bool Enabled, string Text, int Rank);
            public record Output(ValueGroup Primary, ValueGroup Secondary);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty(nameof(Output.Primary), Use = nameof(BuildPrimary),
                    Sources = new[] { nameof(Input.PrimaryEnabled), nameof(Input.PrimaryText), nameof(Input.PrimaryRank) })]
                [MapProperty(nameof(Output.Secondary), Use = nameof(BuildSecondary),
                    Sources = new[] { nameof(Input.SecondaryEnabled), nameof(Input.SecondaryText), nameof(Input.SecondaryRank) })]
                public static partial Output Map(Input source);
                private static ValueGroup BuildPrimary(Input source) =>
                    new(source.PrimaryEnabled, source.PrimaryText, source.PrimaryRank);
                private static ValueGroup BuildSecondary(Input source) =>
                    new(source.SecondaryEnabled, source.SecondaryText, source.SecondaryRank);
            }
            public static class Scenario {
                public static bool Verify() {
                    var result = M.Map(new Input(true, "first", 1, false, "second", 2));
                    return result.Primary == new ValueGroup(true, "first", 1) &&
                        result.Secondary == new ValueGroup(false, "second", 2);
                }
            }
            """);
    }

    [Fact]
    public void Instance_helpers_support_nested_target_creation_and_existing_target_updates()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(string First, string Last);
            public class Child { public string Name { get; set; } = ""; }
            public class Output { public Child? Child { get; set; } }
            [Mapper(Coverage = MappingCoverage.Both)] public partial class M {
                private readonly string _prefix;
                public M(string prefix) { _prefix = prefix; }
                [MapProperty("Child.Name", Use = nameof(Format), Sources = new[] { nameof(Input.First), nameof(Input.Last) })]
                public partial Output Map(Input source);
                [MapProperty("Child.Name", Use = nameof(Format), Sources = new[] { nameof(Input.First), nameof(Input.Last) })]
                public partial void Update(Input source, Output target);
                private string Format(Input source) => _prefix + source.First + " " + source.Last;
            }
            public static class Scenario {
                public static bool Verify() {
                    var mapper = new M("name: ");
                    var target = mapper.Map(new Input("Ada", "Lovelace"));
                    if (target.Child?.Name != "name: Ada Lovelace") return false;
                    var child = target.Child;
                    mapper.Update(new Input("Grace", "Hopper"), target);
                    var empty = new Output();
                    mapper.Update(new Input("Alan", "Turing"), empty);
                    return object.ReferenceEquals(child, target.Child) && target.Child?.Name == "name: Grace Hopper" &&
                        empty.Child?.Name == "name: Alan Turing";
                }
            }
            """);
    }

    [Fact]
    public void Whole_object_updates_replace_the_target_member_and_preserve_ignored_members()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(string Name);
            public record Child(string Name);
            public class Output { public Child? Child { get; set; } public int Version { get; set; } }
            [Mapper] public partial class M {
                [MapProperty(nameof(Output.Child), Use = nameof(Build))]
                [MapIgnoreTarget(nameof(Output.Version))]
                public partial void Update(Input source, Output target);
                private Child Build(Input source) => new(source.Name);
            }
            public static class Scenario {
                public static bool Verify() {
                    var previous = new Child("old");
                    var target = new Output { Child = previous, Version = 7 };
                    new M().Update(new Input("new"), target);
                    return target.Child?.Name == "new" && !object.ReferenceEquals(previous, target.Child) && target.Version == 7;
                }
            }
            """);
    }

    [Fact]
    public void Required_constructor_members_reuse_one_helper_result_per_target_and_call()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(string Name);
            public record Child(string Name);
            public class Output {
                public Output(Child child) { Child = child; ConstructorChild = child; }
                public required Child Child { get; init; }
                public Child ConstructorChild { get; }
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                public static int Calls;
                [MapProperty(nameof(Output.Child), Use = nameof(Build), Sources = new[] { nameof(Input.Name) })]
                [MapIgnoreTarget(nameof(Output.ConstructorChild))]
                public static partial Output Map(Input source);
                private static Child Build(Input source) { Calls++; return new(source.Name); }
            }
            public static class Scenario {
                public static bool Verify() {
                    var first = M.Map(new Input("first"));
                    var second = M.Map(new Input("second"));
                    return M.Calls == 2 && object.ReferenceEquals(first.Child, first.ConstructorChild) &&
                        object.ReferenceEquals(second.Child, second.ConstructorChild) && first.Child.Name == "first" &&
                        second.Child.Name == "second" && !object.ReferenceEquals(first.Child, second.Child);
                }
            }
            """);
    }

    [Theory]
    [InlineData("public record Input(string Name);", "Input", "Input?", "new Input(\"Ada\")", "source.Name")]
    [InlineData("public readonly record struct Input(string Name);", "Input", "Input?", "new Input(\"Ada\")", "source.Name")]
    public void Nullable_roots_are_guarded_and_helpers_receive_the_non_null_source(
        string declaration, string helperType, string sourceType, string input, string value)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            {{declaration}}
            public record Output(string Name);
            [Mapper] public static partial class M {
                public static int Calls;
                [MapProperty(nameof(Output.Name), Use = nameof(Format))]
                public static partial Output? Map({{sourceType}} source);
                private static string Format({{helperType}} source) { Calls++; return {{value}}; }
            }
            public static class Scenario {
                public static bool Verify() => M.Map(null) is null && M.Calls == 0 &&
                    M.Map({{input}})?.Name == "Ada" && M.Calls == 1;
            }
            """);
    }

    [Fact]
    public void Nullable_helper_results_are_supported_for_nullable_targets()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(string? Name);
            public record Output(string? Name);
            [Mapper] public static partial class M {
                [MapProperty(nameof(Output.Name), Use = nameof(Format))]
                public static partial Output Map(Input source);
                private static string? Format(Input? source) => source?.Name;
            }
            public static class Scenario { public static bool Verify() => M.Map(new Input(null)).Name is null; }
            """);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[MapIgnoreSource(nameof(Input.First)), MapIgnoreSource(nameof(Input.Last))]")]
    public void Whole_source_helpers_do_not_implicitly_consume_source_properties_in_both_mode(string ignores)
    {
        var (driver, compilation) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record Input(string First, string Last);
            public record Output(string Name);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                {{ignores}}
                [MapProperty(nameof(Output.Name), Use = nameof(Format))]
                public static partial Output Map(Input source);
                private static string Format(Input source) => source.First + " " + source.Last;
            }
            """);

        if (ignores.Length == 0)
        {
            var errors = driver.GetRunResult().Diagnostics;
            Assert.Equal(2, errors.Length);
            Assert.All(errors, error => Assert.Equal("EMAP010", error.Id));
        }
        else
        {
            Assert.Empty(driver.GetRunResult().Diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        }
    }

    [Fact]
    public void Whole_source_helpers_coexist_with_member_helpers_and_conventional_source_coverage()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int Id, string Name);
            public record Output(int Id, string Name, string Label);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty(nameof(Input.Name), nameof(Output.Name), Use = nameof(Trim))]
                [MapProperty(nameof(Output.Label), Use = nameof(Format))]
                public static partial Output Map(Input source);
                private static string Trim(string value) => value.Trim();
                private static string Format(Input source) => source.Id + ":" + source.Name.Trim();
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(7, " Ada ")) == new Output(7, "Ada", "7:Ada");
            }
            """);
    }

    [Theory]
    [InlineData("[MapProperty(nameof(Output.Name))]")]
    [InlineData("[MapProperty(nameof(Output.Name), Use = null)]")]
    [InlineData("[MapProperty(nameof(Output.Name), Use = \"\")]")]
    [InlineData("[MapProperty(nameof(Output.Name), Use = \" \")]")]
    [InlineData("[MapProperty(null, Use = nameof(Format))]")]
    [InlineData("[MapProperty(\"\", Use = nameof(Format))]")]
    [InlineData("[MapProperty(\"Missing\", Use = nameof(Format))]")]
    [InlineData("[MapProperty(nameof(Output.Name), Use = nameof(Format)), MapIgnoreTarget(nameof(Output.Name))]")]
    [InlineData("[MapProperty(nameof(Output.Name), Use = nameof(Format)), MapConstant(nameof(Output.Name), \"fixed\")]")]
    [InlineData("[MapProperty(nameof(Output.Name), Use = nameof(Format)), MapProperty(nameof(Input.Name), nameof(Output.Name))]")]
    [InlineData("[MapProperty(nameof(Output.Name), Use = nameof(Format)), MapProperty(nameof(Output.Name), Use = nameof(Format))]")]
    public void Invalid_and_conflicting_target_only_bindings_report_configuration_errors(string attributes)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record Input(string Name);
            public record Output(string Name);
            [Mapper] public static partial class M {
                {{attributes}}
                public static partial Output Map(Input source);
                private static string Format(Input source) => source.Name;
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP006");
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Whole_source_bindings_retain_nested_target_type_validation()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(string Name);
            public record Output(string Name);
            [Mapper] public static partial class M {
                [MapProperty("Name.Missing", Use = nameof(Format))]
                public static partial Output Map(Input source);
                private static string Format(Input source) => source.Name;
            }
            """);

        Assert.Equal("EMAP009", Assert.Single(driver.GetRunResult().Diagnostics).Id);
    }

    [Theory]
    [InlineData("Missing", "")]
    [InlineData("Format", "private static string Format(string value) => value;")]
    [InlineData("Format", "private static int Format(Input source) => 7;")]
    [InlineData("Format", "private static string? Format(Input source) => null;")]
    [InlineData("Format", "private string Format(Input source) => source.Name;")]
    [InlineData("Format", "private static string Format<T>(Input source) => source.Name;")]
    [InlineData("Format", "private static string Format(ref Input source) => source.Name;")]
    [InlineData("Format", "private static dynamic Format(Input source) => source.Name;")]
    [InlineData("Format", "private static string Format(dynamic source) => source.Name;")]
    [InlineData("Format", "private static System.Threading.Tasks.Task<string> Format(Input source) => System.Threading.Tasks.Task.FromResult(source.Name);")]
    public void Invalid_whole_source_helpers_report_the_helper_target_and_mapping(string name, string helper)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record Input(string Name);
            public record Output(string Name);
            [Mapper] public partial class M {
                [MapProperty(nameof(Output.Name), Use = "{{name}}")]
                public static partial Output Map(Input source);
                {{helper}}
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP006", error.Id);
        Assert.Contains(name, error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Name", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Map", error.GetMessage(), StringComparison.Ordinal);
        Assert.Equal("TestInput.cs", error.Location.GetLineSpan().Path);
    }

    [Fact]
    public void Whole_source_helpers_are_excluded_from_automatic_converter_discovery()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(Input Child);
            public record Output(string Name, string Child);
            [Mapper] public static partial class M {
                [MapProperty(nameof(Output.Name), Use = nameof(Format))]
                public static partial Output Map(Input source);
                private static string Format(Input source) => "formatted";
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP005");
    }

    [Fact]
    public void Whole_source_bindings_to_generated_methods_still_detect_cycles()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public record Output(Output Child);
            [Mapper] public static partial class M {
                [MapProperty(nameof(Output.Child), Use = nameof(Map))]
                public static partial Output Map(Input source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP008");
    }

    [Fact]
    public void Whole_source_bindings_can_select_a_generated_child_mapping()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(string Name);
            public record Child(string Name);
            public record Output(Child Child);
            [Mapper] public static partial class M {
                [MapProperty(nameof(Output.Child), Use = nameof(MapChild))]
                public static partial Output Map(Input source);
                private static partial Child MapChild(Input source);
            }
            public static class Scenario { public static bool Verify() => M.Map(new Input("Ada")).Child.Name == "Ada"; }
            """);
    }

    [Fact]
    public void Switching_from_a_member_binding_to_a_whole_source_binding_requires_model_approval()
    {
        const string source = """
            using XBullet.EasyMapping;
            public record Input(string Name);
            public record Output(string Name);
            [Mapper(RequireModelApproval = true)] public static partial class M {
                [MapProperty(nameof(Input.Name), nameof(Output.Name), Use = nameof(Format))]
                public static partial Output Map(Input source);
                private static string Format(string name) => name;
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
        var (approved, _) = GeneratorTestCompilation.Generate(source, approval);
        Assert.Empty(approved.GetRunResult().Diagnostics);

        var (changed, _) = GeneratorTestCompilation.Generate(source.Replace(
            "nameof(Input.Name), nameof(Output.Name)", "nameof(Output.Name)", StringComparison.Ordinal), approval);

        Assert.Equal("EMAP011", Assert.Single(changed.GetRunResult().Diagnostics).Id);
    }

    private sealed class MemoryApproval(string path, string content) : AdditionalText
    {
        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
    }
}
