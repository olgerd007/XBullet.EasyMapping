using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class ValueMappingTests
{
    [Fact]
    public void Constants_supply_constructor_arguments_required_members_and_update_properties()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public enum OrderStatus { Pending, Approved }
            public record OrderInput(int Id);
            public record OrderDto(int Id, string SourceSystem, OrderStatus Status);
            public class MutableOrder {
                public int Id { get; set; }
                public required string SourceSystem { get; set; }
                public OrderStatus Status { get; set; }
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapConstant(nameof(OrderDto.SourceSystem), "orders")]
                [MapConstant(nameof(OrderDto.Status), OrderStatus.Approved)]
                public static partial OrderDto Map(OrderInput source);
                [MapConstant(nameof(MutableOrder.SourceSystem), "orders")]
                [MapConstant(nameof(MutableOrder.Status), OrderStatus.Approved)]
                public static partial MutableOrder Create(OrderInput source);
                [MapConstant(nameof(MutableOrder.SourceSystem), "orders")]
                [MapConstant(nameof(MutableOrder.Status), OrderStatus.Approved)]
                public static partial void Update(OrderInput source, MutableOrder target);
            }
            public static class Scenario {
                public static bool Verify() {
                    var source = new OrderInput(7);
                    var target = new MutableOrder { SourceSystem = "old" };
                    OrderMapper.Update(source, target);
                    return OrderMapper.Map(source) == new OrderDto(7, "orders", OrderStatus.Approved) &&
                        OrderMapper.Create(source).SourceSystem == "orders" && target.Id == 7 &&
                        target.SourceSystem == "orders" && target.Status == OrderStatus.Approved;
                }
            }
            """);
    }

    [Theory]
    [InlineData("bool", "true", "true")]
    [InlineData("char", "'\\n'", "'\\n'")]
    [InlineData("byte", "(byte)7", "(byte)7")]
    [InlineData("sbyte", "(sbyte)-7", "(sbyte)-7")]
    [InlineData("short", "(short)-7", "(short)-7")]
    [InlineData("ushort", "(ushort)7", "(ushort)7")]
    [InlineData("int", "int.MinValue", "int.MinValue")]
    [InlineData("uint", "uint.MaxValue", "uint.MaxValue")]
    [InlineData("long", "long.MinValue", "long.MinValue")]
    [InlineData("ulong", "ulong.MaxValue", "ulong.MaxValue")]
    [InlineData("float", "1.25f", "1.25f")]
    [InlineData("double", "1.25d", "1.25d")]
    [InlineData("float", "float.Epsilon", "float.Epsilon")]
    [InlineData("double", "double.MaxValue", "double.MaxValue")]
    [InlineData("int?", "7", "7")]
    [InlineData("string?", "null", "null")]
    [InlineData("int?", "null", "null")]
    [InlineData("System.Type", "typeof(OrderInput)", "typeof(OrderInput)")]
    public void Scalar_constants_preserve_their_declared_types(string type, string value, string expected)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            public record OrderInput();
            public record OrderDto({{type}} Value);
            [Mapper] public static partial class OrderMapper {
                [MapConstant(nameof(OrderDto.Value), {{value}})]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() => object.Equals(OrderMapper.Map(new OrderInput()).Value, {{expected}});
            }
            """);
    }

    [Theory]
    [InlineData("double", "double.NaN", "double.IsNaN(result.Value)")]
    [InlineData("double", "double.PositiveInfinity", "double.IsPositiveInfinity(result.Value)")]
    [InlineData("double", "double.NegativeInfinity", "double.IsNegativeInfinity(result.Value)")]
    [InlineData("float", "float.NaN", "float.IsNaN(result.Value)")]
    [InlineData("float", "float.PositiveInfinity", "float.IsPositiveInfinity(result.Value)")]
    [InlineData("float", "float.NegativeInfinity", "float.IsNegativeInfinity(result.Value)")]
    [InlineData("double", "-0.0d", "System.BitConverter.DoubleToInt64Bits(result.Value) == long.MinValue")]
    [InlineData("float", "-0.0f", "System.BitConverter.SingleToInt32Bits(result.Value) == int.MinValue")]
    public void Floating_point_constants_preserve_special_values(string type, string value, string assertion)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            public record OrderInput();
            public record OrderDto({{type}} Value);
            [Mapper] public static partial class OrderMapper {
                [MapConstant(nameof(OrderDto.Value), {{value}})]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() { var result = OrderMapper.Map(new OrderInput()); return {{assertion}}; }
            }
            """);
    }

    [Fact]
    public void Constant_strings_are_escaped_and_are_not_interpreted_as_generated_code()
    {
        const string value = "orders\"\\\n\t; throw new System.Exception();";
        var literal = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, quote: true);
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            public record OrderInput();
            public record OrderDto(string SourceSystem);
            [Mapper] public static partial class OrderMapper {
                [MapConstant(nameof(OrderDto.SourceSystem), {{literal}})]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput()).SourceSystem == {{literal}};
            }
            """);
    }

    [Theory]
    [InlineData("long", "7")]
    [InlineData("string", "null")]
    [InlineData("int", "null")]
    [InlineData("OrderStatus", "0")]
    [InlineData("OtherStatus", "OrderStatus.Pending")]
    [InlineData("object", "\"orders\"")]
    public void Incompatible_or_unsupported_constants_fail_instead_of_using_convention(string type, string constant)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public enum OrderStatus { Pending }
            public enum OtherStatus { Pending }
            public record OrderInput({{type}} Value);
            public record OrderDto({{type}} Value);
            [Mapper] public static partial class OrderMapper {
                [MapConstant(nameof(OrderDto.Value), {{constant}})]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP006" &&
            diagnostic.GetMessage().Contains("MapConstant", StringComparison.Ordinal));
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Generated_values_use_instance_dependencies_and_are_evaluated_once_for_required_constructor_members()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput();
            public class OrderDto {
                public OrderDto(int sequence) { ConstructorSequence = sequence; Sequence = sequence; }
                public int ConstructorSequence { get; }
                public required int Sequence { get; init; }
            }
            [Mapper] public partial class OrderMapper {
                private int _sequence;
                public OrderMapper(int start) => _sequence = start;
                [MapIgnoreTarget(nameof(OrderDto.ConstructorSequence))]
                [MapGeneratedValue(nameof(OrderDto.Sequence), Use = nameof(NextSequence))]
                public partial OrderDto Map(OrderInput source);
                private int NextSequence() => ++_sequence;
            }
            public static class Scenario {
                public static bool Verify() {
                    var mapper = new OrderMapper(40);
                    var first = mapper.Map(new OrderInput());
                    var second = mapper.Map(new OrderInput());
                    return first.Sequence == 41 && first.ConstructorSequence == 41 &&
                        second.Sequence == 42 && second.ConstructorSequence == 42;
                }
            }
            """);
    }

    [Fact]
    public void Generated_values_run_per_target_and_per_update_but_not_for_null_roots()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput();
            public class OrderDto { public int Id { get; set; } public int Sequence { get; set; } }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                public static int Calls { get; private set; }
                [MapGeneratedValue(nameof(OrderDto.Id), Use = nameof(Next))]
                [MapGeneratedValue(nameof(OrderDto.Sequence), Use = nameof(Next))]
                public static partial OrderDto? Map(OrderInput? source);
                [MapGeneratedValue(nameof(OrderDto.Id), Use = nameof(Next))]
                [MapGeneratedValue(nameof(OrderDto.Sequence), Use = nameof(Next))]
                public static partial void Update(OrderInput source, OrderDto target);
                private static int Next() => ++Calls;
            }
            public static class Scenario {
                public static bool Verify() {
                    if (OrderMapper.Map(null) is not null || OrderMapper.Calls != 0) return false;
                    var first = OrderMapper.Map(new OrderInput());
                    if (first is null || first.Id == first.Sequence || OrderMapper.Calls != 2) return false;
                    try { OrderMapper.Update(null!, first); return false; }
                    catch (System.ArgumentNullException) { }
                    try { OrderMapper.Update(new OrderInput(), null!); return false; }
                    catch (System.ArgumentNullException) { }
                    if (OrderMapper.Calls != 2) return false;
                    OrderMapper.Update(new OrderInput(), first);
                    return first.Id != first.Sequence && OrderMapper.Calls == 4;
                }
            }
            """);
    }

    [Theory]
    [InlineData("Missing", "private static int Default() => 7;")]
    [InlineData("Create", "private static int Create(int value) => value;")]
    [InlineData("Create", "private int Create() => 7;")]
    [InlineData("Create", "private static string Create() => \"orders\";")]
    [InlineData("Create", "private static int? Create() => null;")]
    [InlineData("Create", "private static int Create<T>() => 7;")]
    [InlineData("Create", "private static async System.Threading.Tasks.Task<int> Create() { await System.Threading.Tasks.Task.Yield(); return 7; }")]
    [InlineData("Create", "private static void Create() { }")]
    [InlineData("Create", "private static int _value; private static ref int Create() => ref _value;")]
    [InlineData("Create", "private static dynamic Create() => 7;")]
    public void Invalid_value_factories_report_the_helper_and_target(string name, string helper)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(int Id);
            public record OrderDto(int Id);
            [Mapper] public partial class OrderMapper {
                [MapGeneratedValue(nameof(OrderDto.Id), Use = "{{name}}")]
                public static partial OrderDto Map(OrderInput source);
                {{helper}}
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP006", error.Id);
        Assert.Contains(name, error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Id", error.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("[MapConstant(nameof(OrderDto.Id), 7)] [MapConstant(nameof(OrderDto.Id), 8)]")]
    [InlineData("[MapConstant(nameof(OrderDto.Id), 7)] [MapProperty(nameof(OrderInput.Id), nameof(OrderDto.Id))]")]
    [InlineData("[MapGeneratedValue(nameof(OrderDto.Id), Use = nameof(Create))] [MapConstant(nameof(OrderDto.Id), 7)]")]
    [InlineData("[MapGeneratedValue(nameof(OrderDto.Id), Use = nameof(Create))] [MapIgnoreTarget(nameof(OrderDto.Id))]")]
    [InlineData("[MapProperty(nameof(OrderInput.Id), nameof(OrderDto.Id))] [MapConstant(nameof(OrderDto.Id), 7)]")]
    [InlineData("[MapConstant(nameof(OrderDto.Id), 7)] [MapGeneratedValue(nameof(OrderDto.Id), Use = nameof(Create))]")]
    [InlineData("[MapConstant(nameof(OrderDto.Id), 7)] [MapIgnoreTarget(nameof(OrderDto.Id))]")]
    [InlineData("[MapGeneratedValue(nameof(OrderDto.Id), Use = nameof(Create))] [MapGeneratedValue(nameof(OrderDto.Id), Use = nameof(Create))]")]
    public void Conflicting_target_configurations_are_rejected(string configuration)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(int Id);
            public record OrderDto(int Id);
            [Mapper] public static partial class OrderMapper {
                {{configuration}}
                public static partial OrderDto Map(OrderInput source);
                private static int Create() => 7;
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP006");
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("[MapConstant(nameof(OrderDto.Id), 7)]")]
    [InlineData("[MapGeneratedValue(nameof(OrderDto.Id), Use = nameof(Create))]")]
    public void Value_mappings_do_not_consume_same_named_source_properties_in_strict_mode(string configuration)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(int Id);
            public record OrderDto(int Id);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                {{configuration}}
                public static partial OrderDto Map(OrderInput source);
                private static int Create() => 7;
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP010");
    }

    [Fact]
    public void Explicit_values_bypass_automatic_converters_and_unused_source_ignores_satisfy_strict_coverage()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput(string CustomerName, string SourceSystem);
            public record OrderDto(string CustomerName, string SourceSystem, string Channel);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapIgnoreSource(nameof(OrderInput.SourceSystem))]
                [MapConstant(nameof(OrderDto.SourceSystem), "orders")]
                [MapGeneratedValue(nameof(OrderDto.Channel), Use = nameof(CreateChannel))]
                public static partial OrderDto Map(OrderInput source);
                private static string UpperCase(string value) => value.ToUpperInvariant();
                private static string CreateChannel() => "web";
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput("Ada", "ignored")) == new OrderDto("ADA", "orders", "web");
            }
            """);
    }

    [Fact]
    public void All_overloads_of_generated_value_helper_names_are_explicit_only()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput(string CustomerName);
            public record OrderDto(string CustomerName, int Sequence);
            [Mapper] public static partial class OrderMapper {
                [MapGeneratedValue(nameof(OrderDto.Sequence), Use = nameof(Create))]
                public static partial OrderDto Map(OrderInput source);
                private static int Create() => 7;
                private static string Create(string value) => "changed";
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput("Ada")) == new OrderDto("Ada", 7);
            }
            """);
    }

    [Theory]
    [InlineData("long", "long.MinValue")]
    [InlineData("ulong", "ulong.MaxValue")]
    public void Enum_constants_preserve_underlying_numeric_limits_and_allow_nullable_targets(string underlying, string value)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            public enum OrderStatus : {{underlying}} { Unknown = {{value}} }
            public record OrderInput();
            public record OrderDto(OrderStatus? Status);
            [Mapper] public static partial class OrderMapper {
                [MapConstant(nameof(OrderDto.Status), OrderStatus.Unknown)]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput()).Status == OrderStatus.Unknown;
            }
            """);
    }

    [Fact]
    public void Factories_can_supply_non_attribute_types_and_nullable_results()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System;
            using XBullet.EasyMapping;
            public record OrderInput();
            public record OrderDto(Guid Id, decimal Total, string? CustomerName);
            [Mapper] public static partial class OrderMapper {
                [MapGeneratedValue(nameof(OrderDto.Id), Use = nameof(CreateId))]
                [MapGeneratedValue(nameof(OrderDto.Total), Use = nameof(CreateTotal))]
                [MapGeneratedValue(nameof(OrderDto.CustomerName), Use = nameof(CreateName))]
                public static partial OrderDto Map(OrderInput source);
                private static Guid CreateId() => new Guid("2a1f2469-9406-4e37-9f48-c63ba46561fd");
                private static decimal CreateTotal() => 12.5m;
                private static string? CreateName() => null;
            }
            public static class Scenario {
                public static bool Verify() {
                    var result = OrderMapper.Map(new OrderInput());
                    return result.Id == new Guid("2a1f2469-9406-4e37-9f48-c63ba46561fd") &&
                        result.Total == 12.5m && result.CustomerName is null;
                }
            }
            """);
    }

    [Theory]
    [InlineData("[MapConstant(\"Missing\", 7)]")]
    [InlineData("[MapConstant(\"Customer.Id\", 7)]")]
    [InlineData("[MapConstant(nameof(OrderDto.Id), 1m)]")]
    [InlineData("[MapConstant()]")]
    [InlineData("[MapGeneratedValue(\"Missing\", Use = nameof(Create))]")]
    [InlineData("[MapGeneratedValue(nameof(OrderDto.Id))]")]
    [InlineData("[MapGeneratedValue(nameof(OrderDto.Id), Use = \"\")]")]
    [InlineData("[MapGeneratedValue(nameof(OrderDto.Id), Use = null)]")]
    [InlineData("[MapGeneratedValue()]")]
    public void Invalid_value_attribute_arguments_report_configuration_errors_without_exceptions(string configuration)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(int Id);
            public record OrderDto(int Id);
            [Mapper] public static partial class OrderMapper {
                {{configuration}}
                public static partial OrderDto Map(OrderInput source);
                private static int Create() => 7;
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP006");
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Constant_and_factory_binding_changes_require_model_approval(bool generatedValue)
    {
        var original = generatedValue
            ? "[MapGeneratedValue(nameof(OrderDto.Id), Use = nameof(First))]"
            : "[MapConstant(nameof(OrderDto.Id), 7)]";
        var replacement = generatedValue
            ? "[MapGeneratedValue(nameof(OrderDto.Id), Use = nameof(Second))]"
            : "[MapConstant(nameof(OrderDto.Id), 8)]";
        var source = $$"""
            using XBullet.EasyMapping;
            public record OrderInput();
            public record OrderDto(int Id);
            [Mapper(RequireModelApproval = true)] public static partial class OrderMapper {
                {{original}}
                public static partial OrderDto Map(OrderInput source);
                private static int First() => 7;
                private static int Second() => 8;
            }
            """;
        var (initial, _) = GeneratorTestCompilation.Generate(source);
        var candidate = Assert.Single(Assert.Single(initial.GetRunResult().Results).GeneratedSources.Where(
            item => item.HintName.EndsWith(".ModelApproval.g.cs", StringComparison.Ordinal)));
        var lines = candidate.SourceText.ToString().Split('\n');
        var start = Array.IndexOf(lines, "// <MapperModelApproval>");
        var end = Array.IndexOf(lines, "// </MapperModelApproval>");
        Assert.True(start >= 0 && end > start);
        var approval = new MemoryApproval("OrderMapper.mapper.approved.txt",
            string.Join("\n", lines[(start + 1)..end].Select(line => line[3..])) + "\n");
        var (approved, _) = GeneratorTestCompilation.Generate(source, approval);
        Assert.Empty(approved.GetRunResult().Diagnostics);

        var (changed, _) = GeneratorTestCompilation.Generate(source.Replace(original, replacement, StringComparison.Ordinal), approval);
        var error = Assert.Single(changed.GetRunResult().Diagnostics);
        Assert.Equal("EMAP011", error.Id);
        Assert.Contains("has changed", error.GetMessage(), StringComparison.Ordinal);
    }

    private sealed class MemoryApproval(string path, string content) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
    }
}
