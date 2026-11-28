using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;
using AstroCast.Core.Services.PrimaryDirections;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// The notes the primary directions rows carry (branch DirectionNotes, 1.7.2; finding A5), on the invented charts,
/// in every place that prints the rows: the whole-life report, the Directions panel's list
/// (<see cref="PrimaryDirectionMarkdownFormatter.FormatTable"/>) and the transit report's section of the period's
/// primary directions. The rows named here were found by script among the invented charts at windows of 2 to 15
/// minutes; their dates, ages, arcs, Months/min and Ranges are the ones the export printed before the branch.
/// </summary>
/// <param name="fixture">The shared ephemeris fixture.</param>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class PrimaryDirectionNotesTests(EphemerisFixture fixture)
{
    #region The turning row

    /// <summary>
    /// Person A's converse Moon to Saturn's dexter square at an uncertainty of 10 minutes, as the export printed it
    /// before the branch: its own date, 1984-12-15, lies after both ends of its Range.
    /// </summary>
    private const string TurningRowBefore =
        "| 1984-12-15 | 37.20 | C | Moon | sqr dex | Saturn | 36\u00b039\u2032 | -0.2 | 1984-10-27 to 1984-11-15 |";

    /// <summary>The same row as it prints now: every value as before, a dagger after each of the two figures.</summary>
    private const string TurningRowNow =
        "| 1984-12-15 | 37.20 | C | Moon | sqr dex | Saturn | 36\u00b039\u2032 | -0.2 \u2020 | 1984-10-27 to 1984-11-15 \u2020 |";

    /// <summary>The dagger's footnote when an uncertainty is set.</summary>
    private const string TurningFootnote =
        "\u2020 This direction's date turns within the birth-time window (its promissor crosses the horizon there). Its Range does not bound its date and its Months/min is not a rate, so the row cannot be used to correct the birth time.";

    /// <summary>
    /// The whole-life report marks the turning row and explains it once: the row's date, age, arc, Months/min and
    /// Range are the ones printed before the branch, each figure followed by a dagger, and one footnote follows the
    /// table after a blank line. No other row carries the mark.
    /// </summary>
    [Fact]
    public void Report_MarksTheTurningRow_AndPrintsOneFootnote()
    {
        List<string> lines = Lines(Report(Hartwell(), window: 10));

        lines.Should().Contain(TurningRowNow);
        lines.Should().NotContain(TurningRowBefore);
        TurningRowNow.Replace(" \u2020", string.Empty, StringComparison.Ordinal).Should().Be(TurningRowBefore);
        lines.Count(l => l.StartsWith('|') && l.Contains('\u2020')).Should().Be(1);
        lines.Count(l => l == TurningFootnote).Should().Be(1);

        int afterTable = EndOfDirectionsTable(lines);
        lines[afterTable].Should().BeEmpty();
        lines[afterTable + 1].Should().Be(TurningFootnote);
    }

    /// <summary>The Directions panel's list is the report's table with its notes: the marked row and the footnote under the rows.</summary>
    [Fact]
    public void PanelList_MarksTheTurningRow_AndPrintsTheFootnoteUnderTheRows()
    {
        PrimaryDirectionResult result = Directions(Hartwell(), window: 10);

        List<string> lines = Lines(PrimaryDirectionMarkdownFormatter.FormatTable(result));

        lines.Should().Contain(TurningRowNow);
        lines.Count(l => l == TurningFootnote).Should().Be(1);
        lines.IndexOf(TurningFootnote).Should().BeGreaterThan(lines.IndexOf(TurningRowNow));
    }

    /// <summary>The transit report's section of the period's primary directions marks the same row and prints the same footnote.</summary>
    [Fact]
    public void TransitReport_MarksTheTurningRow_AndPrintsTheFootnote()
    {
        List<string> lines = Lines(TransitReport(Hartwell(), new DateOnly(1984, 1, 1), new DateOnly(1984, 12, 31), window: 10));

        lines.Should().Contain("### Primary Direction Hits in the Report Period");
        lines.Should().Contain(TurningRowNow);
        lines.Count(l => l == TurningFootnote).Should().Be(1);
    }

    /// <summary>
    /// A report with no turning row has no dagger and no footnote: J2000 NYC at an uncertainty of 4 minutes, and
    /// person A at 4 minutes, where the same direction's date still lies inside its Range.
    /// </summary>
    [Fact]
    public void Report_WithNoTurningRow_HasNoDaggerAndNoFootnote()
    {
        Report(J2000Nyc(), window: 4).Should().NotContain("\u2020");

        string hartwell = Report(Hartwell(), window: 4);
        hartwell.Should().NotContain("\u2020");
        Lines(hartwell).Should().Contain(l => l.StartsWith("| 1984-12-15 | 37.20 | C | Moon | sqr dex | Saturn | 36\u00b039\u2032 | -0.2 | ", StringComparison.Ordinal));
    }

    #endregion

    #region Helpers

    /// <summary>J2000 NYC.</summary>
    private ChartResult J2000Nyc() => GoldenFixtures.J2000Nyc(fixture);

    /// <summary>Person A, Eleanor Hartwell (invented), with a birth-time source.</summary>
    private ChartResult Hartwell() =>
        GoldenFixtures.CreateChartEngine(fixture).Calculate(GoldenFixtures.HartwellInput("Birth Certificate"));

    /// <summary>A chart's directions at an uncertainty, under the default key and age limit.</summary>
    /// <param name="chart">The natal chart.</param>
    /// <param name="window">The birth-time uncertainty, in minutes; 0 for none.</param>
    private PrimaryDirectionResult Directions(ChartResult chart, int window) =>
        new PrimaryDirectionService(fixture.EphemerisService)
            .Calculate(chart, new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = window });

    /// <summary>The whole-life report at an uncertainty, with the golden clock.</summary>
    /// <param name="chart">The natal chart.</param>
    /// <param name="window">The birth-time uncertainty, in minutes; 0 for none.</param>
    private string Report(ChartResult chart, int window) =>
        PrimaryDirectionMarkdownFormatter.ToMarkdown(Directions(chart, window), GoldenFixtures.GoldenClock());

    /// <summary>The transit report of a period with the primary directions alone, at an uncertainty.</summary>
    /// <param name="chart">The natal chart.</param>
    /// <param name="start">The period's first day.</param>
    /// <param name="end">The period's last day.</param>
    /// <param name="window">The birth-time uncertainty, in minutes; 0 for none.</param>
    private string TransitReport(ChartResult chart, DateOnly start, DateOnly end, int window)
    {
        TransitInput input = new()
        {
            NatalChart = chart,
            ReportStartDate = start,
            ReportEndDate = end,
            IncludeMinorAspects = false,
            IncludeTransits = false,
            IncludeSolarArcs = false,
            IncludeSecondaryProgressions = false,
            IncludePrimaryDirections = true,
            PrimaryDirectionSettings = new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = window },
        };
        TransitResult result = new TransitService(fixture.EphemerisService, new LoggerConfiguration().CreateLogger()).Calculate(input);
        return TransitMarkdownFormatter.ToMarkdown(result, timeProvider: GoldenFixtures.GoldenClock());
    }

    /// <summary>The index of the first line after the table of directions: the line that follows its last row.</summary>
    /// <param name="lines">A document's lines.</param>
    private static int EndOfDirectionsTable(List<string> lines)
    {
        int index = lines.FindIndex(l => l.StartsWith("| Date (UTC) |", StringComparison.Ordinal));
        index.Should().BePositive("the document has a table of directions");
        while (index < lines.Count && lines[index].StartsWith('|'))
            index++;
        return index;
    }

    /// <summary>A document's lines without their terminators or trailing spaces.</summary>
    /// <param name="markdown">The document.</param>
    private static List<string> Lines(string markdown) => [.. markdown.Split('\n').Select(l => l.TrimEnd('\r', ' '))];

    #endregion
}
