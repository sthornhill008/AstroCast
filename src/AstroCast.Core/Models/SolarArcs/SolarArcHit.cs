namespace AstroCast.Core.Models.SolarArcs;

/// <summary>
/// A dated, exact solar arc contact: the instant at which a natal point, directed by the solar arc,
/// makes an exact aspect to another natal point, or lands on a natal midpoint's 45° dial position
/// (branch Arcs, 1.4.0). Found by <see cref="Services.SolarArcHitFinder"/>.
/// </summary>
public sealed class SolarArcHit
{
    /// <summary>The directed natal point ("Sun", "Mars", "ASC", "MC").</summary>
    public required string DirectedPoint { get; init; }

    /// <summary>
    /// The aspect's angle in degrees: 0, 45, 60, 90, 120, 135 or 180 to a natal point; for a midpoint
    /// contact the harmonic the directed point stands at on the 45° dial, 0, 45, 90, 135 or 180.
    /// </summary>
    public required double AspectAngle { get; init; }

    /// <summary>The aspect's name ("Conjunction", "Semisquare", "Sesquiquadrate", ...).</summary>
    public required string AspectName { get; init; }

    /// <summary>The aspect's abbreviation ("cnj", "ssq", "sxt", "sqr", "tri", "ses", "opp").</summary>
    public required string AspectAbbreviation { get; init; }

    /// <summary>
    /// The natal target: a point's name ("Venus", "MC") or a midpoint's notation ("Sun/Moon", as
    /// <see cref="Midpoint.Notation"/> writes it).
    /// </summary>
    public required string Target { get; init; }

    /// <summary>The first body of the natal midpoint, or null for a contact to a natal point.</summary>
    public string? MidpointBody1 { get; init; }

    /// <summary>The second body of the natal midpoint, or null for a contact to a natal point.</summary>
    public string? MidpointBody2 { get; init; }

    /// <summary>Whether the target is a natal midpoint on the 45° dial.</summary>
    public bool IsMidpoint => MidpointBody1 is not null;

    /// <summary>
    /// The solar arc at the instant, in degrees, unwrapped: the progressed Sun's whole travel from the
    /// natal Sun, so a chart more than about 365 years old carries an arc past 360°.
    /// </summary>
    public required double Arc { get; init; }

    /// <summary>The exact instant as a Julian Day (UT), bisected to within a second.</summary>
    public required double JulianDayUt { get; init; }

    /// <summary>
    /// The exact instant, UTC, to the nearest whole second
    /// (<see cref="Utilities.JulianDayUtility.JulianDayToInstant"/>).
    /// </summary>
    public required DateTime InstantUtc { get; init; }
}
