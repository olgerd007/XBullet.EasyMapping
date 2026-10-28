namespace XBullet.EasyMapping.Generator.Tests.ComplexModels;

[Mapper(Coverage = MappingCoverage.Both)]
public partial class OrderMapper
{
    private readonly string _missingReference;

    public OrderMapper(string missingReference) => _missingReference = missingReference;

    [MapProperty(nameof(OrderInput.Name), nameof(OrderDto.DisplayName))]
    [MapIgnoreSource(nameof(OrderInput.AuditTag))]
    [MapIgnoreTarget(nameof(OrderDto.Channel))]
    public partial OrderDto Map(OrderInput source);

    [MapIgnoreTarget(nameof(MutableOrderDto.Id))]
    [MapIgnoreTarget(nameof(MutableOrderDto.Status))]
    [MapIgnoreTarget(nameof(MutableOrderDto.Version))]
    public partial void Update(OrderUpdateInput source, MutableOrderDto target);

    private static partial CustomerDto? MapCustomer(CustomerInput? source);

    private static partial AddressDto? MapAddress(AddressInput? source);

    [MapProperty(nameof(OrderLineInput.Code), nameof(OrderLineDto.Sku))]
    private partial OrderLineDto MapLine(OrderLineInput source);

    private static partial DiscountDto? MapDiscount(DiscountInput? source);

    private static Money MapMoney(decimal value) => new(value);

    private string MapReference(ProductCode? value) => value?.Value ?? _missingReference;
}
