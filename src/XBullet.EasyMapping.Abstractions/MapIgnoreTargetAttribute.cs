using System.Diagnostics;

namespace XBullet.EasyMapping;

/// <summary>Excludes a target property from a mapping. Required members cannot be silently omitted.</summary>
[Conditional("MAPPER_PRESERVE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class MapIgnoreTargetAttribute : Attribute
{
    public MapIgnoreTargetAttribute(string target)
    {
        Target = target;
    }

    public string Target { get; }
}
