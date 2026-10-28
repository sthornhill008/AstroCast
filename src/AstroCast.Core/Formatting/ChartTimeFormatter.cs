using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats instants on a chart's own clock. A value formatter in the manner of
/// <see cref="PositionFormatter"/>: it reads only the chart result it is given,
/// so services may call it without depending on any document formatter.
/// </summary>
/// <remarks>
/// The helper previously lived on <see cref="ChartMarkdownFormatter"/>, which
/// left the horary analysis — a service — depending on a document formatter
/// for its perfection, station and prohibition times. Its output is unchanged
/// by the move.
/// </remarks>
internal static class ChartTimeFormatter
{
    /// <summary>
    /// Formats an instant to the minute on the chart's own clock, in the same
    /// "local time (zone)" form as the Date/Time and Hour Period lines. Used by
    /// the Stationing table and by the horary analysis for its perfection,
    /// station and prohibition times.
    /// </summary>
    /// <param name="utc">The instant to format, in UTC.</param>
    /// <param name="result">The chart whose clock the instant is rendered on.</param>
    /// <returns>
    /// The instant as <c>yyyy-MM-dd HH:mm</c> local time, on the calendar in force on its date
    /// (<see cref="CalendarDateFormatter"/>, branch R2, 1.3.4), followed by the zone in
    /// parentheses (e.g. "2026-10-23 00:40 (America/New_York)"), or followed by
    /// the chart's resolved offset label when the chart was computed with an
    /// explicit UTC offset (e.g. "2026-10-23 04:40 UTC+0 (explicit)").
    /// </returns>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when the chart has no explicit offset and its time-zone identifier
    /// is not in the chart's time-zone rules.
    /// </exception>
    /// <remarks>
    /// A chart computed through its IANA zone converts through that zone, not
    /// through the chart's resolved offset, so a station on the far side of a
    /// daylight-saving change gets its own offset. It converts on the rules the
    /// chart was cast with (<see cref="ChartResult.TimeZoneRules"/>), or, for a
    /// result the engine did not cast, on the data the app carries. A chart computed with an
    /// explicit UTC offset — the user overriding the zone's rules, the same
    /// condition <c>ChartEngine.ConvertInputToJulianDay</c> tests — keeps that
    /// fixed offset for the station as well and is labelled with the chart's
    /// resolved offset label (e.g. "UTC−5 (explicit)"), so the station time
    /// stays on the clock the Date/Time line was entered on rather than on the
    /// rules the user chose to override.
    /// </remarks>
    internal static string FormatLocalTime(DateTime utc, ChartResult result)
    {
        if (result.Input.UseExplicitOffset && result.Input.UtcOffsetHours.HasValue)
        {
            DateTime fixedLocal = utc.AddHours(result.ResolvedUtcOffsetHours);
            return $"{CalendarDateFormatter.Minutes(fixedLocal)} {result.ResolvedOffsetLabel}";
        }

        DateTime local = (result.TimeZoneRules ?? new TzdbTimeZoneRules())
            .ToLocal(utc, result.Input.TimeZone).LocalDateTime;
        return $"{CalendarDateFormatter.Minutes(local)} ({result.Input.TimeZone})";
    }

    /// <summary>
    /// The offset that follows a printed time when its local reading is one the clocks repeat:
    /// a space and the abbreviation in force at the instant, from the app's tzdb ("NDT"), or the
    /// offset where the zone's name is not a word; otherwise empty. The electional export's period
    /// bounds and the daily horoscope's times both print it.
    /// </summary>
    /// <remarks>
    /// Moved from <see cref="ElectionalMarkdownFormatter"/> in 1.3.3 so that both exports read a
    /// repeated reading by one rule; its body is unchanged.
    /// </remarks>
    /// <param name="local">The local reading, as printed.</param>
    /// <param name="utc">The instant, <see cref="DateTimeKind.Utc"/>.</param>
    /// <param name="rules">The time-zone rules to read the reading and the instant on.</param>
    /// <param name="zone">The IANA zone the reading is on.</param>
    /// <returns>" NDT", " UTC−3" or <see cref="string.Empty"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="rules"/> is null.</exception>
    internal static string RepeatedReadingOffset(DateTime local, DateTime utc, ITimeZoneRules rules, string zone)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (rules.ToUniversal(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), zone).Mapping != LocalTimeMapping.Repeated)
        {
            return string.Empty;
        }

        ZoneReading reading = rules.ToLocal(utc, zone);
        return " " + ZoneWord(OffsetLabelFormatter.Format(reading.Offset, reading.Abbreviation, isExplicit: false));
    }

    /// <summary>
    /// The short form of an offset label: the zone's name where the label has one ("EDT" from
    /// "EDT (UTC−4)"), the label itself otherwise ("UTC−3").
    /// </summary>
    /// <param name="offsetLabel">A label as <see cref="OffsetLabelFormatter.Format"/> writes it.</param>
    /// <returns>The name, or the label.</returns>
    internal static string ZoneWord(string offsetLabel)
    {
        int open = offsetLabel.IndexOf(" (", StringComparison.Ordinal);
        return open > 0 ? offsetLabel[..open] : offsetLabel;
    }
}
