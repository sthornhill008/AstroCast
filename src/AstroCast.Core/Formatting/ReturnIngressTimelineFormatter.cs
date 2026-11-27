using System.Globalization;
using System.Text;

using AstroCast.Core.Models;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats <see cref="ReturnIngressTimeline"/> data as Markdown reports with full
/// UTC date/time precision, suitable for event chart casting and AI interpretation.
/// </summary>
public static class ReturnIngressTimelineFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Generate a complete Return &amp; Ingress Timeline as Markdown.
    /// </summary>
    /// <param name="timeline">The timeline to format.</param>
    /// <returns>A Markdown-formatted timeline report.</returns>
    public static string ToMarkdown(ReturnIngressTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        StringBuilder sb = new();

        // Header
        sb.AppendLine(
            Inv, $"# Return & Ingress Timeline: {timeline.NativeName}, " +
            $"{timeline.ScanStart.Year}\u2013{timeline.ScanEnd.Year}");
        sb.AppendLine();
        sb.AppendLine(Inv, $"**Generated:** {timeline.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Software:** {timeline.EngineVersion}  ");
        sb.AppendLine(
            Inv, $"**Period:** {timeline.ScanStart:yyyy-MM-dd} to {timeline.ScanEnd:yyyy-MM-dd}  ");
        if (timeline.Entries.Count > 0 && CalendarDateFormatter.IsJulian(timeline.Entries.Min(e => e.ExactUtc)))
            sb.AppendLine(CalendarDateFormatter.JulianCalendarLine + "  ");

        int returnCount = timeline.Returns.Count;
        int ingressCount = timeline.Ingresses.Count;

        // Each noun follows its count: "1 return, 1 ingress" (branch ExportText, 1.7.2)
        sb.AppendLine(
            Inv, $"**Events Found:** {timeline.Count} ({returnCount} {(returnCount == 1 ? "return" : "returns")}, {ingressCount} {(ingressCount == 1 ? "ingress" : "ingresses")})  ");
        // A search that did not run says so rather than naming no planets (E2-3).
        sb.AppendLine($"**Return Planets:** {(timeline.ReturnPlanets.Count > 0 ? string.Join(", ", timeline.ReturnPlanets) : "not searched")}  ");
        sb.AppendLine($"**Ingress Planets:** {(timeline.IngressPlanets.Count > 0 ? string.Join(", ", timeline.IngressPlanets) : "not searched")}  ");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Timeline table
        sb.AppendLine("## Timeline");
        sb.AppendLine();
        sb.AppendLine("| # | Date (UTC) | Event | Planet | Position | Label |");
        sb.AppendLine("|---|------------|-------|--------|----------|-------|");

        for (int i = 0; i < timeline.Entries.Count; i++)
        {
            ReturnIngressTimelineEntry entry = timeline.Entries[i];
            sb.AppendLine(
                Inv, $"| {i + 1} " +
                $"| {CalendarDateFormatter.DateTime(entry.ExactUtc)} " +
                $"| {entry.EventType} " +
                $"| {entry.PlanetName} " +
                $"| {entry.PositionFormatted} " +
                $"| {entry.Label} |");
        }

        sb.AppendLine();

        // Summary by planet — Returns
        if (returnCount > 0)
        {
            sb.AppendLine("## Summary by Planet");
            sb.AppendLine();
            sb.AppendLine("### Returns");
            sb.AppendLine();
            sb.AppendLine("| Planet | Natal Position | Count | Next Return |");
            sb.AppendLine("|--------|---------------|-------|-------------|");

            IEnumerable<IGrouping<string, ReturnIngressTimelineEntry>> returnGroups =
                timeline.Returns.GroupBy(e => e.PlanetName);

            foreach (IGrouping<string, ReturnIngressTimelineEntry> group in returnGroups)
            {
                ReturnIngressTimelineEntry first = group.First();
                string nextReturn = CalendarDateFormatter.Date(first.ExactUtc);

                sb.AppendLine(
                    Inv, $"| {group.Key} " +
                    $"| {first.PositionFormatted} " +
                    $"| {group.Count()} " +
                    $"| {nextReturn} |");
            }

            // Add planets with zero returns
            foreach (string planet in timeline.ReturnPlanets)
            {
                if (!timeline.Returns.Any(e => e.PlanetName == planet))
                {
                    sb.AppendLine($"| {planet} | \u2014 | 0 | \u2014 |");
                }
            }

            sb.AppendLine();
        }

        // Summary by planet — Ingresses
        if (ingressCount > 0)
        {
            sb.AppendLine("### Ingresses");
            sb.AppendLine();
            sb.AppendLine("| Planet | Sign Changes | Details |");
            sb.AppendLine("|--------|-------------|---------|");

            IEnumerable<IGrouping<string, ReturnIngressTimelineEntry>> ingressGroups =
                timeline.Ingresses.GroupBy(e => e.PlanetName);

            foreach (IGrouping<string, ReturnIngressTimelineEntry> group in ingressGroups)
            {
                List<ReturnIngressTimelineEntry> items = group.ToList();
                string details = string.Join(
                    ", ",
                    items.Select(e => $"{e.Sign} ({CalendarDateFormatter.Date(e.ExactUtc)})"));

                sb.AppendLine(
                    Inv, $"| {group.Key} " +
                    $"| {items.Count} " +
                    $"| {details} |");
            }

            // Add planets with zero ingresses
            foreach (string planet in timeline.IngressPlanets)
            {
                if (!timeline.Ingresses.Any(e => e.PlanetName == planet))
                {
                    sb.AppendLine($"| {planet} | 0 | \u2014 |");
                }
            }

            sb.AppendLine();
        }

        // Footer
        sb.AppendLine("---");
        sb.AppendLine(
            Inv, $"*Generated by {timeline.EngineVersion} at " +
            $"{timeline.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC*");

        return sb.ToString();
    }

    /// <summary>
    /// Save the timeline Markdown to a file.
    /// </summary>
    /// <param name="timeline">The timeline to export.</param>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public static async Task SaveAsync(
        ReturnIngressTimeline timeline,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(timeline);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string markdown = ToMarkdown(timeline);
        await File.WriteAllTextAsync(filePath, markdown, cancellationToken);
    }
}
