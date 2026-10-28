using System.Diagnostics;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="ElectionalService"/> verifying
/// the full electional calculation pipeline end-to-end.
/// </summary>
/// <remarks>
/// These tests exercise the complete pipeline: disqualification map,
/// VOC calendar, candidate grid, scoring, and ranking.
/// Test dates: April 2026, NYC (40.7°N, 74.0°W).
/// Natal chart: J2000.0 (2000-01-01 12:00 UTC) for reproducibility.
/// Uses the shared "Ephemeris" collection fixture.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalServiceTests
{
    private readonly EphemerisService _ephemeris;
    private readonly ChartEngine _chartEngine;
    private readonly ElectionalService _service;

    /// <summary>NYC latitude.</summary>
    private const double Latitude = 40.7128;

    /// <summary>NYC longitude.</summary>
    private const double Longitude = -74.006;

    /// <summary>NYC timezone.</summary>
    private const string TimeZone = "America/New_York";

    /// <summary>
    /// Receives the shared ephemeris fixture and builds all services.
    /// </summary>
    public ElectionalServiceTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(_ephemeris, logger);
        MidpointService midpoints = new(logger);
        _chartEngine = new ChartEngine(_ephemeris, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(_ephemeris, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(_ephemeris), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
        _service = new ElectionalService(_ephemeris, _chartEngine);
    }

    /// <summary>
    /// Creates a natal chart for testing (J2000.0, NYC).
    /// </summary>
    private ChartResult CreateNatalChart()
    {
        ChartInput input = new()
        {
            Name = "Test Natal",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = TimeZone,
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "New York City",
            EventType = "Birth",
        };

        return _chartEngine.Calculate(input);
    }

    /// <summary>
    /// Creates a standard 7-day ElectionalInput for testing.
    /// Separation filtering is disabled (0.0) so these tests validate
    /// pre-existing scoring and ranking behavior.
    /// </summary>
    /// <remarks>
    /// Disabling separation is deliberate and legitimate <b>for the scoring and
    /// ranking tests that use this builder</b>: it makes the returned set the
    /// plain top-N by score, which is what those tests assert about. It is not a
    /// legitimate basis for asserting the candidate <i>count</i> contract -- see
    /// <see cref="CreateShippedSeparationInput"/> and the note on
    /// <see cref="Calculate_7DayRange_Should_ReturnRequestedCandidateCount"/>.
    /// </remarks>
    private ElectionalInput CreateStandardInput(int candidateCount = 3)
    {
        return new ElectionalInput
        {
            NatalChart = CreateNatalChart(),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch new product",
            RangeStart = new DateOnly(2026, 4, 1),
            RangeEnd = new DateOnly(2026, 4, 7),
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = candidateCount,
            MinimumCandidateSeparationHours = 0.0,
        };
    }

    /// <summary>
    /// Creates the same 7-day input but leaves
    /// <see cref="ElectionalInput.MinimumCandidateSeparationHours"/> at its model
    /// default, so the search runs in the configuration the application ships.
    /// </summary>
    /// <param name="candidateCount">Number of candidate windows to request.</param>
    /// <returns>A populated <see cref="ElectionalInput"/>.</returns>
    private ElectionalInput CreateShippedSeparationInput(int candidateCount = 3)
    {
        return new ElectionalInput
        {
            NatalChart = CreateNatalChart(),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch new product",
            RangeStart = new DateOnly(2026, 4, 1),
            RangeEnd = new DateOnly(2026, 4, 7),
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = candidateCount,
        };
    }

    #region Test 1: Candidate Count

    /// <summary>
    /// A request for three candidate windows returns three, in the configuration
    /// the application actually ships.
    /// </summary>
    /// <remarks>
    /// This test previously ran against <see cref="CreateStandardInput"/>, which
    /// disables the separation filter. That made its name a promise it did not
    /// keep: the count contract held only with the filter switched off, and the
    /// shipped path silently returned fewer. Switched to the shipped default so
    /// the assertion covers the behaviour the name describes. The window here
    /// (7 days, NYC, BusinessLaunch) is a third one, distinct from the two in
    /// <see cref="ElectionalCandidateCountTests"/>.
    /// </remarks>
    [Fact]
    public void Calculate_7DayRange_Should_ReturnRequestedCandidateCount()
    {
        // Arrange -- shipped separation default, not the isolation builder
        ElectionalInput input = CreateShippedSeparationInput(candidateCount: 3);

        // Act
        ElectionalResult result = _service.Calculate(input);

        // Assert
        result.Candidates.Count.Should().Be(3,
            "should return the requested number of candidates at the shipped separation default");
        result.IsRequestUnmet.Should().BeFalse(
            "a fully met request must not be reported as a shortfall");
    }

    #endregion

    #region Test 2: Ranked by Score

    [Fact]
    public void Calculate_Should_ReturnCandidatesRankedByScore()
    {
        // Arrange
        ElectionalInput input = CreateStandardInput();

        // Act
        ElectionalResult result = _service.Calculate(input);

        // Assert -- scores should be in descending order
        result.Candidates[0].Score.Should().BeGreaterThanOrEqualTo(
            result.Candidates[1].Score,
            "candidate 1 should have the highest or equal score");

        if (result.Candidates.Count > 2)
        {
            result.Candidates[1].Score.Should().BeGreaterThanOrEqualTo(
                result.Candidates[2].Score,
                "candidate 2 should have score >= candidate 3");
        }

        // Ranks should be sequential
        for (int i = 0; i < result.Candidates.Count; i++)
        {
            result.Candidates[i].Rank.Should().Be(i + 1,
                $"rank should be {i + 1} for candidate at index {i}");
        }
    }

    #endregion

    #region Test 3: Disqualified Periods

    [Fact]
    public void Calculate_Should_PopulateDisqualifiedPeriods()
    {
        // Arrange -- use a range that includes Mercury retrograde in 2026
        // Mercury retrograde periods in 2026 occur several times;
        // a full-year scan should find at least one.
        ElectionalInput input = new()
        {
            NatalChart = CreateNatalChart(),
            ElectionType = ElectionType.Contract,
            ElectionGoal = "Sign contract",
            RangeStart = new DateOnly(2026, 1, 1),
            RangeEnd = new DateOnly(2026, 12, 31),
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = 3,
            AvoidMercuryRetrograde = true,
        };

        // Act
        ElectionalResult result = _service.Calculate(input);

        // Assert
        result.DisqualifiedPeriods.Should().NotBeEmpty(
            "a full year should have disqualified periods");
        result.DisqualifiedPeriods.Should().Contain(
            p => p.Reason.Contains("Mercury retrograde"),
            "at least one period should be for Mercury retrograde");
    }

    #endregion

    #region Test 4: VOC Periods

    [Fact]
    public void Calculate_Should_PopulateVocPeriods()
    {
        // Arrange
        ElectionalInput input = CreateStandardInput();

        // Act
        ElectionalResult result = _service.Calculate(input);

        // Assert -- Moon goes VOC multiple times per week
        result.VoidOfCoursePeriods.Should().NotBeEmpty(
            "a 7-day range should have at least one VOC period");
    }

    #endregion

    #region Test 5: Score Range

    [Fact]
    public void Calculate_CandidateScores_Should_BeBetween0And100()
    {
        // Arrange
        ElectionalInput input = CreateStandardInput();

        // Act
        ElectionalResult result = _service.Calculate(input);

        // Assert
        foreach (CandidateWindow candidate in result.Candidates)
        {
            candidate.Score.Should().BeInRange(0.0, 100.0,
                $"candidate at rank {candidate.Rank} should have score 0-100");
        }
    }

    #endregion

    #region Test 6: Non-Empty Strengths

    [Fact]
    public void Calculate_Candidates_Should_HaveNonEmptyStrengths()
    {
        // Arrange
        ElectionalInput input = CreateStandardInput();

        // Act
        ElectionalResult result = _service.Calculate(input);

        // Assert
        foreach (CandidateWindow candidate in result.Candidates)
        {
            candidate.Strengths.Should().NotBeEmpty(
                $"candidate at rank {candidate.Rank} should have at least 1 strength");
        }
    }

    #endregion

    #region Test 7: Time Constraint

    [Fact]
    public void Calculate_WithTimeConstraint_Should_RespectConstraint()
    {
        // Arrange -- only allow 09:00-17:00
        ElectionalInput input = new()
        {
            NatalChart = CreateNatalChart(),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch new product",
            RangeStart = new DateOnly(2026, 4, 1),
            RangeEnd = new DateOnly(2026, 4, 7),
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = 3,
            TimeConstraint = new TimeConstraint
            {
                Earliest = new TimeOnly(9, 0),
                Latest = new TimeOnly(17, 0),
            },
        };

        // Act
        ElectionalResult result = _service.Calculate(input);

        // Assert
        foreach (CandidateWindow candidate in result.Candidates)
        {
            TimeOnly candidateTime = TimeOnly.FromDateTime(candidate.LocalDateTime);
            candidateTime.Should().BeOnOrAfter(new TimeOnly(9, 0),
                $"candidate at rank {candidate.Rank} should be at or after 09:00");
            candidateTime.Should().BeOnOrBefore(new TimeOnly(17, 0),
                $"candidate at rank {candidate.Rank} should be at or before 17:00");
        }
    }

    #endregion

    #region Test 8: Day-of-Week Filter

    [Fact]
    public void Calculate_WithAllowedDays_Should_RespectDayFilter()
    {
        // Arrange -- only Monday, Wednesday, Friday
        DayOfWeek[] allowedDays = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday];
        ElectionalInput input = new()
        {
            NatalChart = CreateNatalChart(),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch new product",
            RangeStart = new DateOnly(2026, 4, 1),
            RangeEnd = new DateOnly(2026, 4, 14),
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = 3,
            AllowedDays = allowedDays,
        };

        // Act
        ElectionalResult result = _service.Calculate(input);

        // Assert
        foreach (CandidateWindow candidate in result.Candidates)
        {
            allowedDays.Should().Contain(candidate.LocalDateTime.DayOfWeek,
                $"candidate at rank {candidate.Rank} should be on an allowed day");
        }
    }

    #endregion

    #region Test 9: Valid Chart Data

    [Fact]
    public void Calculate_CandidateCharts_Should_HaveValidData()
    {
        // Arrange
        ElectionalInput input = CreateStandardInput();

        // Act
        ElectionalResult result = _service.Calculate(input);

        // Assert
        foreach (CandidateWindow candidate in result.Candidates)
        {
            candidate.Chart.Should().NotBeNull(
                $"candidate at rank {candidate.Rank} should have a chart");
            candidate.Chart.Positions.Should().HaveCount(12,
                "chart should have 12 celestial bodies");
            candidate.Chart.Houses.Should().NotBeNull(
                "chart should have house data");
            candidate.Moon.Should().NotBeNull(
                "candidate should have moon condition");
            candidate.PlanetaryHour.Should().NotBeNull(
                "candidate should have planetary hour");
            candidate.ScoreBreakdown.Should().NotBeNull(
                "candidate should have score breakdown");
        }
    }

    #endregion

    #region Test 10: Performance Gate

    [Fact]
    public void Calculate_Should_CompleteWithin30Seconds()
    {
        // Arrange -- 30-day range (should scan ~2,880 intervals at 15-min steps)
        ElectionalInput input = new()
        {
            NatalChart = CreateNatalChart(),
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch new product",
            RangeStart = new DateOnly(2026, 4, 1),
            RangeEnd = new DateOnly(2026, 4, 30),
            Latitude = Latitude,
            Longitude = Longitude,
            LocationName = "New York City",
            TimeZone = TimeZone,
            CandidateCount = 3,
        };

        // Act
        Stopwatch sw = Stopwatch.StartNew();
        ElectionalResult result = _service.Calculate(input);
        sw.Stop();

        // Assert
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30),
            "30-day electional calculation should complete within 30 seconds");
        result.Candidates.Should().NotBeEmpty(
            "should find at least one candidate in 30 days");
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="ElectionalService.Calculate"/> honours the
    /// supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before any candidate-grid iteration runs.
    /// </summary>
    [Fact]
    public void Calculate_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        ElectionalInput input = CreateStandardInput();

        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => _service.Calculate(input, cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the candidate-grid sweep immediately");
    }

    #endregion
}
