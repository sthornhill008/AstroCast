using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration and unit tests for <see cref="MidpointService"/> verifying midpoint
/// calculation, 45° dial activation detection, and the BuildPointList helper.
/// Uses J2000.0 epoch (JD 2451545.0) with New York City coordinates.
/// Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class MidpointServiceTests
{
    /// <summary>J2000.0 epoch — Julian Day 2451545.0 = 2000-Jan-1 12:00 UT.</summary>
    private const double J2000 = 2451545.0;

    /// <summary>New York City latitude (north positive).</summary>
    private const double NycLatitude = 40.7128;

    /// <summary>New York City longitude (east positive; west is negative).</summary>
    private const double NycLongitude = -74.006;

    private readonly EphemerisService _ephemerisService;
    private readonly MidpointService _midpointService;

    /// <summary>
    /// Receives the shared ephemeris fixture and creates the midpoint service.
    /// </summary>
    public MidpointServiceTests(EphemerisFixture fixture)
    {
        _ephemerisService = fixture.EphemerisService;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        _midpointService = new MidpointService(logger);
    }

    #region CalculateMidpoints

    [Fact]
    public void CalculateMidpoints_Should_Return91Midpoints_When_14Points()
    {
        // Arrange — 12 planets + ASC + MC = 14 points → 14 choose 2 = 91
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);
        HouseData houses = _ephemerisService.CalculateHouses(J2000, NycLatitude, NycLongitude);
        IReadOnlyList<(string Name, double Longitude)> points =
            MidpointService.BuildPointList(positions, houses);

        // Act
        IReadOnlyList<Midpoint> midpoints = _midpointService.CalculateMidpoints(points);

        // Assert
        midpoints.Should().HaveCount(91,
            "14 chart points should produce 14 × 13 / 2 = 91 unique midpoints");
    }

    [Fact]
    public void CalculateMidpoints_Should_ReturnValidLongitudes_When_Calculated()
    {
        // Arrange
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);
        HouseData houses = _ephemerisService.CalculateHouses(J2000, NycLatitude, NycLongitude);
        IReadOnlyList<(string Name, double Longitude)> points =
            MidpointService.BuildPointList(positions, houses);

        // Act
        IReadOnlyList<Midpoint> midpoints = _midpointService.CalculateMidpoints(points);

        // Assert
        foreach (Midpoint midpoint in midpoints)
        {
            midpoint.Longitude.Should().BeInRange(0.0, 360.0,
                $"midpoint {midpoint.Notation} should have a valid ecliptic longitude");
        }
    }

    #endregion

    #region CalculateNearMidpoint (Static)

    [Fact]
    public void CalculateNearMidpoint_Should_ReturnCorrectMidpoint_When_SameHemisphere()
    {
        // Assert — both points on the same side of the zodiac
        double mid = MidpointService.CalculateNearMidpoint(10.0, 20.0);
        mid.Should().BeApproximately(15.0, 0.001,
            "midpoint of 10° and 20° should be 15°");
    }

    [Fact]
    public void CalculateNearMidpoint_Should_HandleWraparound_When_CrossingZeroAries()
    {
        // Assert — 350° and 10° should give 0° (not 180°)
        double mid1 = MidpointService.CalculateNearMidpoint(350.0, 10.0);
        mid1.Should().BeApproximately(0.0, 0.001,
            "midpoint of 350° and 10° should be 0° (crossing 0° Aries)");

        // Assert — 355° and 5° should also give 0°
        double mid2 = MidpointService.CalculateNearMidpoint(355.0, 5.0);
        mid2.Should().BeApproximately(0.0, 0.001,
            "midpoint of 355° and 5° should be 0° (crossing 0° Aries)");
    }

    [Fact]
    public void CalculateNearMidpoint_Should_ReturnCorrectMidpoint_When_OppositePoints()
    {
        // Assert — 0° and 180° are equidistant; formula returns 90°
        double mid = MidpointService.CalculateNearMidpoint(0.0, 180.0);
        mid.Should().BeApproximately(90.0, 0.001,
            "midpoint of 0° and 180° should be 90° (either 90° or 270° is valid)");
    }

    #endregion

    #region Check45DegreeHarmonic (Static)

    [Fact]
    public void Check45DegreeHarmonic_Should_DetectDirectContact_When_OrbWithin1_5()
    {
        // Assert — point at 100°, midpoint at 101° → direct (0°), orb 1.0°
        (double orb, int harmonic) = MidpointService.Check45DegreeHarmonic(100.0, 101.0);
        harmonic.Should().Be(0, "separation of 1° should be detected as direct contact");
        orb.Should().BeApproximately(1.0, 0.001,
            "orb should be 1.0° for a 1° separation from the midpoint");
    }

    [Fact]
    public void Check45DegreeHarmonic_Should_Detect90Degree_When_OrbWithin1()
    {
        // Assert — point at 100°, midpoint at 10.5° → separation 89.5° → 90° harmonic, orb 0.5°
        (double orb, int harmonic) = MidpointService.Check45DegreeHarmonic(100.0, 10.5);
        harmonic.Should().Be(90, "separation of 89.5° should be detected as 90° harmonic");
        orb.Should().BeApproximately(0.5, 0.001,
            "orb should be 0.5° for a 89.5° separation from the midpoint");
    }

    [Fact]
    public void Check45DegreeHarmonic_Should_Detect45Degree_When_OrbWithin1()
    {
        // Assert — point at 100°, midpoint at 55.3° → separation 44.7° → 45° harmonic, orb 0.3°
        (double orb, int harmonic) = MidpointService.Check45DegreeHarmonic(100.0, 55.3);
        harmonic.Should().Be(45, "separation of 44.7° should be detected as 45° harmonic");
        orb.Should().BeApproximately(0.3, 0.001,
            "orb should be 0.3° for a 44.7° separation from the midpoint");
    }

    #endregion

    #region CalculateActivations

    [Fact]
    public void CalculateActivations_Should_ExcludeSelfActivation_When_BodyFormsMidpoint()
    {
        // Arrange
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);
        HouseData houses = _ephemerisService.CalculateHouses(J2000, NycLatitude, NycLongitude);
        IReadOnlyList<(string Name, double Longitude)> points =
            MidpointService.BuildPointList(positions, houses);
        IReadOnlyList<Midpoint> midpoints = _midpointService.CalculateMidpoints(points);

        // Act
        IReadOnlyList<MidpointActivation> activations =
            _midpointService.CalculateActivations(midpoints, points);

        // Assert — no activation should have an activator that is one of the midpoint bodies
        foreach (MidpointActivation activation in activations)
        {
            activation.ActivatorName.Should().NotBe(activation.Midpoint.Body1Name,
                $"activator should not be {activation.Midpoint.Body1Name} " +
                $"(forms the midpoint {activation.Midpoint.Notation})");
            activation.ActivatorName.Should().NotBe(activation.Midpoint.Body2Name,
                $"activator should not be {activation.Midpoint.Body2Name} " +
                $"(forms the midpoint {activation.Midpoint.Notation})");
        }
    }

    [Fact]
    public void CalculateActivations_Should_ReturnSortedByOrb_When_Calculated()
    {
        // Arrange
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);
        HouseData houses = _ephemerisService.CalculateHouses(J2000, NycLatitude, NycLongitude);
        IReadOnlyList<(string Name, double Longitude)> points =
            MidpointService.BuildPointList(positions, houses);
        IReadOnlyList<Midpoint> midpoints = _midpointService.CalculateMidpoints(points);

        // Act
        IReadOnlyList<MidpointActivation> activations =
            _midpointService.CalculateActivations(midpoints, points);

        // Assert
        activations.Should().NotBeEmpty("there should be at least some activations at J2000.0");

        for (int i = 1; i < activations.Count; i++)
        {
            activations[i].Orb.Should().BeGreaterThanOrEqualTo(activations[i - 1].Orb,
                "activations should be sorted by ascending orb");
        }
    }

    [Fact]
    public void CalculateActivations_Should_ApplyCorrectOrbs_When_DifferentHarmonics()
    {
        // Arrange
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);
        HouseData houses = _ephemerisService.CalculateHouses(J2000, NycLatitude, NycLongitude);
        IReadOnlyList<(string Name, double Longitude)> points =
            MidpointService.BuildPointList(positions, houses);
        IReadOnlyList<Midpoint> midpoints = _midpointService.CalculateMidpoints(points);

        // Act
        IReadOnlyList<MidpointActivation> activations =
            _midpointService.CalculateActivations(midpoints, points);

        // Assert — direct (0°) and opposition (180°) allow ±1.5°
        foreach (MidpointActivation activation in activations
                     .Where(a => a.HarmonicAngle is 0 or 180))
        {
            activation.Orb.Should().BeLessThanOrEqualTo(1.5,
                $"{activation.Notation}: direct/opposition orb must not exceed 1.5°");
        }

        // Assert — 45°, 90°, 135° allow ±1.0°
        foreach (MidpointActivation activation in activations
                     .Where(a => a.HarmonicAngle is 45 or 90 or 135))
        {
            activation.Orb.Should().BeLessThanOrEqualTo(1.0,
                $"{activation.Notation}: 45°/90°/135° orb must not exceed 1.0°");
        }
    }

    #endregion

    #region CalculateAll

    [Fact]
    public void CalculateAll_Should_ReturnBothMidpointsAndActivations_When_Called()
    {
        // Arrange
        IReadOnlyList<PlanetPosition> positions = _ephemerisService.CalculateAllPositions(J2000);
        HouseData houses = _ephemerisService.CalculateHouses(J2000, NycLatitude, NycLongitude);
        IReadOnlyList<(string Name, double Longitude)> points =
            MidpointService.BuildPointList(positions, houses);

        // Act
        (IReadOnlyList<Midpoint> midpoints, IReadOnlyList<MidpointActivation> activations) =
            _midpointService.CalculateAll(points);

        // Assert
        midpoints.Should().HaveCount(91,
            "14 chart points should produce 91 midpoints");
        activations.Should().NotBeEmpty(
            "there should be midpoint activations at J2000.0 NYC");
    }

    #endregion
}
