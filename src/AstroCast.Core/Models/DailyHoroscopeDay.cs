using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Aggregated transit data for a single day within a daily horoscope report.
/// Used internally by the <see cref="Formatting.DailyHoroscopeMarkdownFormatter"/>
/// to collect per-day snapshots before Markdown rendering.
/// </summary>
public sealed class DailyHoroscopeDay
{
    /// <summary>
    /// The day, as a proleptic day (branch R3, 1.3.4): before 1582-10-15 the Julian calendar's day as
    /// proleptic ticks, so Julian 1301-03-07 is 1301-03-15 here. Printed on the calendar in force through
    /// <see cref="Formatting.CalendarDateFormatter.Format(DateOnly, string)"/>.
    /// </summary>
    public required DateOnly Date { get; init; }

    /// <summary>The Moon's zodiac sign at noon on this date.</summary>
    public required ZodiacSign MoonSign { get; init; }

    /// <summary>The Moon's ecliptic longitude at noon on this date (degrees).</summary>
    public required double MoonLongitude { get; init; }

    /// <summary>
    /// Moon phase label for this day (e.g. "Waxing Crescent (mid)").
    /// Derived from Sun-Moon angle at noon. Includes phase position qualifier.
    /// </summary>
    public required string MoonPhaseLabel { get; init; }

    /// <summary>
    /// The Moon's sign change on this date, if it changes sign in the local day: "Moon enters
    /// Cancer at 15:54", the instant on the day's clock with the zone's name where the clocks
    /// repeat the reading ("Moon enters Leo at 00:18 CDT"); null when the Moon keeps its sign all
    /// day. The instant is the end of the void-of-course period that ends in the day, so the note
    /// and that period's printed end always agree.
    /// </summary>
    public string? MoonSignChange { get; init; }

    /// <summary>
    /// Void of Course periods that overlap with this calendar day.
    /// May contain zero, one, or two periods (if a VoC period starts
    /// late on the previous day and another starts late on this day).
    /// Each string is formatted as "HH:mm – HH:mm (Sign)" in local time.
    /// </summary>
    public required IReadOnlyList<string> VoidOfCoursePeriods { get; init; }

    /// <summary>
    /// Active transits: those in orb at local noon on this date, each with its orb and phase at
    /// noon. Sorted by transit planet weight (outermost first), then by natal target.
    /// </summary>
    public required IReadOnlyList<DailyTransitSnapshot> ActiveTransits { get; init; }

    /// <summary>
    /// Transits that become exact (partile) on this date. Subset of
    /// <see cref="ActiveTransits"/>. These are the day's headline aspects.
    /// </summary>
    public required IReadOnlyList<DailyTransitSnapshot> ExactToday { get; init; }

    /// <summary>
    /// Transits entering orb during this date, each with the instant in
    /// <see cref="DailyTransitSnapshot.EntryTime"/>. One that enters after noon is not yet in
    /// <see cref="ActiveTransits"/>.
    /// </summary>
    public required IReadOnlyList<DailyTransitSnapshot> EnteringToday { get; init; }

    /// <summary>
    /// Transits leaving orb during this date, each with the instant in
    /// <see cref="DailyTransitSnapshot.ExitTime"/>. One that leaves before noon is no longer in
    /// <see cref="ActiveTransits"/>.
    /// </summary>
    public required IReadOnlyList<DailyTransitSnapshot> ExitingToday { get; init; }
}

/// <summary>
/// Snapshot of a single transit aspect's status on a specific day.
/// </summary>
public sealed class DailyTransitSnapshot
{
    /// <summary>The transiting body name (e.g. "TR Jupiter").</summary>
    public required string TransitPlanet { get; init; }

    /// <summary>The aspect name (e.g. "conjunction", "trine").</summary>
    public required string Aspect { get; init; }

    /// <summary>The aspect abbreviation (e.g. "cnj", "tri").</summary>
    public required string AspectAbbrev { get; init; }

    /// <summary>The natal target name (e.g. "NA Sun").</summary>
    public required string NatalTarget { get; init; }

    /// <summary>The natal house number the target occupies (1–12).</summary>
    public required int NatalHouse { get; init; }

    /// <summary>The natal house the transiting planet is passing through (1–12).</summary>
    public required int TransitHouse { get; init; }

    /// <summary>The orb at local noon on this day, in degrees: the transiting body's separation from the natal point, less the aspect's angle.</summary>
    public required double OrbDegrees { get; init; }

    /// <summary>"Applying", "Exact", or "Separating".</summary>
    public required string Phase { get; init; }

    /// <summary>
    /// True if this transit becomes exact (partile) on this date.
    /// </summary>
    public required bool IsExactToday { get; init; }

    /// <summary>
    /// The exact pass time(s) on this date, if any: each pass whose instant lies in the local day,
    /// as its reading on the day's clock, "HH:mm", with the zone's name where the clocks repeat
    /// the reading ("01:30 EST").
    /// </summary>
    public IReadOnlyList<string> ExactTimes { get; init; } = [];

    /// <summary>
    /// The instant the transit enters orb during this date, as its reading on the day's clock,
    /// "HH:mm", with the zone's name where the clocks repeat the reading; null if it does not
    /// enter orb this date.
    /// </summary>
    public string? EntryTime { get; init; }

    /// <summary>
    /// The instant the transit leaves orb during this date, as its reading on the day's clock,
    /// "HH:mm", with the zone's name where the clocks repeat the reading; null if it does not
    /// leave orb this date.
    /// </summary>
    public string? ExitTime { get; init; }
}
