using MappingComparison.AutoMapper;
using MappingComparison.Contracts;
using MappingComparison.EasyMapping;
using MappingComparison.Facet;
using MappingComparison.Manual;
using MappingComparison.Mapperly;
using Xunit;

namespace MappingComparison.Tests;

public static class ComparisonCases
{
    public static TheoryData<MappingEngine> Engines
    {
        get
        {
            var engines = new TheoryData<MappingEngine>();
            foreach (var engine in Enum.GetValues<MappingEngine>())
            {
                engines.Add(engine);
            }

            return engines;
        }
    }

    public static PersonInput CreatePerson() => new(
        42,
        "Ada Lovelace",
        new AddressInput("Kyiv", "Main Street"),
        ["reader", "author"],
        [new AddressInput("Lviv", "Old Street")],
        "must not leave the source DTO");

    public static EditableProfile CreateProfile() => new()
    {
        Id = 42,
        Name = "Original name",
        Tags = ["original"],
        ServerNote = "preserve this value"
    };

    public static object? Map(MappingEngine engine, PersonInput? source) => engine switch
    {
        MappingEngine.Manual => ManualMappings.ToDto(source),
        MappingEngine.AutoMapper => new AutoMapperMappings().ToDto(source),
        MappingEngine.AutoMapperNullableCollections => new AutoMapperMappings(allowNullCollections: true).ToDto(source),
        MappingEngine.Mapperly => DtoMappings.ToDto(source),
        MappingEngine.MapperlyCopying => CopyingDtoMappings.ToDto(source),
        MappingEngine.Facet => FacetMappings.ToDto(source),
        MappingEngine.EasyMapping => PersonMappings.ToDto(source),
        _ => throw new ArgumentOutOfRangeException(nameof(engine))
    };

    public static List<string>? GetTags(object? result) => result switch
    {
        PersonDto dto => dto.Tags,
        PersonFacet facet => facet.Tags,
        _ => throw new ArgumentException("Expected a mapped person DTO.", nameof(result))
    };

    public static bool SharesTags(MappingEngine engine) =>
        engine is MappingEngine.Mapperly or MappingEngine.Facet;

    public static void Update(MappingEngine engine, ProfileUpdate source, EditableProfile target)
    {
        switch (engine)
        {
            case MappingEngine.Manual:
                ManualMappings.Update(source, target);
                break;
            case MappingEngine.AutoMapper:
                new AutoMapperMappings().Update(source, target);
                break;
            case MappingEngine.AutoMapperNullableCollections:
                new AutoMapperMappings(allowNullCollections: true).Update(source, target);
                break;
            case MappingEngine.Mapperly:
                DtoMappings.Update(source, target);
                break;
            case MappingEngine.MapperlyCopying:
                CopyingDtoMappings.Update(source, target);
                break;
            case MappingEngine.Facet:
                // Facet owns a different input contract; this test-only bridge makes that cost explicit.
                new ProfileUpdateFacet { Name = source.Name, Tags = source.Tags }.ApplyToSource(target);
                break;
            case MappingEngine.EasyMapping:
                PersonMappings.Update(source, target);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(engine));
        }
    }
}
