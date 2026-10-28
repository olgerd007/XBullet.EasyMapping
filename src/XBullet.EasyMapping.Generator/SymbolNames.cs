using Microsoft.CodeAnalysis;

namespace XBullet.EasyMapping.Generator;

internal static class SymbolNames
{
    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.EscapeKeywordIdentifiers |
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier |
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes);

    public static string Type(ITypeSymbol type) => type.ToDisplayString(TypeFormat);

    public static string Identifier(string name) => "@" + name;

    public static bool IsNullable(ITypeSymbol type) => type.NullableAnnotation == NullableAnnotation.Annotated ||
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };

    public static ITypeSymbol NonNullable(ITypeSymbol type) =>
        type is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nullable
            ? nullable.TypeArguments[0]
            : type.WithNullableAnnotation(NullableAnnotation.NotAnnotated);

    public static IPropertySymbol[] Properties(INamedTypeSymbol type)
    {
        var properties = new Dictionary<string, IPropertySymbol>(StringComparer.Ordinal);
        for (var current = type; current is not null; current = current.BaseType)
        {
            foreach (var property in current.GetMembers().OfType<IPropertySymbol>())
            {
                if (!property.IsStatic && property.DeclaredAccessibility == Accessibility.Public &&
                    !properties.ContainsKey(property.Name))
                {
                    properties.Add(property.Name, property);
                }
            }
        }

        return properties.Values.OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
    }

    public static string AccessModifier(Accessibility accessibility) => accessibility switch
    {
        Microsoft.CodeAnalysis.Accessibility.Public => "public",
        Microsoft.CodeAnalysis.Accessibility.Internal => "internal",
        Microsoft.CodeAnalysis.Accessibility.Private => "private",
        _ => ""
    };
}
