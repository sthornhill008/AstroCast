using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="VoidOfCourseScanner"/> verifying
/// void-of-course Moon detection against known astronomical expectations.
/// </summary>
/// <remarks>
/// These tests require the Swiss Ephemeris DLL and ephemeris data files.
/// Uses the shared "Ephemeris" collection fixture.
/// VOC scanning is computationally intensive — tests use a 30-day range.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class VoidOfCourseScannerTests
{
    private readonly VoidOfCourseScanner _scanner;
    private readonly EphemerisService _ephemeris;

    /// <summary>
    /// Receives the shared ephemeris fixture from the collection.
    /// </summary>
    public VoidOfCourseScannerTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        _scanner = new VoidOfCourseScanner(_ephemeris);
    }

    /// <summary>
    /// Helper to get 30-day VOC periods starting from 2026-01-01.
    /// Cached via Lazy to avoid rescanning in every test.
    /// </summary>
    private IReadOnlyList<VoidOfCoursePeriod> Get30DayVocPeriods()
    {
        double startJd = _ephemeris.DateToJulianDay(2026, 1, 1, 0.0);
        double endJd = _ephemeris.DateToJulianDay(2026, 1, 31, 0.0);
        return _scanner.Scan(startJd, endJd, "UTC");
    }

    #region Period Count

    [Fact]
    public void Scan_30DayRange_Should_ReturnMultipleVocPeriods()
    {
        // Arrange & Act
        IReadOnlyList<VoidOfCoursePeriod> periods = Get30DayVocPeriods();

        // Assert — Moon changes sign roughly every 2.5 days,
        // so in 30 days there should be at least 10 VOC periods
        periods.Should().HaveCountGreaterThanOrEqualTo(10,
            "Moon changes sign ~12 times per month, each change has a preceding VOC period");
    }

    #endregion

    #region Start Before End

    [Fact]
    public void Scan_VocPeriods_Should_HaveStartBeforeEnd()
    {
        // Arrange & Act
        IReadOnlyList<VoidOfCoursePeriod> periods = Get30DayVocPeriods();

        // Assert
        foreach (VoidOfCoursePeriod period in periods)
        {
            period.StartLocal.Should().BeBefore(period.EndLocal,
                $"VOC period in {period.MoonSign} must have start before end");
        }
    }

    #endregion

    #region Moon Sign

    [Fact]
    public void Scan_VocPeriods_Should_HaveNonEmptyMoonSign()
    {
        // Arrange & Act
        IReadOnlyList<VoidOfCoursePeriod> periods = Get30DayVocPeriods();

        // Assert
        foreach (VoidOfCoursePeriod period in periods)
        {
            period.MoonSign.Should().NotBeNullOrWhiteSpace(
                "every VOC period must have a Moon sign");
        }
    }

    #endregion

    #region Duration

    [Fact]
    public void Scan_VocPeriods_Should_CoverReasonableDuration()
    {
        // Arrange & Act
        IReadOnlyList<VoidOfCoursePeriod> periods = Get30DayVocPeriods();

        // Assert — most VOC periods should be between 5 minutes and 24 hours.
        // Allow up to 10% outliers for unusual configurations.
        int reasonableCount = 0;

        foreach (VoidOfCoursePeriod period in periods)
        {
            TimeSpan duration = period.EndLocal - period.StartLocal;

            if (duration.TotalMinutes >= 5.0 && duration.TotalHours <= 24.0)
            {
                reasonableCount++;
            }
        }

        double reasonableRatio = (double)reasonableCount / periods.Count;
        reasonableRatio.Should().BeGreaterThanOrEqualTo(0.90,
            "at least 90% of VOC periods should be between 5 minutes and 24 hours");
    }

    #endregion
}
