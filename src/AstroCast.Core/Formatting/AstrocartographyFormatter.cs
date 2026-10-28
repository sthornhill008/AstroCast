using System.Globalization;
using System.Text;

using AstroCast.Core.Enums;
using AstroCast.Core.Models.Astrocartography;
using AstroCast.Core.Services;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats an <see cref="AcgResult"/> as a Markdown document containing the
/// astrocartography line summary, optional crossing report, and full coordinate
/// tables for every line. Output is self-contained so downstream interpretation
/// agents can render ACG maps without re-running the core computation.
/// </summary>
public static class AstrocartographyFormatter
{
    /// <summary>Invariant culture used for all numeric formatting.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Standard canonical ordering of the four angular line types, used when
    /// iterating over a body's lines in the summary and coordinate sections.
    /// </summary>
    private static readonly AcgLineType[] LineTypeOrder =
    [
        AcgLineType.MC,
        AcgLineType.IC,
        AcgLineType.ASC,
        AcgLineType.DSC,
    ];

    /// <summary>
    /// Render the full astrocartography report as a Markdown string.
    /// </summary>
    /// <param name="result">The ACG result to format.</param>
    /// <param name="crossingReport">Optional crossing report for a specific location.</param>
    /// <returns>A Markdown-formatted ACG report.</returns>
    public static string ToMarkdown(AcgResult result, AcgCrossingReport? crossingReport = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        StringBuilder sb = new();

        AppendHeader(sb, result);
        AppendLineSummary(sb, result);

        if (crossingReport is not null)
        {
            AppendCrossingReport(sb, crossingReport);
        }

        AppendCoordinateTables(sb, result);
        AppendFooter(sb, result);

        return sb.ToString();
    }

    /// <summary>
    /// Save the Markdown rendering of an ACG result to a file.
    /// </summary>
    /// <param name="result">The ACG result to format.</param>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="crossingReport">Optional crossing report for a specific location.</param>
    /// <param name="ct">Cancellation token.</param>
    public static async Task SaveAsync(
        AcgResult result,
        string filePath,
        AcgCrossingReport? crossingReport = null,
        CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string markdown = ToMarkdown(result, crossingReport);
        await File.WriteAllTextAsync(filePath, markdown, ct).ConfigureAwait(false);
    }

    #region Sections

    private static void AppendHeader(StringBuilder sb, AcgResult result)
    {
        string name = result.NatalChart.Input.Name;
        DateTime localDt = result.NatalChart.Input.LocalDateTime;
        // An explicit offset overrides the zone's rules, so the birth wall clock is labelled
        // with that offset, as the chart export's Date/Time line labels it, never with the zone.
        string birthClock = result.NatalChart.Input.UseExplicitOffset && result.NatalChart.Input.UtcOffsetHours.HasValue
            ? result.NatalChart.ResolvedOffsetLabel
            : result.NatalChart.Input.TimeZone;
        string location = result.NatalChart.Input.LocationName;

        sb.AppendLine("# Astrocartography Map");
        sb.AppendLine();
        sb.Append("**Name:** ").AppendLine(name);
        sb.Append("**Born:** ")
          .Append(localDt.ToString("yyyy-MM-dd HH:mm:ss", Inv))
          .Append(' ').Append(birthClock)
          .Append(", ").AppendLine(location);
        sb.Append("**Obliquity:** ")
          .Append(result.Obliquity.ToString("F6", Inv))
          .AppendLine("\u00b0  ");
        sb.Append("**GAST:** ")
          .Append(result.GastDegrees.ToString("F6", Inv))
          .AppendLine("\u00b0  ");
        sb.Append("**Latitude Range:** ")
          .Append(result.LatitudeMin.ToString("F1", Inv))
          .Append("\u00b0 to ")
          .Append(result.LatitudeMax.ToString("F1", Inv))
          .Append("\u00b0 (step ")
          .Append(result.LatitudeStep.ToString("F1", Inv))
          .AppendLine("\u00b0)  ");
        sb.Append("**Generated:** ")
          .Append(DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", Inv))
          .AppendLine(" UTC");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();
    }

    private static void AppendLineSummary(StringBuilder sb, AcgResult result)
    {
        sb.AppendLine("## Angular Lines Summary");
        sb.AppendLine();
        sb.AppendLine("| Body | MC Longitude | IC Longitude | ASC Points | DSC Points | ASC/DSC Latitudes |");
        sb.AppendLine("|------|-------------|-------------|------------|------------|-------------------|");

        // Group lines by body, preserving the body order from NatalChart.Positions.
        foreach (AstroCast.Core.Models.PlanetPosition planet in result.NatalChart.Positions)
        {
            AcgLine? mc = result.Lines.FirstOrDefault(l =>
                l.Body == planet.Body && l.LineType == AcgLineType.MC);
            AcgLine? ic = result.Lines.FirstOrDefault(l =>
                l.Body == planet.Body && l.LineType == AcgLineType.IC);
            AcgLine? asc = result.Lines.FirstOrDefault(l =>
                l.Body == planet.Body && l.LineType == AcgLineType.ASC);
            AcgLine? dsc = result.Lines.FirstOrDefault(l =>
                l.Body == planet.Body && l.LineType == AcgLineType.DSC);

            string bodyName = mc?.BodyName ?? ic?.BodyName ?? asc?.BodyName
                              ?? dsc?.BodyName ?? planet.Name;

            string mcLon = mc is null
                ? "-"
                : mc.Coordinates[0].Longitude.ToString("F2", Inv) + "\u00b0";
            string icLon = ic is null
                ? "-"
                : ic.Coordinates[0].Longitude.ToString("F2", Inv) + "\u00b0";
            string ascCount = asc is null
                ? "-"
                : asc.Coordinates.Count.ToString(Inv);
            string dscCount = dsc is null
                ? "-"
                : dsc.Coordinates.Count.ToString(Inv);

            // The rising and setting lines meet at their ends, so they share one latitude
            // range: where the body rises and sets, within the result's limits.
            AcgLine? horizon = asc ?? dsc;
            string horizonLatitudes = horizon is null || horizon.Coordinates.Count == 0
                ? "-"
                : horizon.Coordinates[0].Latitude.ToString("F2", Inv) + "\u00b0 to "
                  + horizon.Coordinates[^1].Latitude.ToString("F2", Inv) + "\u00b0";

            sb.Append("| ").Append(bodyName)
              .Append(" | ").Append(mcLon)
              .Append(" | ").Append(icLon)
              .Append(" | ").Append(ascCount)
              .Append(" | ").Append(dscCount)
              .Append(" | ").Append(horizonLatitudes)
              .AppendLine(" |");
        }

        sb.AppendLine();
    }

    private static void AppendCrossingReport(StringBuilder sb, AcgCrossingReport report)
    {
        sb.Append("## Crossing Report: ")
          .Append(report.LocationName)
          .Append(" (")
          .Append(report.Latitude.ToString("F4", Inv))
          .Append("\u00b0, ")
          .Append(report.Longitude.ToString("F4", Inv))
          .AppendLine("\u00b0)");
        sb.AppendLine();

        string orb = report.OrbKilometres.ToString("N0", Inv);
        if (report.Crossings.Count == 0)
        {
            sb.Append("*No ACG line passes within ").Append(orb).AppendLine(" km of this location.*");
            sb.AppendLine();
            return;
        }

        sb.Append("Lines within ").Append(orb)
          .AppendLine(" km of the place, by great-circle distance to the nearest point of the line.");
        sb.AppendLine();
        sb.AppendLine("| Body | Line | Distance |");
        sb.AppendLine("|------|------|----------|");

        foreach (AcgCrossing crossing in report.Crossings)
        {
            sb.Append("| ").Append(crossing.BodyName)
              .Append(" | ").Append(crossing.LineType)
              .Append(" | ").Append(crossing.DistanceKilometres.ToString("N0", Inv))
              .AppendLine(" km |");
        }

        sb.AppendLine();
    }

    private static void AppendCoordinateTables(StringBuilder sb, AcgResult result)
    {
        sb.AppendLine("## Line Coordinates");
        sb.AppendLine();

        // Iterate bodies in natal chart order, then by canonical line type order.
        foreach (AstroCast.Core.Models.PlanetPosition planet in result.NatalChart.Positions)
        {
            foreach (AcgLineType lineType in LineTypeOrder)
            {
                AcgLine? line = result.Lines.FirstOrDefault(l =>
                    l.Body == planet.Body && l.LineType == lineType);
                if (line is null)
                {
                    continue;
                }

                sb.Append("### ").Append(line.BodyName).Append(' ').AppendLine(lineType.ToString());
                sb.AppendLine("| Latitude | Longitude |");
                sb.AppendLine("|----------|-----------|");

                foreach (AcgCoordinate coord in line.Coordinates)
                {
                    sb.Append("| ").Append(coord.Latitude.ToString("F2", Inv))
                      .Append(" | ").Append(coord.Longitude.ToString("F2", Inv))
                      .AppendLine(" |");
                }

                sb.AppendLine();
            }
        }
    }

    /// <summary>
    /// Appends the footer: the data note, saying what the lines are and how they are
    /// computed, then the engine version, as the chart export prints it ("AstroCast
    /// 1.3.3 (SwissEph 2.10.03, tzdb 2026e)"), so the file can be dated to the version
    /// that made it (E2-9). The version is the natal chart's: the lines are computed from
    /// it, by the same engine, in the same run.
    /// </summary>
    /// <remarks>
    /// The data note names the bodies the lines are drawn for, in the result's own order
    /// and under the names its lines carry, and the latitude limits from the result.
    /// </remarks>
    private static void AppendFooter(StringBuilder sb, AcgResult result)
    {
        IEnumerable<string> bodies = result.Lines.Select(l => l.BodyName).Distinct();
        string limit = Math.Max(Math.Abs(result.LatitudeMin), Math.Abs(result.LatitudeMax))
            .ToString("0.##", Inv);

        sb.AppendLine("---");
        sb.Append("*Data from AstroCast (Swiss Ephemeris). ")
          .Append("Lines in mundo: each marks where the body itself, not its zodiac degree, ")
          .Append("is on the meridian or the horizon, from its geocentric apparent right ascension ")
          .Append("(RA) and declination on the true equator of date and the Greenwich apparent ")
          .Append("sidereal time (GAST) at the chart's instant. Geocentric, with no parallax (the Moon ")
          .Append("included), and the geometric horizon, with no refraction. Computed in closed form, ")
          .Append("with no search: the MC line at longitude RA \u2212 GAST and the IC line 180\u00b0 from it; ")
          .Append("the ASC (rising) and DSC (setting) lines at RA \u2213 H \u2212 GAST, where ")
          .Append("cos H = \u2212tan(latitude) \u00b7 tan(declination). ")
          .Append("MC and IC lines run from ").Append(limit).Append("\u00b0 S to ").Append(limit)
          .Append("\u00b0 N. ASC and DSC lines have a point at every whole degree from ")
          .Append(limit).Append("\u00b0 S to ").Append(limit)
          .Append("\u00b0 N at which the body rises and sets, and end where it stops, at latitude ")
          .Append("\u00b1(90\u00b0 \u2212 |declination|), where the two lines meet. ")
          .Append("A crossing, listed on screen for a place clicked on the map, is the great-circle distance ")
          .Append("from the place to the nearest point of the line, on a sphere of radius ")
          .Append(AstrocartographyService.EarthRadiusKilometres.ToString("N4", Inv))
          .Append(" km; a line is listed when it is within ")
          .Append(AstrocartographyService.DefaultCrossingOrbKilometres.ToString("N0", Inv)).Append(" km. ")
          .Append("Lines for: ").Append(string.Join(", ", bodies)).AppendLine(".*");
        sb.AppendLine();
        sb.AppendLine($"*Engine: {result.NatalChart.EngineVersion}*");
    }

    #endregion
}
