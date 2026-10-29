namespace XBullet.EasyMapping.Generator.Tests.ComplexModels;

public sealed class MutableOrderDto
{
    public OrderId Id
    {
        get; init;
    }

    public OrderStatus Status
    {
        get; init;
    }

    public int Version
    {
        get; set;
    }

    public CustomerDto? Customer
    {
        get; set;
    }

    public IReadOnlyList<OrderLineDto>? Lines
    {
        get; set;
    }

    public IReadOnlyCollection<string?>? Notes
    {
        get; set;
    }
}
