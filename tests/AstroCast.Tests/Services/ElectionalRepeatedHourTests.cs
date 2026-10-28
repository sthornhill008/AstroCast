using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// An electional candidate is cast at the instant it was sampled and scored at, even in
/// an hour the clocks repeat (TZ-1).
/// </summary>
/// <remarks>
/// <para>
/// New York's clocks went back at 02:00 EDT on 2026-11-01, so every reading from 01:00 to
/// 01:59 occurred twice: first on EDT (05:00–05:59 UT), then on EST (06:00–06:59 UT), per
/// IANA 2026e (Python <c>zoneinfo</c> over <c>tzdata</c> 2026.5). The scan samples every
/// 15 minutes from local midnight (04:00 UT), so a window of 01:00–01:59 holds eight
/// samples, at 05:00, 05:15, … 06:45 UT.
/// </para>
/// <para>
/// The candidate chart used to be cast from the sample's wall clock, which in a repeated
/// hour converts back to the later instant: the four EDT samples were scored at their own
/// instants but cast an hour later, each the same chart as its EST twin.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalRepeatedHourTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ElectionalRepeatedHourTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The eight samples of New York's repeated hour become eight candidates, each cast at
    /// its own instant, labelled with the offset in force then.
    /// </summary>
    [Fact]
    public void Calculate_Should_CastEachCandidateAtItsSampledInstant_InARepeatedHour()
    {
        ChartEngine engine = GoldenFixtures.CreateChartEngine(_fixture);
        ElectionalService service = new(_fixture.EphemerisService, engine);

        ElectionalResult result = service.Calculate(new ElectionalInput
        {
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Repeated hour",
            RangeStart = new DateOnly(2026, 11, 1),
            RangeEnd = new DateOnly(2026, 11, 1),
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            TimeZone = "America/New_York",
            CandidateCount = 8,
            MinimumCandidateSeparationHours = 0.0,
            AvoidMercuryRetrograde = false,
            TimeConstraint = new TimeConstraint
            {
                Earliest = new TimeOnly(1, 0),
                Latest = new TimeOnly(1, 59),
            },
        });

        DateTime[] expected = Enumerable.Range(0, 8)
            .Select(i => new DateTime(2026, 11, 1, 5, 0, 0, DateTimeKind.Utc).AddMinutes(15 * i))
            .ToArray();

        result.Candidates.Select(c => c.Chart.UtcDateTime).Should().BeEquivalentTo(expected,
            "each candidate is cast at the instant it was sampled at, once each");

        foreach (CandidateWindow c in result.Candidates)
        {
            bool onEdt = c.Chart.UtcDateTime.Hour == 5;
            c.LocalDateTime.Should().Be(new DateTime(2026, 11, 1, 1, c.Chart.UtcDateTime.Minute, 0));
            c.Chart.ResolvedOffsetLabel.Should().Be(onEdt ? "EDT (UTC−4)" : "EST (UTC−5)");
            c.Chart.ResolvedUtcOffsetHours.Should().Be(onEdt ? -4.0 : -5.0);
        }
    }
}
