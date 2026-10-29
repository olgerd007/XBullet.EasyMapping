using Microsoft.CodeAnalysis;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class NestedTargetPathTests
{
    [Fact]
    public void Deep_updates_reuse_existing_objects_and_create_missing_descendants()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput(string City);
            public class AddressDto { public string City { get; set; } = ""; public int Version { get; set; } = 1; }
            public class CustomerDto { public AddressDto? Address { get; set; } }
            public class OrderDto { public CustomerDto? Customer { get; set; } }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.City), "Customer.Address.City")]
                [MapIgnoreTarget("Customer.Address.Version")]
                public static partial void Update(OrderInput source, OrderDto target);
            }
            public static class Scenario {
                public static bool Verify() {
                    var address = new AddressDto { Version = 42 };
                    var customer = new CustomerDto { Address = address };
                    var existing = new OrderDto { Customer = customer };
                    OrderMapper.Update(new OrderInput("Kyiv"), existing);
                    if (!object.ReferenceEquals(existing.Customer, customer) ||
                        !object.ReferenceEquals(customer.Address, address) || address.City != "Kyiv" || address.Version != 42) return false;
                    customer.Address = null;
                    OrderMapper.Update(new OrderInput("Lviv"), existing);
                    if (customer.Address is not { City: "Lviv", Version: 1 }) return false;
                    var missing = new OrderDto();
                    OrderMapper.Update(new OrderInput("Odesa"), missing);
                    return missing.Customer?.Address is { City: "Odesa", Version: 1 };
                }
            }
            """);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Ignoring_required_nested_members_is_rejected_even_when_the_root_method_updates(bool update)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(string Name);
            public class CustomerDto { public string Name { get; set; } = ""; public required int Version { get; set; } }
            public class OrderDto { public CustomerDto? Customer { get; set; } }
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Name), "Customer.Name")]
                [MapIgnoreTarget("Customer.Version")]
                public static partial {{(update ? "void Update(OrderInput source, OrderDto target)" : "OrderDto Map(OrderInput source)")}};
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP006" &&
            error.GetMessage().Contains("Customer.Version", StringComparison.Ordinal));
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Constants_and_generated_nested_targets_do_not_consume_same_named_sources()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record OrderInput(int Id, string CustomerName);
            public record CustomerDto(string Name, int Id);
            public record OrderDto(CustomerDto Customer);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.CustomerName), "Customer.Name")]
                [MapGeneratedValue("Customer.Id", Use = nameof(Next))]
                public static partial OrderDto Map(OrderInput source);
                private static int Next() => 7;
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics);
        Assert.Equal("EMAP010", error.Id);
        Assert.Contains("'Id'", error.GetMessage(), StringComparison.Ordinal);
    }

    [Fact]
    public void Source_properties_constants_and_providers_build_one_nested_object()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public enum OrderStatus { Pending, Approved }
            public record OrderInput(string CustomerName);
            public class CustomerDto {
                public string Name { get; set; } = "";
                public OrderStatus Status { get; set; }
                public int Sequence { get; set; }
            }
            public record OrderDto(CustomerDto Customer);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                public static int Calls { get; private set; }
                [MapProperty(nameof(OrderInput.CustomerName), "Customer.Name")]
                [MapConstant("Customer.Status", OrderStatus.Approved)]
                [MapGeneratedValue("Customer.Sequence", Use = nameof(Next))]
                public static partial OrderDto Map(OrderInput source);
                private static int Next() => ++Calls;
            }
            public static class Scenario {
                public static bool Verify() {
                    var result = OrderMapper.Map(new OrderInput("Ada"));
                    return result.Customer.Name == "Ada" && result.Customer.Status == OrderStatus.Approved &&
                        result.Customer.Sequence == 1 && OrderMapper.Calls == 1;
                }
            }
            """);
    }

    [Fact]
    public void Deep_source_and_target_paths_support_immutable_children_and_constructor_parameters()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record Address(string City);
            public record Customer(Address Address);
            public record OrderInput(Customer Customer);
            public record AddressDto(string City);
            public record CustomerDto(AddressDto Address);
            public record OrderDto(CustomerDto Customer);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapProperty("Customer.Address.City", "Customer.Address.City")]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput(new Customer(new Address("Kyiv"))))
                    .Customer.Address.City == "Kyiv";
            }
            """);
    }

    [Fact]
    public void Updates_reuse_existing_children_create_missing_children_and_preserve_ignored_values()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput(string CustomerName);
            public class CustomerDto {
                public string Name { get; set; } = "";
                public int Version { get; set; } = 1;
                public int Sequence { get; set; }
            }
            public class OrderDto { public CustomerDto? Customer { get; set; } }
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                public static int Calls { get; private set; }
                [MapProperty(nameof(OrderInput.CustomerName), "Customer.Name")]
                [MapGeneratedValue("Customer.Sequence", Use = nameof(Next))]
                [MapIgnoreTarget("Customer.Version")]
                public static partial void Update(OrderInput source, OrderDto target);
                private static int Next() => ++Calls;
            }
            public static class Scenario {
                public static bool Verify() {
                    var customer = new CustomerDto { Name = "old", Version = 42 };
                    var existing = new OrderDto { Customer = customer };
                    OrderMapper.Update(new OrderInput("Ada"), existing);
                    if (!object.ReferenceEquals(customer, existing.Customer) || customer.Name != "Ada" ||
                        customer.Version != 42 || customer.Sequence != 1) return false;
                    var missing = new OrderDto();
                    OrderMapper.Update(new OrderInput("Grace"), missing);
                    return missing.Customer is { Name: "Grace", Version: 1, Sequence: 2 } && OrderMapper.Calls == 2;
                }
            }
            """);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?")]
    public void Struct_intermediates_are_copied_back_during_updates(string nullable)
    {
        GeneratorTestCompilation.VerifyRuntime($$"""
            using XBullet.EasyMapping;
            public record OrderInput(string City);
            public struct AddressDto { public string City { get; set; } public int Version { get; set; } }
            public class OrderDto { public AddressDto{{nullable}} Address { get; set; } }
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.City), "Address.City")]
                [MapIgnoreTarget("Address.Version")]
                public static partial void Update(OrderInput source, OrderDto target);
            }
            public static class Scenario {
                public static bool Verify() {
                    var existing = new OrderDto { Address = new AddressDto { City = "old", Version = 42 } };
                    OrderMapper.Update(new OrderInput("Kyiv"), existing);
                    if (existing.Address is not { City: "Kyiv", Version: 42 }) return false;
                    var missing = new OrderDto();
                    OrderMapper.Update(new OrderInput("Lviv"), missing);
                    return missing.Address is { City: "Lviv" };
                }
            }
            """);
    }

    [Fact]
    public void Nested_target_coverage_reports_unmapped_members_instead_of_copying_unrelated_root_properties()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record OrderInput(string CustomerName, int Id);
            public class CustomerDto { public string Name { get; set; } = ""; public int Id { get; set; } }
            public class OrderDto { public CustomerDto Customer { get; set; } = new(); }
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.CustomerName), "Customer.Name")]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP003" &&
            error.GetMessage().Contains("Customer.Id", StringComparison.Ordinal));
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("[MapProperty(nameof(OrderInput.Name), \"Customer.Name\")] [MapIgnoreTarget(\"Customer\")]")]
    [InlineData("[MapConstant(\"Customer\", null)] [MapProperty(nameof(OrderInput.Name), \"Customer.Name\")]")]
    [InlineData("[MapProperty(nameof(OrderInput.Name), \"Customer.Name\")] [MapIgnoreTarget(\"Customer.Name\")]")]
    public void Parent_child_and_ignore_conflicts_are_rejected(string configuration)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(string Name);
            public class CustomerDto { public string Name { get; set; } = ""; }
            public class OrderDto { public CustomerDto? Customer { get; set; } }
            [Mapper] public static partial class OrderMapper {
                {{configuration}}
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP006");
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("Customer..Name")]
    [InlineData("Customer.Name.")]
    [InlineData("Customer.Missing")]
    [InlineData("Customer.Name.Length")]
    public void Invalid_target_paths_report_the_path_without_generator_exceptions(string path)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(string Name);
            public class CustomerDto { public string Name { get; set; } = ""; }
            public class OrderDto { public CustomerDto? Customer { get; set; } }
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Name), "{{path}}")]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.GetMessage().Contains(path, StringComparison.Ordinal));
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Fact]
    public void Nested_target_readonly_intermediates_are_not_silently_replaced_during_updates()
    {
        var (driver, _) = GeneratorTestCompilation.Generate("""
            using XBullet.EasyMapping;
            public record OrderInput(string Name);
            public class CustomerDto { public string Name { get; set; } = ""; }
            public class OrderDto { public CustomerDto Customer { get; } = new(); }
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Name), "Customer.Name")]
                public static partial void Update(OrderInput source, OrderDto target);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP009");
    }
}
