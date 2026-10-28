using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class MappingBehaviorTests
{
    [Fact]
    public void Nullable_nested_methods_preserve_nulls_without_requiring_a_non_nullable_overload()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record ChildInput(int Id);
            public record ChildDto(int Id);
            public record Input(ChildInput? Child);
            public record Output(ChildDto? Child);
            [Mapper] public static partial class TestMapper {
                public static partial Output Map(Input source);
                private static partial ChildDto? MapChild(ChildInput? source);
            }
            public static class Scenario {
                public static bool Verify() => TestMapper.Map(new Input(null)).Child == null &&
                    TestMapper.Map(new Input(new ChildInput(7))).Child!.Id == 7;
            }
            """);
    }

    [Fact]
    public void Private_void_partial_methods_can_omit_the_access_modifier()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public class Output { public int Id { get; set; } }
            [Mapper] public static partial class TestMapper {
                static partial void Update(Input source, Output target);
                public static Output Map(Input source) { var target = new Output(); Update(source, target); return target; }
            }
            public static class Scenario { public static bool Verify() => TestMapper.Map(new Input(7)).Id == 7; }
            """);
    }

    [Fact]
    public void Maps_required_init_members_and_constructor_parameters()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int Id, string Name);
            public sealed class Output {
                public Output(int id) { Id = id; }
                public int Id { get; }
                public required string DisplayName { get; init; }
            }
            [Mapper] public static partial class TestMapper {
                [MapProperty(nameof(Input.Name), nameof(Output.DisplayName))]
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var result = TestMapper.Map(new Input(7, "value"));
                    return result.Id == 7 && result.DisplayName == "value";
                }
            }
            """);
    }

    [Fact]
    public void Supports_instance_mappers_and_exact_handwritten_nullable_converters()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int? Number);
            public record Output(string Number);
            [Mapper] public partial class TestMapper {
                private readonly string _fallback;
                public TestMapper(string fallback) { _fallback = fallback; }
                public partial Output Map(Input input);
                private string Format(int? value) => value?.ToString() ?? _fallback;
            }
            public static class Scenario {
                public static bool Verify() => new TestMapper("missing").Map(new Input(null)).Number == "missing";
            }
            """);
    }

    [Fact]
    public void Collections_materialize_and_widen_nullable_numeric_elements()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Collections.Generic;
            using XBullet.EasyMapping;
            public record Input(int[] Numbers, List<int?> Optional);
            public record Output(IReadOnlyList<long?> Numbers, long?[] Optional);
            [Mapper] public static partial class TestMapper {
                public static partial Output Map(Input input);
            }
            public static class Scenario {
                public static bool Verify() {
                    var input = new Input(new[] { 2, 3 }, new List<int?> { null, 5 });
                    var result = TestMapper.Map(input);
                    input.Numbers[0] = 99;
                    return result.Numbers[0] == 2 && result.Optional[0] == null && result.Optional[1] == 5;
                }
            }
            """);
    }

    [Fact]
    public void Inherited_properties_and_escaped_names_compile()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public class InputBase { public int Id { get; init; } }
            public sealed class Input : InputBase { public string @event { get; init; } = "value"; }
            public class OutputBase { public int Id { get; set; } }
            public sealed class Output : OutputBase { public string @event { get; set; } = ""; }
            [Mapper] public static partial class TestMapper { public static partial Output Map(Input @event); }
            public static class Scenario {
                public static bool Verify() {
                    var output = TestMapper.Map(new Input { Id = 7 });
                    return output.Id == 7 && output.@event == "value";
                }
            }
            """);
    }

    [Fact]
    public void Optional_constructor_arguments_keep_their_declared_defaults()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int Id);
            public class Output {
                public Output(int id, string fallback = "default") { Id = id; }
                public int Id { get; }
            }
            [Mapper] public static partial class TestMapper { public static partial Output Map(Input source); }
            public static class Scenario { public static bool Verify() => TestMapper.Map(new Input(7)).Id == 7; }
            """);
    }

    [Fact]
    public void Non_nullable_root_and_update_target_are_guarded()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System;
            using XBullet.EasyMapping;
            public record Input(int Id);
            public class Output { public int Id { get; set; } }
            [Mapper] public static partial class TestMapper {
                public static partial Output Map(Input source);
                public static partial void Update(Input source, Output target);
            }
            public static class Scenario {
                public static bool Verify() {
                    try { TestMapper.Map(null!); return false; }
                    catch (ArgumentNullException error) { if (error.ParamName != "source") return false; }
                    try { TestMapper.Update(new Input(7), null!); return false; }
                    catch (ArgumentNullException error) { return error.ParamName == "target"; }
                }
            }
            """);
    }

    [Fact]
    public void Nullable_struct_roots_unwrap_without_losing_values()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public readonly record struct Input(int Id);
            public record Output(int Id);
            [Mapper] public static partial class TestMapper { public static partial Output? Map(Input? source); }
            public static class Scenario {
                public static bool Verify() => TestMapper.Map(null) == null && TestMapper.Map(new Input(7))!.Id == 7;
            }
            """);
    }

    [Fact]
    public void Constructor_assigned_required_properties_are_also_initialized_when_necessary()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(string Name);
            public class Output {
                public Output(string name) { Name = name; }
                public required string Name { get; init; }
            }
            [Mapper] public static partial class TestMapper { public static partial Output Map(Input source); }
            public static class Scenario { public static bool Verify() => TestMapper.Map(new Input("value")).Name == "value"; }
            """);
    }
}
