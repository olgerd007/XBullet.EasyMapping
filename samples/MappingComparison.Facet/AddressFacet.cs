using Facet;
using MappingComparison.Contracts;

namespace MappingComparison.Facet;

[Facet(typeof(AddressInput),
    Include = [nameof(AddressInput.City), nameof(AddressInput.Street)],
    GenerateToSource = false, GenerateProjection = false)]
public sealed partial record AddressFacet;
