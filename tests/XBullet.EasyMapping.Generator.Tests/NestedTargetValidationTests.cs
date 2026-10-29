using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class NestedTargetValidationTests
{
    [Theory]
    [InlineData("public interface CustomerDto { string Name { get; set; } }", "CustomerDto", "Customer.Name", "EMAP009")]
    [InlineData("public abstract class CustomerDto { public string Name { get; set; } = \"\"; }", "CustomerDto", "Customer.Name", "EMAP009")]
    [InlineData("public ref struct CustomerDto { public string Name { get; set; } }", "CustomerDto", "Customer.Name", "EMAP009")]
    [InlineData("", "System.Collections.Generic.List<string>", "Customer.Capacity", "EMAP009")]
    [InlineData("", "string[]", "Customer.Length", "EMAP009")]
    [InlineData("", "dynamic", "Customer.Name", "EMAP009")]
    [InlineData("public class CustomerDto { private CustomerDto() {} public string Name { get; set; } = \"\"; }", "CustomerDto", "Customer.Name", "EMAP004")]
    [InlineData("public class CustomerDto { public string Name { get; set; } = \"\"; public string name { get; set; } = \"\"; }", "CustomerDto", "Customer.Name", "EMAP006")]
    [InlineData("public class CustomerDto { public string Name { get; set; } = \"\"; public int Version; }", "CustomerDto", "Customer.Name", "EMAP009")]
    [InlineData("public class CustomerDto { public string Name { get; set; } = \"\"; }", "CustomerDto", "Customer.Missing.Name", "EMAP006")]
    public void Invalid_intermediates_report_an_explicit_generator_diagnostic(string model, string type, string path, string diagnostic)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(string Name);
            {{model}}
            public class OrderDto { public {{type}} Customer { get; set; } = default!; }
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Name), "{{path}}")]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == diagnostic);
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("public CustomerDto Customer { private get; set; } = new();", "CustomerDto", "public class CustomerDto { public string Name { get; set; } = \"\"; }")]
    [InlineData("public CustomerDto Customer { set {} }", "CustomerDto", "public class CustomerDto { public string Name { get; set; } = \"\"; }")]
    [InlineData("public string Customer { get; set; } = \"\";", "string", "")]
    public void Nested_updates_reject_unreadable_or_scalar_intermediates(string property, string type, string model)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(string Name);
            {{model}}
            public class OrderDto { {{property}} }
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Name), "Customer.Name")]
                public static partial void Update(OrderInput source, OrderDto target);
            }
            """);

        var error = Assert.Single(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP009");
        Assert.Contains("Customer", error.GetMessage(), StringComparison.Ordinal);
        Assert.Contains(type == "string" ? "concrete DTO" : "public getter", error.GetMessage(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Customer.Missing")]
    [InlineData("Customer.Missing.Version")]
    [InlineData("Customer.Name.Length")]
    public void Invalid_nested_ignores_are_validated_even_without_source_bindings(string path)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput();
            public class CustomerDto { public string Name { get; set; } = ""; }
            public class OrderDto { public CustomerDto? Customer { get; set; } }
            [Mapper] public static partial class OrderMapper {
                [MapIgnoreTarget("{{path}}")]
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.GetMessage().Contains(path, StringComparison.Ordinal));
        Assert.Empty(Assert.Single(driver.GetRunResult().Results).GeneratedSources);
    }

    [Theory]
    [InlineData("[MapConstant(\"Customer.Version\", 42)]")]
    [InlineData("[MapIgnoreTarget(\"Customer.Version\")]")]
    public void Unselected_nested_constructor_parameters_are_not_silently_accepted(string configuration)
    {
        var (driver, _) = GeneratorTestCompilation.Generate($$"""
            using XBullet.EasyMapping;
            public record OrderInput(string Name);
            public class CustomerDto {
                public CustomerDto() {}
                public CustomerDto(int version) {}
                public string Name { get; set; } = "";
            }
            public record OrderDto(CustomerDto Customer);
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Name), "Customer.Name")]
                {{configuration}}
                public static partial OrderDto Map(OrderInput source);
            }
            """);

        Assert.Contains(driver.GetRunResult().Diagnostics, error => error.Id == "EMAP006" &&
            error.GetMessage().Contains("Customer.Version", StringComparison.Ordinal));
    }

    [Fact]
    public void Nested_optional_constructor_defaults_and_ignored_values_are_retained()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using XBullet.EasyMapping;
            public record OrderInput(string Name);
            public record CustomerDto(string Name, int Version = 7, int Sequence = 8);
            public record OrderDto(CustomerDto Customer);
            [Mapper(Coverage = MappingCoverage.Both)] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Name), "Customer.Name")]
                [MapConstant("Customer.Version", 42)]
                [MapIgnoreTarget("Customer.Sequence")]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput("Ada")).Customer == new CustomerDto("Ada", 42, 8);
            }
            """);
    }

    [Fact]
    public void Nested_required_constructor_guarantees_and_unrelated_attributes_are_honored()
    {
        GeneratorTestCompilation.VerifyRuntime("""
            using System.ComponentModel;
            using System.Diagnostics.CodeAnalysis;
            using XBullet.EasyMapping;
            public record OrderInput(string Name);
            public class CustomerDto {
                [Description("Constructs a complete customer"), SetsRequiredMembers]
                public CustomerDto(string name) { Name = name; Version = 42; }
                public required string Name { get; init; }
                public required int Version { get; init; }
                public static int Shared { get; set; }
                private int Hidden { get; set; }
            }
            public record OrderDto(CustomerDto Customer);
            [Mapper] public static partial class OrderMapper {
                [MapProperty(nameof(OrderInput.Name), "Customer.Name")]
                [MapIgnoreTarget("Customer.Version")]
                public static partial OrderDto Map(OrderInput source);
            }
            public static class Scenario {
                public static bool Verify() => OrderMapper.Map(new OrderInput("Ada")).Customer is { Name: "Ada", Version: 42 };
            }
            """);
    }
}
