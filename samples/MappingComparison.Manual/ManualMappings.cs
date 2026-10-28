using MappingComparison.Contracts;

namespace MappingComparison.Manual;

public static class ManualMappings
{
    public static PersonDto? ToDto(PersonInput? source)
    {
        if (source is null)
        {
            return null;
        }

        return new PersonDto(
            source.Id,
            source.Name,
            source.Address is null ? null : ToDto(source.Address),
            source.Tags is null ? null : [.. source.Tags],
            source.PreviousAddresses?.Select(ToDto).ToList());
    }

    public static void Update(ProfileUpdate source, EditableProfile target)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);

        target.Name = source.Name;
        target.Tags = source.Tags is null ? null : [.. source.Tags];
    }

    private static AddressDto ToDto(AddressInput source) => new(source.City, source.Street);
}
