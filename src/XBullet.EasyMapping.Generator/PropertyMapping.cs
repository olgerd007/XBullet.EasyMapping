using Microsoft.CodeAnalysis;

namespace XBullet.EasyMapping.Generator;

internal sealed class PropertyMapping
{
    public PropertyMapping(SourcePropertyPath source, string? use = null)
    {
        Source = source;
        Use = use;
    }

    private PropertyMapping(TypedConstant? constant, string? use)
    {
        Constant = constant;
        Use = use;
    }

    private PropertyMapping(TargetMappingScope nestedTarget) => NestedTarget = nestedTarget;

    public SourcePropertyPath? Source { get; }

    public TypedConstant? Constant { get; }

    public TargetMappingScope? NestedTarget { get; }

    public string? Use { get; }

    public static PropertyMapping ForConstant(TypedConstant constant) => new(constant, null);

    public static PropertyMapping ForGeneratedValue(string use) => new((TypedConstant?)null, use);

    public static PropertyMapping ForNestedTarget(TargetMappingScope target) => new(target);
}
