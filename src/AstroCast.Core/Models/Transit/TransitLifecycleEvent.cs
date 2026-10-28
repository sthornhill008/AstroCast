namespace AstroCast.Core.Models.Transit;

/// <summary>
/// A single event in a transit's lifecycle (entry, exact, station, exit).
/// </summary>
/// <remarks>
/// Every event is refined to the second by binary search, with one exception: an
/// entry or exit whose walk reached its cap (<c>MaxWalkDays</c>) or the span's edge
/// still in orb, or whose bracket is invalid, keeps noon UT of its date.
/// Entry/exit times mark the moment the transit crosses the orb boundary.
/// Station times mark the moment daily motion crosses zero.
/// Exact pass times mark the moment the aspect separation reaches zero.
/// </remarks>
public sealed class TransitLifecycleEvent
{
    /// <summary>Type of lifecycle event.</summary>
    public required TransitLifecycleEventType EventType { get; init; }

    /// <summary>
    /// UTC date/time of the event, to the second; an entry or exit whose walk
    /// reached its cap or the span's edge keeps noon UT of its date.
    /// </summary>
    public required DateTime Date { get; init; }

    /// <summary>
    /// Date portion only, for backward compatibility, sorting, and display.
    /// </summary>
    public DateOnly DatePart => DateOnly.FromDateTime(Date);

    /// <summary>
    /// Pass number for exact events (1, 2, 3). Null for non-exact events.
    /// </summary>
    public int? PassNumber { get; init; }

    /// <summary>
    /// For station events: Retrograde or Direct. Null for non-station events.
    /// </summary>
    public string? StationType { get; init; }

    /// <summary>
    /// Residual orb in degrees at the exact moment of a partile pass.
    /// Represents the precision achieved by binary search refinement
    /// (typically &lt; 0.001° / &lt; 4 arc-seconds). Null for non-exact events.
    /// </summary>
    public double? ExactOrbDegrees { get; init; }

    /// <summary>
    /// True if this event occurred before the report start date.
    /// </summary>
    public bool BeforeReportPeriod { get; init; }

    /// <summary>
    /// True if this event will occur after the report end date.
    /// </summary>
    public bool BeyondReportPeriod { get; init; }

    /// <summary>
    /// Ecliptic longitude of the transiting planet at the moment of this event.
    /// Populated for Entry and Exit events to support position display in timeline
    /// formatters. Null for Exact and Station events (position can be derived from
    /// the parent <see cref="ActiveTransit"/> or <see cref="TransitStation"/>).
    /// </summary>
    public double? TransitLongitude { get; init; }
}

/// <summary>
/// Types of transit lifecycle events.
/// </summary>
public enum TransitLifecycleEventType
{
    /// <summary>Transit planet enters orb of aspect to natal target.</summary>
    Entry,

    /// <summary>Transit aspect reaches exact (partile, 0°00').</summary>
    Exact,

    /// <summary>Transit planet stations retrograde or direct while in orb.</summary>
    Station,

    /// <summary>Transit planet exits orb, not returning during report period.</summary>
    Exit
}
