using System.Text.RegularExpressions;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Xunit.Abstractions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// T8 (2026-06-10 live-run review, Defect C): for Doctor Visit Candidate 2,
/// the Moon's applying-aspect orb must render identically — one canonical
/// value in D°MM′ format — in all three Electional export sites: the
/// candidate summary header, the Moon Conditions block, and the aspects table.
/// </summary>
/// <remarks>
/// The live run rendered the same fact three ways: 2°18′ (summary header,
/// truncated from a stale outer-planet cache), 2.32° (Moon Conditions,
/// decimal forward-distance), and 219' (aspects table, truncated DMM'
/// concatenation). The assertion here is on three-way equality and format;
/// the canonical value is logged, not hardcoded.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalOrbConsistencyTests
{
    private readonly EphemerisService _ephemeris;
    private readonly ChartEngine _chartEngine;
    private readonly ITestOutputHelper _output;

    /// <summary>Richmond, VA latitude (Doctor Visit live-run location).</summary>
    private const double Latitude = 37.5385;

    /// <summary>Richmond, VA longitude.</summary>
    private const double Longitude = -77.4343;

    /// <summary>Richmond, VA IANA timezone.</summary>
    private const string TimeZone = "America/New_York";

    /// <summary>
    /// Receives the shared ephemeris fixture and builds all services.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    /// <param name="output">xUnit output helper for logging the canonical orb.</param>
    public ElectionalOrbConsistencyTests(EphemerisFixture fixture, ITestOutputHelper output)
    {
        _ephemeris = fixture.EphemerisService;
        _output = output;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(_ephemeris, logger);
        MidpointService midpoints = new(logger);
        _chartEngine = new ChartEngine(_ephemeris, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(_ephemeris, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(_ephemeris), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    [Fact]
    public void ToMarkdown_DoctorVisitCandidate2_MoonApplyingOrbIdenticalInAllThreeSites()
    {
        // Arrange — T8 fixture: 2026-06-12 23:44:59 America/New_York, Richmond VA.
        // Known facts: Moon applying sextile to Jupiter, orb ~2°19′.
        DateTime localDateTime = new(2026, 6, 12, 23, 44, 59);
        ElectionalResult result = BuildSingleCandidateResult(localDateTime);
        CandidateWindow candidate = result.Candidates[0];

        // Fixture preconditions — fail loudly if the applying aspect is not
        // the known Moon-Jupiter sextile
        candidate.Moon.NextApplyingPlanet.Should().Be("Jupiter",
            $"the fixture's Moon applies to Jupiter, got {candidate.Moon.NextApplyingPlanet ?? "(none)"}");
        candidate.Moon.NextApplyingAspectType.Should().Be(AspectType.Sextile,
            $"the fixture's applying aspect is a sextile, got {candidate.Moon.NextApplyingAspectType}");

        AspectResult? canonical = AspectService.FindAspectBetween(
            candidate.Chart.Aspects, "Moon", "Jupiter", AspectType.Sextile);
        canonical.Should().NotBeNull(
            "the Moon-Jupiter sextile must appear in the candidate chart's aspect table "
            + "(its orb is the canonical value)");

        // Act
        string markdown = ElectionalMarkdownFormatter.ToMarkdown(result);

        // Extract the orb from each of the three sites
        Match headerMatch = Regex.Match(markdown,
            @"\*\*Moon:\*\* .*Moon sextile Jupiter \(applying, ([^)]+)\)");
        headerMatch.Success.Should().BeTrue(
            "the candidate summary header must carry the Moon applying-aspect phrase");
        string headerOrb = headerMatch.Groups[1].Value;

        Match moonConditionsMatch = Regex.Match(markdown,
            @"\*\*First Applying Aspect:\*\* Moon Sextile Jupiter \(orb ([^)]+)\)");
        moonConditionsMatch.Success.Should().BeTrue(
            "the Moon Conditions block must carry the first applying aspect with its orb");
        string moonConditionsOrb = moonConditionsMatch.Groups[1].Value;

        string body1 = Regex.Escape(canonical!.Body1Name);
        string body2 = Regex.Escape(canonical.Body2Name);
        string abbreviation = Regex.Escape(canonical.Aspect.Abbreviation);
        Match tableMatch = Regex.Match(markdown,
            $@"\| {body1} \| {abbreviation} \| {body2} \| ([^|]+) \| Ap \|");
        tableMatch.Success.Should().BeTrue(
            $"the aspects table must contain the applying {canonical.Body1Name}-{canonical.Body2Name} "
            + $"{canonical.Aspect.Abbreviation} row");
        string tableOrb = tableMatch.Groups[1].Value.Trim();

        _output.WriteLine(
            $"Canonical Moon-Jupiter sextile orb: {canonical.Orb:F6} deg -> '{tableOrb}'. "
            + $"Sites: header='{headerOrb}', moonConditions='{moonConditionsOrb}', table='{tableOrb}'.");

        // Assert — three-way equality on the single canonical value
        headerOrb.Should().Be(tableOrb,
            $"the candidate summary header orb ('{headerOrb}') must equal the aspects-table orb ('{tableOrb}')");
        moonConditionsOrb.Should().Be(tableOrb,
            $"the Moon Conditions orb ('{moonConditionsOrb}') must equal the aspects-table orb ('{tableOrb}')");

        // Assert — D°MM′ format (prime-marked, zero-padded minutes, no seconds)
        const string formatPattern = @"^\d+\u00b0\d{2}\u2032$";
        headerOrb.Should().MatchRegex(formatPattern,
            $"the header orb must be D°MM′, got '{headerOrb}'");
        moonConditionsOrb.Should().MatchRegex(formatPattern,
            $"the Moon Conditions orb must be D°MM′, got '{moonConditionsOrb}'");
        tableOrb.Should().MatchRegex(formatPattern,
            $"the aspects-table orb must be D°MM′, got '{tableOrb}'");

        // The displayed value must be the shared formatter applied to the canonical orb
        tableOrb.Should().Be(PositionFormatter.FormatOrb(canonical.Orb),
            "the table orb must come from the single shared formatter on the canonical computation");
    }

    /// <summary>
    /// Builds a one-candidate <see cref="ElectionalResult"/> for the given
    /// moment via the production calculation path (chart engine, Moon
    /// condition evaluator, planetary hour calculator, scorer).
    /// </summary>
    /// <param name="localDateTime">Local civil time in America/New_York.</param>
    /// <returns>An electional result containing exactly one candidate.</returns>
    private ElectionalResult BuildSingleCandidateResult(DateTime localDateTime)
    {
        ChartResult natal = _chartEngine.Calculate(new ChartInput
        {
            Name = "Test Client",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = TimeZone,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        });

        ChartInput chartInput = new()
        {
            Name = $"Election {localDateTime:yyyy-MM-dd HH:mm}",
            LocalDateTime = localDateTime,
            TimeZone = TimeZone,
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "Richmond, VA",
            EventType = "Event",
        };

        ChartResult chart = _chartEngine.CalculateForElectional(chartInput);

        MoonConditionEvaluator moonEvaluator = new(_ephemeris);
        MoonCondition moon = moonEvaluator.Evaluate(chart.JulianDay, [], TimeZone);

        PlanetaryHour hour = new PlanetaryHourCalculator(_ephemeris)
            .Calculate(chart.JulianDay, Latitude, Longitude, TimeZone);

        ElectionalScorer scorer = new();
        ScoreBreakdown breakdown = scorer.Score(chart, moon, hour, ElectionType.Other, natal);

        CandidateWindow candidate = new()
        {
            Rank = 1,
            LocalDateTime = localDateTime,
            Chart = chart,
            Moon = moon,
            PlanetaryHour = hour,
            Score = breakdown.OverallScore,
            ScoreBreakdown = breakdown,
            NatalHarmonyAspects = [],
            Strengths = ["Fixture candidate"],
            Compromises = [],
        };

        ElectionalInput input = new()
        {
            NatalChart = natal,
            ElectionType = ElectionType.Other,
            ElectionGoal = "Best time to visit Doctor",
            RangeStart = DateOnly.FromDateTime(localDateTime),
            RangeEnd = DateOnly.FromDateTime(localDateTime),
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "Richmond, VA",
            TimeZone = TimeZone,
        };

        return new ElectionalResult
        {
            Input = input,
            NatalChart = natal,
            Candidates = [candidate],
            // Hand-built single-candidate fixture, not the product of a search:
            // declaring the request as 1 keeps it a met request, so the header
            // carries no shortfall clause to distract from the orb assertions.
            RequestedCandidateCount = 1,
            SamplesWithoutPlanetaryHour = 0,
            DisqualifiedPeriods = [],
            VoidOfCoursePeriods = [],
            CalculatedAt = DateTime.UtcNow,
        };
    }
}
