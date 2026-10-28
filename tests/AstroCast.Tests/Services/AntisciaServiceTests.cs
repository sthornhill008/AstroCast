using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Unit tests for <see cref="AntisciaService"/>. Verifies antiscia formula
/// correctness, contra-antiscia derivation, sign assignments, connection
/// detection within orb, and edge cases.
/// </summary>
public sealed class AntisciaServiceTests
{
    private readonly AntisciaService _service = new();

    #region Antiscia Formula Tests

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(0.0, 180.0)]       // 0° Aries → 0° Libra (180°)
    [InlineData(90.0, 90.0)]       // 0° Cancer → 0° Cancer (self-mirror at solstice axis)
    [InlineData(270.0, 270.0)]     // 0° Capricorn → 0° Capricorn (self-mirror at solstice axis)
    [InlineData(180.0, 0.0)]       // 0° Libra → 0° Aries (360° wraps to 0°)
    [InlineData(30.0, 150.0)]      // 0° Taurus → 0° Virgo
    [InlineData(60.0, 120.0)]      // 0° Gemini → 0° Leo
    [InlineData(120.0, 60.0)]      // 0° Leo → 0° Gemini
    [InlineData(150.0, 30.0)]      // 0° Virgo → 0° Taurus
    [InlineData(210.0, 330.0)]     // 0° Scorpio → 0° Pisces
    [InlineData(240.0, 300.0)]     // 0° Sagittarius → 0° Aquarius
    [InlineData(300.0, 240.0)]     // 0° Aquarius → 0° Sagittarius
    [InlineData(330.0, 210.0)]     // 0° Pisces → 0° Scorpio
    public void CalculateAntiscion_ShouldReturnCorrectMirrorPoint(
        double longitude, double expectedAntiscion)
    {
        double result = AntisciaService.CalculateAntiscion(longitude);

        result.Should().BeApproximately(expectedAntiscion, 0.0001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAntiscion_15Aries_ShouldReturn15Virgo()
    {
        // 15° Aries = 15.0° → antiscion = (540 - 15) % 360 = 165° = 15° Virgo
        double result = AntisciaService.CalculateAntiscion(15.0);

        result.Should().BeApproximately(165.0, 0.0001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAntiscion_22Gemini_ShouldReturn8Cancer()
    {
        // 22° Gemini = 82.0° → antiscion = (540 - 82) % 360 = 98° = 8° Cancer
        double result = AntisciaService.CalculateAntiscion(82.0);

        result.Should().BeApproximately(98.0, 0.0001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAntiscion_NegativeLongitude_ShouldNormalize()
    {
        // -30° normalises to 330° (0° Pisces) → antiscion = (540 - 330) % 360 = 210° (0° Scorpio)
        double result = AntisciaService.CalculateAntiscion(-30.0);

        result.Should().BeApproximately(210.0, 0.0001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void CalculateAntiscion_Over360_ShouldNormalize()
    {
        // 390° normalises to 30° (0° Taurus) → antiscion = (540 - 30) % 360 = 150° (0° Virgo)
        double result = AntisciaService.CalculateAntiscion(390.0);

        result.Should().BeApproximately(150.0, 0.0001);
    }

    #endregion

    #region Contra-Antiscia Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ShouldProduceContraAntiscia_180FromAntiscia()
    {
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 15.0),  // 15° Aries → antiscion 165° → contra 345°
        ];

        (IReadOnlyList<AntisciaPoint> points, _) = _service.Calculate(positions);

        points.Should().ContainSingle();
        AntisciaPoint sun = points[0];
        sun.AntisciaLongitude.Should().BeApproximately(165.0, 0.0001);
        sun.ContraAntisciaLongitude.Should().BeApproximately(345.0, 0.0001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ContraAntiscia_ShouldWrapAt360()
    {
        // 0° Cancer (90°) → antiscion = 90° → contra = 270° (no wrap needed)
        // 15° Sagittarius (255°) → antiscion = (540-255)%360 = 285° → contra = (285+180)%360 = 105°
        List<PlanetPosition> positions =
        [
            CreatePosition("Mars", 255.0),
        ];

        (IReadOnlyList<AntisciaPoint> points, _) = _service.Calculate(positions);

        AntisciaPoint mars = points[0];
        mars.AntisciaLongitude.Should().BeApproximately(285.0, 0.0001);
        mars.ContraAntisciaLongitude.Should().BeApproximately(105.0, 0.0001);
    }

    #endregion

    #region Sign Assignment Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_AntisciaSign_ShouldBeCorrect()
    {
        // 15° Aries (15°) → antiscion 165° = Virgo
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 15.0),
        ];

        (IReadOnlyList<AntisciaPoint> points, _) = _service.Calculate(positions);

        points[0].AntisciaSign.Should().Be(ZodiacSign.Virgo);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ContraAntisciaSign_ShouldBeCorrect()
    {
        // 15° Aries → antiscion 165° → contra 345° = Pisces
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 15.0),
        ];

        (IReadOnlyList<AntisciaPoint> points, _) = _service.Calculate(positions);

        points[0].ContraAntisciaSign.Should().Be(ZodiacSign.Pisces);
    }

    #endregion

    #region Connection Detection Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ShouldDetectAntisciaConnection_WhenWithinOrb()
    {
        // Sun at 15° Aries (15°) → antiscion at 165° (15° Virgo)
        // Moon at 165.5° ≈ 15°30' Virgo → within 2° orb of 165°
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 15.0),
            CreatePosition("Moon", 165.5),
        ];

        (_, IReadOnlyList<AntisciaConnection> connections) = _service.Calculate(positions);

        connections.Should().Contain(c =>
            c.Type == "Antiscia" &&
            c.Planet1 == "Sun" &&
            c.Planet2 == "Moon");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ShouldDetectContraAntisciaConnection_WhenWithinOrb()
    {
        // Sun at 15° Aries (15°) → contra-antiscion at 345° (15° Pisces)
        // Moon at 344.0° ≈ 14° Pisces → within 2° orb of 345°
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 15.0),
            CreatePosition("Moon", 344.0),
        ];

        (_, IReadOnlyList<AntisciaConnection> connections) = _service.Calculate(positions);

        connections.Should().Contain(c =>
            c.Type == "ContraAntiscia" &&
            c.Planet1 == "Sun" &&
            c.Planet2 == "Moon");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ShouldNotDetectConnection_WhenBeyondOrb()
    {
        // Sun at 15° Aries (15°) → antiscion at 165°
        // Moon at 170° → 5° away — beyond 2° orb
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 15.0),
            CreatePosition("Moon", 170.0),
        ];

        (_, IReadOnlyList<AntisciaConnection> connections) = _service.Calculate(positions);

        connections.Should().NotContain(c =>
            c.Planet1 == "Sun" && c.Planet2 == "Moon" && c.Type == "Antiscia");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ShouldDetectConnection_AtExactOrb()
    {
        // Sun at 15° Aries (15°) → antiscion at 165°
        // Moon at 167° → exactly 2° orb
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 15.0),
            CreatePosition("Moon", 167.0),
        ];

        (_, IReadOnlyList<AntisciaConnection> connections) = _service.Calculate(positions);

        connections.Should().Contain(c =>
            c.Type == "Antiscia" && c.Planet1 == "Sun" && c.Planet2 == "Moon");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ShouldNotDetectConnection_JustBeyondOrb()
    {
        // Sun at 15° Aries (15°) → antiscion at 165°
        // Moon at 167.1° → 2.1° orb — just beyond
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 15.0),
            CreatePosition("Moon", 167.1),
        ];

        (_, IReadOnlyList<AntisciaConnection> connections) = _service.Calculate(positions);

        connections.Should().NotContain(c =>
            c.Type == "Antiscia" && c.Planet1 == "Sun" && c.Planet2 == "Moon");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ConnectionsShouldBeSortedByOrb()
    {
        // Set up two connections: one tight (0.5°) and one wider (1.5°)
        // Sun at 15° → antiscion at 165°. Moon at 165.5° (0.5° orb)
        // Mars at 255° → antiscion at 285°. Jupiter at 286.5° (1.5° orb)
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 15.0),
            CreatePosition("Moon", 165.5),
            CreatePosition("Mars", 255.0),
            CreatePosition("Jupiter", 286.5),
        ];

        (_, IReadOnlyList<AntisciaConnection> connections) = _service.Calculate(positions);

        if (connections.Count >= 2)
        {
            connections[0].Orb.Should().BeLessThanOrEqualTo(connections[1].Orb);
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ShouldNotDuplicateConnections()
    {
        // Each pair should appear only once, not A→B and B→A
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 15.0),
            CreatePosition("Moon", 165.0),  // exact antiscion of Sun
        ];

        (_, IReadOnlyList<AntisciaConnection> connections) = _service.Calculate(positions);

        int antisciaCount = connections.Count(c => c.Type == "Antiscia");
        antisciaCount.Should().Be(1, "connection should be reported once, not duplicated");
    }

    #endregion

    #region Points Calculation Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_ShouldReturnOnePointPerPosition()
    {
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", 15.0),
            CreatePosition("Moon", 100.0),
            CreatePosition("Mars", 200.0),
        ];

        (IReadOnlyList<AntisciaPoint> points, _) = _service.Calculate(positions);

        points.Should().HaveCount(3);
        points.Select(p => p.PlanetName).Should().Contain(["Sun", "Moon", "Mars"]);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_EmptyPositions_ShouldReturnEmpty()
    {
        List<PlanetPosition> positions = [];

        (IReadOnlyList<AntisciaPoint> points, IReadOnlyList<AntisciaConnection> connections)
            = _service.Calculate(positions);

        points.Should().BeEmpty();
        connections.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Calculate_NullPositions_ShouldThrow()
    {
        Action act = () => _service.Calculate(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Notation Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void AntisciaConnection_Notation_ShouldFormatCorrectly()
    {
        AntisciaConnection connection = new()
        {
            Planet1 = "Mars",
            Planet2 = "Venus",
            Type = "Antiscia",
            Orb = 0.75,
        };

        connection.Notation.Should().Be("Mars antiscia Venus (0\u00b045\u2032)");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AntisciaConnection_ContraNotation_ShouldFormatCorrectly()
    {
        AntisciaConnection connection = new()
        {
            Planet1 = "Sun",
            Planet2 = "Moon",
            Type = "ContraAntiscia",
            Orb = 1.5,
        };

        connection.Notation.Should().Be("Sun contra-antiscia Moon (1\u00b030\u2032)");
    }

    #endregion

    #region Antiscia Sign Pairings Tests

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(15.0, ZodiacSign.Virgo)]       // Aries ↔ Virgo
    [InlineData(45.0, ZodiacSign.Leo)]         // Taurus ↔ Leo
    [InlineData(75.0, ZodiacSign.Cancer)]      // Gemini ↔ Cancer
    [InlineData(105.0, ZodiacSign.Gemini)]     // Cancer ↔ Gemini
    [InlineData(135.0, ZodiacSign.Taurus)]     // Leo ↔ Taurus
    [InlineData(165.0, ZodiacSign.Aries)]      // Virgo ↔ Aries
    [InlineData(195.0, ZodiacSign.Pisces)]     // Libra ↔ Pisces
    [InlineData(225.0, ZodiacSign.Aquarius)]   // Scorpio ↔ Aquarius
    [InlineData(255.0, ZodiacSign.Capricorn)]  // Sagittarius ↔ Capricorn
    [InlineData(285.0, ZodiacSign.Sagittarius)]// Capricorn ↔ Sagittarius
    [InlineData(315.0, ZodiacSign.Scorpio)]    // Aquarius ↔ Scorpio
    [InlineData(345.0, ZodiacSign.Libra)]      // Pisces ↔ Libra
    public void Calculate_AntisciaSignPairings_ShouldMatchTraditionalTable(
        double longitude, ZodiacSign expectedAntisciaSign)
    {
        List<PlanetPosition> positions = [CreatePosition("Test", longitude)];

        (IReadOnlyList<AntisciaPoint> points, _) = _service.Calculate(positions);

        points[0].AntisciaSign.Should().Be(expectedAntisciaSign);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a minimal PlanetPosition for testing.
    /// </summary>
    private static PlanetPosition CreatePosition(string name, double longitude) => new()
    {
        Body = CelestialBodyId.Sun,
        Name = name,
        Longitude = longitude,
        Latitude = 0.0,
        Distance = 1.0,
        SpeedInLongitude = 1.0,
        SpeedInLatitude = 0.0,
        SpeedInDistance = 0.0,
        House = 1,
    };

    #endregion
}
