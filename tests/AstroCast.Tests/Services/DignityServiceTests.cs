using FluentAssertions;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Services;
using AstroCast.Tests.Fixtures;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="DignityService"/> — essential dignity lookups using
/// the traditional (Ptolemaic) rulership system. All tests are pure unit tests
/// that require no Swiss Ephemeris or other external resources.
/// </summary>
public sealed class DignityServiceTests
{
    private readonly DignityService _service = new();

    #region Domicile Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDomicile_When_SunInLeo()
    {
        DignityInfo result = _service.GetDignity("Sun", ZodiacSign.Leo);

        result.IsDomicile.Should().BeTrue();
        result.Score.Should().Be(5);
        result.Label.Should().Be("Domicile");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDomicile_When_MoonInCancer()
    {
        DignityInfo result = _service.GetDignity("Moon", ZodiacSign.Cancer);

        result.IsDomicile.Should().BeTrue();
        result.Score.Should().Be(5);
        result.Label.Should().Be("Domicile");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDomicile_When_MercuryInGemini()
    {
        DignityInfo result = _service.GetDignity("Mercury", ZodiacSign.Gemini);

        result.IsDomicile.Should().BeTrue();
        result.Score.Should().Be(5);
        result.Label.Should().Be("Domicile");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDomicile_When_MercuryInVirgo()
    {
        // Mercury in Virgo is both domicile AND exalted in the traditional system.
        // Both flags are set; domicile takes precedence in Score and Label
        // because it is the higher dignity (+5 vs +4).
        DignityInfo result = _service.GetDignity("Mercury", ZodiacSign.Virgo);

        result.IsDomicile.Should().BeTrue();
        result.IsExalted.Should().BeTrue("Mercury is traditionally exalted in Virgo as well as domiciled");
        result.Score.Should().Be(5, "domicile (+5) takes precedence over exaltation (+4)");
        result.Label.Should().Be("Domicile");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDomicile_When_VenusInTaurus()
    {
        DignityInfo result = _service.GetDignity("Venus", ZodiacSign.Taurus);

        result.IsDomicile.Should().BeTrue();
        result.Score.Should().Be(5);
        result.Label.Should().Be("Domicile");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDomicile_When_VenusInLibra()
    {
        DignityInfo result = _service.GetDignity("Venus", ZodiacSign.Libra);

        result.IsDomicile.Should().BeTrue();
        result.Score.Should().Be(5);
        result.Label.Should().Be("Domicile");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDomicile_When_MarsInAries()
    {
        DignityInfo result = _service.GetDignity("Mars", ZodiacSign.Aries);

        result.IsDomicile.Should().BeTrue();
        result.Score.Should().Be(5);
        result.Label.Should().Be("Domicile");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDomicile_When_MarsInScorpio()
    {
        DignityInfo result = _service.GetDignity("Mars", ZodiacSign.Scorpio);

        result.IsDomicile.Should().BeTrue();
        result.Score.Should().Be(5);
        result.Label.Should().Be("Domicile");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDomicile_When_JupiterInSagittarius()
    {
        DignityInfo result = _service.GetDignity("Jupiter", ZodiacSign.Sagittarius);

        result.IsDomicile.Should().BeTrue();
        result.Score.Should().Be(5);
        result.Label.Should().Be("Domicile");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDomicile_When_JupiterInPisces()
    {
        DignityInfo result = _service.GetDignity("Jupiter", ZodiacSign.Pisces);

        result.IsDomicile.Should().BeTrue();
        result.Score.Should().Be(5);
        result.Label.Should().Be("Domicile");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDomicile_When_SaturnInCapricorn()
    {
        DignityInfo result = _service.GetDignity("Saturn", ZodiacSign.Capricorn);

        result.IsDomicile.Should().BeTrue();
        result.Score.Should().Be(5);
        result.Label.Should().Be("Domicile");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDomicile_When_SaturnInAquarius()
    {
        DignityInfo result = _service.GetDignity("Saturn", ZodiacSign.Aquarius);

        result.IsDomicile.Should().BeTrue();
        result.Score.Should().Be(5);
        result.Label.Should().Be("Domicile");
    }

    #endregion

    #region Exaltation Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnExalted_When_SunInAries()
    {
        DignityInfo result = _service.GetDignity("Sun", ZodiacSign.Aries);

        result.IsExalted.Should().BeTrue();
        result.Score.Should().Be(4);
        result.Label.Should().Be("Exalted");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnExalted_When_MoonInTaurus()
    {
        DignityInfo result = _service.GetDignity("Moon", ZodiacSign.Taurus);

        result.IsExalted.Should().BeTrue();
        result.Score.Should().Be(4);
        result.Label.Should().Be("Exalted");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnExalted_When_VenusInPisces()
    {
        DignityInfo result = _service.GetDignity("Venus", ZodiacSign.Pisces);

        result.IsExalted.Should().BeTrue();
        result.Score.Should().Be(4);
        result.Label.Should().Be("Exalted");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnExalted_When_MarsInCapricorn()
    {
        DignityInfo result = _service.GetDignity("Mars", ZodiacSign.Capricorn);

        result.IsExalted.Should().BeTrue();
        result.Score.Should().Be(4);
        result.Label.Should().Be("Exalted");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnExalted_When_JupiterInCancer()
    {
        DignityInfo result = _service.GetDignity("Jupiter", ZodiacSign.Cancer);

        result.IsExalted.Should().BeTrue();
        result.Score.Should().Be(4);
        result.Label.Should().Be("Exalted");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnExalted_When_SaturnInLibra()
    {
        DignityInfo result = _service.GetDignity("Saturn", ZodiacSign.Libra);

        result.IsExalted.Should().BeTrue();
        result.Score.Should().Be(4);
        result.Label.Should().Be("Exalted");
    }

    #endregion

    #region Detriment Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDetriment_When_SunInAquarius()
    {
        DignityInfo result = _service.GetDignity("Sun", ZodiacSign.Aquarius);

        result.IsInDetriment.Should().BeTrue();
        result.Score.Should().Be(-5);
        result.Label.Should().Be("Detriment");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDetriment_When_MoonInCapricorn()
    {
        DignityInfo result = _service.GetDignity("Moon", ZodiacSign.Capricorn);

        result.IsInDetriment.Should().BeTrue();
        result.Score.Should().Be(-5);
        result.Label.Should().Be("Detriment");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDetriment_When_MarsInLibra()
    {
        DignityInfo result = _service.GetDignity("Mars", ZodiacSign.Libra);

        result.IsInDetriment.Should().BeTrue();
        result.Score.Should().Be(-5);
        result.Label.Should().Be("Detriment");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDetriment_When_MarsInTaurus()
    {
        DignityInfo result = _service.GetDignity("Mars", ZodiacSign.Taurus);

        result.IsInDetriment.Should().BeTrue();
        result.Score.Should().Be(-5);
        result.Label.Should().Be("Detriment");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDetriment_When_VenusInAries()
    {
        DignityInfo result = _service.GetDignity("Venus", ZodiacSign.Aries);

        result.IsInDetriment.Should().BeTrue();
        result.Score.Should().Be(-5);
        result.Label.Should().Be("Detriment");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDetriment_When_VenusInScorpio()
    {
        DignityInfo result = _service.GetDignity("Venus", ZodiacSign.Scorpio);

        result.IsInDetriment.Should().BeTrue();
        result.Score.Should().Be(-5);
        result.Label.Should().Be("Detriment");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDetriment_When_JupiterInGemini()
    {
        DignityInfo result = _service.GetDignity("Jupiter", ZodiacSign.Gemini);

        result.IsInDetriment.Should().BeTrue();
        result.Score.Should().Be(-5);
        result.Label.Should().Be("Detriment");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnDetriment_When_SaturnInCancer()
    {
        DignityInfo result = _service.GetDignity("Saturn", ZodiacSign.Cancer);

        result.IsInDetriment.Should().BeTrue();
        result.Score.Should().Be(-5);
        result.Label.Should().Be("Detriment");
    }

    #endregion

    #region Fall Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnFall_When_SunInLibra()
    {
        DignityInfo result = _service.GetDignity("Sun", ZodiacSign.Libra);

        result.IsInFall.Should().BeTrue();
        result.Score.Should().Be(-4);
        result.Label.Should().Be("Fall");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnFall_When_MoonInScorpio()
    {
        DignityInfo result = _service.GetDignity("Moon", ZodiacSign.Scorpio);

        result.IsInFall.Should().BeTrue();
        result.Score.Should().Be(-4);
        result.Label.Should().Be("Fall");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnFall_When_MarsInCancer()
    {
        DignityInfo result = _service.GetDignity("Mars", ZodiacSign.Cancer);

        result.IsInFall.Should().BeTrue();
        result.Score.Should().Be(-4);
        result.Label.Should().Be("Fall");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnFall_When_VenusInVirgo()
    {
        DignityInfo result = _service.GetDignity("Venus", ZodiacSign.Virgo);

        result.IsInFall.Should().BeTrue();
        result.Score.Should().Be(-4);
        result.Label.Should().Be("Fall");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnFall_When_JupiterInCapricorn()
    {
        DignityInfo result = _service.GetDignity("Jupiter", ZodiacSign.Capricorn);

        result.IsInFall.Should().BeTrue();
        result.Score.Should().Be(-4);
        result.Label.Should().Be("Fall");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnFall_When_SaturnInAries()
    {
        DignityInfo result = _service.GetDignity("Saturn", ZodiacSign.Aries);

        result.IsInFall.Should().BeTrue();
        result.Score.Should().Be(-4);
        result.Label.Should().Be("Fall");
    }

    #endregion

    #region Peregrine Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnPeregrine_When_SunInGemini()
    {
        DignityInfo result = _service.GetDignity("Sun", ZodiacSign.Gemini);

        result.IsPeregrine.Should().BeTrue();
        result.Score.Should().Be(0);
        result.Label.Should().Be("Peregrine");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnPeregrine_When_MarsInLeo()
    {
        DignityInfo result = _service.GetDignity("Mars", ZodiacSign.Leo);

        result.IsPeregrine.Should().BeTrue();
        result.Score.Should().Be(0);
        result.Label.Should().Be("Peregrine");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnPeregrine_When_UranusInTaurus()
    {
        // Uranus has no traditional dignity — modern rulership of Aquarius
        // is not used in the Ptolemaic system.
        DignityInfo result = _service.GetDignity("Uranus", ZodiacSign.Taurus);

        result.IsPeregrine.Should().BeTrue();
        result.Score.Should().Be(0);
        result.Label.Should().Be("Peregrine");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnPeregrine_When_NeptuneInPisces()
    {
        // Neptune has no traditional dignity — modern rulership of Pisces
        // is not used in the Ptolemaic system.
        DignityInfo result = _service.GetDignity("Neptune", ZodiacSign.Pisces);

        result.IsPeregrine.Should().BeTrue();
        result.Score.Should().Be(0);
        result.Label.Should().Be("Peregrine");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnPeregrine_When_PlutoInAquarius()
    {
        // Pluto has no traditional dignity — modern rulership of Scorpio
        // is not used in the Ptolemaic system.
        DignityInfo result = _service.GetDignity("Pluto", ZodiacSign.Aquarius);

        result.IsPeregrine.Should().BeTrue();
        result.Score.Should().Be(0);
        result.Label.Should().Be("Peregrine");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnPeregrine_When_ChironInAries()
    {
        DignityInfo result = _service.GetDignity("Chiron", ZodiacSign.Aries);

        result.IsPeregrine.Should().BeTrue();
        result.Score.Should().Be(0);
        result.Label.Should().Be("Peregrine");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ReturnPeregrine_When_TrueNodeInPisces()
    {
        DignityInfo result = _service.GetDignity("True Node", ZodiacSign.Pisces);

        result.IsPeregrine.Should().BeTrue();
        result.Score.Should().Be(0);
        result.Label.Should().Be("Peregrine");
    }

    #endregion

    #region Dispositor / Sign Ruler Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSignRuler_Should_ReturnMars_When_Aries()
    {
        _service.GetSignRuler(ZodiacSign.Aries).Should().Be("Mars");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSignRuler_Should_ReturnVenus_When_Taurus()
    {
        _service.GetSignRuler(ZodiacSign.Taurus).Should().Be("Venus");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSignRuler_Should_ReturnMercury_When_Gemini()
    {
        _service.GetSignRuler(ZodiacSign.Gemini).Should().Be("Mercury");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSignRuler_Should_ReturnMoon_When_Cancer()
    {
        _service.GetSignRuler(ZodiacSign.Cancer).Should().Be("Moon");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSignRuler_Should_ReturnSun_When_Leo()
    {
        _service.GetSignRuler(ZodiacSign.Leo).Should().Be("Sun");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSignRuler_Should_ReturnMercury_When_Virgo()
    {
        _service.GetSignRuler(ZodiacSign.Virgo).Should().Be("Mercury");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSignRuler_Should_ReturnVenus_When_Libra()
    {
        _service.GetSignRuler(ZodiacSign.Libra).Should().Be("Venus");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSignRuler_Should_ReturnMars_When_Scorpio()
    {
        _service.GetSignRuler(ZodiacSign.Scorpio).Should().Be("Mars");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSignRuler_Should_ReturnJupiter_When_Sagittarius()
    {
        _service.GetSignRuler(ZodiacSign.Sagittarius).Should().Be("Jupiter");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSignRuler_Should_ReturnSaturn_When_Capricorn()
    {
        _service.GetSignRuler(ZodiacSign.Capricorn).Should().Be("Saturn");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSignRuler_Should_ReturnSaturn_When_Aquarius()
    {
        _service.GetSignRuler(ZodiacSign.Aquarius).Should().Be("Saturn");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSignRuler_Should_ReturnJupiter_When_Pisces()
    {
        _service.GetSignRuler(ZodiacSign.Pisces).Should().Be("Jupiter");
    }

    #endregion

    #region Dispositor Property Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_SetCorrectDispositor_When_MarsInCancer()
    {
        DignityInfo result = _service.GetDignity("Mars", ZodiacSign.Cancer);

        result.Dispositor.Should().Be("Moon", "Moon rules Cancer");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_SetCorrectDispositor_When_UranusInTaurus()
    {
        DignityInfo result = _service.GetDignity("Uranus", ZodiacSign.Taurus);

        result.Dispositor.Should().Be("Venus", "Venus rules Taurus, regardless of the planet occupying it");
    }

    #endregion

    #region GetDignities Collection Test

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignities_Should_ReturnDignityForEachPosition_When_FullChartProvided()
    {
        // Arrange — 12 planet positions with known signs
        List<PlanetPosition> positions =
        [
            CreatePosition(CelestialBodyId.Sun, "Sun", 135.0),           // Leo (domicile)
            CreatePosition(CelestialBodyId.Moon, "Moon", 95.0),          // Cancer (domicile)
            CreatePosition(CelestialBodyId.Mercury, "Mercury", 165.0),   // Virgo (domicile + exalted)
            CreatePosition(CelestialBodyId.Venus, "Venus", 215.0),       // Scorpio (detriment)
            CreatePosition(CelestialBodyId.Mars, "Mars", 95.0),          // Cancer (fall)
            CreatePosition(CelestialBodyId.Jupiter, "Jupiter", 65.0),    // Gemini (detriment)
            CreatePosition(CelestialBodyId.Saturn, "Saturn", 345.0),     // Pisces (peregrine — traditional detriment is Cancer/Leo? no — Saturn rules Cap/Aqu, detriment in Cancer/Leo)
            CreatePosition(CelestialBodyId.Uranus, "Uranus", 35.0),      // Taurus (peregrine)
            CreatePosition(CelestialBodyId.Neptune, "Neptune", 345.0),   // Pisces (peregrine)
            CreatePosition(CelestialBodyId.Pluto, "Pluto", 305.0),       // Aquarius (peregrine)
            CreatePosition(CelestialBodyId.TrueNode, "True Node", 345.0), // Pisces (peregrine)
            CreatePosition(CelestialBodyId.Chiron, "Chiron", 15.0),      // Aries (peregrine)
        ];

        // Act
        IReadOnlyList<DignityInfo> dignities = _service.GetDignities(positions);

        // Assert
        dignities.Should().HaveCount(12);
        dignities[0].Label.Should().Be("Domicile", "Sun in Leo");
        dignities[1].Label.Should().Be("Domicile", "Moon in Cancer");
        dignities[2].Label.Should().Be("Domicile", "Mercury in Virgo (domicile takes precedence)");
        dignities[3].Label.Should().Be("Detriment", "Venus in Scorpio");
        dignities[4].Label.Should().Be("Fall", "Mars in Cancer");
        dignities[5].Label.Should().Be("Detriment", "Jupiter in Gemini");
        dignities[6].Label.Should().Be("Peregrine", "Saturn in Pisces");
        dignities[7].Label.Should().Be("Peregrine", "Uranus in Taurus");
        dignities[8].Label.Should().Be("Peregrine", "Neptune in Pisces");
        dignities[9].Label.Should().Be("Peregrine", "Pluto in Aquarius");
        dignities[10].Label.Should().Be("Peregrine", "True Node in Pisces");
        dignities[11].Label.Should().Be("Peregrine", "Chiron in Aries");
    }

    #endregion

    #region Integration Test

    [Collection("Ephemeris")]
    [Trait("Category", "Integration")]
    public sealed class DignityIntegrationTests
    {
        private readonly ChartEngine _engine;
        private readonly DignityService _dignityService = new();

        public DignityIntegrationTests(EphemerisFixture fixture)
        {
            ILogger logger = new LoggerConfiguration().CreateLogger();
            AspectService aspects = new(fixture.EphemerisService, logger);
            MidpointService midpoints = new(logger);
            _engine = new ChartEngine(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
        }

        [Fact]
        public void GetDignities_Should_MatchExpectedDignities_When_KnownChartCalculated()
        {
            // J2000 epoch: 2000-01-01 12:00 UT at Greenwich
            ChartInput input = new()
            {
                Name = "J2000 Epoch",
                LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0),
                TimeZone = "UTC",
                Latitude = 51.4772,
                Longitude = -0.0005,
                LocationName = "Greenwich, UK",
                EventType = "Event",
            };

            ChartResult result = _engine.Calculate(input);

            // Verify dignities list is populated and matches position count
            result.Dignities.Should().HaveCount(result.Positions.Count);

            // Verify each dignity matches its corresponding position
            for (int i = 0; i < result.Positions.Count; i++)
            {
                result.Dignities[i].PlanetName.Should().Be(result.Positions[i].Name);
                result.Dignities[i].Sign.Should().Be(result.Positions[i].Sign);
            }

            // Spot-check known J2000 dignities:
            // Sun at ~280° = Capricorn → peregrine
            DignityInfo sunDignity = result.Dignities.First(d => d.PlanetName == "Sun");
            sunDignity.Sign.Should().Be(ZodiacSign.Capricorn);
            sunDignity.IsPeregrine.Should().BeTrue("Sun has no dignity in Capricorn");
            sunDignity.Dispositor.Should().Be("Saturn", "Saturn rules Capricorn");
        }
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ThrowArgumentException_When_PlanetNameIsNull()
    {
        Action act = () => _service.GetDignity(null!, ZodiacSign.Aries);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignity_Should_ThrowArgumentException_When_PlanetNameIsEmpty()
    {
        Action act = () => _service.GetDignity("", ZodiacSign.Aries);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignities_Should_ThrowArgumentNullException_When_PositionsIsNull()
    {
        Action act = () => _service.GetDignities(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignities_Should_ReturnEmptyList_When_PositionsIsEmpty()
    {
        IReadOnlyList<DignityInfo> result = _service.GetDignities([]);
        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetSignRuler_Should_ThrowArgumentOutOfRangeException_When_InvalidSign()
    {
        Action act = () => _service.GetSignRuler((ZodiacSign)99);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Helpers

    /// <summary>Creates a minimal PlanetPosition for testing.</summary>
    private static PlanetPosition CreatePosition(CelestialBodyId body, string name, double longitude)
    {
        return new PlanetPosition
        {
            Body = body,
            Name = name,
            Longitude = longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = 1.0,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
        };
    }

    #endregion

    #region Triplicity Ruler Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetTriplicityRulers_Should_ReturnFireRulers_When_Aries()
    {
        TriplicityRulerInfo result = _service.GetTriplicityRulers(ZodiacSign.Aries, isDayChart: true);

        result.Element.Should().Be("Fire");
        result.DayRuler.Should().Be("Sun");
        result.NightRuler.Should().Be("Jupiter");
        result.ParticipatingRuler.Should().Be("Saturn");
        result.ActiveRuler.Should().Be("Sun");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetTriplicityRulers_Should_ReturnNightRuler_When_NightChart()
    {
        TriplicityRulerInfo result = _service.GetTriplicityRulers(ZodiacSign.Leo, isDayChart: false);

        result.ActiveRuler.Should().Be("Jupiter"); // Night ruler for Fire
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetTriplicityRulers_Should_ReturnEarthRulers_When_Taurus()
    {
        TriplicityRulerInfo result = _service.GetTriplicityRulers(ZodiacSign.Taurus, isDayChart: true);

        result.Element.Should().Be("Earth");
        result.DayRuler.Should().Be("Venus");
        result.NightRuler.Should().Be("Moon");
        result.ParticipatingRuler.Should().Be("Mars");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetTriplicityRulers_Should_ReturnAirRulers_When_Gemini()
    {
        TriplicityRulerInfo result = _service.GetTriplicityRulers(ZodiacSign.Gemini, isDayChart: true);

        result.Element.Should().Be("Air");
        result.DayRuler.Should().Be("Saturn");
        result.NightRuler.Should().Be("Mercury");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetTriplicityRulers_Should_ReturnWaterRulers_When_Cancer()
    {
        TriplicityRulerInfo result = _service.GetTriplicityRulers(ZodiacSign.Cancer, isDayChart: true);

        result.Element.Should().Be("Water");
        result.DayRuler.Should().Be("Venus");
        result.NightRuler.Should().Be("Mars");
        result.ParticipatingRuler.Should().Be("Moon");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignityExtended_Should_SetTriplicityRuler_When_SunInAriesDay()
    {
        // Sun in Aries, day chart — Sun IS the fire triplicity day ruler
        DignityInfo result = _service.GetDignityExtended("Sun", ZodiacSign.Aries, 10.0, isDayChart: true);

        result.TriplicityRuler.Should().Be("Sun");
        result.IsTriplicityRuler.Should().BeTrue();
        result.Score.Should().Be(4); // Exalted takes precedence over triplicity
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignityExtended_Should_NotBeTriplicityRuler_When_MarsInAries()
    {
        // Mars in Aries (domicile) — Mars is NOT the fire triplicity day ruler (Sun is)
        DignityInfo result = _service.GetDignityExtended("Mars", ZodiacSign.Aries, 5.0, isDayChart: true);

        result.IsTriplicityRuler.Should().BeFalse();
        result.Score.Should().Be(5); // Domicile
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignityExtended_Should_ReturnTriplicityScore_When_OnlyTriplicity()
    {
        // Jupiter in Aries, night chart — Jupiter IS the fire triplicity night ruler
        // Jupiter is NOT domicile or exalted in Aries, so triplicity score (+3) applies
        DignityInfo result = _service.GetDignityExtended("Jupiter", ZodiacSign.Aries, 15.0, isDayChart: false);

        result.IsTriplicityRuler.Should().BeTrue();
        result.Score.Should().Be(3);
        result.Label.Should().Be("Triplicity");
    }

    #endregion

    #region Decan Ruler Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDecanRuler_Should_ReturnMars_When_FirstDecanAries()
    {
        // 0-10° Aries = first decan = Mars
        _service.GetDecanRuler(5.0).Should().Be("Mars");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDecanRuler_Should_ReturnSun_When_SecondDecanAries()
    {
        // 10-20° Aries = second decan = Sun
        _service.GetDecanRuler(15.0).Should().Be("Sun");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDecanRuler_Should_ReturnVenus_When_ThirdDecanAries()
    {
        // 20-30° Aries = third decan = Venus
        _service.GetDecanRuler(25.0).Should().Be("Venus");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDecanRuler_Should_ReturnMoon_When_SecondDecanTaurus()
    {
        // 10-20° Taurus = second decan = Moon
        // Taurus starts at 30°, so 40-50° = Moon
        _service.GetDecanRuler(45.0).Should().Be("Moon");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDecanRuler_Should_ReturnMars_When_ThirdDecanPisces()
    {
        // 20-30° Pisces = third decan = Mars
        // Pisces starts at 330°, so 350-360° = Mars
        _service.GetDecanRuler(355.0).Should().Be("Mars");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignityExtended_Should_SetDecanFields()
    {
        // Mars at 5° Aries — Mars IS the decan ruler of first decan Aries
        DignityInfo result = _service.GetDignityExtended("Mars", ZodiacSign.Aries, 5.0, isDayChart: true);

        result.DecanRuler.Should().Be("Mars");
        result.IsInOwnDecan.Should().BeTrue();
        result.Score.Should().Be(5); // Domicile takes precedence
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignityExtended_Should_ReturnFaceScore_When_OnlyDecan()
    {
        // Venus at 25° Aries — decan ruler is Venus! Venus is not domicile/exalted/detriment/fall in Aries
        DignityInfo result = _service.GetDignityExtended("Venus", ZodiacSign.Aries, 25.0, isDayChart: true);

        result.IsInOwnDecan.Should().BeTrue();
        result.IsDomicile.Should().BeFalse();
        result.IsExalted.Should().BeFalse();
        result.IsTriplicityRuler.Should().BeFalse(); // Sun is fire day ruler, not Venus
        result.Score.Should().Be(1);
        result.Label.Should().Be("Face");
    }

    #endregion

    #region Element Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetElement_Should_ReturnCorrectElements()
    {
        _service.GetElement(ZodiacSign.Aries).Should().Be("Fire");
        _service.GetElement(ZodiacSign.Taurus).Should().Be("Earth");
        _service.GetElement(ZodiacSign.Gemini).Should().Be("Air");
        _service.GetElement(ZodiacSign.Cancer).Should().Be("Water");
        _service.GetElement(ZodiacSign.Leo).Should().Be("Fire");
        _service.GetElement(ZodiacSign.Virgo).Should().Be("Earth");
        _service.GetElement(ZodiacSign.Libra).Should().Be("Air");
        _service.GetElement(ZodiacSign.Scorpio).Should().Be("Water");
        _service.GetElement(ZodiacSign.Sagittarius).Should().Be("Fire");
        _service.GetElement(ZodiacSign.Capricorn).Should().Be("Earth");
        _service.GetElement(ZodiacSign.Aquarius).Should().Be("Air");
        _service.GetElement(ZodiacSign.Pisces).Should().Be("Water");
    }

    #endregion

    #region Extended IsPeregrine Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignityExtended_Should_NotBePeregrine_When_TriplicityRuler()
    {
        // Jupiter in Aries, night chart — triplicity ruler
        DignityInfo result = _service.GetDignityExtended("Jupiter", ZodiacSign.Aries, 15.0, isDayChart: false);

        result.IsPeregrine.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignityExtended_Should_NotBePeregrine_When_InOwnDecan()
    {
        // Venus at 25° Aries — own decan
        DignityInfo result = _service.GetDignityExtended("Venus", ZodiacSign.Aries, 25.0, isDayChart: true);

        result.IsPeregrine.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetDignityExtended_Should_BePeregrine_When_NoDignitiesAtAll()
    {
        // Mercury in Leo at 15° (second decan = Jupiter, not Mercury), day chart
        // Mercury is NOT domicile, exalted, detriment, or fall in Leo
        // Fire triplicity day ruler = Sun (not Mercury), so not triplicity ruler
        // Second decan Leo = Jupiter (not Mercury), so not in own decan
        DignityInfo result = _service.GetDignityExtended("Mercury", ZodiacSign.Leo, 135.0, isDayChart: true);

        result.IsDomicile.Should().BeFalse();
        result.IsExalted.Should().BeFalse();
        result.IsTriplicityRuler.Should().BeFalse();
        result.IsInOwnDecan.Should().BeFalse();
        result.IsInDetriment.Should().BeFalse();
        result.IsInFall.Should().BeFalse();
        result.IsPeregrine.Should().BeTrue();
    }

    #endregion
}
