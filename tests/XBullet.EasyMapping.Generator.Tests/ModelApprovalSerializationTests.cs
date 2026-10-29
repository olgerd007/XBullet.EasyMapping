using Microsoft.CodeAnalysis;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class ModelApprovalSerializationTests
{
    [Theory]
    [InlineData("RequireModelApproval = false", "public static partial OrderDto Map(OrderInput source);", false)]
    [InlineData("RequireModelApproval = true", "", false)]
    [InlineData("RequireModelApproval = true, Coverage = MappingCoverage.Target", "public static partial OrderDto Map(OrderInput source);", true)]
    public void Approval_is_emitted_only_for_opted_in_mappers_with_generated_methods(string options, string method, bool expected)
    {
        var (driver, compilation) = GeneratorTestCompilation.Generate($$"""
            using System.ComponentModel;
            using XBullet.EasyMapping;
            public record OrderInput(int Id);
            public record OrderDto(int Id);
            [Description("Order mappings")]
            [Mapper({{options}})] public static partial class OrderMapper { {{method}} }
            """);

        Assert.Equal(expected ? 1 : 0, driver.GetRunResult().Diagnostics.Length);
        Assert.Empty(compilation.GetDiagnostics().Where(error => error.Severity >= DiagnosticSeverity.Warning));
        if (expected) { Assert.Contains("coverage Target", Snapshot(driver), StringComparison.Ordinal); }
    }

    [Fact]
    public void Malformed_approval_option_does_not_mask_the_compilers_configuration_error()
    {
        var (driver, compilation) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record OrderInput(int Id);
            public record OrderDto(int Id);
            [Mapper(RequireModelApproval = null)] public static partial class OrderMapper {
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Empty(driver.GetRunResult().Diagnostics);
        Assert.Contains(compilation.GetDiagnostics(), error => error.Id == "CS0037");
        Assert.DoesNotContain(Assert.Single(driver.GetRunResult().Results).GeneratedSources, source =>
            source.HintName.EndsWith(".ModelApproval.g.cs", StringComparison.Ordinal));
    }

    [Fact]
    public void Approval_serializes_arrays_types_literals_interfaces_indexers_and_constructor_metadata()
    {
        var (driver, compilation) = GeneratorTestCompilation.Generate("""
            using System;
            using System.Runtime.InteropServices;
            using XBullet.EasyMapping;
            [AttributeUsage(AttributeTargets.All)]
            public sealed class OrderNoteAttribute : Attribute {
                public OrderNoteAttribute(object?[]? values, Type? type, char marker, bool enabled) {}
                public bool Reviewed { get; set; }
            }
            public interface ICustomer { string Name { get; } }
            [OrderNote(new object?[] { 7, "web", null }, typeof(ICustomer), '\n', true, Reviewed = false)]
            public class OrderPayload<T> {
                [OrderNote(null, null, 'x', false)] public const string? Empty = null;
                public const char Marker = '\t';
                public const bool Enabled = true;
                public const bool Disabled = false;
                public static int Shared;
                private int _hidden;
                public T Value { get; set; } = default!;
                public ICustomer? Customer { get; set; }
                public int this[int index] { get => index + _hidden; set { _hidden = value; } }
                public OrderPayload() {}
                public OrderPayload([Optional] int version) {}
                public OrderPayload(params string[] labels) {}
            }
            public record OrderInput(int Id, OrderPayload<string?[]> Metadata);
            public record OrderDto(int Id);
            [Mapper(Coverage = MappingCoverage.Both, RequireModelApproval = true)] public partial class OrderMapper {
                [MapIgnoreSource(nameof(OrderInput.Metadata))]
                public partial OrderDto Map(OrderInput source);
                [MapIgnoreSource(nameof(OrderInput.Metadata))]
                public partial void Update(OrderInput source, MutableOrderDto target);
            }
            public class MutableOrderDto { public int Id { get; set; } }
            """);

        Assert.Equal("EMAP011", Assert.Single(driver.GetRunResult().Diagnostics).Id);
        Assert.Empty(compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning));
        var snapshot = Snapshot(driver);
        Assert.Contains("mapping instance", snapshot, StringComparison.Ordinal);
        Assert.Contains("void Update", snapshot, StringComparison.Ordinal);
        Assert.Contains("coverage Both", snapshot, StringComparison.Ordinal);
        Assert.Contains("[7, \"web\", null], typeof(global::ICustomer), '\\n', true, Reviewed=false", snapshot, StringComparison.Ordinal);
        Assert.Contains("(null, null, 'x', false)", snapshot, StringComparison.Ordinal);
        Assert.Contains("constant=null", snapshot, StringComparison.Ordinal);
        Assert.Contains("constant='\\t'", snapshot, StringComparison.Ordinal);
        Assert.Contains("constant=true", snapshot, StringComparison.Ordinal);
        Assert.Contains("constant=false", snapshot, StringComparison.Ordinal);
        Assert.Contains("kind=Interface", snapshot, StringComparison.Ordinal);
        Assert.Contains("index=(None int index", snapshot, StringComparison.Ordinal);
        Assert.Contains("version nullable=NotAnnotated optional", snapshot, StringComparison.Ordinal);
        Assert.Contains("labels nullable=NotAnnotated params", snapshot, StringComparison.Ordinal);
        Assert.Contains("arguments=NotAnnotated<NotAnnotated[Annotated]>", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("field Shared", snapshot, StringComparison.Ordinal);
        Assert.DoesNotContain("field _hidden", snapshot, StringComparison.Ordinal);
    }

    [Fact]
    public void Approval_serializes_array_and_type_constants_with_nested_target_paths()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using System;
            using XBullet.EasyMapping;
            public record OrderInput();
            public record CustomerDto(int[] Numbers, Type Type, string[]? Labels);
            public record OrderDto(CustomerDto Customer);
            [Mapper(RequireModelApproval = true)] public static partial class OrderMapper {
                [MapConstant("Customer.Numbers", new int[] { 7, 8 })]
                [MapConstant("Customer.Type", typeof(OrderInput))]
                [MapConstant("Customer.Labels", null)]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Equal("EMAP011", Assert.Single(driver.GetRunResult().Diagnostics).Id);
        var snapshot = Snapshot(driver);
        Assert.Contains("\"Customer.Numbers\", [7, 8]", snapshot, StringComparison.Ordinal);
        Assert.Contains("\"Customer.Type\", typeof(global::OrderInput)", snapshot, StringComparison.Ordinal);
        Assert.Contains("\"Customer.Labels\", null", snapshot, StringComparison.Ordinal);
    }

    private static string Snapshot(GeneratorDriver driver) => Assert.Single(
        Assert.Single(driver.GetRunResult().Results).GeneratedSources.Where(source =>
            source.HintName.EndsWith(".ModelApproval.g.cs", StringComparison.Ordinal))).SourceText.ToString();
}
