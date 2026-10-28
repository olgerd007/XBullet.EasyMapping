using Facet;
using MappingComparison.Contracts;

namespace MappingComparison.Facet;

[Facet(typeof(EditableProfile),
    Include = [nameof(EditableProfile.Name), nameof(EditableProfile.Tags)],
    GenerateToSource = true, GenerateProjection = false)]
public sealed partial class ProfileUpdateFacet;
