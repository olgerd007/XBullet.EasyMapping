using System.Diagnostics;

namespace XBullet.EasyMapping;

/// <summary>Assigns an attribute constant or array to a target property path or constructor parameter.</summary>
[Conditional("MAPPER_PRESERVE_ATTRIBUTES")]
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class MapConstantAttribute : Attribute
{
    public MapConstantAttribute(string target, object? value)
    {
        Target = target;
        Value = value;
    }

    public string Target { get; }

    public object? Value { get; }
}
