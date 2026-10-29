using System.Text;
using Microsoft.CodeAnalysis;

namespace XBullet.EasyMapping.Generator;

internal sealed class SourcePropertyPath
{
    private readonly IPropertySymbol[] _properties;

    public SourcePropertyPath(IPropertySymbol property) : this([property])
    {
    }

    private SourcePropertyPath(IPropertySymbol[] properties) => _properties = properties;

    public string RootName => _properties[0].Name;

    public static SourcePropertyPath? Resolve(IPropertySymbol[] source, string path,
        CancellationToken cancellationToken, out string error)
    {
        var segments = path.Split('.');
        if (segments.Any(segment => segment.Length == 0))
        {
            error = $"Source path '{path}' must contain non-empty property names separated by dots";
            return null;
        }

        var properties = new IPropertySymbol[segments.Length];
        for (var index = 0; index < segments.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var segment = segments[index];
            var matches = source.Where(property => string.Equals(property.Name, segment, StringComparison.OrdinalIgnoreCase)).ToArray();
            var property = matches.FirstOrDefault(candidate => candidate.Name == segment);
            if (property is null || property.IsIndexer || property.GetMethod?.DeclaredAccessibility != Accessibility.Public)
            {
                error = $"Source path '{path}' segment '{segment}' must name an existing public readable instance property; fields and indexers are unsupported";
                return null;
            }

            if (matches.Length > 1)
            {
                error = $"Source path '{path}' segment '{segment}' is ambiguous; property names must not differ only by case or come from competing interface declarations";
                return null;
            }

            properties[index] = property;
            if (index == segments.Length - 1)
            {
                continue;
            }

            var receiverType = SymbolNames.NonNullable(property.Type);
            var nextType = receiverType as INamedTypeSymbol ?? (receiverType is IArrayTypeSymbol ? receiverType.BaseType : null);
            if (nextType is null)
            {
                error = $"Source path '{path}' cannot traverse property '{segment}' of type '{SymbolNames.Type(property.Type)}'";
                return null;
            }

            source = NestedProperties(nextType);
        }

        error = "";
        return new SourcePropertyPath(properties);
    }

    private static IPropertySymbol[] NestedProperties(INamedTypeSymbol type)
    {
        var properties = SymbolNames.Properties(type);
        if (type.TypeKind != TypeKind.Interface)
        {
            return properties;
        }

        // Inherited interface members are not included in GetMembers or BaseType.
        var ownNames = new HashSet<string>(properties.Select(property => property.Name), StringComparer.Ordinal);
        var inherited = type.AllInterfaces.SelectMany(SymbolNames.Properties)
            .Distinct<IPropertySymbol>(SymbolEqualityComparer.Default).ToArray();
        return properties.Concat(inherited.Where(property => !ownNames.Contains(property.Name) &&
            !inherited.Any(other => other.Name == property.Name &&
                other.ContainingType.AllInterfaces.Contains(property.ContainingType, SymbolEqualityComparer.Default)))).ToArray();
    }

    public ITypeSymbol GetResultType(Compilation compilation)
    {
        var type = _properties[_properties.Length - 1].Type;
        if (SymbolNames.IsNullable(type) || !_properties.Take(_properties.Length - 1).Any(property => SymbolNames.IsNullable(property.Type)))
        {
            return type;
        }

        return type.IsValueType
            ? compilation.GetSpecialType(SpecialType.System_Nullable_T).Construct(type).WithNullableAnnotation(NullableAnnotation.Annotated)
            : type.WithNullableAnnotation(NullableAnnotation.Annotated);
    }

    public string Access(string sourceName)
    {
        var expression = new StringBuilder(sourceName);
        for (var index = 0; index < _properties.Length; index++)
        {
            var conditional = index > 0 && SymbolNames.IsNullable(_properties[index - 1].Type);
            expression.Append(conditional ? "?." : ".").Append(SymbolNames.Identifier(_properties[index].Name));
        }

        return expression.ToString();
    }
}
