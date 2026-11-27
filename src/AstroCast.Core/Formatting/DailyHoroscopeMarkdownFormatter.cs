using System.Globalization;
using System.Text;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a <see cref="TransitResult"/> into a per-day Markdown digest for AI
/// interpretation as daily horoscopes. The days themselves (Moon sign, Moon phase,
/// Void of Course information and transit snapshots) are built by
/// <see cref="DailyHoroscopeService.BuildDays"/>; this class only renders them.
/// </summary>
public static class DailyHoroscopeMarkdownFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    #region Slow-Planet Classification

    /// <summary>
    /// Transit bodies considered "slow-moving" for the backdrop section.
    /// Jupiter through Pluto, plus Chiron and North Node.
    /// </summary>
    private static readonly HashSet<TransitBody> SlowPlanets =
    [
        TransitBody.Jupiter,
        TransitBody.Saturn,
        TransitBody.Uranus,
        TransitBody.Neptune,
        TransitBody.Pluto,
        TransitBody.Chiron,
        TransitBody.NorthNode,
    ];

    /// <summary>
    /// Display sort order for transit bodies (outermost first).
    /// </summary>
    private static readonly Dictionary<TransitBody, int> BodySortOrder = new()
    {
        [TransitBody.Pluto] = 0,
        [TransitBody.Neptune] = 1,
        [TransitBody.Uranus] = 2,
        [TransitBody.Saturn] = 3,
        [TransitBody.Jupiter] = 4,
        [TransitBody.Chiron] = 5,
        [TransitBody.NorthNode] = 6,
        [TransitBody.Mars] = 7,
    };

    #endregion

    #region Public API

    /// <summary>
    /// Formats a transit result and pre-built daily data into a complete daily horoscope
    /// Markdown document.
    /// </summary>
    /// <param name="result">The transit calculation result.</param>
    /// <param name="days">Pre-built per-day data from <see cref="DailyHoroscopeService.BuildDays"/>.</param>
    /// <param name="birthTimeSource">Birth time source (e.g., "Birth certificate").</param>
    /// <param name="interpretationContext">Optional free-text context for AI interpretation.</param>
    /// <param name="timeZoneId">
    /// IANA timezone for local time display: the zone the days were built on. The header
    /// names it as the clock the days and every time in them are on, for a chart cast at an
    /// explicit offset as for any other, and the backdrop's dates are read on it (on the natal
    /// chart's zone when it is omitted).
    /// </param>
    /// <returns>Complete Markdown string.</returns>
    public static string ToMarkdown(
        TransitResult result,
        IReadOnlyList<DailyHoroscopeDay> days,
        string? birthTimeSource = null,
        string? interpretationContext = null,
        string? timeZoneId = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(days);

        string effectiveSource = string.IsNullOrEmpty(birthTimeSource) ? "Not specified" : birthTimeSource;
        bool unknownBirthTime = string.Equals(birthTimeSource, "Unknown", StringComparison.OrdinalIgnoreCase);

        StringBuilder sb = new();

        AppendHeader(sb, result, days, effectiveSource, interpretationContext, timeZoneId);
        AppendNatalChartSummary(sb, result, unknownBirthTime);
        AppendSlowMovingBackdrop(sb, result, timeZoneId);
        AppendDailySnapshots(sb, days, unknownBirthTime);
        AppendFooter(sb, result, days.Count == 0);

        return sb.ToString();
    }

    /// <summary>
    /// Save daily horoscope data as Markdown to a file.
    /// </summary>
    /// <param name="result">The transit calculation result.</param>
    /// <param name="days">Pre-built per-day data.</param>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="birthTimeSource">Birth time source.</param>
    /// <param name="interpretationContext">Optional AI context.</param>
    /// <param name="timeZoneId">
    /// IANA timezone for local time display: the zone the days were built on. The header
    /// names it as the clock the days and every time in them are on, for a chart cast at an
    /// explicit offset as for any other, and the backdrop's dates are read on it (on the natal
    /// chart's zone when it is omitted).
    /// </param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(
        TransitResult result,
        IReadOnlyList<DailyHoroscopeDay> days,
        string filePath,
        string? birthTimeSource = null,
        string? interpretationContext = null,
        string? timeZoneId = null,
        CancellationToken ct = default)
    {
        string md = ToMarkdown(result, days, birthTimeSource, interpretationContext, timeZoneId);
        await File.WriteAllTextAsync(filePath, md, ct).ConfigureAwait(false);
    }

    #endregion

    #region Header

    /// <summary>
    /// Appends the document header with chart metadata.
    /// </summary>
    private static void AppendHeader(
        StringBuilder sb,
        TransitResult result,
        IReadOnlyList<DailyHoroscopeDay> days,
        string birthTimeSource,
        string? interpretationContext,
        string? timeZoneId)
    {
        ChartInput input = result.NatalChart.Input;
        int dayCount = days.Count;

        sb.AppendLine("# Daily Horoscope Data");
        sb.AppendLine();
        sb.AppendLine($"**Name:** {input.Name}  ");
        // An explicit offset overrides the zone's rules, so the birth wall clock is labelled
        // with that offset, as the chart export's Date/Time line labels it, never with the zone.
        string natalClock = input.UseExplicitOffset && input.UtcOffsetHours.HasValue
            ? result.NatalChart.ResolvedOffsetLabel
            : input.TimeZone;
        sb.AppendLine(Inv, $"**Natal Date:** {input.LocalDateTime:yyyy-MM-dd}, {input.LocalDateTime:HH:mm:ss} {natalClock}, {input.LocationName}  ");
        sb.AppendLine($"**Birth Time Source:** {birthTimeSource}  ");
        sb.AppendLine(Inv, $"**Report Period:** {FormatDate(result.ReportStartDate)} to {FormatDate(result.ReportEndDate)} ({dayCount} {(dayCount == 1 ? "day" : "days")})  ");
        if (CalendarDateFormatter.IsJulian(result.NatalChart.UtcDateTime) || result.ReportStartDate < CalendarDateFormatter.GregorianCalendarStartDate)
            sb.AppendLine(CalendarDateFormatter.JulianCalendarLine + "  ");
        sb.AppendLine(Inv, $"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Software:** AstroCast \u2014 Swiss Ephemeris, Tropical, {result.NatalChart.HouseSystemName}  ");

        // The days are the reader's present days, on the zone's clock, whatever clock the birth
        // time was entered on: an explicit offset fixes only the birth instant. Every time in a
        // day is a reading on the same clock.
        if (!string.IsNullOrWhiteSpace(timeZoneId))
        {
            sb.AppendLine($"**Timezone:** {timeZoneId} — the days and every time in them (void of course, exact, entering and leaving orb, the Moon's sign change) are on this zone's clock  ");
            sb.AppendLine("**Orbs and Phases:** at 12:00 each day on this clock; a transit is listed when it is in orb at 12:00");
        }

        if (!string.IsNullOrWhiteSpace(interpretationContext))
        {
            sb.AppendLine();
            sb.AppendLine($"**Interpretation Context:** {interpretationContext.Trim()}");
        }

        sb.AppendLine();
    }

    #endregion

    #region Natal Chart Summary

    /// <summary>
    /// Appends a compact natal positions table.
    /// </summary>
    private static void AppendNatalChartSummary(
        StringBuilder sb, TransitResult result, bool unknownBirthTime)
    {
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Natal Chart Summary");
        sb.AppendLine();
        sb.AppendLine("| Body | Position | House |");
        sb.AppendLine("|------|----------|-------|");

        foreach (PlanetPosition pos in result.NatalChart.Positions)
        {
            string house = unknownBirthTime ? "N/A" : (pos.House?.ToString(Inv) ?? "N/A");
            sb.AppendLine($"| {pos.Name} | {FormatLongitude(pos.Longitude)} | {house} |");
        }

        if (!unknownBirthTime)
        {
            HouseData houses = result.NatalChart.Houses;
            sb.AppendLine($"| ASC | {FormatLongitude(houses.Ascendant)} | 1 |");
            sb.AppendLine($"| MC | {FormatLongitude(houses.Midheaven)} | 10 |");
        }

        sb.AppendLine();
    }

    #endregion

    #region Slow-Moving Transit Backdrop

    /// <summary>
    /// Appends the slow-moving transit backdrop — outer planet transits active
    /// during the report period that set the weekly/monthly context.
    /// </summary>
    /// <param name="sb">The document being written.</param>
    /// <param name="result">The transit calculation result.</param>
    /// <param name="timeZoneId">The zone the days are on; the natal chart's zone when null.</param>
    private static void AppendSlowMovingBackdrop(StringBuilder sb, TransitResult result, string? timeZoneId)
    {
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Slow-Moving Transit Backdrop");
        sb.AppendLine();

        List<ActiveTransit> slowTransits = result.ActiveTransits
            .Where(t => SlowPlanets.Contains(t.TransitPlanet))
            .OrderBy(t => BodySortOrder.GetValueOrDefault(t.TransitPlanet, 99))
            .ToList();

        if (slowTransits.Count == 0)
        {
            sb.AppendLine("No slow-moving transits active during this period.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Transit | Aspect | Natal Target | House | In Orb |");
        sb.AppendLine("|---------|--------|--------------|-------|--------|");

        foreach (ActiveTransit transit in slowTransits)
        {
            string inOrbRange = FormatLifecycleDateRange(transit, result, timeZoneId);
            sb.AppendLine(Inv, $"| TR {DailyHoroscopeService.FormatPlanetName(transit.TransitPlanet)} | {transit.AspectAbbreviation} | NA {transit.NatalTarget} | {transit.NatalHouseNumber} | {inOrbRange} |");
        }

        sb.AppendLine();
    }

    #endregion

    #region Daily Snapshots

    /// <summary>
    /// Appends the footer: the engine version, as the chart export prints it ("AstroCast
    /// 1.3.3 (SwissEph 2.10.03, tzdb 2026e)"), so the file can be dated to the version that
    /// made it (E2-9). The version is the natal chart's: the days are read against it, by the
    /// same engine, in the same run. Every day closes with a rule, the one before it; with no
    /// days the export ends at the Daily Snapshots heading, and a rule comes first. The header's
    /// Generated and Software lines are unchanged.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="result">The transit result, whose natal chart carries the engine version.</param>
    /// <param name="noDays">Whether the report has no days, so no day's closing rule precedes it.</param>
    private static void AppendFooter(StringBuilder sb, TransitResult result, bool noDays)
    {
        if (noDays)
            sb.AppendLine("---");
        sb.AppendLine($"*Engine: {result.NatalChart.EngineVersion}*");
    }

    /// <summary>
    /// Appends all daily snapshot sections.
    /// </summary>
    private static void AppendDailySnapshots(
        StringBuilder sb, IReadOnlyList<DailyHoroscopeDay> days, bool unknownBirthTime)
    {
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Daily Snapshots");
        sb.AppendLine();

        foreach (DailyHoroscopeDay day in days)
        {
            AppendSingleDay(sb, day, unknownBirthTime);
        }
    }

    /// <summary>
    /// Appends a single day's snapshot section.
    /// </summary>
    private static void AppendSingleDay(
        StringBuilder sb, DailyHoroscopeDay day, bool unknownBirthTime)
    {
        // Day header: the day is a proleptic day (branch R3, 1.3.4), printed on the calendar in
        // force with its own weekday: proleptic 1301-03-15, Julian 1301-03-07, reads "Tuesday,
        // March 7, 1301".
        sb.AppendLine($"### {CalendarDateFormatter.Format(day.Date, "dddd, MMMM d, yyyy")}");

        // Moon line
        string vocDisplay = day.VoidOfCoursePeriods.Count > 0
            ? string.Join("; ", day.VoidOfCoursePeriods)
            : "None";
        // The Moon at noon, then its sign change in the day, if any, in parentheses.
        string signChange = day.MoonSignChange is null ? string.Empty : $" ({day.MoonSignChange})";
        sb.AppendLine($"**Moon:** {day.MoonSign} ({day.MoonPhaseLabel}){signChange} | **VoC:** {vocDisplay}");
        sb.AppendLine();

        // Exact Today table
        if (day.ExactToday.Count > 0)
        {
            sb.AppendLine("**Exact Today:**");
            sb.AppendLine();

            if (unknownBirthTime)
            {
                sb.AppendLine("| Transit | Aspect | Natal Target | Time |");
                sb.AppendLine("|---------|--------|--------------|------|");
            }
            else
            {
                sb.AppendLine("| Transit | Aspect | Natal Target | House | Time |");
                sb.AppendLine("|---------|--------|--------------|-------|------|");
            }

            foreach (DailyTransitSnapshot snapshot in day.ExactToday)
            {
                string times = snapshot.ExactTimes.Count > 0
                    ? string.Join(", ", snapshot.ExactTimes)
                    : "\u2014";

                if (unknownBirthTime)
                {
                    sb.AppendLine($"| {snapshot.TransitPlanet} | {snapshot.AspectAbbrev} | {snapshot.NatalTarget} | {times} |");
                }
                else
                {
                    sb.AppendLine(Inv, $"| {snapshot.TransitPlanet} | {snapshot.AspectAbbrev} | {snapshot.NatalTarget} | {snapshot.NatalHouse} | {times} |");
                }
            }

            sb.AppendLine();
        }

        // Active (in orb) table
        if (day.ActiveTransits.Count > 0)
        {
            sb.AppendLine("**Active (in orb):**");
            sb.AppendLine();

            if (unknownBirthTime)
            {
                sb.AppendLine("| Transit | Aspect | Natal Target | Orb | Phase |");
                sb.AppendLine("|---------|--------|--------------|-----|-------|");
            }
            else
            {
                sb.AppendLine("| Transit | Aspect | Natal Target | House | Orb | Phase |");
                sb.AppendLine("|---------|--------|--------------|-------|-----|-------|");
            }

            foreach (DailyTransitSnapshot snapshot in day.ActiveTransits)
            {
                string orbFormatted = PositionFormatter.FormatOrb(snapshot.OrbDegrees);

                if (unknownBirthTime)
                {
                    sb.AppendLine($"| {snapshot.TransitPlanet} | {snapshot.AspectAbbrev} | {snapshot.NatalTarget} | {orbFormatted} | {snapshot.Phase} |");
                }
                else
                {
                    sb.AppendLine(Inv, $"| {snapshot.TransitPlanet} | {snapshot.AspectAbbrev} | {snapshot.NatalTarget} | {snapshot.NatalHouse} | {orbFormatted} | {snapshot.Phase} |");
                }
            }

            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("No transits of Jupiter, Saturn, Uranus, Neptune, Pluto, Chiron, the North Node or a slow Mars today.");
            sb.AppendLine();
        }

        // Entering orb
        if (day.EnteringToday.Count > 0)
        {
            string entering = string.Join(", ", day.EnteringToday.Select(s =>
                string.Create(Inv, $"{s.TransitPlanet} {s.AspectAbbrev} {s.NatalTarget} (H{s.NatalHouse}){AtTime(s.EntryTime)}")));
            sb.AppendLine($"**Entering orb:** {entering}");
            sb.AppendLine();
        }

        // Exiting orb
        if (day.ExitingToday.Count > 0)
        {
            string exiting = string.Join(", ", day.ExitingToday.Select(s =>
                string.Create(Inv, $"{s.TransitPlanet} {s.AspectAbbrev} {s.NatalTarget} (H{s.NatalHouse}){AtTime(s.ExitTime)}")));
            sb.AppendLine($"**Exiting orb:** {exiting}");
            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    #endregion

    #region Formatting Helpers

    /// <summary>
    /// The instant an Entering or Exiting entry carries, " at 18:04", or nothing when the snapshot
    /// has none.
    /// </summary>
    /// <param name="time">The entry's or exit's printed time, or null.</param>
    /// <returns>" at " and the time, or <see cref="string.Empty"/>.</returns>
    private static string AtTime(string? time) => time is null ? string.Empty : " at " + time;

    /// <summary>
    /// Formats a picked <see cref="DateOnly"/>, a reading (the report's bounds), as typed (yyyy-MM-dd).
    /// </summary>
    private static string FormatDate(DateOnly date) => date.ToString("yyyy-MM-dd", Inv);

    /// <summary>
    /// Formats an ecliptic longitude as DD°MM' Sign.
    /// </summary>
    private static string FormatLongitude(double longitude)
    {
        ZodiacSign sign = DailyHoroscopeService.LongitudeToSign(longitude);
        double withinSign = longitude % 30.0;
        int deg = (int)withinSign;
        int min = (int)((withinSign - deg) * 60.0);
        return string.Create(Inv, $"{deg:D2}\u00b0{min:D2}' {sign}");
    }

    /// <summary>
    /// Formats the lifecycle date range for an active transit (entry to exit): the local dates of
    /// the entry and exit instants on the days' clock, "(before)" when the entry's local date falls
    /// before the report and "(beyond)" when the exit's falls after it.
    /// </summary>
    /// <remarks>
    /// Until 1.3.3 the cells printed the instants' UT dates under the local-zone header, and the
    /// (before)/(beyond) flags came from those UT dates (E2-7): an entry at 00:38 UT on
    /// 2026-10-15 printed 2026-10-15 for a New York reader, for whom it was 20:38 on the 14th.
    /// </remarks>
    /// <param name="transit">The transit.</param>
    /// <param name="result">The transit calculation result, for the report's dates and the natal chart.</param>
    /// <param name="timeZoneId">The zone the days are on; the natal chart's zone when null.</param>
    /// <returns>"2026-10-14 – (beyond)" and the like.</returns>
    private static string FormatLifecycleDateRange(ActiveTransit transit, TransitResult result, string? timeZoneId)
    {
        TransitLifecycleEvent? entry = transit.Lifecycle
            .FirstOrDefault(e => e.EventType == TransitLifecycleEventType.Entry);
        TransitLifecycleEvent? exit = transit.Lifecycle
            .FirstOrDefault(e => e.EventType == TransitLifecycleEventType.Exit);

        ITimeZoneRules rules = result.NatalChart.TimeZoneRules ?? new TzdbTimeZoneRules();
        string zone = timeZoneId ?? result.NatalChart.Input.TimeZone;
        DateOnly LocalDate(DateTime utc) => DateOnly.FromDateTime(rules.ToLocal(utc, zone).LocalDateTime);

        // The report's bounds as the proleptic days they denote (branch R3, 1.3.4), compared with a
        // local date derived from an instant.
        DateOnly firstDay = CalendarDateFormatter.ProlepticDay(result.ReportStartDate);
        DateOnly lastDay = CalendarDateFormatter.ProlepticDay(result.ReportEndDate);

        string start = entry is not null
            ? (LocalDate(entry.Date) < firstDay ? "(before)" : CalendarDateFormatter.Format(LocalDate(entry.Date), "yyyy-MM-dd"))
            : "?";
        string end = exit is not null
            ? (LocalDate(exit.Date) > lastDay ? "(beyond)" : CalendarDateFormatter.Format(LocalDate(exit.Date), "yyyy-MM-dd"))
            : "?";

        return $"{start} \u2013 {end}";
    }

    #endregion
}
