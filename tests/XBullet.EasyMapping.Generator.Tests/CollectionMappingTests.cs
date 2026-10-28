using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class CollectionMappingTests
{
    [Theory]
    [InlineData("List<int>", "new List<int> { int.MinValue, 7 }")]
    [InlineData("int[]", "new[] { int.MinValue, 7 }")]
    [InlineData("IList<int>", "new List<int> { int.MinValue, 7 }")]
    [InlineData("IReadOnlyList<int>", "new List<int> { int.MinValue, 7 }")]
    [InlineData("ICollection<int>", "new List<int> { int.MinValue, 7 }")]
    [InlineData("IReadOnlyCollection<int>", "new List<int> { int.MinValue, 7 }")]
    public void Counted_collections_preserve_nulls_values_and_single_getter_evaluation(string sourceType, string values)
    {
        var emptyValues = sourceType == "int[]" ? "System.Array.Empty<int>()" : "new List<int>()";
        GeneratorTestCompilation.VerifyRuntime($$"""
            using System.Collections.Generic;
            using XBullet.EasyMapping;
            public sealed class Input {
                private readonly {{sourceType}}? _values;
                public Input({{sourceType}}? values) { _values = values; }
                public int Reads { get; private set; }
                public {{sourceType}}? Values { get { Reads++; return _values; } }
            }
            public record Output(List<long>? Values);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapIgnoreSource(nameof(Input.Reads))]
                public static partial Output Map(Input source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var input = new Input({{values}});
                    var result = M.Map(input);
                    var empty = new Input({{emptyValues}});
                    var missing = new Input(null);
                    return result.Values!.Count == 2 && result.Values[0] == int.MinValue && result.Values[1] == 7 &&
                        input.Reads == 1 && M.Map(empty).Values!.Count == 0 && empty.Reads == 1 &&
                        M.Map(missing).Values == null && missing.Reads == 1;
                }
            }
            """);
    }

    [Theory]
    [InlineData("List<string>", "List<string>", "new List<string> { \"a\", \"b\" }")]
    [InlineData("string[]", "string[]", "new[] { \"a\", \"b\" }")]
    [InlineData("List<string>", "string[]", "new List<string> { \"a\", \"b\" }")]
    [InlineData("string[]", "IEnumerable<string>", "new[] { \"a\", \"b\" }")]
    public void Identity_elements_still_materialize_an_independent_container(string sourceType, string targetType, string values)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using System.Collections.Generic;
            using System.Linq;
            using XBullet.EasyMapping;
            public record Input({{sourceType}} Values);
            public record Output({{targetType}} Values);
            [Mapper] public static partial class M { public static partial Output Map(Input source); }
            public static class Scenario {
                public static bool Verify() {
                    var values = {{values}};
                    var result = M.Map(new Input(values));
                    values[0] = "changed";
                    return !object.ReferenceEquals(values, result.Values) &&
                        result.Values.SequenceEqual(new[] { "a", "b" });
                }
            }
            """);
    }

    [Fact]
    public void Instance_element_converters_keep_precedence_and_handle_null_elements_during_create_and_update()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Collections.Generic;
            using System.Linq;
            using XBullet.EasyMapping;
            public record Input(List<string?> Values);
            public sealed class Output { public List<string> Values { get; set; } = new(); }
            [Mapper] public partial class M {
                private readonly string _prefix;
                public M(string prefix) { _prefix = prefix; }
                public partial Output Map(Input source);
                public partial void Update(Input source, Output target);
                private string Convert(string? value) => _prefix + (value ?? "missing");
            }
            public static class Scenario {
                public static bool Verify() {
                    var mapper = new M("converted:");
                    var input = new Input(new List<string?> { "a", null });
                    var created = mapper.Map(input);
                    var target = new Output();
                    var original = target.Values;
                    mapper.Update(input, target);
                    var expected = new[] { "converted:a", "converted:missing" };
                    return created.Values.SequenceEqual(expected) && target.Values.SequenceEqual(expected) &&
                        !object.ReferenceEquals(original, target.Values) && !object.ReferenceEquals(input.Values, target.Values);
                }
            }
            """);
    }

    [Fact]
    public void Same_type_element_converter_is_not_skipped_by_the_identity_copy()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Collections.Generic;
            using XBullet.EasyMapping;
            public record Input(List<string> Values);
            public record Output(List<string> Values);
            [Mapper] public static partial class M {
                public static partial Output Map(Input source);
                private static string Convert(string source) => "converted:" + source;
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(new List<string> { "a" })).Values[0] == "converted:a";
            }
            """);
    }

    [Fact]
    public void Nested_list_helpers_preserve_null_rows_and_deep_copy_elements()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Collections.Generic;
            using XBullet.EasyMapping;
            public record ChildInput(int Id);
            public record ChildOutput(int Id);
            public record Input(List<List<ChildInput>?>? Rows);
            public record Output(List<List<ChildOutput>?>? Rows);
            [Mapper] public static partial class M {
                public static partial Output Map(Input source);
                private static partial ChildOutput MapChild(ChildInput source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var row = new List<ChildInput> { new ChildInput(7) };
                    var input = new Input(new List<List<ChildInput>?> { row, null, new() });
                    var result = M.Map(input);
                    row.Clear();
                    return result.Rows!.Count == 3 && result.Rows[0]!.Count == 1 && result.Rows[0]![0].Id == 7 &&
                        result.Rows[1] == null && result.Rows[2]!.Count == 0 && M.Map(new Input(null)).Rows == null;
                }
            }
            """);
    }

    [Theory]
    [InlineData("int")]
    [InlineData("long")]
    public void Arbitrary_enumerables_use_their_generic_contract_once_and_are_disposed(string targetElement)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using System;
            using System.Collections;
            using System.Collections.Generic;
            using System.Linq;
            using XBullet.EasyMapping;
            public sealed class SingleUse : IEnumerable<int> {
                public int Enumerations { get; private set; }
                public bool Disposed { get; private set; }
                public int Count => throw new InvalidOperationException("Do not inspect arbitrary Count properties.");
                public IEnumerator<string> GetEnumerator() => throw new InvalidOperationException("Wrong enumeration contract.");
                IEnumerator<int> IEnumerable<int>.GetEnumerator() {
                    if (++Enumerations != 1) { throw new InvalidOperationException("Enumerated twice."); }
                    return Iterate().GetEnumerator();
                }
                IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable<int>)this).GetEnumerator();
                private IEnumerable<int> Iterate() {
                    try { yield return 3; yield return 7; }
                    finally { Disposed = true; }
                }
            }
            public record Input(SingleUse Values);
            public record Output(List<{{targetElement}}> Values);
            [Mapper] public static partial class M { public static partial Output Map(Input source); }
            public static class Scenario {
                public static bool Verify() {
                    var values = new SingleUse();
                    var result = M.Map(new Input(values));
                    return result.Values.SequenceEqual(new {{targetElement}}[] { 3, 7 }) && values.Enumerations == 1 && values.Disposed;
                }
            }
            """);
    }

    [Fact]
    public void Helper_names_do_not_shadow_mapper_methods_or_parameters()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Collections.Generic;
            using XBullet.EasyMapping;
            public record ChildInput(int Id);
            public record ChildOutput(int Id);
            public record Input(List<ChildInput> Values);
            public record Output(List<ChildOutput> Values);
            [Mapper] public static partial class M {
                public static partial Output Map(Input __mapperValue0);
                private static ChildOutput __mapperValue2(ChildInput source) => new(source.Id);
                private static void __mapperValue3() { }
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new Input(new List<ChildInput> { new(7) })).Values[0].Id == 7;
            }
            """);
    }
}
