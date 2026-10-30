using System.Text;
using Microsoft.CodeAnalysis;

namespace XBullet.EasyMapping.Generator;

internal sealed class MappingValueBuilder
{
    private readonly Compilation _compilation;
    private readonly ConversionBuilder _conversion;
    private readonly SourceCoverageValidator _coverage;
    private readonly string _sourceName;
    private readonly ITypeSymbol _sourceType;
    private readonly string _methodName;
    private readonly Action<DiagnosticDescriptor, string[]> _report;
    private readonly Dictionary<string, string> _generatedLocals = new(StringComparer.OrdinalIgnoreCase);

    public MappingValueBuilder(Compilation compilation, ConversionBuilder conversion, SourceCoverageValidator coverage,
        string sourceName, ITypeSymbol sourceType, string methodName, Action<DiagnosticDescriptor, string[]> report)
    {
        _compilation = compilation;
        _conversion = conversion;
        _coverage = coverage;
        _sourceName = sourceName;
        _sourceType = sourceType;
        _methodName = methodName;
        _report = report;
    }

    public StringBuilder Declarations { get; } = new();

    public string NewName() => _conversion.NewName();

    public string Build(PropertyMapping mapping, ITypeSymbol target, string member)
    {
        if (mapping.UsesWholeSource)
        {
            foreach (var sourceName in mapping.ConsumedSources)
            {
                _coverage.RecordMapped(sourceName);
            }

            return Cache(member, _conversion.Convert(_sourceType, target, _sourceName, member, mapping.Use));
        }

        if (mapping.Source is { } source)
        {
            _coverage.RecordMapped(source.RootName);
            return _conversion.Convert(source.GetResultType(_compilation), target, source.Access(_sourceName), member, mapping.Use);
        }

        if (mapping.Constant is { } constant)
        {
            if (ConstantValueBuilder.TryBuild(constant, target, out var expression, out var error))
            {
                return constant.Kind == TypedConstantKind.Array && !constant.IsNull ? Cache(member, expression) : expression;
            }

            _report(MappingDiagnostics.Configuration, [$"MapConstant for target '{member}' in mapping '{_methodName}': {error}"]);
            return "default!";
        }

        return Cache(member, _conversion.GenerateValue(target, member, mapping.Use!));
    }

    private string Cache(string member, string expression)
    {
        if (_generatedLocals.TryGetValue(member, out var local))
        {
            return local;
        }

        local = _conversion.NewName();
        _generatedLocals.Add(member, local);
        // A required member can also be a constructor argument; use the same value in both.
        Declarations.Append("var ").Append(local).Append(" = ").Append(expression).AppendLine(";");
        return local;
    }
}
