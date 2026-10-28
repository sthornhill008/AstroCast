using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Real-instant tests for Mercury's sect and hayz, and for the applying benefic and malefic
/// conditions, cast through <see cref="ChartEngine"/> with the shared ephemeris fixture. The
/// expected values come from outside the code: the rules as the Techniques reference states
/// them — Mercury behind the Sun in the zodiac rises before it and is diurnal, ahead of the
/// Sun it is nocturnal (:95; Ptolemy, <i>Tetrabiblos</i> I.7); an applying benefic aspect is a
/// trine or sextile from Jupiter or Venus, an applying malefic aspect a square or opposition
/// from Mars or Saturn (:159-160) — applied to Swiss Ephemeris positions, rising times and
/// aspects computed independently of AstroCast.Core.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class SectAndConditionsIntegrationTests
{
    private readonly ChartEngine _engine;

    /// <summary>
    /// Creates a ChartEngine using the shared ephemeris fixture.
    /// </summary>
    /// <param name="fixture">The shared Swiss Ephemeris fixture.</param>
    public SectAndConditionsIntegrationTests(EphemerisFixture fixture)
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        DignityService dignityService = new();
        _engine = new ChartEngine(
            fixture.EphemerisService, aspects, midpoints,
            dignityService, new MoonConditionService(),
            new ChartContextService(dignityService, new SectService()),
            new FixedStarService(fixture.EphemerisService, logger),
            new DeclinationAspectService(), new PlanetConditionService(),
            new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(new DignityService()), new AntisciaService(),
            new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    [Fact]
    public void Calculate_Should_MarkMercuryNocturnalInSectAndHayz_When_EveningStarInNightChart()
    {
        // The Venus-station sample instant: JD UT 2461314.95590278, 39.9612 N, 82.9988 W.
        // Independently (Swiss Ephemeris, fixtures/truth/out/horary-rows.out): Sun 188.2868°,
        // Mercury 211.2811° — Mercury is 22.99° ahead of the Sun, an evening star, so
        // nocturnal. The Sun is 6.9° below the horizon (a night chart), so Mercury is in sect;
        // Mercury is below the horizon (−29.4°) in Scorpio, a feminine sign, so it is in hayz.
        ChartResult result = _engine.Calculate(StationSampleInstant());

        result.JulianDay.Should().BeApproximately(2461314.95590278, 1e-8);
        result.ChartContext.Should().NotBeNull();
        result.ChartContext!.IsDayChart.Should().BeFalse();
        SectInfo mercury = result.ChartContext.PlanetarySect.First(s => s.PlanetName == "Mercury");
        mercury.NaturalSect.Should().Be("Nocturnal");
        mercury.IsInSect.Should().BeTrue();
        mercury.IsHayz.Should().BeTrue();
    }

    [Fact]
    public void Calculate_Should_MarkMercuryNocturnalOutOfSectNotHayz_When_EveningStarInDayChart()
    {
        // Person A's instant: JD UT 2432464.1041666665, 40.2737 N, 76.8844 W.
        // Independently (Swiss Ephemeris, fixtures/truth/conditions_hartwell.py): Sun
        // 191.5001°, Mercury 215.1542° — Mercury is 23.65° ahead of the Sun, an evening
        // star, so nocturnal. The Sun is 34.0° above the horizon (a day chart), so Mercury
        // is out of sect and cannot be in hayz.
        ChartResult result = _engine.Calculate(HartwellInstant());

        result.JulianDay.Should().BeApproximately(2432464.1041666665, 1e-8);
        result.ChartContext.Should().NotBeNull();
        result.ChartContext!.IsDayChart.Should().BeTrue();
        SectInfo mercury = result.ChartContext.PlanetarySect.First(s => s.PlanetName == "Mercury");
        mercury.NaturalSect.Should().Be("Nocturnal");
        mercury.IsInSect.Should().BeFalse();
        mercury.IsHayz.Should().BeFalse();
    }

    [Fact]
    public void Calculate_Should_FlagOnlyHardAspectsFromMalefics_When_Hartwell()
    {
        // Person A's instant. Independently (Swiss Ephemeris aspects,
        // fixtures/truth/conditions_hartwell.py): the True Node receives an applying square
        // from Saturn (a malefic aspect) and nothing else conditions it — its only condition
        // is "Applying malefic aspect", scoring −1. The Moon also receives an applying aspect
        // from Saturn, but a sextile (a soft aspect from a malefic, so no malefic flag): its
        // only condition is the applying Venus trine, "Applying benefic aspect", scoring +1.
        ChartResult result = _engine.Calculate(HartwellInstant());

        PlanetCondition node = result.PlanetConditions.First(c => c.PlanetName == "True Node");
        node.Conditions.Should().Equal("Applying malefic aspect");
        node.AccidentalScore.Should().Be(-1);
        PlanetCondition moon = result.PlanetConditions.First(c => c.PlanetName == "Moon");
        moon.Conditions.Should().Equal("Applying benefic aspect");
        moon.AccidentalScore.Should().Be(1);
        moon.HasApplyingMaleficAspect.Should().BeFalse();
    }

    [Fact]
    public void Calculate_Should_FlagOnlyBesiegement_When_StationSampleMoon()
    {
        // The same instant. Independently (Swiss Ephemeris aspects): the Moon lies
        // between Mars and Saturn with no benefic in the arc (besieged, −5), and its one applying
        // aspect from Mars or Saturn is a sextile from Saturn: no malefic flag. Mars's one
        // applying aspect from Jupiter or Venus is a square from Venus: no benefic flag. The
        // facts are longitude-only, so the place does not move them.
        ChartResult result = _engine.Calculate(StationSampleInstant());

        PlanetCondition moon = result.PlanetConditions.First(c => c.PlanetName == "Moon");
        moon.Conditions.Should().Equal("Besieged");
        moon.AccidentalScore.Should().Be(-5);
        result.PlanetConditions.Any(c => c.PlanetName == "Mars" && c.HasApplyingBeneficAspect)
            .Should().BeFalse();
    }

    /// <summary>
    /// The Venus-station sample instant (2026-10-01 06:56:30 EDT, a bare value), cast in Placidus
    /// at Columbus, Ohio.
    /// </summary>
    private static ChartInput StationSampleInstant() => new()
    {
        Name = "Station sample instant",
        LocalDateTime = new DateTime(2026, 10, 1, 6, 56, 30),
        TimeZone = "America/New_York",
        Latitude = 39.9612,
        Longitude = -82.9988,
        LocationName = "Columbus, Ohio",
        EventType = "Event",
    };

    /// <summary>
    /// Person A's instant (invented: 1947-10-05 09:30:00 at an explicit UTC−5), cast in Placidus.
    /// </summary>
    private static ChartInput HartwellInstant() => new()
    {
        Name = "Eleanor Hartwell",
        LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
        TimeZone = "America/New_York",
        UtcOffsetHours = -5.0,
        UseExplicitOffset = true,
        Latitude = 40.2737,
        Longitude = -76.8844,
        LocationName = "Harrisburg, Pennsylvania",
        EventType = "Birth",
    };
}
