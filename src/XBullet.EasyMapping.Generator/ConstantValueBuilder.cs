using System.Globalization;
using Microsoft.CodeAnalysis;

namespace XBullet.EasyMapping.Generator;

internal static class ConstantValueBuilder
{
    public static bool TryBuild(TypedConstant constant, ITypeSymbol target, out string expression, out string error)
    {
        expression = "default!";
        if (constant.Kind == TypedConstantKind.Error)
        {
            error = "the value must be a valid C# attribute constant";
            return false;
        }
        if (constant.IsNull)
        {
            if (!SymbolNames.IsNullable(target))
            {
                error = $"null requires a nullable target, but '{SymbolNames.Type(target)}' is non-nullable";
                return false;
            }
            expression = "default(" + SymbolNames.Type(target) + ")";
            error = "";
            return true;
        }
        if (constant.Type is null || !SymbolEqualityComparer.Default.Equals(constant.Type, SymbolNames.NonNullable(target)))
        {
            error = $"constant type '{constant.Type?.ToDisplayString() ?? "unknown"}' must exactly match target type '{SymbolNames.Type(target)}' (apart from nullable lifting)";
            return false;
        }
        if (constant.Kind == TypedConstantKind.Array)
        {
            if (SymbolNames.NonNullable(target) is not IArrayTypeSymbol { Rank: 1 } array)
            {
                error = "array constants require a one-dimensional array target";
                return false;
            }
            var elements = new List<string>();
            for (var index = 0; index < constant.Values.Length; index++)
            {
                var item = constant.Values[index];
                // object[] explicitly requests boxing its attribute-compatible element values.
                var elementType = array.ElementType.SpecialType == SpecialType.System_Object && !item.IsNull && item.Type is { } itemType
                    ? SymbolNames.NonNullable(itemType) : array.ElementType;
                if (!TryBuild(item, elementType, out var element, out error))
                {
                    error = $"array element [{index}]: {error}";
                    return false;
                }
                elements.Add(element);
            }
            expression = "new " + SymbolNames.Type(SymbolNames.NonNullable(target)) + " { " + string.Join(", ", elements) + " }";
            error = "";
            return true;
        }

        expression = constant.Kind == TypedConstantKind.Type && constant.Value is ITypeSymbol type
            ? "typeof(" + SymbolNames.Type(type) + ")"
            : "(" + SymbolNames.Type(constant.Type) + ")(" + Literal(constant.Value!) + ")";
        error = "";
        return true;
    }

    private static string Literal(object value) => value switch
    {
        double number when double.IsNaN(number) => "double.NaN",
        double number when double.IsPositiveInfinity(number) => "double.PositiveInfinity",
        double number when double.IsNegativeInfinity(number) => "double.NegativeInfinity",
        double number when number == 0 && double.IsNegativeInfinity(1d / number) => "-0.0D",
        double number => number.ToString("R", CultureInfo.InvariantCulture) + "D",
        float number when float.IsNaN(number) => "float.NaN",
        float number when float.IsPositiveInfinity(number) => "float.PositiveInfinity",
        float number when float.IsNegativeInfinity(number) => "float.NegativeInfinity",
        float number when number == 0 && float.IsNegativeInfinity(1f / number) => "-0.0F",
        float number => number.ToString("R", CultureInfo.InvariantCulture) + "F",
        _ => Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false)
    };
}
