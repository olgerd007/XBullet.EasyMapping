using MappingComparison.Contracts;
using XBullet.EasyMapping;

namespace MappingComparison.EasyMapping;

[Mapper(Coverage = MappingCoverage.Both, RequireModelApproval = true)]
public static partial class PersonMappings
{
    [MapProperty(nameof(PersonInput.Name), nameof(PersonDto.DisplayName))]
    [MapIgnoreSource(nameof(PersonInput.InternalNote))]
    public static partial PersonDto? ToDto(PersonInput? source);

    [MapIgnoreTarget(nameof(EditableProfile.Id))]
    [MapIgnoreTarget(nameof(EditableProfile.ServerNote))]
    public static partial void Update(ProfileUpdate source, EditableProfile target);

    private static partial AddressDto MapAddress(AddressInput source);
}
