namespace XBullet.EasyMapping.Generator.Tests.ComplexModels;

public sealed class CustomerInput
{
    public required string Name { get; set; }

    public AddressInput? ShippingAddress { get; set; }
}
