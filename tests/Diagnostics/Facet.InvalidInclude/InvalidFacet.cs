using Facet;

namespace Facet.InvalidInclude;

[Facet(typeof(Source), Include = [nameof(Source.Id), "Missing"],
    GenerateToSource = false, GenerateProjection = false)]
public partial record InvalidFacet;
