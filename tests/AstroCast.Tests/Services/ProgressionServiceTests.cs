using System.Globalization;

using AstroCast.Core.Models;
using AstroCast.Core.Models.Progressions;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;
using AstroCast.Tests.Services.MinorPoints;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="ProgressionService"/>.
/// Uses the shared ephemeris fixture and a real natal chart.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ProgressionServiceTests
{
    private readonly ProgressionService _progressionService;
    private readonly ChartResult _natalChart;
    private readonly ChartEngine _engine;
    private readonly EphemerisFixture _fixture;

    /// <summary>
    /// Receives the shared ephemeris fixture, builds a natal chart via ChartEngine,
    /// and constructs the ProgressionService.
    /// </summary>
    public ProgressionServiceTests(EphemerisFixture fixture)
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        ChartEngine engine = new(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);

        ChartInput input = new()
        {
            Name = "Progression Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        };

        _natalChart = engine.Calculate(input);
        _progressionService = new ProgressionService(fixture.EphemerisService);
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
    /// Calculate should return a valid ProgressionResult with all properties populated.
    /// </summary>
    [Fact]
    public void Calculate_Should_ReturnValidResult()
    {
        // Arrange
        DateOnly reportDate = new(2026, 2, 25);

        // Act
        ProgressionResult result = _progressionService.Calculate(_natalChart, reportDate);

        // Assert
        result.Should().NotBeNull();
        result.ProgressedDate.Should().BeAfter(_natalChart.UtcDateTime);
        result.ProgressedJulianDay.Should().BeGreaterThan(0);
        result.Positions.Should().NotBeEmpty();
        result.MoonInfo.Should().NotBeNull();
        result.Aspects.Should().NotBeNull();
        result.SignChanges.Should().NotBeNull();
        result.Stations.Should().NotBeNull();
    }

    /// <summary>
    /// There should be 7 progressed positions: Sun, Moon, Mercury, Venus, Mars, ASC, MC.
    /// </summary>
    [Fact]
    public void Calculate_Should_Include7ProgressedBodies()
    {
        // Arrange
        DateOnly reportDate = new(2026, 2, 25);

        // Act
        ProgressionResult result = _progressionService.Calculate(_natalChart, reportDate);

        // Assert
        result.Positions.Should().HaveCount(7,
            "5 planets (Sun, Moon, Mercury, Venus, Mars) + 2 angles (ASC, MC)");

        string[] expectedNames = ["Sun", "Moon", "Mercury", "Venus", "Mars", "ASC", "MC"];
        result.Positions.Select(p => p.Name).Should().BeEquivalentTo(expectedNames);
    }

    /// <summary>
    /// Solar arc value should be positive (Sun has moved forward since birth).
    /// </summary>
    [Fact]
    public void Calculate_SolarArcValue_Should_BePositive()
    {
        // Arrange
        DateOnly reportDate = new(2026, 2, 25);

        // Act
        ProgressionResult result = _progressionService.Calculate(_natalChart, reportDate);

        // Assert — at age ~26, solar arc should be roughly 26° (Sun moves ~1°/year)
        result.SolarArcValue.Should().BeGreaterThan(0,
            "progressed Sun advances from natal position");
        result.SolarArcValue.Should().BeInRange(20.0, 32.0,
            "at age ~26, solar arc should be roughly 26° (±6° tolerance)");
    }

    /// <summary>
    /// Progressed Moon should have a valid phase name from the 8 lunar phases.
    /// </summary>
    [Fact]
    public void Calculate_ProgressedMoon_Should_HaveValidPhase()
    {
        // Arrange
        DateOnly reportDate = new(2026, 2, 25);

        // Act
        ProgressionResult result = _progressionService.Calculate(_natalChart, reportDate);

        // Assert
        string[] validPhases =
        [
            "New Moon", "Crescent", "First Quarter", "Gibbous",
            "Full Moon", "Disseminating", "Last Quarter", "Balsamic"
        ];

        result.MoonInfo.PhaseName.Should().BeOneOf(validPhases);
        result.MoonInfo.PhaseAngle.Should().BeInRange(0.0, 360.0);
    }

    /// <summary>
    /// Progressed Moon should occupy a valid natal house (1–12).
    /// </summary>
    [Fact]
    public void Calculate_ProgressedMoon_Should_HaveValidHouse()
    {
        // Arrange
        DateOnly reportDate = new(2026, 2, 25);

        // Act
        ProgressionResult result = _progressionService.Calculate(_natalChart, reportDate);

        // Assert
        result.MoonInfo.NatalHouse.Should().BeInRange(1, 12);
        result.MoonInfo.Sign.Should().NotBeNullOrWhiteSpace();
        result.MoonInfo.Degrees.Should().BeInRange(0, 29);
        result.MoonInfo.Minutes.Should().BeInRange(0, 59);
    }

    /// <summary>
    /// Progressed aspects should only contain major aspects (cnj, opp, tri, sqr, sxt).
    /// </summary>
    [Fact]
    public void Calculate_ProgressedAspects_Should_HaveMajorAspectsOnly()
    {
        // Arrange
        DateOnly reportDate = new(2026, 2, 25);

        // Act
        ProgressionResult result = _progressionService.Calculate(_natalChart, reportDate);

        // Assert
        string[] validAbbreviations = ["cnj", "opp", "tri", "sqr", "sxt"];

        foreach (ProgressedAspect aspect in result.Aspects)
        {
            aspect.AspectAbbreviation.Should().BeOneOf(validAbbreviations,
                $"aspect '{aspect.ProgressedBody} {aspect.AspectName} {aspect.NatalTarget}' " +
                "should be a major aspect");
        }
    }

    /// <summary>
    /// All progressed aspects should have orbs within the 1° maximum.
    /// </summary>
    [Fact]
    public void Calculate_ProgressedAspects_Should_Have1DegreeOrb()
    {
        // Arrange
        DateOnly reportDate = new(2026, 2, 25);

        // Act
        ProgressionResult result = _progressionService.Calculate(_natalChart, reportDate);

        // Assert
        foreach (ProgressedAspect aspect in result.Aspects)
        {
            aspect.OrbDegrees.Should().BeLessThanOrEqualTo(1.0,
                $"aspect '{aspect.ProgressedBody} {aspect.AspectAbbreviation} {aspect.NatalTarget}' " +
                "should have orb within 1°");

            aspect.Direction.Should().BeOneOf("applying", "separating",
                "direction must be applying or separating");
        }
    }

    /// <summary>
    /// Person A at 2022-02-02: the progressed Sun is 0.0024° short of opposing natal
    /// Uranus and closing, so the aspect is applying.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Until 1.3.3 the direction compared the orb now with the orb 0.01 progressed day
    /// later. The progressed Sun covers about 0.01° in that step, so an aspect closer to
    /// exact than half of it has passed exact and opened again by the end of the step,
    /// and was called separating (finding T3-3).
    /// </para>
    /// <para>
    /// Tier B (direct Swiss Ephemeris calls, fixtures/truth/progressed_hartwell.py):
    /// progressed instant JD 2432538.43379249 (birth + (2022-02-02 12:00 UT − birth) /
    /// 365.25); progressed Sun 266.16839206°, natal Uranus 86.17079200°; orb 0.0023999°,
    /// closing at 1.0182° per progressed day by a ±5-second symmetric difference; exact by
    /// bisection at 2022-02-03 08:40 UT.
    /// </para>
    /// </remarks>
    [Fact]
    public void Calculate_ProgressedAspect_Should_BeApplying_When_WithinHalfAStepOfExact()
    {
        ProgressionResult result = _progressionService.Calculate(Hartwell(), new DateOnly(2022, 2, 2));

        ProgressedAspect opposition = result.Aspects.Single(a =>
            a.ProgressedBody == "P Sun" && a.AspectName == "Opposition" && a.NatalTarget == "Uranus");
        opposition.OrbDegrees.Should().Be(0.0024, "Tier B puts the orb at 0.0023999°");
        opposition.Direction.Should().Be("applying",
            "the progressed Sun reaches the opposition 0.0024 progressed day later");
    }

    /// <summary>
    /// Person A at 2022-02-04: the progressed Sun passed its opposition to natal Uranus
    /// 0.0032° ago, so the aspect is separating.
    /// </summary>
    /// <remarks>
    /// Tier B (fixtures/truth/progressed_hartwell.py): progressed Sun 266.17396754°, natal
    /// Uranus 86.17079200°; orb 0.0031755°, opening at 1.0182° per progressed day; exact at
    /// 2022-02-03 08:40 UT, the report date before. Right before 1.3.3 as well.
    /// </remarks>
    [Fact]
    public void Calculate_ProgressedAspect_Should_BeSeparating_When_JustPastExact()
    {
        ProgressionResult result = _progressionService.Calculate(Hartwell(), new DateOnly(2022, 2, 4));

        ProgressedAspect opposition = result.Aspects.Single(a =>
            a.ProgressedBody == "P Sun" && a.AspectName == "Opposition" && a.NatalTarget == "Uranus");
        opposition.OrbDegrees.Should().Be(0.0032, "Tier B puts the orb at 0.0031755°");
        opposition.Direction.Should().Be("separating",
            "the progressed Sun perfected the opposition 0.0032 progressed day earlier");
    }

    /// <summary>
    /// Sign changes list should be valid (empty is acceptable if no sign changes
    /// occur within 6 months). Any entries should have valid sign names.
    /// </summary>
    [Fact]
    public void Calculate_SignChanges_Should_BeValidOrEmpty()
    {
        // Arrange
        DateOnly reportDate = new(2026, 2, 25);

        // Act
        ProgressionResult result = _progressionService.Calculate(_natalChart, reportDate);

        // Assert
        string[] validSigns =
        [
            "Aries", "Taurus", "Gemini", "Cancer", "Leo", "Virgo",
            "Libra", "Scorpio", "Sagittarius", "Capricorn", "Aquarius", "Pisces"
        ];

        foreach (ProgressedSignChange change in result.SignChanges)
        {
            change.BodyName.Should().NotBeNullOrWhiteSpace();
            change.FromSign.Should().BeOneOf(validSigns);
            change.ToSign.Should().BeOneOf(validSigns);
            change.FromSign.Should().NotBe(change.ToSign,
                "a sign change must be from one sign to a different sign");
        }
    }
    #region Branch G — angles by solar arc, dated events (G1–G5)

    /// <summary>The J2000 NYC fixture chart (2000-01-01 07:00 America/New_York, New York City, Placidus).</summary>
    private ChartResult J2000Nyc() => GoldenFixtures.J2000Nyc(_fixture);

    /// <summary>
    /// G1 (R1). At 2026-04-15 the progressed Midheaven is the natal Midheaven plus the solar arc,
    /// 26.769192° (235.2381°, 25°14′ Scorpio), and the progressed Ascendant is the Ascendant for
    /// that Midheaven at the birth latitude with the natal true obliquity, 302.844414° (02°50′
    /// Aquarius), Tier B; the movements from natal are 26.769° and 28.603°. Until branch G the
    /// angles were the quotidian ones, the chart for the progressed instant at the birthplace:
    /// ASC 87.45° (27°27′ Gemini), MC 333.43° (03°26′ Pisces).
    /// </summary>
    [Fact]
    public void Calculate_Angles_AreTheNatalMidheavenPlusTheSolarArcAndItsAscendant()
    {
        ChartResult natal = J2000Nyc();

        ProgressionResult result = _progressionService.Calculate(natal, new DateOnly(2026, 4, 15));

        result.SolarArcValue.Should().BeApproximately(26.769192, 0.001);

        ProgressedPosition mc = result.Positions.Single(p => p.Name == "MC");
        mc.ProgressedLongitude.Should().BeApproximately((natal.Houses.Midheaven + 26.769192) % 360.0, 0.001);
        mc.ProgressedLongitude.Should().BeApproximately(235.2381, 0.001);
        mc.MovementFromNatal.Should().BeApproximately(26.769, 0.001);

        ProgressedPosition asc = result.Positions.Single(p => p.Name == "ASC");
        asc.ProgressedLongitude.Should().BeApproximately(302.844414, 0.001);
        asc.MovementFromNatal.Should().BeApproximately(28.603, 0.001);
    }

    /// <summary>
    /// G2 (R1, R2). At 2026-04-15 the progressed angles make exactly one aspect to a natal point:
    /// the progressed Ascendant conjunct natal Neptune, orb 0.3486°, applying (the Ascendant moves
    /// 1.304822° a progressed day). Until branch G the quotidian angles made P ASC trine Mars and
    /// P MC sextile ASC, and an angle's aspect was always "applying".
    /// </summary>
    [Fact]
    public void Calculate_AngleAspects_AreTheAscendantConjunctNeptuneApplying()
    {
        ProgressionResult result = _progressionService.Calculate(J2000Nyc(), new DateOnly(2026, 4, 15));

        ProgressedAspect aspect = result.Aspects
            .Where(a => a.ProgressedBody is "P ASC" or "P MC")
            .Should().ContainSingle("the directed Ascendant is within 1° of natal Neptune and the Midheaven of nothing")
            .Subject;

        aspect.ProgressedBody.Should().Be("P ASC");
        aspect.AspectName.Should().Be("Conjunction");
        aspect.NatalTarget.Should().Be("Neptune");
        aspect.OrbDegrees.Should().BeApproximately(0.3486, 0.001);
        aspect.Direction.Should().Be("applying");
    }

    /// <summary>
    /// G3 (R3). The directed angles' sign changes are found on the functions MC(t) and ASC(t) of
    /// the progressed Julian Day, as the bodies' are: at 2031-01-15 the Midheaven enters
    /// Sagittarius on 2030-12-22 (15:13:42 UT), and at 2024-06-01 the Ascendant enters Aquarius on
    /// 2024-01-26 (09:02:14 UT), Tier B. Until branch G the quotidian scan, which compared the two
    /// ends of a window the quotidian angles turn through twelve times, listed neither.
    /// </summary>
    [Fact]
    public void Calculate_SignChanges_IncludeTheDirectedAnglesCrossings()
    {
        ChartResult natal = J2000Nyc();

        ProgressionResult january2031 = _progressionService.Calculate(natal, new DateOnly(2031, 1, 15));
        january2031.SignChanges.Should().ContainSingle(c => c.BodyName == "MC")
            .Which.Should().BeEquivalentTo(new ProgressedSignChange
            {
                BodyName = "MC",
                FromSign = "Scorpio",
                ToSign = "Sagittarius",
                Date = new DateOnly(2030, 12, 22),
            });

        ProgressionResult june2024 = _progressionService.Calculate(natal, new DateOnly(2024, 6, 1));
        june2024.SignChanges.Should().ContainSingle(c => c.BodyName == "ASC")
            .Which.Should().BeEquivalentTo(new ProgressedSignChange
            {
                BodyName = "ASC",
                FromSign = "Capricorn",
                ToSign = "Aquarius",
                Date = new DateOnly(2024, 1, 26),
            });
    }

    /// <summary>
    /// G4 (R4, R5). At 2028-06-08 the progressed Moon enters Sagittarius 88.93 real days ahead,
    /// on 2028-09-05 at 10:21:17 UT (Tier B): the scan reaches the 90-day bound, the change is
    /// bisected and dated by its instant. Until branch G the scan stopped at 0.24 progressed day,
    /// 87.7 real days, and reported none.
    /// </summary>
    [Fact]
    public void Calculate_MoonNextSignChange_IsFoundUpToTheBoundAndDatedByItsInstant()
    {
        ProgressionResult result = _progressionService.Calculate(J2000Nyc(), new DateOnly(2028, 6, 8));

        result.MoonInfo.NextSign.Should().Be("Sagittarius");
        result.MoonInfo.NextSignChangeDate.Should().Be(new DateOnly(2028, 9, 5));
    }

    /// <summary>
    /// G5 (R4, R6). At 2050-01-28 progressed Mercury stations retrograde 349.5 real days ahead, at
    /// progressed 2000-02-21 12:46:24, real 2051-01-13 00:26:47 UT (Tier B): the station scan
    /// reaches the 365-day bound, the speed's zero is bisected and dated by its instant. Until
    /// branch G the scan stopped at 0.95 progressed day, 347 real days, and reported none.
    /// </summary>
    [Fact]
    public void Calculate_Stations_AreFoundUpToTheBoundAndDatedByTheirInstant()
    {
        ProgressionResult result = _progressionService.Calculate(J2000Nyc(), new DateOnly(2050, 1, 28));

        result.Stations.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new ProgressedStation
            {
                BodyName = "Mercury",
                StationType = "Retrograde",
                Date = new DateOnly(2051, 1, 13),
            });
    }

    /// <summary>
    /// A progressed body's aspects to its own natal place are listed (branch Modes, Steve's decision of
    /// 2026-10-17, as the dated hits list them): J2000's progressed Moon returns to the natal Moon on
    /// 2027-04-17 21:00:52 UT (Tier B, arcs_truth.py), so on 2027-04-15 it is conjunct within 1° and
    /// applying. The angles keep the skip: the progressed Midheaven is the solar arc Midheaven, whose
    /// aspect to itself is the arc.
    /// </summary>
    [Fact]
    public void Calculate_ProgressedAspects_IncludeABodyToItsOwnNatalPlace()
    {
        ProgressionResult result = _progressionService.Calculate(J2000Nyc(), new DateOnly(2027, 4, 15));

        ProgressedAspect lunarReturn = result.Aspects.Single(a => a.ProgressedBody == "P Moon" && a.NatalTarget == "Moon");
        (lunarReturn.AspectName, lunarReturn.AspectAbbreviation, lunarReturn.Direction).Should().Be(("Conjunction", "cnj", "applying"));
        lunarReturn.OrbDegrees.Should().BeLessThan(0.15, "two days of progressed Moon at about 0.036 degrees a day");
        result.Aspects.Should().NotContain(a => (a.ProgressedBody == "P MC" && a.NatalTarget == "MC") || (a.ProgressedBody == "P ASC" && a.NatalTarget == "ASC"));
    }

    #endregion

    #region Branch AsteroidsTiming — the natal five as targets (R7)

    /// <summary>The J2000 NYC natal carrying the five, as the goldens cast it.</summary>
    private ChartResult J2000NycWithFive() =>
        GoldenFixtures.CreateChartEngine(_fixture).Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true));

    /// <summary>The natal five's names, the only targets of <see cref="ProgressionResult.MinorPointAspects"/>.</summary>
    private static readonly string[] Five = MinorPointTierB.Names;

    /// <summary>
    /// Tier B (ast_truth_timing.py): with the natal carrying the five, the progressed points' aspects to them at
    /// the solar arc golden's start (2026-01-01) and at the transit golden's (2026-04-12) are the list's
    /// P-to-natal rows: point, aspect and target equal, the orb within 1e-4° and the direction equal.
    /// </summary>
    /// <param name="report">The list's report.</param>
    /// <param name="start">The report's start.</param>
    [Theory]
    [InlineData("solararc_2026", "2026-01-01")]
    [InlineData("transit_20260412_18", "2026-04-12")]
    public void Calculate_SwitchOn_MinorPointAspects_MatchTierB(string report, string start)
    {
        List<MinorPointTierB.TimingProgressionRow> expected = MinorPointTierB.LoadTimingProgressions(report, "P-to-natal");
        expected.Should().NotBeEmpty();

        ProgressionResult result = _progressionService.Calculate(
            J2000NycWithFive(), DateOnly.ParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture));

        result.MinorPointAspects.Select(a => (a.ProgressedBody, a.AspectAbbreviation, a.NatalTarget))
            .Should().BeEquivalentTo(expected.Select(r => ($"P {r.Point}", r.Aspect, r.Target)), "the aspects to the five are the list's rows");

        foreach (MinorPointTierB.TimingProgressionRow row in expected)
        {
            string because = $"of P {row.Point} {row.Aspect} {row.Target} at {start}";
            ProgressedAspect aspect = result.MinorPointAspects.Single(a =>
                a.ProgressedBody == $"P {row.Point}" && a.AspectAbbreviation == row.Aspect && a.NatalTarget == row.Target);

            row.Orb.Should().NotBeNull(because);
            if (row.Orb is double orb)
                aspect.OrbDegrees.Should().BeApproximately(orb, 1e-4, because);
            aspect.Direction.Should().BeEquivalentTo(row.Direction, because);
        }
    }

    /// <summary>
    /// The five are targets only: no progressed asteroid or Lilith, every row of <see cref="ProgressionResult.MinorPointAspects"/>
    /// from one of the seven progressed points to one of the five, none of the five in the chart's own aspects, and
    /// the chart's own aspects those of the natal without the five (R9).
    /// </summary>
    [Fact]
    public void Calculate_SwitchOn_TheFiveAreTargetsOnly()
    {
        DateOnly start = new(2026, 1, 1);
        string[] progressed = ["P Sun", "P Moon", "P Mercury", "P Venus", "P Mars", "P ASC", "P MC"];

        ProgressionResult result = _progressionService.Calculate(J2000NycWithFive(), start);
        ProgressionResult plain = _progressionService.Calculate(GoldenFixtures.J2000Nyc(_fixture), start);

        result.Positions.Select(p => p.Name).Should().Equal("Sun", "Moon", "Mercury", "Venus", "Mars", "ASC", "MC");
        result.MinorPointAspects.Should().NotBeEmpty();
        result.MinorPointAspects.Should().OnlyContain(a => progressed.Contains(a.ProgressedBody) && Five.Contains(a.NatalTarget));
        result.Aspects.Should().NotContain(a => Five.Contains(a.NatalTarget) || Five.Any(f => a.ProgressedBody == $"P {f}"));
        result.Aspects.Select(a => (a.ProgressedBody, a.AspectName, a.NatalTarget, a.OrbDegrees, a.Direction))
            .Should().Equal(plain.Aspects.Select(a => (a.ProgressedBody, a.AspectName, a.NatalTarget, a.OrbDegrees, a.Direction)));
        plain.MinorPointAspects.Should().BeEmpty();
    }

    #endregion
}
