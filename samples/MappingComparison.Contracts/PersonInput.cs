namespace MappingComparison.Contracts;

public sealed record PersonInput(
    int Id,
    string Name,
    AddressInput? Address,
    List<string>? Tags,
    List<AddressInput>? PreviousAddresses,
    string InternalNote);
