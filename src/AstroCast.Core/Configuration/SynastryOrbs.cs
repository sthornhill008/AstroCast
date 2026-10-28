using AstroCast.Core.Enums;

namespace AstroCast.Core.Configuration;

/// <summary>
/// Synastry-specific orb table that varies by planet category.
/// Tighter than natal orbs to focus on significant inter-chart contacts.
/// </summary>
/// <remarks>
/// The orb for any aspect pair is determined by the wider (more generous)
/// category of the two bodies involved. Luminaries get the widest orbs,
/// angles the tightest.
/// </remarks>
public static class SynastryOrbs
{
    /// <summary>Planet category for orb determination.</summary>
    public enum PlanetCategory
    {
        /// <summary>Sun, Moon — widest orbs.</summary>
        Luminary,

        /// <summary>Mercury, Venus, Mars — moderate orbs.</summary>
        Personal,

        /// <summary>Jupiter through Pluto, Chiron, True Node — tighter orbs.</summary>
        Outer,

        /// <summary>ASC, MC — tightest orbs.</summary>
        Angle
    }

    /// <summary>
    /// Determine the category of a body by name.
    /// Handles prefixed names (e.g., "A Sun", "B MC") by stripping the prefix.
    /// </summary>
    /// <param name="bodyName">Body name, optionally prefixed with "A " or "B ".</param>
    /// <returns>The planet category for orb lookup.</returns>
    public static PlanetCategory GetCategory(string bodyName)
    {
        // Strip "A " or "B " prefix if present
        string name = bodyName;
        if (name.Length > 2 && (name.StartsWith("A ") || name.StartsWith("B ")))
            name = name[2..];

        return name switch
        {
            "Sun" or "Moon" => PlanetCategory.Luminary,
            "Mercury" or "Venus" or "Mars" => PlanetCategory.Personal,
            "ASC" or "MC" => PlanetCategory.Angle,
            _ => PlanetCategory.Outer // Jupiter, Saturn, Uranus, Neptune, Pluto, Chiron, True Node
        };
    }

    /// <summary>
    /// Get the synastry orb for a given aspect between two bodies.
    /// Uses the wider orb of the two bodies involved (lower enum value = wider orb).
    /// </summary>
    /// <param name="aspectType">The aspect type to look up.</param>
    /// <param name="body1Name">Name of the first body.</param>
    /// <param name="body2Name">Name of the second body.</param>
    /// <returns>Maximum orb in degrees for this aspect/body combination.</returns>
    public static double GetOrb(AspectType aspectType, string body1Name, string body2Name)
    {
        PlanetCategory cat1 = GetCategory(body1Name);
        PlanetCategory cat2 = GetCategory(body2Name);

        // Use the wider (more generous) category — lower enum value = wider
        PlanetCategory effectiveCategory = (PlanetCategory)Math.Min((int)cat1, (int)cat2);

        return (aspectType, effectiveCategory) switch
        {
            // Conjunction
            (AspectType.Conjunction, PlanetCategory.Luminary) => 8.0,
            (AspectType.Conjunction, PlanetCategory.Personal) => 7.0,
            (AspectType.Conjunction, PlanetCategory.Outer) => 5.0,
            (AspectType.Conjunction, PlanetCategory.Angle) => 5.0,

            // Opposition
            (AspectType.Opposition, PlanetCategory.Luminary) => 8.0,
            (AspectType.Opposition, PlanetCategory.Personal) => 7.0,
            (AspectType.Opposition, PlanetCategory.Outer) => 5.0,
            (AspectType.Opposition, PlanetCategory.Angle) => 5.0,

            // Trine
            (AspectType.Trine, PlanetCategory.Luminary) => 7.0,
            (AspectType.Trine, PlanetCategory.Personal) => 6.0,
            (AspectType.Trine, PlanetCategory.Outer) => 4.0,
            (AspectType.Trine, PlanetCategory.Angle) => 4.0,

            // Square
            (AspectType.Square, PlanetCategory.Luminary) => 7.0,
            (AspectType.Square, PlanetCategory.Personal) => 6.0,
            (AspectType.Square, PlanetCategory.Outer) => 4.0,
            (AspectType.Square, PlanetCategory.Angle) => 4.0,

            // Sextile
            (AspectType.Sextile, PlanetCategory.Luminary) => 5.0,
            (AspectType.Sextile, PlanetCategory.Personal) => 4.0,
            (AspectType.Sextile, PlanetCategory.Outer) => 3.0,
            (AspectType.Sextile, PlanetCategory.Angle) => 3.0,

            // Minor aspects (semi-square, sesquiquadrate, semi-sextile, quincunx)
            (_, PlanetCategory.Luminary) => 2.0,
            (_, PlanetCategory.Personal) => 2.0,
            (_, PlanetCategory.Outer) => 1.5,
            (_, PlanetCategory.Angle) => 1.5,

            // Fallback for unnamed enum values
            _ => 2.0,
        };
    }
}
