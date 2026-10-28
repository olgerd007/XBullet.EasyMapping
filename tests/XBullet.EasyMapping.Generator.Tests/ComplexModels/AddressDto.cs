namespace XBullet.EasyMapping.Generator.Tests.ComplexModels;

public sealed record AddressDto(string City, IReadOnlyList<string>? Labels);
