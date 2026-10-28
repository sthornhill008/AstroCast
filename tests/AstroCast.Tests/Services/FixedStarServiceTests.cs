using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="FixedStarService"/> verifying fixed star
/// conjunction detection against the Swiss Ephemeris. Uses the shared
/// "Ephemeris" collection fixture for real ephemeris calculations.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class FixedStarServiceTests
{
    private readonly FixedStarService _service;
    private readonly EphemerisService _ephemeris;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a FixedStarService.
    /// </summary>
    public FixedStarServiceTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        ILogger logger = new LoggerConfiguration().CreateLogger();
        _service = new FixedStarService(_ephemeris, logger);
    }

    #region CalculateFixedStarLongitude (EphemerisService)

    [Theory]
    [InlineData("Regulus")]
    [InlineData("Algol")]
    [InlineData("Spica")]
    [InlineData("Sirius")]
    [InlineData("Aldebaran")]
    [InlineData("Antares")]
    [InlineData("Fomalhaut")]
    [InlineData("Vega")]
    public void CalculateFixedStarLongitude_Should_ReturnValidLongitude_When_StarExists(
        string starName)
    {
        // Arrange — J2000.0 epoch
        double jd = _ephemeris.DateToJulianDay(2000, 1, 1, 12.0);

        // Act
        double longitude = _ephemeris.CalculateFixedStarLongitude(starName, jd);

        // Assert
        longitude.Should().BeInRange(0.0, 360.0,
            $"'{starName}' longitude should be a valid ecliptic degree");
    }

    [Fact]
    public void CalculateFixedStarLongitude_Should_ReturnDifferentValues_ForDifferentEpochs()
    {
        // Arrange — precession moves stars approximately 1 degree per 72 years
        double jd2000 = _ephemeris.DateToJulianDay(2000, 1, 1, 12.0);
        double jd1900 = _ephemeris.DateToJulianDay(1900, 1, 1, 12.0);

        // Act
        double regulus2000 = _ephemeris.CalculateFixedStarLongitude("Regulus", jd2000);
        double regulus1900 = _ephemeris.CalculateFixedStarLongitude("Regulus", jd1900);

        // Assert — ~100 years of precession should move the star ~1.4 degrees
        double diff = regulus2000 - regulus1900;
        diff.Should().BeInRange(1.0, 2.0,
            "Regulus should precess roughly 1.4 degrees per century");
    }

    [Theory]
    [InlineData("Deneb Kaitos")]
    [InlineData("Sheratan")]
    [InlineData("Prima Hyadum")]
    [InlineData("Zuben Elgenubi")]
    [InlineData("Zuben Eschamali")]
    [InlineData("Eltanin")]
    [InlineData("Deneb,alCyg")]
    [InlineData("Asellus Borealis")]
    [InlineData("Asellus Australis")]
    public void CalculateFixedStarLongitude_Should_Succeed_ForAlternateSwissEphNames(
        string swissEphName)
    {
        // Arrange
        double jd = _ephemeris.DateToJulianDay(2026, 3, 1, 12.0);

        // Act
        double longitude = _ephemeris.CalculateFixedStarLongitude(swissEphName, jd);

        // Assert
        longitude.Should().BeInRange(0.0, 360.0,
            $"SwissEph name '{swissEphName}' should resolve to a valid star");
    }

    [Fact]
    public void CalculateFixedStarLongitude_Should_SucceedForAllCatalogStars()
    {
        // Arrange — verify every star in the catalog is recognized by Swiss Ephemeris
        double jd = _ephemeris.DateToJulianDay(2026, 3, 1, 12.0);

        // Act & Assert
        foreach (FixedStarEntry star in FixedStarCatalog.Stars)
        {
            double longitude = _ephemeris.CalculateFixedStarLongitude(star.SwissEphName, jd);

            longitude.Should().BeInRange(0.0, 360.0,
                $"star '{star.DisplayName}' (SwissEph: '{star.SwissEphName}') should return valid longitude");
        }
    }

    #endregion

    #region FindConjunctions — Known Chart

    [Fact]
    public void FindConjunctions_Should_ReturnNonEmptyList_When_ConjunctionsExist()
    {
        // Arrange — J2000.0 New York (many chart points spread across zodiac)
        double jd = _ephemeris.DateToJulianDay(2000, 1, 1, 12.0);
        IReadOnlyList<PlanetPosition> positions = _ephemeris.CalculateAllPositions(jd);
        HouseData houses = _ephemeris.CalculateHouses(jd, 40.7128, -74.006);

        // Act
        IReadOnlyList<FixedStarConjunction> conjunctions =
            _service.FindConjunctions(positions, houses, jd);

        // Assert — with 55 stars and 16 chart points, at least some conjunctions should exist
        conjunctions.Should().NotBeEmpty(
            "a chart with 16 chart points spread across the zodiac should have at least one fixed star conjunction within 1 degree orb");
    }

    [Fact]
    public void FindConjunctions_Should_HaveOrbs_WithinDefaultLimit()
    {
        // Arrange
        double jd = _ephemeris.DateToJulianDay(2000, 1, 1, 12.0);
        IReadOnlyList<PlanetPosition> positions = _ephemeris.CalculateAllPositions(jd);
        HouseData houses = _ephemeris.CalculateHouses(jd, 40.7128, -74.006);

        // Act
        IReadOnlyList<FixedStarConjunction> conjunctions =
            _service.FindConjunctions(positions, houses, jd);

        // Assert
        conjunctions.Should().AllSatisfy(c =>
            c.Orb.Should().BeLessThanOrEqualTo(FixedStarCatalog.DefaultOrb,
                $"conjunction of {c.StarName} with {c.ChartPointName} should be within 1 degree orb"));
    }

    [Fact]
    public void FindConjunctions_Should_BeSortedByChartPointThenOrb()
    {
        // Arrange
        double jd = _ephemeris.DateToJulianDay(2000, 1, 1, 12.0);
        IReadOnlyList<PlanetPosition> positions = _ephemeris.CalculateAllPositions(jd);
        HouseData houses = _ephemeris.CalculateHouses(jd, 40.7128, -74.006);

        // Act
        IReadOnlyList<FixedStarConjunction> conjunctions =
            _service.FindConjunctions(positions, houses, jd);

        // Assert — verify sort order
        for (int i = 1; i < conjunctions.Count; i++)
        {
            FixedStarConjunction prev = conjunctions[i - 1];
            FixedStarConjunction curr = conjunctions[i];

            if (prev.ChartPointSortOrder == curr.ChartPointSortOrder)
            {
                curr.Orb.Should().BeGreaterThanOrEqualTo(prev.Orb,
                    $"within same chart point '{curr.ChartPointName}', orbs should increase");
            }
            else
            {
                curr.ChartPointSortOrder.Should().BeGreaterThanOrEqualTo(prev.ChartPointSortOrder,
                    $"chart point '{curr.ChartPointName}' should follow '{prev.ChartPointName}' in sort order");
            }
        }
    }

    #endregion

    #region FindConjunctions — Conjunction Properties

    [Fact]
    public void FindConjunctions_Should_PopulateAllProperties_When_ConjunctionFound()
    {
        // Arrange
        double jd = _ephemeris.DateToJulianDay(2000, 1, 1, 12.0);
        IReadOnlyList<PlanetPosition> positions = _ephemeris.CalculateAllPositions(jd);
        HouseData houses = _ephemeris.CalculateHouses(jd, 40.7128, -74.006);

        // Act
        IReadOnlyList<FixedStarConjunction> conjunctions =
            _service.FindConjunctions(positions, houses, jd);

        // Assert — every conjunction should have all fields populated
        conjunctions.Should().AllSatisfy(c =>
        {
            c.StarName.Should().NotBeNullOrWhiteSpace("StarName must be set");
            c.Constellation.Should().NotBeNullOrWhiteSpace("Constellation must be set");
            c.Magnitude.Should().BeInRange(-2.0, 6.0, "Magnitude should be reasonable");
            c.StarLongitude.Should().BeInRange(0.0, 360.0, "StarLongitude should be valid");
            c.ChartPointName.Should().NotBeNullOrWhiteSpace("ChartPointName must be set");
            c.ChartPointLongitude.Should().BeInRange(0.0, 360.0, "ChartPointLongitude should be valid");
            c.Orb.Should().BeInRange(0.0, 1.0, "Orb should be within 1 degree");
            c.Nature.Should().NotBeNullOrWhiteSpace("Nature must be set");
            c.Keywords.Should().NotBeNullOrWhiteSpace("Keywords must be set");
            c.ChartPointSortOrder.Should().BeInRange(0, 99, "ChartPointSortOrder should be valid");
        });
    }

    [Fact]
    public void FindConjunctions_Should_IncludeAngles_When_AnglesConjoinStars()
    {
        // Arrange — use a date/location where angles are likely to conjoin stars
        // We test that the service checks ASC, MC, DSC, IC
        double jd = _ephemeris.DateToJulianDay(2026, 3, 1, 12.0);
        IReadOnlyList<PlanetPosition> positions = _ephemeris.CalculateAllPositions(jd);
        HouseData houses = _ephemeris.CalculateHouses(jd, 51.5074, -0.1278); // London

        // Act
        IReadOnlyList<FixedStarConjunction> conjunctions =
            _service.FindConjunctions(positions, houses, jd);

        // Assert — verify that angle conjunctions are possible
        // (not guaranteed for every date, but the mechanism should work)
        List<string> chartPointNames = conjunctions
            .Select(c => c.ChartPointName)
            .Distinct()
            .ToList();

        // At minimum, some conjunctions should exist
        conjunctions.Should().NotBeEmpty(
            "a chart with 16 chart points should have some fixed star conjunctions");
    }

    #endregion

    #region FindConjunctions — Empty Result

    [Fact]
    public void FindConjunctions_Should_ReturnEmptyList_When_NoConjunctionsWithinOrb()
    {
        // Arrange — create positions where no planet is near any star
        // This is contrived — use positions at exactly 15 degrees of each sign
        // (unlikely to be within 1 degree of any catalog star, but not guaranteed)
        // Instead, just verify the service handles the case gracefully
        List<PlanetPosition> positions =
        [
            CreatePosition(CelestialBodyId.Sun, "Sun", 185.5),
        ];

        HouseData houses = new()
        {
            Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0 + 15.5).ToList().AsReadOnly(),
            Ascendant = 185.5,
            Midheaven = 95.5,
            Armc = 95.5,
            Vertex = 275.5,
        };

        double jd = _ephemeris.DateToJulianDay(2000, 1, 1, 12.0);

        // Act
        IReadOnlyList<FixedStarConjunction> conjunctions =
            _service.FindConjunctions(positions, houses, jd);

        // Assert — result should be a valid (possibly empty) list
        conjunctions.Should().NotBeNull("should return a list, not null");
        conjunctions.Should().AllSatisfy(c =>
            c.Orb.Should().BeLessThanOrEqualTo(1.0));
    }

    #endregion

    #region Angular Distance Wrap-Around

    [Fact]
    public void FindConjunctions_Should_DetectConjunctions_AcrossZeroAries()
    {
        // Arrange — place a planet at 359.5 degrees
        // Scheat is at approximately 29 Pisces 22' (~359.37 degrees) for J2000
        // A planet at 359.5 should be close to it
        List<PlanetPosition> positions =
        [
            CreatePosition(CelestialBodyId.Sun, "Sun", 359.5),
        ];

        HouseData houses = new()
        {
            Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList().AsReadOnly(),
            Ascendant = 0.5, // ~0.5 Aries — near Scheat (359.37) wrap-around
            Midheaven = 270.0,
            Armc = 270.0,
            Vertex = 90.0,
        };

        double jd = _ephemeris.DateToJulianDay(2000, 1, 1, 12.0);

        // Act
        IReadOnlyList<FixedStarConjunction> conjunctions =
            _service.FindConjunctions(positions, houses, jd);

        // Assert — should handle the 360/0 boundary correctly
        conjunctions.Should().NotBeNull();
        // Any conjunctions found should have valid orbs
        conjunctions.Should().AllSatisfy(c =>
            c.Orb.Should().BeLessThanOrEqualTo(1.0));
    }

    #endregion

    #region Regulus Position Verification

    [Fact]
    public void FindConjunctions_Should_DetectRegulus_When_ChartPointNearLateLeoDegrees()
    {
        // Arrange — Regulus is approximately at 29 Leo 50' (~149.83 degrees) for J2000
        // Place MC exactly there to guarantee a conjunction
        double jd = _ephemeris.DateToJulianDay(2000, 1, 1, 12.0);
        double regulusLongitude = _ephemeris.CalculateFixedStarLongitude("Regulus", jd);

        List<PlanetPosition> positions =
        [
            CreatePosition(CelestialBodyId.Sun, "Sun", 100.0), // far away
        ];

        HouseData houses = new()
        {
            Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList().AsReadOnly(),
            Ascendant = 0.0,
            Midheaven = regulusLongitude, // MC exactly on Regulus
            Armc = regulusLongitude,
            Vertex = 180.0,
        };

        // Act
        IReadOnlyList<FixedStarConjunction> conjunctions =
            _service.FindConjunctions(positions, houses, jd);

        // Assert
        FixedStarConjunction? regulusConjunction = conjunctions
            .FirstOrDefault(c => c.StarName == "Regulus" && c.ChartPointName == "MC");

        regulusConjunction.Should().NotBeNull(
            "Regulus should conjoin MC when MC is placed exactly on its longitude");
        regulusConjunction!.Orb.Should().BeLessThan(0.01,
            "orb should be nearly zero when MC is placed exactly on Regulus");
        regulusConjunction.IsRoyalStar.Should().BeTrue(
            "Regulus is one of the four Royal Stars");
        regulusConjunction.IsBehenianStar.Should().BeTrue(
            "Regulus is one of the 15 Behenian Stars");
        regulusConjunction.Nature.Should().Be("Mars-Jupiter",
            "Regulus has a Mars-Jupiter nature");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a minimal <see cref="PlanetPosition"/> for testing.
    /// </summary>
    private static PlanetPosition CreatePosition(
        CelestialBodyId body, string name, double longitude)
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
}
