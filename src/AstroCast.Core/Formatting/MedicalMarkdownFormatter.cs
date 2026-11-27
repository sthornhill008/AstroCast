using System.Globalization;
using System.Text;

using AstroCast.Core.Enums;
using AstroCast.Core.MedicalAstrology;
using AstroCast.Core.Models;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a natal chart and its <see cref="HealthProfile"/> as a Markdown document
/// containing full chart data plus medical astrology correspondence tables.
/// Designed for upload to an AI with an interpretation prompt — outputs raw data,
/// not interpretive text. Follows the same pattern as
/// <see cref="ChartMarkdownFormatter"/> and <see cref="SolarReturnMarkdownFormatter"/>.
/// </summary>
public static class MedicalMarkdownFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Generate a complete medical astrology report as a Markdown string.
    /// Contains the full natal chart data followed by medical correspondence tables.
    /// </summary>
    /// <param name="chartResult">The natal chart calculation result.</param>
    /// <param name="profile">The medical astrology health profile overlay.</param>
    /// <param name="timeProvider">
    /// Clock forwarded to <see cref="ChartMarkdownFormatter.ToMarkdown"/> for the
    /// embedded natal chart so its date-keyed profection / firdaria / zodiacal
    /// releasing sections resolve deterministically. Defaults to
    /// <see cref="TimeProvider.System"/> when <c>null</c>, preserving production
    /// behaviour; golden tests pass a fixed <c>FakeTimeProvider</c> (F-GR-2).
    /// </param>
    /// <param name="healthContext">
    /// The free-text health context from the Medical panel, or <c>null</c>. When it is not blank it is trimmed and
    /// printed in the header, after the Tradition line, as <c>**Health Context:** {text}</c>, each line break
    /// becoming a space; a null or blank context prints no line.
    /// </param>
    /// <returns>A Markdown-formatted report for AI interpretation.</returns>
    public static string Format(
        ChartResult chartResult, HealthProfile profile, TimeProvider? timeProvider = null,
        string? healthContext = null)
    {
        ArgumentNullException.ThrowIfNull(chartResult);
        ArgumentNullException.ThrowIfNull(profile);

        StringBuilder sb = new();

        // Section 1: Header
        sb.AppendLine($"# Medical Astrology Health Profile: {profile.SubjectName}");
        sb.AppendLine();
        sb.AppendLine(Inv, $"**Generated:** {chartResult.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Software:** {chartResult.EngineVersion}  ");
        sb.AppendLine($"**Method:** Tropical zodiac, {chartResult.HouseSystemName} houses  ");
        sb.AppendLine("**Tradition:** Galenic melothesia, Ebertin COSI, Nauman Med-Scan  ");
        if (!string.IsNullOrWhiteSpace(healthContext))
        {
            string context = string.Join(' ', MarkdownLineSplitter.SplitLines(healthContext.Trim()));
            sb.AppendLine($"**Health Context:** {context}  ");
        }
        sb.AppendLine();

        // Section 2: Full natal chart (reuse ChartMarkdownFormatter, strip header/footer)
        sb.AppendLine("## Natal Chart");
        sb.AppendLine();
        sb.Append(FormatChartSection(chartResult, timeProvider));
        sb.AppendLine();

        // The export's own sections, under the line that says what they are. The embedded chart leaves its
        // Medical Astrology Correspondences section out, which carried the line and a shorter copy of the
        // tables below (branch ExportText, 1.7.2; finding N14).
        sb.AppendLine(ChartMarkdownFormatter.MedicalBasisLine);
        sb.AppendLine();

        // Section 3: Temperament Calculation
        sb.AppendLine("## Temperament Assessment");
        sb.AppendLine();
        sb.AppendLine($"**Dominant Temperament:** {profile.Temperament.DominantText}  ");
        sb.AppendLine($"**Secondary Temperament:** {profile.Temperament.SecondaryText}  ");
        sb.AppendLine(Inv, $"**Total Points:** {profile.Temperament.TotalPoints}");
        sb.AppendLine();
        sb.AppendLine("### Elemental Scores");
        sb.AppendLine();
        sb.AppendLine("Scoring (AstroCast's own scheme): Ascendant sign = 3 pts, Ascendant ruler sign = 2 pts, Moon sign = 3 pts, Moon dispositor sign = 2 pts, Sun sign = 1 pt, each body in houses 1, 4, 7 or 10 = 1 pt.");
        sb.AppendLine();
        sb.AppendLine("| Element | Temperament | Qualities | Score |");
        sb.AppendLine("|---------|-------------|-----------|-------|");
        sb.AppendLine(Inv, $"| Fire | Choleric | Hot & Dry | {profile.Temperament.FireScore} |");
        sb.AppendLine(Inv, $"| Earth | Melancholic | Cold & Dry | {profile.Temperament.EarthScore} |");
        sb.AppendLine(Inv, $"| Air | Sanguine | Hot & Moist | {profile.Temperament.AirScore} |");
        sb.AppendLine(Inv, $"| Water | Phlegmatic | Cold & Moist | {profile.Temperament.WaterScore} |");
        sb.AppendLine();

        // Section 4: Zodiacal Anatomy — Emphasised Signs
        sb.AppendLine("## Zodiacal Anatomy (Emphasised Signs)");
        sb.AppendLine();
        sb.AppendLine("Signs with significant planetary emphasis: Ascendant sign, Sun sign, Moon sign, and signs with 2+ planets.");
        sb.AppendLine();

        foreach (SignEmphasis emphasis in profile.SignEmphases)
        {
            sb.AppendLine($"### {emphasis.Sign} ({emphasis.Reason})");
            sb.AppendLine();
            sb.AppendLine("| Body Region | Traditional Correspondence |");
            sb.AppendLine("|-------------|---------------------------|");
            foreach (BodyRegion region in emphasis.BodyRegions)
            {
                sb.AppendLine($"| {region.Name} | {region.Description} |");
            }
            sb.AppendLine();
        }

        // Section 5: Planetary Body Correspondences
        sb.AppendLine("## Planetary Body Correspondences");
        sb.AppendLine();
        sb.AppendLine("Traditional organ/system rulerships for each planet in this chart.");
        sb.AppendLine();
        sb.AppendLine("| Planet | Sign | House | Dir | Body Regions | Health Role |");
        sb.AppendLine("|--------|------|-------|-----|-------------|------------|");

        foreach (PlanetPosition pos in chartResult.Positions)
        {
            IReadOnlyList<BodyRegion> regions = PlanetaryHealthMap.GetBodyRegions(pos.Body);
            string healthRole = PlanetaryHealthMap.GetHealthRole(pos.Body);

            if (regions.Count == 0) continue; // Skip TrueNode, Chiron

            string houseStr = pos.House.HasValue ? pos.House.Value.ToString(Inv) : "-";
            string regionNames = string.Join("; ", regions.Select(r => r.Name));
            string roleShort = TruncateAtSentence(healthRole);

            sb.AppendLine($"| {pos.Name} | {pos.Sign} | {houseStr} | {pos.DirectionCode} | {regionNames} | {roleShort} |");
        }

        sb.AppendLine();

        // Section 6: Hard Aspects (Medical Significance)
        sb.AppendLine("## Hard Aspects (Medical Significance)");
        sb.AppendLine();

        if (profile.MedicalAspects.Count == 0)
        {
            sb.AppendLine("No hard aspects (conjunction, opposition, square, quincunx) detected.");
        }
        else
        {
            sb.AppendLine("| Aspect | Type | Orb | Ap/Sp | Significance | Body 1 Regions | Body 2 Regions |");
            sb.AppendLine("|--------|------|-----|-------|-------------|----------------|----------------|");

            foreach (MedicalAspectInfo aspect in profile.MedicalAspects)
            {
                string apSp = aspect.IsApplying ? "Ap" : "Sp";
                string body1 = string.Join("; ", aspect.Body1Regions.Select(r => r.Name));
                string body2 = string.Join("; ", aspect.Body2Regions.Select(r => r.Name));

                sb.AppendLine(
                    $"| {aspect.AspectDescription} | {aspect.AspectType} | {PositionFormatter.FormatOrb(aspect.Orb)} | {apSp} | {aspect.Significance} | {body1} | {body2} |");
            }
        }

        sb.AppendLine();

        // Section 7: Health Houses (6th, 8th, 12th)
        sb.AppendLine("## Health Houses");
        sb.AppendLine();
        sb.AppendLine("| House | Traditional Meaning | Cusp Sign | Planets |");
        sb.AppendLine("|-------|---------------------|-----------|---------|");

        // 6th house
        ZodiacSign sixthSign = profile.SixthHouseSign;
        string planetsIn6 = profile.PlanetsInSixth.Count > 0
            ? string.Join(", ", profile.PlanetsInSixth) : "None";
        sb.AppendLine($"| 6th | Daily health, acute illness, health habits | {sixthSign} | {planetsIn6} |");

        // 8th house
        ZodiacSign eighthSign = GetSignFromLongitude(chartResult.Houses.Cusps[7]);
        List<string> planetsIn8 = chartResult.Positions
            .Where(p => p.House == 8).Select(p => p.Name).ToList();
        string planetsIn8Str = planetsIn8.Count > 0 ? string.Join(", ", planetsIn8) : "None";
        sb.AppendLine($"| 8th | Chronic conditions, surgery, regeneration | {eighthSign} | {planetsIn8Str} |");

        // 12th house
        ZodiacSign twelfthSign = GetSignFromLongitude(chartResult.Houses.Cusps[11]);
        List<string> planetsIn12 = chartResult.Positions
            .Where(p => p.House == 12).Select(p => p.Name).ToList();
        string planetsIn12Str = planetsIn12.Count > 0 ? string.Join(", ", planetsIn12) : "None";
        sb.AppendLine($"| 12th | Hidden illness, hospitals, immune system | {twelfthSign} | {planetsIn12Str} |");

        sb.AppendLine();

        // Section 8: Health House Cusp Body Regions
        sb.AppendLine("### Health House Cusp Correspondences");
        sb.AppendLine();

        AppendHouseCuspRegions(sb, "6th", sixthSign);
        AppendHouseCuspRegions(sb, "8th", eighthSign);
        AppendHouseCuspRegions(sb, "12th", twelfthSign);

        // Footer
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine(Inv, $"*Generated by {chartResult.EngineVersion} at {chartResult.CalculatedAt:yyyy-MM-dd HH:mm:ss} UTC*");

        return sb.ToString();
    }

    /// <summary>
    /// Save a medical astrology report as Markdown to a file.
    /// </summary>
    /// <param name="chartResult">The natal chart calculation result.</param>
    /// <param name="profile">The medical astrology health profile overlay.</param>
    /// <param name="filePath">The file path to write to.</param>
    /// <param name="ct">Optional cancellation token.</param>
    /// <param name="healthContext">
    /// The free-text health context from the Medical panel, or <c>null</c>; printed in the header as
    /// <see cref="Format"/> describes.
    /// </param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    public static async Task SaveAsync(
        ChartResult chartResult, HealthProfile profile, string filePath,
        CancellationToken ct = default, string? healthContext = null)
    {
        string md = Format(chartResult, profile, healthContext: healthContext);
        await File.WriteAllTextAsync(filePath, md, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Format the natal chart body by reusing <see cref="ChartMarkdownFormatter"/>
    /// and stripping the H1 header line and the footer line.
    /// </summary>
    /// <param name="chart">The chart result to format.</param>
    /// <param name="timeProvider">
    /// Clock forwarded to <see cref="ChartMarkdownFormatter.ToMarkdown"/> so the
    /// natal time-lord sections are date-deterministic under test.
    /// </param>
    private static string FormatChartSection(ChartResult chart, TimeProvider? timeProvider)
    {
        // Never the five minor points (R8)
        string fullMarkdown = ChartMarkdownFormatter.ToMarkdown(chart, timeProvider: timeProvider, minorPoints: false, medicalCorrespondences: false);

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

    /// <summary>
    /// Append body region table for a health house cusp sign.
    /// </summary>
    private static void AppendHouseCuspRegions(StringBuilder sb, string houseLabel, ZodiacSign sign)
    {
        IReadOnlyList<BodyRegion> regions = ZodiacBodyMap.GetBodyRegions(sign);
        sb.AppendLine($"**{houseLabel} House cusp ({sign}):**");
        sb.AppendLine();
        sb.AppendLine("| Body Region | Correspondence |");
        sb.AppendLine("|-------------|----------------|");
        foreach (BodyRegion region in regions)
        {
            sb.AppendLine($"| {region.Name} | {region.Description} |");
        }
        sb.AppendLine();
    }

    /// <summary>
    /// Truncate a string at the first sentence boundary for table brevity.
    /// </summary>
    private static string TruncateAtSentence(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        int periodIndex = text.IndexOf(". ", StringComparison.Ordinal);
        return periodIndex > 0 ? text[..(periodIndex + 1)] : text;
    }

    /// <summary>
    /// Derive the zodiac sign from an ecliptic longitude (0-360 degrees).
    /// </summary>
    private static ZodiacSign GetSignFromLongitude(double longitude) =>
        (ZodiacSign)((int)(longitude % 360.0 / 30.0) + 1);
}
