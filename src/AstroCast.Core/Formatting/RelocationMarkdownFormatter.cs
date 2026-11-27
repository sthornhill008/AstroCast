using System.Globalization;
using System.Text;

using AstroCast.Core.Models;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a <see cref="RelocationResult"/> as a Markdown document: a header
/// naming the chart's own place and moment, by its type, and the new place;
/// the Angle Changes and House Changes tables; then the full relocated chart.
/// The natal chart is referenced but not repeated in full — it is expected
/// to be available separately.
/// </summary>
/// <remarks>
/// The relocated chart body prints the birth clock, the birth's sect and planetary hour, and the
/// angles, houses, Lots and angular planets of the place, as the engine casts it; its Annual
/// Profection, Firdaria and Zodiacal Releasing sections are the natal chart's, printed where they
/// print in a natal export (<see cref="ChartMarkdownFormatter.ToMarkdown"/>'s <c>timeLordsChart</c>;
/// branch K2, 1.3.4; ruling S-D6). Until 1.3.4 they were re-derived from the relocated Ascendant and
/// Lots and counted from the UTC date.
/// </remarks>
public static class RelocationMarkdownFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Generate a complete relocation report as a Markdown string.
    /// </summary>
    /// <param name="result">The relocation analysis result to format.</param>
    /// <param name="timeProvider">
    /// Clock forwarded to <see cref="ChartMarkdownFormatter.ToMarkdown"/> for the
    /// embedded relocated chart so its date-keyed profection / firdaria /
    /// zodiacal releasing sections resolve deterministically. Defaults to
    /// <see cref="TimeProvider.System"/> when <c>null</c>, preserving production
    /// behaviour; golden tests pass a fixed <c>FakeTimeProvider</c> (F-GR-2).
    /// </param>
    /// <returns>A Markdown-formatted relocation report.</returns>
    public static string ToMarkdown(RelocationResult result, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        StringBuilder sb = new();

        // Header
        sb.AppendLine($"# Relocation: {result.NatalChart.Input.Name}");
        sb.AppendLine();
        string word = PlaceAndMomentWord(result.NatalChart.Input.EventType);
        sb.AppendLine(Inv, $"**{word} Location:** {result.NatalChart.Input.LocationName} ({result.NatalChart.Input.Latitude:F4}, {result.NatalChart.Input.Longitude:F4})  ");
        sb.AppendLine(Inv, $"**Relocation Target:** {result.RelocationTarget.LocationName} ({result.RelocationTarget.Latitude:F4}, {result.RelocationTarget.Longitude:F4})  ");
        // An explicit offset overrides the zone's rules, so the chart's wall clock is labelled
        // with that offset, as the chart export's Date/Time line labels it, never with the zone.
        string birthClock = result.NatalChart.Input.UseExplicitOffset && result.NatalChart.Input.UtcOffsetHours.HasValue
            ? result.NatalChart.ResolvedOffsetLabel
            : $"({result.NatalChart.Input.TimeZone})";
        sb.AppendLine(Inv, $"**{word} Date/Time:** {result.NatalChart.Input.LocalDateTime:yyyy-MM-dd HH:mm:ss} {birthClock}  ");
        sb.AppendLine($"**UTC Moment:** {CalendarDateFormatter.DateTime(result.NatalChart.UtcDateTime)} UTC ({result.NatalChart.ResolvedOffsetLabel})  ");
        if (CalendarDateFormatter.IsJulian(result.NatalChart.UtcDateTime))
            sb.AppendLine(CalendarDateFormatter.JulianCalendarLine + "  ");
        sb.AppendLine(Inv, $"**Julian Day:** {result.NatalChart.JulianDay:F6}");
        sb.AppendLine();

        // Angle Changes
        sb.AppendLine("## Angle Changes");
        sb.AppendLine();
        sb.AppendLine("| Angle | Natal | Relocated | Changed |");
        sb.AppendLine("|-------|-------|-----------|---------|");

        string ascChanged = result.AscendantSignChanged ? "\u2705 Yes" : "No";
        string mcChanged = result.MidheavenSignChanged ? "\u2705 Yes" : "No";
        sb.AppendLine($"| Ascendant | {result.NatalAscendantSign} ({PositionFormatter.FormatLongitude(result.NatalChart.Houses.Ascendant)}) | {result.RelocatedAscendantSign} ({PositionFormatter.FormatLongitude(result.RelocatedChart.Houses.Ascendant)}) | {ascChanged} |");
        sb.AppendLine($"| Midheaven | {result.NatalMidheavenSign} ({PositionFormatter.FormatLongitude(result.NatalChart.Houses.Midheaven)}) | {result.RelocatedMidheavenSign} ({PositionFormatter.FormatLongitude(result.RelocatedChart.Houses.Midheaven)}) | {mcChanged} |");
        sb.AppendLine();

        if (result.AscendantSignChanged && result.RelocatedChart.ChartContext is not null
            && result.NatalChart.ChartContext is not null)
        {
            sb.AppendLine($"**Chart ruler changes:** {result.NatalChart.ChartContext.ChartRuler} (natal) \u2192 {result.RelocatedChart.ChartContext.ChartRuler} (relocated)");
            sb.AppendLine();
        }

        // House Changes
        sb.AppendLine("## House Changes");
        sb.AppendLine();

        if (result.HouseChanges.Count > 0)
        {
            sb.AppendLine("| Planet | Natal House | Relocated House | Note |");
            sb.AppendLine("|--------|-------------|-----------------|------|");

            foreach (HouseChange hc in result.HouseChanges)
            {
                string note = "";
                if (hc.BecameAngular) note = "\u2b06 Became angular";
                else if (hc.LeftAngle) note = "\u2b07 Left angle";

                sb.AppendLine(Inv, $"| {hc.PlanetName} | {hc.NatalHouse} | {hc.RelocatedHouse} | {note} |");
            }
        }
        else
        {
            sb.AppendLine("No house changes.");
        }

        sb.AppendLine();

        // Relocated Chart: the chart export without its own title and footer, as the composite and Davison
        // exports embed theirs (branch ExportText, 1.7.2; finding N10). Until 1.7.2 the whole export was
        // embedded, a first-level title under this second-level heading, with its own footer.
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("## Relocated Chart");
        sb.AppendLine();
        sb.Append(FormatChartSection(ChartMarkdownFormatter.ToMarkdown(
            result.RelocatedChart, timeProvider: timeProvider, includeTemperament: false, timeLordsChart: result.NatalChart)));
        sb.AppendLine();

        // Footer: the one footer, its version the relocated chart's, with the Swiss Ephemeris and time-zone
        // part every chart export prints (until 1.7.2 the relocation service's shorter text)
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine(
            Inv, $"*Generated by {result.RelocatedChart.EngineVersion} at {result.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC*");

        return sb.ToString();
    }

    /// <summary>
    /// The body of a chart export for embedding under a heading: its H1 title line, the blank line after it,
    /// and its footer (the closing rule and the "*Generated by ...*" line) are taken off, as the composite and
    /// Davison exports do, and the lines are joined with the builder's own line ending.
    /// </summary>
    /// <param name="fullMarkdown">The complete chart Markdown from <see cref="ChartMarkdownFormatter"/>.</param>
    /// <returns>The chart Markdown without its title and footer.</returns>
    private static string FormatChartSection(string fullMarkdown)
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

        while (endIndex > startIndex && string.IsNullOrWhiteSpace(lines[endIndex]))
            endIndex--;

        StringBuilder body = new();
        for (int i = startIndex; i <= endIndex; i++)
        {
            body.Append(lines[i]);
            if (i < endIndex)
                body.AppendLine();
        }

        return body.ToString();
    }

    /// <summary>
    /// The word that labels the chart's own place and moment in the header, by the chart's type: "Birth" for
    /// a Birth chart, "Question" for a Horary chart, "Event" for every other type (1.6.1, branch StaleResult),
    /// so the lines read Birth Location and Birth Date/Time, Question Location and Question Date/Time, or
    /// Event Location and Event Date/Time. Until 1.6.1 every header read Birth.
    /// </summary>
    /// <param name="eventType">The chart's event type as stored, compared ignoring case.</param>
    /// <returns>"Birth", "Question" or "Event".</returns>
    private static string PlaceAndMomentWord(string eventType) =>
        string.Equals(eventType, "Birth", StringComparison.OrdinalIgnoreCase) ? "Birth"
        : string.Equals(eventType, "Horary", StringComparison.OrdinalIgnoreCase) ? "Question"
        : "Event";

    /// <summary>
    /// Save a relocation result as Markdown to a file.
    /// </summary>
    /// <param name="result">The relocation result to save.</param>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(
        RelocationResult result, string filePath, CancellationToken ct = default)
    {
        string markdown = ToMarkdown(result);
        await File.WriteAllTextAsync(filePath, markdown, ct).ConfigureAwait(false);
    }
}
