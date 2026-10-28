using Riok.Mapperly.Abstractions;

namespace Mapperly.UnmappedTarget;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class InvalidMapping
{
    public static partial Target Map(Source source);
}
