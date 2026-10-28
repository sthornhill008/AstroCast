using AstroCast.Core.Enums;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="TransitHouseOverlayCalculator"/> verifying house overlay
/// calculation and sign interception detection.
/// Uses the real Swiss Ephemeris via the shared test fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class TransitHouseOverlayCalculatorTests
{
    private readonly TransitHouseOverlayCalculator _calculator;
    private readonly TransitEphemerisHelper _helper;

    /// <summary>Evenly spaced house cusps for deterministic testing (30° per house).</summary>
    private static readonly List<double> EvenCusps =
        Enumerable.Range(0, 12).Select(i => i * 30.0).ToList();

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the calculator.
    /// </summary>
    public TransitHouseOverlayCalculatorTests(EphemerisFixture fixture)
    {
        _helper = new TransitEphemerisHelper(fixture.EphemerisService);
        _calculator = new TransitHouseOverlayCalculator(_helper);
    }

    [Fact]
    public void Calculate_Should_ReturnOneOverlayPerBody()
    {
        DateOnly snapshotDate = new(2026, 3, 15);
        List<TransitBody> bodies = [TransitBody.Saturn, TransitBody.Jupiter, TransitBody.Pluto];

        IReadOnlyList<TransitHouseOverlay> overlays =
            _calculator.Calculate(bodies, snapshotDate, EvenCusps);

        overlays.Should().HaveCount(3,
            "there should be exactly one overlay per transit body");

        overlays.Select(o => o.Planet).Should().BeEquivalentTo(bodies,
            "overlay planets should match the input transit bodies");
    }

    [Fact]
    public void Calculate_HouseNumber_Should_BeValid()
    {
        DateOnly snapshotDate = new(2026, 3, 15);
        List<TransitBody> bodies =
        [
            TransitBody.Pluto, TransitBody.Neptune, TransitBody.Uranus,
            TransitBody.Saturn, TransitBody.Jupiter, TransitBody.Chiron,
            TransitBody.NorthNode,
        ];

        IReadOnlyList<TransitHouseOverlay> overlays =
            _calculator.Calculate(bodies, snapshotDate, EvenCusps);

        foreach (TransitHouseOverlay overlay in overlays)
        {
            overlay.HouseNumber.Should().BeInRange(1, 12,
                $"{overlay.Planet} house number should be between 1 and 12");
        }
    }

    [Fact]
    public void IsInterceptedSign_Should_ReturnFalse_WithEvenCusps()
    {
        // With evenly spaced 30° cusps, no sign can be intercepted because
        // each house spans exactly one sign.
        // Place a longitude at 15° Taurus (45°) → house 2 cusp = 30° (Taurus),
        // house 3 cusp = 60° (Gemini). Sign index 1 (Taurus) == cusp sign index 1. Not intercepted.
        bool result = TransitHouseOverlayCalculator.IsInterceptedSign(
            longitude: 45.0, houseNumber: 2, cusps: EvenCusps);

        result.Should().BeFalse(
            "evenly spaced cusps (30° per house) should never produce interceptions");
    }

    [Fact]
    public void IsInterceptedSign_Should_ReturnTrue_WhenSignSkipped()
    {
        // Create cusps where house 1 spans from 25° Aries (25°) to 35° Taurus (65°),
        // completely containing Taurus (30°–60°) without a cusp in Taurus.
        // A longitude at 45° (15° Taurus, sign index 1) should be intercepted:
        // cusp sign index = (int)(25/30) = 0 (Aries), next cusp sign index = (int)(65/30) = 2 (Gemini).
        // Sign 1 (Taurus) != 0 (Aries) and 1 != 2 (Gemini) → intercepted.
        List<double> unevenCusps =
        [
            25.0,   // House 1 cusp: 25° Aries
            65.0,   // House 2 cusp: 5° Gemini (skips Taurus)
            95.0,   // House 3
            125.0,  // House 4
            155.0,  // House 5
            185.0,  // House 6
            205.0,  // House 7 cusp: 25° Libra
            245.0,  // House 8 cusp: 5° Sagittarius (skips Scorpio)
            275.0,  // House 9
            305.0,  // House 10
            335.0,  // House 11
            5.0,    // House 12
        ];

        // 45° = 15° Taurus, falls in house 1 (between 25° and 65°)
        bool result = TransitHouseOverlayCalculator.IsInterceptedSign(
            longitude: 45.0, houseNumber: 1, cusps: unevenCusps);

        result.Should().BeTrue(
            "Taurus should be intercepted when house 1 spans from Aries to Gemini");
    }
}
