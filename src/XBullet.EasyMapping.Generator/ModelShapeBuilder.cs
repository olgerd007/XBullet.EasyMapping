using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace XBullet.EasyMapping.Generator;

internal sealed class ModelShapeBuilder
{
    private readonly CancellationToken _cancellationToken;
    private readonly HashSet<ITypeSymbol> _visited = new(SymbolEqualityComparer.IncludeNullability);
    private readonly HashSet<INamedTypeSymbol> _activeDefinitions = new(SymbolEqualityComparer.Default);
    private readonly SortedDictionary<string, string> _models = new(StringComparer.Ordinal);

    private ModelShapeBuilder(CancellationToken cancellationToken) => _cancellationToken = cancellationToken;

    public static ModelApprovalSnapshot? Create(INamedTypeSymbol mapper, IMethodSymbol[] methods, CancellationToken cancellationToken)
    {
        var attribute = mapper.GetAttributes().First(candidate =>
            candidate.AttributeClass?.ToDisplayString() == "XBullet.EasyMapping.MapperAttribute");
        if (methods.Length == 0 || !attribute.NamedArguments.Any(argument =>
            argument.Key == "RequireModelApproval" && argument.Value.Value is true)) { return null; }

        var builder = new ModelShapeBuilder(cancellationToken);
        var content = new StringBuilder("Mapper model approval v1\n");
        content.Append("mapper ").Append(SymbolNames.Type(mapper)).Append('\n');
        var coverage = attribute.NamedArguments.FirstOrDefault(argument => argument.Key == "Coverage").Value.Value;
        content.Append("coverage ").Append(coverage is 1 ? "Both" : "Target").Append('\n');

        foreach (var method in methods)
        {
            cancellationToken.ThrowIfCancellationRequested();
            content.Append("mapping ").Append(method.IsStatic ? "static " : "instance ")
                .Append(SymbolNames.Type(method.ReturnType)).Append(' ').Append(method.Name).Append('(')
                .Append(string.Join(", ", method.Parameters.Select(Parameter))).Append(")\n");
            foreach (var configuration in Attributes(method)) { content.Append("  ").Append(configuration).Append('\n'); }
            foreach (var parameter in method.Parameters) { builder.Visit(parameter.Type, forceModel: true); }
            if (!method.ReturnsVoid) { builder.Visit(method.ReturnType, forceModel: true); }
        }

        foreach (var model in builder._models.Values) { content.Append(model); }
        var diagnostic = new MappingDiagnostic(MappingDiagnostics.ModelApproval,
            mapper.Locations.First(location => location.IsInSource), "Model approval required");
        return new ModelApprovalSnapshot(mapper.ToDisplayString(), content.ToString(), diagnostic,
            attribute.ApplicationSyntaxReference?.SyntaxTree.FilePath ?? "");
    }

    private void Visit(ITypeSymbol type, bool forceModel = false)
    {
        _cancellationToken.ThrowIfCancellationRequested();
        type = SymbolNames.NonNullable(type);
        if (type is IArrayTypeSymbol array) { Visit(array.ElementType); return; }
        if (type is not INamedTypeSymbol named) { return; }
        if (named.SpecialType != SpecialType.None) { return; }
        if (!forceModel && IsPlatformType(named))
        {
            foreach (var argument in named.TypeArguments) { Visit(argument); }
            return;
        }
        if (_visited.Contains(type)) { return; }
        // A legal Node<T> -> Node<Node<T>> property must not expand forever.
        if (!_activeDefinitions.Add(named.OriginalDefinition))
        {
            foreach (var argument in named.TypeArguments) { Visit(argument); }
            return;
        }
        _visited.Add(type);
        foreach (var argument in named.TypeArguments) { Visit(argument); }

        var definition = new StringBuilder("model ").Append(SymbolNames.Type(named)).Append(" kind=")
            .Append(named.TypeKind).Append(" record=").Append(named.IsRecord).Append(" readonly=").Append(named.IsReadOnly)
            .Append(" abstract=").Append(named.IsAbstract).Append(" sealed=").Append(named.IsSealed);
        if (named.TypeArguments.Length > 0) { definition.Append(" arguments=").Append(NullabilityShape(named)); }
        definition.Append('\n');
        if (named.EnumUnderlyingType is { } underlyingType)
        {
            definition.Append("  underlying ").Append(SymbolNames.Type(underlyingType)).Append('\n');
        }
        if (named.BaseType is { } parent)
        {
            definition.Append("  base ").Append(SymbolNames.Type(parent)).Append('\n');
            Visit(parent);
        }
        foreach (var contract in named.Interfaces.OrderBy(SymbolNames.Type, StringComparer.Ordinal))
        {
            definition.Append("  interface ").Append(SymbolNames.Type(contract)).Append('\n');
        }
        AppendAttributes(definition, named, "  ");
        foreach (var property in SymbolNames.Properties(named))
        {
            definition.Append("  property ").Append(property.Name).Append(" : ").Append(SymbolNames.Type(property.Type))
                .Append(" nullable=").Append(property.NullableAnnotation).Append(" get=").Append(Accessor(property.GetMethod))
                .Append(" set=").Append(Accessor(property.SetMethod)).Append(" required=").Append(property.IsRequired);
            if (property.IsIndexer) { definition.Append(" index=(").Append(string.Join(", ", property.Parameters.Select(Parameter))).Append(')'); }
            definition.Append('\n');
            AppendAttributes(definition, property, "    ");
            Visit(property.Type);
        }
        for (var current = named; current is not null; current = current.BaseType)
        {
            foreach (var field in current.GetMembers().OfType<IFieldSymbol>().Where(field =>
                field.DeclaredAccessibility == Accessibility.Public && (!field.IsStatic || field.HasConstantValue))
                .OrderBy(field => field.Name, StringComparer.Ordinal))
            {
                definition.Append("  field ").Append(field.Name).Append(" : ").Append(SymbolNames.Type(field.Type))
                    .Append(" readonly=").Append(field.IsReadOnly).Append(" static=").Append(field.IsStatic);
                if (field.HasConstantValue) { definition.Append(" constant=").Append(Constant(field.ConstantValue)); }
                definition.Append('\n');
                AppendAttributes(definition, field, "    ");
                Visit(field.Type);
            }
        }
        foreach (var constructor in named.InstanceConstructors.Where(constructor => constructor.DeclaredAccessibility == Accessibility.Public)
            .OrderBy(constructor => string.Join(", ", constructor.Parameters.Select(Parameter)), StringComparer.Ordinal))
        {
            definition.Append("  constructor (").Append(string.Join(", ", constructor.Parameters.Select(Parameter))).Append(")\n");
            AppendAttributes(definition, constructor, "    ");
            foreach (var parameter in constructor.Parameters) { Visit(parameter.Type); }
        }
        // Type display text does not distinguish oblivious string from non-nullable string.
        _models.Add(SymbolNames.Type(named) + "|" + NullabilityShape(named), definition.ToString());
        _activeDefinitions.Remove(named.OriginalDefinition);
    }

    private static bool IsPlatformType(INamedTypeSymbol type)
    {
        // Framework types are opaque leaves; locally declared DTOs are still inspected.
        if (type.Locations.Any(location => location.IsInSource)) { return false; }
        var assembly = type.ContainingAssembly.Identity.Name;
        return assembly is "mscorlib" or "netstandard" or "System" || assembly.StartsWith("System.", StringComparison.Ordinal);
    }

    private static string Accessor(IMethodSymbol? method) => method is null ? "none" :
        method.DeclaredAccessibility + (method.IsInitOnly ? " init" : "");

    private static string NullabilityShape(ITypeSymbol type) => type.NullableAnnotation + (type switch
    {
        IArrayTypeSymbol array => "[" + NullabilityShape(array.ElementType) + "]",
        INamedTypeSymbol { TypeArguments.Length: > 0 } named => "<" + string.Join(",", named.TypeArguments.Select(NullabilityShape)) + ">",
        _ => ""
    });

    private static string Parameter(IParameterSymbol parameter) => parameter.RefKind + " " + SymbolNames.Type(parameter.Type) +
        " " + parameter.Name + " nullable=" + parameter.NullableAnnotation + (parameter.IsParams ? " params" : "") +
        (parameter.HasExplicitDefaultValue ? " default=" + Constant(parameter.ExplicitDefaultValue) : parameter.IsOptional ? " optional" : "") +
        string.Concat(Attributes(parameter).Select(attribute => " " + attribute));

    private static void AppendAttributes(StringBuilder target, ISymbol symbol, string indent)
    {
        foreach (var attribute in Attributes(symbol)) { target.Append(indent).Append(attribute).Append('\n'); }
    }

    private static IEnumerable<string> Attributes(ISymbol symbol) => symbol.GetAttributes()
        .Where(attribute => attribute.AttributeClass is not null &&
            attribute.AttributeClass.ContainingNamespace.ToDisplayString() != "System.Runtime.CompilerServices")
        .Select(attribute => "attribute " + SymbolNames.Type(attribute.AttributeClass!) + "(" +
            string.Join(", ", attribute.ConstructorArguments.Select(Argument).Concat(attribute.NamedArguments
                .OrderBy(argument => argument.Key, StringComparer.Ordinal).Select(argument => argument.Key + "=" + Argument(argument.Value)))) + ")")
        .OrderBy(attribute => attribute, StringComparer.Ordinal);

    private static string Argument(TypedConstant argument) => argument.Kind switch
    {
        TypedConstantKind.Array => argument.IsNull ? "null" : "[" + string.Join(", ", argument.Values.Select(Argument)) + "]",
        TypedConstantKind.Type => argument.Value is ITypeSymbol type ? "typeof(" + SymbolNames.Type(type) + ")" : "null",
        _ => Constant(argument.Value)
    };

    private static string Constant(object? value) => value switch
    {
        null => "null",
        string text => SymbolDisplay.FormatLiteral(text, quote: true),
        char character => SymbolDisplay.FormatLiteral(character, quote: true),
        bool flag => flag ? "true" : "false",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString()
    } ?? "null";
}
