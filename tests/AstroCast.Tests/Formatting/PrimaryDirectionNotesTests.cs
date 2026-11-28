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

    #region The cut-off Range

    /// <summary>The double dagger's footnote where rows are cut off by birth and by the age limit.</summary>
    private const string BothReasonsFootnote =
        "\u2021 No Range: at one end of the birth-time window this direction falls before birth or beyond the age limit.";

    /// <summary>
    /// J2000 NYC at an uncertainty of 4 minutes: the seven rows without a Range carry the double dagger after their
    /// em dash, every other cell as before the branch, and one footnote names both reasons, a row cut off by birth
    /// (the converse Midheaven to Mars's dexter trine, age 0.49) and one by the age limit (Alcyone to the
    /// Ascendant, age 99.20) being among them. No row turns, so no dagger prints.
    /// </summary>
    [Fact]
    public void Report_MarksTheRowsCutOffByBirthAndByTheAgeLimit_AndNamesBothReasons()
    {
        List<string> lines = Lines(Report(J2000Nyc(), window: 4));

        lines.Should().Contain("| 2000-06-27 | 0.49 | C | Midheaven | tri dex | Mars | 0\u00b028\u2032 | +3.0 | \u2014 \u2021 |");
        lines.Should().Contain("| 2099-03-14 | 99.20 | D | Ascendant | cnj | Alcyone | 97\u00b046\u2032 | -3.1 | \u2014 \u2021 |");
        lines.Count(l => l.StartsWith('|') && l.Contains('\u2021')).Should().Be(7);
        lines.Should().NotContain(l => l.EndsWith("| \u2014 |", StringComparison.Ordinal), "every row without a Range has its reason");
        lines.Count(l => l == BothReasonsFootnote).Should().Be(1);
        lines.Should().NotContain(l => l.Contains('\u2020'));

        int afterTable = EndOfDirectionsTable(lines);
        lines[afterTable].Should().BeEmpty();
        lines[afterTable + 1].Should().Be(BothReasonsFootnote);
    }

    /// <summary>
    /// The footnote names the reasons that occur among the printed rows and no other: the transit report's section
    /// for J2000 NYC's first year holds rows cut off by birth alone, and its section for 2099 rows cut off by the
    /// age limit alone.
    /// </summary>
    [Fact]
    public void TransitReport_FootnoteNamesOnlyTheReasonsOfItsOwnRows()
    {
        List<string> firstYear = Lines(TransitReport(J2000Nyc(), new DateOnly(2000, 1, 2), new DateOnly(2000, 12, 31), window: 4));
        List<string> lastYear = Lines(TransitReport(J2000Nyc(), new DateOnly(2099, 1, 1), new DateOnly(2099, 12, 31), window: 4));

        firstYear.Should().Contain("| 2000-06-27 | 0.49 | C | Midheaven | tri dex | Mars | 0\u00b028\u2032 | +3.0 | \u2014 \u2021 |");
        firstYear.Should().Contain("\u2021 No Range: at one end of the birth-time window this direction falls before birth.");
        firstYear.Count(l => l.StartsWith('\u2021')).Should().Be(1);

        lastYear.Should().Contain("| 2099-03-14 | 99.20 | D | Ascendant | cnj | Alcyone | 97\u00b046\u2032 | -3.1 | \u2014 \u2021 |");
        lastYear.Should().Contain("\u2021 No Range: at one end of the birth-time window this direction falls beyond the age limit.");
        lastYear.Count(l => l.StartsWith('\u2021')).Should().Be(1);
    }

    /// <summary>
    /// Person A at an uncertainty of 10 minutes has a turning row and rows without a Range: both footnotes print,
    /// the dagger's first, a blank line between them. Her last row, the converse Ascendant to Pollux at age 99.95,
    /// has no Months/min either and carries the double dagger in both cells.
    /// </summary>
    [Fact]
    public void Report_WithBothKindsOfRow_PrintsTheDaggersFootnoteFirst()
    {
        List<string> lines = Lines(Report(Hartwell(), window: 10));

        lines.Should().Contain("| 2047-09-16 | 99.95 | C | Ascendant | cnj | Pollux | 98\u00b030\u2032 | \u2014 \u2021 | \u2014 \u2021 |");
        lines.Should().Contain("| 1947-11-27 | 0.14 | D | Moon | cnj | Betelgeuse | 0\u00b008\u2032 | -0.6 | \u2014 \u2021 |");
        int afterTable = EndOfDirectionsTable(lines);
        lines[afterTable].Should().BeEmpty();
        lines[afterTable + 1].Should().Be(TurningFootnote);
        lines[afterTable + 2].Should().BeEmpty();
        lines[afterTable + 3].Should().Be(BothReasonsFootnote);
    }

    /// <summary>
    /// With no uncertainty set the report has no Range column: person A's one row without Months/min carries the
    /// double dagger, and the footnote speaks of that figure and of a minute from the recorded birth time. The
    /// Directions panel's list shows the same row and the same footnote.
    /// </summary>
    [Fact]
    public void Report_WithoutAWindow_MarksTheRowWithoutMonthsPerMinute()
    {
        const string row = "| 2047-09-16 | 99.95 | C | Ascendant | cnj | Pollux | 98\u00b030\u2032 | \u2014 \u2021 |";
        const string footnote = "\u2021 No Months/min: a minute from the recorded birth time this direction falls beyond the age limit.";
        PrimaryDirectionResult result = Directions(Hartwell(), window: 0);

        List<string> report = Lines(PrimaryDirectionMarkdownFormatter.ToMarkdown(result, GoldenFixtures.GoldenClock()));
        List<string> panel = Lines(PrimaryDirectionMarkdownFormatter.FormatTable(result));

        report.Should().Contain(row);
        report.Count(l => l == footnote).Should().Be(1);
        report.Should().NotContain(l => l.Contains("No Range", StringComparison.Ordinal));
        panel.Should().Contain(row);
        panel.Count(l => l == footnote).Should().Be(1);
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
