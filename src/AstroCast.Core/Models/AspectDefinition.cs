using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Defines an aspect type with its exact angle, orb allowance, category, and display symbol.
/// The static <see cref="All"/> property provides the complete set of configured aspects.
/// </summary>
public sealed class AspectDefinition
{
    /// <summary>The aspect type.</summary>
    public required AspectType Type { get; init; }

    /// <summary>The exact angle of the aspect in degrees.</summary>
    public required double Angle { get; init; }

    /// <summary>Maximum orb allowance in degrees.</summary>
    public required double Orb { get; init; }

    /// <summary>The aspect category (Major or Ebertin).</summary>
    public required AspectCategory Category { get; init; }

    /// <summary>Display symbol for the aspect (e.g., "☌" for conjunction).</summary>
    public required string Symbol { get; init; }

    /// <summary>ASCII abbreviation for console display (e.g., "cnj" for conjunction).</summary>
    public required string Abbreviation { get; init; }

    /// <summary>
    /// All configured aspect definitions: 5 major Ptolemaic aspects and 4 Ebertin aspects.
    /// </summary>
    public static IReadOnlyList<AspectDefinition> All { get; } = new List<AspectDefinition>
    {
        new() { Type = AspectType.Conjunction,    Angle =   0, Orb = 8, Category = AspectCategory.Major,   Symbol = "\u260c", Abbreviation = "cnj" },
        new() { Type = AspectType.Opposition,     Angle = 180, Orb = 8, Category = AspectCategory.Major,   Symbol = "\u260d", Abbreviation = "opp" },
        new() { Type = AspectType.Trine,          Angle = 120, Orb = 7, Category = AspectCategory.Major,   Symbol = "\u25b3", Abbreviation = "tri" },
        new() { Type = AspectType.Square,         Angle =  90, Orb = 7, Category = AspectCategory.Major,   Symbol = "\u25a1", Abbreviation = "sqr" },
        new() { Type = AspectType.Sextile,        Angle =  60, Orb = 5, Category = AspectCategory.Major,   Symbol = "\u26b9", Abbreviation = "sxt" },
        new() { Type = AspectType.SemiSquare,     Angle =  45, Orb = 2, Category = AspectCategory.Ebertin, Symbol = "\u2220", Abbreviation = "ssq" },
        new() { Type = AspectType.Sesquiquadrate, Angle = 135, Orb = 2, Category = AspectCategory.Ebertin, Symbol = "\u22bc", Abbreviation = "ses" },
        new() { Type = AspectType.SemiSextile,    Angle =  30, Orb = 2, Category = AspectCategory.Ebertin, Symbol = "\u26ba", Abbreviation = "ssx" },
        new() { Type = AspectType.Quincunx,       Angle = 150, Orb = 2, Category = AspectCategory.Ebertin, Symbol = "\u26bb", Abbreviation = "qnx" },
    };
}
