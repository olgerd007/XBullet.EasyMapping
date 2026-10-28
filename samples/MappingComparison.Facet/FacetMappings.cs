using MappingComparison.Contracts;

namespace MappingComparison.Facet;

public static class FacetMappings
{
    // The generated constructor accepts a non-null source; this boundary defines root null behavior.
    public static PersonFacet? ToDto(PersonInput? source) => source is null ? null : new PersonFacet(source);
}
