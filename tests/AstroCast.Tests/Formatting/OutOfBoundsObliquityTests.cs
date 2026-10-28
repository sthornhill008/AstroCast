using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Pins out-of-bounds (branch F7, 1.3.3): a declination is out of bounds when its absolute value exceeds the
/// chart's own true obliquity, <see cref="ChartResult.Obliquity"/>, and the Sun never is, since the bounds are its
/// own greatest declination. The Planetary Positions table's OOB cell, the Out-of-Bounds Planets list with its
/// Excess column, and the electional export's Natal Chart Summary all read it.
/// </summary>
/// <remarks>
/// <para>
/// Until 1.3.3 every chart was read against a constant, 23.4367°, the obliquity of about 2020: a body at 23.45° was
/// flagged in 1800, when the bounds were 23.467°, and the Sun itself was flagged near a solstice whenever the
/// obliquity exceeded the constant (foundations F5-1: the Sun 161 of 454 wrong flags in the 9,979-chart sweep).
/// </para>
/// <para>
/// The obliquities in the first theory are the IAU mean obliquity (Meeus, eq. 22.2) at 1800-06-20 and 2026-06-20,
/// as literals. The Sun's declinations and the true obliquities quoted are an independent Swiss Ephemeris build's
/// (pyswisseph 2.10.03, Moshier): at 1800-06-20 12:00 UT the Sun at +23.46207 against 23.46737; at 1949-12-22
/// 04:20 UT the Sun at −23.448387 against 23.448084, past it by 0.0003° through its ecliptic latitude (−0.0003°).
/// </para>
/// <para>
/// Lines are compared whole, without their terminators: the formatters write <see cref="Environment.NewLine"/>,
/// which differs between Windows and macOS. A row's OOB cell is read by finding the row by its first cell and
/// taking its last cell.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class OutOfBoundsObliquityTests
{
    private readonly ChartEngine _engine;

    /// <summary>Receives the shared ephemeris fixture and builds the engine.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public OutOfBoundsObliquityTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _engine = GoldenFixtures.CreateChartEngine(fixture);
    }

    /// <summary>
    /// A declination of 23.45°, north or south, is in bounds against the obliquity of 1800-06-20 (23.4652°) and out
    /// of bounds against that of 2026-06-20 (23.4358°).
    /// </summary>
    /// <param name="declination">The declination, degrees.</param>
    /// <param name="obliquity">The obliquity, degrees.</param>
    /// <param name="expected">Whether it is out of bounds.</param>
    [Theory]
    [InlineData(23.45, 23.4652, false)]
    [InlineData(-23.45, 23.4652, false)]
    [InlineData(23.45, 23.4358, true)]
    [InlineData(-23.45, 23.4358, true)]
    public void IsOutOfBounds_Declination_IsReadAgainstTheObliquityGiven(double declination, double obliquity, bool expected)
    {
        PositionFormatter.IsOutOfBounds(declination, obliquity).Should().Be(expected);
    }

    /// <summary>
    /// At a declination of −23.4400° against an obliquity of 23.4370°, Mars is out of bounds and the Sun is not.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="expected">Whether it is out of bounds.</param>
    [Theory]
    [InlineData(CelestialBodyId.Sun, false)]
    [InlineData(CelestialBodyId.Mars, true)]
    public void IsOutOfBounds_Position_TheSunIsNeverOutOfBounds(CelestialBodyId body, bool expected)
    {
        PositionFormatter.IsOutOfBounds(Position(body, -23.4400), 23.4370).Should().Be(expected);
    }

    /// <summary>
    /// A declination of 23.45° against the obliquity of the engine's chart for Greenwich: in bounds at 1800-06-20
    /// 12:00 UT (true obliquity 23.46737), out of bounds at 2026-06-20 12:00 UT.
    /// </summary>
    /// <param name="year">The year of the chart, cast on 20 June at 12:00 UT.</param>
    /// <param name="expected">Whether 23.45° is out of bounds.</param>
    [Theory]
    [InlineData(1800, false)]
    [InlineData(2026, true)]
    public void IsOutOfBounds_AgainstTheEnginesChart_ReadsTheChartsObliquity(int year, bool expected)
    {
        ChartResult chart = _engine.Calculate(Greenwich(new DateTime(year, 6, 20, 12, 0, 0)));

        PositionFormatter.IsOutOfBounds(23.45, chart.Obliquity).Should().Be(expected);
    }

    /// <summary>
    /// The Sun of 1800-06-20 12:00 UT at Greenwich, past the old constant and short of its own chart's obliquity, is
    /// not out of bounds: its OOB cell is empty and the Out-of-Bounds Planets list does not name it.
    /// </summary>
    [Fact]
    public void ToMarkdown_TheSunOf1800_IsNotOutOfBounds()
    {
        ChartResult chart = _engine.Calculate(Greenwich(new DateTime(1800, 6, 20, 12, 0, 0)));
        double sunDeclination = SunDeclination(chart);

        sunDeclination.Should().BeGreaterThan(23.4367);
        sunDeclination.Should().BeLessThan(chart.Obliquity);

        List<string> lines = Lines(ChartMarkdownFormatter.ToMarkdown(chart));

        LastCell(RowByFirstCell(lines, "## Planetary Positions", "Sun")).Should().BeEmpty();
        OutOfBoundsRows(lines).Should().NotContain(row => FirstCell(row) == "Sun");
    }

    /// <summary>
    /// The Sun of 1949-12-22 04:20 UT at Greenwich passes its own chart's obliquity, through its ecliptic latitude,
    /// and is still not out of bounds: the bounds are its own greatest declination.
    /// </summary>
    [Fact]
    public void ToMarkdown_TheSunPastTheObliquity_IsNotOutOfBounds()
    {
        ChartResult chart = _engine.Calculate(Greenwich(new DateTime(1949, 12, 22, 4, 20, 0)));

        Math.Abs(SunDeclination(chart)).Should().BeGreaterThan(chart.Obliquity);

        List<string> lines = Lines(ChartMarkdownFormatter.ToMarkdown(chart));

        LastCell(RowByFirstCell(lines, "## Planetary Positions", "Sun")).Should().BeEmpty();
        OutOfBoundsRows(lines).Should().NotContain(row => FirstCell(row) == "Sun");
    }

    /// <summary>
    /// A chart whose obliquity is 23.46°, with the Moon at +24.00° and Mars at +23.45°: the Moon alone is out of
    /// bounds, by 0.54°, and Mars, past the old constant, is not.
    /// </summary>
    [Fact]
    public void ToMarkdown_ConstructedChart_ReadsItsOwnObliquity()
    {
        List<string> lines = Lines(ChartMarkdownFormatter.ToMarkdown(ConstructedChart()));

        OutOfBoundsRows(lines).Should().Equal("| Moon | +24°00′00″ | 0.54° |");
        LastCell(RowByFirstCell(lines, "## Planetary Positions", "Moon")).Should().Be("OOB");
        LastCell(RowByFirstCell(lines, "## Planetary Positions", "Mars")).Should().BeEmpty();
    }

    /// <summary>
    /// The electional export's Natal Chart Summary reads the natal chart's own obliquity: with 23.46°, the Moon at
    /// +24.00° is out of bounds and Mars at +23.45° is not.
    /// </summary>
    [Fact]
    public void ElectionalToMarkdown_NatalChartSummary_ReadsTheNatalChartsObliquity()
    {
        ChartResult natal = ConstructedChart();
        ElectionalResult result = new()
        {
            Input = new ElectionalInput
            {
                NatalChart = natal,
                ElectionType = ElectionType.BusinessLaunch,
                ElectionGoal = "Launch a new software product.",
                RangeStart = new DateOnly(2026, 4, 20),
                RangeEnd = new DateOnly(2026, 4, 25),
                Latitude = 51.4769,
                Longitude = 0.0,
                LocationName = "Greenwich",
                TimeZone = "UTC",
                CandidateCount = 3,
            },
            NatalChart = natal,
            Candidates = [],
            RequestedCandidateCount = 3,
            SamplesWithoutPlanetaryHour = 0,
            DisqualifiedPeriods = [],
            VoidOfCoursePeriods = [],
            CalculatedAt = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc),
        };

        List<string> lines = Lines(ElectionalMarkdownFormatter.ToMarkdown(result));

        LastCell(RowByFirstCell(lines, "## Natal Chart Summary", "Moon")).Should().Be("OOB");
        LastCell(RowByFirstCell(lines, "## Natal Chart Summary", "Mars")).Should().BeEmpty();
    }

    /// <summary>
    /// A chart whose obliquity is 23.46°, with the twelve bodies, of which only the Moon (+24.00°) and Mars
    /// (+23.45°) carry a declination.
    /// </summary>
    /// <returns>The chart.</returns>
    private static ChartResult ConstructedChart() => new()
    {
        Input = Greenwich(new DateTime(2026, 6, 20, 12, 0, 0)),
        JulianDay = 2461212.0,
        Obliquity = 23.46,
        UtcDateTime = new DateTime(2026, 6, 20, 12, 0, 0, DateTimeKind.Utc),
        Positions =
        [
            Position(CelestialBodyId.Sun, null),
            Position(CelestialBodyId.Moon, 24.00),
            Position(CelestialBodyId.Mercury, null),
            Position(CelestialBodyId.Venus, null),
            Position(CelestialBodyId.Mars, 23.45),
            Position(CelestialBodyId.Jupiter, null),
            Position(CelestialBodyId.Saturn, null),
            Position(CelestialBodyId.Uranus, null),
            Position(CelestialBodyId.Neptune, null),
            Position(CelestialBodyId.Pluto, null),
            Position(CelestialBodyId.TrueNode, null),
            Position(CelestialBodyId.Chiron, null),
        ],
        Houses = new HouseData
        {
            Cusps = [.. Enumerable.Range(0, 12).Select(i => i * 30.0)],
            Ascendant = 0.0,
            Midheaven = 270.0,
            Armc = 0.0,
            Vertex = 0.0,
        },
        Aspects = [],
        Midpoints = [],
        MidpointActivations = [],
        Dignities = [],
        MoonCondition = null,
        ChartContext = null,
        FixedStarConjunctions = [],
        ResolvedUtcOffsetHours = 0.0,
        ResolvedOffsetLabel = "UTC (UTC+0)",
        EngineVersion = "Test 1.0.0",
        CalculatedAt = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc),
    };

    /// <summary>A position with the given declination, at a longitude of 0°, its name the body's.</summary>
    /// <param name="body">The body.</param>
    /// <param name="declination">The declination, degrees, or <see langword="null"/>.</param>
    /// <returns>The position.</returns>
    private static PlanetPosition Position(CelestialBodyId body, double? declination) => new()
    {
        Body = body,
        Name = body.ToString(),
        Longitude = 0.0,
        Latitude = 0.0,
        Distance = 1.0,
        SpeedInLongitude = 1.0,
        SpeedInLatitude = 0.0,
        SpeedInDistance = 0.0,
        Declination = declination,
    };

    /// <summary>A chart at Greenwich on UTC's clock, in Placidus.</summary>
    /// <param name="localDateTime">The wall clock, which on UTC's clock is the instant.</param>
    /// <returns>The input.</returns>
    private static ChartInput Greenwich(DateTime localDateTime) => new()
    {
        Name = "Greenwich",
        LocalDateTime = localDateTime,
        TimeZone = "UTC",
        Latitude = 51.4769,
        Longitude = 0.0,
        LocationName = "Greenwich",
    };

    /// <summary>The Sun's declination in a chart.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The declination, degrees.</returns>
    private static double SunDeclination(ChartResult chart) =>
        chart.Positions.Single(p => p.Body == CelestialBodyId.Sun).Declination
            ?? throw new InvalidOperationException("The engine's Sun has no declination.");

    /// <summary>
    /// The first row, after the given heading, whose first cell is the given text.
    /// </summary>
    /// <param name="lines">The export's lines.</param>
    /// <param name="heading">The heading of the section the row is in.</param>
    /// <param name="firstCell">The row's first cell.</param>
    /// <returns>The row.</returns>
    private static string RowByFirstCell(List<string> lines, string heading, string firstCell)
    {
        int start = lines.IndexOf(heading);
        start.Should().BeGreaterThanOrEqualTo(0, "the export has a section headed \"{0}\"", heading);

        return lines.Skip(start + 1).First(l => l.StartsWith('|') && FirstCell(l) == firstCell);
    }

    /// <summary>
    /// The data rows of the Out-of-Bounds Planets list: the table's rows after its header and separator, up to the
    /// next heading. A data row begins "| " and the separator "|-".
    /// </summary>
    /// <param name="lines">The export's lines.</param>
    /// <returns>The rows, none when no planet is out of bounds.</returns>
    private static List<string> OutOfBoundsRows(List<string> lines)
    {
        int start = lines.IndexOf("### Out-of-Bounds Planets");
        start.Should().BeGreaterThanOrEqualTo(0, "the chart export lists its out-of-bounds planets");

        int next = lines.FindIndex(start + 1, l => l.StartsWith('#'));
        int end = next < 0 ? lines.Count : next;

        return
        [
            .. lines.GetRange(start + 1, end - start - 1)
                .Where(l => l.StartsWith("| ", StringComparison.Ordinal) && l is not "| Planet | Declination | Excess |"),
        ];
    }

    /// <summary>A table row's cells, trimmed, without the empty text outside its outer bars.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The cells.</returns>
    private static string[] Cells(string row)
    {
        string[] parts = row.Split('|');
        return [.. parts[1..^1].Select(c => c.Trim())];
    }

    /// <summary>A table row's first cell.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The cell, trimmed.</returns>
    private static string FirstCell(string row) => Cells(row)[0];

    /// <summary>A table row's last cell.</summary>
    /// <param name="row">The row.</param>
    /// <returns>The cell, trimmed.</returns>
    private static string LastCell(string row) => Cells(row)[^1];

    /// <summary>The export's lines, without their terminators.</summary>
    /// <param name="markdown">The export.</param>
    /// <returns>Its lines.</returns>
    private static List<string> Lines(string markdown) => [.. markdown.Split('\n').Select(l => l.TrimEnd('\r'))];
}
