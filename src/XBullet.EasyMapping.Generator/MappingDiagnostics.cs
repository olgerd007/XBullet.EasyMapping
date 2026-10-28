using Microsoft.CodeAnalysis;

namespace XBullet.EasyMapping.Generator;

internal static class MappingDiagnostics
{
    public static readonly DiagnosticDescriptor Mapper = Create("EMAP001", "Unsupported mapper", "{0}");
    public static readonly DiagnosticDescriptor Signature = Create("EMAP002", "Unsupported mapping signature", "{0}");
    public static readonly DiagnosticDescriptor MissingSource = Create("EMAP003", "Missing source property", "No readable source property for target '{0}' in mapping '{1}'");
    public static readonly DiagnosticDescriptor Constructor = Create("EMAP004", "Constructor selection failed", "{0}");
    public static readonly DiagnosticDescriptor Conversion = Create("EMAP005", "Unsupported conversion", "Cannot map '{0}' from '{1}' to '{2}': {3}");
    public static readonly DiagnosticDescriptor Configuration = Create("EMAP006", "Invalid mapping configuration", "{0}");
    public static readonly DiagnosticDescriptor Ambiguous = Create("EMAP007", "Ambiguous mapping methods", "Multiple methods map '{0}' to '{1}'; keep one mapping per type pair in this mapper");
    public static readonly DiagnosticDescriptor Cycle = Create("EMAP008", "Recursive mapping unsupported", "Generated mapping '{0}' participates in a cycle; use an explicitly implemented graph mapping");
    public static readonly DiagnosticDescriptor Target = Create("EMAP009", "Unsupported mapping target", "{0}");
    public static readonly DiagnosticDescriptor UnmappedSource = Create("EMAP010", "Unmapped source property", "Source property '{0}' in mapping '{1}' is neither mapped nor ignored; map it or use MapIgnoreSource");
    public static readonly DiagnosticDescriptor ModelApproval = Create("EMAP011", "Model approval required", "{0}");

    private static DiagnosticDescriptor Create(string id, string title, string message) =>
        new(id, title, message, "Mapping", DiagnosticSeverity.Error, isEnabledByDefault: true);
}
