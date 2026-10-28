namespace XBullet.EasyMapping.Generator.Tests.ComplexModels;

public abstract class OrderInputBase
{
    public OrderId Id { get; init; }

    public string AuditTag { get; init; } = string.Empty;
}
