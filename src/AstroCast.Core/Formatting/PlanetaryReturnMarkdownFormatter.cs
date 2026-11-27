using System.Globalization;
using System.Text;

using AstroCast.Core.Models;
using AstroCast.Core.Services;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a <see cref="PlanetaryReturnResult"/> as a Markdown document containing
/// the return chart data, natal planet reference, and precision metadata.
/// </summary>
public static class PlanetaryReturnMarkdownFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Generate a complete planetary return report as a Markdown string.
    /// </summary>
    /// <param name="result">The planetary return result to format.</param>
    /// <param name="interpretationContext">Optional free-text context for AI interpretation.</param>
    /// <returns>A Markdown-formatted planetary return report.</returns>
    public static string ToMarkdown(PlanetaryReturnResult result, string? interpretationContext = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        StringBuilder sb = new();

        // Section 1: Header
        sb.AppendLine(
            Inv, $"# {result.PlanetName} Return: {result.NatalChart.Input.Name}, #{result.ReturnNumber}");
        sb.AppendLine();
        sb.AppendLine(Inv, $"**Generated:** {result.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Software:** {result.EngineVersion}  ");
        sb.AppendLine($"**Method:** Tropical zodiac, {result.NatalChart.HouseSystemName} houses  ");

        if (!string.IsNullOrWhiteSpace(interpretationContext))
        {
            sb.AppendLine($"**Interpretation Context:** {interpretationContext.Trim()}");
        }

        sb.AppendLine();

        // Section 2: Return metadata
        sb.AppendLine("## Return Details");
        sb.AppendLine();
        sb.AppendLine($"**Planet:** {result.PlanetName}  ");
        sb.AppendLine(Inv, $"**Return Number:** {result.ReturnNumber} (age {result.AgeAtReturn})  ");
        sb.AppendLine(
            $"**Natal {result.PlanetName}:** {PositionFormatter.FormatLongitude(result.NatalLongitude)}  ");
        sb.AppendLine(
            $"**Return {result.PlanetName}:** {PositionFormatter.FormatLongitude(result.ReturnLongitude)}  ");
        sb.AppendLine(
            Inv, $"**Precision:** {result.PrecisionArcSeconds:F4} arc-seconds  ");
        sb.AppendLine(
            $"**Return Date/Time:** {CalendarDateFormatter.DateTime(result.ReturnChart.UtcDateTime)} UTC  ");
        if (CalendarDateFormatter.IsJulian(result.ReturnChart.UtcDateTime) || CalendarDateFormatter.IsJulian(result.NatalChart.UtcDateTime))
            sb.AppendLine(CalendarDateFormatter.JulianCalendarLine + "  ");
        sb.AppendLine($"**Other passes:** {FormatOtherPasses(result.Passes)}  ");
        sb.AppendLine(
            Inv, $"**Return Location:** {result.ReturnLocationName} " +
            $"({result.ReturnLatitude:F4}, {result.ReturnLongitude_Geo:F4})");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Section 3: Time-lord context evaluated at the return's instant, keyed off the natal chart (not
        // the return chart). The profection is the year running at the return, by completed anniversaries
        // (branch H, 1.3.4); the solar return alone rounds to its own year (isReturnContext).
        ChartMarkdownFormatter.AppendProfectionYear(
            sb,
            result.NatalChart,
            referenceDate: result.ReturnChart.UtcDateTime,
            headingOverride: "## Annual Profection (at Return Ingress)");

        ChartMarkdownFormatter.AppendFirdaria(
            sb,
            result.NatalChart,
            referenceDate: result.ReturnChart.UtcDateTime,
            headingOverride: "## Firdaria (at Return Ingress)");

        ChartMarkdownFormatter.AppendZodiacalReleasing(
            sb,
            result.NatalChart,
            referenceDate: result.ReturnChart.UtcDateTime,
            headingOverride: "## Zodiacal Releasing (Spirit) (at Return Ingress)");

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

        string prefix = result.PlanetName.Length >= 2
            ? result.PlanetName[..2].ToUpperInvariant()
            : result.PlanetName.ToUpperInvariant();

        PositionFormatter.AppendCrossChartDeclinationTable(sb, crossDecAspects,
            "## Return-to-Natal Declination Aspects (\u00b11\u00b0 orb)",
            prefix, "Return", $"{prefix} Dec",
            "NA", "Natal", "Na Dec");

        // Footer
        sb.AppendLine("---");
        sb.AppendLine(
            Inv, $"*Generated by {result.EngineVersion} at {result.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC*");

        return sb.ToString();
    }

    /// <summary>
    /// Save a planetary return result as Markdown to a file.
    /// </summary>
    /// <param name="result">The planetary return result to save.</param>
    /// <param name="filePath">Destination file path for the Markdown output.</param>
    /// <param name="interpretationContext">Optional free-text context for AI interpretation.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(
        PlanetaryReturnResult result, string filePath,
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
    /// <summary>
    /// The Other passes line's value: "none" for a single-pass return, or the passage's passes after
    /// the first, each as its UTC instant with its direction, which alternates: the second pass is
    /// retrograde, the third direct.
    /// </summary>
    /// <param name="passes">The passage's passes in order, the first the return's own.</param>
    /// <returns>The line's value.</returns>
    private static string FormatOtherPasses(IReadOnlyList<DateTime> passes)
    {
        if (passes.Count <= 1)
            return "none";

        List<string> others = [];
        for (int i = 1; i < passes.Count; i++)
        {
            string direction = i % 2 == 1 ? "retrograde" : "direct";
            others.Add($"{CalendarDateFormatter.DateTime(passes[i])} UTC ({direction})");
        }

        return string.Join(", ", others);
    }

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
                sb.AppendLine();
        }

        return sb.ToString();
    }
}
