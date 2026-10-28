using XBullet.EasyMapping;

[Mapper(Coverage = MappingCoverage.Both, RequireModelApproval = true)]
public static partial class NullableSmokeMappings
{
    [MapProperty(nameof(PersonInput.Name), nameof(PersonDto.DisplayName))]
    [MapIgnoreSource(nameof(PersonInput.InternalNote))]
    public static partial PersonDto? Map(PersonInput? source);
}
