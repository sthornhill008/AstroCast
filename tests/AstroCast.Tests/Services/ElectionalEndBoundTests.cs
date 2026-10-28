using AstroCast.Core.Enums;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the range's end bound (branch O1, 1.3.3): no grid sample lies at or after the end bound, local
/// midnight opening the day after the last date. A sample is admitted only while its instant, taken to the
/// second, is before the end bound's.
/// </summary>
/// <remarks>
/// <para>
/// Until 1.3.3 the last index's Julian Day could land a few ulps below the end bound, pass the test, and
/// round to local midnight after the last day: the O investigation found that sample in 71 of 716 searches,
/// reported in 6 and the only window in 5. Search K100, wholly inside a Mercury retrograde that was to be
/// avoided, returned "Wed 04 Nov 2026 12:00 AM", whose retrograde had been cut at that same midnight.
/// </para>
/// <para>
/// Expected instants come from Python <c>zoneinfo</c>; the retrograde's stations from Tier B (see
/// <see cref="MercuryRetroStationTests"/>).
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalEndBoundTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ElectionalEndBoundTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>
    /// K100: a contract at Richmond from 28 October to 3 November 2026, Mercury retrograde avoided. Mercury
    /// is retrograde from 24 October to 13 November, so no moment of the range qualifies, and the
    /// disqualified period is the retrograde with its own stations.
    /// </summary>
    [Fact]
    public void Calculate_K100_WhollyInsideAMercuryRetrograde_ReturnsNoCandidate()
    {
        ElectionalResult result = Service().Calculate(new ElectionalInput
        {
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            ElectionType = ElectionType.Contract,
            ElectionGoal = "K100",
            RangeStart = new DateOnly(2026, 10, 28),
            RangeEnd = new DateOnly(2026, 11, 3),
            Latitude = 37.5407,
            Longitude = -77.436,
            LocationName = "Richmond, Virginia",
            TimeZone = "America/New_York",
            CandidateCount = 3,
            AvoidMercuryRetrograde = true,
        });

        result.Candidates.Should().BeEmpty("every moment of the range lies in a retrograde that was to be avoided");
        DisqualifiedPeriod retrograde = result.DisqualifiedPeriods.Should().ContainSingle(p => p.Reason == "Mercury retrograde").Which;
        retrograde.Start.Should().BeCloseTo(new DateTime(2026, 10, 24, 3, 12, 43), TimeSpan.FromSeconds(1),
            "the station retrograde, 2026-10-24 07:12:43 UT, read in EDT");
        retrograde.End.Should().BeCloseTo(new DateTime(2026, 11, 13, 10, 53, 53), TimeSpan.FromSeconds(1),
            "the station direct, 2026-11-13 15:53:53 UT, read in EST");
    }

    /// <summary>
    /// Sydney, 4 and 5 April 2026, the time limited to 00:00–00:00 so that only midnights qualify. The two
    /// midnights of the range, 2026-04-03 13:00 UT and 2026-04-04 13:00 UT (AEDT), are offered; the midnight
    /// opening 6 April, the end bound, is not.
    /// </summary>
    [Fact]
    public void Calculate_SydneyMidnights_ReturnNothingAfterTheLastDay()
    {
        ElectionalResult result = Service().Calculate(new ElectionalInput
        {
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Sydney midnights",
            RangeStart = new DateOnly(2026, 4, 4),
            RangeEnd = new DateOnly(2026, 4, 5),
            Latitude = -33.8688,
            Longitude = 151.2093,
            LocationName = "Sydney",
            TimeZone = "Australia/Sydney",
            CandidateCount = 5,
            MinimumCandidateSeparationHours = 0.0,
            TimeConstraint = new TimeConstraint { Earliest = new TimeOnly(0, 0), Latest = new TimeOnly(0, 0) },
        });

        result.Candidates.Should().OnlyContain(c => DateOnly.FromDateTime(c.LocalDateTime) <= new DateOnly(2026, 4, 5),
            "no moment after the last acceptable date may be offered");
        result.Candidates.Select(c => c.Chart.UtcDateTime).Should().BeEquivalentTo(
            [new DateTime(2026, 4, 3, 13, 0, 0, DateTimeKind.Utc), new DateTime(2026, 4, 4, 13, 0, 0, DateTimeKind.Utc)],
            "the range's two midnights, 00:00 AEDT on 4 and 5 April");
    }

    private ElectionalService Service() => new(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture));
}
