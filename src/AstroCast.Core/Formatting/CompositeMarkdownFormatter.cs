using System.Globalization;
using System.Text;

using AstroCast.Core.Models;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a <see cref="CompositeResult"/> as a combined Markdown document
/// containing the composite chart data and brief summaries of both source charts.
/// A composite body is a midpoint, not a point in the sky (ruling S-D4, branch L2,
/// 1.3.4): the composite body prints no declinations, right ascensions,
/// out-of-bounds, declination aspects, stations, planet conditions or
/// applying/separating marks, and no Moon's next aspect or void of course, and the
/// header says so once, in the "Not shown" line after the Method line. It keeps day
/// or night, the Moon's phase, sign and via combusta, the midpoints, fixed stars and
/// medical correspondences. Mercury and Venus take the midpoint nearer the composite
/// Sun (ruling S-D2, branch L3, 1.3.4): each body moved gets a "Note" line after the
/// Not shown line, naming its position and the short-arc midpoint opposite it. The
/// Method line states the midpoint rule and that the MC is the midpoint of the two
/// Midheavens, the houses cast for it at the mean birth latitude in the composite's
/// house system (S-D3).
/// </summary>
public static class CompositeMarkdownFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// The header line that says once what a composite export leaves out, and why (S-D4): printed
    /// directly after the Method line, with the two trailing spaces every header line carries.
    /// </summary>
    private const string NotShownLine =
        "**Not shown:** declinations, right ascensions, out-of-bounds, declination aspects, stations, planet " +
        "conditions, applying/separating marks, the Moon's next aspect and void of course \u2014 a composite body " +
        "is a midpoint, not a point in the sky.  ";

    /// <summary>
    /// Generate a complete composite chart report as a Markdown string.
    /// </summary>
    /// <param name="result">The composite chart result to format.</param>
    /// <param name="interpretationContext">Optional free-text context for AI interpretation.</param>
    /// <returns>A Markdown-formatted composite chart report.</returns>
    public static string ToMarkdown(CompositeResult result, string? interpretationContext = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        StringBuilder sb = new();

        // Section 1: Header
        sb.AppendLine(
            $"# Composite Chart: {result.ChartA.Input.Name} & {result.ChartB.Input.Name}");
        sb.AppendLine();
        sb.AppendLine(Inv, $"**Generated:** {result.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Software:** {result.EngineVersion}  ");
        sb.AppendLine(
            "**Method:** Tropical zodiac, short-arc midpoints (Mercury and Venus nearer the composite Sun); " +
            $"MC the midpoint of the two Midheavens, {result.CompositeChart.HouseSystemName} houses for it at the mean birth latitude  ");
        sb.AppendLine(NotShownLine);
        foreach (string name in result.SunSideBodies)
        {
            // One Note per body that took the midpoint nearer the composite Sun (S-D2).
            PlanetPosition? body = result.CompositeChart.Positions.FirstOrDefault(
                p => string.Equals(p.Name, name, StringComparison.Ordinal));
            if (body is null)
                continue;

            double shortArcMidpoint = (body.Longitude + 180.0) % 360.0;
            sb.AppendLine(
                $"**Note:** {name} takes the midpoint nearer the composite Sun, {PositionFormatter.FormatLongitude(body.Longitude)}; " +
                $"the short-arc midpoint, {PositionFormatter.FormatLongitude(shortArcMidpoint)}, lies opposite.  ");
        }

        if (CalendarDateFormatter.IsJulian(result.ChartA.UtcDateTime) || CalendarDateFormatter.IsJulian(result.ChartB.UtcDateTime))
            sb.AppendLine(CalendarDateFormatter.JulianCalendarLine + "  ");
        sb.AppendLine(
            Inv, $"**Reference Location:** {result.ReferenceLocationDescription} " +
            $"({result.ReferenceLatitude:F4}, {result.ReferenceLongitude:F4})");

        if (!string.IsNullOrWhiteSpace(interpretationContext))
        {
            sb.AppendLine($"**Interpretation Context:** {interpretationContext.Trim()}");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Section 2: Composite chart body (reuse ChartMarkdownFormatter)
        sb.AppendLine("## Composite Chart");
        sb.AppendLine();
        sb.Append(FormatChartSection(result.CompositeChart));
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Section 3: Source Chart A summary
        sb.AppendLine($"## Source Chart A: {result.ChartA.Input.Name}");
        sb.AppendLine();
        sb.Append(FormatSourceSummary(result.ChartA));
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Section 4: Source Chart B summary
        sb.AppendLine($"## Source Chart B: {result.ChartB.Input.Name}");
        sb.AppendLine();
        sb.Append(FormatSourceSummary(result.ChartB));
        sb.AppendLine();

        // Footer
        sb.AppendLine("---");
        sb.AppendLine(
            Inv, $"*Generated by {result.EngineVersion} at {result.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC*");

        return sb.ToString();
    }

    /// <summary>
    /// Save a composite result as Markdown to a file.
    /// </summary>
    /// <param name="result">The composite chart result to save.</param>
    /// <param name="filePath">Destination file path for the Markdown output.</param>
    /// <param name="interpretationContext">Optional free-text context for AI interpretation.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(
        CompositeResult result, string filePath,
        string? interpretationContext = null, CancellationToken ct = default)
    {
        string md = ToMarkdown(result, interpretationContext);
        await File.WriteAllTextAsync(filePath, md, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Format the composite chart body by reusing <see cref="ChartMarkdownFormatter"/>
    /// as a composite (no declinations, marks or Moon's next aspect; S-D4) and stripping
    /// the H1 header line and the footer line.
    /// </summary>
    /// <param name="chart">The composite chart result to format.</param>
    /// <returns>Markdown body without the H1 header or trailing footer.</returns>
    private static string FormatChartSection(ChartResult chart)
    {
        string fullMarkdown = ChartMarkdownFormatter.ToMarkdown(chart, includeTemperament: false, composite: true);

        // Split into lines and remove:
        // - First line (H1 heading)
        // - Empty line after the heading
        // - Last two lines (footer: "---" and "*Generated by ...*")
        string[] lines = MarkdownLineSplitter.SplitLines(fullMarkdown);

        // Find the first non-empty line after the H1 heading
        int startIndex = 0;
        if (lines.Length > 0 && lines[0].TrimEnd().StartsWith('#'))
            startIndex = 1;

        // Skip blank line after heading
        if (startIndex < lines.Length && string.IsNullOrWhiteSpace(lines[startIndex]))
            startIndex++;

        // Remove footer lines (last "---" and "*Generated by...*")
        int endIndex = lines.Length - 1;
        while (endIndex > startIndex && string.IsNullOrWhiteSpace(lines[endIndex]))
            endIndex--;

        if (endIndex > startIndex && lines[endIndex].TrimEnd().StartsWith("*Generated by"))
            endIndex--;

        if (endIndex > startIndex && lines[endIndex].TrimEnd() == "---")
            endIndex--;

        // Rebuild the body content
        StringBuilder sb = new();
        for (int i = startIndex; i <= endIndex; i++)
        {
            sb.Append(lines[i]);
            if (i < endIndex)
                sb.AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Format a brief summary of a source chart showing positions and house cusps only.
    /// </summary>
    /// <param name="chart">The source chart to summarise.</param>
    /// <returns>Markdown with date/location metadata, positions, and house cusps.</returns>
    private static string FormatSourceSummary(ChartResult chart)
    {
        StringBuilder sb = new();

        // Metadata. An explicit offset overrides the zone's rules, so the wall clock is
        // labelled with that offset, as the chart export's Date/Time line labels it.
        string clockLabel = chart.Input.UseExplicitOffset && chart.Input.UtcOffsetHours.HasValue
            ? chart.ResolvedOffsetLabel
            : $"({chart.Input.TimeZone})";
        sb.AppendLine(
            Inv, $"**Date/Time:** {chart.Input.LocalDateTime:yyyy-MM-dd HH:mm:ss} {clockLabel}  ");
        sb.AppendLine(
            Inv, $"**Location:** {chart.Input.LocationName} ({chart.Input.Latitude:F4}, {chart.Input.Longitude:F4})");
        sb.AppendLine();

        // Positions
        sb.AppendLine("### Planetary Positions");
        sb.AppendLine();
        sb.AppendLine("| Body | Position | House | Dir |");
        sb.AppendLine("|------|----------|-------|-----|");

        foreach (PlanetPosition p in chart.Positions)
        {
            string houseStr = p.House.HasValue ? string.Create(Inv, $"{p.House.Value}") : "-";
            sb.AppendLine(
                $"| {p.Name} | {PositionFormatter.FormatPosition(p)} | {houseStr} | {p.DirectionCode} |");
        }

        sb.AppendLine();

        // House Cusps
        sb.AppendLine("### House Cusps");
        sb.AppendLine();
        sb.AppendLine("| House | Cusp |");
        sb.AppendLine("|-------|------|");

        for (int i = 0; i < chart.Houses.Cusps.Count; i++)
        {
            sb.AppendLine(
                Inv, $"| {i + 1} | {PositionFormatter.FormatLongitude(chart.Houses.Cusps[i])} |");
        }

        sb.AppendLine();
        sb.AppendLine($"**ASC:** {PositionFormatter.FormatLongitude(chart.Houses.Ascendant)}  ");
        sb.AppendLine($"**MC:** {PositionFormatter.FormatLongitude(chart.Houses.Midheaven)}");

        return sb.ToString();
    }
}
