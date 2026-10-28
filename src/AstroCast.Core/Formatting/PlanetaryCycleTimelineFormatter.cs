using System.Globalization;
using System.Text;

using AstroCast.Core.Models;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a <see cref="PlanetaryCycleTimeline"/> as a Markdown document
/// showing the five phase points, retrograde passes, and optional position snapshots.
/// </summary>
public static class PlanetaryCycleTimelineFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Generate a complete cycle timeline report as a Markdown string.
    /// </summary>
    /// <param name="timeline">The cycle timeline to format.</param>
    /// <returns>A Markdown-formatted cycle timeline report.</returns>
    public static string ToMarkdown(PlanetaryCycleTimeline timeline)
    {
        ArgumentNullException.ThrowIfNull(timeline);

        StringBuilder sb = new();

        // Header
        sb.AppendLine(
            Inv, $"# {timeline.PlanetName} Cycle Timeline: {timeline.NativeName}, " +
            $"Return #{timeline.ReturnNumber} \u2192 #{timeline.ReturnNumber + 1}");
        sb.AppendLine();
        sb.AppendLine(Inv, $"**Generated:** {timeline.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Software:** {timeline.EngineVersion}  ");
        sb.AppendLine($"**Natal {timeline.PlanetName}:** {timeline.NatalPositionFormatted}  ");

        int totalYears = (int)(timeline.CycleDurationDays / 365.25);
        int remainingMonths = (int)((timeline.CycleDurationDays - totalYears * 365.25) / 30.44);

        string durationStr = totalYears > 0
            ? string.Create(Inv, $"{timeline.CycleDurationDays:F0} days ({totalYears}y {remainingMonths}m)")
            : string.Create(Inv, $"{timeline.CycleDurationDays:F0} days ({remainingMonths}m)");

        sb.AppendLine($"**Cycle Duration:** {durationStr}  ");

        if (timeline.PhasePoints.Count >= 2)
        {
            DateTime cycleStart = timeline.PhasePoints[0].ExactUtc;
            DateTime cycleEnd = timeline.PhasePoints[^1].ExactUtc;
            sb.AppendLine($"**Cycle Period:** {CalendarDateFormatter.Date(cycleStart)} \u2192 {CalendarDateFormatter.Date(cycleEnd)}");
            if (CalendarDateFormatter.IsJulian(cycleStart))
                sb.AppendLine(CalendarDateFormatter.JulianCalendarLine);
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Phase Points Table
        sb.AppendLine("## Phase Points");
        sb.AppendLine();
        sb.AppendLine("| Phase | Angle | Target | Date | Days | Cycle % | Passes |");
        sb.AppendLine("|-------|-------|--------|------|------|---------|--------|");

        foreach (CyclePhasePoint point in timeline.PhasePoints)
        {
            string angleStr = point.PhaseAngle == 360 ? "360\u00b0" : string.Create(Inv, $"{point.PhaseAngle}\u00b0");
            sb.AppendLine(
                Inv, $"| {point.PhaseName} | {angleStr} | {point.TargetPositionFormatted} | " +
                $"{CalendarDateFormatter.Date(point.ExactUtc)} | {point.DaysFromStart:F0} | " +
                $"{point.CyclePercentage:F1}% | {point.PassCount} |");
        }

        sb.AppendLine();

        // Retrograde passes detail
        bool hasMultiplePasses = false;
        foreach (CyclePhasePoint point in timeline.PhasePoints)
        {
            if (point.PassCount > 1)
            {
                if (!hasMultiplePasses)
                {
                    sb.AppendLine("## Retrograde Passes");
                    sb.AppendLine();
                    hasMultiplePasses = true;
                }

                sb.AppendLine(Inv, $"### {point.PhaseName} ({point.PhaseAngle}\u00b0) \u2014 {point.PassCount} Crossings");
                sb.AppendLine();
                sb.AppendLine("| Pass | Date |");
                sb.AppendLine("|------|------|");

                for (int i = 0; i < point.AllPasses.Count; i++)
                {
                    string label = i == 0 ? "1st" : i == point.AllPasses.Count - 1 ? "Final" : string.Create(Inv, $"{i + 1}");
                    sb.AppendLine($"| {label} | {CalendarDateFormatter.Minutes(point.AllPasses[i])} UTC |");
                }

                sb.AppendLine();
            }
        }

        // Snapshots
        bool hasSnapshots = false;
        foreach (CyclePhasePoint point in timeline.PhasePoints)
        {
            if (point.Snapshot is { Count: > 0 })
            {
                if (!hasSnapshots)
                {
                    sb.AppendLine("## Phase Snapshots");
                    sb.AppendLine();
                    hasSnapshots = true;
                }

                sb.AppendLine($"### {point.PhaseName} \u2014 {CalendarDateFormatter.Minutes(point.ExactUtc)} UTC");
                sb.AppendLine();
                sb.AppendLine("| Planet | Position | Sign |");
                sb.AppendLine("|--------|----------|------|");

                foreach (PlanetPosition pos in point.Snapshot)
                {
                    string formattedLon = PositionFormatter.FormatLongitude(pos.Longitude);
                    sb.AppendLine($"| {pos.Name} | {formattedLon} | {pos.Sign} |");
                }

                sb.AppendLine();
            }
        }

        // Footer
        sb.AppendLine("---");
        sb.AppendLine(
            Inv, $"*Generated by {timeline.EngineVersion} at {timeline.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC*");

        return sb.ToString();
    }

    /// <summary>
    /// Save a cycle timeline as Markdown to a file.
    /// </summary>
    /// <param name="timeline">The cycle timeline to save.</param>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(
        PlanetaryCycleTimeline timeline, string filePath,
        CancellationToken ct = default)
    {
        string md = ToMarkdown(timeline);
        await File.WriteAllTextAsync(filePath, md, ct).ConfigureAwait(false);
    }
}
