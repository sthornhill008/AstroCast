using System.Globalization;
using System.Text;

using AstroCast.Core.Enums;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a <see cref="TransitResult"/> as a self-contained chronological timeline
/// with embedded natal chart data, Transit Windows (lifecycle summaries per aspect),
/// a date-ordered Events table (exact passes, entry/exit, stations, ingresses,
/// midpoint activations, transit-to-transit aspects, and declination aspects),
/// and Cluster Analysis (convergence zone detection with Event Type B date
/// recommendations). The embedded natal chart makes the export self-contained
/// for AI interpretation without requiring a separate natal chart upload. When the
/// natal chart carries Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsTiming,
/// 1.6.0; R7), the transits to them have Transit Windows and Events sections of their
/// own after the chart's, and the Clusters read the chart's events only (scoping
/// decision 5).
/// </summary>
public static class TransitTimelineMarkdownFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Formats the transit result as an enhanced timeline Markdown document.
    /// </summary>
    /// <param name="result">The transit calculation result.</param>
    /// <param name="birthTimeSource">
    /// Optional birth time source description (e.g., "Birth certificate",
    /// "Rectified"). Displayed in the header when provided.
    /// </param>
    /// <param name="interpretationContext">
    /// Optional interpretation context note (e.g., "Focus on career transits").
    /// Displayed in the header when provided.
    /// </param>
    /// <param name="timeProvider">
    /// Clock forwarded to <see cref="ChartMarkdownFormatter.ToMarkdown"/> for the
    /// embedded natal chart so its date-keyed profection / firdaria / zodiacal
    /// releasing sections resolve deterministically. Defaults to
    /// <see cref="TimeProvider.System"/> when <c>null</c>, preserving production
    /// behaviour; golden tests pass a fixed <c>FakeTimeProvider</c> (F-GR-2).
    /// </param>
    /// <returns>Complete Markdown string with Transit Windows, Events table, and Clusters.</returns>
    public static string ToMarkdown(
        TransitResult result,
        string? birthTimeSource = null,
        string? interpretationContext = null,
        TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        StringBuilder sb = new();

        AppendHeader(sb, result, birthTimeSource, interpretationContext);
        AppendNatalChartSection(sb, result, timeProvider);
        AppendTransitWindows(sb, "## Transit Windows", result.ActiveTransits);
        if (CarriesMinorPoints(result))
            AppendTransitWindows(sb, "## Transit Windows: Asteroids and Lilith", result.MinorPointTransits);
        AppendTimeline(sb, result);
        if (CarriesMinorPoints(result))
            AppendMinorPointTimeline(sb, result);
        AppendClusters(sb, result);
        AppendFooter(sb, result.NatalChart.HouseSystemName);
        AppendEngineFooter(sb, result);

        return sb.ToString();
    }

    /// <summary>
    /// Save a transit timeline as Markdown to a file.
    /// </summary>
    /// <param name="result">The transit calculation result.</param>
    /// <param name="filePath">Destination file path for the Markdown output.</param>
    /// <param name="birthTimeSource">Optional birth time source description.</param>
    /// <param name="interpretationContext">Optional interpretation context note.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(
        TransitResult result, string filePath,
        string? birthTimeSource = null,
        string? interpretationContext = null,
        CancellationToken ct = default)
    {
        string md = ToMarkdown(result, birthTimeSource, interpretationContext);
        await File.WriteAllTextAsync(filePath, md, ct).ConfigureAwait(false);
    }

    #region Header

    /// <summary>
    /// Appends the document header with chart metadata and optional context fields.
    /// </summary>
    private static void AppendHeader(
        StringBuilder sb, TransitResult result,
        string? birthTimeSource, string? interpretationContext)
    {
        Models.ChartInput input = result.NatalChart.Input;

        sb.AppendLine("# Transit Timeline");
        sb.AppendLine();
        sb.AppendLine($"**Name:** {input.Name}");
        sb.AppendLine(Inv, $"**Report Period:** {result.ReportStartDate:yyyy-MM-dd} to {result.ReportEndDate:yyyy-MM-dd}");
        if (CalendarDateFormatter.IsJulian(result.NatalChart.UtcDateTime) || result.ReportStartDate < CalendarDateFormatter.GregorianCalendarStartDate)
            sb.AppendLine(CalendarDateFormatter.JulianCalendarLine);

        if (!string.IsNullOrWhiteSpace(birthTimeSource))
            sb.AppendLine($"**Birth Time Source:** {birthTimeSource}");

        if (!string.IsNullOrWhiteSpace(interpretationContext))
            sb.AppendLine($"**Context:** {interpretationContext}");

        sb.AppendLine(Inv, $"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    #endregion

    #region Natal Chart

    /// <summary>
    /// Appends the full natal chart data by delegating to <see cref="ChartMarkdownFormatter"/>.
    /// Embeds the natal chart between the header and the Transit Windows section,
    /// making the timeline export self-contained for AI interpretation.
    /// </summary>
    /// <remarks>
    /// The natal chart provides essential context for timeline interpretation:
    /// fixed star conjunctions (for reactivation notes), house rulerships,
    /// declination clusters, essential dignities, profection/firdaria/ZR data,
    /// and planet conditions. Without this section, AI interpreters cannot
    /// produce rich descriptions that name each natal point's structural role.
    /// </remarks>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="result">The transit result containing the natal chart.</param>
    /// <param name="timeProvider">
    /// Clock forwarded to <see cref="ChartMarkdownFormatter.ToMarkdown"/> so the
    /// natal time-lord sections are date-deterministic under test.
    /// </param>
    private static void AppendNatalChartSection(
        StringBuilder sb, TransitResult result, TimeProvider? timeProvider)
    {
        sb.AppendLine("## Natal Chart");
        sb.AppendLine();

        string natalMarkdown = ChartMarkdownFormatter.ToMarkdown(result.NatalChart, timeProvider: timeProvider);
        sb.Append(FormatNatalBody(natalMarkdown));
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    /// <summary>
    /// Strips the H1 header and the footer line from the natal chart Markdown,
    /// since the natal chart is embedded as a subsection within the timeline document.
    /// Follows the same pattern as <see cref="TransitMarkdownFormatter"/>.
    /// </summary>
    /// <param name="fullMarkdown">The complete natal chart Markdown from <see cref="ChartMarkdownFormatter"/>.</param>
    /// <returns>The natal chart Markdown without the H1 header and footer.</returns>
    private static string FormatNatalBody(string fullMarkdown)
    {
        string[] lines = MarkdownLineSplitter.SplitLines(fullMarkdown);

        int startIndex = 0;
        if (lines.Length > 0 && lines[0].TrimEnd().StartsWith('#'))
            startIndex = 1;

        if (startIndex < lines.Length && string.IsNullOrWhiteSpace(lines[startIndex]))
            startIndex++;

        int endIndex = lines.Length - 1;
        while (endIndex > startIndex && string.IsNullOrWhiteSpace(lines[endIndex]))
            endIndex--;

        if (endIndex > startIndex && lines[endIndex].TrimEnd().StartsWith("*Generated by"))
            endIndex--;

        if (endIndex > startIndex && lines[endIndex].TrimEnd() == "---")
            endIndex--;

        StringBuilder bodySb = new();
        for (int i = startIndex; i <= endIndex; i++)
        {
            bodySb.Append(lines[i]);
            if (i < endIndex)
                bodySb.Append('\n');
        }

        return bodySb.ToString();
    }

    #endregion

    #region Transit Windows

    /// <summary>
    /// Whether the natal chart carries Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsTiming, 1.6.0):
    /// the condition for the Transit Windows and Events sections of the transits to them.
    /// </summary>
    /// <param name="result">The transit calculation result.</param>
    /// <returns>Whether the natal chart carries the five.</returns>
    private static bool CarriesMinorPoints(TransitResult result) => result.NatalChart.MinorPoints.Count > 0;

    /// <summary>
    /// Appends a Transit Windows section — lifecycle summaries for each transit given, under the heading
    /// given; nothing when there is none. One writer for the chart's own transits and, since branch
    /// AsteroidsTiming (1.6.0), the transits to the five.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="heading">The section's heading line.</param>
    /// <param name="transits">The transits.</param>
    private static void AppendTransitWindows(StringBuilder sb, string heading, IReadOnlyList<ActiveTransit> transits)
    {
        if (transits.Count == 0)
            return;

        sb.AppendLine(heading);
        sb.AppendLine();

        foreach (ActiveTransit transit in transits)
        {
            string transitHeading = $"TR {FormatPlanetName(transit.TransitPlanet)} " +
                                    $"{transit.AspectAbbreviation} NA {transit.NatalTargetSymbol}";
            sb.AppendLine($"### {transitHeading}");
            sb.AppendLine();

            foreach (TransitLifecycleEvent evt in transit.Lifecycle)
            {
                string boundaryNote = "";
                if (evt.BeforeReportPeriod)
                    boundaryNote = " *(before report period)*";
                else if (evt.BeyondReportPeriod)
                    boundaryNote = " *(beyond report period)*";

                switch (evt.EventType)
                {
                    case TransitLifecycleEventType.Entry:
                        sb.AppendLine($"- **Entry:** {FormatUtcTime(evt.Date)}{boundaryNote}");
                        break;
                    case TransitLifecycleEventType.Exact:
                        string orbNote = evt.ExactOrbDegrees.HasValue
                            ? $" ({FormatOrb(evt.ExactOrbDegrees)})"
                            : "";
                        sb.AppendLine(Inv, $"- **Exact pass {evt.PassNumber}:** {FormatUtcTime(evt.Date)}{orbNote}{boundaryNote}");
                        break;
                    case TransitLifecycleEventType.Station:
                        sb.AppendLine($"- **Station {evt.StationType}:** {FormatUtcTime(evt.Date)} (in orb){boundaryNote}");
                        break;
                    case TransitLifecycleEventType.Exit:
                        sb.AppendLine($"- **Exit:** {FormatUtcTime(evt.Date)}{boundaryNote}");
                        break;
                }
            }

            // Duration summary
            TransitLifecycleEvent? entry = transit.Lifecycle
                .FirstOrDefault(e => e.EventType == TransitLifecycleEventType.Entry);
            TransitLifecycleEvent? exit = transit.Lifecycle
                .FirstOrDefault(e => e.EventType == TransitLifecycleEventType.Exit);

            if (entry is not null && exit is not null)
            {
                int durationDays = (int)(exit.Date - entry.Date).TotalDays;
                sb.AppendLine(
                    Inv, $"- Duration: {durationDays} {(durationDays == 1 ? "day" : "days")} | " +
                    $"Natal house: {FormatOrdinal(transit.NatalHouseNumber)} | " +
                    $"Transit house: {FormatOrdinal(transit.TransitHouseNumber)}");
            }

            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    #endregion

    #region Timeline

    /// <summary>
    /// Builds a unified list of timeline events from all transit data sources,
    /// sorts them chronologically, and renders as a Markdown table with Position column.
    /// </summary>
    private static void AppendTimeline(StringBuilder sb, TransitResult result)
    {
        List<TimelineEvent> events = [];

        ExtractExactPasses(events, result.ActiveTransits);
        ExtractEntryExitEvents(events, result.ActiveTransits);
        ExtractMidpointExactPasses(events, result.MidpointActivations);
        ExtractStations(events, result.Stations);
        ExtractIngresses(events, result.Ingresses);
        ExtractTransitToTransitAspects(events, result.TransitToTransitAspects);
        ExtractDeclinationAspects(events, result.TransitDeclinationAspects);

        AppendEventsTable(sb, "## Events", events);
    }

    /// <summary>
    /// Appends the Events section of the transits to the natal Ceres, Pallas, Juno, Vesta and Lilith (branch
    /// AsteroidsTiming, 1.6.0; R7): their exact passes, entries and exits by the same extractors as the chart's
    /// own, in the same table, under its own heading. Printed only when the natal chart carries the five; the
    /// Clusters never read these events (scoping decision 5).
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="result">The transit calculation result.</param>
    private static void AppendMinorPointTimeline(StringBuilder sb, TransitResult result)
    {
        List<TimelineEvent> events = [];

        ExtractExactPasses(events, result.MinorPointTransits);
        ExtractEntryExitEvents(events, result.MinorPointTransits);

        AppendEventsTable(sb, "## Events: Asteroids and Lilith", events);
    }

    /// <summary>
    /// Sorts timeline events chronologically (ties by their transit label) and renders them as a Markdown
    /// table with a Position column under the heading given, or the none sentence.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="heading">The section's heading line.</param>
    /// <param name="events">The events, sorted in place.</param>
    private static void AppendEventsTable(StringBuilder sb, string heading, List<TimelineEvent> events)
    {
        events.Sort((a, b) =>
        {
            int dateCompare = a.SortDate.CompareTo(b.SortDate);
            if (dateCompare != 0) return dateCompare;
            return string.Compare(a.Transit, b.Transit, StringComparison.Ordinal);
        });

        sb.AppendLine(heading);
        sb.AppendLine();

        if (events.Count == 0)
        {
            sb.AppendLine("No transit events during this period.");
            sb.AppendLine();
            return;
        }

        sb.AppendLine("| Date | Transit | Event | Position | Orb |");
        sb.AppendLine("|------|---------|-------|----------|-----|");

        foreach (TimelineEvent evt in events)
        {
            sb.AppendLine($"| {evt.DateDisplay} | {evt.Transit} | {evt.Description} | {evt.PositionDisplay} | {evt.OrbDisplay} |");
        }

        sb.AppendLine();
    }

    /// <summary>
    /// Extracts exact-pass events from active transit lifecycles.
    /// Filters out events flagged as before or beyond the report period.
    /// </summary>
    private static void ExtractExactPasses(
        List<TimelineEvent> events, IReadOnlyList<ActiveTransit> transits)
    {
        foreach (ActiveTransit transit in transits)
        {
            foreach (TransitLifecycleEvent evt in transit.Lifecycle)
            {
                if (evt.EventType != TransitLifecycleEventType.Exact)
                    continue;
                if (evt.BeforeReportPeriod || evt.BeyondReportPeriod)
                    continue;

                string transitLabel = $"TR {FormatPlanetName(transit.TransitPlanet)} {transit.AspectAbbreviation} NA {transit.NatalTarget}";
                string passLabel = evt.PassNumber.HasValue
                    ? string.Create(Inv, $"Exact pass {evt.PassNumber}")
                    : "Exact";

                // Position: the natal target's sign position (transit is AT the aspect angle)
                string position = PositionFormatter.FormatLongitude(transit.NatalLongitude);

                events.Add(new TimelineEvent
                {
                    SortDate = evt.Date,
                    DateDisplay = FormatUtcTime(evt.Date),
                    Transit = transitLabel,
                    Description = passLabel,
                    PositionDisplay = position,
                    OrbDisplay = FormatOrb(evt.ExactOrbDegrees),
                });
            }
        }
    }

    /// <summary>
    /// Extracts Entry and Exit events from active transit lifecycles into the timeline.
    /// Includes entries before the report period for context.
    /// </summary>
    /// <remarks>
    /// The Orb cell of an Entry or Exit row is the transit's orb limit, the separation at the
    /// boundary crossing (E2-4): <see cref="TransitOrbs.GetOrb"/> for the aspect that
    /// <see cref="TransitAspectFinder.TryGetAspect"/> names, as the daily horoscope reads it.
    /// Until 1.3.3 it was the orb at report start, and empty when that was 0.
    /// </remarks>
    /// <param name="events">The timeline's events, to which the rows are added.</param>
    /// <param name="transits">The active transits.</param>
    /// <exception cref="InvalidOperationException">
    /// A transit's aspect name is not one of the transit aspect finder's (it cannot be: the names
    /// come from the same finder).
    /// </exception>
    private static void ExtractEntryExitEvents(
        List<TimelineEvent> events, IReadOnlyList<ActiveTransit> transits)
    {
        foreach (ActiveTransit transit in transits)
        {
            foreach (TransitLifecycleEvent evt in transit.Lifecycle)
            {
                if (evt.EventType != TransitLifecycleEventType.Entry &&
                    evt.EventType != TransitLifecycleEventType.Exit)
                    continue;

                string transitLabel = $"TR {FormatPlanetName(transit.TransitPlanet)} {transit.AspectAbbreviation} NA {transit.NatalTarget}";
                string eventLabel = evt.EventType == TransitLifecycleEventType.Entry ? "Entry" : "Exit";

                // Position from transit longitude at entry/exit
                string position = evt.TransitLongitude.HasValue
                    ? PositionFormatter.FormatLongitude(evt.TransitLongitude.Value)
                    : "";

                // Orb at entry/exit is the orb limit
                if (!TransitAspectFinder.TryGetAspect(transit.AspectName, out double angle, out bool isMinor))
                {
                    throw new InvalidOperationException($"Unknown transit aspect '{transit.AspectName}'.");
                }

                string orbDisplay = FormatOrb(TransitOrbs.GetOrb(transit.TransitPlanet, angle, isMinor));

                events.Add(new TimelineEvent
                {
                    SortDate = evt.Date,
                    DateDisplay = FormatUtcTime(evt.Date),
                    Transit = transitLabel,
                    Description = eventLabel,
                    PositionDisplay = position,
                    OrbDisplay = orbDisplay,
                });
            }
        }
    }

    /// <summary>
    /// Extracts exact-pass events from midpoint activation lifecycles.
    /// Filters out events flagged as before or beyond the report period.
    /// </summary>
    private static void ExtractMidpointExactPasses(
        List<TimelineEvent> events, IReadOnlyList<TransitMidpointActivation> activations)
    {
        foreach (TransitMidpointActivation activation in activations)
        {
            foreach (TransitLifecycleEvent evt in activation.Lifecycle)
            {
                if (evt.EventType != TransitLifecycleEventType.Exact)
                    continue;
                if (evt.BeforeReportPeriod || evt.BeyondReportPeriod)
                    continue;

                string transitLabel = $"TR {FormatPlanetName(activation.TransitPlanet)} = {activation.MidpointBody1}/{activation.MidpointBody2}";

                events.Add(new TimelineEvent
                {
                    SortDate = evt.Date,
                    DateDisplay = FormatUtcTime(evt.Date),
                    Transit = transitLabel,
                    Description = "Midpoint exact",
                    PositionDisplay = "",
                    OrbDisplay = FormatOrb(evt.ExactOrbDegrees),
                });
            }
        }
    }

    /// <summary>
    /// Extracts station events (retrograde/direct) from the transit result.
    /// </summary>
    private static void ExtractStations(
        List<TimelineEvent> events, IReadOnlyList<TransitStation> stations)
    {
        foreach (TransitStation station in stations)
        {
            string transitLabel = FormatPlanetName(station.Planet);
            DateTime sortDate = station.DateTimeUtc
                ?? station.Date.ToDateTime(TimeOnly.Parse("12:00", Inv));
            string dateDisplay = station.DateTimeUtc.HasValue
                ? FormatUtcTime(station.DateTimeUtc.Value)
                : CalendarDateFormatter.Format(station.Date, "yyyy-MM-dd");
            string position = string.Create(Inv, $"{station.Degrees}\u00b0{station.Minutes:D2}' {station.Sign}");

            events.Add(new TimelineEvent
            {
                SortDate = sortDate,
                DateDisplay = dateDisplay,
                Transit = transitLabel,
                Description = $"Station {station.StationType}",
                PositionDisplay = position,
                OrbDisplay = "",
            });
        }
    }

    /// <summary>
    /// Extracts sign ingress events from the transit result.
    /// </summary>
    private static void ExtractIngresses(
        List<TimelineEvent> events, IReadOnlyList<TransitIngress> ingresses)
    {
        foreach (TransitIngress ingress in ingresses)
        {
            string transitLabel = FormatPlanetName(ingress.Planet);

            events.Add(new TimelineEvent
            {
                SortDate = ingress.Date.ToDateTime(TimeOnly.Parse("12:00", Inv)),
                DateDisplay = CalendarDateFormatter.Format(ingress.Date, "yyyy-MM-dd"),
                Transit = transitLabel,
                Description = $"Ingress {ingress.FromSign} \u2192 {ingress.ToSign}",
                PositionDisplay = $"0\u00b000' {ingress.ToSign}",
                OrbDisplay = "",
            });
        }
    }

    /// <summary>
    /// Extracts transit-to-transit aspect events from the transit result.
    /// </summary>
    private static void ExtractTransitToTransitAspects(
        List<TimelineEvent> events, IReadOnlyList<TransitToTransitAspect> aspects)
    {
        foreach (TransitToTransitAspect aspect in aspects)
        {
            string transitLabel = $"TR {aspect.Planet1Name} {aspect.AspectAbbreviation} TR {aspect.Planet2Name}";
            string position = $"{aspect.Planet1Position} / {aspect.Planet2Position}";

            events.Add(new TimelineEvent
            {
                SortDate = aspect.ExactDateTimeUtc,
                DateDisplay = FormatUtcTime(aspect.ExactDateTimeUtc),
                Transit = transitLabel,
                Description = $"Transit-to-transit {aspect.AspectName.ToLowerInvariant()}",
                PositionDisplay = position,
                OrbDisplay = FormatOrb(aspect.ExactOrbDegrees),
            });
        }
    }

    /// <summary>
    /// Extracts transit declination aspect events (parallels/contraparallels).
    /// </summary>
    private static void ExtractDeclinationAspects(
        List<TimelineEvent> events, IReadOnlyList<TransitDeclinationAspect>? aspects)
    {
        if (aspects is null)
            return;

        foreach (TransitDeclinationAspect aspect in aspects)
        {
            string aspectType = aspect.IsParallel ? "Parallel" : "Contraparallel";
            string transitLabel = $"TR {aspect.TransitPlanetName} {aspect.Abbreviation.ToLowerInvariant()} NA {aspect.NatalPlanetName}";
            string declSign = aspect.TransitDeclination >= 0 ? "N" : "S";
            string position = string.Create(Inv, $"{Math.Abs(aspect.TransitDeclination):F2}\u00b0{declSign}");

            events.Add(new TimelineEvent
            {
                SortDate = aspect.Date.ToDateTime(TimeOnly.Parse("12:00", Inv)),
                DateDisplay = CalendarDateFormatter.Format(aspect.Date, "yyyy-MM-dd"),
                Transit = transitLabel,
                Description = aspectType,
                PositionDisplay = position,
                OrbDisplay = FormatOrb(aspect.Orb),
            });
        }
    }

    #endregion

    #region Clusters

    /// <summary>
    /// The width of a cluster in days: an event belongs to the cluster whose first event lies at
    /// most this many days before it.
    /// </summary>
    private const int ClusterWindowDays = 3;

    /// <summary>
    /// Appends the Cluster Analysis section. A cluster is a run of in-period events in date order
    /// each within <see cref="ClusterWindowDays"/> of the cluster's first event; the first event
    /// outside that window starts a new cluster, and a cluster of fewer than two events is dropped
    /// (branch D, 1.3.4). Until 1.3.4 each event was measured from the one before it, so a cluster
    /// was a chain of any length: 45 of 79 in the real exports were longer than three days, one a
    /// month of 56 events (audit finding T1-12).
    /// </summary>
    private static void AppendClusters(StringBuilder sb, TransitResult result)
    {
        // Build a flat list of in-period events for clustering
        List<TimelineEvent> events = [];

        ExtractExactPasses(events, result.ActiveTransits);
        ExtractEntryExitEvents(events, result.ActiveTransits);
        ExtractStations(events, result.Stations);
        ExtractTransitToTransitAspects(events, result.TransitToTransitAspects);
        ExtractDeclinationAspects(events, result.TransitDeclinationAspects);

        // Filter to report period only: the typed bounds as the proleptic days they denote (branch
        // R3, 1.3.4), the events' dates being instants' days.
        DateOnly reportStart = CalendarDateFormatter.ProlepticDay(result.ReportStartDate);
        DateOnly reportEnd = CalendarDateFormatter.ProlepticDay(result.ReportEndDate);
        events = events
            .Where(e =>
            {
                DateOnly d = DateOnly.FromDateTime(e.SortDate);
                return d >= reportStart && d <= reportEnd;
            })
            .OrderBy(e => e.SortDate)
            .ToList();

        if (events.Count < 2)
            return;

        // Find clusters: each event is measured from its cluster's first event
        List<List<TimelineEvent>> clusters = [];
        List<TimelineEvent> currentCluster = [events[0]];

        for (int i = 1; i < events.Count; i++)
        {
            double daysFromFirst = (events[i].SortDate - currentCluster[0].SortDate).TotalDays;

            if (daysFromFirst <= ClusterWindowDays)
            {
                currentCluster.Add(events[i]);
            }
            else
            {
                if (currentCluster.Count >= 2)
                    clusters.Add(currentCluster);
                currentCluster = [events[i]];
            }
        }

        if (currentCluster.Count >= 2)
            clusters.Add(currentCluster);

        if (clusters.Count == 0)
            return;

        sb.AppendLine("## Clusters");
        sb.AppendLine();

        foreach (List<TimelineEvent> cluster in clusters)
        {
            DateTime clusterStart = cluster[0].SortDate;
            DateTime clusterEnd = cluster[^1].SortDate;

            sb.AppendLine(
                Inv, $"### {CalendarDateFormatter.Date(clusterStart)} to {CalendarDateFormatter.Date(clusterEnd)} \u2014 {cluster.Count} events");
            sb.AppendLine();
            sb.AppendLine("| Date | Transit | Event |");
            sb.AppendLine("|------|---------|-------|");

            foreach (TimelineEvent evt in cluster)
            {
                sb.AppendLine($"| {evt.DateDisplay} | {evt.Transit} | {evt.Description} |");
            }

            // Recommend dates: the first three distinct dates of an exact pass or an entry, any body
            List<string> recommendedDates = cluster
                .Where(e =>
                    e.Description.StartsWith("Exact", StringComparison.Ordinal) ||
                    e.Description == "Entry")
                .Select(e => CalendarDateFormatter.Date(e.SortDate))
                .Distinct()
                .Take(3)
                .ToList();

            if (recommendedDates.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine($"*Recommended Event Type B dates: {string.Join(", ", recommendedDates)}*");
            }

            sb.AppendLine();
        }

        sb.AppendLine("---");
        sb.AppendLine();
    }

    #endregion

    #region Footer

    /// <summary>
    /// Appends the document footer with precision notes.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="houseSystemName">The house system name to display in the footer.</param>
    private static void AppendFooter(StringBuilder sb, string houseSystemName)
    {
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"*Data from AstroCast (Swiss Ephemeris, Tropical, {houseSystemName}). " +
            "All times refined to sub-minute UTC precision via binary search (~1 arc-second). " +
            "Entry/exit times mark the orb boundary crossing.*");
        sb.AppendLine();
    }

    /// <summary>
    /// Appends the engine version after the data footer, as the chart export prints it
    /// ("AstroCast 1.3.3 (SwissEph 2.10.03, tzdb 2026e)"), so the file can be dated to the
    /// version that made it (E2-9). The version is the natal chart's: the transits are computed
    /// against it, by the same engine, in the same run. The footer's rule is the one before it.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="result">The transit result, whose natal chart carries the engine version.</param>
    private static void AppendEngineFooter(StringBuilder sb, TransitResult result)
    {
        sb.AppendLine($"*Engine: {result.NatalChart.EngineVersion}*");
    }

    #endregion

    #region Formatting Helpers

    /// <summary>
    /// Formats a <see cref="TransitBody"/> enum to its display name.
    /// </summary>
    /// <param name="body">The transit body.</param>
    /// <returns>Human-readable planet name.</returns>
    private static string FormatPlanetName(TransitBody body) => body switch
    {
        TransitBody.Jupiter => "Jupiter",
        TransitBody.Saturn => "Saturn",
        TransitBody.Uranus => "Uranus",
        TransitBody.Neptune => "Neptune",
        TransitBody.Pluto => "Pluto",
        TransitBody.Chiron => "Chiron",
        TransitBody.NorthNode => "North Node",
        TransitBody.Mars => "Mars",
        TransitBody.Mercury => "Mercury",
        TransitBody.Venus => "Venus",
        _ => body.ToString(),
    };

    /// <summary>
    /// Formats an orb as truncated D°MM′ through <see cref="PositionFormatter.FormatOrb"/> (branch
    /// O-D3, 1.3.4; until 1.3.4 with an apostrophe). Returns an empty string if the orb is null.
    /// </summary>
    /// <param name="orbDegrees">Orb in decimal degrees, or null.</param>
    /// <returns>Formatted orb string (e.g., "0°03′") or empty string.</returns>
    private static string FormatOrb(double? orbDegrees) =>
        orbDegrees is null ? "" : PositionFormatter.FormatOrb(orbDegrees.Value);

    /// <summary>
    /// Formats a UTC instant as "yyyy-MM-dd HH:mm UTC" on the calendar in force
    /// (<see cref="CalendarDateFormatter"/>, branch R2, 1.3.4).
    /// </summary>
    private static string FormatUtcTime(DateTime utc) =>
        CalendarDateFormatter.Minutes(utc) + " UTC";

    /// <summary>
    /// Formats a house number as an ordinal string (e.g., "1st", "2nd", "10th").
    /// </summary>
    private static string FormatOrdinal(int number) => number switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => string.Create(Inv, $"{number}th"),
    };

    #endregion

    #region Inner Types

    /// <summary>
    /// Internal record for building the unified, sorted timeline.
    /// </summary>
    private sealed class TimelineEvent
    {
        /// <summary>UTC date/time used for chronological sorting.</summary>
        public required DateTime SortDate { get; init; }

        /// <summary>Formatted date string for display in the table.</summary>
        public required string DateDisplay { get; init; }

        /// <summary>Transit description (e.g., "TR Saturn cnj NA Sun").</summary>
        public required string Transit { get; init; }

        /// <summary>Event description (e.g., "Exact pass 1", "Station Retrograde").</summary>
        public required string Description { get; init; }

        /// <summary>Formatted position (e.g., "10°15' Pisces") or empty.</summary>
        public required string PositionDisplay { get; init; }

        /// <summary>Formatted residual orb (e.g., "0°03'") or empty for non-exact events.</summary>
        public required string OrbDisplay { get; init; }
    }

    #endregion
}
