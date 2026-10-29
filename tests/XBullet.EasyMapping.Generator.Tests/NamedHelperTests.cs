using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class NamedHelperTests
{
    [Fact]
    public void Different_properties_select_different_helpers_with_the_same_signature()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public enum OrderStatus { Pending, Approved, Cancelled }
            public record Customer(OrderStatus Status);
            public record OrderInput(Customer? Customer, Customer? BackupCustomer);
            public record OrderDto(OrderStatus CustomerStatus, OrderStatus BackupStatus);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Customer), nameof(OrderDto.CustomerStatus), Use = nameof(MapCustomer))]
                [MapProperty(nameof(OrderInput.BackupCustomer), nameof(OrderDto.BackupStatus), Use = nameof(MapBackup))]
                public static partial OrderDto Map(OrderInput source);
                private static OrderStatus MapCustomer(Customer? value) => value?.Status ?? OrderStatus.Pending;
                private static OrderStatus MapBackup(Customer? value) => value?.Status ?? OrderStatus.Cancelled;
            }
            public static class Scenario {
                public static bool Verify() =>
                    OrderMapper.Map(new OrderInput(null, new Customer(OrderStatus.Approved))) ==
                        new OrderDto(OrderStatus.Pending, OrderStatus.Approved) &&
                    OrderMapper.Map(new OrderInput(new Customer(OrderStatus.Approved), null)) ==
                        new OrderDto(OrderStatus.Approved, OrderStatus.Cancelled);
            }
            """);
    }

    [Fact]
    public void Named_helpers_do_not_change_automatic_conversions_in_any_mapping_method()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput(string Customer, string BackupCustomer);
            public record OrderDto(string Customer, string BackupCustomer);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Customer), nameof(OrderDto.Customer), Use = nameof(Format))]
                public static partial OrderDto Map(OrderInput source);
                public static partial OrderDto Copy(OrderInput source);
                private static string Format(string value) => "formatted:" + value;
            }
            public static class Scenario {
                public static bool Verify() {
                    var source = new OrderInput("Ada", "Grace");
                    return OrderMapper.Map(source) == new OrderDto("formatted:Ada", "Grace") &&
                        OrderMapper.Copy(source) == new OrderDto("Ada", "Grace");
                }
            }
            """);
    }

    [Fact]
    public void Existing_automatic_helpers_remain_available_beside_explicit_helpers()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput(int Id, int CustomerId);
            public record OrderDto(string Id, string CustomerId);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.CustomerId), nameof(OrderDto.CustomerId), Use = nameof(CustomerCode))]
                public static partial OrderDto Map(OrderInput source);
                private static string CustomerCode(int value) => "C" + value;
                private static string DefaultCode(int value) => "O" + value;
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput(7, 8)) == new OrderDto("O7", "C8");
            }
            """);
    }

    [Fact]
    public void Instance_helpers_work_in_updates_and_creation_with_nested_source_paths()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Customer(string Name);
            public record OrderInput(Customer? Customer);
            public class OrderDto { public string CustomerName { get; set; } = ""; }
            [Mapper(Coverage = MappingCoverage.Both)] public partial class OrderMapper {
                private readonly string _fallback;
                public OrderMapper(string fallback) => _fallback = fallback;
                [MapProperty("Customer.Name", nameof(OrderDto.CustomerName), Use = nameof(FormatName))]
                public partial OrderDto Map(OrderInput source);
                [MapProperty("Customer.Name", nameof(OrderDto.CustomerName), Use = nameof(FormatName))]
                public partial void Update(OrderInput source, OrderDto target);
                private string FormatName(string? name) => name ?? _fallback;
            }
            public static class Scenario {
                public static bool Verify() {
                    var mapper = new OrderMapper("unknown");
                    var target = new OrderDto();
                    mapper.Update(new OrderInput(null), target);
                    return target.CustomerName == "unknown" &&
                        mapper.Map(new OrderInput(new Customer("Ada"))).CustomerName == "Ada";
                }
            }
            """);
    }

    [Fact]
    public void Selecting_the_whole_object_handles_runtime_null_despite_nonnullable_annotations()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Customer(string Name);
            public record OrderInput(Customer Customer);
            public record OrderDto(string CustomerName);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Customer), nameof(OrderDto.CustomerName), Use = nameof(FormatName))]
                public static partial OrderDto Map(OrderInput source);
                private static string FormatName(Customer? customer) => customer?.Name ?? "missing";
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput(null!)).CustomerName == "missing";
            }
            """);
    }

    [Fact]
    public void Nullable_value_inputs_are_passed_to_the_helper_without_skipping_null()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput(int? Quantity);
            public record OrderDto(int Quantity);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Quantity), nameof(OrderDto.Quantity), Use = nameof(DefaultQuantity))]
                public static partial OrderDto Map(OrderInput source);
                private static int DefaultQuantity(int? value) => value ?? 1;
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput(null)).Quantity == 1 &&
                    OrderMapper.Map(new OrderInput(7)).Quantity == 7;
            }
            """);
    }

    [Fact]
    public void The_selected_overload_is_called_even_when_another_overload_accepts_an_implicit_conversion()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput(int Quantity);
            public record OrderDto(string Quantity);
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Quantity), nameof(OrderDto.Quantity), Use = nameof(Format))]
                public static partial OrderDto Map(OrderInput source);
                private static string Format(int? value) => "nullable";
                private static string Format(long value) => "long";
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput(7)).Quantity == "nullable";
            }
            """);
    }

    [Theory]
    [InlineData("Missing", "private static int Default(int value) => value;")]
    [InlineData("Convert", "private static string Convert(string value) => value;")]
    [InlineData("Convert", "private static string Convert(int value) => value.ToString();")]
    [InlineData("Convert", "private int Convert(int value) => value;")]
    [InlineData("Convert", "private static int Convert<T>(int value) => value;")]
    [InlineData("Convert", "private static async System.Threading.Tasks.Task<int> Convert(int value) { await System.Threading.Tasks.Task.Yield(); return value; }")]
    [InlineData("Convert", "private static int Convert(ref int value) => value;")]
    [InlineData("Convert", "private static int Convert(int first, int second) => first + second;")]
    [InlineData("Convert", "private static void Convert(int value) { }")]
    [InlineData("Convert", "private static ref int Convert(ref int value) => ref value;")]
    public void Invalid_bindings_report_the_helper_target_and_mapping_without_falling_back(string name, string helper)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(int Quantity);
            public record OrderDto(int Quantity);
            [Mapper] public partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Quantity), nameof(OrderDto.Quantity), Use = "{{name}}")]
                public static partial OrderDto Map(OrderInput source);
                {{helper}}
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP006", error.Id);
        Assert.Contains(name, error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Quantity", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("Map", error.GetMessage(), StringComparison.Ordinal);
        Assert.True(error.Location.IsInSource);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    [InlineData("\" \"")]
    public void Explicit_empty_helper_names_are_configuration_errors(string name)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(int Quantity);
            public record OrderDto(int Quantity);
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Quantity), nameof(OrderDto.Quantity), Use = {{name}})]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP006" &&
            diagnostic.GetMessage().Contains("Use", StringComparison.Ordinal));
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("int?", "int", "private static int Convert(int value) => value;")]
    [InlineData("int", "int", "private static int? Convert(int value) => value;")]
    [InlineData("string?", "string", "private static string Convert(string value) => value;")]
    [InlineData("string", "string", "private static string? Convert(string value) => value;")]
    [InlineData("System.Collections.Generic.List<string?>", "int", "private static int Convert(System.Collections.Generic.List<string> value) => value.Count;")]
    public void Named_helpers_cannot_discard_input_or_output_nullability(string sourceType, string targetType, string helper)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput({{sourceType}} Quantity);
            public record OrderDto({{targetType}} Quantity);
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Quantity), nameof(OrderDto.Quantity), Use = nameof(Convert))]
                public static partial OrderDto Map(OrderInput source);
                {{helper}}
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP006");
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("dynamic", "int", "private static int Convert(dynamic value) => 1;")]
    [InlineData("int", "dynamic", "private static dynamic Convert(int value) => value;")]
    public void Dynamic_helper_signatures_cannot_defer_selection_to_runtime(string sourceType, string targetType, string helper)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput({{sourceType}} Quantity);
            public record OrderDto({{targetType}} Quantity);
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Quantity), nameof(OrderDto.Quantity), Use = nameof(Convert))]
                public static partial OrderDto Map(OrderInput source);
                {{helper}}
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP006", error.Id);
        Assert.Contains("statically typed", error.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void An_exact_overload_wins_over_a_nullable_compatible_overload()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput(int Quantity);
            public record OrderDto(string Quantity);
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Quantity), nameof(OrderDto.Quantity), Use = nameof(Format))]
                public static partial OrderDto Map(OrderInput source);
                private static string Format(int value) => "exact";
                private static string Format(int? value) => "nullable";
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput(7)).Quantity == "exact";
            }
            """);
    }

    [Fact]
    public void Ambiguous_compatible_overloads_report_the_requested_helper()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record OrderInput(int Quantity);
            public record OrderDto(int? Quantity);
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Quantity), nameof(OrderDto.Quantity), Use = nameof(Format))]
                public static partial OrderDto Map(OrderInput source);
                private static int Format(int value) => value;
                private static int? Format(int? value) => value;
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP006", error.Id);
        Assert.Contains("Format", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("ambiguous", error.GetMessage(), StringComparison.Ordinal);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Collection_helpers_receive_the_whole_collection_including_null()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Collections.Generic;
            using XBullet.EasyMapping;
            public record OrderInput(List<int>? OrderNumbers);
            public record OrderDto(List<int> OrderNumbers);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.OrderNumbers), nameof(OrderDto.OrderNumbers), Use = nameof(CopyOrders))]
                public static partial OrderDto Map(OrderInput source);
                private static List<int> CopyOrders(List<int>? values) => values is null ? new() { -1 } : new(values);
            }
            public static class Scenario {
                public static bool Verify() {
                    var values = new List<int> { 7, 8 };
                    var result = OrderMapper.Map(new OrderInput(values));
                    values.Clear();
                    return result.OrderNumbers.Count == 2 && result.OrderNumbers[0] == 7 &&
                        OrderMapper.Map(new OrderInput(null)).OrderNumbers[0] == -1;
                }
            }
            """);
    }

    [Fact]
    public void Nullable_reference_results_are_allowed_for_nullable_targets()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput(string? CustomerName);
            public record OrderDto(string? CustomerName);
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.CustomerName), nameof(OrderDto.CustomerName), Use = nameof(Format))]
                public static partial OrderDto Map(OrderInput source);
                private static string? Format(string? value) => value is null ? "missing" : null;
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput(null)).CustomerName == "missing" &&
                    OrderMapper.Map(new OrderInput("Ada")).CustomerName is null;
            }
            """);
    }

    [Fact]
    public void Generated_nested_mapping_methods_can_be_selected_explicitly()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Customer(string Name);
            public record CustomerDto(string Name);
            public record OrderInput(Customer Customer);
            public record OrderDto(CustomerDto Customer);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Customer), nameof(OrderDto.Customer), Use = nameof(MapCustomer))]
                public static partial OrderDto Map(OrderInput source);
                private static partial CustomerDto MapCustomer(Customer source);
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput(new Customer("Ada"))).Customer.Name == "Ada";
            }
            """);
    }

    [Fact]
    public void Explicit_helper_dependencies_still_reject_recursive_generated_mappings()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record OrderInput(OrderInput? Parent);
            public record OrderDto(OrderDto? Parent);
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Parent), nameof(OrderDto.Parent), Use = nameof(Map))]
                public static partial OrderDto? Map(OrderInput? source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP008");
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void A_bound_source_property_cannot_also_be_ignored_under_strict_coverage()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record OrderInput(int CustomerId);
            public record OrderDto(string CustomerCode);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.CustomerId), nameof(OrderDto.CustomerCode), Use = nameof(Format))]
                [MapIgnoreSource(nameof(OrderInput.CustomerId))]
                public static partial OrderDto Map(OrderInput source);
                private static string Format(int value) => "customer:" + value;
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP006" &&
            diagnostic.GetMessage().Contains("both mapped and ignored", StringComparison.Ordinal));
    }

    [Fact]
    public void Changing_a_helper_binding_requires_model_shape_approval()
    {
        const string source = """
            using XBullet.EasyMapping;
            public record OrderInput(int Quantity);
            public record OrderDto(int Quantity);
            [Mapper(RequireModelApproval = true)] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Quantity), nameof(OrderDto.Quantity), Use = nameof(First))]
                public static partial OrderDto Map(OrderInput source);
                private static int First(int value) => value;
                private static int Second(int value) => value + 1;
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

        var (changed, _) = GeneratorTestCompilation.Generate(
            source.Replace("Use = nameof(First)", "Use = nameof(Second)", StringComparison.Ordinal), approval);

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
