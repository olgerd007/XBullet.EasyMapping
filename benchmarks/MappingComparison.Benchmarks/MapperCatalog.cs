using System.Text.Json;
using MappingComparison.AutoMapper;
using MappingComparison.Contracts;
using MappingComparison.EasyMapping;
using MappingComparison.Facet;
using MappingComparison.Manual;
using MappingComparison.Mapperly;

namespace MappingComparison.Benchmarks;

public static class MapperCatalog
{
    public static IReadOnlyList<MapperCase> Create()
    {
        // Configuration, expression compilation and validation stay outside measured work.
        var autoMapper = new AutoMapperMappings();
        var nullableAutoMapper = new AutoMapperMappings(allowNullCollections: true);
        autoMapper.Configuration.CompileMappings();
        nullableAutoMapper.Configuration.CompileMappings();

        return
        [
            new("Manual", false, false, source => ManualMappings.ToDto(source)),
            new("AutoMapper", false, true, source => autoMapper.ToDto(source)),
            new("AutoMapperNullable", false, false, source => nullableAutoMapper.ToDto(source)),
            new("Mapperly", true, false, source => DtoMappings.ToDto(source)),
            new("MapperlyCopying", false, false, source => CopyingDtoMappings.ToDto(source)),
            new("Facet", true, false, source => FacetMappings.ToDto(source)),
            new("EasyMapping", false, false, source => PersonMappings.ToDto(source)),
            new("EasyMappingBefore", false, false, source => PreOptimizationMappings.ToDto(source))
        ];
    }

    public static void Validate(MapperCase mapper, PersonInput source)
    {
        if (mapper.Map(null) is not null)
        {
            throw new InvalidOperationException($"{mapper.Name}: null root was not preserved.");
        }

        var result = mapper.Map(source);
        var actual = JsonSerializer.SerializeToElement(result);
        var expectedDto = ManualMappings.ToDto(source)!;
        if (mapper.NormalizesNullCollections)
        {
            expectedDto = expectedDto with
            {
                Tags = expectedDto.Tags ?? [],
                PreviousAddresses = expectedDto.PreviousAddresses ?? []
            };
        }

        var expected = JsonSerializer.SerializeToElement(expectedDto);
        if (!JsonElement.DeepEquals(actual, expected))
        {
            throw new InvalidOperationException($"{mapper.Name}: mapped values differ from the baseline.");
        }

        var tags = result switch
        {
            PersonDto dto => dto.Tags,
            PersonFacet facet => facet.Tags,
            _ => throw new InvalidOperationException($"{mapper.Name}: unexpected result type.")
        };
        if (source.Tags is not null && ReferenceEquals(source.Tags, tags) != mapper.SharesTags)
        {
            throw new InvalidOperationException($"{mapper.Name}: tag collection ownership changed.");
        }
    }

    public static int GetId(object? result) => result switch
    {
        PersonDto dto => dto.Id,
        PersonFacet facet => facet.Id,
        _ => throw new InvalidOperationException("Mapping returned an unexpected result.")
    };
}
