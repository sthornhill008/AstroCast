using System.Globalization;
using System.Runtime.CompilerServices;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services.Davison;

/// <summary>
/// <see cref="DavisonService"/> (branch Davison, 1.7.0): the midpoint moment (the mean Julian Day) and place (the
/// mean latitude, the mean longitude by the shorter arc), the cast at them in chart A's house system, and the
/// chart's type, name, place, offset and switch. The pairs' truth is Claude's <c>davison_truth.py</c> on
/// pyswisseph 2.10.03, its rows in <c>TierB/davison_pairs.tsv</c> beside this file: the midpoint instant to the
/// second, the place to 1e-6°, the Sun, Moon, Ascendant and Midheaven to 1e-6°.
/// </summary>
/// <param name="fixture">The shared ephemeris fixture.</param>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class DavisonServiceTests(EphemerisFixture fixture)
{
    /// <summary>Tolerance on a longitude of the truth, in degrees: the lists print six decimals.</summary>
    private const double LongitudeTolerance = 1e-4;

    /// <summary>New York and Tokyo cross the antimeridian the short way: the midpoint is in the Pacific, not the Mediterranean.</summary>
    [Fact]
    public void MidpointLongitude_NewYorkAndTokyo_TakesTheShortArc()
    {
        DavisonService.MidpointLongitude(-74.006, 139.6917).Should().BeApproximately(-147.15715, 1e-9);
    }

    /// <summary>London and Sydney are under 180° apart, so the short arc's midpoint is the plain mean.</summary>
    [Fact]
    public void MidpointLongitude_LondonAndSydney_EqualsThePlainMean()
    {
        DavisonService.MidpointLongitude(-0.1278, 151.2093).Should().BeApproximately(75.54075, 1e-9);
    }

    /// <summary>Two charts at one place have that place as their midpoint.</summary>
    [Fact]
    public void MidpointLongitude_SamePlace_IsThePlace()
    {
        DavisonService.MidpointLongitude(18.9553, 18.9553).Should().BeApproximately(18.9553, 1e-9);
    }

    /// <summary>Two places exactly 180° apart have no shorter arc: the midpoint is the eastward half from A.</summary>
    /// <param name="lonA">Chart A's longitude.</param>
    /// <param name="lonB">Chart B's longitude.</param>
    /// <param name="expected">The midpoint.</param>
    [Theory]
    [InlineData(170.0, -10.0, -100.0)]
    [InlineData(-10.0, 170.0, 80.0)]
    public void MidpointLongitude_ExactlyOpposite_TakesTheEastwardHalf(double lonA, double lonB, double expected)
    {
        DavisonService.MidpointLongitude(lonA, lonB).Should().BeApproximately(expected, 1e-9);
    }

    /// <summary>The midpoint moment is the mean of the two Julian Days: 23:00 UT and 01:00 UT the next day give 00:00 UT.</summary>
    [Fact]
    public void MidpointJulianDay_IsTheMean()
    {
        double jdA = JulianDayUtility.InstantToJulianDay(new DateTime(2000, 1, 1, 23, 0, 0, DateTimeKind.Utc));
        double jdB = JulianDayUtility.InstantToJulianDay(new DateTime(2000, 1, 2, 1, 0, 0, DateTimeKind.Utc));

        double mid = DavisonService.MidpointJulianDay(jdA, jdB);

        mid.Should().BeApproximately(2451545.5, 1e-9);
        JulianDayUtility.JulianDayToInstant(mid).Should().Be(new DateTime(2000, 1, 2, 0, 0, 0, DateTimeKind.Utc));
    }

    /// <summary>The goldens' pair, J2000 NYC and the Secondary fixture, against the truth's "golden" row.</summary>
    [Fact]
    public void Calculate_GoldenPair_MatchesTheTruth()
    {
        TruthRow truth = Truth("golden");
        DavisonResult result = Service().Calculate(J2000Nyc(), SecondaryNatal(), includeMinorPoints: false);

        result.MidpointJulianDay.Should().BeApproximately(2446149.1458333, 1e-7);
        result.MidpointUtc.Should().Be(new DateTime(1985, 3, 24, 15, 30, 0, DateTimeKind.Utc));
        result.MidpointUtc.Kind.Should().Be(DateTimeKind.Utc);
        result.MidpointLatitude.Should().BeApproximately(37.3825, 1e-6);
        result.MidpointLongitude.Should().BeApproximately(-96.12485, 1e-6);
        AssertChartMatches(result, truth);
    }

    /// <summary>New York 2000 and Tokyo 1990, across the antimeridian, against the truth's "antimeridian" row.</summary>
    [Fact]
    public void Calculate_AntimeridianPair_MatchesTheTruth()
    {
        TruthRow truth = Truth("antimeridian");
        ChartResult tokyo = Engine().Calculate(new ChartInput
        {
            Name = "Tokyo 1990",
            LocalDateTime = new DateTime(1990, 6, 21, 10, 0, 0),
            TimeZone = "UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = 35.6762,
            Longitude = 139.6917,
            LocationName = "Tokyo",
            EventType = "Birth",
        });

        DavisonResult result = Service().Calculate(J2000Nyc(), tokyo, includeMinorPoints: false);

        result.MidpointJulianDay.Should().BeApproximately(2449804.4583333, 1e-7);
        result.MidpointUtc.Should().Be(new DateTime(1995, 3, 27, 23, 0, 0, DateTimeKind.Utc));
        result.MidpointLatitude.Should().BeApproximately(38.1945, 1e-6);
        result.MidpointLongitude.Should().BeApproximately(-147.15715, 1e-6);
        AssertChartMatches(result, truth);
    }

    /// <summary>N1 (1300) and the same place a century on: the midpoint is 1350-03-16 00:00 UT, on the Julian calendar.</summary>
    [Fact]
    public void Calculate_JulianEraPair_IsLabelledJulian()
    {
        TruthRow truth = Truth("julian");
        ChartResult n1 = Engine().Calculate(GoldenFixtures.N1Input());
        ChartResult aCenturyOn = Engine().Calculate(new ChartInput
        {
            Name = "N1 a century on",
            LocalDateTime = new DateTime(1400, 3, 15, 12, 0, 0),
            TimeZone = "Etc/UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = 51.4779,
            Longitude = -0.0015,
            LocationName = "Greenwich",
            EventType = "Birth",
        });

        DavisonResult result = Service().Calculate(n1, aCenturyOn, includeMinorPoints: false);

        result.MidpointJulianDay.Should().BeApproximately(2214219.5, 1e-7);
        CalendarDateFormatter.IsJulian(result.MidpointUtc).Should().BeTrue();
        CalendarDateFormatter.DateTime(result.MidpointUtc).Should().Be("1350-03-16 00:00:00");
        AssertChartMatches(result, truth);
    }

    /// <summary>The Davison chart takes chart A's house system, not chart B's and not the composite's agree-or-Placidus rule.</summary>
    [Fact]
    public void Calculate_UsesChartAsHouseSystem()
    {
        ChartResult tromso = Engine().Calculate(GoldenFixtures.TromsoInput());

        DavisonResult result = Service().Calculate(tromso, SecondaryNatal(), includeMinorPoints: false);

        result.DavisonChart.Input.HouseSystem.Should().Be(HouseSystem.Equal);
        result.DavisonChart.HouseSystemName.Should().Be(HouseSystem.Equal.DisplayName, "the engine names the system as the exports print it");
    }

    /// <summary>The chart's type, name, place and offset, and no source notes.</summary>
    [Fact]
    public void Calculate_TypeNamePlaceAndOffset()
    {
        DavisonResult result = Service().Calculate(J2000Nyc(), SecondaryNatal(), includeMinorPoints: false);

        ChartInput input = result.DavisonChart.Input;
        input.EventType.Should().Be("Davison");
        input.Name.Should().Be("Davison: J2000 NYC Fixture & Secondary Fixture");
        input.LocationName.Should().Be("Midpoint of New York City & Los Angeles");
        result.MidpointLocationDescription.Should().Be("Midpoint of New York City & Los Angeles");
        input.UseExplicitOffset.Should().BeTrue();
        input.UtcOffsetHours.Should().Be(0.0);
        input.SourceNotes.Should().BeNull();
        input.Gender.Should().Be(Gender.Unspecified);
        result.DavisonChart.ResolvedUtcOffsetHours.Should().Be(0.0);
        result.EngineVersion.Should().Be(result.DavisonChart.EngineVersion);
    }

    /// <summary>The switch alone decides the five: on with neither source carrying them, off with both carrying them.</summary>
    /// <param name="on">The switch.</param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Calculate_TheSwitchDecidesTheFive(bool on)
    {
        ChartResult chartA = Engine().Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: !on));
        ChartResult chartB = Engine().Calculate(GoldenFixtures.SecondaryNatalInput(includeMinorPoints: !on));
        chartA.MinorPoints.Count.Should().Be(on ? 0 : 5, "the sources carry the opposite of the switch");

        DavisonResult result = Service().Calculate(chartA, chartB, includeMinorPoints: on);

        result.DavisonChart.Input.IncludeMinorPoints.Should().Be(on);
        result.DavisonChart.MinorPoints.Count.Should().Be(on ? 5 : 0);
    }

    /// <summary>A horary chart as a source gives a Davison chart: the type is "Davison" and no horary perfection is analysed.</summary>
    [Fact]
    public void Calculate_HorarySource_GivesADavisonChart()
    {
        ChartResult horary = Engine().Calculate(new ChartInput
        {
            Name = "A question",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Horary",
            HoraryQuestion = "Will the Davison chart cast?",
        });

        DavisonResult result = Service().Calculate(horary, SecondaryNatal(), includeMinorPoints: false);

        result.DavisonChart.Input.EventType.Should().Be("Davison");
        result.DavisonChart.Input.HoraryQuestion.Should().BeNull();
        result.DavisonChart.HoraryPerfection.Should().BeNull();
        result.ChartA.Should().BeSameAs(horary);
    }

    #region Helpers

    /// <summary>A row of the truth's pairs list.</summary>
    /// <param name="Pair">The pair's name.</param>
    /// <param name="JulianDay">The midpoint Julian Day (UT).</param>
    /// <param name="Sun">The Sun's longitude at the midpoint, in degrees.</param>
    /// <param name="Moon">The Moon's longitude.</param>
    /// <param name="Ascendant">The Ascendant.</param>
    /// <param name="Midheaven">The Midheaven.</param>
    private sealed record TruthRow(string Pair, double JulianDay, double Sun, double Moon, double Ascendant, double Midheaven);

    /// <summary>Reads one row of <c>TierB/davison_pairs.tsv</c> by the pair's name.</summary>
    /// <param name="pair">The pair's name: golden, antimeridian, london_sydney, julian or exact180.</param>
    /// <param name="callerFilePath">This file's path, from the compiler.</param>
    /// <returns>The row.</returns>
    private static TruthRow Truth(string pair, [CallerFilePath] string callerFilePath = "")
    {
        string directory = Path.GetDirectoryName(callerFilePath)
            ?? throw new InvalidOperationException("Unable to determine the test's source directory.");
        string path = Path.Combine(directory, "TierB", "davison_pairs.tsv");
        File.Exists(path).Should().BeTrue($"the truth list '{path}' should exist");

        string[] cells = File.ReadAllLines(path)
            .Skip(1)
            .Select(line => line.Split('\t'))
            .Should().ContainSingle(c => c[0] == pair, $"the pair '{pair}' is in the list").Subject;
        cells.Should().HaveCount(11);
        return new TruthRow(cells[0], Parse(cells[1]), Parse(cells[6]), Parse(cells[7]), Parse(cells[8]), Parse(cells[9]));
    }

    /// <summary>A list's number, printed with a point.</summary>
    /// <param name="cell">The cell.</param>
    /// <returns>The number.</returns>
    private static double Parse(string cell) => double.Parse(cell, CultureInfo.InvariantCulture);

    /// <summary>The chart's Julian Day, Sun, Moon, Ascendant and Midheaven against a truth row.</summary>
    /// <param name="result">The result.</param>
    /// <param name="truth">The row.</param>
    private static void AssertChartMatches(DavisonResult result, TruthRow truth)
    {
        ChartResult chart = result.DavisonChart;
        chart.JulianDay.Should().BeApproximately(result.MidpointJulianDay, 1.0 / 86400.0, "the chart is cast at the midpoint, to the second");
        chart.JulianDay.Should().BeApproximately(truth.JulianDay, 1.0 / 86400.0);
        chart.UtcDateTime.Should().Be(result.MidpointUtc);
        Longitude(chart, CelestialBodyId.Sun).Should().BeApproximately(truth.Sun, LongitudeTolerance);
        Longitude(chart, CelestialBodyId.Moon).Should().BeApproximately(truth.Moon, LongitudeTolerance);
        chart.Houses.Ascendant.Should().BeApproximately(truth.Ascendant, LongitudeTolerance);
        chart.Houses.Midheaven.Should().BeApproximately(truth.Midheaven, LongitudeTolerance);
    }

    /// <summary>A body's longitude in a chart.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="body">The body.</param>
    /// <returns>The longitude, in degrees.</returns>
    private static double Longitude(ChartResult chart, CelestialBodyId body) =>
        chart.Positions.Single(p => p.Body == body).Longitude;

    /// <summary>A chart engine over the shared ephemeris.</summary>
    private ChartEngine Engine() => GoldenFixtures.CreateChartEngine(fixture);

    /// <summary>A Davison service over a fresh engine.</summary>
    private DavisonService Service() => new(Engine(), new Serilog.LoggerConfiguration().CreateLogger());

    /// <summary>J2000 NYC, the switch off.</summary>
    private ChartResult J2000Nyc() => GoldenFixtures.J2000Nyc(fixture);

    /// <summary>The Secondary fixture, the switch off.</summary>
    private ChartResult SecondaryNatal() => GoldenFixtures.SecondaryNatal(fixture);

    #endregion
}
