using System.Globalization;
using System.Text;

using AstroCast.Core.Models;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats <see cref="EclipseSeries"/> data as Markdown reports with full UTC
/// date/time precision and detailed phase timing tables for each eclipse.
/// </summary>
public static class EclipseFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Generate a complete eclipse series report as Markdown.
    /// </summary>
    /// <param name="series">The series to format.</param>
    /// <returns>A Markdown-formatted eclipse report with phase timing details.</returns>
    public static string SeriesToMarkdown(EclipseSeries series)
    {
        ArgumentNullException.ThrowIfNull(series);

        StringBuilder sb = new();

        sb.AppendLine(
            Inv, $"# Eclipses: {series.ScanStart.Year}\u2013{series.ScanEnd.Year}");
        sb.AppendLine();
        sb.AppendLine(Inv, $"**Generated:** {series.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Software:** {series.EngineVersion}  ");
        sb.AppendLine(
            Inv, $"**Period:** {series.ScanStart:yyyy-MM-dd} to {series.ScanEnd:yyyy-MM-dd}  ");
        if (series.Eclipses.Count > 0 && CalendarDateFormatter.IsJulian(series.Eclipses.Min(e => e.MaximumUtc)))
            sb.AppendLine(CalendarDateFormatter.JulianCalendarLine + "  ");
        sb.AppendLine(
            Inv, $"**Eclipses Found:** {series.Count} " +
            $"({series.SolarEclipses.Count} Solar, {series.LunarEclipses.Count} Lunar)");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Summary timeline table
        sb.AppendLine("## Eclipse Timeline");
        sb.AppendLine();
        sb.AppendLine(
            "| # | Date (UTC) | Category | Type | Sign | Position | Node Dist | Duration |");
        sb.AppendLine(
            "|---|------------|----------|------|------|----------|-----------|----------|");

        for (int i = 0; i < series.Eclipses.Count; i++)
        {
            EclipseResult e = series.Eclipses[i];
            string duration = FormatDuration(e.TotalityDurationMinutes);

            sb.AppendLine(
                Inv, $"| {i + 1} | {CalendarDateFormatter.DateTime(e.MaximumUtc)} | " +
                $"{e.Category} | {e.Type} | {e.Sign} | {e.PositionFormatted} | " +
                $"{e.NodeDistance:F1}\u00b0 | {duration} |");
        }

        sb.AppendLine();
        sb.AppendLine(
            "*Duration: for a solar eclipse, the duration of the central eclipse over the whole Earth, " +
            "from the first moment the total or annular phase is seen anywhere to the last; " +
            "for a lunar eclipse, the duration of totality.*");
        sb.AppendLine();

        // Detailed section for each eclipse
        sb.AppendLine("## Eclipse Details");
        sb.AppendLine();

        for (int i = 0; i < series.Eclipses.Count; i++)
        {
            EclipseResult e = series.Eclipses[i];
            sb.AppendLine(
                Inv, $"### #{i + 1} \u2014 {e.FullLabel}, {CalendarDateFormatter.Format(e.MaximumUtc, "MMMM d, yyyy")}");
            sb.AppendLine();

            // Phase timing table
            sb.AppendLine("| Phase | Time (UTC) |");
            sb.AppendLine("|-------|------------|");

            if (e.PenumbralBeginUtc is not null)
                sb.AppendLine(Inv, $"| Penumbral begins | {e.PenumbralBeginUtc:HH:mm:ss} |");
            if (e.FirstContactUtc is not null)
                sb.AppendLine(
                    Inv, $"| {(e.IsSolar ? "First contact" : "Partial begins")} | {e.FirstContactUtc:HH:mm:ss} |");
            // A solar eclipse's begin and end are the central phase's over the whole Earth, not
            // totality at any one place; a lunar eclipse's are totality.
            if (e.TotalityBeginUtc is not null)
                sb.AppendLine(
                    Inv, $"| {(e.IsSolar ? "Central eclipse begins (whole Earth)" : "Totality begins")} | {e.TotalityBeginUtc:HH:mm:ss} |");
            sb.AppendLine(Inv, $"| Maximum | {e.MaximumUtc:HH:mm:ss} |");
            if (e.TotalityEndUtc is not null)
                sb.AppendLine(
                    Inv, $"| {(e.IsSolar ? "Central eclipse ends (whole Earth)" : "Totality ends")} | {e.TotalityEndUtc:HH:mm:ss} |");
            if (e.LastContactUtc is not null)
                sb.AppendLine(
                    Inv, $"| {(e.IsSolar ? "Last contact" : "Partial ends")} | {e.LastContactUtc:HH:mm:ss} |");
            if (e.PenumbralEndUtc is not null)
                sb.AppendLine(Inv, $"| Penumbral ends | {e.PenumbralEndUtc:HH:mm:ss} |");

            sb.AppendLine();
            sb.AppendLine(
                $"**Sun:** {PositionFormatter.FormatLongitude(e.SunLongitude)} | " +
                $"**Moon:** {PositionFormatter.FormatLongitude(e.MoonLongitude)} | " +
                $"**Node:** {PositionFormatter.FormatLongitude(e.NodeLongitude)}  ");

            if (e.TotalityDurationMinutes is not null)
            {
                // A solar eclipse's figure is the central phase over the whole Earth, not totality
                // at any one place (T4-4, Q7); a lunar eclipse's is totality.
                TimeSpan dur = TimeSpan.FromMinutes(e.TotalityDurationMinutes.Value);
                string name = e.IsSolar ? "Duration of the central eclipse (whole Earth)" : "Totality Duration";
                sb.AppendLine(
                    Inv, $"**{name}:** {dur.Hours}h {dur.Minutes}m {dur.Seconds}s  ");
            }

            sb.AppendLine();
        }

        // Footer
        sb.AppendLine("---");
        sb.AppendLine(
            Inv, $"*Generated by {series.EngineVersion} at " +
            $"{series.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC*");

        return sb.ToString();
    }

    /// <summary>
    /// Save an eclipse series report as Markdown to a file.
    /// </summary>
    public static async Task SaveSeriesAsync(
        EclipseSeries series, string filePath,
        CancellationToken ct = default)
    {
        string md = SeriesToMarkdown(series);
        await File.WriteAllTextAsync(filePath, md, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Format a duration in minutes as a readable string.
    /// </summary>
    private static string FormatDuration(double? minutes)
    {
        if (minutes is null) return "\u2014";
        TimeSpan dur = TimeSpan.FromMinutes(minutes.Value);
        return dur.TotalHours >= 1
            ? string.Create(Inv, $"{(int)dur.TotalHours}h {dur.Minutes}m")
            : string.Create(Inv, $"{dur.Minutes}m {dur.Seconds}s");
    }
}
