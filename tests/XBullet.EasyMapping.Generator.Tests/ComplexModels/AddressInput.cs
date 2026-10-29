namespace XBullet.EasyMapping.Generator.Tests.ComplexModels;

public sealed class AddressInput
{
    public required string City
    {
        get; set;
    }

    public List<string>? Labels
    {
        get; set;
    }
}
