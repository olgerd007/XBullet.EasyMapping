using MappingComparison.Contracts;

namespace MappingComparison.Benchmarks;

// Frozen from the generated PersonMappings body recorded in the initial performance
// evidence. Keep this implementation unchanged for same-run before/after comparisons.
public static class PreOptimizationMappings
{
    public static PersonDto? ToDto(PersonInput? source)
    {
        if (source is null) { return default; }
        return new PersonDto(
            Id: source.Id,
            DisplayName: source.Name,
            Address: source.Address is { } address ? MapAddress(address) : default,
            Tags: source.Tags is { } tags
                ? Enumerable.ToList(Enumerable.Select(tags, item => item)) : default,
            PreviousAddresses: source.PreviousAddresses is { } addresses
                ? Enumerable.ToList(Enumerable.Select(addresses, item => MapAddress(item))) : default);
    }

    private static AddressDto MapAddress(AddressInput source)
    {
        if (source is null) { throw new ArgumentNullException(nameof(source)); }
        return new AddressDto(City: source.City, Street: source.Street);
    }
}
