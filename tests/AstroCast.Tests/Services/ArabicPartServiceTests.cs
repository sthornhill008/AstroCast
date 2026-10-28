using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="ArabicPartService"/> — Arabic Parts (Lots) and
/// Hellenistic Lots calculation. All tests are pure unit tests.
/// </summary>
public sealed class ArabicPartServiceTests
{
    private readonly ArabicPartService _service;
    private readonly DignityService _dignity;

    public ArabicPartServiceTests()
    {
        _dignity = new DignityService();
        _service = new ArabicPartService(_dignity);
    }

    #region Part of Fortune Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_ComputePartOfFortune_DayChart()
    {
        // Asc=0°, Moon=90°, Sun=30° → Fortune = 0 + 90 - 30 = 60°
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        ArabicPart fortune = result.First(p => p.Name == "Part of Fortune");
        fortune.Longitude.Should().BeApproximately(60.0, 0.01);
        fortune.Group.Should().Be("Arabic");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_ReversePartOfFortune_NightChart()
    {
        // Asc=0°, Moon=90°, Sun=30° → Night: Fortune = 0 + Sun - Moon = 0 + 30 - 90 = -60 → 300°
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: false);

        ArabicPart fortune = result.First(p => p.Name == "Part of Fortune");
        fortune.Longitude.Should().BeApproximately(300.0, 0.01);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_NormalizeFortuneAcross360Boundary()
    {
        // Asc=350°, Moon=20°, Sun=300° → 350 + 20 - 300 = 70°
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 300.0, moonLon: 20.0),
            CreateHouses(ascendant: 350.0),
            isDayChart: true);

        ArabicPart fortune = result.First(p => p.Name == "Part of Fortune");
        fortune.Longitude.Should().BeApproximately(70.0, 0.01);
    }

    #endregion

    #region Part of Spirit Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_ComputePartOfSpirit_DayChart()
    {
        // Asc=0°, Sun=30°, Moon=90° → Spirit = 0 + 30 - 90 = -60 → 300°
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        ArabicPart spirit = result.First(p => p.Name == "Part of Spirit");
        spirit.Longitude.Should().BeApproximately(300.0, 0.01);
    }

    #endregion

    #region Non-Reversing Parts Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_NotReversePartOfDeath_NightChart()
    {
        // Part of Death does not reverse: Asc + Cusp8 - Moon (always)
        IReadOnlyList<ArabicPart> dayResult = _service.CalculateAll(
            CreatePositions(sunLon: 120.0, moonLon: 60.0),
            CreateHouses(ascendant: 0.0, midheaven: 270.0),
            isDayChart: true);

        IReadOnlyList<ArabicPart> nightResult = _service.CalculateAll(
            CreatePositions(sunLon: 120.0, moonLon: 60.0),
            CreateHouses(ascendant: 0.0, midheaven: 270.0),
            isDayChart: false);

        ArabicPart dayDeath = dayResult.First(p => p.Name == "Part of Death");
        ArabicPart nightDeath = nightResult.First(p => p.Name == "Part of Death");

        dayDeath.Longitude.Should().Be(nightDeath.Longitude);
    }

    #endregion

    #region Part of Vocation (MC-based) Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_UseMCAsBase_ForPartOfVocation()
    {
        // Part of Vocation = MC + Moon - Sun
        // MC=270°, Moon=90°, Sun=30° → 270 + 90 - 30 = 330°
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0),
            CreateHouses(ascendant: 0.0, midheaven: 270.0),
            isDayChart: true);

        ArabicPart vocation = result.First(p => p.Name == "Part of Vocation");
        vocation.Longitude.Should().BeApproximately(330.0, 0.01);
    }

    #endregion

    #region Part of Exaltation (Fixed Degree) Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_UseFixedDegree_ForPartOfExaltation()
    {
        // Part of Exaltation (day) = Asc + 19°Aries - Sun = 0 + 19 - 30 = -11 → 349°
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        ArabicPart exaltation = result.First(p => p.Name == "Part of Exaltation");
        exaltation.Longitude.Should().BeApproximately(349.0, 0.01);
    }

    #endregion

    #region Hermetic Lot Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_ComputeLotOfEros_ReferencingSpirit()
    {
        // Fortune = Asc + Moon - Sun = 0 + 90 - 30 = 60
        // Spirit = Asc + Sun - Moon = 0 + 30 - 90 = 300
        // Eros (day) = Asc + Venus - Spirit = 0 + 150 - 300 = -150 → 210°
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0, venusLon: 150.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        ArabicPart eros = result.First(p => p.Name == "Lot of Eros");
        eros.Longitude.Should().BeApproximately(210.0, 0.01);
        eros.Group.Should().Be("Hermetic");
    }

    /// <summary>
    /// The Lot of Necessity by day is Asc + Fortune − Mercury, after Paulus Alexandrinus (ch. 23; ruling L1).
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_ComputeLotOfNecessity_ReferencingFortune()
    {
        // Fortune = 0 + 90 - 30 = 60
        // Necessity (day, Paulus ch. 23) = Asc + Fortune - Mercury = 0 + 60 - 75 = -15 → 345°
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0, mercuryLon: 75.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        ArabicPart necessity = result.First(p => p.Name == "Lot of Necessity");
        necessity.Longitude.Should().BeApproximately(345.0, 0.01);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_ComputeLotOfVictory_ReferencingSpirit()
    {
        // Spirit = 0 + 30 - 90 = 300
        // Victory (day) = Asc + Jupiter - Spirit = 0 + 260 - 300 = -40 → 320°
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0, jupiterLon: 260.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        ArabicPart victory = result.First(p => p.Name == "Lot of Victory");
        victory.Longitude.Should().BeApproximately(320.0, 0.01);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_ComputeLotOfNemesis_ReferencingFortune()
    {
        // Fortune = 60
        // Nemesis (day) = Asc + Fortune - Saturn = 0 + 60 - 280 = -220 → 140°
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0, saturnLon: 280.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        ArabicPart nemesis = result.First(p => p.Name == "Lot of Nemesis");
        nemesis.Longitude.Should().BeApproximately(140.0, 0.01);
    }

    /// <summary>
    /// The Lots of Necessity and Courage follow Paulus Alexandrinus (ch. 23; ruling L1): by day Asc + Fortune − Mercury
    /// and Asc + Fortune − Mars, reversed by night to Asc + Mercury − Fortune and Asc + Mars − Fortune. With the Sun at
    /// 30°, the Moon at 90° and the Ascendant at 0°, Fortune is 60° by day and 300° by night; Mercury is at 75° and Mars
    /// at 200°. Until 1.3.3 both Lots took their factors the other way round: 15°, 225°, 140° and 100°.
    /// </summary>
    /// <param name="lot">The Lot's name.</param>
    /// <param name="time">"day" or "night".</param>
    /// <param name="expected">The Lot's longitude, in degrees.</param>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Lot of Necessity", "day", 345.0)]
    [InlineData("Lot of Necessity", "night", 135.0)]
    [InlineData("Lot of Courage", "day", 220.0)]
    [InlineData("Lot of Courage", "night", 260.0)]
    public void CalculateAll_NecessityAndCourage_FollowPaulus(string lot, string time, double expected)
    {
        string expectedFormula = (lot, time) switch
        {
            ("Lot of Necessity", "day") => "Asc + Fortune − Mercury",
            ("Lot of Necessity", "night") => "Asc + Mercury − Fortune",
            ("Lot of Courage", "day") => "Asc + Fortune − Mars",
            ("Lot of Courage", "night") => "Asc + Mars − Fortune",
            _ => throw new ArgumentOutOfRangeException(nameof(lot), lot, "Not a case of this test."),
        };

        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: time == "day");

        ArabicPart part = result.First(p => p.Name == lot);
        part.Longitude.Should().BeApproximately(expected, 1e-12);
        part.Formula.Should().Be(expectedFormula);
    }

    #endregion

    #region House Assignment Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_AssignHouse_ToEachPart()
    {
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        foreach (ArabicPart part in result)
        {
            part.House.Should().NotBeNull($"{part.Name} should have a house assignment");
            part.House.Should().BeInRange(1, 12);
        }
    }

    #endregion

    #region Sign and Ruler Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_AssignSignAndRuler()
    {
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        ArabicPart fortune = result.First(p => p.Name == "Part of Fortune");
        // Fortune at 60° = Gemini; ruler = Mercury
        fortune.Sign.Should().Be(ZodiacSign.Gemini);
        fortune.Ruler.Should().Be("Mercury");
    }

    #endregion

    #region Precision Tests

    /// <summary>
    /// A Lot keeps its full longitude (finding F8-2): with the Moon at 90.123456789°, the Sun at 30° and the
    /// Ascendant at 0° in a day chart, Fortune is 60.123456789°. Until 1.3.3 a Lot was rounded to 0.0001°, here
    /// 60.1235°, and the display then truncated the rounded value to the arcsecond.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_LotLongitude_IsNotRounded()
    {
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.123456789),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        result.First(p => p.Name == "Part of Fortune").Longitude.Should().BeApproximately(60.123456789, 1e-12);
        result.First(p => p.Name == "Lot of Fortune").Longitude.Should().BeApproximately(60.123456789, 1e-12);
    }

    /// <summary>
    /// A Lot 0.16″ before the end of Aquarius (finding F8-3): with the Moon at 329.99995667°, and the Sun and the
    /// Ascendant at 0°, in a day chart, Fortune is 329.99995667°: in Aquarius, ruled by Saturn, printed
    /// 29°59′59″ Aquarius. Until 1.3.3 the longitude was rounded to 330.0000°, so its sign read Pisces and its
    /// position 00°00′00″ Pisces, while its ruler, taken from the unrounded longitude, read Saturn.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_LotJustBeforeTheEndOfAquarius_SignRulerAndPositionAgree()
    {
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 0.0, moonLon: 329.99995667),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        ArabicPart fortune = result.First(p => p.Name == "Part of Fortune");
        fortune.Longitude.Should().BeApproximately(329.99995667, 1e-12);
        fortune.Sign.Should().Be(ZodiacSign.Aquarius);
        fortune.Ruler.Should().Be("Saturn");
        PositionFormatter.FormatLongitude(fortune.Longitude).Should().Be("29°59′59″ Aquarius");
    }

    #endregion

    #region Catalog Integrity Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Catalog_Should_Have15Definitions()
    {
        ArabicPartCatalog.All.Should().HaveCount(15);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Catalog_Should_Have8ArabicAnd7Hermetic()
    {
        ArabicPartCatalog.All.Count(d => d.Group == "Arabic").Should().Be(8);
        ArabicPartCatalog.All.Count(d => d.Group == "Hermetic").Should().Be(7);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_Return15Parts()
    {
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreateFullPositions(),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        result.Should().HaveCount(15);
    }

    #endregion

    #region Edge Cases

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_HandleAllZeroLongitudes()
    {
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 0.0, moonLon: 0.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        // All parts at 0° should still be valid
        ArabicPart fortune = result.First(p => p.Name == "Part of Fortune");
        fortune.Longitude.Should().BeApproximately(0.0, 0.01);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_HandleEmptyPositions()
    {
        // With no planets, Parts that reference planet factors should be skipped
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            [], CreateHouses(ascendant: 0.0), isDayChart: true);

        result.Should().BeEmpty();
    }

    #endregion

    #region Formula Display Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_ShowDayFormula_ForDayChart()
    {
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: true);

        ArabicPart fortune = result.First(p => p.Name == "Part of Fortune");
        fortune.Formula.Should().Contain("Moon");
        fortune.Formula.Should().Contain("Sun");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAll_Should_ShowReversedFormula_ForNightChart()
    {
        IReadOnlyList<ArabicPart> result = _service.CalculateAll(
            CreatePositions(sunLon: 30.0, moonLon: 90.0),
            CreateHouses(ascendant: 0.0),
            isDayChart: false);

        ArabicPart fortune = result.First(p => p.Name == "Part of Fortune");
        // Night formula: Asc + Sun - Moon (reversed)
        fortune.Formula.Should().Contain("Sun");
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates positions with Sun and Moon at specified longitudes.
    /// Other planets at default positions.
    /// </summary>
    private static IReadOnlyList<PlanetPosition> CreatePositions(
        double sunLon, double moonLon,
        double mercuryLon = 75.0, double venusLon = 150.0,
        double marsLon = 200.0, double jupiterLon = 260.0,
        double saturnLon = 280.0)
    {
        return
        [
            CreatePosition("Sun", CelestialBodyId.Sun, sunLon),
            CreatePosition("Moon", CelestialBodyId.Moon, moonLon),
            CreatePosition("Mercury", CelestialBodyId.Mercury, mercuryLon),
            CreatePosition("Venus", CelestialBodyId.Venus, venusLon),
            CreatePosition("Mars", CelestialBodyId.Mars, marsLon),
            CreatePosition("Jupiter", CelestialBodyId.Jupiter, jupiterLon),
            CreatePosition("Saturn", CelestialBodyId.Saturn, saturnLon),
        ];
    }

    /// <summary>
    /// Creates positions for all 7 classical planets at distinct longitudes.
    /// </summary>
    private static IReadOnlyList<PlanetPosition> CreateFullPositions() =>
        CreatePositions(30.0, 90.0, 75.0, 150.0, 200.0, 260.0, 280.0);

    private static PlanetPosition CreatePosition(string name, CelestialBodyId body, double longitude) =>
        new()
        {
            Body = body,
            Name = name,
            Longitude = longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = 1.0,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
            House = 1,
        };

    /// <summary>
    /// Creates houses with specified Ascendant. Cusps are equally spaced at 30° intervals.
    /// </summary>
    private static HouseData CreateHouses(double ascendant, double midheaven = 270.0) => new()
    {
        Cusps = Enumerable.Range(0, 12)
            .Select(i => (ascendant + i * 30.0) % 360.0)
            .ToList(),
        Ascendant = ascendant,
        Midheaven = midheaven,
        Armc = 0.0,
        Vertex = 0.0,
    };

    #endregion
}
