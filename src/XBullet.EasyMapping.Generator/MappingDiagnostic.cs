using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace XBullet.EasyMapping.Generator;

internal sealed class MappingDiagnostic : IEquatable<MappingDiagnostic>
{
    public MappingDiagnostic(DiagnosticDescriptor descriptor, Location location, params string[] arguments)
    {
        Descriptor = descriptor;
        var span = location.GetLineSpan();
        FilePath = span.Path;
        Span = location.SourceSpan;
        Lines = span.Span;
        Arguments = arguments;
    }

    private DiagnosticDescriptor Descriptor { get; }
    private string FilePath { get; }
    private TextSpan Span { get; }
    private LinePositionSpan Lines { get; }
    private string[] Arguments { get; }

    private MappingDiagnostic(MappingDiagnostic original, string[] arguments)
    {
        Descriptor = original.Descriptor;
        FilePath = original.FilePath;
        Span = original.Span;
        Lines = original.Lines;
        Arguments = arguments;
    }

    public MappingDiagnostic WithArguments(params string[] arguments) => new(this, arguments);

    public Diagnostic ToDiagnostic() => Diagnostic.Create(
        Descriptor, Location.Create(FilePath, Span, Lines), Arguments.Cast<object>().ToArray());

    public bool Equals(MappingDiagnostic? other) => other is not null &&
        Descriptor.Id == other.Descriptor.Id && FilePath == other.FilePath && Span == other.Span &&
        Lines.Equals(other.Lines) && Arguments.SequenceEqual(other.Arguments);

    public override bool Equals(object? obj) => Equals(obj as MappingDiagnostic);

    public override int GetHashCode() => Descriptor.Id.GetHashCode();
}
