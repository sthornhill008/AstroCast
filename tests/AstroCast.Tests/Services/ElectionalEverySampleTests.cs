using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the electional search's scoring of every eligible grid sample (S-D10(b), branch O2, 1.3.3): the top
/// candidate instant is the best-scoring eligible sample of the grid, the earliest on a tie.
/// </summary>
/// <remarks>
/// <para>
/// Until 1.3.3 the search fully scored only a pool chosen by a Moon-only quick score (the top
/// <c>CandidateCount × 15</c>, plus the best of each separation-width bin), so a moment whose Moon scored
/// modestly could be invisible however well it scored in full: the O investigation found a better sample
/// never scored in 6 of its 26 real re-runs (largest 7 points) and 187 of 324 standard searches (largest 11).
/// </para>
/// <para>
/// The three searches below were chosen with the O2 harness at <c>fccdbb5</c> from 180 five-day searches on
/// the J2000 New York natal chart: in each the pool missed the best sample by more than a point (by 6.5, 5.0
/// and 4.5). Their expected instants and scores are the harness's, every eligible sample scored. The test
/// also finds the best sample itself, by casting the full chart at every eligible sample of the grid.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalEverySampleTests
{
    /// <summary>The grid's step for a range of 90 days or less: 15 minutes.</summary>
    private const double ShortRangeStep = 15.0 / 1440.0;

    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ElectionalEverySampleTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>
    /// Three five-day searches, three candidates asked, Mercury retrograde avoided: the top candidate instant
    /// and its score are the best eligible sample's. New York's surgery search has five samples tied at
    /// 76.75 (03:30 to 04:30 EST on 9 December); the earliest is reported. Before 1.3.3 the tops were
    /// 7 Dec 11:15 AM (70.25), 6 Apr 7:30 AM (67.95) and 14 May 9:45 PM (62.75).
    /// </summary>
    /// <param name="place">The place's name.</param>
    /// <param name="latitude">The place's latitude.</param>
    /// <param name="longitude">The place's longitude.</param>
    /// <param name="timeZone">The place's IANA zone.</param>
    /// <param name="type">The election type.</param>
    /// <param name="start">The first acceptable date, yyyy-MM-dd.</param>
    /// <param name="end">The last acceptable date, yyyy-MM-dd.</param>
    /// <param name="expectedTopUtc">The best sample's instant, yyyy-MM-dd HH:mm UT.</param>
    /// <param name="expectedScore">The best sample's score.</param>
    [Theory]
    [InlineData("New York City", 40.7128, -74.006, "America/New_York", ElectionType.Surgery, "2026-12-07", "2026-12-11", "2026-12-09 08:30", 76.75)]
    [InlineData("Richmond, Virginia", 37.5407, -77.436, "America/New_York", ElectionType.RealEstateListing, "2026-04-06", "2026-04-10", "2026-04-06 17:00", 72.95)]
    [InlineData("Sydney", -33.8688, 151.2093, "Australia/Sydney", ElectionType.CreativeLaunch, "2026-05-11", "2026-05-15", "2026-05-13 01:45", 67.25)]
    public void Calculate_TopCandidate_IsTheBestEligibleSample(
        string place, double latitude, double longitude, string timeZone, ElectionType type,
        string start, string end, string expectedTopUtc, double expectedScore)
    {
        ElectionalInput input = new()
        {
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            ElectionType = type,
            ElectionGoal = "Every sample scored",
            RangeStart = DateOnly.ParseExact(start, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            RangeEnd = DateOnly.ParseExact(end, "yyyy-MM-dd", CultureInfo.InvariantCulture),
            Latitude = latitude,
            Longitude = longitude,
            LocationName = place,
            TimeZone = timeZone,
            CandidateCount = 3,
        };
        DateTime expectedUtc = DateTime.SpecifyKind(
            DateTime.ParseExact(expectedTopUtc, "yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture), DateTimeKind.Utc);

        ElectionalResult result = Service().Calculate(input);

        result.Candidates.Should().NotBeEmpty();
        result.Candidates[0].Chart.UtcDateTime.Should().Be(expectedUtc, "the harness's best sample, every eligible sample scored");
        result.Candidates[0].Score.Should().Be(expectedScore);

        (DateTime bestUtc, double bestScore, int eligible) = BestEligibleSample(input);
        eligible.Should().BeGreaterThan(0);
        bestUtc.Should().Be(expectedUtc, "the best of the {0} eligible samples, each scored on its full chart", eligible);
        bestScore.Should().Be(expectedScore);
    }

    /// <summary>
    /// The NYC golden's inputs (BusinessLaunch, 20 to 25 April 2026, three candidates): Thu 23 Apr 12:15 AM
    /// (88.5), Thu 23 Apr 2:15 PM (85.5) and Tue 21 Apr 1:30 PM (84.25), printed 88, 86 and 84. Until 1.3.3 the
    /// pool lost the 2:15 PM moment and returned 88.5, 82.25 and 78.5.
    /// </summary>
    [Fact]
    public void Calculate_NycGoldenInputs_ReturnTheBestSeparatedSamples()
    {
        ElectionalResult result = Service().Calculate(new ElectionalInput
        {
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch a new software product.",
            RangeStart = new DateOnly(2026, 4, 20),
            RangeEnd = new DateOnly(2026, 4, 25),
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            TimeZone = "America/New_York",
            CandidateCount = 3,
        });

        result.Candidates.Select(c => (c.LocalDateTime, c.Score)).Should().Equal(
            (new DateTime(2026, 4, 23, 0, 15, 0), 88.5),
            (new DateTime(2026, 4, 23, 14, 15, 0), 85.5),
            (new DateTime(2026, 4, 21, 13, 30, 0), 84.25));
    }

    /// <summary>
    /// The Richmond golden's inputs (Other, 11 to 14 June 2026, two candidates): the second candidate is
    /// Thu 11 Jun 9:00 PM (80.2, printed 80). Until 1.3.3 it was Sat 13 Jun 12:00 AM (77.5, printed 78).
    /// </summary>
    [Fact]
    public void Calculate_RichmondGoldenInputs_SecondCandidateIsTheBestSeparatedSample()
    {
        ElectionalResult result = Service().Calculate(new ElectionalInput
        {
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            ElectionType = ElectionType.Other,
            ElectionGoal = "Best time to visit Doctor",
            RangeStart = new DateOnly(2026, 6, 11),
            RangeEnd = new DateOnly(2026, 6, 14),
            Latitude = 37.5385,
            Longitude = -77.4343,
            LocationName = "Richmond, VA",
            TimeZone = "America/New_York",
            CandidateCount = 2,
        });

        result.Candidates.Should().HaveCount(2);
        result.Candidates[1].LocalDateTime.Should().Be(new DateTime(2026, 6, 11, 21, 0, 0));
        result.Candidates[1].Score.Should().Be(80.2);
    }

    /// <summary>
    /// Finds the best eligible sample of a search's grid by casting the full chart at each one: the grid of
    /// a range of 90 days or less, from the first instant of the first date to the first instant of the day
    /// after the last, less every sample inside a Mercury retrograde or a void of course, by its instant.
    /// The search's own time and day limits are not used: the cases set none.
    /// </summary>
    /// <param name="input">The search.</param>
    /// <returns>The best sample's instant and score, the earliest on a tie, and the number of eligible samples.</returns>
    private (DateTime Utc, double Score, int Eligible) BestEligibleSample(ElectionalInput input)
    {
        TzdbTimeZoneRules rules = new();
        IEphemerisService ephemeris = _fixture.EphemerisService;
        ChartEngine engine = GoldenFixtures.CreateChartEngine(_fixture);
        MoonConditionEvaluator evaluator = new(ephemeris, rules);
        PlanetaryHourCalculator hourCalculator = new(ephemeris, rules);
        ElectionalScorer scorer = new();

        double startJd = JulianDayUtility.LocalDayStartToJulianDay(ephemeris, input.RangeStart, rules, input.TimeZone);
        double endJd = JulianDayUtility.LocalDayStartToJulianDay(ephemeris, input.RangeEnd.AddDays(1), rules, input.TimeZone);
        DateTime endUtc = JulianDayUtility.ToDateTimeUtc(ephemeris, endJd);
        IReadOnlyList<DisqualifiedPeriod> retrogrades = new MercuryRetroDetector(ephemeris, rules).FindRetrogradePeriods(startJd, endJd, input.TimeZone);
        IReadOnlyList<VoidOfCoursePeriod> voids = new VoidOfCourseScanner(ephemeris, rules).Scan(startJd, endJd, input.TimeZone);

        DateTime bestUtc = default;
        double bestScore = double.NegativeInfinity;
        int eligible = 0;
        for (int index = 0; ; index++)
        {
            double jd = startJd + (index * ShortRangeStep);
            DateTime utc = JulianDayUtility.ToDateTimeUtc(ephemeris, jd);
            if (jd >= endJd || utc >= endUtc)
            {
                break;
            }

            if (retrogrades.Any(p => utc >= p.StartUtc && utc < p.EndUtc) || voids.Any(v => utc >= v.StartUtc && utc < v.EndUtc))
            {
                continue;
            }

            eligible++;
            DateTime local = JulianDayUtility.ToLocalDateTime(ephemeris, jd, rules, input.TimeZone);
            ChartResult chart = engine.CalculateForElectional(new ChartInput
            {
                Name = string.Create(CultureInfo.InvariantCulture, $"Election {local:yyyy-MM-dd HH:mm}"),
                LocalDateTime = local,
                TimeZone = input.TimeZone,
                Latitude = input.Latitude,
                Longitude = input.Longitude,
                LocationName = input.LocationName,
                EventType = "Event",
                HouseSystem = input.NatalChart.Input.HouseSystem,
            }, utc);
            double score = scorer.Score(
                chart,
                evaluator.Evaluate(jd, voids, input.TimeZone),
                hourCalculator.Calculate(jd, input.Latitude, input.Longitude, input.TimeZone),
                input.ElectionType,
                input.NatalChart).OverallScore;

            // Strictly greater: on a tie the earliest sample stands.
            if (score > bestScore)
            {
                bestUtc = utc;
                bestScore = score;
            }
        }

        return (bestUtc, bestScore, eligible);
    }

    private ElectionalService Service() => new(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture));
}
