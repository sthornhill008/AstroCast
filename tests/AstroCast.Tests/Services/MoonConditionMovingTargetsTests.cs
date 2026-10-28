using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Engine-level tests of the Moon's first applying aspect and void-of-course
/// status on real instants where holding the targets at their chart-instant
/// longitudes gives the wrong answer, through <see cref="ChartEngine.Calculate"/>
/// and the real ephemeris.
/// </summary>
/// <remarks>
/// The reference answers come from the horary review's independent scan of the
/// Moon and the nine targets, every body moving, bisected to one second; the
/// two void cases are rows of its 2000–2030 sweep
/// (<c>out/moonsweep-2000-2030-12h.md</c>), quoted in each test. Each instant
/// is entered as a UT wall clock, so the chart instant is exactly the one
/// quoted.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class MoonConditionMovingTargetsTests
{
    private readonly ChartEngine _engine;

    /// <summary>Receives the shared ephemeris fixture and builds a ChartEngine.</summary>
    public MoonConditionMovingTargetsTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    /// <summary>An event chart at a UT wall clock (Richmond, Virginia; the place does not move the Moon).</summary>
    private MoonConditionInfo MoonAtUt(int year, int month, int day, int hour) =>
        _engine.Calculate(new ChartInput
        {
            Name = $"Moon {year:D4}-{month:D2}-{day:D2} {hour:D2}:00 UT",
            LocalDateTime = new DateTime(year, month, day, hour, 0, 0),
            TimeZone = "Etc/UTC",
            Latitude = 37.5385087,
            Longitude = -77.43428,
            LocationName = "Richmond, Virginia",
            EventType = "Event",
        }).MoonCondition ?? throw new InvalidOperationException("the engine stored no Moon condition");

    [Fact]
    public void Calculate_Should_FindJupiterSquare_When_RetrogradeJupiterComesToMeetTheMoon()
    {
        // 2026-01-10 02:00 UT, Moon 13°24′ Libra. Held at their chart longitudes, Mars's square
        // point is the nearer (6.28° against Jupiter's 6.75°), so a fixed-target scan answers
        // Mars □. Moving, retrograde Jupiter (−0.14°/day) comes to meet the Moon while Mars
        // (+0.77°/day) runs ahead: the independent scan has Moon □ Jupiter exact
        // 2026-01-10 15:08:16 UT and Moon □ Mars only at 15:11:43.
        MoonConditionInfo moon = MoonAtUt(2026, 1, 10, 2);

        moon.IsVoidOfCourse.Should().BeFalse();
        moon.FirstApplyingPlanet.Should().Be("Jupiter");
        moon.FirstApplyingAspectType.Should().Be(AspectType.Square);
        moon.FirstApplyingOrb.Should().BeApproximately(6.7455, 0.0002,
            "the orb reported is the separation from exact at the chart instant");
    }

    [Fact]
    public void Calculate_Should_BeVoid_When_TheLastAspectInTheSignPerfectsOnlyAfterTheExit()
    {
        // Sweep row: | 2000-01-24 12:00 | Venus Square | - (VoC) | Moon leaves sign 01-25 09:09 |
        // The Moon (18°03′ Virgo) has 11.95° of Virgo left. Held still, Venus's square point is
        // 11.55° ahead, so a fixed-target scan answers Venus □. Venus moves on at +1.23°/day:
        // the square is exact 2000-01-25 10:29:46 UT, 81 minutes after the Moon enters Libra at
        // 09:09:09, and nothing else is exact before the exit. The Moon is void.
        MoonConditionInfo moon = MoonAtUt(2000, 1, 24, 12);

        moon.IsVoidOfCourse.Should().BeTrue();
        moon.FirstApplyingPlanet.Should().BeNull();
        moon.FirstApplyingAspectType.Should().BeNull();
        moon.FirstApplyingOrb.Should().BeNull();
    }

    [Fact]
    public void Calculate_Should_NotBeVoid_When_RetrogradeSaturnReturnsIntoTheMoonsSign()
    {
        // Sweep row: | 2000-10-16 00:00 | - (VoC) | Saturn Conjunction at 10-16 06:17 | Moon leaves sign 10-16 06:18 |
        // The Moon (26°20′ Taurus) has 3.667° of Taurus left. Saturn stands 0.002° into Gemini,
        // so held still its conjunction point lies past the sign boundary and a fixed-target
        // scan calls the Moon void. Saturn is retrograde (−0.056°/day) and recrosses into
        // Taurus: the conjunction is exact 2000-10-16 06:17:27 UT, 81 seconds before the Moon
        // leaves Taurus at 06:18:48.
        MoonConditionInfo moon = MoonAtUt(2000, 10, 16, 0);

        moon.IsVoidOfCourse.Should().BeFalse();
        moon.FirstApplyingPlanet.Should().Be("Saturn");
        moon.FirstApplyingAspectType.Should().Be(AspectType.Conjunction);
        moon.FirstApplyingOrb.Should().BeApproximately(3.6686, 0.0002);
    }
}
