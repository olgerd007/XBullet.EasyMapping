using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace XBullet.EasyMapping.Generator;

internal sealed class ConversionBuilder
{
    private readonly CSharpCompilation _compilation;
    private readonly IMethodSymbol _method;
    private readonly IMethodSymbol[] _candidates;
    private readonly Action<DiagnosticDescriptor, string[]> _report;
    private readonly HashSet<string> _names;
    private int _counter;

    public ConversionBuilder(Compilation compilation, IMethodSymbol method, IMethodSymbol[] candidates,
        Action<DiagnosticDescriptor, string[]> report)
    {
        _compilation = (CSharpCompilation)compilation;
        _method = method;
        _candidates = candidates;
        _report = report;
        _names = new HashSet<string>(method.Parameters.Select(parameter => parameter.Name), StringComparer.Ordinal);
        _names.UnionWith(method.ContainingType.GetMembers().Select(member => member.Name));
    }

    public HashSet<IMethodSymbol> Dependencies { get; } = new(SymbolEqualityComparer.Default);

    public List<string> CollectionHelpers { get; } = [];

    public string NewName()
    {
        string name;
        do { name = "__mapperValue" + _counter++; } while (!_names.Add(name));
        return name;
    }

    public string Convert(ITypeSymbol source, ITypeSymbol target, string expression, string member)
    {
        var exactCandidates = FindCandidates(source, target, exact: true)
            .Where(candidate => !candidate.IsPartialDefinition || candidate.PartialImplementationPart is not null ||
                !SymbolNames.IsNullable(source) || SymbolNames.IsNullable(target)).ToArray();
        if (exactCandidates.Length > 0)
        {
            return Call(exactCandidates, source, target, expression);
        }

        if (IsQueryable(source) || IsQueryable(target))
        {
            return Fail(source, target, member, "IQueryable projections are not supported");
        }

        if (SymbolNames.IsNullable(source))
        {
            if (!SymbolNames.IsNullable(target))
            {
                return Fail(source, target, member, "nullable source requires a nullable target or a handwritten null-handling converter");
            }

            var local = NewName();
            var converted = ConvertNonNull(SymbolNames.NonNullable(source), SymbolNames.NonNullable(target), local, member);
            return $"({expression} is {{ }} {local} ? {converted} : default({SymbolNames.Type(target)}))";
        }

        var nonNullResult = ConvertNonNull(source, SymbolNames.NonNullable(target), expression, member);
        return SymbolNames.IsNullable(target) ? $"({SymbolNames.Type(target)})({nonNullResult})" : nonNullResult;
    }

    private string ConvertNonNull(ITypeSymbol source, ITypeSymbol target, string expression, string member)
    {
        var candidates = FindCandidates(source, target, exact: false).ToArray();
        if (candidates.Length > 0)
        {
            return Call(candidates, source, target, expression);
        }

        if (TryElement(source, out var sourceElement) && TryTargetCollection(target, out var targetElement, out var array))
        {
            var item = NewName();
            var converted = Convert(sourceElement, targetElement, item, member + "[]");
            if (converted == item)
            {
                return $"global::System.Linq.Enumerable.{(array ? "ToArray" : "ToList")}({expression})";
            }

            if (!array && TryCollectionCount(source, out var countMember))
            {
                return BuildListConversion(source, targetElement, expression, item, converted, countMember);
            }

            var select = $"global::System.Linq.Enumerable.Select({expression}, {item} => {converted})";
            return $"global::System.Linq.Enumerable.{(array ? "ToArray" : "ToList")}({select})";
        }

        if (SymbolEqualityComparer.Default.Equals(source, target) &&
            (source.IsValueType || source.SpecialType == SpecialType.System_String))
        {
            return expression;
        }

        var conversion = _compilation.ClassifyConversion(source, target);
        if (conversion.IsImplicit && conversion.IsNumeric)
        {
            return $"({SymbolNames.Type(target)})({expression})";
        }

        return Fail(source, target, member, "declare a mapping method or a handwritten converter for this type pair");
    }

    private string BuildListConversion(ITypeSymbol source, ITypeSymbol targetElement, string expression,
        string item, string converted, string countMember)
    {
        var helper = NewName();
        var input = NewName();
        var output = NewName();
        var listType = $"global::System.Collections.Generic.List<{SymbolNames.Type(targetElement)}>";
        var body = new StringBuilder();
        body.Append(_method.IsStatic ? "static " : "").Append(listType).Append(' ').Append(helper)
            .Append('(').Append(SymbolNames.Type(source)).Append(' ').Append(input).AppendLine(") {")
            .Append("var ").Append(output).Append(" = new ").Append(listType).Append('(')
            .Append(input).Append('.').Append(countMember).AppendLine(");")
            .Append("foreach (var ").Append(item).Append(" in ").Append(input).AppendLine(") {")
            .Append(output).Append(".Add(").Append(converted).AppendLine(");")
            .AppendLine("}")
            .Append("return ").Append(output).AppendLine(";")
            .AppendLine("}");
        CollectionHelpers.Add(body.ToString());
        return helper + "(" + expression + ")";
    }

    private static bool TryCollectionCount(ITypeSymbol source, out string member)
    {
        if (source is IArrayTypeSymbol { Rank: 1 })
        {
            member = "Length";
            return true;
        }

        // Keep arbitrary enumerables on the existing single-pass LINQ path. Restrict
        // direct enumeration to framework contracts with predictable element/count members.
        if (source is INamedTypeSymbol { IsGenericType: true } named &&
            named.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" &&
            named.Name is "List" or "IList" or "IReadOnlyList" or "ICollection" or "IReadOnlyCollection")
        {
            member = "Count";
            return true;
        }

        member = "";
        return false;
    }

    private IEnumerable<IMethodSymbol> FindCandidates(ITypeSymbol source, ITypeSymbol target, bool exact) =>
        _candidates.Where(candidate => candidate.Parameters.Length == 1 && !candidate.ReturnsVoid &&
            candidate.TypeParameters.Length == 0 && !candidate.IsAsync &&
            candidate.Parameters[0].RefKind == RefKind.None && candidate.RefKind == RefKind.None &&
            (!_method.IsStatic || candidate.IsStatic) &&
            (exact
                ? SymbolEqualityComparer.IncludeNullability.Equals(source, candidate.Parameters[0].Type) &&
                  SymbolEqualityComparer.IncludeNullability.Equals(target, candidate.ReturnType)
                : SymbolEqualityComparer.Default.Equals(source, SymbolNames.NonNullable(candidate.Parameters[0].Type)) &&
                  SymbolEqualityComparer.Default.Equals(target, SymbolNames.NonNullable(candidate.ReturnType)) &&
                  !SymbolNames.IsNullable(candidate.ReturnType)));

    private string Call(IMethodSymbol[] candidates, ITypeSymbol source, ITypeSymbol target, string expression)
    {
        if (candidates.Length != 1)
        {
            _report(MappingDiagnostics.Ambiguous, [SymbolNames.Type(source), SymbolNames.Type(target)]);
            return "default!";
        }

        var candidate = candidates[0];
        Dependencies.Add(candidate);
        var receiver = candidate.IsStatic ? SymbolNames.Type(candidate.ContainingType) : "this";
        return receiver + "." + SymbolNames.Identifier(candidate.Name) + "(" + expression + ")";
    }

    private string Fail(ITypeSymbol source, ITypeSymbol target, string member, string reason)
    {
        _report(MappingDiagnostics.Conversion, [member, SymbolNames.Type(source), SymbolNames.Type(target), reason]);
        return "default!";
    }

    private static bool IsQueryable(ITypeSymbol type) => type is INamedTypeSymbol named &&
        new[] { named }.Concat(named.AllInterfaces).Any(candidate =>
            candidate.OriginalDefinition.ToDisplayString() == "System.Linq.IQueryable<T>");

    internal static bool TryElement(ITypeSymbol type, out ITypeSymbol element)
    {
        if (type is IArrayTypeSymbol { Rank: 1 } array)
        {
            element = array.ElementType;
            return true;
        }

        if (type is INamedTypeSymbol named && type.SpecialType != SpecialType.System_String)
        {
            var elements = new[] { named }.Concat(named.AllInterfaces).Where(candidate =>
                candidate.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T)
                .Select(candidate => candidate.TypeArguments[0]).Distinct<ITypeSymbol>(SymbolEqualityComparer.IncludeNullability).ToArray();
            if (elements.Length == 1)
            {
                element = elements[0];
                return true;
            }
        }

        element = type;
        return false;
    }

    private static bool TryTargetCollection(ITypeSymbol type, out ITypeSymbol element, out bool array)
    {
        array = type is IArrayTypeSymbol { Rank: 1 };
        if (array)
        {
            element = ((IArrayTypeSymbol)type).ElementType;
            return true;
        }

        if (type is INamedTypeSymbol { IsGenericType: true } named &&
            named.ContainingNamespace.ToDisplayString() == "System.Collections.Generic" &&
            named.Name is "List" or "IList" or "IReadOnlyList" or "ICollection" or "IReadOnlyCollection" or "IEnumerable")
        {
            element = named.TypeArguments[0];
            return true;
        }

        element = type;
        return false;
    }
}
