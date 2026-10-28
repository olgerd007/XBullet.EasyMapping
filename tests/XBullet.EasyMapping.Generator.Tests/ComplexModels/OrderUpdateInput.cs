namespace XBullet.EasyMapping.Generator.Tests.ComplexModels;

public sealed record OrderUpdateInput(CustomerInput? Customer, List<OrderLineInput>? Lines, List<string?>? Notes);
