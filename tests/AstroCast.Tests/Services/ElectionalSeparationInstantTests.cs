using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the separation filter's measure (branch O1, 1.3.3): the time between candidates' instants
/// (<see cref="ChartResult.UtcDateTime"/>), not between their local clock readings.
/// </summary>
/// <remarks>
/// <para>
/// Across a change of the clocks the two differ by the change. Spring forward, a pair 5–6 hours apart in time
/// reads 6–7 hours apart on the clock and was kept as "6 hours apart"; fall back, a pair 6–7 hours apart reads
/// 5–6 and was rejected. The O investigation found 8 such pairs kept in 80 searches across spring-forward nights.
/// </para>
/// <para>
/// The instants come from Python <c>zoneinfo</c> (IANA 2026e): London, 29 March 2026, 00:15 GMT is 00:15 UT and
/// 07:00 BST is 06:00 UT (the investigation's BusinessLaunch pair); New York, 8 March 2026, 01:45 EST is 06:45 UT
/// and 03:00 EDT is 07:00 UT; New York, 1 November 2026, 00:00 EDT is 04:00 UT and 05:30 EST is 10:30 UT.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ElectionalSeparationInstantTests
{
    /// <summary>
    /// London's spring-forward night: 00:15 GMT and 07:00 BST read 6.75 hours apart and are 5.75 hours apart in
    /// time, so a six-hour separation keeps only the better.
    /// </summary>
    [Fact]
    public void ApplySeparationFilter_LondonSpringForwardPair_KeepsOnlyTheBetter()
    {
        CandidateWindow first = Candidate(new DateTime(2026, 3, 29, 0, 15, 0), new DateTime(2026, 3, 29, 0, 15, 0, DateTimeKind.Utc), 74.0);
        CandidateWindow second = Candidate(new DateTime(2026, 3, 29, 7, 0, 0), new DateTime(2026, 3, 29, 6, 0, 0, DateTimeKind.Utc), 71.5);

        List<CandidateWindow> kept = ElectionalService.ApplySeparationFilter([first, second], 5, 6.0);

        kept.Should().Equal([first], "5.75 hours apart in time is under the six-hour separation");
    }

    /// <summary>
    /// New York's spring-forward night at a half-hour separation: 01:45 EST and 03:00 EDT read 75 minutes apart
    /// and are 15 minutes apart in time.
    /// </summary>
    [Fact]
    public void ApplySeparationFilter_NewYorkSkippedHour_KeepsOnlyTheBetter()
    {
        CandidateWindow better = Candidate(new DateTime(2026, 3, 8, 3, 0, 0), new DateTime(2026, 3, 8, 7, 0, 0, DateTimeKind.Utc), 47.25);
        CandidateWindow worse = Candidate(new DateTime(2026, 3, 8, 1, 45, 0), new DateTime(2026, 3, 8, 6, 45, 0, DateTimeKind.Utc), 40.0);

        List<CandidateWindow> kept = ElectionalService.ApplySeparationFilter([better, worse], 5, 0.5);

        kept.Should().Equal([better], "15 minutes apart in time is under the half-hour separation");
    }

    /// <summary>
    /// New York's fall-back night: 00:00 EDT and 05:30 EST read 5.5 hours apart and are 6.5 hours apart in time,
    /// so a six-hour separation keeps both.
    /// </summary>
    [Fact]
    public void ApplySeparationFilter_NewYorkRepeatedHour_KeepsAPairSixAndAHalfHoursApart()
    {
        CandidateWindow first = Candidate(new DateTime(2026, 11, 1, 0, 0, 0), new DateTime(2026, 11, 1, 4, 0, 0, DateTimeKind.Utc), 80.0);
        CandidateWindow second = Candidate(new DateTime(2026, 11, 1, 5, 30, 0), new DateTime(2026, 11, 1, 10, 30, 0, DateTimeKind.Utc), 70.0);

        List<CandidateWindow> kept = ElectionalService.ApplySeparationFilter([first, second], 5, 6.0);

        kept.Should().Equal([first, second], "6.5 hours apart in time meets the six-hour separation");
    }

    /// <summary>A candidate whose chart is cast at <paramref name="utc"/> and read <paramref name="local"/>.</summary>
    /// <param name="local">The local reading.</param>
    /// <param name="utc">The instant.</param>
    /// <param name="score">The overall score.</param>
    /// <returns>The stub candidate.</returns>
    private static CandidateWindow Candidate(DateTime local, DateTime utc, double score)
    {
        ScoreWeights weights = new(0.25, 0.10, 0.15, 0.10, 0.15, 0.15, 0.10);
        ChartResult chart = new()
        {
            Input = new ChartInput
            {
                Name = "Stub", LocalDateTime = local, TimeZone = "UTC", Latitude = 0.0, Longitude = 0.0, LocationName = "Stub", EventType = "Event",
            },
            JulianDay = 2460000.0,
            Obliquity = 23.4367,
            UtcDateTime = utc,
            Positions = [],
            Houses = new HouseData { Cusps = new double[12], Ascendant = 0.0, Midheaven = 90.0, Armc = 0.0, Vertex = 0.0 },
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = 0.0,
            ResolvedOffsetLabel = "UTC+0",
            EngineVersion = "stub",
            CalculatedAt = DateTime.UtcNow,
        };

        return new CandidateWindow
        {
            Rank = 0,
            LocalDateTime = local,
            Chart = chart,
            Moon = new MoonCondition
            {
                Longitude = 0.0, Sign = "Aries", Phase = MoonPhaseType.NewMoon, PhasePercent = 0.0, PhasePosition = "early",
                IsVoidOfCourse = false, IsInViaCombusta = false, DailyMotion = 13.0,
            },
            PlanetaryHour = new PlanetaryHour { RulingPlanet = "Sun", HourNumber = 1, IsDaytime = true, HourStart = local, HourEnd = local.AddHours(1) },
            Score = score,
            ScoreBreakdown = new ScoreBreakdown
            {
                MoonScore = score, AscendantScore = score, SignificatorScore = score, NatalHarmonyScore = score, MaleficAvoidanceScore = score,
                MercuryScore = score, PlanetaryHourScore = score, Weights = weights, ElectionTypeName = "Other",
            },
            NatalHarmonyAspects = [],
            Strengths = [],
            Compromises = [],
        };
    }
}
