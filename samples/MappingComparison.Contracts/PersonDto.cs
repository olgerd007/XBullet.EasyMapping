namespace MappingComparison.Contracts;

public sealed record PersonDto(
    int Id,
    string DisplayName,
    AddressDto? Address,
    List<string>? Tags,
    List<AddressDto>? PreviousAddresses);
