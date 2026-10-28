using System.Text.Json;
using MappingComparison.Facet;
using Xunit;

namespace MappingComparison.Tests;

public sealed class DtoMappingTests
{
    [Theory]
    [MemberData(nameof(ComparisonCases.Engines), MemberType = typeof(ComparisonCases))]
    public void Maps_immutable_dto_with_renamed_member_and_nested_values(MappingEngine engine)
    {
        var result = JsonSerializer.SerializeToElement(ComparisonCases.Map(engine, ComparisonCases.CreatePerson()));

        Assert.Equal(42, result.GetProperty("Id").GetInt32());
        Assert.Equal("Ada Lovelace", result.GetProperty("DisplayName").GetString());
        Assert.Equal("Kyiv", result.GetProperty("Address").GetProperty("City").GetString());
        Assert.Equal("Main Street", result.GetProperty("Address").GetProperty("Street").GetString());
        Assert.Equal(["reader", "author"], result.GetProperty("Tags").EnumerateArray().Select(tag => tag.GetString()));
        var previousAddress = Assert.Single(result.GetProperty("PreviousAddresses").EnumerateArray());
        Assert.Equal("Lviv", previousAddress.GetProperty("City").GetString());
        Assert.Equal("Old Street", previousAddress.GetProperty("Street").GetString());
        Assert.Equal(
            ["Address", "DisplayName", "Id", "PreviousAddresses", "Tags"],
            result.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
    }

    [Theory]
    [MemberData(nameof(ComparisonCases.Engines), MemberType = typeof(ComparisonCases))]
    public void Null_root_returns_null_at_the_declared_mapping_boundary(MappingEngine engine)
    {
        Assert.Null(ComparisonCases.Map(engine, null));
    }

    [Theory]
    [MemberData(nameof(ComparisonCases.Engines), MemberType = typeof(ComparisonCases))]
    public void Null_nested_dto_is_preserved(MappingEngine engine)
    {
        var source = ComparisonCases.CreatePerson() with { Address = null };

        var result = JsonSerializer.SerializeToElement(ComparisonCases.Map(engine, source));

        Assert.Equal(JsonValueKind.Null, result.GetProperty("Address").ValueKind);
    }

    [Theory]
    [MemberData(nameof(ComparisonCases.Engines), MemberType = typeof(ComparisonCases))]
    public void Null_collections_expose_each_engines_policy(MappingEngine engine)
    {
        var source = ComparisonCases.CreatePerson() with { Tags = null, PreviousAddresses = null };

        var result = JsonSerializer.SerializeToElement(ComparisonCases.Map(engine, source));

        foreach (var propertyName in new[] { "Tags", "PreviousAddresses" })
        {
            var collection = result.GetProperty(propertyName);
            if (engine == MappingEngine.AutoMapper)
            {
                Assert.Equal(JsonValueKind.Array, collection.ValueKind);
                Assert.Empty(collection.EnumerateArray());
            }
            else
            {
                Assert.Equal(JsonValueKind.Null, collection.ValueKind);
            }
        }
    }

    [Theory]
    [MemberData(nameof(ComparisonCases.Engines), MemberType = typeof(ComparisonCases))]
    public void Empty_collections_remain_empty(MappingEngine engine)
    {
        var source = ComparisonCases.CreatePerson() with { Tags = [], PreviousAddresses = [] };

        var result = JsonSerializer.SerializeToElement(ComparisonCases.Map(engine, source));

        Assert.Empty(result.GetProperty("Tags").EnumerateArray());
        Assert.Empty(result.GetProperty("PreviousAddresses").EnumerateArray());
    }

    [Theory]
    [MemberData(nameof(ComparisonCases.Engines), MemberType = typeof(ComparisonCases))]
    public void Mutating_source_tags_reveals_reference_sharing(MappingEngine engine)
    {
        var source = ComparisonCases.CreatePerson();
        var result = ComparisonCases.Map(engine, source);
        var tags = Assert.IsType<List<string>>(ComparisonCases.GetTags(result));

        source.Tags!.Add("later mutation");

        if (ComparisonCases.SharesTags(engine))
        {
            Assert.Same(source.Tags, tags);
            Assert.Contains("later mutation", tags);
        }
        else
        {
            Assert.NotSame(source.Tags, tags);
            Assert.DoesNotContain("later mutation", tags);
        }
    }

    [Theory]
    [MemberData(nameof(ComparisonCases.Engines), MemberType = typeof(ComparisonCases))]
    public void Nested_collection_conversion_creates_an_independent_list(MappingEngine engine)
    {
        var source = ComparisonCases.CreatePerson();
        var result = ComparisonCases.Map(engine, source);

        source.PreviousAddresses!.Clear();

        var serialized = JsonSerializer.SerializeToElement(result);
        Assert.Single(serialized.GetProperty("PreviousAddresses").EnumerateArray());
    }

    [Fact]
    public void Facet_renamed_positional_parameter_initializes_the_declared_property()
    {
        var facet = new PersonFacet(7, "Constructor value", null, null, null);

        Assert.Equal("Constructor value", facet.DisplayName);
        Assert.Equal("Changed value", (facet with { DisplayName = "Changed value" }).DisplayName);
    }

    [Fact]
    public void Facet_generated_record_round_trips_through_json()
    {
        var original = new PersonFacet(ComparisonCases.CreatePerson());
        var serialized = JsonSerializer.Serialize(original);

        var received = JsonSerializer.Deserialize<PersonFacet>(serialized);

        Assert.NotNull(received);
        Assert.Equal(serialized, JsonSerializer.Serialize(received));
    }
}
