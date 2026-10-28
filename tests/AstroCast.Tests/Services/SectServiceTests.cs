using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="SectService"/> — planetary sect analysis including
/// in-sect/out-of-sect status, benefic/malefic classification, and hayz.
/// All tests are pure unit tests requiring no external resources.
/// </summary>
public sealed class SectServiceTests
{
    private readonly SectService _service = new();

    #region Natural Sect Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ClassifySunAsDiurnal()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Moon", 50.0, 2));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        SectInfo sun = result.First(s => s.PlanetName == "Sun");
        sun.NaturalSect.Should().Be("Diurnal");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ClassifyMoonAsNocturnal()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Moon", 50.0, 2));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        SectInfo moon = result.First(s => s.PlanetName == "Moon");
        moon.NaturalSect.Should().Be("Nocturnal");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ClassifyJupiterAsDiurnal()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Jupiter", 200.0, 8));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        SectInfo jupiter = result.First(s => s.PlanetName == "Jupiter");
        jupiter.NaturalSect.Should().Be("Diurnal");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ClassifySaturnAsDiurnal()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Saturn", 250.0, 9));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        SectInfo saturn = result.First(s => s.PlanetName == "Saturn");
        saturn.NaturalSect.Should().Be("Diurnal");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ClassifyVenusAsNocturnal()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Venus", 100.0, 5));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        SectInfo venus = result.First(s => s.PlanetName == "Venus");
        venus.NaturalSect.Should().Be("Nocturnal");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ClassifyMarsAsNocturnal()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Mars", 300.0, 12));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        SectInfo mars = result.First(s => s.PlanetName == "Mars");
        mars.NaturalSect.Should().Be("Nocturnal");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ClassifyMercuryAsDiurnal_When_TenDegreesBehindSun()
    {
        // Mercury at 110°, Sun at 120° — Mercury behind the Sun in the zodiac rises before it:
        // a morning star, diurnal (Ptolemy, Tetrabiblos I.7; the Techniques reference, :95)
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Mercury", 110.0, 10));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        SectInfo mercury = result.First(s => s.PlanetName == "Mercury");
        mercury.NaturalSect.Should().Be("Diurnal");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ClassifyMercuryAsNocturnal_When_TenDegreesAheadOfSun()
    {
        // Mercury at 130°, Sun at 120° — Mercury ahead of the Sun in the zodiac rises after it:
        // an evening star, nocturnal
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Mercury", 130.0, 5));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        SectInfo mercury = result.First(s => s.PlanetName == "Mercury");
        mercury.NaturalSect.Should().Be("Nocturnal");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ClassifyMercuryAsDiurnal_When_BehindSunAcrossZeroAries()
    {
        // Sun at 5° Aries, Mercury at 25° Pisces (355°): the forward arc from the Sun to Mercury
        // is 350°, so Mercury is 10° behind the Sun across 0° Aries — a morning star, diurnal
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 5.0, 10), ("Mercury", 355.0, 10));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        SectInfo mercury = result.First(s => s.PlanetName == "Mercury");
        mercury.NaturalSect.Should().Be("Diurnal");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ClassifyMercuryAsNocturnal_When_AheadOfSunAcrossZeroAries()
    {
        // Sun at 25° Pisces (355°), Mercury at 5° Aries: the forward arc is 10°, so Mercury is
        // 10° ahead of the Sun across 0° Aries — an evening star, nocturnal
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 355.0, 10), ("Mercury", 5.0, 10));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        SectInfo mercury = result.First(s => s.PlanetName == "Mercury");
        mercury.NaturalSect.Should().Be("Nocturnal");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ClassifyMercuryAsNocturnal_When_ExactlyConjunctSun()
    {
        // A forward arc of exactly 0° is not greater than 180°, so the rule gives nocturnal
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Mercury", 120.0, 10));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        SectInfo mercury = result.First(s => s.PlanetName == "Mercury");
        mercury.NaturalSect.Should().Be("Nocturnal");
    }

    #endregion

    #region In-Sect / Out-of-Sect Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_MarkSunInSect_When_DayChart()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        result.First(s => s.PlanetName == "Sun").IsInSect.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_MarkSunOutOfSect_When_NightChart()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 4));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: false);

        result.First(s => s.PlanetName == "Sun").IsInSect.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_MarkMoonInSect_When_NightChart()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 4), ("Moon", 50.0, 2));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: false);

        result.First(s => s.PlanetName == "Moon").IsInSect.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_MarkMorningMercuryInSect_When_DayChart()
    {
        // Mercury 10° behind the Sun is a morning star, diurnal: in sect by day
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Mercury", 110.0, 10));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        result.First(s => s.PlanetName == "Mercury").IsInSect.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_MarkMorningMercuryOutOfSect_When_NightChart()
    {
        // Mercury 10° behind the Sun is a morning star, diurnal: out of sect by night
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 4), ("Mercury", 110.0, 4));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: false);

        result.First(s => s.PlanetName == "Mercury").IsInSect.Should().BeFalse();
    }

    #endregion

    #region Malefic Classification Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_MarkSaturnAsInSectMalefic_When_DayChart()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Saturn", 250.0, 9), ("Mars", 300.0, 12));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        SectInfo saturn = result.First(s => s.PlanetName == "Saturn");
        saturn.IsInSectMalefic.Should().BeTrue();
        saturn.IsOutOfSectMalefic.Should().BeFalse();

        SectInfo mars = result.First(s => s.PlanetName == "Mars");
        mars.IsOutOfSectMalefic.Should().BeTrue();
        mars.IsInSectMalefic.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_MarkMarsAsInSectMalefic_When_NightChart()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 4), ("Saturn", 250.0, 9), ("Mars", 300.0, 3));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: false);

        SectInfo mars = result.First(s => s.PlanetName == "Mars");
        mars.IsInSectMalefic.Should().BeTrue();

        SectInfo saturn = result.First(s => s.PlanetName == "Saturn");
        saturn.IsOutOfSectMalefic.Should().BeTrue();
    }

    #endregion

    #region Benefic Classification Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_MarkJupiterAsInSectBenefic_When_DayChart()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Jupiter", 200.0, 8), ("Venus", 100.0, 5));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        result.First(s => s.PlanetName == "Jupiter").IsInSectBenefic.Should().BeTrue();
        result.First(s => s.PlanetName == "Venus").IsInSectBenefic.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_MarkVenusAsInSectBenefic_When_NightChart()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 4), ("Jupiter", 200.0, 8), ("Venus", 100.0, 5));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: false);

        result.First(s => s.PlanetName == "Venus").IsInSectBenefic.Should().BeTrue();
        result.First(s => s.PlanetName == "Jupiter").IsInSectBenefic.Should().BeFalse();
    }

    #endregion

    #region Hayz Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectHayz_When_DiurnalPlanetAboveHorizonInMasculineSign_DayChart()
    {
        // Jupiter (diurnal) in house 10 (above horizon) in Sagittarius (masculine/fire sign)
        // Sagittarius = 240-270°, longitude 250° = ~10° Sagittarius
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Jupiter", 250.0, 10));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        SectInfo jupiter = result.First(s => s.PlanetName == "Jupiter");
        jupiter.IsHayz.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectHayz_When_NocturnalPlanetBelowHorizonInFeminineSign_NightChart()
    {
        // Venus (nocturnal) in house 4 (below horizon) in Taurus (feminine/earth sign)
        // Taurus = 30-60°, longitude 45° = 15° Taurus
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 4), ("Venus", 45.0, 4));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: false);

        SectInfo venus = result.First(s => s.PlanetName == "Venus");
        venus.IsHayz.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotDetectHayz_When_DiurnalPlanetBelowHorizon()
    {
        // Jupiter (diurnal) in house 4 (below horizon) — hayz requires above horizon for diurnal
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Jupiter", 250.0, 4));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        result.First(s => s.PlanetName == "Jupiter").IsHayz.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotDetectHayz_When_DiurnalPlanetInFeminineSign()
    {
        // Jupiter (diurnal) above horizon (house 10) but in Cancer (feminine sign)
        // Cancer = 90-120°, longitude 100° = 10° Cancer
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Jupiter", 100.0, 10));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        result.First(s => s.PlanetName == "Jupiter").IsHayz.Should().BeFalse();
    }

    #endregion

    #region House of Joy Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_SetCorrectHouseOfJoy_ForAllClassicalPlanets()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 9), ("Moon", 50.0, 3), ("Mercury", 80.0, 1),
            ("Venus", 30.0, 5), ("Mars", 200.0, 6), ("Jupiter", 260.0, 11),
            ("Saturn", 300.0, 12));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        result.First(s => s.PlanetName == "Sun").HouseOfJoy.Should().Be(9);
        result.First(s => s.PlanetName == "Moon").HouseOfJoy.Should().Be(3);
        result.First(s => s.PlanetName == "Mercury").HouseOfJoy.Should().Be(1);
        result.First(s => s.PlanetName == "Venus").HouseOfJoy.Should().Be(5);
        result.First(s => s.PlanetName == "Mars").HouseOfJoy.Should().Be(6);
        result.First(s => s.PlanetName == "Jupiter").HouseOfJoy.Should().Be(11);
        result.First(s => s.PlanetName == "Saturn").HouseOfJoy.Should().Be(12);
    }

    #endregion

    #region Edge Cases

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_HandleEmptyPositions()
    {
        IReadOnlyList<SectInfo> result = _service.Analyze([], isDayChart: true);
        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_SkipNonClassicalPlanets()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0, 10), ("Uranus", 15.0, 7));

        IReadOnlyList<SectInfo> result = _service.Analyze(positions, isDayChart: true);

        result.Should().HaveCount(1);
        result[0].PlanetName.Should().Be("Sun");
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates a list of test PlanetPositions from name/longitude/house tuples.
    /// </summary>
    private static IReadOnlyList<PlanetPosition> CreatePositions(
        params (string Name, double Longitude, int House)[] specs)
    {
        return specs.Select(s => new PlanetPosition
        {
            Body = CelestialBodyId.Sun, // Body ID not used by SectService
            Name = s.Name,
            Longitude = s.Longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = 1.0,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
            House = s.House,
        }).ToList();
    }

    #endregion
}
