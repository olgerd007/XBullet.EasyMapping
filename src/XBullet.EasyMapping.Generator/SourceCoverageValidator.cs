using Microsoft.CodeAnalysis;

namespace XBullet.EasyMapping.Generator;

internal sealed class SourceCoverageValidator
{
    private readonly string[] _sourceNames;
    private readonly string _methodName;
    private readonly bool _requireCoverage;
    private readonly Action<DiagnosticDescriptor, string[]> _report;
    private readonly HashSet<string> _mapped = new(StringComparer.Ordinal);
    private readonly HashSet<string> _ignored = new(StringComparer.Ordinal);

    public SourceCoverageValidator(string[] sourceNames, IMethodSymbol method, bool requireCoverage,
        Action<DiagnosticDescriptor, string[]> report)
    {
        _sourceNames = sourceNames;
        _methodName = method.Name;
        _requireCoverage = requireCoverage;
        _report = report;

        foreach (var attribute in method.GetAttributes().Where(attribute =>
            attribute.AttributeClass?.ToDisplayString() == "XBullet.EasyMapping.MapIgnoreSourceAttribute"))
        {
            var name = attribute.ConstructorArguments.Length == 1
                ? attribute.ConstructorArguments[0].Value as string
                : null;
            if (name is null || !sourceNames.Contains(name, StringComparer.Ordinal) || !_ignored.Add(name))
            {
                _report(MappingDiagnostics.Configuration,
                    ["MapIgnoreSource requires an existing readable source property and cannot be repeated"]);
            }
        }
    }

    public void RecordMapped(string sourceName) => _mapped.Add(sourceName);

    public void Validate()
    {
        foreach (var name in _sourceNames)
        {
            if (_mapped.Contains(name) && _ignored.Contains(name))
            {
                _report(MappingDiagnostics.Configuration, [$"Source '{name}' cannot be both mapped and ignored"]);
            }
            else if (_requireCoverage && !_mapped.Contains(name) && !_ignored.Contains(name))
            {
                _report(MappingDiagnostics.UnmappedSource, [name, _methodName]);
            }
        }
    }
}
