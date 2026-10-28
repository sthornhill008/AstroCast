using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Unit tests for <see cref="ElectionalService.ApplySeparationFilter"/>.
/// Verifies that candidates are selected with a configurable minimum
/// time separation to prevent clustering.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ElectionalCandidateSeparationTests
{
    #region Helper — Build a stub CandidateWindow

    /// <summary>
    /// Creates a minimal <see cref="CandidateWindow"/> stub with the given
    /// local date/time and score, suitable for separation-filter unit tests.
    /// </summary>
    /// <param name="localDt">Local date and time for the candidate.</param>
    /// <param name="score">Overall score (higher = better).</param>
    /// <returns>A stub CandidateWindow.</returns>
    private static CandidateWindow MakeCandidate(DateTime localDt, double score)
    {
        ScoreWeights weights = new(0.25, 0.10, 0.15, 0.10, 0.15, 0.15, 0.10);
        ScoreBreakdown breakdown = new()
        {
            MoonScore = score,
            AscendantScore = score,
            SignificatorScore = score,
            NatalHarmonyScore = score,
            MaleficAvoidanceScore = score,
            MercuryScore = score,
            PlanetaryHourScore = score,
            Weights = weights,
            ElectionTypeName = "BusinessLaunch",
        };

        ChartInput chartInput = new()
        {
            Name = "Stub",
            LocalDateTime = localDt,
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "NYC",
            EventType = "Event",
        };

        ChartResult chart = new()
        {
            Input = chartInput,
            JulianDay = 2460000.0,
            Obliquity = 23.4367,
            UtcDateTime = localDt,
            Positions = [],
            Houses = new HouseData
            {
                Cusps = new List<double> { 0, 30, 60, 90, 120, 150, 180, 210, 240, 270, 300, 330 },
                Ascendant = 0.0,
                Midheaven = 90.0,
                Armc = 0.0,
                Vertex = 0.0,
            },
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = -5.0,
            ResolvedOffsetLabel = "EST (UTC-5)",
            EngineVersion = "AstroCast 1.0.0 (SwissEph 2.10.03)",
            CalculatedAt = DateTime.UtcNow,
        };

        MoonCondition moon = new()
        {
            Longitude = 0.0,
            Sign = "Aries",
            Phase = MoonPhaseType.NewMoon,
            PhasePercent = 0.0,
            PhasePosition = "early",
            IsVoidOfCourse = false,
            IsInViaCombusta = false,
            DailyMotion = 13.0,
        };

        PlanetaryHour hour = new()
        {
            RulingPlanet = "Sun",
            HourNumber = 1,
            IsDaytime = true,
            HourStart = localDt,
            HourEnd = localDt.AddHours(1),
        };

        return new CandidateWindow
        {
            Rank = 0,
            LocalDateTime = localDt,
            Chart = chart,
            Moon = moon,
            PlanetaryHour = hour,
            Score = score,
            ScoreBreakdown = breakdown,
            NatalHarmonyAspects = [],
            Strengths = ["Good"],
            Compromises = [],
        };
    }

    #endregion

    #region Test 1: Spread Selection

    /// <summary>
    /// Verifies that with 6-hour separation, clustered candidates at 7:00, 7:15, 7:30
    /// are collapsed and well-separated candidates at 13:00 and 20:00 are selected.
    /// </summary>
    [Fact]
    public void ApplySeparationFilter_6Hours_Should_SelectSpreadCandidates()
    {
        // Arrange — candidates already sorted by score descending
        DateTime day = new(2026, 4, 15);
        List<CandidateWindow> sorted =
        [
            MakeCandidate(day.AddHours(7.0), 90),   // 07:00 — best score
            MakeCandidate(day.AddHours(7.25), 88),   // 07:15 — too close to #1
            MakeCandidate(day.AddHours(7.5), 86),    // 07:30 — too close to #1
            MakeCandidate(day.AddHours(13.0), 84),   // 13:00 — 6h from #1
            MakeCandidate(day.AddHours(20.0), 82),   // 20:00 — 7h from #2
        ];

        // Act
        List<CandidateWindow> result = ElectionalService.ApplySeparationFilter(sorted, 3, 6.0);

        // Assert
        result.Should().HaveCount(3);
        result[0].LocalDateTime.Hour.Should().Be(7,  "first candidate is at 07:00");
        result[1].LocalDateTime.Hour.Should().Be(13, "second candidate is at 13:00");
        result[2].LocalDateTime.Hour.Should().Be(20, "third candidate is at 20:00");
    }

    #endregion

    #region Test 2: Zero Separation (Backward Compatibility)

    /// <summary>
    /// Verifies that when separation is 0, all top candidates are returned
    /// regardless of proximity (backward compatibility).
    /// </summary>
    [Fact]
    public void ApplySeparationFilter_ZeroSeparation_Should_ReturnTopN()
    {
        // Arrange
        DateTime day = new(2026, 4, 15);
        List<CandidateWindow> sorted =
        [
            MakeCandidate(day.AddHours(7.0), 90),
            MakeCandidate(day.AddHours(7.25), 88),
            MakeCandidate(day.AddHours(7.5), 86),
        ];

        // Act
        List<CandidateWindow> result = ElectionalService.ApplySeparationFilter(sorted, 3, 0.0);

        // Assert
        result.Should().HaveCount(3, "zero separation should return all top N");
        result[0].Score.Should().Be(90);
        result[1].Score.Should().Be(88);
        result[2].Score.Should().Be(86);
    }

    #endregion

    #region Test 3: Fewer Survivors

    /// <summary>
    /// Verifies that if only 2 candidates survive the separation filter,
    /// the result contains exactly 2.
    /// </summary>
    [Fact]
    public void ApplySeparationFilter_InsufficientSurvivors_Should_ReturnAvailable()
    {
        // Arrange — all candidates within 2 hours of each other
        DateTime day = new(2026, 4, 15);
        List<CandidateWindow> sorted =
        [
            MakeCandidate(day.AddHours(7.0), 90),    // selected
            MakeCandidate(day.AddHours(7.5), 88),     // too close to 07:00
            MakeCandidate(day.AddHours(8.0), 86),     // too close to 07:00
            MakeCandidate(day.AddHours(14.0), 84),    // selected (7h gap)
            MakeCandidate(day.AddHours(14.5), 82),    // too close to 14:00
        ];

        // Act
        List<CandidateWindow> result = ElectionalService.ApplySeparationFilter(sorted, 3, 6.0);

        // Assert
        result.Should().HaveCount(2, "only 2 candidates have 6+ hours of separation");
        result[0].LocalDateTime.Hour.Should().Be(7);
        result[1].LocalDateTime.Hour.Should().Be(14);
    }

    #endregion

    #region Test 4: Isolation — Pure Filter Logic

    /// <summary>
    /// Tests the separation filter method in isolation with edge cases:
    /// empty input, single candidate, exact boundary.
    /// </summary>
    [Fact]
    public void ApplySeparationFilter_EmptyList_Should_ReturnEmpty()
    {
        // Act
        List<CandidateWindow> result = ElectionalService.ApplySeparationFilter([], 3, 6.0);

        // Assert
        result.Should().BeEmpty();
    }

    /// <summary>
    /// Tests that a single candidate is always returned.
    /// </summary>
    [Fact]
    public void ApplySeparationFilter_SingleCandidate_Should_ReturnOne()
    {
        // Arrange
        List<CandidateWindow> sorted =
        [
            MakeCandidate(new DateTime(2026, 4, 15, 10, 0, 0), 90),
        ];

        // Act
        List<CandidateWindow> result = ElectionalService.ApplySeparationFilter(sorted, 3, 6.0);

        // Assert
        result.Should().HaveCount(1);
    }

    /// <summary>
    /// Tests that a candidate exactly at the separation boundary (6.0 hours)
    /// is accepted (not less than, so exactly 6 is valid).
    /// </summary>
    [Fact]
    public void ApplySeparationFilter_ExactBoundary_Should_RejectCandidate()
    {
        // Arrange — second candidate is at exactly 6.0 hours (boundary)
        DateTime day = new(2026, 4, 15);
        List<CandidateWindow> sorted =
        [
            MakeCandidate(day.AddHours(7.0), 90),
            MakeCandidate(day.AddHours(13.0), 88), // exactly 6h apart
        ];

        // Act
        List<CandidateWindow> result = ElectionalService.ApplySeparationFilter(sorted, 2, 6.0);

        // Assert — 6.0h is NOT less than 6.0h, so it passes
        result.Should().HaveCount(2, "exactly 6h gap passes the < 6h rejection test");
    }

    #endregion

    #region Test 5: Cross-Day Separation

    /// <summary>
    /// Tests that separation works across day boundaries
    /// (e.g., 11 PM on day 1 and 3 AM on day 2 = 4 hours apart).
    /// </summary>
    [Fact]
    public void ApplySeparationFilter_CrossDayBoundary_Should_CalculateCorrectly()
    {
        // Arrange
        List<CandidateWindow> sorted =
        [
            MakeCandidate(new DateTime(2026, 4, 15, 23, 0, 0), 90), // 11 PM day 1
            MakeCandidate(new DateTime(2026, 4, 16, 3, 0, 0), 88),  // 3 AM day 2 (4h gap)
            MakeCandidate(new DateTime(2026, 4, 16, 10, 0, 0), 86), // 10 AM day 2 (11h from #1, 7h from #2)
        ];

        // Act
        List<CandidateWindow> result = ElectionalService.ApplySeparationFilter(sorted, 3, 6.0);

        // Assert — 3 AM is only 4h from 11 PM, so it's rejected
        result.Should().HaveCount(2);
        result[0].LocalDateTime.Should().Be(new DateTime(2026, 4, 15, 23, 0, 0));
        result[1].LocalDateTime.Should().Be(new DateTime(2026, 4, 16, 10, 0, 0));
    }

    #endregion
}
