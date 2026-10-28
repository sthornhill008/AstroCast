using System.Globalization;

using AstroCast.Core.Models;
using AstroCast.Core.Models.Progressions;
using AstroCast.Core.Models.SolarArcs;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;
using AstroCast.Tests.Services.MinorPoints;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="SolarArcService"/>.
/// Uses the shared ephemeris fixture to build a natal chart and compute
/// the solar arc value via ProgressionService.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class SolarArcServiceTests
{
    private readonly SolarArcService _solarArcService;
    private readonly ChartResult _natalChart;
    private readonly double _solarArcValue;
    private readonly ChartEngine _engine;
    private readonly EphemerisFixture _fixture;

    /// <summary>
    /// Receives the shared ephemeris fixture, builds a natal chart, computes
    /// the solar arc value from ProgressionService, and constructs SolarArcService.
    /// </summary>
    public SolarArcServiceTests(EphemerisFixture fixture)
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        ChartEngine engine = new(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);

        ChartInput input = new()
        {
            Name = "Solar Arc Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        };

        _natalChart = engine.Calculate(input);

        // Get the solar arc value from the progression service
        ProgressionService progressionService = new(fixture.EphemerisService);
        ProgressionResult progressionResult = progressionService.Calculate(
            _natalChart, new DateOnly(2026, 2, 25));
        _solarArcValue = progressionResult.SolarArcValue;

        _solarArcService = new SolarArcService();
        _engine = engine;
        _fixture = fixture;
    }

    /// <summary>
    /// Person A (invented), Eleanor Hartwell: 1947-10-05 09:30 at an explicit UTC−5
    /// (14:30 UT), Harrisburg, Pennsylvania.
    /// </summary>
    private ChartResult Hartwell() => _engine.Calculate(new ChartInput
    {
        Name = "Eleanor Hartwell",
        LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
        TimeZone = "America/New_York",
        UseExplicitOffset = true,
        UtcOffsetHours = -5.0,
        Latitude = 40.2737,
        Longitude = -76.8844,
        LocationName = "Harrisburg, Pennsylvania, United States",
        EventType = "Birth",
    });

    /// <summary>
    /// Calculate should return a valid SolarArcResult with all properties populated.
    /// </summary>
    [Fact]
    public void Calculate_Should_ReturnValidResult()
    {
        // Act
        SolarArcResult result = _solarArcService.Calculate(_natalChart, _solarArcValue);

        // Assert
        result.Should().NotBeNull();
        result.ArcValue.Should().BeGreaterThan(0);
        result.ArcFormatted.Should().NotBeNullOrWhiteSpace();
        result.Positions.Should().NotBeEmpty();
        result.Aspects.Should().NotBeNull();
    }

    /// <summary>
    /// There should be 14 directed positions: 12 natal bodies + ASC + MC.
    /// </summary>
    [Fact]
    public void Calculate_Should_Include14DirectedPositions()
    {
        // Act
        SolarArcResult result = _solarArcService.Calculate(_natalChart, _solarArcValue);

        // Assert
        result.Positions.Should().HaveCount(14,
            "12 natal bodies (Sun, Moon, Mercury, Venus, Mars, Jupiter, Saturn, " +
            "Uranus, Neptune, Pluto, Chiron, True Node) + 2 angles (ASC, MC)");
    }

    /// <summary>
    /// Each directed longitude should equal natal longitude + arc value (mod 360).
    /// </summary>
    [Fact]
    public void Calculate_DirectedLongitude_Should_EqualNatalPlusArc()
    {
        // Act
        SolarArcResult result = _solarArcService.Calculate(_natalChart, _solarArcValue);

        // Assert
        foreach (SolarArcPosition position in result.Positions)
        {
            double expected = (position.NatalLongitude + _solarArcValue) % 360.0;
            if (expected < 0) expected += 360.0;

            position.DirectedLongitude.Should().BeApproximately(expected, 0.001,
                $"directed longitude for {position.Name} should equal natal + arc");
        }
    }

    /// <summary>
    /// The aspects are the five majors and the 45° series, semisquare and sesquiquadrate (branch Modes,
    /// scoping C3b), the two hard like the conjunction, square and opposition. J2000 NYC at 2026-02-25
    /// (arc about 26.63°) has SA Venus semisquare the natal Moon (Tier B: exact on 2026-04-11) and
    /// SA Saturn sesquiquadrate the Sun/True Node midpoint is a hit, not an aspect; the semisquare here is
    /// the one checked.
    /// </summary>
    [Fact]
    public void Calculate_Aspects_AreTheMajorsAndThe45DegreeSeries()
    {
        SolarArcResult result = _solarArcService.Calculate(_natalChart, _solarArcValue);

        string[] validAbbreviations = ["cnj", "opp", "tri", "sqr", "sxt", "ssq", "ses"];
        foreach (SolarArcAspect aspect in result.Aspects)
        {
            aspect.AspectAbbreviation.Should().BeOneOf(validAbbreviations,
                $"aspect '{aspect.DirectedBody} {aspect.AspectName} {aspect.NatalTarget}' " +
                "should be a major or a 45-degree-series aspect");
        }

        SolarArcAspect semisquare = result.Aspects.Single(a =>
            a.DirectedBody == "SA Venus" && a.NatalTarget == "Moon");
        (semisquare.AspectName, semisquare.AspectAbbreviation, semisquare.IsHardAspect).Should().Be(("Semisquare", "ssq", true));
        semisquare.Direction.Should().Be("applying", "the arc reaches the semisquare on 2026-04-11");
    }

    /// <summary>
    /// All aspects should have orbs within the 1° maximum.
    /// </summary>
    [Fact]
    public void Calculate_Aspects_Should_Have1DegreeOrb()
    {
        // Act
        SolarArcResult result = _solarArcService.Calculate(_natalChart, _solarArcValue);

        // Assert
        foreach (SolarArcAspect aspect in result.Aspects)
        {
            aspect.OrbDegrees.Should().BeLessThanOrEqualTo(1.0,
                $"aspect '{aspect.DirectedBody} {aspect.AspectAbbreviation} {aspect.NatalTarget}' " +
                "should have orb within 1°");

            aspect.Direction.Should().BeOneOf("applying", "separating",
                "direction must be applying or separating");
        }
    }

    /// <summary>
    /// Person A at 2022-02-02 (arc 74.66824406°): the directed Sun is 0.0024° short of
    /// opposing natal Uranus. The arc only grows, so the aspect is applying.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Until 1.3.3 the direction compared the orb now with the orb 0.01° of arc later. An
    /// aspect closer to exact than 0.005° of arc had passed exact and opened again by then,
    /// and was called separating (finding T3-3).
    /// </para>
    /// <para>
    /// Tier B (direct Swiss Ephemeris calls, fixtures/truth/progressed_hartwell.py): the
    /// arc is the progressed Sun at JD 2432538.43379249 less natal Sun 191.50014800°; natal
    /// Uranus 86.17079200°; the opposition is exact at an arc of 74.67064400°, 0.0023999°
    /// further on.
    /// </para>
    /// </remarks>
    [Fact]
    public void Calculate_Aspect_Should_BeApplying_When_WithinHalfAStepOfExact()
    {
        SolarArcResult result = _solarArcService.Calculate(Hartwell(), 74.66824405931797);

        SolarArcAspect opposition = result.Aspects.Single(a =>
            a.DirectedBody == "SA Sun" && a.AspectName == "Opposition" && a.NatalTarget == "Uranus");
        opposition.OrbDegrees.Should().Be(0.0024, "Tier B puts the orb at 0.0023999°");
        opposition.Direction.Should().Be("applying", "the arc reaches the opposition 0.0024° further on");
    }

    /// <summary>
    /// Person A at 2022-02-04 (arc 74.67381954°): the directed Sun passed its opposition to
    /// natal Uranus 0.0032° of arc ago, so the aspect is separating.
    /// </summary>
    /// <remarks>
    /// Tier B (fixtures/truth/progressed_hartwell.py): natal Uranus 86.17079200°; the
    /// opposition was exact at an arc of 74.67064400°. Right before 1.3.3 as well.
    /// </remarks>
    [Fact]
    public void Calculate_Aspect_Should_BeSeparating_When_JustPastExact()
    {
        SolarArcResult result = _solarArcService.Calculate(Hartwell(), 74.67381954088862);

        SolarArcAspect opposition = result.Aspects.Single(a =>
            a.DirectedBody == "SA Sun" && a.AspectName == "Opposition" && a.NatalTarget == "Uranus");
        opposition.OrbDegrees.Should().Be(0.0032, "Tier B puts the orb at 0.0031755°");
        opposition.Direction.Should().Be("separating", "the arc passed the opposition 0.0032° ago");
    }

    /// <summary>
    /// No aspect should be a self-aspect (SA Sun → natal Sun, etc.).
    /// </summary>
    [Fact]
    public void Calculate_Aspects_Should_NotIncludeSelfAspects()
    {
        // Act
        SolarArcResult result = _solarArcService.Calculate(_natalChart, _solarArcValue);

        // Assert
        foreach (SolarArcAspect aspect in result.Aspects)
        {
            // Extract the body name from "SA {name}"
            string directedBodyName = aspect.DirectedBody.StartsWith("SA ", StringComparison.Ordinal)
                ? aspect.DirectedBody[3..]
                : aspect.DirectedBody;

            directedBodyName.Should().NotBe(aspect.NatalTarget,
                $"SA {directedBodyName} should not aspect natal {aspect.NatalTarget} (self-aspect)");
        }
    }

    /// <summary>
    /// Any aspect involving ASC or MC should have InvolvesAngle set to true.
    /// </summary>
    [Fact]
    public void Calculate_Aspects_InvolvesAngle_Should_BeTrueForAngles()
    {
        // Act
        SolarArcResult result = _solarArcService.Calculate(_natalChart, _solarArcValue);

        // Assert
        string[] angleNames = ["ASC", "MC"];

        foreach (SolarArcAspect aspect in result.Aspects)
        {
            string directedBodyName = aspect.DirectedBody.StartsWith("SA ", StringComparison.Ordinal)
                ? aspect.DirectedBody[3..]
                : aspect.DirectedBody;

            bool shouldInvolveAngle = angleNames.Contains(directedBodyName)
                || angleNames.Contains(aspect.NatalTarget);

            if (shouldInvolveAngle)
            {
                aspect.InvolvesAngle.Should().BeTrue(
                    $"aspect '{aspect.DirectedBody} {aspect.AspectAbbreviation} {aspect.NatalTarget}' " +
                    "involves an angle and should have InvolvesAngle = true");
            }
        }
    }

    /// <summary>
    /// Aspects are sorted hard first (conjunction, opposition, square, semisquare and sesquiquadrate),
    /// then by orb ascending; the semisquare to the Moon sorts among the hard ones.
    /// </summary>
    [Fact]
    public void Calculate_Aspects_Should_BeSortedHardFirst_ThenByOrb()
    {
        // Act
        SolarArcResult result = _solarArcService.Calculate(_natalChart, _solarArcValue);

        // Assert
        result.Aspects.Should().HaveCountGreaterThan(1);
        result.Aspects.Select(a => a.IsHardAspect).Should().ContainInOrder(true, false);
        result.Aspects.Where(a => a.AspectAbbreviation is "ssq" or "ses").Should().OnlyContain(a => a.IsHardAspect);

        // Verify hard aspects come before soft aspects
        bool seenSoft = false;
        double lastHardOrb = -1;
        double lastSoftOrb = -1;

        foreach (SolarArcAspect aspect in result.Aspects)
        {
            if (aspect.IsHardAspect)
            {
                seenSoft.Should().BeFalse(
                    "hard aspects should come before all soft aspects");

                aspect.OrbDegrees.Should().BeGreaterThanOrEqualTo(lastHardOrb,
                    "hard aspects should be sorted by orb ascending");
                lastHardOrb = aspect.OrbDegrees;
            }
            else
            {
                seenSoft = true;
                aspect.OrbDegrees.Should().BeGreaterThanOrEqualTo(lastSoftOrb,
                    "soft aspects should be sorted by orb ascending");
                lastSoftOrb = aspect.OrbDegrees;
            }
        }
    }

    /// <summary>
    /// ArcFormatted should match the arc value in degrees°minutes' format.
    /// </summary>
    [Fact]
    public void Calculate_ArcFormatted_Should_MatchArcValue()
    {
        // Arrange — use a known arc value: 36.133° → 36°07'
        double knownArc = 36.133;

        // Act
        SolarArcResult result = _solarArcService.Calculate(_natalChart, knownArc);

        // Assert
        int expectedDegrees = 36;
        int expectedMinutes = (int)((knownArc - expectedDegrees) * 60.0); // 7
        string expectedFormatted = $"{expectedDegrees}\u00b0{expectedMinutes:D2}'";

        result.ArcFormatted.Should().Be(expectedFormatted);
    }

    /// <summary>
    /// With zero arc, all directed longitudes should equal natal longitudes.
    /// </summary>
    [Fact]
    public void Calculate_WithZeroArc_DirectedShouldEqualNatal()
    {
        // Arrange
        double zeroArc = 0.0;

        // Act
        SolarArcResult result = _solarArcService.Calculate(_natalChart, zeroArc);

        // Assert
        foreach (SolarArcPosition position in result.Positions)
        {
            position.DirectedLongitude.Should().BeApproximately(
                position.NatalLongitude, 0.001,
                $"with zero arc, directed {position.Name} should equal natal");
        }
    }

    #region Branch AsteroidsTiming — the natal five as targets (R7)

    /// <summary>The J2000 NYC natal carrying the five, as the goldens cast it.</summary>
    private ChartResult J2000NycWithFive() =>
        GoldenFixtures.CreateChartEngine(_fixture).Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true));

    /// <summary>The solar arc at a report's start, as the transit report takes it: the progressions' arc.</summary>
    /// <param name="natal">The natal chart.</param>
    /// <param name="start">The report's start.</param>
    /// <returns>The arc, in degrees.</returns>
    private double ArcAt(ChartResult natal, DateOnly start) =>
        new ProgressionService(_fixture.EphemerisService).Calculate(natal, start).SolarArcValue;

    /// <summary>The natal five's names, the only targets of <see cref="SolarArcResult.MinorPointAspects"/>.</summary>
    private static readonly string[] Five = MinorPointTierB.Names;

    /// <summary>
    /// Tier B (ast_truth_timing.py): with the natal carrying the five, the directed points' aspects to them at the
    /// solar arc golden's start (2026-01-01) and at the transit golden's (2026-04-12) are the list's SA-to-natal
    /// rows: point, aspect and target equal, the orb within 1e-4° and the direction equal. SA Venus sesquiquadrate
    /// Pallas, 0.002° inside the orb on 2026-01-01, is among them.
    /// </summary>
    /// <param name="report">The list's report.</param>
    /// <param name="start">The report's start.</param>
    [Theory]
    [InlineData("solararc_2026", "2026-01-01")]
    [InlineData("transit_20260412_18", "2026-04-12")]
    public void Calculate_SwitchOn_MinorPointAspects_MatchTierB(string report, string start)
    {
        List<MinorPointTierB.TimingProgressionRow> expected = MinorPointTierB.LoadTimingProgressions(report, "SA-to-natal");
        expected.Should().NotBeEmpty();
        ChartResult natal = J2000NycWithFive();

        SolarArcResult result = _solarArcService.Calculate(
            natal, ArcAt(natal, DateOnly.ParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture)));

        result.MinorPointAspects.Select(a => (a.DirectedBody, a.AspectAbbreviation, a.NatalTarget))
            .Should().BeEquivalentTo(expected.Select(r => ($"SA {r.Point}", r.Aspect, r.Target)), "the aspects to the five are the list's rows");

        foreach (MinorPointTierB.TimingProgressionRow row in expected)
        {
            string because = $"of SA {row.Point} {row.Aspect} {row.Target} at {start}";
            SolarArcAspect aspect = result.MinorPointAspects.Single(a =>
                a.DirectedBody == $"SA {row.Point}" && a.AspectAbbreviation == row.Aspect && a.NatalTarget == row.Target);

            row.Orb.Should().NotBeNull(because);
            if (row.Orb is double orb)
                aspect.OrbDegrees.Should().BeApproximately(orb, 1e-4, because);
            aspect.Direction.Should().BeEquivalentTo(row.Direction, because);
        }
    }

    /// <summary>
    /// The five are targets only: no directed asteroid or Lilith, the 14 directed positions unchanged, every row of
    /// <see cref="SolarArcResult.MinorPointAspects"/> from one of the 14 to one of the five, sorted hard first then by
    /// orb as the chart's own are, none of the five in the chart's own aspects, and those the natal's without the
    /// five (R9).
    /// </summary>
    [Fact]
    public void Calculate_SwitchOn_TheFiveAreTargetsOnly()
    {
        ChartResult natal = J2000NycWithFive();
        ChartResult plainNatal = GoldenFixtures.J2000Nyc(_fixture);
        double arc = ArcAt(natal, new DateOnly(2026, 1, 1));

        SolarArcResult result = _solarArcService.Calculate(natal, arc);
        SolarArcResult plain = _solarArcService.Calculate(plainNatal, arc);

        result.Positions.Should().HaveCount(14);
        result.Positions.Should().NotContain(p => Five.Contains(p.Name));
        result.MinorPointAspects.Should().NotBeEmpty();
        result.MinorPointAspects.Should().OnlyContain(a => Five.Contains(a.NatalTarget) && Five.All(f => a.DirectedBody != $"SA {f}"));
        result.MinorPointAspects.Should().BeInDescendingOrder(a => a.IsHardAspect);
        result.MinorPointAspects.Where(a => a.IsHardAspect).Should().BeInAscendingOrder(a => a.OrbDegrees);
        result.MinorPointAspects.Where(a => a.IsHardAspect == false).Should().BeInAscendingOrder(a => a.OrbDegrees);
        result.Aspects.Should().NotContain(a => Five.Contains(a.NatalTarget));
        result.Aspects.Select(a => (a.DirectedBody, a.AspectName, a.NatalTarget, a.OrbDegrees, a.Direction))
            .Should().Equal(plain.Aspects.Select(a => (a.DirectedBody, a.AspectName, a.NatalTarget, a.OrbDegrees, a.Direction)));
        plain.MinorPointAspects.Should().BeEmpty();
    }

    #endregion
}
