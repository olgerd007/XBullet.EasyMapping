using System.Diagnostics;
using System.Reflection;
using XBullet.EasyMapping;
using Xunit;

namespace XBullet.EasyMapping.Generator.Tests;

public sealed class AttributeContractTests
{
    [Fact]
    public void Mapper_defaults_preserve_existing_target_coverage_and_optional_approval()
    {
        var attribute = new MapperAttribute();

        Assert.Equal(MappingCoverage.Target, attribute.Coverage);
        Assert.False(attribute.RequireModelApproval);

        attribute.Coverage = MappingCoverage.Both;
        attribute.RequireModelApproval = true;

        Assert.Equal(MappingCoverage.Both, attribute.Coverage);
        Assert.True(attribute.RequireModelApproval);
    }

    [Fact]
    public void Mapping_attributes_preserve_declared_property_names()
    {
        var property = new MapPropertyAttribute("Input", "Output");

        Assert.Equal("Input", property.Source);
        Assert.Equal("Output", property.Target);
        Assert.Null(property.Use);
        property.Use = "MapCustomerStatus";
        Assert.Equal("MapCustomerStatus", property.Use);
        Assert.Equal("UnusedInput", new MapIgnoreSourceAttribute("UnusedInput").Source);
        Assert.Equal("UnusedOutput", new MapIgnoreTargetAttribute("UnusedOutput").Target);
    }

    [Theory]
    [InlineData(typeof(MapperAttribute), AttributeTargets.Class, false)]
    [InlineData(typeof(MapPropertyAttribute), AttributeTargets.Method, true)]
    [InlineData(typeof(MapConstantAttribute), AttributeTargets.Method, true)]
    [InlineData(typeof(MapGeneratedValueAttribute), AttributeTargets.Method, true)]
    [InlineData(typeof(MapIgnoreSourceAttribute), AttributeTargets.Method, true)]
    [InlineData(typeof(MapIgnoreTargetAttribute), AttributeTargets.Method, true)]
    public void Mapping_metadata_is_compile_time_only_unless_explicitly_preserved(
        Type attributeType, AttributeTargets targets, bool allowMultiple)
    {
        var usage = attributeType.GetCustomAttribute<AttributeUsageAttribute>();

        Assert.NotNull(usage);
        Assert.Equal(targets, usage.ValidOn);
        Assert.Equal(allowMultiple, usage.AllowMultiple);
        Assert.False(usage.Inherited);
        Assert.Equal("MAPPER_PRESERVE_ATTRIBUTES",
            attributeType.GetCustomAttribute<ConditionalAttribute>()?.ConditionString);
    }

    [Fact]
    public void Value_attributes_preserve_the_target_constant_and_helper_name()
    {
        var constant = new MapConstantAttribute("SourceSystem", "orders");
        Assert.Equal("SourceSystem", constant.Target);
        Assert.Equal("orders", constant.Value);
        Assert.Null(new MapConstantAttribute("CustomerName", null).Value);

        var generated = new MapGeneratedValueAttribute("Id");
        Assert.Equal("Id", generated.Target);
        Assert.Null(generated.Use);
        generated.Use = "CreateOrderId";
        Assert.Equal("CreateOrderId", generated.Use);
    }
}
