namespace XBullet.EasyMapping.Generator.Tests.ComplexModels;

public sealed class OrderInput : OrderInputBase
{
    public required string Name
    {
        get; init;
    }

    public DateTimeOffset CreatedAt
    {
        get; init;
    }

    public OrderStatus Status
    {
        get; init;
    }

    public CustomerInput? Customer
    {
        get; init;
    }

    public List<OrderLineInput>? Lines
    {
        get; init;
    }

    public List<string?>? Notes
    {
        get; init;
    }

    public List<List<int?>?>? Batches
    {
        get; init;
    }
}
