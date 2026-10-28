using MappingComparison.Contracts;
using Xunit;

namespace MappingComparison.Tests;

public sealed class DtoUpdateTests
{
    [Theory]
    [MemberData(nameof(ComparisonCases.Engines), MemberType = typeof(ComparisonCases))]
    public void Updates_only_allowed_fields_on_existing_dto(MappingEngine engine)
    {
        var target = ComparisonCases.CreateProfile();
        var source = new ProfileUpdate("Updated name", ["updated"]);

        ComparisonCases.Update(engine, source, target);

        Assert.Equal("Updated name", target.Name);
        Assert.Equal(["updated"], target.Tags);
        Assert.Equal(42, target.Id);
        Assert.Equal("preserve this value", target.ServerNote);
    }

    [Theory]
    [MemberData(nameof(ComparisonCases.Engines), MemberType = typeof(ComparisonCases))]
    public void Null_update_values_clear_fields_rather_than_implying_omission(MappingEngine engine)
    {
        var target = ComparisonCases.CreateProfile();

        ComparisonCases.Update(engine, new ProfileUpdate(null, null), target);

        Assert.Null(target.Name);
        if (engine == MappingEngine.AutoMapper)
        {
            Assert.NotNull(target.Tags);
            Assert.Empty(target.Tags);
        }
        else
        {
            Assert.Null(target.Tags);
        }

        Assert.Equal(42, target.Id);
        Assert.Equal("preserve this value", target.ServerNote);
    }

    [Theory]
    [MemberData(nameof(ComparisonCases.Engines), MemberType = typeof(ComparisonCases))]
    public void Updates_expose_source_list_aliasing(MappingEngine engine)
    {
        var target = ComparisonCases.CreateProfile();
        var source = new ProfileUpdate("Updated", ["new"]);

        ComparisonCases.Update(engine, source, target);
        source.Tags!.Add("later mutation");

        if (ComparisonCases.SharesTags(engine))
        {
            Assert.Same(source.Tags, target.Tags);
            Assert.Contains("later mutation", target.Tags!);
        }
        else
        {
            Assert.NotSame(source.Tags, target.Tags);
            Assert.DoesNotContain("later mutation", target.Tags!);
        }
    }

    [Theory]
    [MemberData(nameof(ComparisonCases.Engines), MemberType = typeof(ComparisonCases))]
    public void Updates_expose_whether_existing_list_identity_is_preserved(MappingEngine engine)
    {
        var target = ComparisonCases.CreateProfile();
        var originalList = target.Tags;

        ComparisonCases.Update(engine, new ProfileUpdate("Updated", ["replacement"]), target);

        if (engine is MappingEngine.AutoMapper or MappingEngine.AutoMapperNullableCollections)
        {
            Assert.Same(originalList, target.Tags);
            Assert.Equal(["replacement"], originalList);
        }
        else
        {
            Assert.NotSame(originalList, target.Tags);
            Assert.Equal(["original"], originalList);
        }
    }
}
