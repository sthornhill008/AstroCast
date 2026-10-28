using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// A single exact aspect between two celestial bodies found by the mundane aspect finder.
/// </summary>
/// <remarks>
/// Represents one precise moment when two planets form a specific angular relationship.
/// The finder scans a date range using a harmonic mode (45° or 90°) and locates all
/// exact crossings to sub-arc-second precision. Includes cycle phase (0°–360°) and
/// waxing/waning classification.
/// </remarks>
public sealed class MundaneAspectResult
{
    /// <summary>The first body (conventionally the slower one).</summary>
    public required CelestialBodyId Body1 { get; init; }

    /// <summary>Display name of body 1.</summary>
    public required string Body1Name { get; init; }

    /// <summary>The second body (conventionally the faster one).</summary>
    public required CelestialBodyId Body2 { get; init; }

    /// <summary>Display name of body 2.</summary>
    public required string Body2Name { get; init; }

    /// <summary>The aspect type name (e.g., "Conjunction", "Waxing Square", "Waning Semi-square").</summary>
    public required string AspectName { get; init; }

    /// <summary>The target angle used in the search: 0, 45, 60, 90, 120, 135, 180.</summary>
    public required double TargetAngle { get; init; }

    /// <summary>The exact UTC date/time of the aspect.</summary>
    public required DateTime ExactUtc { get; init; }

    /// <summary>Body 1 ecliptic longitude at the exact moment.</summary>
    public required double Longitude1 { get; init; }

    /// <summary>Body 2 ecliptic longitude at the exact moment.</summary>
    public required double Longitude2 { get; init; }

    /// <summary>Formatted position of body 1 (e.g., "12°44' Aries").</summary>
    public required string Position1Formatted { get; init; }

    /// <summary>Formatted position of body 2 (e.g., "12°44' Cancer").</summary>
    public required string Position2Formatted { get; init; }

    /// <summary>Sign of body 1.</summary>
    public required string Sign1 { get; init; }

    /// <summary>Sign of body 2.</summary>
    public required string Sign2 { get; init; }

    /// <summary>
    /// Actual angular separation at the exact moment, in degrees.
    /// Should match the target angle to sub-arc-second precision.
    /// </summary>
    public required double ActualSeparation { get; init; }

    /// <summary>Precision: difference between actual separation and target angle, in arc-seconds.</summary>
    public required double PrecisionArcSeconds { get; init; }

    /// <summary>
    /// The cycle phase (0°–360°) indicating where this aspect falls in the full
    /// conjunction-to-conjunction cycle. 0° = conjunction, 90° = waxing square,
    /// 180° = opposition, 270° = waning square.
    /// </summary>
    /// <remarks>
    /// Measured from the slower body to the faster, the one with the shorter mean sidereal period:
    /// the faster body's longitude minus the slower's, whichever is named first. A pair with a body
    /// not ranked by period (the True Node) is measured from <see cref="Body1"/> to
    /// <see cref="Body2"/>.
    /// </remarks>
    public required double CyclePhase { get; init; }

    /// <summary>
    /// "Waxing" (0°–180°) or "Waning" (180°–360°).
    /// Conjunction and Opposition are labelled without a phase prefix.
    /// </summary>
    public required string PhaseLabel { get; init; }

    /// <summary>Engine version.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>When calculated.</summary>
    public required DateTime CalculatedAt { get; init; }
}

/// <summary>
/// A series of mundane aspects found within a date range using a harmonic mode.
/// </summary>
/// <remarks>
/// Contains all exact aspects for a harmonic series (45° or 90°) between two bodies
/// within the scan window, sorted chronologically. The harmonic mode searches multiple
/// target angles in a single operation and returns a complete cycle timeline.
/// </remarks>
public sealed class MundaneAspectSeries
{
    /// <summary>The first body tracked.</summary>
    public required CelestialBodyId Body1 { get; init; }

    /// <summary>Body 1 display name.</summary>
    public required string Body1Name { get; init; }

    /// <summary>The second body tracked.</summary>
    public required CelestialBodyId Body2 { get; init; }

    /// <summary>Body 2 display name.</summary>
    public required string Body2Name { get; init; }

    /// <summary>The harmonic mode used (e.g., "45° Harmonic", "90° Harmonic").</summary>
    public required string HarmonicMode { get; init; }

    /// <summary>All aspects found, chronologically.</summary>
    public required IReadOnlyList<MundaneAspectResult> Aspects { get; init; }

    /// <summary>Scan start.</summary>
    public required DateTime ScanStart { get; init; }

    /// <summary>Scan end.</summary>
    public required DateTime ScanEnd { get; init; }

    /// <summary>Count.</summary>
    public required int Count { get; init; }

    /// <summary>Engine version.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>When calculated.</summary>
    public required DateTime CalculatedAt { get; init; }
}
