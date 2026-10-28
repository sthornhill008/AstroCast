using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Unit and integration tests for <see cref="ChartContextService"/>.
/// Unit tests use constructed positions, houses, and dignities.
/// Integration tests use the shared ephemeris fixture with real calculations.
/// </summary>
public sealed class ChartContextServiceTests
{
    private readonly ChartContextService _service;
    private readonly DignityService _dignityService;

    /// <summary>
    /// Creates a ChartContextService with a real DignityService for unit tests.
    /// </summary>
    public ChartContextServiceTests()
    {
        _dignityService = new DignityService();
        _service = new ChartContextService(_dignityService, new SectService());
    }

    #region Chart Ruler Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnMars_When_AscendantInAries()
    {
        // Aries starts at 0 degrees
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0); // 15 Aries

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.ChartRuler.Should().Be("Mars");
        result.AscendantSign.Should().Be(ZodiacSign.Aries);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnVenus_When_AscendantInTaurus()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(45.0); // 15 Taurus

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.ChartRuler.Should().Be("Venus");
        result.AscendantSign.Should().Be(ZodiacSign.Taurus);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnMercury_When_AscendantInGemini()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(75.0); // 15 Gemini

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.ChartRuler.Should().Be("Mercury");
        result.AscendantSign.Should().Be(ZodiacSign.Gemini);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnMoon_When_AscendantInCancer()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(105.0); // 15 Cancer

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.ChartRuler.Should().Be("Moon");
        result.AscendantSign.Should().Be(ZodiacSign.Cancer);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnSun_When_AscendantInLeo()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(135.0); // 15 Leo

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.ChartRuler.Should().Be("Sun");
        result.AscendantSign.Should().Be(ZodiacSign.Leo);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnVenus_When_AscendantInLibra()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(195.0); // 15 Libra

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.ChartRuler.Should().Be("Venus");
        result.AscendantSign.Should().Be(ZodiacSign.Libra);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnSaturn_When_AscendantInCapricorn()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(285.0); // 15 Capricorn

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.ChartRuler.Should().Be("Saturn");
        result.AscendantSign.Should().Be(ZodiacSign.Capricorn);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnJupiter_When_AscendantInPisces()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(345.0); // 15 Pisces

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.ChartRuler.Should().Be("Jupiter");
        result.AscendantSign.Should().Be(ZodiacSign.Pisces);
    }

    #endregion

    #region Sect Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnDayChart_When_SunInHouse10()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithSunHouse(10);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.IsDayChart.Should().BeTrue();
        result.SectLabel.Should().Be("Day");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnDayChart_When_SunInHouse7()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithSunHouse(7);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.IsDayChart.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnNightChart_When_SunInHouse1()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithSunHouse(1);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.IsDayChart.Should().BeFalse();
        result.SectLabel.Should().Be("Night");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnNightChart_When_SunInHouse6()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithSunHouse(6);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.IsDayChart.Should().BeFalse();
    }

    #endregion

    #region Event Significator Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnVenus_When_EventTypeIsWedding()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "Wedding");

        result.EventSignificator.Should().Be("Venus");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnJupiter_When_EventTypeIsBusinessLaunch()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "BusinessLaunch");

        result.EventSignificator.Should().Be("Jupiter");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnMercury_When_EventTypeIsContract()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "Contract");

        result.EventSignificator.Should().Be("Mercury");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnMoon_When_EventTypeIsRealEstate()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "RealEstate");

        result.EventSignificator.Should().Be("Moon");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnMars_When_EventTypeIsSurgery()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "Surgery");

        result.EventSignificator.Should().Be("Mars");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnSun_When_EventTypeIsPolitical()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "Political");

        result.EventSignificator.Should().Be("Sun");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnNull_When_EventTypeIsBirth()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "Birth");

        result.EventSignificator.Should().BeNull();
        result.EventSignificatorDignity.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnNull_When_EventTypeIsNull()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.EventSignificator.Should().BeNull();
        result.KeyHouses.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnKeyHouses_When_EventTypeIsWedding()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "Wedding");

        result.KeyHouses.Should().BeEquivalentTo(new[] { 1, 7, 5 });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnSaturn_When_EventTypeIsJobStart()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "JobStart");

        result.EventSignificator.Should().Be("Saturn");
        result.KeyHouses.Should().BeEquivalentTo(new[] { 1, 10, 6 });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnMoon_When_EventTypeIsRelocation()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "Relocation");

        result.EventSignificator.Should().Be("Moon");
        result.KeyHouses.Should().BeEquivalentTo(new[] { 1, 4, 7 });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnJupiter_When_EventTypeIsLegalFiling()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "LegalFiling");

        result.EventSignificator.Should().Be("Jupiter");
        result.KeyHouses.Should().BeEquivalentTo(new[] { 1, 7, 9 });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnJupiter_When_EventTypeIsInvestment()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "Investment");

        result.EventSignificator.Should().Be("Jupiter");
        result.KeyHouses.Should().BeEquivalentTo(new[] { 1, 2, 8 });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnSaturn_When_EventTypeIsOrganization()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "Organization");

        result.EventSignificator.Should().Be("Saturn");
        result.KeyHouses.Should().BeEquivalentTo(new[] { 1, 10, 11 });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnVenus_When_EventTypeIsCreativeProject()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "CreativeProject");

        result.EventSignificator.Should().Be("Venus");
        result.KeyHouses.Should().BeEquivalentTo(new[] { 1, 5, 10 });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnMercury_When_EventTypeIsTechnologyLaunch()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "TechnologyLaunch");

        result.EventSignificator.Should().Be("Mercury");
        result.KeyHouses.Should().BeEquivalentTo(new[] { 1, 10, 11 });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnMercury_When_EventTypeIsEducation()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "Education");

        result.EventSignificator.Should().Be("Mercury");
        result.KeyHouses.Should().BeEquivalentTo(new[] { 1, 9, 3 });
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnNull_When_EventTypeIsOther()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "Other");

        result.EventSignificator.Should().BeNull();
        result.KeyHouses.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnJupiter_When_EventTypeIsTravel()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, "Travel");

        result.EventSignificator.Should().Be("Jupiter");
        result.KeyHouses.Should().BeEquivalentTo(new[] { 1, 9, 3 });
    }

    #endregion

    #region Angular Planet Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectAngularPlanet_When_PlanetWithin5DegreesOfAsc()
    {
        HouseData houses = CreateHouses(ascendant: 100.0);
        List<PlanetPosition> positions = CreateMinimalPositions();
        positions.Add(CreatePosition("Venus", 102.0, house: 1));
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.AngularPlanets.Should().Contain(ap => ap.PlanetName == "Venus" && ap.Angle == "ASC");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectAngularPlanet_When_PlanetWithin5DegreesOfMC()
    {
        HouseData houses = CreateHouses(ascendant: 100.0, midheaven: 10.0);
        List<PlanetPosition> positions = CreateMinimalPositions();
        positions.Add(CreatePosition("Jupiter", 12.0, house: 10));
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.AngularPlanets.Should().Contain(ap => ap.PlanetName == "Jupiter" && ap.Angle == "MC");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotDetectAngular_When_PlanetMoreThan5DegreesFromAngle()
    {
        HouseData houses = CreateHouses(ascendant: 100.0, midheaven: 10.0);
        List<PlanetPosition> positions = CreateMinimalPositions();
        positions.Add(CreatePosition("Venus", 120.0, house: 2));
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.AngularPlanets.Should().NotContain(ap => ap.PlanetName == "Venus");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReturnMultipleAngular_When_TwoPlanetsNearAngles()
    {
        HouseData houses = CreateHouses(ascendant: 100.0, midheaven: 10.0);
        List<PlanetPosition> positions = CreateMinimalPositions();
        positions.Add(CreatePosition("Venus", 101.0, house: 1));
        positions.Add(CreatePosition("Mars", 8.0, house: 10));
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.AngularPlanets.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_OrderAngularByOrb_When_MultiplePlanetsDetected()
    {
        HouseData houses = CreateHouses(ascendant: 100.0, midheaven: 10.0);
        List<PlanetPosition> positions = CreateMinimalPositions();
        positions.Add(CreatePosition("Venus", 104.0, house: 1)); // 4 deg from ASC
        positions.Add(CreatePosition("Mars", 11.0, house: 10));  // 1 deg from MC
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        List<AngularPlanet> venusAndMars = result.AngularPlanets
            .Where(ap => ap.PlanetName is "Venus" or "Mars")
            .ToList();

        // Mars (1 deg orb) should come before Venus (4 deg orb)
        if (venusAndMars.Count >= 2)
        {
            int marsIndex = venusAndMars.FindIndex(ap => ap.PlanetName == "Mars");
            int venusIndex = venusAndMars.FindIndex(ap => ap.PlanetName == "Venus");
            marsIndex.Should().BeLessThan(venusIndex,
                "angular planets should be ordered by orb (tightest first)");
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_FlagBenefic_When_VenusIsAngular()
    {
        HouseData houses = CreateHouses(ascendant: 100.0);
        List<PlanetPosition> positions = CreateMinimalPositions();
        positions.Add(CreatePosition("Venus", 101.0, house: 1));
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        AngularPlanet? venus = result.AngularPlanets.FirstOrDefault(ap => ap.PlanetName == "Venus");
        venus.Should().NotBeNull();
        venus!.IsBenefic.Should().BeTrue();
        venus.IsMalefic.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_FlagMalefic_When_MarsIsAngular()
    {
        HouseData houses = CreateHouses(ascendant: 100.0);
        List<PlanetPosition> positions = CreateMinimalPositions();
        positions.Add(CreatePosition("Mars", 99.0, house: 1));
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        AngularPlanet? mars = result.AngularPlanets.FirstOrDefault(ap => ap.PlanetName == "Mars");
        mars.Should().NotBeNull();
        mars!.IsMalefic.Should().BeTrue();
        mars.IsBenefic.Should().BeFalse();
    }

    #endregion

    #region Mutual Reception Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectMutualReception_When_MarsInCancerAndMoonInAries()
    {
        HouseData houses = CreateHouses(ascendant: 15.0);
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 135.0, house: 5),     // Sun in Leo
            CreatePosition("Moon", 10.0, house: 1),      // Moon in Aries (ruled by Mars)
            CreatePosition("Mars", 100.0, house: 4),     // Mars in Cancer (ruled by Moon)
        ];
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.MutualReceptions.Should().ContainSingle();
        MutualReception mr = result.MutualReceptions[0];
        mr.Planet1.Should().BeOneOf("Moon", "Mars");
        mr.Planet2.Should().BeOneOf("Moon", "Mars");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotDetectMutualReception_When_NoPairsMatch()
    {
        HouseData houses = CreateHouses(ascendant: 15.0);
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 135.0, house: 5),     // Sun in Leo
            CreatePosition("Moon", 50.0, house: 2),      // Moon in Taurus
            CreatePosition("Mars", 200.0, house: 7),     // Mars in Libra
        ];
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.MutualReceptions.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_IgnoreOuterPlanets_When_CheckingMutualReception()
    {
        HouseData houses = CreateHouses(ascendant: 15.0);
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 135.0, house: 5),     // Sun in Leo
            CreatePosition("Moon", 50.0, house: 2),      // Moon in Taurus
            CreatePosition("Uranus", 15.0, house: 1),    // Uranus in Aries (outer planet)
            CreatePosition("Neptune", 345.0, house: 12), // Neptune in Pisces (outer planet)
        ];
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        // Uranus and Neptune have no traditional domicile rulerships
        result.MutualReceptions.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectMultiple_When_TwoReceptionPairsExist()
    {
        HouseData houses = CreateHouses(ascendant: 15.0);
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 135.0, house: 5),     // Sun in Leo
            CreatePosition("Moon", 10.0, house: 1),      // Moon in Aries (ruled by Mars)
            CreatePosition("Mars", 100.0, house: 4),     // Mars in Cancer (ruled by Moon) -> MR pair 1
            CreatePosition("Venus", 285.0, house: 10),   // Venus in Capricorn (ruled by Saturn)
            CreatePosition("Saturn", 35.0, house: 2),    // Saturn in Taurus (ruled by Venus) -> MR pair 2
        ];
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.MutualReceptions.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectDomicileReception_WithReceptionType()
    {
        // Moon in Aries (Mars rules Aries) + Mars in Cancer (Moon rules Cancer) = Domicile
        HouseData houses = CreateHouses(ascendant: 15.0);
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 280.0, house: 10),      // Sun in Capricorn (above horizon = day chart)
            CreatePosition("Moon", 10.0, house: 1),        // Moon in Aries
            CreatePosition("Mars", 100.0, house: 4),       // Mars in Cancer
        ];
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.MutualReceptions.Should().ContainSingle();
        MutualReception mr = result.MutualReceptions[0];
        mr.ReceptionType.Should().Be("Domicile");
        mr.Strength.Should().Be("Strongest");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectExaltationReception_When_BothExalted()
    {
        // Sun in Aries (exalted) + Mars in Capricorn (exalted)
        // Sun exalted in Aries, Mars exalted in Capricorn
        // Is Sun exalted in Mars's sign? Mars is in Capricorn, Sun exalted in Aries — no.
        // We need: each in the OTHER's exaltation sign.
        // Saturn exalted in Libra, Venus exalted in Pisces.
        // Saturn in Pisces (Venus exalted here) + Venus in Libra (Saturn exalted here) = Exaltation reception
        HouseData houses = CreateHouses(ascendant: 15.0);
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 280.0, house: 10),      // Sun in Capricorn (day chart)
            CreatePosition("Moon", 50.0, house: 2),        // Moon in Taurus
            CreatePosition("Saturn", 350.0, house: 12),    // Saturn in Pisces (Venus exalted here)
            CreatePosition("Venus", 195.0, house: 7),      // Venus in Libra (Saturn exalted here)
        ];
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.MutualReceptions.Should().Contain(mr => mr.ReceptionType == "Exaltation");
        MutualReception exaltation = result.MutualReceptions.First(mr => mr.ReceptionType == "Exaltation");
        exaltation.Strength.Should().Be("Strong");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectMixedDomicileExaltation_When_OneByEachType()
    {
        // Moon in Aries (Mars rules Aries — Moon in Mars's domicile)
        // Mars in Taurus (Moon exalted in Taurus — Mars in Moon's exaltation)
        // = Mixed (Domicile-Exaltation)
        HouseData houses = CreateHouses(ascendant: 15.0);
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 280.0, house: 10),      // Sun in Capricorn (day chart)
            CreatePosition("Moon", 10.0, house: 1),        // Moon in Aries (Mars domicile)
            CreatePosition("Mars", 40.0, house: 2),        // Mars in Taurus (Moon exaltation)
        ];
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.MutualReceptions.Should().Contain(mr => mr.ReceptionType == "Mixed (Domicile-Exaltation)");
        MutualReception mixed = result.MutualReceptions.First(mr => mr.ReceptionType == "Mixed (Domicile-Exaltation)");
        mixed.Strength.Should().Be("Strong");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_PreferStrongerReceptionType_WhenMultipleApply()
    {
        // Domicile should be detected before exaltation/triplicity for the same pair
        // Moon in Aries (Mars rules) + Mars in Cancer (Moon rules) = Domicile (not triplicity etc.)
        HouseData houses = CreateHouses(ascendant: 15.0);
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 280.0, house: 10),      // Sun in Capricorn (day chart)
            CreatePosition("Moon", 10.0, house: 1),        // Moon in Aries
            CreatePosition("Mars", 100.0, house: 4),       // Mars in Cancer
        ];
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.MutualReceptions.Should().ContainSingle();
        result.MutualReceptions[0].ReceptionType.Should().Be("Domicile",
            "domicile reception should be selected as the strongest type");
    }

    #endregion

    #region Chart Ruler Dignity Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReportChartRulerDignity_When_RulerIsDomicile()
    {
        // Ascendant in Aries (ruler = Mars), Mars in Aries (domicile)
        HouseData houses = CreateHouses(ascendant: 15.0);
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 135.0, house: 5),
            CreatePosition("Moon", 50.0, house: 2),
            CreatePosition("Mars", 10.0, house: 1), // Mars in Aries = domicile
        ];
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.ChartRulerDignity.IsDomicile.Should().BeTrue();
        result.ChartRulerDignity.Score.Should().Be(5);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReportChartRulerRetrograde_When_RulerIsRetrograde()
    {
        HouseData houses = CreateHouses(ascendant: 15.0); // Aries -> Mars
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 135.0, house: 5),
            CreatePosition("Moon", 50.0, house: 2),
            CreatePosition("Mars", 10.0, house: 1, speed: -0.5), // retrograde
        ];
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.IsChartRulerRetrograde.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ReportChartRulerHouse_When_RulerInHouse10()
    {
        HouseData houses = CreateHouses(ascendant: 15.0); // Aries -> Mars
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 135.0, house: 5),
            CreatePosition("Moon", 50.0, house: 2),
            CreatePosition("Mars", 280.0, house: 10),
        ];
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.ChartRulerHouse.Should().Be(10);
    }

    #endregion

    #region Sect and Triplicity Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_PopulatePlanetarySect()
    {
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.PlanetarySect.Should().NotBeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_PopulateAscendantTriplicityRulers()
    {
        // Ascendant at 15° Aries (Fire sign)
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithAscendant(15.0);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.AscendantTriplicityRulers.Should().NotBeNull();
        result.AscendantTriplicityRulers!.Element.Should().Be("Fire");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_SelectDayRulerAsActive_When_DayChart()
    {
        // Sun in house 10 (above horizon) = day chart, Ascendant in Aries (Fire)
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithSunHouse(10);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.IsDayChart.Should().BeTrue();
        result.AscendantTriplicityRulers.Should().NotBeNull();
        // Aries = Fire, day ruler = Sun
        result.AscendantTriplicityRulers!.ActiveRuler.Should().Be("Sun");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_SelectNightRulerAsActive_When_NightChart()
    {
        // Sun in house 4 (below horizon) = night chart, Ascendant in Aries (Fire)
        (IReadOnlyList<PlanetPosition> positions, HouseData houses, IReadOnlyList<DignityInfo> dignities) =
            CreateInputWithSunHouse(4);

        ChartContextInfo result = _service.Analyze(positions, houses, dignities, null);

        result.IsDayChart.Should().BeFalse();
        result.AscendantTriplicityRulers.Should().NotBeNull();
        // Aries = Fire, night ruler = Jupiter
        result.AscendantTriplicityRulers!.ActiveRuler.Should().Be("Jupiter");
    }

    #endregion

    #region Integration Test

    /// <summary>
    /// Integration tests using real Swiss Ephemeris calculations.
    /// </summary>
    [Collection("Ephemeris")]
    [Trait("Category", "Integration")]
    public sealed class IntegrationTests
    {
        private readonly ChartEngine _engine;

        /// <summary>
        /// Creates a ChartEngine using the shared ephemeris fixture.
        /// </summary>
        public IntegrationTests(EphemerisFixture fixture)
        {
            ILogger logger = new LoggerConfiguration().CreateLogger();
            AspectService aspects = new(fixture.EphemerisService, logger);
            MidpointService midpoints = new(logger);
            DignityService dignityService = new();
            _engine = new ChartEngine(
                fixture.EphemerisService, aspects, midpoints,
                dignityService, new MoonConditionService(),
                new ChartContextService(dignityService, new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
        }

        [Fact]
        public void Analyze_Should_ProduceValidContext_When_RealChartCalculated()
        {
            ChartInput input = new()
            {
                Name = "Context Integration Test",
                LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
                TimeZone = "America/New_York",
                Latitude = 40.7128,
                Longitude = -74.006,
                LocationName = "New York City",
                EventType = "Event",
            };

            ChartResult result = _engine.Calculate(input);

            result.ChartContext.Should().NotBeNull();
            result.ChartContext!.ChartRuler.Should().NotBeNullOrWhiteSpace();
            result.ChartContext.AscendantSign.Should().BeOneOf(
                ZodiacSign.Aries, ZodiacSign.Taurus, ZodiacSign.Gemini,
                ZodiacSign.Cancer, ZodiacSign.Leo, ZodiacSign.Virgo,
                ZodiacSign.Libra, ZodiacSign.Scorpio, ZodiacSign.Sagittarius,
                ZodiacSign.Capricorn, ZodiacSign.Aquarius, ZodiacSign.Pisces);
            result.ChartContext.ChartRulerDignity.Should().NotBeNull();
            result.ChartContext.ChartRulerHouse.Should().BeInRange(1, 12);
            result.ChartContext.AngularPlanets.Should().NotBeNull();
            result.ChartContext.MutualReceptions.Should().NotBeNull();
        }
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates test inputs with a specific Ascendant longitude.
    /// Includes Sun (in Leo, house 5) and Moon (in Taurus, house 2) as
    /// minimum positions, plus the chart ruler if it is Sun or Moon.
    /// </summary>
    private (IReadOnlyList<PlanetPosition> Positions, HouseData Houses, IReadOnlyList<DignityInfo> Dignities)
        CreateInputWithAscendant(double ascendantLongitude)
    {
        HouseData houses = CreateHouses(ascendant: ascendantLongitude);
        List<PlanetPosition> positions = CreateMinimalPositions();
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);
        return (positions, houses, dignities);
    }

    /// <summary>
    /// Creates test inputs with the Sun placed in a specific house: its longitude in the middle of
    /// that house, counted in 30° steps from the Ascendant at 15°, since day or night follows the
    /// Sun's longitude (houses 7 to 12 lie above the horizon).
    /// </summary>
    private (IReadOnlyList<PlanetPosition> Positions, HouseData Houses, IReadOnlyList<DignityInfo> Dignities)
        CreateInputWithSunHouse(int sunHouse)
    {
        const double ascendant = 15.0;
        HouseData houses = CreateHouses(ascendant: ascendant);
        double sunLongitude = (ascendant + (sunHouse - 1) * 30.0 + 15.0) % 360.0;
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", sunLongitude, house: sunHouse),
            CreatePosition("Moon", 50.0, house: 2),
        ];
        IReadOnlyList<DignityInfo> dignities = _dignityService.GetDignities(positions);
        return (positions, houses, dignities);
    }

    /// <summary>
    /// Creates minimal positions (Sun in Leo, Moon in Taurus) for basic tests.
    /// </summary>
    private static List<PlanetPosition> CreateMinimalPositions() =>
    [
        CreatePosition("Sun", 135.0, house: 5),
        CreatePosition("Moon", 50.0, house: 2),
    ];

    /// <summary>
    /// Creates a HouseData with the specified Ascendant and optional Midheaven.
    /// </summary>
    private static HouseData CreateHouses(double ascendant, double midheaven = 270.0) => new()
    {
        Cusps = Enumerable.Range(0, 12).Select(i => (ascendant + i * 30.0) % 360.0).ToList(),
        Ascendant = ascendant,
        Midheaven = midheaven,
        Armc = 0.0,
        Vertex = 0.0,
    };

    /// <summary>
    /// Creates a test PlanetPosition with the specified name, longitude, house, and speed.
    /// </summary>
    private static PlanetPosition CreatePosition(
        string name, double longitude, int house, double speed = 1.0) => new()
    {
        Body = CelestialBodyId.Sun, // body ID doesn't matter for context tests
        Name = name,
        Longitude = longitude,
        Latitude = 0.0,
        Distance = 1.0,
        SpeedInLongitude = speed,
        SpeedInLatitude = 0.0,
        SpeedInDistance = 0.0,
        House = house,
    };

    #endregion
}
