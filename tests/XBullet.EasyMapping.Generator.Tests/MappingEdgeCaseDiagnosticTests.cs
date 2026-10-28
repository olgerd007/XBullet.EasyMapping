using Microsoft.CodeAnalysis;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class MappingEdgeCaseDiagnosticTests
{
    [Theory]
    [InlineData("List<string?>", "List<string>", "nullable source")]
    [InlineData("List<int?>", "long[]", "nullable source")]
    [InlineData("Dictionary<string, int>", "Dictionary<string, int>", "declare a mapping")]
    [InlineData("int[,]", "int[,]", "declare a mapping")]
    [InlineData("List<int>", "HashSet<int>", "declare a mapping")]
    [InlineData("IQueryable<int>", "List<int>", "IQueryable projections")]
    [InlineData("List<int>", "IQueryable<int>", "IQueryable projections")]
    [InlineData("object", "string", "declare a mapping")]
    [InlineData("System.DateTimeOffset", "System.DateTime", "declare a mapping")]
    [InlineData("InputStatus", "OutputStatus", "declare a mapping")]
    [InlineData("List<int>", "IReadOnlyList<string>", "declare a mapping")]
    [InlineData("IEnumerable<int>", "List<byte>", "declare a mapping")]
    [InlineData("string", "char[]", "declare a mapping")]
    public void Unsupported_or_lossy_member_conversions_are_source_located_errors(string sourceType, string targetType, string reason)
    {
        var (driver, compilation) = GeneratorTestCompilation.Generate($$"""
            using System.Collections.Generic;
            using System.Linq;
            using XBullet.EasyMapping;
            public enum InputStatus { Ready }
            public enum OutputStatus { Ready }
            public record Input({{sourceType}} Value);
            public record Output({{targetType}} Value);
            [Mapper] public static partial class M { public static partial Output Map(Input source); }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP005");
        Assert.Equal(DiagnosticSeverity.Error, error.Severity);
        Assert.Equal("TestInput.cs", error.Location.GetLineSpan().Path);
        Assert.Contains(reason, error.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
        Assert.DoesNotContain(compilation.GetDiagnostics(), diagnostic =>
            diagnostic.Severity == DiagnosticSeverity.Error && diagnostic.Id != "CS8795");
    }

    [Theory]
    [InlineData("public interface Input { int Id { get; } } public record Output(int Id);", "EMAP002")]
    [InlineData("public record Input(int Id); public abstract record Output(int Id);", "EMAP002")]
    [InlineData("public record Input(int Id); public class Output { private Output() {} public int Id {get;set;} }", "EMAP004")]
    [InlineData("public record Input(int Id); public class Output { public int Id {get;private set;} }", "EMAP009")]
    [InlineData("public record Input(int Item); public class Output { public int this[int index] { get => index; set {} } }", "EMAP009")]
    public void Unsupported_roots_constructors_and_setters_fail_before_emitting_an_implementation(string models, string diagnosticId)
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            [Mapper] public static partial class M { public static partial Output Map(Input source); }
            """ + models);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == diagnosticId);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void An_enumerable_with_multiple_element_types_cannot_silently_choose_one()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using System.Collections;
            using System.Collections.Generic;
            using System.Linq;
            using XBullet.EasyMapping;
            public sealed class Values : IEnumerable<int>, IEnumerable<string> {
                IEnumerator<int> IEnumerable<int>.GetEnumerator() => Enumerable.Empty<int>().GetEnumerator();
                IEnumerator<string> IEnumerable<string>.GetEnumerator() => Enumerable.Empty<string>().GetEnumerator();
                IEnumerator IEnumerable.GetEnumerator() => Enumerable.Empty<int>().GetEnumerator();
            }
            public record Input(Values Items);
            public record Output(List<int> Items);
            [Mapper] public static partial class M { public static partial Output Map(Input source); }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP005");
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Indirect_cycles_through_nullable_nested_models_are_rejected()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(ChildInput? Child);
            public record ChildInput(Input? Parent);
            public record Output(ChildDto? Child);
            public record ChildDto(Output? Parent);
            [Mapper] public static partial class M {
                public static partial Output Map(Input source);
                private static partial ChildDto? MapChild(ChildInput? source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP008");
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Static_mapping_does_not_select_an_instance_converter()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Input(int Value);
            public record Output(string Value);
            [Mapper] public partial class M {
                public static partial Output Map(Input source);
                private string Convert(int value) => value.ToString();
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP005");
    }

    [Fact]
    public void User_defined_implicit_operators_require_an_explicit_converter()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public readonly record struct Code(int Value) {
                public static implicit operator int(Code source) => source.Value;
            }
            public record Input(Code Value);
            public record Output(int Value);
            [Mapper] public static partial class M { public static partial Output Map(Input source); }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP005");
    }
}
