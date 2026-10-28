namespace XBullet.EasyMapping.Generator.Tests.ComplexModels;

public abstract class OrderDtoBase
{
    protected OrderDtoBase(OrderId id) => Id = id;

    public OrderId Id { get; }
}
