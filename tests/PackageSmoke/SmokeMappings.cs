using XBullet.EasyMapping;

[Mapper(Coverage = MappingCoverage.Both, RequireModelApproval = true)]
public static partial class SmokeMappings
{
    [MapProperty(nameof(PersonInput.Name), nameof(PersonDto.DisplayName))]
    [MapIgnoreSource(nameof(PersonInput.InternalNote))]
    public static partial PersonDto Map(PersonInput source);

    [MapProperty(nameof(PersonDto.DisplayName), Use = nameof(FormatDisplayName), Sources = new[] { nameof(PersonInput.Id), nameof(PersonInput.Name) })]
    [MapIgnoreSource(nameof(PersonInput.InternalNote))]
    public static partial PersonDto MapWithWholeSource(PersonInput source);

    private static string FormatDisplayName(PersonInput source) => $"{source.Id}:{source.Name}";
}
