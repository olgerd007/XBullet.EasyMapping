using System.Diagnostics;

namespace XBullet.EasyMapping;

/// <summary>Assigns a value from a parameterless helper to a target property path or constructor parameter.</summary>
[Conditional("MAPPER_PRESERVE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class MapGeneratedValueAttribute : Attribute
{
    public MapGeneratedValueAttribute(string target) => Target = target;

    public string Target { get; }

    /// <summary>Names a synchronous parameterless helper in the mapper that produces the target value.</summary>
    public string? Use { get; set; }
}
