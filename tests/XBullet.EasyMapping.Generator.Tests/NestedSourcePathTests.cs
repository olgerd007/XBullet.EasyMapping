using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class NestedSourcePathTests
{
    [Fact]
    public void Nested_enum_properties_map_to_constructor_arguments_without_a_converter()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public enum OrderStatus { Pending, Approved }
            public record Customer(OrderStatus Status);
            public record OrderInput(Customer Customer);
            public record OrderDto(OrderStatus CustomerOrderStatus);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty(nameof(OrderInput.Customer) + "." + nameof(Customer.Status), nameof(OrderDto.CustomerOrderStatus))]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new OrderInput(new Customer(OrderStatus.Approved)))
                    .CustomerOrderStatus == OrderStatus.Approved;
            }
            """);
    }

    [Fact]
    public void Multiple_paths_from_one_root_map_inherited_members_for_creation_and_updates()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public class CustomerBase { public bool IsActive { get; set; } }
            public class Customer : CustomerBase { public string Value { get; set; } = ""; }
            public record OrderInput(Customer Customer);
            public class OrderDto {
                public bool CustomerIsActive { get; set; }
                public string CustomerName { get; set; } = "";
            }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty("Customer.IsActive", nameof(OrderDto.CustomerIsActive))]
                [MapProperty("Customer.Value", nameof(OrderDto.CustomerName))]
                public static partial OrderDto Map(OrderInput source);
                [MapProperty("Customer.IsActive", nameof(OrderDto.CustomerIsActive))]
                [MapProperty("Customer.Value", nameof(OrderDto.CustomerName))]
                public static partial void Update(OrderInput source, OrderDto target);
            }
            public static class Scenario {
                public static bool Verify() {
                    var source = new OrderInput(new Customer { IsActive = true, Value = "Ada" });
                    var created = M.Map(source);
                    var updated = new OrderDto();
                    M.Update(source, updated);
                    return created.CustomerIsActive && created.CustomerName == "Ada" &&
                        updated.CustomerIsActive && updated.CustomerName == "Ada";
                }
            }
            """);
    }

    [Theory]
    [InlineData("class", "new Customer(7)")]
    [InlineData("readonly record struct", "new Customer(7)")]
    public void Nullable_intermediates_propagate_null_and_allow_numeric_widening(string kind, string value)
    {
        var change = kind == "class" ? "public record Customer(int Value);" : $"public {kind} Customer(int Value);";
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            {{change}}
            public record OrderInput(Customer? Customer);
            public record OrderDto(long? Value);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty("Customer.Value", nameof(OrderDto.Value))]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new OrderInput(null)).Value is null &&
                    M.Map(new OrderInput({{value}})).Value == 7L;
            }
            """);
    }

    [Fact]
    public void Deep_nullable_paths_evaluate_each_getter_once()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public sealed class Details {
                public int Calls { get; private set; }
                public string Name { get { Calls++; return "Ada"; } }
            }
            public sealed class Customer {
                public int Calls { get; private set; }
                public Details? Item { get; set; }
                public Details? Details { get { Calls++; return Item; } }
            }
            public sealed class OrderInput {
                public int Calls { get; private set; }
                public Customer? Item { get; set; }
                public Customer? Customer { get { Calls++; return Item; } }
            }
            public record OrderDto(string? Name);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapIgnoreSource(nameof(OrderInput.Calls)), MapIgnoreSource(nameof(OrderInput.Item))]
                [MapProperty("Customer.Details.Name", nameof(OrderDto.Name))]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() {
                    var missing = new OrderInput();
                    if (M.Map(missing).Name is not null || missing.Calls != 1) return false;
                    var change = new Customer();
                    var middleMissing = new OrderInput { Item = change };
                    if (M.Map(middleMissing).Name is not null || middleMissing.Calls != 1 || change.Calls != 1) return false;
                    var details = new Details();
                    change.Item = details;
                    var present = new OrderInput { Item = change };
                    return M.Map(present).Name == "Ada" && present.Calls == 1 && change.Calls == 2 && details.Calls == 1;
                }
            }
            """);
    }

    [Fact]
    public void Handwritten_converters_can_handle_null_from_an_intermediate()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public enum OrderStatus { Pending, Approved }
            public record Customer(OrderStatus Status);
            public record OrderInput(Customer? Customer);
            public record OrderDto(OrderStatus Status);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty("Customer.Status", nameof(OrderDto.Status))]
                public static partial OrderDto Map(OrderInput source);
                private static OrderStatus MapStatus(OrderStatus? value) => value ?? OrderStatus.Pending;
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new OrderInput(null)).Status == OrderStatus.Pending &&
                    M.Map(new OrderInput(new Customer(OrderStatus.Approved))).Status == OrderStatus.Approved;
            }
            """);
    }

    [Fact]
    public void Nested_interface_properties_and_escaped_identifiers_are_supported()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public interface IBase { string @event { get; } }
            public interface IChange : IBase { }
            public record Customer(string @event) : IChange;
            public record OrderInput(IChange @class);
            public record OrderDto(string Value);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty("class.event", nameof(OrderDto.Value))]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new OrderInput(new Customer("Ada"))).Value == "Ada";
            }
            """);
    }

    [Theory]
    [InlineData(".Customer.Status")]
    [InlineData("Customer..Status")]
    [InlineData("Customer.Status.")]
    [InlineData("Customer.Missing")]
    [InlineData("Missing.Status")]
    [InlineData("Customer.Secret")]
    [InlineData("Customer.Hidden")]
    [InlineData("Customer.Static")]
    [InlineData("Customer.Field")]
    [InlineData("Customer.Item")]
    public void Invalid_paths_report_the_configured_path_without_generator_exceptions(string path)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public class Customer {
                public int Status { get; set; }
                private int Secret { get; set; }
                public int Hidden { private get; set; }
                public static int Static { get; set; }
                public int Field;
                public int this[int index] => index;
            }
            public record OrderInput(Customer Customer);
            public record OrderDto(int Status);
            [Mapper] public static partial class M {
                [MapProperty("{{path}}", nameof(OrderDto.Status))]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic =>
            diagnostic.Id == "EMAP006" && diagnostic.GetMessage().Contains(path, StringComparison.Ordinal));
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("Customer?", "OrderStatus", "nullable source")]
    [InlineData("Customer", "OtherStatus", "declare a mapping")]
    [InlineData("Customer", "byte", "declare a mapping")]
    public void Unsafe_nested_conversions_still_report_conversion_errors(string sourceType, string targetType, string reason)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public enum OrderStatus : byte { Pending, Approved }
            public enum OtherStatus : byte { Pending, Approved }
            public record Customer(OrderStatus Status);
            public record OrderInput({{sourceType}} Customer);
            public record OrderDto({{targetType}} Status);
            [Mapper] public static partial class M {
                [MapProperty("Customer.Status", nameof(OrderDto.Status))]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic =>
            diagnostic.Id == "EMAP005" && diagnostic.GetMessage().Contains(reason, StringComparison.Ordinal));
    }

    [Fact]
    public void Mapping_a_nested_path_conflicts_with_ignoring_its_root_source_property()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record Customer(int Value);
            public record OrderInput(Customer Customer);
            public record OrderDto(int Value);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty("Customer.Value", nameof(OrderDto.Value)), MapIgnoreSource(nameof(OrderInput.Customer))]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP006" &&
            diagnostic.GetMessage().Contains("both mapped and ignored", StringComparison.Ordinal));
    }

    [Fact]
    public void Collection_leaves_reuse_collection_conversion_and_keep_missing_paths_null()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.Collections.Generic;
            using XBullet.EasyMapping;
            public record Details(List<int> Values);
            public record OrderInput(Details? Details);
            public record OrderDto(long[]? Values);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty("Details.Values", nameof(OrderDto.Values))]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() {
                    if (M.Map(new OrderInput(null)).Values is not null) return false;
                    var values = new List<int> { 7, 8 };
                    var result = M.Map(new OrderInput(new Details(values)));
                    values.Clear();
                    return result.Values is { Length: 2 } && result.Values[0] == 7L && result.Values[1] == 8L;
                }
            }
            """);
    }

    [Fact]
    public void Nullable_struct_roots_and_paths_with_nonnullable_struct_descendants_are_supported()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public readonly record struct Details(int Value);
            public readonly record struct Customer(Details Details);
            public readonly record struct OrderInput(Customer? Customer);
            public record OrderDto(int? Value);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty("Customer.Details.Value", nameof(OrderDto.Value))]
                public static partial OrderDto? Map(OrderInput? source);
            }
            public static class Scenario {
                public static bool Verify() => M.Map(null) is null &&
                    M.Map(new OrderInput(null)) is { Value: null } &&
                    M.Map(new OrderInput(new Customer(new Details(7)))) is { Value: 7 };
            }
            """);
    }

    [Fact]
    public void Nested_target_paths_remain_unsupported()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record OrderInput(int Value);
            public record Details(int Value);
            public record OrderDto(Details Details);
            [Mapper] public static partial class M {
                [MapProperty(nameof(OrderInput.Value), "Details.Value")]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP006" &&
            diagnostic.GetMessage().Contains("direct target", StringComparison.Ordinal));
    }

    [Fact]
    public void Changing_the_selected_nested_property_requires_new_model_approval()
    {
        const string source = """
            using XBullet.EasyMapping;
            public record Customer(int First, int Second);
            public record OrderInput(Customer Customer);
            public record OrderDto(int Value);
            [Mapper(Coverage = MappingCoverage.Both, RequireModelApproval = true)] public static partial class M {
                [MapProperty("Customer.First", nameof(OrderDto.Value))]
                public static partial OrderDto Map(OrderInput source);
            }
            """;
        var (initial, _) = GeneratorTestCompilation.Generate(source);
        var generated = Assert.Single(Assert.Single(initial.GetRunResult().Results).GeneratedSources.Where(
            item => item.HintName.EndsWith(".ModelApproval.g.cs", StringComparison.Ordinal)));
        var lines = generated.SourceText.ToString().Split('\n');
        var start = Array.IndexOf(lines, "// <MapperModelApproval>");
        var end = Array.IndexOf(lines, "// </MapperModelApproval>");
        Assert.True(start >= 0 && end > start);
        var approval = new MemoryApproval("M.mapper.approved.txt",
            string.Join("\n", lines[(start + 1)..end].Select(line => line[3..])) + "\n");

        var (approved, _) = GeneratorTestCompilation.Generate(source, approval);
        Assert.Empty(approved.GetRunResult().Diagnostics);
        var (changed, _) = GeneratorTestCompilation.Generate(
            source.Replace("Customer.First", "Customer.Second", StringComparison.Ordinal), approval);

        var error = Assert.Single(changed.GetRunResult().Diagnostics);
        Assert.Equal("EMAP011", error.Id);
        Assert.Contains("has changed", error.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Array_length_can_be_selected_through_a_nullable_array()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Customer(int[]? OrderNumbers);
            public record OrderInput(Customer Customer);
            public record OrderDto(int? OrderCount);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty("Customer.OrderNumbers.Length", nameof(OrderDto.OrderCount))]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new OrderInput(new Customer(null))).OrderCount is null &&
                    M.Map(new OrderInput(new Customer(new[] { 7, 8 }))).OrderCount == 2;
            }
            """);
    }

    [Fact]
    public void More_specific_inherited_interface_properties_hide_base_declarations()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public interface ICustomerBase { string Name { get; } }
            public interface INamedCustomer : ICustomerBase { new string Name { get; } }
            public interface ICustomer : INamedCustomer { }
            public record Customer(string Name) : ICustomer;
            public record OrderInput(ICustomer Customer);
            public record OrderDto(string CustomerName);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class M {
                [MapProperty("Customer.Name", nameof(OrderDto.CustomerName))]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() => M.Map(new OrderInput(new Customer("Ada"))).CustomerName == "Ada";
            }
            """);
    }

    [Theory]
    [InlineData("public class Customer { public string Name { get; set; } = \"\"; public string name { get; set; } = \"\"; }", "ambiguous")]
    [InlineData("public interface IFirst { string Name { get; } } public interface ISecond { string Name { get; } } public interface Customer : IFirst, ISecond { }", "ambiguous")]
    [InlineData("public record Customer(dynamic Name);", "cannot traverse")]
    public void Ambiguous_or_dynamic_paths_are_rejected(string declaration, string reason)
    {
        var path = reason == "cannot traverse" ? "Customer.Name.Value" : "Customer.Name";
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            {{declaration}}
            public record OrderInput(Customer Customer);
            public record OrderDto(string CustomerName);
            [Mapper] public static partial class M {
                [MapProperty("{{path}}", nameof(OrderDto.CustomerName))]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, diagnostic => diagnostic.Id == "EMAP006" &&
            diagnostic.GetMessage().Contains(reason, StringComparison.Ordinal));
    }

    private sealed class MemoryApproval(string path, string content) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(CancellationToken cancellationToken = default) => SourceText.From(content);
    }
}
