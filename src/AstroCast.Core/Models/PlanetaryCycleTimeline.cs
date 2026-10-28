using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Complete cycle timeline between two consecutive planetary returns,
/// showing the four critical phase points (return, opening square,
/// opposition, closing square) plus the next return.
/// </summary>
/// <remarks>
/// <para>
/// A planetary cycle is analogous to lunar phases applied to any planet's
/// synodic cycle relative to its natal position. The five phase points are:
/// Return (0°), Opening Square (90°), Opposition (180°), Closing Square (270°),
/// and Next Return (360°/0°).
/// </para>
/// <para>
/// For outer planets (Mars, Jupiter, Saturn), some phase points may involve
/// retrograde triple crossings where the planet crosses the target longitude
/// three times (direct, retrograde, final direct).
/// </para>
/// </remarks>
public sealed class PlanetaryCycleTimeline
{
    /// <summary>The planet whose cycle this tracks.</summary>
    public required CelestialBodyId Planet { get; init; }

    /// <summary>Planet display name.</summary>
    public required string PlanetName { get; init; }

    /// <summary>The natal planet longitude (the 0° reference point).</summary>
    public required double NatalLongitude { get; init; }

    /// <summary>The natal planet's formatted position (e.g., "26°46' Leo").</summary>
    public required string NatalPositionFormatted { get; init; }

    /// <summary>The native's name.</summary>
    public required string NativeName { get; init; }

    /// <summary>The starting return number.</summary>
    public required int ReturnNumber { get; init; }

    /// <summary>The five phase points in chronological order.</summary>
    public required IReadOnlyList<CyclePhasePoint> PhasePoints { get; init; }

    /// <summary>Total cycle duration in days.</summary>
    public required double CycleDurationDays { get; init; }

    /// <summary>Engine version.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>When this was calculated.</summary>
    public required DateTime CalculatedAt { get; init; }
}

/// <summary>
/// A single phase point in a planetary cycle timeline.
/// </summary>
public sealed class CyclePhasePoint
{
    /// <summary>The phase name: "Return", "Opening Square", "Opposition", "Closing Square", "Next Return".</summary>
    public required string PhaseName { get; init; }

    /// <summary>The angular offset from natal longitude: 0, 90, 180, 270, 360.</summary>
    public required int PhaseAngle { get; init; }

    /// <summary>The target longitude the transiting planet reaches at this phase.</summary>
    public required double TargetLongitude { get; init; }

    /// <summary>The formatted target position (e.g., "26°46' Scorpio" for the opening square of 26°46' Leo).</summary>
    public required string TargetPositionFormatted { get; init; }

    /// <summary>The exact UTC date/time of this phase point.</summary>
    public required DateTime ExactUtc { get; init; }

    /// <summary>The zodiac sign the transiting planet is in at this phase point.</summary>
    public required string Sign { get; init; }

    /// <summary>
    /// Number of days from the cycle start (Return) to this phase point.
    /// Zero for the Return itself.
    /// </summary>
    public required double DaysFromStart { get; init; }

    /// <summary>
    /// Percentage of the total cycle elapsed at this phase point (0–100).
    /// </summary>
    public required double CyclePercentage { get; init; }

    /// <summary>
    /// If the transiting planet was retrograde during this phase crossing,
    /// there may be multiple passes. This records how many times the planet
    /// crossed this phase point (1 = single direct pass, 3 = triple crossing;
    /// 0 when no pass was found and the point is placed at its estimate).
    /// </summary>
    public required int PassCount { get; init; }

    /// <summary>
    /// The dates of all passes (for retrograde triple crossings).
    /// The first entry is the first pass, the last is the final direct pass.
    /// For single-pass crossings, this contains one entry matching ExactUtc.
    /// </summary>
    public required IReadOnlyList<DateTime> AllPasses { get; init; }

    /// <summary>
    /// Optional: planetary positions snapshot at this phase point.
    /// Null if snapshots were not requested.
    /// </summary>
    public IReadOnlyList<PlanetPosition>? Snapshot { get; init; }
}
