using System.Globalization;
using System.Text;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a <see cref="ParanResult"/> as a Markdown document containing
/// detected paran events and their underlying angular crossings.
/// </summary>
/// <remarks>
/// Ecliptic longitudes are formatted with 2-letter sign abbreviations
/// (Ar, Ta, Ge, Cn, Le, Vi, Li, Sc, Sg, Cp, Aq, Pi) for compact display
/// in the tabular paran output.
/// </remarks>
public static class ParanMarkdownFormatter
{
    /// <summary>Invariant culture used for all numeric and date formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Two-letter zodiac sign abbreviations, indexed by sign ordinal (0 = Aries).
    /// </summary>
    private static readonly string[] SignAbbreviations =
    [
        "Ar", "Ta", "Ge", "Cn", "Le", "Vi",
        "Li", "Sc", "Sg", "Cp", "Aq", "Pi",
    ];

    /// <summary>
    /// Generate a complete paran report as a Markdown string.
    /// </summary>
    /// <param name="result">The paran result to format.</param>
    /// <param name="chartName">The chart or person name for the report heading.</param>
    /// <returns>A Markdown-formatted paran report.</returns>
    public static string Format(ParanResult result, string chartName)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(chartName);

        StringBuilder sb = new();

        // Header
        sb.AppendLine($"## Parans (Paranatellonta) \u2014 {chartName}");
        sb.AppendLine();
        sb.AppendLine(Inv, $"**Location:** {result.Latitude:F4}\u00b0{(result.Latitude >= 0 ? "N" : "S")}, " +
                      $"{Math.Abs(result.Longitude):F4}\u00b0{(result.Longitude >= 0 ? "E" : "W")}  ");
        sb.AppendLine(Inv, $"**Orb:** {result.OrbMinutes:F1} minutes  ");
        sb.AppendLine(Inv, $"**Bodies:** {result.BodyCount}  ");
        sb.AppendLine(Inv, $"**Parans found:** {result.Parans.Count}  ");
        sb.AppendLine();

        // Paran Events table
        if (result.Parans.Count == 0)
        {
            sb.AppendLine("*No paran events detected within the configured orb.*");
            sb.AppendLine();
        }
        else
        {
            sb.AppendLine("### Paran Events");
            sb.AppendLine();
            sb.AppendLine("| Body A | Angle A | Position A | Body B | Angle B | Position B | Orb (min) |");
            sb.AppendLine("|--------|---------|------------|--------|---------|------------|-----------|");

            foreach (ParanEvent paran in result.Parans)
            {
                string posA = FormatShortLongitude(paran.LongitudeA);
                string posB = FormatShortLongitude(paran.LongitudeB);
                sb.AppendLine(
                    Inv, $"| {paran.BodyAName} | {ChartMarkdownFormatter.FormatAngleType(paran.AngleA)} | {posA} | " +
                    $"{paran.BodyBName} | {ChartMarkdownFormatter.FormatAngleType(paran.AngleB)} | {posB} | {paran.OrbMinutes:F2} |");
            }

            sb.AppendLine();
        }

        // Angular Crossings table
        sb.AppendLine("### Angular Crossings");
        sb.AppendLine();
        sb.AppendLine("| Body | Angle | Position | JD (UT) |");
        sb.AppendLine("|------|-------|----------|---------|");

        foreach (AngularCrossing crossing in result.Crossings)
        {
            string pos = FormatShortLongitude(crossing.Longitude);
            sb.AppendLine(
                Inv, $"| {crossing.BodyName} | {ChartMarkdownFormatter.FormatAngleType(crossing.Angle)} | {pos} | {crossing.JulianDayUt:F6} |");
        }

        sb.AppendLine();

        return sb.ToString();
    }

    /// <summary>
    /// Format the result and save to a file.
    /// </summary>
    /// <param name="result">The paran result to format.</param>
    /// <param name="chartName">The chart or person name for the report heading.</param>
    /// <param name="filePath">The file path to write the Markdown to.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    public static async Task SaveAsync(ParanResult result, string chartName, string filePath)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(chartName);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string markdown = Format(result, chartName);
        await File.WriteAllTextAsync(filePath, markdown, Encoding.UTF8);
    }

    /// <summary>
    /// Formats an ecliptic longitude using 2-letter sign abbreviations
    /// in the compact form DD\u00b0SiMM' (e.g., "15\u00b0Cn23'").
    /// </summary>
    /// <param name="longitude">Ecliptic longitude in decimal degrees (0-360).</param>
    /// <returns>Compact formatted position string.</returns>
    internal static string FormatShortLongitude(double longitude)
    {
        double normalised = ((longitude % 360.0) + 360.0) % 360.0;
        int signIndex = (int)(normalised / 30.0);
        string sign = SignAbbreviations[signIndex];
        double posInSign = normalised % 30.0;
        int degrees = (int)posInSign;
        int minutes = (int)((posInSign - degrees) * 60.0);
        return string.Create(Inv, $"{degrees}\u00b0{sign}{minutes:D2}'");
    }
}
