using System.Diagnostics;

namespace XBullet.EasyMapping;

/// <summary>Maps a source property path or a whole-source helper result to a target property path or constructor parameter.</summary>
[Conditional("MAPPER_PRESERVE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class MapPropertyAttribute : Attribute
{
    /// <summary>Maps a target using a helper that receives the whole source. Use is required.</summary>
    public MapPropertyAttribute(string target) : this(string.Empty, target)
    {
    }

    public MapPropertyAttribute(string source, string target)
    {
        Source = source;
        Target = target;
    }

    /// <summary>The selected source path, or an empty string for a whole-source helper.</summary>
    public string Source
    {
        get;
    }

    public string Target
    {
        get;
    }

    /// <summary>Names a helper receiving the selected source property, or the whole source with the target-only overload.</summary>
    public string? Use
    {
        get; set;
    }

    /// <summary>Declares readable source root properties consumed by a target-only whole-source helper for coverage validation.</summary>
    public string[]? Sources
    {
        get; set;
    }
}
