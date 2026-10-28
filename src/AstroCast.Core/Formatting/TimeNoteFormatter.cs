using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Builds a chart's Time Note: the one or two plain sentences that say how its wall clock
/// was read when the reading was not a single instant on its zone's standard time (rule D17,
/// open question Q5, TZ card 4). A value formatter in the manner of
/// <see cref="OffsetLabelFormatter"/>: the chart engine calls it once and records the text on
/// the result, and every export prints that text.
/// </summary>
/// <remarks>
/// <para>
/// <b>Three cases, and only three.</b>
/// </para>
/// <list type="bullet">
/// <item><description>
/// <b>Repeated</b> — the clocks went back over the reading, so it names two instants. The
/// Note gives both in UT with the zone record's name for each, says the chart is cast for
/// the later (D17), and that an explicit offset of the earlier offset casts it for the
/// earlier.
/// </description></item>
/// <item><description>
/// <b>Skipped</b> — the clocks went forward over the reading, so it names none. The Note
/// gives the gap, from the clock before the change to the clock after it, and the instant
/// the chart is cast for: the first after the gap, with its name and in UT.
/// </description></item>
/// <item><description>
/// <b>Local mean time</b> — the instant falls where the zone record is named "LMT": before
/// standard time the data gives the local mean time of the zone's reference city, to the
/// second. The Note names that city (from the canonical zone identifier) and the offset, and
/// says that an explicit offset gives the birthplace's own mean time instead (TZ card 4).
/// </description></item>
/// </list>
/// <para>
/// A reading that occurs once on any other time, and any chart cast at an explicit offset,
/// has no Note. Every instant and offset comes from the zone's own record, never from
/// subtracting an hour.
/// </para>
/// <para>
/// <b>Culture-independent.</b> Every date, time and number goes through the invariant
/// culture, and the offsets through <see cref="OffsetLabelFormatter"/>, so the Note reads
/// the same on any machine.
/// </para>
/// </remarks>
public static class TimeNoteFormatter
{
    /// <summary>Invariant culture used for all date, time and numeric formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>The zone record's name for local mean time in the IANA data.</summary>
    private const string LocalMeanTimeName = "LMT";

    /// <summary>
    /// Builds the Time Note of a chart cast through its zone.
    /// </summary>
    /// <param name="reading">The wall-clock reading as entered, on the zone's clock.</param>
    /// <param name="zoneId">The zone identifier as entered, printed as the chart's Date/Time line prints it.</param>
    /// <param name="referenceZoneId">
    /// The canonical identifier of that zone, whose last part names the city whose local mean
    /// time the data gives ("America/New_York" gives "New York"). An alias names the city of
    /// the zone it stands for, whose data it converts with.
    /// </param>
    /// <param name="zone">
    /// How the reading maps under D17: the later instant of a repeated reading, the first
    /// instant after the gap of a skipped one, with the other side of the change of clock.
    /// </param>
    /// <param name="castUtcDateTime">
    /// The instant the chart is cast at: <paramref name="zone"/>'s own instant, except for an
    /// electional candidate sampled at the earlier instant of a repeated reading.
    /// </param>
    /// <returns>The Note, or <see langword="null"/> when the reading needs none.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="zoneId"/>, <paramref name="referenceZoneId"/> or
    /// <paramref name="zone"/> is <see langword="null"/>.
    /// </exception>
    public static string? Format(
        DateTime reading, string zoneId, string referenceZoneId, ZoneInstant zone, DateTime castUtcDateTime)
    {
        ArgumentNullException.ThrowIfNull(zoneId);
        ArgumentNullException.ThrowIfNull(referenceZoneId);
        ArgumentNullException.ThrowIfNull(zone);

        DateTime day = reading.Date;

        if (zone.Mapping == LocalTimeMapping.Repeated
            && zone.EarlierUtcDateTime is DateTime earlier
            && zone.EarlierOffset is TimeSpan earlierOffset)
        {
            string earlierLabel = OffsetLabelFormatter.Format(earlierOffset, zone.EarlierAbbreviation, isExplicit: false);
            string laterLabel = OffsetLabelFormatter.Format(zone.Offset, zone.Abbreviation, isExplicit: false);
            string castFor = castUtcDateTime == earlier
                ? "The chart is cast for the earlier."
                : string.Create(Inv,
                    $"The chart is cast for the later; an explicit offset of {OffsetLabelFormatter.Format(earlierOffset, null, isExplicit: false)} casts it for the earlier.");

            return string.Create(Inv,
                $"{Clock(reading)} occurred twice on {day:yyyy-MM-dd} in {zoneId}: at {Stamp(earlier, day)} UT on {earlierLabel}, and again at {Stamp(zone.UtcDateTime, day)} UT on {laterLabel}. {castFor}");
        }

        if (zone.Mapping == LocalTimeMapping.Skipped && zone.EarlierOffset is TimeSpan beforeOffset)
        {
            // The gap runs from the change of clock read on the old offset to the same instant
            // read on the new one: both from the zone's own record.
            DateTime from = DateTime.SpecifyKind(zone.UtcDateTime + beforeOffset, DateTimeKind.Unspecified);
            DateTime to = zone.ResolvedLocalDateTime;
            bool dated = from.Date != day || to.Date != day;
            string label = OffsetLabelFormatter.Format(zone.Offset, zone.Abbreviation, isExplicit: false);

            return string.Create(Inv,
                $"{Clock(reading)} did not occur on {day:yyyy-MM-dd} in {zoneId}: the clocks went from {Wall(from, dated)} to {Wall(to, dated)}. The chart is cast for the first instant after the gap, {Wall(to, dated)} {label}, which is {Stamp(zone.UtcDateTime, day)} UT.");
        }

        if (zone.Mapping == LocalTimeMapping.Unique
            && string.Equals(zone.Abbreviation, LocalMeanTimeName, StringComparison.Ordinal))
        {
            string offset = OffsetLabelFormatter.Format(zone.Offset, null, isExplicit: false);

            return string.Create(Inv,
                $"Before standard time, the time in {zoneId} is the local mean time of {City(referenceZoneId)}, {offset}; the chart is cast for {Stamp(castUtcDateTime, day)} UT. An explicit offset gives the birthplace's own mean time.");
        }

        return null;
    }

    /// <summary>
    /// A clock time: <c>HH:mm</c>, or <c>HH:mm:ss</c> when it carries seconds.
    /// </summary>
    /// <param name="value">The time.</param>
    /// <returns>The time of day.</returns>
    private static string Clock(DateTime value) =>
        value.Second == 0 ? value.ToString("HH:mm", Inv) : value.ToString("HH:mm:ss", Inv);

    /// <summary>
    /// A UT instant as the Note prints it: the time, preceded by its date on the calendar in force
    /// (<see cref="CalendarDateFormatter"/>, branch R2, 1.3.4) when that is not the date of the
    /// reading as typed.
    /// </summary>
    /// <param name="utc">The instant.</param>
    /// <param name="day">The date of the reading.</param>
    /// <returns>The instant.</returns>
    private static string Stamp(DateTime utc, DateTime day) =>
        CalendarDateFormatter.Date(utc) == day.ToString("yyyy-MM-dd", Inv)
            ? Clock(utc)
            : $"{CalendarDateFormatter.Date(utc)} {Clock(utc)}";

    /// <summary>
    /// A clock derived from the instant of a change of the clocks, with its date on the calendar in
    /// force when the gap it bounds crosses a day.
    /// </summary>
    /// <param name="value">The clock.</param>
    /// <param name="dated">Whether to print the date.</param>
    /// <returns>The clock.</returns>
    private static string Wall(DateTime value, bool dated) =>
        dated ? $"{CalendarDateFormatter.Date(value)} {Clock(value)}" : Clock(value);

    /// <summary>
    /// The city a zone identifier names: its last part, with underscores as spaces
    /// ("America/Argentina/Buenos_Aires" gives "Buenos Aires").
    /// </summary>
    /// <param name="zoneId">The canonical zone identifier.</param>
    /// <returns>The city.</returns>
    private static string City(string zoneId) =>
        zoneId[(zoneId.LastIndexOf('/') + 1)..].Replace('_', ' ');
}
