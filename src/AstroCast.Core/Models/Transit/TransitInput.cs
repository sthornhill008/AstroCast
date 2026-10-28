using AstroCast.Core.Formatting;
using AstroCast.Core.Models.PrimaryDirections;

namespace AstroCast.Core.Models.Transit;

/// <summary>
/// Input parameters for a transit calculation.
/// </summary>
public sealed class TransitInput
{
    /// <summary>The natal chart result being transited.</summary>
    public required ChartResult NatalChart { get; init; }

    /// <summary>First day of the transit report window.</summary>
    public required DateOnly ReportStartDate { get; init; }

    /// <summary>Last day of the transit report window.</summary>
    public required DateOnly ReportEndDate { get; init; }

    /// <summary>
    /// When true, includes minor aspects (semisquare, sesquiquadrate,
    /// semisextile, quincunx, sesquiquintile) in addition to major aspects.
    /// </summary>
    public bool IncludeMinorAspects { get; init; }

    /// <summary>
    /// Whether to run the transit scan: the transiting bodies, their stations and ingresses, the
    /// transits to the natal points, the house overlays, the midpoint activations, the
    /// transit-to-transit and declination aspects. Defaults to true (branch Modes, 1.4.0 item 2).
    /// </summary>
    public bool IncludeTransits { get; init; } = true;

    /// <summary>
    /// Whether to include the solar arc directions: the directed positions and the aspects in orb
    /// at the report's start, and the dated hits in the period. Defaults to true.
    /// </summary>
    public bool IncludeSolarArcs { get; init; } = true;

    /// <summary>
    /// Whether to include the secondary progressions: the progressed positions, Moon, sign changes,
    /// stations and aspects in orb at the report's start, and the dated hits in the period.
    /// Defaults to true.
    /// </summary>
    public bool IncludeSecondaryProgressions { get; init; } = true;

    /// <summary>
    /// Whether to include the primary directions (branch PrimaryExport, 1.5.0): the chart's whole-life
    /// directions calculated once, and those exact in the period listed. Defaults to false, so every
    /// existing caller and golden is unchanged (scoping decision 1).
    /// </summary>
    public bool IncludePrimaryDirections { get; init; }

    /// <summary>
    /// The settings of the primary directions when they are included: the key, the significators, the age
    /// limit and the birth-time uncertainty. <see cref="PrimaryDirectionSettings.Default"/> by default.
    /// </summary>
    public PrimaryDirectionSettings PrimaryDirectionSettings { get; init; } = PrimaryDirectionSettings.Default;

    /// <summary>
    /// The midpoint day of the report period: the proleptic day of the start plus half the days to the
    /// proleptic day of the end, rounded down (branch R3, 1.3.4; R-D7). A day derived from the scan, not
    /// a typed date: across October 1582 it is never one of the ten dates that never occurred (typed
    /// 1582-09-25 to 1582-10-25 gives 1582-10-15, where typed arithmetic gave 1582-10-10), and in a
    /// Julian leap year it may be the Julian 29 February no typed date can hold (1400-02-28 to
    /// 1400-03-02 gives 1400-03-09, Julian 1400-02-29). Printed through
    /// <see cref="CalendarDateFormatter.Format(DateOnly, string)"/>; <see cref="TransitResult.MidpointDate"/>
    /// carries the same value.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when a bound is one of 1582-10-05 to 1582-10-14, which never occurred.
    /// </exception>
    public DateOnly MidpointDate =>
        Midpoint(CalendarDateFormatter.ProlepticDay(ReportStartDate), CalendarDateFormatter.ProlepticDay(ReportEndDate));

    /// <summary>The first day plus half the days to the last, rounded down.</summary>
    /// <param name="start">The first proleptic day.</param>
    /// <param name="end">The last proleptic day.</param>
    /// <returns>The midpoint day.</returns>
    private static DateOnly Midpoint(DateOnly start, DateOnly end) =>
        start.AddDays((end.DayNumber - start.DayNumber) / 2);
}
