using System.Diagnostics;

namespace XBullet.EasyMapping;

/// <summary>Explicitly excludes a readable source property from a mapping.</summary>
[Conditional("MAPPER_PRESERVE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class MapIgnoreSourceAttribute : Attribute
{
    public MapIgnoreSourceAttribute(string source)
    {
        Source = source;
    }

    public string Source { get; }
}
