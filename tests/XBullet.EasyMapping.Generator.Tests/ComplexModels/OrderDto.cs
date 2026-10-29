namespace XBullet.EasyMapping.Generator.Tests.ComplexModels;

public sealed class OrderDto : OrderDtoBase
{
    public OrderDto(OrderId id, string channel = "web") : base(id) => Channel = channel;

    public string Channel
    {
        get;
    }

    public required string DisplayName
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

    public CustomerDto? Customer
    {
        get; init;
    }

    public IReadOnlyList<OrderLineDto>? Lines
    {
        get; init;
    }

    public IReadOnlyCollection<string?>? Notes
    {
        get; init;
    }

    public long?[]?[]? Batches
    {
        get; init;
    }
}
