using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class GeneratorBoundaryTests
{
    [Theory]
    [InlineData("public partial interface OrderMapper", "EMAP001")]
    [InlineData("public partial record OrderMapper", "EMAP001")]
    [InlineData("public partial class OrderMapper { } public class OrderMapper", "EMAP001")]
    public void Invalid_mapper_declarations_fail_with_mapper_diagnostics(string declaration, string diagnostic)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            [Mapper] {{declaration}} {}
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == diagnostic);
    }

    [Theory]
    [InlineData("public static partial OrderDto Map(params OrderInput[] source);")]
    [InlineData("public abstract partial OrderDto Map(OrderInput source);")]
    [InlineData("public virtual partial OrderDto Map(OrderInput source);")]
    [InlineData("public override partial OrderDto Map(OrderInput source);")]
    [InlineData("public static async partial System.Threading.Tasks.Task<OrderDto> Map(OrderInput source);")]
    [InlineData("public static partial OrderDto Map(this OrderInput source);")]
    [InlineData("public static partial ref OrderDto Map(OrderInput source);")]
    [InlineData("public static partial OrderDto Map(ref OrderInput source);")]
    [InlineData("public static partial OrderDto Map();")]
    [InlineData("public static partial void Update(OrderInput source);")]
    [InlineData("protected static partial OrderDto Map(OrderInput source);")]
    [InlineData("public static partial OrderDto Map(OrderInput source = null!);")]
    [InlineData("public static partial int[] Map(OrderInput source);")]
    [InlineData("public static partial OrderDto Map(int[] source);")]
    [InlineData("public static partial OrderDto Map(int source);")]
    [InlineData("public static partial string Map(OrderInput source);")]
    [InlineData("public static partial OrderDto Map(System.Collections.Generic.List<int> source);")]
    [InlineData("public static partial OrderDto Map(dynamic source);")]
    [InlineData("public static partial dynamic Map(OrderInput source);")]
    [InlineData("public static partial OrderDto Map(OrderStatus source);")]
    [InlineData("public static partial OrderStatus Map(OrderInput source);")]
    [InlineData("public static partial OrderDto Map(OrderInput* source);")]
    [InlineData("public static partial System.Span<int> Map(OrderInput source);")]
    [InlineData("public static partial OrderDto Map(System.Span<int> source);")]
    [InlineData("public static partial System.Collections.Generic.List<OrderDto> Map(OrderInput source);")]
    [InlineData("public static partial void Update(OrderInput source, OrderValueDto target);")]
    [InlineData("public static partial void Update(OrderInput source, OrderDto? target);")]
    public void Unsupported_method_boundaries_report_signatures_without_generator_failures(string declaration)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(int Id);
            public record OrderDto(int Id);
            public readonly record struct OrderValueDto(int Id);
            public enum OrderStatus { Pending }
            [Mapper] public static unsafe partial class OrderMapper { {{declaration}} }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP002");
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Strict_source_coverage_rejects_indexers_but_does_not_treat_them_as_named_properties()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public class OrderInput {
                public int Id { get; set; }
                public int this[int index] => index;
                public static int Shared;
                private int Hidden => 1;
                public int WriteOnly { set {} }
            }
            public record OrderDto(int Id);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP009", error.Id);
        Assert.Contains("Source indexers", error.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("[MapIgnoreTarget]")]
    [InlineData("[MapIgnoreSource]")]
    public void Incomplete_ignore_attributes_report_configuration_errors(string attribute)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(int Id);
            public record OrderDto(int Id);
            [Mapper] public static partial class OrderMapper {
                {{attribute}} public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP006");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"Both\"")]
    public void Malformed_coverage_values_report_configuration_errors(string value)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(int Id);
            public record OrderDto(int Id);
            [Mapper(Coverage = {{value}})] public static partial class OrderMapper {
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP006");
    }

    [Theory]
    [InlineData("[MapProperty(null, nameof(OrderDto.Name))]")]
    [InlineData("[MapProperty(nameof(OrderInput.Name), null)]")]
    [InlineData("[MapProperty(nameof(OrderInput.Name), nameof(OrderDto.Name))] [MapProperty(nameof(OrderInput.Name), nameof(OrderDto.Name))]")]
    [InlineData("[MapConstant(null, \"web\")]")]
    [InlineData("[MapGeneratedValue(null, Use = nameof(Name))]")]
    [InlineData("[MapIgnoreTarget(null)]")]
    [InlineData("[MapIgnoreTarget(nameof(OrderDto.Name))] [MapIgnoreTarget(nameof(OrderDto.Name))]")]
    public void Null_and_duplicate_target_configurations_report_errors(string configuration)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(string Name);
            public record OrderDto(string Name);
            [Mapper] public static partial class OrderMapper {
                {{configuration}}
                public static partial OrderDto Map(OrderInput source);
                private static string Name() => "web";
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP006");
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("Name")]
    [InlineData("Customer.Name")]
    public void Mapped_and_ignored_constructor_parameters_cannot_be_emitted(string path)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(string Name);
            public record CustomerDto(string Name = "default");
            public record OrderDto(CustomerDto Customer, string Name = "default");
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Name), "Customer.Name")]
                [MapProperty(nameof(OrderInput.Name), nameof(OrderDto.Name))]
                [MapIgnoreTarget("{{path}}")]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP006" &&
            error.GetMessage().Contains("both mapped and ignored", StringComparison.Ordinal));
    }

    [Fact]
    public void Namespaces_internal_methods_and_nullable_struct_roots_compile_and_execute()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System;
            using XBullet.EasyMapping;
            public static class Scenario { public static bool Verify() => Orders.OrderMapper.Verify(); }
            namespace Orders {
                public readonly record struct OrderInput(int Id);
                public record OrderDto(int Id);
                [Mapper] public static partial class OrderMapper {
                    internal static partial OrderDto? Map(OrderInput? source);
                    internal static partial OrderDto Required(OrderInput? source);
                    internal static partial OrderDto MapValue(OrderInput source);
                    public static bool Verify() {
                        if (Map(null) is not null || Map(new OrderInput(7))?.Id != 7) return false;
                        if (MapValue(new OrderInput(8)).Id != 8) return false;
                        try { Required(null); return false; }
                        catch (ArgumentNullException error) { return error.ParamName == "source"; }
                    }
                }
            }
            """);
    }

    [Fact]
    public void Constructor_parameter_bindings_cannot_select_an_unused_root_overload()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record OrderInput(int Id);
            public class OrderDto {
                public OrderDto() {}
                public OrderDto(string label) {}
                public OrderDto(ref int id) { Id = id; }
                public int Id { get; set; }
                private int _hidden;
                public static int Shared;
                public int ReadHidden() => _hidden;
            }
            [Mapper] public static partial class OrderMapper {
                [MapConstant("label", "web")]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP006", error.Id);
        Assert.Contains("not used by the selected constructor", error.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Array_constants_can_explicitly_supply_null_nullable_arrays()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput();
            public record OrderDto(string[]? Labels);
            [Mapper] public static partial class OrderMapper {
                [MapConstant(nameof(OrderDto.Labels), (string[]?)null)]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput()).Labels is null;
            }
            """);
    }

    [Fact]
    public void Interface_source_paths_honor_redeclarations_and_unrelated_inherited_properties()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public interface ICustomerBase { string Name { get; } int Id { get; } }
            public interface ICustomer : ICustomerBase { new string Name { get; } }
            public record Customer(string Name, int Id) : ICustomer;
            public record OrderInput(ICustomer Customer);
            public record OrderDto(string CustomerName, int CustomerId);
            [Mapper] public static partial class OrderMapper {
                [MapProperty("Customer.Name", nameof(OrderDto.CustomerName))]
                [MapProperty("Customer.Id", nameof(OrderDto.CustomerId))]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput(new Customer("Ada", 7))) == new OrderDto("Ada", 7);
            }
            """);
    }

    [Fact]
    public void Write_only_nested_source_properties_are_rejected()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public class Customer { public string Name { set {} } }
            public record OrderInput(Customer Customer);
            public record OrderDto(string Name);
            [Mapper] public static partial class OrderMapper {
                [MapProperty("Customer.Name", nameof(OrderDto.Name))]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP006");
        Assert.Contains("Customer.Name", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains("readable", error.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Nested_indexers_cannot_be_selected_using_their_symbol_name()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public class Customer { public string this[int index] => "Ada"; }
            public record OrderInput(Customer Customer);
            public record OrderDto(string Name);
            [Mapper] public static partial class OrderMapper {
                [MapProperty("Customer.this[]", nameof(OrderDto.Name))]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP006");
        Assert.Contains("indexers", error.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("System.Collections.ArrayList")]
    [InlineData("System.Collections.Generic.ICollection<int>")]
    [InlineData("System.Collections.Generic.IReadOnlyCollection<int>")]
    [InlineData("System.Collections.Generic.IReadOnlyList<int>")]
    [InlineData("System.Collections.Generic.IList<int>")]
    [InlineData("int[,]")]
    [InlineData("Orders.List<int>")]
    public void Collection_targets_accept_only_the_supported_contracts(string targetType)
    {
        var (driver, compilation) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            namespace Orders { public class List<T> {} }
            public record OrderInput(int[] Numbers);
            public record OrderDto({{targetType}} Numbers);
            [Mapper] public static partial class OrderMapper {
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        if (targetType.StartsWith("System.Collections.Generic.I", StringComparison.Ordinal))
        {
            Assert.Empty(driver.GetRunResult().Diagnostics);
            Assert.Empty(compilation.GetDiagnostics().Where(error => error.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error));
        }
        else
        {
            Assert.Equal("EMAP005", Assert.Single(driver.GetRunResult().Diagnostics).Id);
        }
    }
}
