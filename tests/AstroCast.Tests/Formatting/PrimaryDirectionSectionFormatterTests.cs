using System.Text;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// <see cref="PrimaryDirectionSectionFormatter"/> (branch PrimaryExport, 1.5.0): the header lines by their
/// rules, the table's columns and cells, and the none sentence, on synthetic charts and hits.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PrimaryDirectionSectionFormatterTests
{
    /// <summary>The header lines print in order, directly after the heading and a blank, with no blank between them.</summary>
    [Fact]
    public void AppendSection_PrintsTheHeaderLinesInOrderWithNoBlankBetween()
    {
        List<string> lines = Section(Result(leftOut: 0, sourceNotes: "Birth Certificate"), [], "Sub", "None.");

        lines[0].Should().Be("## Primary Directions");
        lines[1].Should().BeEmpty();
        lines[2].Should().Be("**Method:** Placidus semi-arc, in zodiaco; direct and converse (traditional)");
        lines[3].Should().Be("**Key:** Naibod (0°59′08″ a year)");
        lines[4].Should().Be("**Significators:** Ascendant, Midheaven, Sun, Moon, Part of Fortune; the Descendant and IC by the conjunctions of bodies and stars");
        lines[5].Should().Be("**Promissors:** the planets and Chiron with the Ptolemaic aspects, the node by conjunction and opposition; their antiscia and the 55 fixed stars by conjunction");
        lines[6].Should().BeEmpty();
        lines[7].Should().Be("### Sub");
    }

    /// <summary>The Key line names the setting's key by its display name.</summary>
    /// <param name="key">The key.</param>
    /// <param name="expected">The line.</param>
    [Theory]
    [InlineData(PrimaryDirectionKey.Naibod, "**Key:** Naibod (0°59′08″ a year)")]
    [InlineData(PrimaryDirectionKey.Ptolemy, "**Key:** Ptolemy (1° a year)")]
    [InlineData(PrimaryDirectionKey.SolarArcRightAscension, "**Key:** True solar arc in right ascension")]
    public void Key_PrintsTheKeysDisplayName(PrimaryDirectionKey key, string expected)
    {
        List<string> lines = Section(Result(settings: new PrimaryDirectionSettings { Key = key }), [], "Sub", "None.");

        lines.Should().Contain(expected);
    }

    /// <summary>With the planets as significators the line adds Mercury to Pluto after the Part of Fortune.</summary>
    [Fact]
    public void Significators_WithPlanets_AddMercuryToPluto()
    {
        List<string> lines = Section(Result(settings: new PrimaryDirectionSettings { PlanetsAsSignificators = true }), [], "Sub", "None.");

        lines.Should().Contain("**Significators:** Ascendant, Midheaven, Sun, Moon, Part of Fortune, Mercury to Pluto; the Descendant and IC by the conjunctions of bodies and stars");
    }

    /// <summary>Above one, the Left out line is plural and names the latitude F4 with N.</summary>
    [Fact]
    public void LeftOut_AboveOne_PrintsThePluralLine()
    {
        List<string> lines = Section(Result(leftOut: 60), [], "Sub", "None.");

        lines.Should().Contain("**Left out:** 60 directions whose point never rises or sets at 40.7128° N (no semi-arc)");
    }

    /// <summary>One direction left out reads in the singular.</summary>
    [Fact]
    public void LeftOut_One_IsSingular()
    {
        List<string> lines = Section(Result(leftOut: 1), [], "Sub", "None.");

        lines.Should().Contain("**Left out:** 1 direction whose point never rises or sets at 40.7128° N (no semi-arc)");
    }

    /// <summary>Nothing left out prints no line.</summary>
    [Fact]
    public void LeftOut_Zero_PrintsNoLine()
    {
        List<string> lines = Section(Result(leftOut: 0), [], "Sub", "None.");

        lines.Should().NotContain(l => l.StartsWith("**Left out:**", StringComparison.Ordinal));
    }

    /// <summary>A southern latitude prints its magnitude with S.</summary>
    [Fact]
    public void LeftOut_SouthernLatitude_PrintsS()
    {
        List<string> lines = Section(Result(leftOut: 12, latitude: -33.8688), [], "Sub", "None.");

        lines.Should().Contain("**Left out:** 12 directions whose point never rises or sets at 33.8688° S (no semi-arc)");
    }

    /// <summary>
    /// The Birth time line prints when the source is null, blank or "Unknown" in any case, naming the state,
    /// and not when a source is given.
    /// </summary>
    /// <param name="sourceNotes">The chart's birth-time source.</param>
    /// <param name="expectedState">The words after "Birth Time Source", or null for no line.</param>
    [Theory]
    [InlineData(null, "not specified")]
    [InlineData("", "not specified")]
    [InlineData("   ", "not specified")]
    [InlineData("Unknown", "Unknown")]
    [InlineData("UNKNOWN", "Unknown")]
    [InlineData("Birth Certificate", null)]
    public void BirthTime_Line_FollowsTheSource(string? sourceNotes, string? expectedState)
    {
        List<string> lines = Section(Result(sourceNotes: sourceNotes), [], "Sub", "None.");

        if (expectedState is null)
        {
            lines.Should().NotContain(l => l.StartsWith("**Birth time:**", StringComparison.Ordinal));
        }
        else
        {
            lines.Should().Contain(
                $"**Birth time:** unconfirmed (Birth Time Source {expectedState}): every date below moves with the birth time; see Months/min");
        }
    }

    /// <summary>With an uncertainty the table has a Range column naming it, and its separator cell.</summary>
    [Fact]
    public void Table_RangeColumn_WhenUncertaintySet()
    {
        List<string> lines = Section(
            Result(settings: new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = 4 }),
            [Hit()], "Sub", "None.");

        lines.Should().Contain("| Date (UTC) | Age | D/C | Significator | Aspect | Promissor | Arc | Months/min | Range (±4 min) |");
        lines.Should().Contain("|------------|-----|-----|--------------|--------|-----------|-----|------------|----------------|");
    }

    /// <summary>Without an uncertainty the table ends at Months/min.</summary>
    [Fact]
    public void Table_NoRangeColumn_WhenUncertaintyZero()
    {
        List<string> lines = Section(Result(), [Hit()], "Sub", "None.");

        lines.Should().Contain("| Date (UTC) | Age | D/C | Significator | Aspect | Promissor | Arc | Months/min |");
        lines.Should().Contain("|------------|-----|-----|--------------|--------|-----------|-----|------------|");
        lines.Should().NotContain(l => l.Contains("Range", StringComparison.Ordinal));
    }

    /// <summary>A null Months/min and a null Range print an em dash each.</summary>
    [Fact]
    public void Table_NullMonthsAndRange_PrintDashes()
    {
        PrimaryDirectionHit hit = Hit(monthsPerMinute: null, rangeStart: null, rangeEnd: null);

        string row = PrimaryDirectionSectionFormatter.FormatRow(hit, withRange: true);

        row.Should().EndWith("| 16°00′ | — | — |");
    }

    /// <summary>The aspect label by aspect and the angle's sign: sin for the positive, dex for the negative.</summary>
    /// <param name="aspect">The aspect.</param>
    /// <param name="angle">The signed angle.</param>
    /// <param name="expected">The label.</param>
    [Theory]
    [InlineData(PrimaryDirectionAspect.Conjunction, 0.0, "cnj")]
    [InlineData(PrimaryDirectionAspect.Opposition, 180.0, "opp")]
    [InlineData(PrimaryDirectionAspect.Sextile, 60.0, "sxt sin")]
    [InlineData(PrimaryDirectionAspect.Sextile, -60.0, "sxt dex")]
    [InlineData(PrimaryDirectionAspect.Square, 90.0, "sqr sin")]
    [InlineData(PrimaryDirectionAspect.Square, -90.0, "sqr dex")]
    [InlineData(PrimaryDirectionAspect.Trine, 120.0, "tri sin")]
    [InlineData(PrimaryDirectionAspect.Trine, -120.0, "tri dex")]
    [InlineData(PrimaryDirectionAspect.Antiscion, 0.0, "antiscion")]
    [InlineData(PrimaryDirectionAspect.ContraAntiscion, 0.0, "contra-antiscion")]
    public void AspectLabel_ByAspectAndSign(PrimaryDirectionAspect aspect, double angle, string expected)
    {
        PrimaryDirectionSectionFormatter.AspectLabel(Hit(aspect: aspect, angle: angle)).Should().Be(expected);
    }

    /// <summary>
    /// Months per minute print signed to one decimal, "+0.0;-0.0;0.0": a figure that rounds to zero prints 0.0
    /// without a sign, the format's third section.
    /// </summary>
    /// <param name="months">The figure.</param>
    /// <param name="expected">The cell.</param>
    [Theory]
    [InlineData(-2.9539, "-3.0")]
    [InlineData(2.136, "+2.1")]
    [InlineData(0.04, "0.0")]
    public void MonthsPerMinute_Format(double months, string expected)
    {
        PrimaryDirectionSectionFormatter.FormatMonthsPerMinute(months).Should().Be(expected);
    }

    /// <summary>A row prints every cell: the UT date, the age to two decimals, D, the names, the label, the arc, the months and the range.</summary>
    [Fact]
    public void Row_PrintsEveryCell()
    {
        string row = PrimaryDirectionSectionFormatter.FormatRow(Hit(), withRange: true);

        row.Should().Be("| 2016-03-26 | 16.23 | D | Midheaven | cnj | Moon | 16°00′ | -3.0 | 2015-04-02 to 2017-03-21 |");
    }

    /// <summary>Without a hit the sub-heading prints, then the none sentence in the table's place.</summary>
    [Fact]
    public void NoHits_PrintsTheSubheadingAndTheNoneSentence()
    {
        List<string> lines = Section(Result(), [], "Primary Directions, Birth to Age 100", "No primary direction falls before age 100.");

        int heading = lines.IndexOf("### Primary Directions, Birth to Age 100");
        heading.Should().BePositive();
        lines[heading + 1].Should().BeEmpty();
        lines[heading + 2].Should().Be("No primary direction falls before age 100.");
        lines.Should().NotContain(l => l.StartsWith("| Date (UTC)", StringComparison.Ordinal));
    }

    #region The turning row (branch DirectionNotes, 1.7.2)

    /// <summary>The dagger's footnote when an uncertainty is set.</summary>
    private const string TurningFootnoteWithWindow =
        "\u2020 This direction's date turns within the birth-time window (its promissor crosses the horizon there). Its Range does not bound its date and its Months/min is not a rate, so the row cannot be used to correct the birth time.";

    /// <summary>The dagger's footnote when no uncertainty is set.</summary>
    private const string TurningFootnoteWithoutWindow =
        "\u2020 This direction's date turns within a minute of the recorded birth time (its promissor crosses the horizon there). Its Range does not bound its date and its Months/min is not a rate, so the row cannot be used to correct the birth time.";

    /// <summary>
    /// A turning row carries a dagger after the value in its Months/min cell and in its Range cell, a space
    /// between; every other cell is the row's own (finding A5: the values stay, the row is marked).
    /// </summary>
    [Fact]
    public void Row_Turning_CarriesADaggerAfterEachFigure()
    {
        PrimaryDirectionHit hit = Hit(isTurning: true);

        PrimaryDirectionSectionFormatter.FormatRow(hit, withRange: true)
            .Should().Be("| 2016-03-26 | 16.23 | D | Midheaven | cnj | Moon | 16°00′ | -3.0 \u2020 | 2015-04-02 to 2017-03-21 \u2020 |");
        PrimaryDirectionSectionFormatter.FormatRow(hit, withRange: false)
            .Should().Be("| 2016-03-26 | 16.23 | D | Midheaven | cnj | Moon | 16°00′ | -3.0 \u2020 |");
    }

    /// <summary>
    /// With a turning row among the rows one footnote follows the table, after a blank line, however many rows
    /// carry the dagger; with an uncertainty set it speaks of the birth-time window.
    /// </summary>
    [Fact]
    public void Table_WithTurningRows_PrintsOneFootnoteAfterABlankLine()
    {
        List<string> lines = Section(
            Result(settings: new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = 4 }),
            [Hit(), Hit(isTurning: true), Hit(isTurning: true)], "Sub", "None.");

        int lastRow = lines.FindLastIndex(l => l.StartsWith("| 2016-03-26", StringComparison.Ordinal));
        lines[lastRow + 1].Should().BeEmpty();
        lines[lastRow + 2].Should().Be(TurningFootnoteWithWindow);
        lines.Count(l => l.StartsWith('\u2020')).Should().Be(1);
        lines.Count(l => l.EndsWith("\u2020 |", StringComparison.Ordinal)).Should().Be(2);
    }

    /// <summary>Without an uncertainty the footnote speaks of a minute of the recorded birth time.</summary>
    [Fact]
    public void Table_TurningRowWithoutAWindow_FootnoteSaysWithinAMinute()
    {
        List<string> lines = Section(Result(), [Hit(isTurning: true)], "Sub", "None.");

        lines.Should().Contain(TurningFootnoteWithoutWindow);
        lines.Should().NotContain(TurningFootnoteWithWindow);
    }

    /// <summary>Without a turning row the table has no dagger and no footnote.</summary>
    [Fact]
    public void Table_NoTurningRow_HasNoDaggerAndNoFootnote()
    {
        List<string> lines = Section(
            Result(settings: new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = 4 }), [Hit(), Hit()], "Sub", "None.");

        lines.Should().NotContain(l => l.Contains('\u2020'));
    }

    #endregion

    #region The missing figure (branch DirectionNotes, 1.7.2)

    /// <summary>
    /// A row whose Range is missing for a recorded reason prints the em dash and a double dagger, and so does its
    /// Months/min cell when that figure is missing for a recorded reason; a figure that is there is untouched.
    /// </summary>
    [Fact]
    public void Row_MissingFigureWithAReason_CarriesTheDoubleDagger()
    {
        PrimaryDirectionHit rangeOnly = Hit(noRange: PrimaryDirectionGapReasons.BeforeBirth);
        PrimaryDirectionHit both = Hit(
            monthsPerMinute: null, noMonths: PrimaryDirectionGapReasons.BeyondAgeLimit, noRange: PrimaryDirectionGapReasons.BeyondAgeLimit);

        PrimaryDirectionSectionFormatter.FormatRow(rangeOnly, withRange: true).Should().EndWith("| 16°00′ | -3.0 | — \u2021 |");
        PrimaryDirectionSectionFormatter.FormatRow(both, withRange: true).Should().EndWith("| 16°00′ | — \u2021 | — \u2021 |");
        PrimaryDirectionSectionFormatter.FormatRow(both, withRange: false).Should().EndWith("| 16°00′ | — \u2021 |");
    }

    /// <summary>
    /// The double dagger's footnote names the reasons that occur among the rows and no other, in plain words joined
    /// with "or".
    /// </summary>
    /// <param name="first">One row's reasons.</param>
    /// <param name="second">Another row's reasons.</param>
    /// <param name="expected">The words after "this direction".</param>
    [Theory]
    [InlineData(PrimaryDirectionGapReasons.BeforeBirth, PrimaryDirectionGapReasons.BeforeBirth, "falls before birth")]
    [InlineData(PrimaryDirectionGapReasons.BeyondAgeLimit, PrimaryDirectionGapReasons.BeyondAgeLimit, "falls beyond the age limit")]
    [InlineData(PrimaryDirectionGapReasons.BeforeBirth, PrimaryDirectionGapReasons.BeyondAgeLimit, "falls before birth or beyond the age limit")]
    [InlineData(PrimaryDirectionGapReasons.SectChange, PrimaryDirectionGapReasons.SectChange, "is to a Part of Fortune whose formula changes between day and night")]
    [InlineData(PrimaryDirectionGapReasons.BeyondAgeLimit, PrimaryDirectionGapReasons.SectChange, "falls beyond the age limit or is to a Part of Fortune whose formula changes between day and night")]
    [InlineData(PrimaryDirectionGapReasons.NoSemiArc, PrimaryDirectionGapReasons.NoSemiArc, "has no date")]
    [InlineData(PrimaryDirectionGapReasons.BeforeBirth | PrimaryDirectionGapReasons.BeyondAgeLimit, PrimaryDirectionGapReasons.NoSemiArc, "falls before birth or beyond the age limit or has no date")]
    public void Table_MissingRanges_FootnoteNamesTheReasonsThatOccur(
        PrimaryDirectionGapReasons first, PrimaryDirectionGapReasons second, string expected)
    {
        List<string> lines = Section(
            Result(settings: new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = 4 }),
            [Hit(), Hit(noRange: first), Hit(noRange: second)], "Sub", "None.");

        string footnote = $"\u2021 No Range: at one end of the birth-time window this direction {expected}.";
        int lastRow = lines.FindLastIndex(l => l.StartsWith("| 2016-03-26", StringComparison.Ordinal));
        lines[lastRow + 1].Should().BeEmpty();
        lines[lastRow + 2].Should().Be(footnote);
        lines.Count(l => l.StartsWith('\u2021')).Should().Be(1);
    }

    /// <summary>When a row is turning and another has no range, the dagger's footnote comes first, a blank line between the two.</summary>
    [Fact]
    public void Table_TurningAndMissing_PrintsTheDaggersFootnoteFirst()
    {
        List<string> lines = Section(
            Result(settings: new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = 4 }),
            [Hit(isTurning: true), Hit(noRange: PrimaryDirectionGapReasons.BeforeBirth)], "Sub", "None.");

        int dagger = lines.IndexOf(TurningFootnoteWithWindow);
        dagger.Should().BePositive();
        lines[dagger + 1].Should().BeEmpty();
        lines[dagger + 2].Should().Be("\u2021 No Range: at one end of the birth-time window this direction falls before birth.");
    }

    /// <summary>
    /// With no uncertainty set there is no Range column: a row without months per minute carries the double dagger,
    /// and the footnote speaks of the figure that is missing and of a minute from the recorded birth time.
    /// </summary>
    [Fact]
    public void Table_MissingMonthsWithoutAWindow_FootnoteSpeaksOfMonthsPerMinute()
    {
        List<string> lines = Section(
            Result(), [Hit(), Hit(monthsPerMinute: null, noMonths: PrimaryDirectionGapReasons.BeyondAgeLimit)], "Sub", "None.");

        lines.Should().Contain("\u2021 No Months/min: a minute from the recorded birth time this direction falls beyond the age limit.");
        lines.Should().NotContain(l => l.Contains("No Range", StringComparison.Ordinal));
    }

    /// <summary>
    /// With an uncertainty set, a row without months per minute takes the same mark and the same footnote as the
    /// rows without a range, and its reason joins theirs.
    /// </summary>
    [Fact]
    public void Table_MissingMonthsWithAWindow_IsCoveredByTheRangesFootnote()
    {
        List<string> lines = Section(
            Result(settings: new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = 4 }),
            [
                Hit(noRange: PrimaryDirectionGapReasons.BeforeBirth),
                Hit(monthsPerMinute: null, noMonths: PrimaryDirectionGapReasons.BeyondAgeLimit, noRange: PrimaryDirectionGapReasons.BeyondAgeLimit),
            ],
            "Sub", "None.");

        lines.Should().Contain("\u2021 No Range: at one end of the birth-time window this direction falls before birth or beyond the age limit.");
        lines.Count(l => l.StartsWith('\u2021')).Should().Be(1);
    }

    /// <summary>A missing figure with no recorded reason prints the em dash alone, and no footnote follows.</summary>
    [Fact]
    public void Table_MissingFigureWithoutAReason_HasNoMarkAndNoFootnote()
    {
        List<string> lines = Section(
            Result(settings: new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = 4 }),
            [Hit(monthsPerMinute: null)], "Sub", "None.");

        lines.Should().NotContain(l => l.Contains('\u2021'));
    }

    #endregion

    #region Helpers

    /// <summary>The section's lines for a result and its hits.</summary>
    private static List<string> Section(
        PrimaryDirectionResult result, IReadOnlyList<PrimaryDirectionHit> hits, string subheading, string noneSentence)
    {
        StringBuilder sb = new();
        PrimaryDirectionSectionFormatter.AppendSection(sb, result, hits, subheading, noneSentence);
        return [.. sb.ToString().Split('\n').Select(l => l.TrimEnd('\r', ' '))];
    }

    /// <summary>A result over a minimal chart at a latitude with a birth-time source.</summary>
    private static PrimaryDirectionResult Result(
        int leftOut = 0, string? sourceNotes = null, double latitude = 40.7128, PrimaryDirectionSettings? settings = null) => new()
    {
        NatalChart = MinimalChart(latitude, sourceNotes),
        Settings = settings ?? PrimaryDirectionSettings.Default,
        Hits = [],
        LeftOutCount = leftOut,
    };

    /// <summary>The direct Moon conjunct the Midheaven of J2000 NYC, with Tier B's figures, as a synthetic hit.</summary>
    private static PrimaryDirectionHit Hit(
        PrimaryDirectionAspect aspect = PrimaryDirectionAspect.Conjunction,
        double angle = 0.0,
        double? monthsPerMinute = -2.9539,
        DateTime? rangeStart = null,
        DateTime? rangeEnd = null,
        bool isTurning = false,
        PrimaryDirectionGapReasons noMonths = PrimaryDirectionGapReasons.None,
        PrimaryDirectionGapReasons noRange = PrimaryDirectionGapReasons.None) => new()
    {
        IsTurning = isTurning,
        NoMonthsPerMinuteReasons = noMonths,
        NoRangeReasons = noRange,
        Significator = "Midheaven",
        Promissor = "Moon",
        PromissorKind = PrimaryDirectionPromissorKind.Body,
        Aspect = aspect,
        AspectAngle = angle,
        Motion = PrimaryDirectionMotion.Direct,
        Arc = 16.001096,
        Years = 16.234098,
        JulianDayUt = 2457474.377752,
        InstantUtc = new DateTime(2016, 3, 26, 21, 3, 58, DateTimeKind.Utc),
        MonthsPerMinute = monthsPerMinute,
        RangeStartUtc = rangeStart ?? (monthsPerMinute is null || noRange != PrimaryDirectionGapReasons.None ? null : new DateTime(2015, 4, 2, 5, 50, 20, DateTimeKind.Utc)),
        RangeEndUtc = rangeEnd ?? (monthsPerMinute is null || noRange != PrimaryDirectionGapReasons.None ? null : new DateTime(2017, 3, 21, 12, 18, 15, DateTimeKind.Utc)),
    };

    /// <summary>A chart with only what the section reads: its latitude and its birth-time source.</summary>
    private static ChartResult MinimalChart(double latitude, string? sourceNotes) => new()
    {
        Input = new ChartInput
        {
            Name = "Section Fixture",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = latitude,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
            SourceNotes = sourceNotes,
        },
        JulianDay = 2451545.0,
        Obliquity = 23.4367,
        UtcDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        Positions = [],
        Houses = new HouseData { Cusps = [], Ascendant = 0.0, Midheaven = 270.0, Armc = 270.0, Vertex = 180.0 },
        Aspects = [],
        Midpoints = [],
        MidpointActivations = [],
        Dignities = [],
        MoonCondition = null,
        ChartContext = null,
        FixedStarConjunctions = [],
        ResolvedUtcOffsetHours = -5.0,
        ResolvedOffsetLabel = "EST (UTC−5)",
        EngineVersion = "AstroCast 1.5.0 (SwissEph 2.10.03)",
        CalculatedAt = DateTime.UtcNow,
    };

    #endregion

    #region Hard breaks (branch ExportText, 1.7.2)

    /// <summary>
    /// The section's header lines each end with two spaces, Markdown's hard break, but the last (finding N8).
    /// Until 1.7.2 none did, so a Markdown reader ran Method, Key, Significators and Promissors into one
    /// paragraph.
    /// </summary>
    [Fact]
    public void AppendSection_BreaksEveryHeaderLineButTheLast()
    {
        StringBuilder sb = new();
        PrimaryDirectionSectionFormatter.AppendSection(sb, Result(leftOut: 3, sourceNotes: null), [], "Sub", "None.");
        string[] lines = [.. sb.ToString().Split('\n').Select(l => l.TrimEnd('\r'))];

        string[] header = [.. lines.Skip(2).TakeWhile(l => l.Length > 0)];

        header.Should().HaveCount(6, "Method, Key, Significators, Promissors, Left out and Birth time");
        header[0].Should().Be(PrimaryDirectionSectionFormatter.MethodLine + "  ");
        header[..^1].Should().OnlyContain(l => l.EndsWith("  ", StringComparison.Ordinal));
        header[^1].Should().StartWith("**Birth time:**").And.NotEndWith(" ");
    }

    #endregion
}
