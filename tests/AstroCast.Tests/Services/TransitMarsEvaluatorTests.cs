using AstroCast.Core.Enums;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="TransitMarsEvaluator"/> verifying Mars inclusion logic
/// based on station detection and slow-motion aspect evaluation.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class TransitMarsEvaluatorTests
{
    private readonly TransitMarsEvaluator _evaluator;
    private readonly TransitEphemerisHelper _helper;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the evaluator chain.
    /// </summary>
    public TransitMarsEvaluatorTests(EphemerisFixture fixture)
    {
        _helper = new TransitEphemerisHelper(fixture.EphemerisService);
        TransitStationDetector stationDetector = new(_helper);
        _evaluator = new TransitMarsEvaluator(_helper, stationDetector);
    }

    [Fact]
    public void ShouldIncludeMars_WhenMarsStations_Should_ReturnTrue()
    {
        // Mars retrogrades roughly every ~26 months.
        // Mars stations retrograde around January 2027.
        // Scan Dec 2026 – Feb 2027 to capture the station.
        DateOnly start = new(2026, 12, 1);
        DateOnly end = new(2027, 2, 28);

        List<double> natalTargets = []; // Empty targets — station alone qualifies

        bool include = _evaluator.ShouldIncludeMars(start, end, natalTargets);

        include.Should().BeTrue(
            "Mars should be included when it stations during the report period");
    }

    [Fact]
    public void ShouldIncludeMars_WhenMarsDirectAndFast_Should_ReturnFalse()
    {
        // Pick a period when Mars is direct and moving at normal speed.
        // Mars in March 2026 should be moving normally (~0.5°+/day).
        DateOnly start = new(2026, 3, 1);
        DateOnly end = new(2026, 3, 31);

        List<double> natalTargets = []; // No targets to aspect

        bool include = _evaluator.ShouldIncludeMars(start, end, natalTargets);

        include.Should().BeFalse(
            "Mars should not be included when moving at normal speed with no natal targets");
    }

    [Fact]
    public void ShouldIncludeMars_WhenMarsSlowNearNatalPlanet_Should_ReturnTrue()
    {
        // Find a date near a Mars station and create a natal target at Mars's longitude.
        // Mars stations retrograde around Jan 2027. Use that window.
        // First, find Mars's approximate position near the station.
        DateOnly start = new(2026, 12, 15);
        DateOnly end = new(2027, 1, 15);

        // Get Mars position at a date near station to create a matching natal target
        double jdMidRange = _helper.DateToJulianDay(new DateOnly(2027, 1, 1));
        double marsLongitude = _helper.GetLongitude(TransitBody.Mars, jdMidRange);

        // Create a natal target at Mars's exact longitude (forcing a conjunction)
        List<double> natalTargets = [marsLongitude];

        bool include = _evaluator.ShouldIncludeMars(start, end, natalTargets);

        include.Should().BeTrue(
            "Mars should be included when slow and aspecting a natal planet");
    }
}
