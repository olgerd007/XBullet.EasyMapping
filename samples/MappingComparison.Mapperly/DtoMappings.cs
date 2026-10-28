using MappingComparison.Contracts;
using Riok.Mapperly.Abstractions;

namespace MappingComparison.Mapperly;

[Mapper(RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class DtoMappings
{
    [MapProperty(nameof(PersonInput.Name), nameof(PersonDto.DisplayName))]
    public static partial PersonDto? ToDto(PersonInput? source);

    [MapperIgnoreTarget(nameof(EditableProfile.Id))]
    [MapperIgnoreTarget(nameof(EditableProfile.ServerNote))]
    public static partial void Update(ProfileUpdate source, EditableProfile target);
}
