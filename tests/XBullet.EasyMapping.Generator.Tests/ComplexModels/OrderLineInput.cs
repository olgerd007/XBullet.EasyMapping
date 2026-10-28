namespace XBullet.EasyMapping.Generator.Tests.ComplexModels;

public sealed record OrderLineInput(
    string Code,
    decimal UnitPrice,
    int Quantity,
    List<DiscountInput?>? Discounts,
    ProductCode? Reference);
