using Facet;
using MappingComparison.Contracts;

namespace MappingComparison.Facet;

[Facet(typeof(PersonInput),
    Include = [nameof(PersonInput.Id), nameof(PersonInput.Name), nameof(PersonInput.Address),
        nameof(PersonInput.Tags), nameof(PersonInput.PreviousAddresses)],
    NestedFacets = [typeof(AddressFacet)],
    GenerateToSource = false, GenerateProjection = false)]
public sealed partial record PersonFacet
{
    [MapFrom(nameof(PersonInput.Name))]
    // Facet supplies this positional record parameter in the generated partial declaration.
    public string DisplayName { get; init; } = DisplayName;
}
