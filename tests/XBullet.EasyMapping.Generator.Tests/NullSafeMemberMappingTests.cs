using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class NullSafeMemberMappingTests
{
    [Theory]
    [InlineData("int", "int", "7")]
    [InlineData("int", "long", "7L")]
    [InlineData("decimal", "decimal", "7m")]
    [InlineData("bool", "bool", "true")]
    [InlineData("System.Guid", "System.Guid", "System.Guid.NewGuid()")]
    [InlineData("System.DateTime", "System.DateTime", "new System.DateTime(2024, 1, 1)")]
    [InlineData("SampleMode", "SampleMode", "SampleMode.Enabled")]
    public void Missing_value_members_use_target_defaults_for_creation_and_updates(string sourceType, string targetType, string value)
    {
        var sourceValue = sourceType == "int" && targetType == "long" ? "7" : value;
        GeneratorTestCompilation.VerifyRuntime($$"""
            using System.Collections.Generic;
            using XBullet.EasyMapping;
            public enum SampleMode { Unknown, Enabled }
            public record Input({{sourceType}}? Value);
            public class Output { public {{targetType}} Value { get; set; } }
            [Mapper(NullSafeMemberMapping = true)] public static partial class M {
                public static partial Output Map(Input source);
                public static partial void Update(Input source, Output target);
            }
            public static class Scenario {
                public static bool Verify() {
                    {{sourceType}} supplied = {{sourceValue}};
                    var target = M.Map(new Input(supplied));
                    if (!EqualityComparer<{{targetType}}>.Default.Equals(target.Value, supplied)) return false;
                    M.Update(new Input(null), target);
                    return EqualityComparer<{{targetType}}>.Default.Equals(target.Value, default) &&
                        EqualityComparer<{{targetType}}>.Default.Equals(M.Map(new Input(null)).Value, default);
                }
            }
            """);
    }

    [Theory]
    [InlineData("enable")]
    [InlineData("disable")]
    public void Nested_references_are_guarded_even_without_nullable_annotations(string context)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            #nullable {{context}}
            public record Child(int Value, string Name);
            public record Parent(Child Child);
            public record Input(Parent Parent);
            #nullable enable
            public class Output {
                public int Value { get; set; } = 99;
                public string Name { get; set; } = "initial";
            }
            [Mapper(Coverage = MappingCoverage.Both, NullSafeMemberMapping = true)] public static partial class M {
                [MapProperty("Parent.Child.Value", nameof(Output.Value))]
                [MapProperty("Parent.Child.Name", nameof(Output.Name))]
                public static partial Output Map(Input source);
                [MapProperty("Parent.Child.Value", nameof(Output.Value))]
                [MapProperty("Parent.Child.Name", nameof(Output.Name))]
                public static partial void Update(Input source, Output target);
            }
            public static class Scenario {
                public static bool Verify() {
                    var missingRootMember = M.Map(new Input(null!));
                    var missingNestedMember = new Input(new Parent(null!));
                    var target = new Output();
                    M.Update(missingNestedMember, target);
                    var present = M.Map(new Input(new Parent(new Child(7, "sample"))));
                    return missingRootMember.Value == 0 && missingRootMember.Name == null &&
                        target.Value == 0 && target.Name == null && present.Value == 7 && present.Name == "sample";
                }
            }
            """);
    }

    [Fact]
    public void Reference_members_assign_null_even_to_non_nullable_target_declarations()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public class Settings {}
            public record Input(Settings? Value, string? Name);
            public class Output {
                public Settings Value { get; set; } = new Settings();
                public string Name { get; set; } = "initial";
            }
            [Mapper(NullSafeMemberMapping = true, AllowReferenceAssignment = true)] public static partial class M {
                public static partial Output Map(Input source);
                public static partial void Update(Input source, Output target);
            }
            public static class Scenario {
                public static bool Verify() {
                    var settings = new Settings();
                    var created = M.Map(new Input(settings, "sample"));
                    if (!object.ReferenceEquals(settings, created.Value)) return false;
                    M.Update(new Input(null, null), created);
                    var missing = M.Map(new Input(null, null));
                    return created.Value == null && created.Name == null && missing.Value == null && missing.Name == null;
                }
            }
            """);
    }

    [Theory]
    [InlineData("List<int>", "List<long>")]
    [InlineData("ICollection<int>", "List<long>")]
    [InlineData("int[]", "long[]")]
    [InlineData("IEnumerable<int>", "IEnumerable<long>")]
    [InlineData("IReadOnlyCollection<int>", "IReadOnlyCollection<long>")]
    public void Missing_collections_become_empty_and_present_collections_are_converted(string sourceType, string targetType)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using System.Collections.Generic;
            using System.Linq;
            using XBullet.EasyMapping;
            public record Input({{sourceType}}? Values);
            public class Output { public {{targetType}}? Values { get; set; } }
            [Mapper(NullSafeMemberMapping = true)] public static partial class M {
                public static partial Output Map(Input source);
                public static partial void Update(Input source, Output target);
            }
            public static class Scenario {
                public static bool Verify() {
                    var first = M.Map(new Input(null));
                    var second = M.Map(new Input(null));
                    var values = new {{(sourceType == "int[]" ? "int[]" : "List<int>")}} { 7, 8 };
                    var target = M.Map(new Input(values));
                    if (!target.Values!.SequenceEqual(new long[] { 7, 8 }) || object.ReferenceEquals(values, target.Values)) return false;
                    M.Update(new Input(null), target);
                    return first.Values is not null && !first.Values.Any() && second.Values is not null &&
                        !second.Values.Any() && target.Values is not null && !target.Values.Any();
                }
            }
            """);
    }

    [Theory]
    [InlineData("")]
    [InlineData("[MapProperty(nameof(Input.Value), nameof(Output.Value), Use = nameof(ConvertValue))]")]
    public void Null_aware_converters_keep_control_of_missing_member_values(string attribute)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            public record Input(string? Value);
            public record Output(string Value);
            [Mapper(NullSafeMemberMapping = true)] public static partial class M {
                {{attribute}}
                public static partial Output Map(Input source);
                private static string ConvertValue(string? value) => value == null ? "missing" : "converted:" + value;
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(null)).Value == "missing" &&
                    M.Map(new Input("sample")).Value == "converted:sample";
            }
            """);
    }

    [Fact]
    public void Non_nullable_named_converters_are_only_called_for_present_values()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Child(string Name);
            public record Input(Child Child);
            public record Output(int Length);
            [Mapper(NullSafeMemberMapping = true)] public static partial class M {
                public static int Calls { get; private set; }
                [MapProperty("Child.Name", nameof(Output.Length), Use = nameof(Length))]
                public static partial Output Map(Input source);
                private static int Length(string value) { Calls++; return value.Length; }
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(null!)).Length == 0 && M.Calls == 0 &&
                    M.Map(new Input(new Child("abc"))).Length == 3 && M.Calls == 1;
            }
            """);
    }

    [Fact]
    public void Null_aware_collection_converter_overrides_the_empty_collection_fallback()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Collections.Generic;
            using XBullet.EasyMapping;
            public record Input(List<int>? Values);
            public record Output(List<int> Values);
            [Mapper(NullSafeMemberMapping = true)] public static partial class M {
                public static partial Output Map(Input source);
                private static List<int> ConvertValues(List<int>? values) => values ?? new List<int> { 99 };
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(null)).Values[0] == 99;
            }
            """);
    }

    [Fact]
    public void Null_aware_converter_can_return_a_non_nullable_value_for_a_nullable_target()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Input(int? Value);
            public record Output(int? Value);
            [Mapper(NullSafeMemberMapping = true)] public static partial class M {
                public static partial Output Map(Input source);
                private static int ConvertValue(int? value) => value ?? 99;
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(null)).Value == 99 && M.Map(new Input(7)).Value == 7;
            }
            """);
    }

    [Fact]
    public void Collection_elements_use_member_fallbacks_without_calling_non_nullable_converters_for_null()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Collections.Generic;
            using System.Linq;
            using XBullet.EasyMapping;
            public record First(string Name);
            public record Second(string Name);
            public record Input(List<int?> Values, List<First?> Items);
            public record Output(List<int> Values, List<Second?> Items);
            [Mapper(NullSafeMemberMapping = true)] public static partial class M {
                public static partial Output Map(Input source);
                private static partial Second MapItem(First source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var target = M.Map(new Input(new List<int?> { 7, null }, new List<First?> { new First("sample"), null }));
                    return target.Values.SequenceEqual(new[] { 7, 0 }) && target.Items[0]?.Name == "sample" && target.Items[1] == null;
                }
            }
            """);
    }

    [Fact]
    public void Missing_paths_support_constructor_init_and_nested_target_members()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Child(int Value);
            public record Input(Child Child);
            public record ConstructorOutput(int Value);
            public class InitOutput { public required int Value { get; init; } }
            public record NestedOutput(Child Child);
            [Mapper(NullSafeMemberMapping = true)] public static partial class M {
                [MapProperty("Child.Value", nameof(ConstructorOutput.Value))]
                public static partial ConstructorOutput MapConstructor(Input source);
                [MapProperty("Child.Value", nameof(InitOutput.Value))]
                public static partial InitOutput MapInit(Input source);
                [MapProperty("Child.Value", "Child.Value")]
                public static partial NestedOutput MapNested(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var missing = new Input(null!);
                    return M.MapConstructor(missing).Value == 0 && M.MapInit(missing).Value == 0 &&
                        M.MapNested(missing).Child.Value == 0 && M.MapNested(new Input(new Child(7))).Child.Value == 7;
                }
            }
            """);
    }

    [Fact]
    public void Null_safe_member_access_evaluates_getters_once()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public sealed class Child {
                public int Reads { get; private set; }
                public int Value { get { Reads++; return 7; } }
            }
            public sealed class Input {
                private readonly Child? _child;
                public Input(Child? child) { _child = child; }
                public int Reads { get; private set; }
                public Child Child { get { Reads++; return _child!; } }
            }
            public record Output(int Value);
            [Mapper(NullSafeMemberMapping = true)] public static partial class M {
                [MapProperty("Child.Value", nameof(Output.Value))]
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var missing = new Input(null);
                    if (M.Map(missing).Value != 0 || missing.Reads != 1) return false;
                    var child = new Child();
                    var present = new Input(child);
                    return M.Map(present).Value == 7 && present.Reads == 1 && child.Reads == 1;
                }
            }
            """);
    }

    [Fact]
    public void Root_source_null_handling_is_unchanged()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System;
            using XBullet.EasyMapping;
            public record Input(int? Value);
            public record Output(int Value);
            [Mapper(NullSafeMemberMapping = true)] public static partial class M {
                public static partial Output Map(Input source);
                public static partial Output? MapNullable(Input? source);
            }
            public static class Scenario {
                public static bool Verify() {
                    if (M.MapNullable(null) != null) return false;
                    try { M.Map(null!); return false; }
                    catch (ArgumentNullException) { return true; }
                }
            }
            """);
    }

    [Theory]
    [InlineData("[Mapper]")]
    [InlineData("[Mapper(NullSafeMemberMapping = false)]")]
    public void Default_and_explicitly_disabled_mappers_keep_strict_member_nullability(string attribute)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record Input(int? Value);
            public record Output(int Value);
            {{attribute}} public static partial class M { public static partial Output Map(Input source); }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP005");
    }

    [Theory]
    [InlineData("int?", "SampleMode")]
    [InlineData("First?", "Second")]
    [InlineData("First", "First")]
    [InlineData("IQueryable<int>", "List<int>")]
    public void Option_does_not_enable_unsupported_conversions_or_reference_assignment(string sourceType, string targetType)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using System.Collections.Generic;
            using System.Linq;
            using XBullet.EasyMapping;
            public enum SampleMode { Unknown, Enabled }
            public class First {}
            public class Second {}
            public record Input({{sourceType}} Value);
            public record Output({{targetType}} Value);
            [Mapper(NullSafeMemberMapping = true)] public static partial class M {
                public static partial Output Map(Input source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP005");
    }
}
