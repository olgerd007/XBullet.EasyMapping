using Microsoft.CodeAnalysis;
using XBullet.EasyMapping.Generator;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class ArrayConstantTests
{
    [Fact]
    public void Roslyn_null_array_constants_emit_a_typed_default_without_an_allocation_local()
    {
        var (_, compilation) = GeneratorTestCompilation.Generate("""
            using System;
            public sealed class OrderLabelsAttribute : Attribute { public OrderLabelsAttribute(string[]? labels) {} }
            public record OrderInput();
            public record OrderDto(string[]? Labels);
            public static class OrderMapper {
                [OrderLabels(null)] public static OrderDto Map(OrderInput source) => new(null);
            }
            """);
        var mapper = compilation.GetTypeByMetadataName("OrderMapper")!;
        var method = Assert.Single(mapper.GetMembers("Map").OfType<IMethodSymbol>());
        var constant = Assert.Single(Assert.Single(method.GetAttributes()).ConstructorArguments);
        Assert.Equal(TypedConstantKind.Array, constant.Kind);
        Assert.True(constant.IsNull);
        void Report(DiagnosticDescriptor descriptor, string[] arguments) => Assert.Fail(string.Join("; ", arguments));
        var conversions = new ConversionBuilder(compilation, method, [], [], Report);
        var coverage = new SourceCoverageValidator([], method, false, Report);
        var values = new MappingValueBuilder(compilation, conversions, coverage, "source", "Map", Report);
        var target = Assert.Single(compilation.GetTypeByMetadataName("OrderDto")!.GetMembers("Labels").OfType<IPropertySymbol>());

        var expression = values.Build(PropertyMapping.ForConstant(constant), target.Type, "Labels");

        Assert.Equal("default(string[]?)", expression);
        Assert.Equal("", values.Declarations.ToString());
    }

    [Fact]
    public void Required_constructor_and_initializer_share_one_nested_array_value()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput();
            public class CustomerDto {
                private readonly int[] _original;
                public CustomerDto(int[] numbers) => _original = numbers;
                public required int[] Numbers { get; init; }
                public bool SameValue() => object.ReferenceEquals(_original, Numbers);
            }
            public record OrderDto(CustomerDto Customer);
            [Mapper] public static partial class OrderMapper {
                [MapConstant("Customer.Numbers", new int[] { 7, 8 })]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var first = OrderMapper.Map(new OrderInput());
                    var second = OrderMapper.Map(new OrderInput());
                    return first.Customer.SameValue() && second.Customer.SameValue() &&
                        !object.ReferenceEquals(first.Customer.Numbers, second.Customer.Numbers);
                }
            }
            """);
    }

    [Fact]
    public void Array_constants_are_fresh_for_each_target_and_mapping_call()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput();
            public record OrderDto(int[] OrderNumbers, int[] BackupNumbers);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapConstant(nameof(OrderDto.OrderNumbers), new int[] { 7, 8 })]
                [MapConstant(nameof(OrderDto.BackupNumbers), new int[] { 7, 8 })]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var first = OrderMapper.Map(new OrderInput());
                    first.OrderNumbers[0] = 99;
                    var second = OrderMapper.Map(new OrderInput());
                    return first.BackupNumbers[0] == 7 && second.OrderNumbers[0] == 7 &&
                        !object.ReferenceEquals(first.OrderNumbers, second.OrderNumbers) &&
                        !object.ReferenceEquals(first.OrderNumbers, first.BackupNumbers);
                }
            }
            """);
    }

    [Theory]
    [InlineData("string?[]", "new string?[] { \"orders\", null }", "result.Values.Length == 2 && result.Values[0] == \"orders\" && result.Values[1] is null")]
    [InlineData("OrderStatus[]", "new OrderStatus[] { OrderStatus.Approved }", "result.Values[0] == OrderStatus.Approved")]
    [InlineData("int[]", "new int[] { }", "result.Values.Length == 0")]
    [InlineData("object?[]", "new object?[] { 7, \"orders\", null }", "object.Equals(result.Values[0], 7) && object.Equals(result.Values[1], \"orders\") && result.Values[2] is null")]
    public void Array_element_values_and_nullable_annotations_are_preserved(string type, string values, string assertion)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            public enum OrderStatus { Pending, Approved }
            public record OrderInput();
            public record OrderDto({{type}} Values);
            [Mapper] public static partial class OrderMapper {
                [MapConstant(nameof(OrderDto.Values), {{values}})]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() { var result = OrderMapper.Map(new OrderInput()); return {{assertion}}; }
            }
            """);
    }

    [Fact]
    public void Null_array_elements_cannot_be_assigned_to_nonnullable_elements()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record OrderInput();
            public record OrderDto(string[] Values);
            [Mapper] public static partial class OrderMapper {
                [MapConstant(nameof(OrderDto.Values), new string?[] { null })]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP006" &&
            error.GetMessage().Contains("element", StringComparison.Ordinal));
    }
}
