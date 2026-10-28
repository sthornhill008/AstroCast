using System.Globalization;

using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Represents a detected paran (paranatellonta) — two celestial bodies
/// simultaneously occupying angular positions at a specific geographic location.
/// </summary>
/// <remarks>
/// <para>
/// A paran event is declared when Body A crosses Angle X and Body B crosses
/// Angle Y within the configured time orb (default: 4 minutes of clock time,
/// equivalent to ~1° of right ascension). The smaller the orb, the more
/// potent the paran is considered.
/// </para>
/// <para>
/// Unlike ecliptic aspects, parans are location-dependent — the same birth
/// time produces different parans in different cities. This makes parans
/// inherently tied to the "where" of a chart, not just the "when".
/// </para>
/// <para>
/// Following Bernadette Brady's methodology, parans reveal life themes
/// that unfold progressively rather than the more immediate psychological
/// dynamics visible in standard aspect analysis.
/// </para>
/// </remarks>
public sealed class ParanEvent
{
    /// <summary>The first body in the paran pair.</summary>
    public required CelestialBodyId BodyA { get; init; }

    /// <summary>Display name of the first body.</summary>
    public required string BodyAName { get; init; }

    /// <summary>The angle occupied by Body A (Rising, Culminating, Setting, Anti-Culminating).</summary>
    public required AngleType AngleA { get; init; }

    /// <summary>Julian Day (UT) when Body A crosses its angle.</summary>
    public required double JulianDayA { get; init; }

    /// <summary>Body A's ecliptic longitude at the moment of its angular crossing.</summary>
    public required double LongitudeA { get; init; }

    /// <summary>The second body in the paran pair.</summary>
    public required CelestialBodyId BodyB { get; init; }

    /// <summary>Display name of the second body.</summary>
    public required string BodyBName { get; init; }

    /// <summary>The angle occupied by Body B (Rising, Culminating, Setting, Anti-Culminating).</summary>
    public required AngleType AngleB { get; init; }

    /// <summary>Julian Day (UT) when Body B crosses its angle.</summary>
    public required double JulianDayB { get; init; }

    /// <summary>Body B's ecliptic longitude at the moment of its angular crossing.</summary>
    public required double LongitudeB { get; init; }

    /// <summary>
    /// The time difference between the two angular crossings, expressed in minutes.
    /// Always non-negative. A value of 0.0 indicates an exact simultaneous crossing.
    /// </summary>
    public required double OrbMinutes { get; init; }

    /// <summary>
    /// The midpoint Julian Day (UT) between the two crossings, representing the
    /// "centre" of the paran event. Useful for sorting and for casting a paran
    /// moment chart.
    /// </summary>
    public double MidpointJulianDay => (JulianDayA + JulianDayB) / 2.0;

    /// <summary>
    /// Human-readable description of the paran in the form
    /// "Sun Rising — Moon Culminating (orb: 2.3 min)".
    /// </summary>
    public string Description =>
        string.Create(CultureInfo.InvariantCulture, $"{BodyAName} {AngleA} \u2014 {BodyBName} {AngleB} (orb: {OrbMinutes:F1} min)");
}
