using System.Globalization;
using System.Text;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a <see cref="SynastryResult"/> as a combined Markdown document
/// containing both natal charts and all synastry data (inter-chart aspects
/// and house overlays in both directions). When both charts carry Ceres, Pallas, Juno, Vesta and Lilith
/// (branch AsteroidsExport, 1.6.0; R6, scoping decision 8), each embedded chart prints its own Asteroids and
/// Lilith section and the report prints <c>## Inter-Chart Aspects: Asteroids and Lilith</c> directly after the
/// Inter-Chart Aspects section; the house overlays never list the five.
/// </summary>
public static class SynastryMarkdownFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>The heading of the minor points' inter-chart section (R6, scoping decision 8).</summary>
    internal const string MinorPointAspectsHeading = "## Inter-Chart Aspects: Asteroids and Lilith";

    /// <summary>The section's opening line: what is aspected to what, Lilith's definition and the orb.</summary>
    internal const string MinorPointAspectsIntroduction =
        "Each chart's Ceres, Pallas, Juno, Vesta and Lilith (Black Moon Lilith, the mean lunar apogee) to the other " +
        "chart's Sun to Pluto, Ascendant and Midheaven: the five Ptolemaic aspects at a 2\u00b000\u2032 orb.";

    /// <summary>
    /// Generate a complete synastry report as a Markdown string.
    /// </summary>
    /// <param name="result">The synastry analysis result to format.</param>
    /// <param name="timeProvider">
    /// Clock threaded into each person's embedded natal chart so the date-keyed
    /// profection / firdaria / zodiacal releasing sections resolve
    /// deterministically. Defaults to <see cref="TimeProvider.System"/> when
    /// <c>null</c>, preserving production behaviour; golden tests pass a fixed
    /// <c>FakeTimeProvider</c> so the two embedded natal blocks stop drifting
    /// with the calendar (F-GR-2). Forwarded to
    /// <see cref="ChartMarkdownFormatter.ToMarkdown"/>.
    /// </param>
    /// <returns>A Markdown-formatted synastry report.</returns>
    public static string ToMarkdown(SynastryResult result, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        StringBuilder sb = new();

        // Section 1: Header
        sb.AppendLine($"# Synastry: {result.PersonA.Input.Name} & {result.PersonB.Input.Name}");
        sb.AppendLine();
        sb.AppendLine(Inv, $"**Generated:** {result.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Software:** {result.EngineVersion}  ");
        sb.AppendLine($"**Method:** Tropical zodiac, {result.PersonA.HouseSystemName} houses");
        if (CalendarDateFormatter.IsJulian(result.PersonA.UtcDateTime) || CalendarDateFormatter.IsJulian(result.PersonB.UtcDateTime))
            sb.AppendLine(CalendarDateFormatter.JulianCalendarLine);
        if (result.RelationshipType != RelationshipType.Unspecified)
            sb.AppendLine($"**Relationship:** {result.RelationshipType.DisplayName}");
        if (!string.IsNullOrEmpty(result.RelationshipNotes))
            sb.AppendLine($"**Relationship Notes:** {result.RelationshipNotes}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Section 2: Person A natal chart
        sb.AppendLine($"## Person A: {result.PersonA.Input.Name}");
        sb.AppendLine();
        sb.Append(FormatNatalSection(result.PersonA, timeProvider));
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Section 3: Person B natal chart
        sb.AppendLine($"## Person B: {result.PersonB.Input.Name}");
        sb.AppendLine();
        sb.Append(FormatNatalSection(result.PersonB, timeProvider));
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Section 4: Inter-chart aspects
        sb.AppendLine("## Inter-Chart Aspects");
        sb.AppendLine();
        sb.AppendLine("| A Body | Aspect | B Body | Orb | Notes |");
        sb.AppendLine("|--------|--------|--------|-----|-------|");

        foreach (InterChartAspect a in result.InterChartAspects)
        {
            sb.AppendLine(
                $"| {a.Body1Name} | {a.Aspect.Abbreviation} | {a.Body2Name} | {PositionFormatter.FormatOrb(a.Orb)} | {a.Note ?? ""} |");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Section 4b: the minor points' inter-chart aspects, only when both charts carry the five (R6,
        // scoping decision 8; branch AsteroidsExport, 1.6.0), with the same spacing and its own rule after
        if (result.PersonA.MinorPoints.Count > 0 && result.PersonB.MinorPoints.Count > 0)
            AppendMinorPointAspects(sb, result);

        // Section 5: House overlays — A in B
        sb.AppendLine(
            $"## House Overlays: {result.PersonA.Input.Name}'s Planets in {result.PersonB.Input.Name}'s Houses");
        sb.AppendLine();
        sb.AppendLine("| A Body | A Position | B House |");
        sb.AppendLine("|--------|------------|---------|");

        foreach (HouseOverlay h in result.PersonAInPersonBHouses)
        {
            sb.AppendLine($"| {h.BodyName} | {h.FormattedPosition} | B {OrdinalHouse(h.House)} |");
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // Section 6: House overlays — B in A
        sb.AppendLine(
            $"## House Overlays: {result.PersonB.Input.Name}'s Planets in {result.PersonA.Input.Name}'s Houses");
        sb.AppendLine();
        sb.AppendLine("| B Body | B Position | A House |");
        sb.AppendLine("|--------|------------|---------|");

        foreach (HouseOverlay h in result.PersonBInPersonAHouses)
        {
            sb.AppendLine($"| {h.BodyName} | {h.FormattedPosition} | A {OrdinalHouse(h.House)} |");
        }

        sb.AppendLine();

        // Section 7: Inter-chart declination aspects
        IReadOnlyList<DeclinationAspect> crossDecAspects =
            DeclinationAspectService.FindCrossChartDeclinationAspects(
                result.PersonA.Positions, result.PersonB.Positions);

        PositionFormatter.AppendCrossChartDeclinationTable(sb, crossDecAspects,
            "## Inter-Chart Declination Aspects (\u00b11\u00b0 orb)",
            "A", "Person A", "A Dec",
            "B", "Person B", "B Dec");

        // Footer
        sb.AppendLine("---");
        sb.AppendLine(
            Inv, $"*Generated by {result.EngineVersion} at {result.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC*");

        return sb.ToString();
    }

    /// <summary>
    /// Save a synastry result as Markdown to a file.
    /// </summary>
    /// <param name="result">The synastry analysis result to save.</param>
    /// <param name="filePath">Destination file path for the Markdown output.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(
        SynastryResult result, string filePath, CancellationToken ct = default)
    {
        string md = ToMarkdown(result);
        await File.WriteAllTextAsync(filePath, md, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Appends the minor points' inter-chart section: the heading, a blank, the opening line, a blank, then the
    /// inter-chart table (A Body, Aspect, B Body, Orb, Notes) with one row per aspect in the result's order, by
    /// orb, each orb the row's own through <see cref="PositionFormatter.FormatOrb"/>, or the none line; then a
    /// blank, a rule and a blank, as the Inter-Chart Aspects section ends.
    /// </summary>
    /// <param name="sb">StringBuilder to append to.</param>
    /// <param name="result">The synastry, both charts carrying the five.</param>
    private static void AppendMinorPointAspects(StringBuilder sb, SynastryResult result)
    {
        sb.AppendLine(MinorPointAspectsHeading);
        sb.AppendLine();
        sb.AppendLine(MinorPointAspectsIntroduction);
        sb.AppendLine();

        if (result.MinorPointAspects.Count == 0)
        {
            sb.AppendLine(ChartMarkdownFormatter.NoMinorPointAspectsLine);
        }
        else
        {
            sb.AppendLine("| A Body | Aspect | B Body | Orb | Notes |");
            sb.AppendLine("|--------|--------|--------|-----|-------|");

            foreach (InterChartAspect a in result.MinorPointAspects)
            {
                sb.AppendLine(
                    $"| {a.Body1Name} | {a.Aspect.Abbreviation} | {a.Body2Name} | {PositionFormatter.FormatOrb(a.Orb)} | {a.Note ?? ""} |");
            }
        }

        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    /// <summary>
    /// Format a single natal chart section by reusing <see cref="ChartMarkdownFormatter"/>
    /// and stripping the H1 header line and the footer line.
    /// </summary>
    /// <param name="chart">The chart result to format.</param>
    /// <param name="timeProvider">
    /// Clock forwarded to <see cref="ChartMarkdownFormatter.ToMarkdown"/> so the
    /// natal time-lord sections are date-deterministic under test.
    /// </param>
    /// <returns>Markdown body without the H1 header or trailing footer.</returns>
    private static string FormatNatalSection(ChartResult chart, TimeProvider? timeProvider)
    {
        string fullMarkdown = ChartMarkdownFormatter.ToMarkdown(chart, timeProvider: timeProvider);

        // Split into lines and remove:
        // - First line (H1 heading: "# Name")
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
    /// Convert a house number to its ordinal string (1st, 2nd, 3rd, ..., 12th).
    /// </summary>
    /// <param name="house">House number (1–12).</param>
    /// <returns>Ordinal string representation.</returns>
    private static string OrdinalHouse(int house) => house switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => string.Create(Inv, $"{house}th")
    };
}
