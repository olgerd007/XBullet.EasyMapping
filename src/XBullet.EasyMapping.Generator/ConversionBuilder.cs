using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace XBullet.EasyMapping.Generator;

internal sealed class ConversionBuilder
{
    private readonly CSharpCompilation _compilation;
    private readonly IMethodSymbol _method;
    private readonly IMethodSymbol[] _candidates;
    private readonly HashSet<string> _explicitHelpers;
    private readonly Action<DiagnosticDescriptor, string[]> _report;
    private readonly HashSet<string> _names;
    private readonly bool _allowReferenceAssignment;
    private int _counter;

    public ConversionBuilder(Compilation compilation, IMethodSymbol method, IMethodSymbol[] candidates, HashSet<string> explicitHelpers,
        Action<DiagnosticDescriptor, string[]> report, bool allowReferenceAssignment = false, bool nullSafeMemberMapping = false)
    {
        _compilation = (CSharpCompilation)compilation;
        _method = method;
        _candidates = candidates;
        _explicitHelpers = explicitHelpers;
        _report = report;
        _allowReferenceAssignment = allowReferenceAssignment;
        NullSafeMemberMapping = nullSafeMemberMapping;
        _names = new HashSet<string>(method.Parameters.Select(parameter => parameter.Name), StringComparer.Ordinal);
        _names.UnionWith(method.ContainingType.GetMembers().Select(member => member.Name));
    }

    public HashSet<IMethodSymbol> Dependencies { get; } = new(SymbolEqualityComparer.Default);

    public List<string> CollectionHelpers { get; } = [];

    public bool NullSafeMemberMapping
    {
        get;
    }

    public string NewName()
    {
        string name;
        do
        {
            name = "__mapperValue" + _counter++;
        } while (!_names.Add(name));
        return name;
    }

    public string Convert(ITypeSymbol source, ITypeSymbol target, string expression, string member, string? use = null)
    {
        if (NullSafeMemberMapping && (source.IsReferenceType || SymbolNames.IsNullable(source)))
        {
            return ConvertNullSafe(source, target, expression, member, use);
        }

        if (use is not null)
        {
            return ConvertUsingHelper(source, target, expression, member, use);
        }

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

    private string ConvertNullSafe(ITypeSymbol source, ITypeSymbol target, string expression, string member, string? use)
    {
        var nullableSource = source.IsReferenceType ? source.WithNullableAnnotation(NullableAnnotation.Annotated) : source;
        if (use is not null)
        {
            if (_candidates.Any(candidate => candidate.Name == use && HasConverterSignature(candidate) &&
                SymbolNames.IsNullable(candidate.Parameters[0].Type) && CanPass(nullableSource, candidate.Parameters[0].Type) &&
                CanPass(candidate.ReturnType, target)))
            {
                return ConvertUsingHelper(nullableSource, target, expression, member, use);
            }
        }
        else
        {
            var nullAwareCandidates = FindCandidates(nullableSource, target, exact: true)
                .Where(candidate => !candidate.IsPartialDefinition || candidate.PartialImplementationPart is not null ||
                    SymbolNames.IsNullable(target)).ToArray();
            if (nullAwareCandidates.Length == 0)
            {
                nullAwareCandidates = _candidates.Where(candidate => !_explicitHelpers.Contains(candidate.Name) &&
                    HasConverterSignature(candidate) && SymbolNames.IsNullable(candidate.Parameters[0].Type) &&
                    CanPass(nullableSource, candidate.Parameters[0].Type) && CanPass(candidate.ReturnType, target) &&
                    (!candidate.IsPartialDefinition || candidate.PartialImplementationPart is not null ||
                        SymbolNames.IsNullable(target))).ToArray();
            }

            if (nullAwareCandidates.Length > 0)
            {
                return Call(nullAwareCandidates, nullableSource, target, expression);
            }
        }

        if (IsQueryable(source) || IsQueryable(target))
        {
            return Fail(source, target, member, "IQueryable projections are not supported");
        }

        var local = NewName();
        var converted = use is null
            ? ConvertNonNull(SymbolNames.NonNullable(source), SymbolNames.NonNullable(target), local, member)
            : ConvertUsingHelper(SymbolNames.NonNullable(source), target, local, member, use);
        return $"({expression} is {{ }} {local} ? {converted} : {NullMemberValue(target)})";
    }

    private static string NullMemberValue(ITypeSymbol target)
    {
        if (TryTargetCollection(target, out var element, out var array))
        {
            return array
                ? $"global::System.Array.Empty<{SymbolNames.Type(element)}>()"
                : $"new global::System.Collections.Generic.List<{SymbolNames.Type(element)}>()";
        }

        // Opting in permits null reference assignments even for non-nullable member
        // declarations. Suppress the generated warning without changing their types.
        return $"default({SymbolNames.Type(target)})" + (target.IsReferenceType && !SymbolNames.IsNullable(target) ? "!" : "");
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
            (source.IsValueType || source.SpecialType == SpecialType.System_String ||
                (_allowReferenceAssignment && source.IsReferenceType &&
                    SymbolEqualityComparer.IncludeNullability.Equals(
                        SymbolNames.NonNullable(source), SymbolNames.NonNullable(target)))))
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

    private bool HasConverterSignature(IMethodSymbol candidate) => candidate.Parameters.Length == 1 && !candidate.ReturnsVoid &&
            candidate.TypeParameters.Length == 0 && !candidate.IsAsync &&
            candidate.Parameters[0].RefKind == RefKind.None && candidate.RefKind == RefKind.None &&
            (!_method.IsStatic || candidate.IsStatic);

    private IEnumerable<IMethodSymbol> FindCandidates(ITypeSymbol source, ITypeSymbol target, bool exact) =>
        _candidates.Where(candidate => !_explicitHelpers.Contains(candidate.Name) && HasConverterSignature(candidate) &&
            (exact
                ? SymbolEqualityComparer.IncludeNullability.Equals(source, candidate.Parameters[0].Type) &&
                  SymbolEqualityComparer.IncludeNullability.Equals(target, candidate.ReturnType)
                : SymbolEqualityComparer.Default.Equals(source, SymbolNames.NonNullable(candidate.Parameters[0].Type)) &&
                  SymbolEqualityComparer.Default.Equals(target, SymbolNames.NonNullable(candidate.ReturnType)) &&
                  !SymbolNames.IsNullable(candidate.ReturnType)));

    private string ConvertUsingHelper(ITypeSymbol source, ITypeSymbol target, string expression, string member, string use)
    {
        var compatible = _candidates.Where(candidate => candidate.Name == use && HasConverterSignature(candidate) &&
            candidate.Parameters[0].Type.TypeKind != TypeKind.Dynamic && candidate.ReturnType.TypeKind != TypeKind.Dynamic &&
            CanPass(source, candidate.Parameters[0].Type) && CanPass(candidate.ReturnType, target)).ToArray();
        var exact = compatible.Where(candidate =>
            SymbolEqualityComparer.IncludeNullability.Equals(source, candidate.Parameters[0].Type) &&
            SymbolEqualityComparer.IncludeNullability.Equals(target, candidate.ReturnType)).ToArray();
        var candidates = exact.Length > 0 ? exact : compatible;
        if (candidates.Length != 1)
        {
            var reason = candidates.Length == 0
                ? $"has no compatible synchronous, non-generic, single-input method from '{SymbolNames.Type(source)}' to '{SymbolNames.Type(target)}' with statically typed parameters/results, safe nullability, and static/instance access"
                : $"is ambiguous from '{SymbolNames.Type(source)}' to '{SymbolNames.Type(target)}'; use a unique helper name or an exact signature";
            _report(MappingDiagnostics.Configuration,
                [$"MapProperty.Use helper '{use}' for target '{member}' in mapping '{_method.Name}' {reason}"]);
            return "default!";
        }

        // Pin the selected overload: another overload may accept a numeric or reference
        // conversion that is outside the supported helper type pair.
        var argument = "(" + SymbolNames.Type(candidates[0].Parameters[0].Type) + ")(" + expression + ")";
        return Call(candidates, source, target, argument);
    }

    private static bool CanPass(ITypeSymbol source, ITypeSymbol target) =>
        SymbolEqualityComparer.IncludeNullability.Equals(SymbolNames.NonNullable(source), SymbolNames.NonNullable(target)) &&
        (!SymbolNames.IsNullable(source) || SymbolNames.IsNullable(target));

    public string GenerateValue(ITypeSymbol target, string member, string use)
    {
        var compatible = _candidates.Where(candidate => candidate.Name == use && candidate.Parameters.Length == 0 &&
            !candidate.ReturnsVoid && candidate.TypeParameters.Length == 0 && !candidate.IsAsync &&
            candidate.RefKind == RefKind.None && candidate.ReturnType.TypeKind != TypeKind.Dynamic &&
            (!_method.IsStatic || candidate.IsStatic) && CanPass(candidate.ReturnType, target)).ToArray();
        var exact = compatible.Where(candidate => SymbolEqualityComparer.IncludeNullability.Equals(target, candidate.ReturnType)).ToArray();
        var candidates = exact.Length > 0 ? exact : compatible;
        if (candidates.Length != 1)
        {
            _report(MappingDiagnostics.Configuration,
                [$"MapGeneratedValue.Use helper '{use}' for target '{member}' in mapping '{_method.Name}' requires one compatible synchronous, non-generic, parameterless method returning '{SymbolNames.Type(target)}' with safe nullability and static/instance access"]);
            return "default!";
        }

        return Call(candidates[0], "");
    }

    private string Call(IMethodSymbol[] candidates, ITypeSymbol source, ITypeSymbol target, string expression)
    {
        if (candidates.Length != 1)
        {
            _report(MappingDiagnostics.Ambiguous, [SymbolNames.Type(source), SymbolNames.Type(target)]);
            return "default!";
        }

        return Call(candidates[0], expression);
    }

    private string Call(IMethodSymbol candidate, string expression)
    {
        Dependencies.Add(candidate);
        var receiver = candidate.IsStatic ? SymbolNames.Type(candidate.ContainingType) : "this";
        return receiver + "." + SymbolNames.Identifier(candidate.Name) + "(" + expression + ")";
    }

    private string Fail(ITypeSymbol source, ITypeSymbol target, string member, string reason)
    {
        _report(MappingDiagnostics.Conversion, [member, SymbolNames.Type(source), SymbolNames.Type(target), reason, _method.Name]);
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
