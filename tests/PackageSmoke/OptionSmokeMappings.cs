using XBullet.EasyMapping;

[Mapper(AllowReferenceAssignment = true, NullSafeMemberMapping = true)]
public static partial class OptionSmokeMappings
{
    [MapProperty("Child.Value", nameof(OptionSmokeOutput.NestedValue))]
    public static partial OptionSmokeOutput Map(OptionSmokeInput source);
}
