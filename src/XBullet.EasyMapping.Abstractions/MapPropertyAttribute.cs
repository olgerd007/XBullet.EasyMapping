using System.Diagnostics;

namespace XBullet.EasyMapping;

/// <summary>Maps a direct or dot-separated source property path to a target property or constructor parameter.</summary>
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
}
