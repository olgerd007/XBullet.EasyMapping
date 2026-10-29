using System.Collections.Immutable;

namespace XBullet.EasyMapping.Generator;

// Only value data crosses the incremental pipeline boundary; symbols stay in semantic analysis.
internal sealed class GeneratedMapper : IEquatable<GeneratedMapper>
{
    public GeneratedMapper(string hintName, string source, ImmutableArray<MappingDiagnostic> diagnostics,
        ModelApprovalSnapshot? approval = null)
    {
        HintName = hintName;
        Source = source;
        Diagnostics = diagnostics;
        Approval = approval;
    }

    public string HintName
    {
        get;
    }
    public string Source
    {
        get;
    }
    public ImmutableArray<MappingDiagnostic> Diagnostics
    {
        get;
    }
    public ModelApprovalSnapshot? Approval
    {
        get;
    }

    public bool Equals(GeneratedMapper? other) => other is not null &&
        HintName == other.HintName && Source == other.Source && Diagnostics.SequenceEqual(other.Diagnostics) &&
        Equals(Approval, other.Approval);

    public override bool Equals(object? obj) => Equals(obj as GeneratedMapper);

    public override int GetHashCode() => HintName.GetHashCode();
}
