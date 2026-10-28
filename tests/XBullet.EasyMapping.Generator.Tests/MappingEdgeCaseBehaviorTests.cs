using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class MappingEdgeCaseBehaviorTests
{
    [Fact]
    public void Closed_generic_base_properties_keep_substituted_types_and_nullable_values()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public abstract class Box<T> { public T Value { get; init; } = default!; }
            public sealed class Input : Box<int?> {}
            public sealed class Output : Box<long?> {}
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input { Value = null }).Value == null &&
                    M.Map(new Input { Value = int.MinValue }).Value == int.MinValue;
            }
            """);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(7)]
    public void Nullable_property_getters_are_evaluated_once(int? value)
    {
        var literal = value?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null";
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            public sealed class Input {
                public int AccessCount { get; private set; }
                public int? Value { get { AccessCount++; return {{literal}}; } }
            }
            public record Output(long? Value);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapIgnoreSource(nameof(Input.AccessCount))]
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var source = new Input();
                    var result = M.Map(source);
                    return result.Value == {{literal}} && source.AccessCount == 1;
                }
            }
            """);
    }

    [Fact]
    public void Sets_required_members_prevents_duplicate_initialization_of_constructor_assigned_members()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Diagnostics.CodeAnalysis;
            using XBullet.EasyMapping;
            public record Input(string Name);
            public sealed class Output {
                private string _name = "";
                [SetsRequiredMembers] public Output(string name) { Name = name; }
                public int SetterCalls { get; private set; }
                public required string Name { get => _name; init { _name = value; SetterCalls++; } }
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapIgnoreTarget(nameof(Output.SetterCalls))]
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var result = M.Map(new Input("value"));
                    return result.Name == "value" && result.SetterCalls == 1;
                }
            }
            """);
    }

    [Fact]
    public void Handwritten_null_handling_converters_apply_to_collection_elements()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Collections.Generic;
            using System.Globalization;
            using XBullet.EasyMapping;
            public record Input(List<int?> Numbers);
            public record Output(string[] Numbers);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                public static partial Output Map(Input source);
                private static string Format(int? value) => value?.ToString(CultureInfo.InvariantCulture) ?? "missing";
            }
            public static class Scenario {
                public static bool Verify() {
                    var result = M.Map(new Input(new List<int?> { null, 5 }));
                    return result.Numbers.Length == 2 && result.Numbers[0] == "missing" && result.Numbers[1] == "5";
                }
            }
            """);
    }

    [Fact]
    public void Explicit_same_type_mapping_copies_mutable_members_instead_of_sharing_them()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Collections.Generic;
            using XBullet.EasyMapping;
            public sealed class Details {
                public string Name { get; set; } = "";
                public List<string> Tags { get; set; } = new();
            }
            public record Input(Details Details);
            public record Output(Details Details);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                public static partial Output Map(Input source);
                private static partial Details CopyDetails(Details source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var details = new Details { Name = "before", Tags = new List<string> { "tag" } };
                    var result = M.Map(new Input(details));
                    details.Name = "after";
                    details.Tags.Add("late");
                    return !object.ReferenceEquals(details, result.Details) &&
                        !object.ReferenceEquals(details.Tags, result.Details.Tags) &&
                        result.Details.Name == "before" && result.Details.Tags.Count == 1 && result.Details.Tags[0] == "tag";
                }
            }
            """);
    }

    [Fact]
    public void Generated_locals_and_lambdas_do_not_collide_with_a_mapper_parameter()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Collections.Generic;
            using XBullet.EasyMapping;
            public record Input(List<int?>? Numbers);
            public record Output(long?[]? Numbers);
            [Mapper] public static partial class M {
                public static partial Output Map(Input __mapperValue0);
            }
            public static class Scenario {
                public static bool Verify() {
                    var result = M.Map(new Input(new List<int?> { null, int.MinValue }));
                    return result.Numbers!.Length == 2 && result.Numbers[0] == null &&
                        result.Numbers[1] == int.MinValue && M.Map(new Input(null)).Numbers == null;
                }
            }
            """);
    }

    [Fact]
    public void Omitted_optional_enum_and_null_constructor_arguments_keep_their_defaults()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public enum State { Draft, Ready }
            public record Input(int Id);
            public sealed class Output {
                public Output(int id, State state = State.Ready, string? label = null) { Id = id; State = state; Label = label; }
                public int Id { get; }
                public State State { get; }
                public string? Label { get; }
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapIgnoreTarget(nameof(Output.State)), MapIgnoreTarget(nameof(Output.Label))]
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var result = M.Map(new Input(7));
                    return result.Id == 7 && result.State == State.Ready && result.Label == null;
                }
            }
            """);
    }

    [Fact]
    public void Readonly_record_struct_targets_receive_required_init_members()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int Id, string Name);
            public readonly record struct Output {
                public int Id { get; init; }
                public required string Name { get; init; }
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var result = M.Map(new Input(7, "value"));
                    return result.Id == 7 && result.Name == "value";
                }
            }
            """);
    }
}
