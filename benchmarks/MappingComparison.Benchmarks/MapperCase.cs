using MappingComparison.Contracts;

namespace MappingComparison.Benchmarks;

public sealed record MapperCase(
    string Name,
    bool SharesTags,
    bool NormalizesNullCollections,
    Func<PersonInput?, object?> Map);
