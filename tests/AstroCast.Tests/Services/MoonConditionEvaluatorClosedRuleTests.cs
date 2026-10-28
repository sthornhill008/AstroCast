using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins <see cref="MoonConditionEvaluator"/> to the closed definition (H3, branch P, 1.3.3): the
/// electional Moon's next applying aspect is the first exact major aspect (0°, 60°, 90°, 120°,
/// 180°, either side) to the Sun or Mercury through Pluto, targets in motion, strictly after the
/// instant and before the Moon leaves its sign, as <see cref="MoonConditionService.GetMoonCondition"/>
/// finds it; none means void of course.
/// </summary>
/// <remarks>
/// <para>
/// The expected aspects and orbs are Tier B: direct <c>swe_calc_ut</c> calls into the shipped library
/// and files, crossings on a 0.01-day grid bisected to 1e-9 day, the orb the separation from exact at
/// the instant (<c>p-moon/truth/moon_next.py</c>). None of it goes through AstroCast.
/// </para>
/// <para>
/// Until 1.3.3 the evaluator showed the nearest applying aspect within 8°, judged applying from the
/// orb 0.01 day later, with Jupiter through Pluto cached for up to half a day: an aspect more than 8°
/// away was never shown (synthesis Y6-5), one perfecting within 14.4 minutes read separating and was
/// skipped, and the answer at an instant depended on what the evaluator had been asked before (Y6-10).
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class MoonConditionEvaluatorClosedRuleTests
{
    private const string NewYork = "America/New_York";

    /// <summary>Check-table row 61's instant, 2026-10-07 12:15 UT.</summary>
    private const double Row61Jd = 2461321.01041667;

    /// <summary>
    /// The first instant of row 61's scans (the synthesis evaluator-path job): row 61's instant is its
    /// 225th 15-minute step.
    /// </summary>
    private const double Row61PathStartJd = 2461318.6666666665;

    private readonly EphemerisFixture _fixture;
    private readonly EphemerisService _ephemeris;
    private readonly TzdbTimeZoneRules _rules = new();

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public MoonConditionEvaluatorClosedRuleTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        _ephemeris = fixture.EphemerisService;
    }

    /// <summary>
    /// Row 61 (Y6-10), from a new evaluator: the Moon's next aspect is the square to Uranus, exact at
    /// 12:21:43 UT, 6.5 minutes after the instant (Tier B orb 0.063724°). Until 1.3.3 a new evaluator
    /// gave the sextile to Venus: the square, perfecting inside the 14.4-minute look-ahead, read
    /// separating.
    /// </summary>
    [Fact]
    public void Evaluate_Row61_FromANewEvaluator_IsTheSquareToUranus()
    {
        MoonCondition moon = new MoonConditionEvaluator(_ephemeris).Evaluate(Row61Jd, [], "UTC");

        ShouldBe(moon, "Uranus", AspectType.Square, 0.063724, "0°03′");
    }

    /// <summary>
    /// Row 61 (Y6-10) from evaluators that first evaluated every 15 minutes from 0, 3, 6 and 9 hours
    /// before the scan's start, as row 61's four scans did: each gives the square to Uranus. Until 1.3.3
    /// the cache of Jupiter to Pluto gave Uranus, Uranus, Venus and Venus.
    /// </summary>
    /// <param name="hoursEarlier">How long before the scan's start the evaluator began.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(9)]
    public void Evaluate_Row61_WhateverTheEvaluatorWasAskedBefore_IsTheSquareToUranus(int hoursEarlier)
    {
        MoonConditionEvaluator evaluator = new(_ephemeris);
        const double step = 15.0 / 1440.0;
        for (int i = -hoursEarlier * 4; i < 225; i++)
        {
            evaluator.Evaluate(Row61PathStartJd + (i * step), [], "UTC");
        }

        MoonCondition moon = evaluator.Evaluate(Row61PathStartJd + (225 * step), [], "UTC");

        ShouldBe(moon, "Uranus", AspectType.Square, 0.063724, "0°03′");
    }

    /// <summary>
    /// 2026-04-22 19:00 New York: the Moon's next aspect is the sextile to Venus, exact at 14:52:21 the
    /// next afternoon, 10.587565° away (Tier B). More than 8°: until 1.3.3 the evaluator could not show it.
    /// </summary>
    [Fact]
    public void Evaluate_AnAspectMoreThan8DegreesAway_IsTheNextAspect()
    {
        MoonCondition moon = new MoonConditionEvaluator(_ephemeris).Evaluate(LocalJd(2026, 4, 22, 19, 0), [], NewYork);

        ShouldBe(moon, "Venus", AspectType.Sextile, 10.587565, "10°35′");
    }

    /// <summary>
    /// 2026-04-23 14:50 New York: the sextile to Venus is exact at 14:52:21, 0.020569° away (Tier B).
    /// Inside the old 14.4-minute look-ahead, so until 1.3.3 it read separating and was skipped.
    /// </summary>
    [Fact]
    public void Evaluate_AnAspectPerfectingWithin14Minutes_IsTheNextAspect()
    {
        MoonCondition moon = new MoonConditionEvaluator(_ephemeris).Evaluate(LocalJd(2026, 4, 23, 14, 50), [], NewYork);

        ShouldBe(moon, "Venus", AspectType.Sextile, 0.020569, "0°01′");
    }

    /// <summary>
    /// 2026-04-25 12:00 New York, with no void periods passed: the Moon is void in Leo (Tier B: from its
    /// trine to Mercury at 18:20:59 the evening before to its exit at 21:04:27), so it has no next aspect.
    /// No period holds the instant, so the void's bounds are unknown to the evaluator.
    /// </summary>
    [Fact]
    public void Evaluate_AVoidMoon_IsVoid_EvenWithNoPeriodsPassed()
    {
        MoonCondition moon = new MoonConditionEvaluator(_ephemeris).Evaluate(LocalJd(2026, 4, 25, 12, 0), [], NewYork);

        moon.IsVoidOfCourse.Should().BeTrue("the Moon makes no exact major aspect before it leaves Leo");
        moon.NextApplyingPlanet.Should().BeNull();
        moon.NextApplyingAspectType.Should().BeNull();
        moon.NextApplyingOrb.Should().BeNull();
        moon.NextApplyingAspectDescription.Should().BeNull();
        moon.VoidStartLocal.Should().BeNull("no period was passed");
        moon.VoidEndLocal.Should().BeNull("no period was passed");
    }

    /// <summary>
    /// Hourly through April 2026 (UTC), the evaluator's next aspect, orb and void equal what
    /// <see cref="MoonConditionService.GetMoonCondition"/> gives for the chart positions at the same
    /// instant: one rule, one source.
    /// </summary>
    [Fact]
    public void Evaluate_HourlyThroughAMonth_EqualsMoonConditionService()
    {
        MoonConditionEvaluator evaluator = new(_ephemeris);
        MoonConditionService service = new();
        double april = _ephemeris.DateToJulianDay(2026, 4, 1, 0.0);
        List<string> differences = [];

        for (int hour = 0; hour < 30 * 24; hour++)
        {
            double jd = april + (hour / 24.0);
            MoonCondition moon = evaluator.Evaluate(jd, [], "UTC");
            MoonConditionInfo info = service.GetMoonCondition(_ephemeris.CalculateAllPositions(jd), [], _ephemeris, jd)
                ?? throw new InvalidOperationException("no Moon condition");

            if (moon.NextApplyingPlanet != info.FirstApplyingPlanet
                || moon.NextApplyingAspectType != info.FirstApplyingAspectType
                || moon.NextApplyingOrb != info.FirstApplyingOrb
                || moon.IsVoidOfCourse != info.IsVoidOfCourse)
            {
                differences.Add(string.Create(CultureInfo.InvariantCulture,
                    $"{JulianDayUtility.ToDateTimeUtc(_ephemeris, jd):yyyy-MM-dd HH:mm}: evaluator {moon.NextApplyingPlanet} {moon.NextApplyingAspectType} {moon.NextApplyingOrb} void {moon.IsVoidOfCourse}; service {info.FirstApplyingPlanet} {info.FirstApplyingAspectType} {info.FirstApplyingOrb} void {info.IsVoidOfCourse}"));
            }
        }

        differences.Should().BeEmpty("the evaluator reads the Moon from the service");
    }

    /// <summary>
    /// The two golden searches (Y6-4): every candidate's header Moon reading, the evaluator's, names the
    /// aspect, planet and void status its own chart's Moon Conditions block names.
    /// </summary>
    /// <param name="golden">Which golden search.</param>
    [Theory]
    [InlineData("NYC")]
    [InlineData("Richmond")]
    public void ElectionalSearch_EveryCandidatesHeaderMoon_EqualsItsMoonConditionsBlock(string golden)
    {
        ElectionalService service = new(_ephemeris, GoldenFixtures.CreateChartEngine(_fixture));
        ElectionalResult result = service.Calculate(golden == "NYC"
            ? GoldenInput(ElectionType.BusinessLaunch, "Launch a new software product.", new DateOnly(2026, 4, 20), new DateOnly(2026, 4, 25), 40.7128, -74.006, "New York City", 3)
            : GoldenInput(ElectionType.Other, "Best time to visit Doctor", new DateOnly(2026, 6, 11), new DateOnly(2026, 6, 14), 37.5385, -77.4343, "Richmond, VA", 2));

        result.Candidates.Should().NotBeEmpty();
        foreach (CandidateWindow candidate in result.Candidates)
        {
            MoonConditionInfo block = candidate.Chart.MoonCondition ?? throw new InvalidOperationException("no Moon Conditions block");
            string because = string.Create(CultureInfo.InvariantCulture, $"candidate {candidate.Rank} at {candidate.LocalDateTime:yyyy-MM-dd HH:mm} is one Moon");
            candidate.Moon.NextApplyingPlanet.Should().Be(block.FirstApplyingPlanet, because);
            candidate.Moon.NextApplyingAspectType.Should().Be(block.FirstApplyingAspectType, because);
            candidate.Moon.IsVoidOfCourse.Should().Be(block.IsVoidOfCourse, because);
        }
    }

    /// <summary>Asserts the next aspect, the orb against Tier B and the description's printed orb.</summary>
    private static void ShouldBe(MoonCondition moon, string planet, AspectType aspect, double tierBOrb, string printedOrb)
    {
        moon.IsVoidOfCourse.Should().BeFalse();
        moon.NextApplyingPlanet.Should().Be(planet);
        moon.NextApplyingAspectType.Should().Be(aspect);
        moon.NextApplyingOrb.Should().NotBeNull();
        moon.NextApplyingOrb.GetValueOrDefault().Should().BeApproximately(tierBOrb, 0.0001, "the orb is the separation from exact at the instant");
        moon.NextApplyingAspectDescription.Should().Be(
            string.Create(CultureInfo.InvariantCulture, $"Moon {aspect.ToString().ToLowerInvariant()} {planet} (applying, {printedOrb})"));
    }

    private double LocalJd(int year, int month, int day, int hour, int minute) =>
        JulianDayUtility.LocalMidnightToJulianDay(_ephemeris, new DateOnly(year, month, day), _rules, NewYork) + ((hour + (minute / 60.0)) / 24.0);

    private ElectionalInput GoldenInput(ElectionType type, string goal, DateOnly start, DateOnly end, double lat, double lon, string place, int count) => new()
    {
        NatalChart = GoldenFixtures.J2000Nyc(_fixture),
        ElectionType = type,
        ElectionGoal = goal,
        RangeStart = start,
        RangeEnd = end,
        Latitude = lat,
        Longitude = lon,
        LocationName = place,
        TimeZone = NewYork,
        CandidateCount = count,
    };
}
