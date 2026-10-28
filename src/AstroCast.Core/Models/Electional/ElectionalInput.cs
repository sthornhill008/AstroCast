using AstroCast.Core.Enums;

namespace AstroCast.Core.Models.Electional;

/// <summary>
/// Input parameters for an electional chart calculation.
/// Defines the date range, location, constraints, and natal chart
/// for the electional search.
/// </summary>
public sealed class ElectionalInput
{
    /// <summary>The client's natal chart result.</summary>
    public required ChartResult NatalChart { get; init; }

    /// <summary>Type of event being elected.</summary>
    public required ElectionType ElectionType { get; init; }

    /// <summary>Free-text description of the election goal.</summary>
    public required string ElectionGoal { get; init; }

    /// <summary>First acceptable date for the event.</summary>
    public required DateOnly RangeStart { get; init; }

    /// <summary>Last acceptable date for the event.</summary>
    public required DateOnly RangeEnd { get; init; }

    /// <summary>Event location latitude (north positive).</summary>
    public required double Latitude { get; init; }

    /// <summary>Event location longitude (east positive).</summary>
    public required double Longitude { get; init; }

    /// <summary>Human-readable event location name.</summary>
    public required string LocationName { get; init; }

    /// <summary>IANA timezone for the event location.</summary>
    public required string TimeZone { get; init; }

    /// <summary>Number of candidate windows to return (default 3, max 5).</summary>
    public int CandidateCount { get; init; } = 3;

    /// <summary>Optional time-of-day constraints. Null = any time.</summary>
    public TimeConstraint? TimeConstraint { get; init; }

    /// <summary>Optional day-of-week constraints. Null = any day.</summary>
    public IReadOnlyList<DayOfWeek>? AllowedDays { get; init; }

    /// <summary>
    /// Free-text constraints from the client (e.g., "not during Mercury Rx").
    /// Stored for display in the export; parsed constraints are separate properties.
    /// </summary>
    public string? ConstraintNotes { get; init; }

    /// <summary>When true, disqualify windows during Mercury retrograde.</summary>
    public bool AvoidMercuryRetrograde { get; init; } = true;

    /// <summary>
    /// Minimum time separation in hours between selected candidates.
    /// After selecting the top-ranked candidate, all others within this window
    /// are excluded before selecting the next. Set to 0 to disable separation
    /// filtering (backward compatibility). Default is 6 hours.
    /// </summary>
    public double MinimumCandidateSeparationHours { get; init; } = 6.0;
}
