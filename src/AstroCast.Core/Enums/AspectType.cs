namespace AstroCast.Core.Enums;

/// <summary>
/// Types of astrological aspects, including major Ptolemaic aspects
/// and Ebertin cosmobiology aspects.
/// </summary>
public enum AspectType
{
    /// <summary>0° — bodies at the same ecliptic longitude.</summary>
    Conjunction,

    /// <summary>180° — bodies opposite each other.</summary>
    Opposition,

    /// <summary>120° — bodies 120° apart (harmonious).</summary>
    Trine,

    /// <summary>90° — bodies 90° apart (dynamic tension).</summary>
    Square,

    /// <summary>60° — bodies 60° apart (harmonious, minor).</summary>
    Sextile,

    /// <summary>45° — Ebertin: semi-square (friction).</summary>
    SemiSquare,

    /// <summary>135° — Ebertin: sesquiquadrate (friction).</summary>
    Sesquiquadrate,

    /// <summary>30° — Ebertin: semi-sextile (adjustment).</summary>
    SemiSextile,

    /// <summary>150° — Ebertin: quincunx / inconjunct (adjustment).</summary>
    Quincunx
}

/// <summary>
/// Extension methods for <see cref="AspectType"/>.
/// </summary>
public static class AspectTypeExtensions
{
    /// <summary>
    /// Returns true if the aspect type is a major Ptolemaic aspect
    /// (conjunction, opposition, trine, square, sextile).
    /// </summary>
    /// <param name="type">The aspect type to check.</param>
    /// <returns>True for major aspects, false for Ebertin/minor aspects.</returns>
    public static bool IsMajor(this AspectType type)
    {
        return type is AspectType.Conjunction
            or AspectType.Opposition
            or AspectType.Trine
            or AspectType.Square
            or AspectType.Sextile;
    }
}
