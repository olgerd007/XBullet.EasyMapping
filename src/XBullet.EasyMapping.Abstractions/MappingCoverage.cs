namespace XBullet.EasyMapping;

/// <summary>Specifies which DTO properties must be accounted for by a mapping.</summary>
public enum MappingCoverage
{
    /// <summary>Require every target property to be mapped or explicitly ignored.</summary>
    Target = 0,

    /// <summary>Also require every readable source property to be mapped or explicitly ignored.</summary>
    Both = 1
}
