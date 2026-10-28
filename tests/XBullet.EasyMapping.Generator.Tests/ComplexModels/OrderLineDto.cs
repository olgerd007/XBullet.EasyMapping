namespace XBullet.EasyMapping.Generator.Tests.ComplexModels;

public sealed record OrderLineDto(
    string Sku,
    Money UnitPrice,
    long Quantity,
    IReadOnlyList<DiscountDto?>? Discounts,
    string Reference);
