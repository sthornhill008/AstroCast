using System.Globalization;
using System.Text;

using AstroCast.Core.Models;
using AstroCast.Core.Services;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a <see cref="SolarReturnResult"/> as a Markdown document containing
/// the return chart data, natal Sun reference, and precision metadata.
/// </summary>
public static class SolarReturnMarkdownFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Generate a complete solar return report as a Markdown string.
    /// </summary>
    /// <param name="result">The solar return result to format.</param>
    /// <param name="interpretationContext">Optional free-text context for AI interpretation.</param>
    /// <returns>A Markdown-formatted solar return report.</returns>
    public static string ToMarkdown(SolarReturnResult result, string? interpretationContext = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        StringBuilder sb = new();

        // Section 1: Header
        sb.AppendLine(
            Inv, $"# Solar Return: {result.NatalChart.Input.Name}, {result.Year}");
        sb.AppendLine();
        sb.AppendLine(Inv, $"**Generated:** {result.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Software:** {result.EngineVersion}  ");
        sb.AppendLine($"**Method:** Tropical zodiac, {result.NatalChart.HouseSystemName} houses  ");

        if (!string.IsNullOrWhiteSpace(interpretationContext))
        {
            sb.AppendLine($"**Interpretation Context:** {interpretationContext.Trim()}");
        }

        sb.AppendLine();

        // Section 2: Solar return metadata
        sb.AppendLine("## Return Details");
        sb.AppendLine();
        sb.AppendLine(
            $"**Natal Sun:** {PositionFormatter.FormatLongitude(result.NatalSunLongitude)}  ");
        sb.AppendLine(
            $"**Return Sun:** {PositionFormatter.FormatLongitude(result.ReturnSunLongitude)}  ");
        sb.AppendLine(
            Inv, $"**Precision:** {result.PrecisionArcSeconds:F4} arc-seconds  ");
        sb.AppendLine(
            $"**Return Date/Time:** {CalendarDateFormatter.DateTime(result.ReturnChart.UtcDateTime)} UTC  ");
        if (CalendarDateFormatter.IsJulian(result.ReturnChart.UtcDateTime) || CalendarDateFormatter.IsJulian(result.NatalChart.UtcDateTime))
            sb.AppendLine(CalendarDateFormatter.JulianCalendarLine + "  ");
        sb.AppendLine(
            Inv, $"**Return Location:** {result.ReturnLocationName} " +
            $"({result.ReturnLatitude:F4}, {result.ReturnLongitude:F4})");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Section 3: Time-lord context of the year the return opens, keyed off the natal
        // chart (not the return chart). All three sections are read for that one year
        // (isReturnContext; branch ReturnAges, 1.7.2): ProfectionAnalyzer.YearOpenedBySolarReturn
        // decides its age and the day it is read on, the birthday the year begins on when
        // the return falls before it. Until 1.7.2 only the profection's age was the year's,
        // and the Firdaria and the releasing read the year running on the return's own day.
        ChartMarkdownFormatter.AppendProfectionYear(
            sb,
            result.NatalChart,
            referenceDate: result.ReturnChart.UtcDateTime,
            headingOverride: "## Annual Profection (at Return Ingress)",
            isReturnContext: true);

        ChartMarkdownFormatter.AppendFirdaria(
            sb,
            result.NatalChart,
            referenceDate: result.ReturnChart.UtcDateTime,
            headingOverride: "## Firdaria (at Return Ingress)",
            isReturnContext: true);

        ChartMarkdownFormatter.AppendZodiacalReleasing(
            sb,
            result.NatalChart,
            referenceDate: result.ReturnChart.UtcDateTime,
            headingOverride: "## Zodiacal Releasing (Spirit) (at Return Ingress)",
            isReturnContext: true);

        sb.AppendLine("---");
        sb.AppendLine();

        // Section 4: Full chart body (reuse ChartMarkdownFormatter, strip header/footer)
        sb.AppendLine("## Return Chart");
        sb.AppendLine();
        sb.Append(FormatChartSection(result.ReturnChart));
        sb.AppendLine();

        // Section 4: Return-to-natal declination aspects
        IReadOnlyList<DeclinationAspect> crossDecAspects =
            DeclinationAspectService.FindCrossChartDeclinationAspects(
                result.ReturnChart.Positions, result.NatalChart.Positions);

        PositionFormatter.AppendCrossChartDeclinationTable(sb, crossDecAspects,
            "## Return-to-Natal Declination Aspects (\u00b11\u00b0 orb)",
            "SR", "Return", "SR Dec",
            "NA", "Natal", "Na Dec");

        // Footer
        sb.AppendLine("---");
        sb.AppendLine(
            Inv, $"*Generated by {result.EngineVersion} at {result.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC*");

        return sb.ToString();
    }

    /// <summary>
    /// Save a solar return result as Markdown to a file.
    /// </summary>
    /// <param name="result">The solar return result to save.</param>
    /// <param name="filePath">Destination file path for the Markdown output.</param>
    /// <param name="interpretationContext">Optional free-text context for AI interpretation.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(
        SolarReturnResult result, string filePath,
        string? interpretationContext = null, CancellationToken ct = default)
    {
        string md = ToMarkdown(result, interpretationContext);
        await File.WriteAllTextAsync(filePath, md, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Format the chart body by reusing <see cref="ChartMarkdownFormatter"/>
    /// and stripping the H1 header line and the footer line.
    /// </summary>
    /// <param name="chart">The chart result to format.</param>
    /// <returns>Markdown body without the H1 header or trailing footer.</returns>
    private static string FormatChartSection(ChartResult chart)
    {
        string fullMarkdown = ChartMarkdownFormatter.ToMarkdown(chart);

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

        StringBuilder sb = new();
        for (int i = startIndex; i <= endIndex; i++)
        {
            sb.Append(lines[i]);
            if (i < endIndex)
                sb.Append('\n');
        }

        return sb.ToString();
    }
}
