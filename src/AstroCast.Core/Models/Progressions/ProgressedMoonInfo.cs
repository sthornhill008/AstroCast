namespace AstroCast.Core.Models.Progressions;

/// <summary>
/// Detailed information about the progressed Moon at report midpoint.
/// The progressed Moon is the most interpretively significant progressed body.
/// </summary>
public sealed class ProgressedMoonInfo
{
    /// <summary>Progressed Moon longitude in degrees.</summary>
    public required double Longitude { get; init; }

    /// <summary>Progressed Moon sign.</summary>
    public required string Sign { get; init; }

    /// <summary>Degree within sign (0–29).</summary>
    public required int Degrees { get; init; }

    /// <summary>Arc-minutes within degree (0–59).</summary>
    public required int Minutes { get; init; }

    /// <summary>Natal house the progressed Moon occupies (1–12).</summary>
    public required int NatalHouse { get; init; }

    /// <summary>
    /// Progressed lunar phase name (e.g., "New Moon", "First Quarter", "Full Moon").
    /// Based on the angle between progressed Sun and progressed Moon.
    /// </summary>
    public required string PhaseName { get; init; }

    /// <summary>
    /// Angle between progressed Sun and progressed Moon in degrees (0–360).
    /// </summary>
    public required double PhaseAngle { get; init; }

    /// <summary>
    /// Date the progressed Moon enters the next sign. Null if beyond 3 months
    /// of the report period.
    /// </summary>
    public DateOnly? NextSignChangeDate { get; init; }

    /// <summary>
    /// The next sign the progressed Moon will enter. Null if beyond 3 months.
    /// </summary>
    public string? NextSign { get; init; }
}
