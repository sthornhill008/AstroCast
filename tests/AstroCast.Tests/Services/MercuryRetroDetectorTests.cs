using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="MercuryRetroDetector"/> verifying
/// Mercury retrograde detection against known astronomical expectations.
/// </summary>
/// <remarks>
/// Mercury goes retrograde approximately 3 times per year for ~3 weeks each.
/// Uses the shared "Ephemeris" collection fixture.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class MercuryRetroDetectorTests
{
    private readonly MercuryRetroDetector _detector;
    private readonly EphemerisService _ephemeris;

    /// <summary>
    /// Receives the shared ephemeris fixture from the collection.
    /// </summary>
    public MercuryRetroDetectorTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        _detector = new MercuryRetroDetector(_ephemeris);
    }

    #region Full Year Count

    [Fact]
    public void FindRetrogradePeriods_2026_Should_FindKnownRetrogradeCount()
    {
        // Arrange — scan full year 2026
        double startJd = _ephemeris.DateToJulianDay(2026, 1, 1, 0.0);
        double endJd = _ephemeris.DateToJulianDay(2026, 12, 31, 23.99);

        // Act
        IReadOnlyList<DisqualifiedPeriod> periods = _detector.FindRetrogradePeriods(
            startJd, endJd, "UTC");

        // Assert — Mercury goes retrograde ~3 times per year
        periods.Should().HaveCountGreaterThanOrEqualTo(2,
            "Mercury should go retrograde at least 2 times in a year")
            .And.HaveCountLessThanOrEqualTo(4,
            "Mercury should go retrograde at most 4 times in a year");
    }

    #endregion

    #region Start Before End

    [Fact]
    public void FindRetrogradePeriods_Should_HaveStartBeforeEnd()
    {
        // Arrange
        double startJd = _ephemeris.DateToJulianDay(2026, 1, 1, 0.0);
        double endJd = _ephemeris.DateToJulianDay(2026, 12, 31, 23.99);

        // Act
        IReadOnlyList<DisqualifiedPeriod> periods = _detector.FindRetrogradePeriods(
            startJd, endJd, "UTC");

        // Assert
        foreach (DisqualifiedPeriod period in periods)
        {
            period.Start.Should().BeBefore(period.End,
                "retrograde period start must be before end");
        }
    }

    #endregion

    #region Reason

    [Fact]
    public void FindRetrogradePeriods_Should_HaveCorrectReason()
    {
        // Arrange
        double startJd = _ephemeris.DateToJulianDay(2026, 1, 1, 0.0);
        double endJd = _ephemeris.DateToJulianDay(2026, 12, 31, 23.99);

        // Act
        IReadOnlyList<DisqualifiedPeriod> periods = _detector.FindRetrogradePeriods(
            startJd, endJd, "UTC");

        // Assert
        foreach (DisqualifiedPeriod period in periods)
        {
            period.Reason.Should().Be("Mercury retrograde",
                "all periods from this detector should have reason 'Mercury retrograde'");
        }
    }

    #endregion

    #region Duration

    [Fact]
    public void FindRetrogradePeriods_RetrogradeLength_Should_BeReasonable()
    {
        // Arrange
        double startJd = _ephemeris.DateToJulianDay(2026, 1, 1, 0.0);
        double endJd = _ephemeris.DateToJulianDay(2026, 12, 31, 23.99);

        // Act
        IReadOnlyList<DisqualifiedPeriod> periods = _detector.FindRetrogradePeriods(
            startJd, endJd, "UTC");

        // Assert — Mercury retrograde typically lasts 18–24 days.
        // Allow 14–30 days to accommodate edge cases.
        foreach (DisqualifiedPeriod period in periods)
        {
            TimeSpan duration = period.End - period.Start;
            duration.TotalDays.Should().BeInRange(14.0, 30.0,
                $"Mercury retrograde period should last 14–30 days, " +
                $"got {duration.TotalDays:F1} days ({period.Start:yyyy-MM-dd} to {period.End:yyyy-MM-dd})");
        }
    }

    #endregion

    #region No Retrograde

    [Fact]
    public void FindRetrogradePeriods_ShortRange_NoRetrograde_Should_ReturnEmpty()
    {
        // Arrange — scan a 7-day range in April 2026, well outside any retrograde
        double startJd = _ephemeris.DateToJulianDay(2026, 4, 1, 0.0);
        double endJd = _ephemeris.DateToJulianDay(2026, 4, 8, 0.0);

        // Act
        IReadOnlyList<DisqualifiedPeriod> periods = _detector.FindRetrogradePeriods(
            startJd, endJd, "UTC");

        // Assert
        periods.Should().BeEmpty(
            "no Mercury retrograde should occur in early April 2026");
    }

    #endregion
}
