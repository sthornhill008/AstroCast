using System.Globalization;
using System.Text;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;

namespace AstroCast.Core.Formatting;

/// <summary>
/// The one writer of a Primary Directions section (branch PrimaryExport, 1.5.0): the method, key,
/// significator and promissor lines, the count left out, the birth-time caution, then a sub-heading and a
/// table of hits or a sentence. The whole-life report and the transit report both print it, with their
/// own sub-heading, hits and none sentence.
/// </summary>
internal static class PrimaryDirectionSectionFormatter
{
    /// <summary>Invariant culture for every number and date.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>The section's heading.</summary>
    internal const string Heading = "## Primary Directions";

    /// <summary>The Method line: the technique is fixed.</summary>
    internal const string MethodLine = "**Method:** Placidus semi-arc, in zodiaco; direct and converse (traditional)";

    /// <summary>The Promissors line: the promissor points are fixed (rule R4).</summary>
    internal const string PromissorsLine =
        "**Promissors:** the planets and Chiron with the Ptolemaic aspects, the node by conjunction and opposition; "
        + "their antiscia and the 55 fixed stars by conjunction";

    /// <summary>The cell printed for a figure the engine could not give: an em dash (U+2014).</summary>
    internal const string NoFigure = "—";

    /// <summary>The table's header, before the optional Range column.</summary>
    private const string TableHeader = "| Date (UTC) | Age | D/C | Significator | Aspect | Promissor | Arc | Months/min |";

    /// <summary>The table's separator row, before the optional Range column.</summary>
    private const string TableSeparator = "|------------|-----|-----|--------------|--------|-----------|-----|------------|";

    /// <summary>The Range column's separator cell.</summary>
    private const string RangeSeparator = "----------------|";

    /// <summary>
    /// Appends the section: the heading, a blank line, the header lines with no blank between them, a blank
    /// line, the sub-heading, a blank line, then the table of <paramref name="hits"/> in the order given,
    /// or <paramref name="noneSentence"/> when there is none, and a closing blank line.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="result">The run: its settings, its count left out and its natal chart.</param>
    /// <param name="hits">The hits to tabulate: the whole life, or the period's.</param>
    /// <param name="subheading">The sub-heading, without its heading mark.</param>
    /// <param name="noneSentence">The sentence printed when there is no hit.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    internal static void AppendSection(
        StringBuilder sb,
        PrimaryDirectionResult result,
        IReadOnlyList<PrimaryDirectionHit> hits,
        string subheading,
        string noneSentence)
    {
        ArgumentNullException.ThrowIfNull(sb);
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(subheading);
        ArgumentNullException.ThrowIfNull(noneSentence);

        sb.AppendLine(Heading);
        sb.AppendLine();
        sb.AppendLine(MethodLine);
        sb.AppendLine($"**Key:** {result.Settings.Key.DisplayName}");
        sb.AppendLine(SignificatorsLine(result.Settings));
        sb.AppendLine(PromissorsLine);
        if (LeftOutLine(result) is string leftOut)
            sb.AppendLine(leftOut);
        if (BirthTimeLine(result.NatalChart.Input) is string birthTime)
            sb.AppendLine(birthTime);
        sb.AppendLine();
        sb.AppendLine($"### {subheading}");
        sb.AppendLine();

        AppendTable(sb, result.Settings, hits, noneSentence);
        sb.AppendLine();
    }

    /// <summary>
    /// Appends the table of hits as the section prints it: the header row (with the Range column when the
    /// uncertainty is above 0), the separator row and one row per hit in the order given; or, when there is no
    /// hit, the none sentence. Each line is ended by <see cref="StringBuilder.AppendLine()"/>, and no blank line
    /// follows. The section and <see cref="PrimaryDirectionMarkdownFormatter.FormatTable"/> both draw it from
    /// here (branch PrimaryPanel, 1.5.0), so the Directions panel's list and the report show the same rows.
    /// </summary>
    /// <param name="sb">The string builder to append to.</param>
    /// <param name="settings">The run's settings: the uncertainty decides the Range column.</param>
    /// <param name="hits">The hits to tabulate.</param>
    /// <param name="noneSentence">The sentence printed when there is no hit.</param>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    internal static void AppendTable(
        StringBuilder sb,
        PrimaryDirectionSettings settings,
        IReadOnlyList<PrimaryDirectionHit> hits,
        string noneSentence)
    {
        ArgumentNullException.ThrowIfNull(sb);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(hits);
        ArgumentNullException.ThrowIfNull(noneSentence);

        if (hits.Count == 0)
        {
            sb.AppendLine(noneSentence);
            return;
        }

        int uncertainty = settings.BirthTimeUncertaintyMinutes;
        bool withRange = uncertainty > 0;

        sb.Append(TableHeader);
        if (withRange)
            sb.Append(Inv, $" Range (±{uncertainty} min) |");
        sb.AppendLine();

        sb.Append(TableSeparator);
        if (withRange)
            sb.Append(RangeSeparator);
        sb.AppendLine();

        foreach (PrimaryDirectionHit hit in hits)
            sb.AppendLine(FormatRow(hit, withRange));
    }

    /// <summary>
    /// The Significators line: the angles, the lights and the Lot, with Mercury to Pluto when the setting adds
    /// them, then the Descendant and IC by the conjunctions of bodies and stars (rule R5 as amended on 2026-10-18).
    /// </summary>
    /// <param name="settings">The run's settings.</param>
    /// <returns>The line.</returns>
    internal static string SignificatorsLine(PrimaryDirectionSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        string planets = settings.PlanetsAsSignificators ? ", Mercury to Pluto" : "";
        return $"**Significators:** Ascendant, Midheaven, Sun, Moon, Part of Fortune{planets}; the Descendant and IC by the conjunctions of bodies and stars";
    }

    /// <summary>
    /// The Left out line, only when the count is above 0: the directions whose point never rises or sets at
    /// the chart's latitude, printed as the electional export prints a latitude, F4 with N or S.
    /// </summary>
    /// <param name="result">The run.</param>
    /// <returns>The line, or null when nothing was left out.</returns>
    internal static string? LeftOutLine(PrimaryDirectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        int count = result.LeftOutCount;
        if (count <= 0)
            return null;

        double latitude = result.NatalChart.Input.Latitude;
        string noun = count == 1 ? "direction" : "directions";
        return string.Create(Inv,
            $"**Left out:** {count} {noun} whose point never rises or sets at {Math.Abs(latitude):F4}° {(latitude >= 0 ? "N" : "S")} (no semi-arc)");
    }

    /// <summary>
    /// The Birth time caution, only when the chart's birth-time source is null, blank or "Unknown" in any
    /// case: every date moves with the birth time, by the Months/min column.
    /// </summary>
    /// <param name="input">The natal chart's input.</param>
    /// <returns>The line, or null when the source is given.</returns>
    internal static string? BirthTimeLine(ChartInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        string? source = input.SourceNotes;

        string? state = null;
        if (string.IsNullOrWhiteSpace(source))
            state = "not specified";
        else if (string.Equals(source.Trim(), "Unknown", StringComparison.OrdinalIgnoreCase))
            state = "Unknown";

        return state is null
            ? null
            : $"**Birth time:** unconfirmed (Birth Time Source {state}): every date below moves with the birth time; see Months/min";
    }

    /// <summary>
    /// A hit's table row: the UT date on the calendar in force, the age to two decimals, D or C, the
    /// significator and promissor by name, the aspect label, the arc as D°MM′, the months per minute signed
    /// to one decimal, and, when asked, the range as two dates; a missing figure prints an em dash.
    /// </summary>
    /// <param name="hit">The hit.</param>
    /// <param name="withRange">Whether the Range column is printed.</param>
    /// <returns>The row.</returns>
    internal static string FormatRow(PrimaryDirectionHit hit, bool withRange)
    {
        ArgumentNullException.ThrowIfNull(hit);

        string motion = hit.Motion == PrimaryDirectionMotion.Direct ? "D" : "C";
        string row = string.Create(Inv,
            $"| {CalendarDateFormatter.Date(hit.InstantUtc)} | {hit.Years:F2} | {motion} | {hit.Significator} | {AspectLabel(hit)} | {hit.Promissor} | {PositionFormatter.FormatArc(hit.Arc)} | {FormatMonthsPerMinute(hit.MonthsPerMinute)} |");

        if (!withRange)
            return row;

        string range = hit.RangeStartUtc is DateTime start && hit.RangeEndUtc is DateTime end
            ? $"{CalendarDateFormatter.Date(start)} to {CalendarDateFormatter.Date(end)}"
            : NoFigure;
        return $"{row} {range} |";
    }

    /// <summary>
    /// The aspect label: cnj, opp, sxt sin or dex, sqr sin or dex, tri sin or dex (sin for the positive angle,
    /// dex for the negative), antiscion, contra-antiscion.
    /// </summary>
    /// <param name="hit">The hit.</param>
    /// <returns>The label.</returns>
    internal static string AspectLabel(PrimaryDirectionHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        string side = hit.AspectAngle >= 0.0 ? "sin" : "dex";
        return hit.Aspect switch
        {
            PrimaryDirectionAspect.Conjunction => "cnj",
            PrimaryDirectionAspect.Opposition => "opp",
            PrimaryDirectionAspect.Sextile => $"sxt {side}",
            PrimaryDirectionAspect.Square => $"sqr {side}",
            PrimaryDirectionAspect.Trine => $"tri {side}",
            PrimaryDirectionAspect.Antiscion => "antiscion",
            PrimaryDirectionAspect.ContraAntiscion => "contra-antiscion",
            _ => hit.Aspect.ToString(),
        };
    }

    /// <summary>The months per minute, signed to one decimal ("+0.0;-0.0;0.0"), or an em dash when null.</summary>
    /// <param name="monthsPerMinute">The figure, or null.</param>
    /// <returns>The cell.</returns>
    internal static string FormatMonthsPerMinute(double? monthsPerMinute) =>
        monthsPerMinute is double months ? months.ToString("+0.0;-0.0;0.0", Inv) : NoFigure;
}
