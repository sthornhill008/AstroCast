using AstroCast.Core.Enums;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="MoonConditionEvaluator"/> verifying
/// Moon condition evaluation against known astronomical expectations.
/// </summary>
/// <remarks>
/// These tests require the Swiss Ephemeris DLL and ephemeris data files.
/// Uses the shared "Ephemeris" collection fixture.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class MoonConditionEvaluatorTests
{
    private readonly MoonConditionEvaluator _evaluator;
    private readonly EphemerisService _ephemeris;

    /// <summary>
    /// Receives the shared ephemeris fixture from the collection.
    /// </summary>
    public MoonConditionEvaluatorTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        _evaluator = new MoonConditionEvaluator(_ephemeris);
    }

    /// <summary>Empty VOC periods list for tests that don't need VOC detection.</summary>
    private static readonly IReadOnlyList<VoidOfCoursePeriod> EmptyVoidPeriods =
        Array.Empty<VoidOfCoursePeriod>().AsReadOnly();

    #region Phase Validation

    [Fact]
    public void Evaluate_Should_ReturnValidPhase()
    {
        // Arrange — 2026-01-15 12:00 UTC
        double jd = _ephemeris.DateToJulianDay(2026, 1, 15, 12.0);

        // Act
        MoonCondition result = _evaluator.Evaluate(jd, EmptyVoidPeriods, "UTC");

        // Assert
        Enum.IsDefined(typeof(MoonPhaseType), result.Phase).Should().BeTrue(
            "Phase must be a valid MoonPhaseType value");
    }

    #endregion

    #region Phase Percent

    [Fact]
    public void Evaluate_PhasePercent_Should_BeBetween0And100()
    {
        // Arrange — 2026-03-01 12:00 UTC
        double jd = _ephemeris.DateToJulianDay(2026, 3, 1, 12.0);

        // Act
        MoonCondition result = _evaluator.Evaluate(jd, EmptyVoidPeriods, "UTC");

        // Assert
        result.PhasePercent.Should().BeInRange(0.0, 100.0,
            "PhasePercent must be between 0 and 100");
    }

    #endregion

    #region Daily Motion

    [Fact]
    public void Evaluate_DailyMotion_Should_BeReasonable()
    {
        // Arrange — 2026-02-10 12:00 UTC
        double jd = _ephemeris.DateToJulianDay(2026, 2, 10, 12.0);

        // Act
        MoonCondition result = _evaluator.Evaluate(jd, EmptyVoidPeriods, "UTC");

        // Assert
        result.DailyMotion.Should().BeInRange(11.0, 15.0,
            "Moon's daily motion should be in the typical range of 11–15°/day");
    }

    #endregion

    #region Via Combusta

    [Fact]
    public void Evaluate_ViaCombusta_Should_BeTrueWhenMoonInRange()
    {
        // Arrange — find a Julian Day where Moon is between 195°–225° (15° Libra – 15° Scorpio)
        // Search from 2026-01-01, stepping daily until we find Moon in Via Combusta
        double jd = FindMoonInLongitudeRange(195.0, 225.0);

        // Act
        MoonCondition result = _evaluator.Evaluate(jd, EmptyVoidPeriods, "UTC");

        // Assert
        result.IsInViaCombusta.Should().BeTrue(
            $"Moon at {result.Longitude:F1}° should be in Via Combusta (195°–225°)");
        result.Longitude.Should().BeInRange(195.0, 225.0,
            "Moon longitude should be in Via Combusta range");
    }

    [Fact]
    public void Evaluate_ViaCombusta_Should_BeFalseOutsideRange()
    {
        // Arrange — find a Julian Day where Moon is NOT in Via Combusta
        // Use a date where Moon is around 0°–30° Aries (well outside 195°–225°)
        double jd = FindMoonInLongitudeRange(0.0, 30.0);

        // Act
        MoonCondition result = _evaluator.Evaluate(jd, EmptyVoidPeriods, "UTC");

        // Assert
        result.IsInViaCombusta.Should().BeFalse(
            $"Moon at {result.Longitude:F1}° should not be in Via Combusta");
    }

    #endregion

    #region Sign Match

    [Fact]
    public void Evaluate_Sign_Should_MatchMoonLongitude()
    {
        // Arrange — use multiple dates and verify sign matches longitude
        double jd = _ephemeris.DateToJulianDay(2026, 4, 15, 12.0);

        // Act
        MoonCondition result = _evaluator.Evaluate(jd, EmptyVoidPeriods, "UTC");

        // Assert — derive expected sign from longitude
        string expectedSign = VoidOfCourseScanner.SignFromLongitude(result.Longitude);
        result.Sign.Should().Be(expectedSign,
            $"Moon at {result.Longitude:F1}° should be in {expectedSign}");
    }

    #endregion

    /// <summary>
    /// Searches for a Julian Day where the Moon is within the given longitude range.
    /// Steps through 6-hour intervals starting from 2026-01-01 for up to 30 days.
    /// </summary>
    /// <param name="minLongitude">Minimum longitude (inclusive).</param>
    /// <param name="maxLongitude">Maximum longitude (exclusive).</param>
    /// <returns>Julian Day where Moon is in the specified range.</returns>
    private double FindMoonInLongitudeRange(double minLongitude, double maxLongitude)
    {
        double startJd = _ephemeris.DateToJulianDay(2026, 1, 1, 0.0);

        for (double jd = startJd; jd < startJd + 30.0; jd += 0.25)
        {
            double moonLon = _ephemeris.CalculatePosition(jd, CelestialBodyId.Moon).Longitude;

            if (moonLon >= minLongitude && moonLon < maxLongitude)
            {
                return jd;
            }
        }

        throw new InvalidOperationException(
            $"Could not find Moon in longitude range {minLongitude}–{maxLongitude} within 30 days");
    }
}
