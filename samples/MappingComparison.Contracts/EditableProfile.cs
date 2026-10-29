namespace MappingComparison.Contracts;

// This is a mutable DTO, not an aggregate whose invariants can be bypassed.
public sealed class EditableProfile
{
    public int Id
    {
        get; init;
    }

    public string? Name
    {
        get; set;
    }

    public List<string>? Tags
    {
        get; set;
    }

    public string ServerNote { get; set; } = string.Empty;
}
