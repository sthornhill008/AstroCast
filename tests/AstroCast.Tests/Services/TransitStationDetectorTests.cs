using AstroCast.Core.Enums;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="TransitStationDetector"/> verifying station detection
/// for outer planets using the real Swiss Ephemeris.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class TransitStationDetectorTests
{
    private readonly TransitStationDetector _detector;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the detector chain.
    /// </summary>
    public TransitStationDetectorTests(EphemerisFixture fixture)
    {
        TransitEphemerisHelper helper = new(fixture.EphemerisService);
        _detector = new TransitStationDetector(helper);
    }

    [Fact]
    public void FindStations_Saturn_2026_Should_FindRetrograde()
    {
        // Saturn stations retrograde around July 2026.
        // Scan a wide window to ensure we capture it.
        DateOnly start = new(2026, 6, 1);
        DateOnly end = new(2026, 8, 31);

        IReadOnlyList<TransitStation> stations =
            _detector.FindStations(TransitBody.Saturn, start, end);

        stations.Should().NotBeEmpty(
            "Saturn should station retrograde around July 2026");

        stations.Should().Contain(s => s.StationType == "Retrograde",
            "at least one station should be a retrograde station");

        // Verify sign data is populated
        stations.Should().AllSatisfy(s =>
        {
            s.Sign.Should().NotBeNullOrEmpty("station sign should be populated");
            s.Degrees.Should().BeInRange(0, 29, "degrees should be 0–29");
            s.Minutes.Should().BeInRange(0, 59, "minutes should be 0–59");
        });
    }

    [Fact]
    public void FindStations_ShortPeriod_NoStation_Should_ReturnEmpty()
    {
        // A 7-day window in early 2026 — no outer planet stations expected
        DateOnly start = new(2026, 2, 1);
        DateOnly end = new(2026, 2, 7);

        IReadOnlyList<TransitStation> stations =
            _detector.FindStations(TransitBody.Neptune, start, end);

        stations.Should().BeEmpty(
            "no Neptune station should occur in a 7-day window in February 2026");
    }

    [Fact]
    public void FindStations_Should_ReturnChronologicalOrder()
    {
        // Scan a full year for Saturn — may find both Rx and Direct stations
        DateOnly start = new(2026, 1, 1);
        DateOnly end = new(2026, 12, 31);

        IReadOnlyList<TransitStation> stations =
            _detector.FindStations(TransitBody.Saturn, start, end);

        for (int i = 1; i < stations.Count; i++)
        {
            stations[i].Date.Should().BeOnOrAfter(stations[i - 1].Date,
                "stations should be in chronological order");
        }
    }
}
