using System.Diagnostics;

namespace XBullet.EasyMapping;

/// <summary>Maps a source property path to a target property path or constructor parameter. Paths may be dot-separated.</summary>
[Conditional("MAPPER_PRESERVE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class MapPropertyAttribute : Attribute
{
    public MapPropertyAttribute(string source, string target)
    {
        Source = source;
        Target = target;
    }

    public string Source { get; }

    public string Target { get; }

    /// <summary>Names a helper in the mapper to use only for explicitly configured member conversions.</summary>
    public string? Use { get; set; }
}
