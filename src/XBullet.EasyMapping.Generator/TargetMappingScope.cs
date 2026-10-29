using Microsoft.CodeAnalysis;

namespace XBullet.EasyMapping.Generator;

internal sealed class TargetMappingScope
{
    public Dictionary<string, PropertyMapping> Mappings { get; } = new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> Ignored { get; } = new(StringComparer.OrdinalIgnoreCase);

    public static TargetMappingScope Create(Dictionary<string, PropertyMapping> mappings, HashSet<string> ignored,
        Action<DiagnosticDescriptor, string[]> report, CancellationToken cancellationToken, string prefix = "")
    {
        var scope = new TargetMappingScope();
        var names = mappings.Keys.Concat(ignored).Select(path => path.Split('.')[0])
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.Ordinal);
        foreach (var name in names)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var childPrefix = name + ".";
            var children = mappings.Where(pair => pair.Key.StartsWith(childPrefix, StringComparison.OrdinalIgnoreCase))
                .ToDictionary(pair => pair.Key.Substring(childPrefix.Length), pair => pair.Value, StringComparer.OrdinalIgnoreCase);
            var childIgnores = new HashSet<string>(ignored.Where(path => path.StartsWith(childPrefix, StringComparison.OrdinalIgnoreCase))
                .Select(path => path.Substring(childPrefix.Length)), StringComparer.OrdinalIgnoreCase);
            if (mappings.TryGetValue(name, out var direct))
            {
                scope.Mappings.Add(name, direct);
                if (children.Count > 0 || childIgnores.Count > 0)
                {
                    report(MappingDiagnostics.Configuration,
                        [$"Target '{prefix}{name}' cannot be assigned as a whole object and also configured through nested paths"]);
                }
            }
            else if (children.Count > 0 || childIgnores.Count > 0)
            {
                scope.Mappings.Add(name, PropertyMapping.ForNestedTarget(
                    Create(children, childIgnores, report, cancellationToken, prefix + name + ".")));
            }

            if (ignored.Contains(name))
            {
                scope.Ignored.Add(name);
                if (scope.Mappings.ContainsKey(name))
                {
                    report(MappingDiagnostics.Configuration, [$"Target '{prefix}{name}' cannot be both mapped and ignored"]);
                }
            }
        }

        return scope;
    }

    public string FirstPath()
    {
        var first = Mappings.FirstOrDefault();
        return first.Value?.NestedTarget is { } nested ? first.Key + "." + nested.FirstPath() :
            first.Key ?? Ignored.FirstOrDefault() ?? "";
    }
}
