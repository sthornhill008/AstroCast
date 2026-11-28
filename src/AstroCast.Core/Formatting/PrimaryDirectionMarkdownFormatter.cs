using System.Globalization;
using System.Text;

using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Formats a <see cref="PrimaryDirectionResult"/> as the whole-life Primary Directions Report (branch
/// PrimaryExport, 1.5.0), on the transit report's shape: the header, the natal chart embedded, the Primary
/// Directions section from birth to the age limit (<see cref="PrimaryDirectionSectionFormatter"/>), and
/// the engine footer. Markdown only, as the transit family is.
/// </summary>
public static class PrimaryDirectionMarkdownFormatter
{
    /// <summary>Invariant culture for every number and date.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// Formats the result as a complete Markdown document.
    /// </summary>
    /// <param name="result">The primary directions of a chart.</param>
    /// <param name="timeProvider">
    /// Clock forwarded to <see cref="ChartMarkdownFormatter.ToMarkdown"/> for the embedded natal chart, so
    /// its date-keyed time-lord sections resolve deterministically; <see cref="TimeProvider.System"/> when
    /// null.
    /// </param>
    /// <returns>The Markdown document.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    public static string ToMarkdown(PrimaryDirectionResult result, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        ChartResult chart = result.NatalChart;
        ChartInput input = chart.Input;
        int ageLimit = result.Settings.AgeLimitYears;

        StringBuilder sb = new();

        sb.AppendLine("# Primary Directions Report");
        sb.AppendLine();
        sb.AppendLine($"**Name:** {input.Name}  ");
        // An explicit offset overrides the zone's rules, so the birth wall clock is labelled with that
        // offset, as the transit report labels it, never with the zone.
        string natalClock = input.UseExplicitOffset && input.UtcOffsetHours.HasValue
            ? chart.ResolvedOffsetLabel
            : input.TimeZone;
        sb.AppendLine(Inv, $"**Natal Date:** {input.LocalDateTime:yyyy-MM-dd}, {input.LocalDateTime:HH:mm:ss} {natalClock}, {input.LocationName}  ");
        sb.AppendLine($"**Birth Time Source:** {(string.IsNullOrEmpty(input.SourceNotes) ? "Not specified" : input.SourceNotes)}  ");
        sb.AppendLine(Inv, $"**Span:** birth to age {ageLimit}  ");
        // The earliest instant the report prints is the birth: before 1582-10-15 the dates are Julian (R-D2)
        if (CalendarDateFormatter.IsJulian(chart.UtcDateTime))
            sb.AppendLine(CalendarDateFormatter.JulianCalendarLine + "  ");
        sb.AppendLine("**Techniques:** Primary Directions  ");
        sb.AppendLine(Inv, $"**Generated:** {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC  ");
        sb.AppendLine($"**Software:** AstroCast — Swiss Ephemeris, Tropical, {chart.HouseSystemName}");
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        // The natal chart, as the transit report embeds it: without its title and footer, and never with the
        // five minor points (R8)
        sb.AppendLine("## Natal Chart");
        sb.AppendLine();
        sb.Append(TransitMarkdownFormatter.FormatNatalBody(ChartMarkdownFormatter.ToMarkdown(chart, timeProvider: timeProvider, minorPoints: false)));
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("---");
        sb.AppendLine();

        PrimaryDirectionSectionFormatter.AppendSection(
            sb,
            result,
            result.Hits,
            string.Create(Inv, $"Primary Directions, Birth to Age {ageLimit}"),
            NoneSentence(result.Settings));

        sb.AppendLine("---");
        sb.AppendLine($"*Engine: {chart.EngineVersion}*");

        return sb.ToString();
    }

    /// <summary>
    /// Formats the whole-life section's table exactly as <see cref="ToMarkdown"/> prints it (branch PrimaryPanel,
    /// 1.5.0): the header row, with the Range column when the uncertainty is above 0, the separator row and one
    /// row per hit; or, when there is no hit, the none sentence "No primary direction falls before age 100.".
    /// Each line is ended as <see cref="StringBuilder.AppendLine()"/> ends it. The Directions panel lists it,
    /// so the screen and the file show the same rows.
    /// </summary>
    /// <remarks>
    /// The table's notes come with it, as the report prints them under its rows (branch DirectionNotes, 1.7.2;
    /// finding A5): a turning row's daggers and their footnote
    /// (<see cref="PrimaryDirectionSectionFormatter.AppendTable"/>).
    /// </remarks>
    /// <param name="result">The primary directions of a chart.</param>
    /// <returns>The table, or the none sentence.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    public static string FormatTable(PrimaryDirectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        StringBuilder sb = new();
        PrimaryDirectionSectionFormatter.AppendTable(sb, result.Settings, result.Hits, NoneSentence(result.Settings));
        return sb.ToString();
    }

    /// <summary>The whole-life none sentence: "No primary direction falls before age 100.", the setting's limit.</summary>
    /// <param name="settings">The run's settings.</param>
    /// <returns>The sentence.</returns>
    private static string NoneSentence(PrimaryDirectionSettings settings) =>
        string.Create(Inv, $"No primary direction falls before age {settings.AgeLimitYears}.");

    /// <summary>
    /// Saves the result as Markdown to a file.
    /// </summary>
    /// <param name="result">The primary directions of a chart.</param>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes when the file is written.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="result"/> is null.</exception>
    public static async Task SaveAsync(PrimaryDirectionResult result, string filePath, CancellationToken ct = default)
    {
        string md = ToMarkdown(result);
        await File.WriteAllTextAsync(filePath, md, ct).ConfigureAwait(false);
    }
}
