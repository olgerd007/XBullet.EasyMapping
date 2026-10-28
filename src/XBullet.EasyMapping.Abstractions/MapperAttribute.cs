using System.Diagnostics;

namespace XBullet.EasyMapping;

/// <summary>Generates implementations of partial mapping methods in this class.</summary>
[Conditional("MAPPER_PRESERVE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class MapperAttribute : Attribute
{
    /// <summary>Controls whether unused source properties require an explicit ignore.</summary>
    public MappingCoverage Coverage { get; set; } = MappingCoverage.Target;

    /// <summary>Requires an approved model-shape snapshot supplied through AdditionalFiles.</summary>
    public bool RequireModelApproval { get; set; }
}
