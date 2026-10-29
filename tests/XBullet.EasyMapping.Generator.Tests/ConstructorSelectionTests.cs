using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class ConstructorSelectionTests
{
    [Theory]
    [InlineData("public int Id { get; }")]
    [InlineData("public int Id { get; private set; }")]
    [InlineData("public int Id { get; protected init; }")]
    public void Selects_a_constructor_that_maps_a_property_without_a_public_setter(string property)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            public record Input(int Id, string Name);
            public class Output {
                public Output() { }
                public Output(int id) { Id = id; }
                {{property}}
                public required string Name { get; init; }
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var result = M.Map(new Input(7, "Ada"));
                    return result.Id == 7 && result.Name == "Ada";
                }
            }
            """);
    }

    [Fact]
    public void Constructor_selection_accounts_for_inherited_properties_and_renames()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int SourceId);
            public class OutputBase {
                protected OutputBase(int id) { Id = id; }
                public int Id { get; }
            }
            public class Output : OutputBase {
                public Output() : base(-1) { }
                public Output(int id) : base(id) { }
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty(nameof(Input.SourceId), nameof(Output.Id))]
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(7)).Id == 7;
            }
            """);
    }

    [Fact]
    public void Selects_the_only_constructor_that_covers_all_read_only_properties()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int Id, string Name);
            public class Output {
                public Output() { }
                public Output(int id) { Id = id; }
                public Output(int id, string name, int unused = 42) { Id = id; Name = name; }
                public int Id { get; }
                public string Name { get; } = "default";
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var result = M.Map(new Input(7, "Ada"));
                    return result.Id == 7 && result.Name == "Ada";
                }
            }
            """);
    }

    [Theory]
    [InlineData("set")]
    [InlineData("init")]
    public void Prefers_parameterless_construction_when_properties_are_writable(string setter)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public class Output {
                public Output() { }
                public Output(int id) { throw new System.InvalidOperationException(); }
                public int Id { get; {{setter}}; }
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(7)).Id == 7;
            }
            """);
    }

    [Fact]
    public void Ignored_read_only_properties_do_not_prevent_parameterless_construction()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public class Output {
                public Output() { }
                public Output(int id = 42) { throw new System.InvalidOperationException(); }
                public int Id { get; } = -1;
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapIgnoreSource(nameof(Input.Id)), MapIgnoreTarget(nameof(Output.Id))]
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(7)).Id == -1;
            }
            """);
    }

    [Fact]
    public void Competing_constructors_that_cover_read_only_properties_report_ambiguity()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int Id, string Name);
            public class Output {
                public Output() { }
                public Output(int id) { Id = id; }
                public Output(int id, string name) { Id = id; }
                public int Id { get; }
            }
            [Mapper] public static partial class M { public static partial Output Map(Input source); }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP004", error.Id);
        Assert.Contains("multiple mappable constructors", error.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Read_only_properties_without_a_matching_constructor_keep_the_target_diagnostic()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public class Output { public int Id { get; } }
            [Mapper] public static partial class M { public static partial Output Map(Input source); }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP009", error.Id);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }
}
