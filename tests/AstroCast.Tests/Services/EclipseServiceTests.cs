using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="EclipseService"/> verifying solar and lunar eclipse finding,
/// type classification, phase timing, and precision using the Swiss Ephemeris
/// dedicated eclipse functions. Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class EclipseServiceTests
{
    private readonly EclipseService _service;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the service.
    /// </summary>
    public EclipseServiceTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        _service = new EclipseService(fixture.EphemerisService, logger);
    }

    #region Known Solar Eclipses

    /// <summary>
    /// April 8, 2024 — Total Solar Eclipse. A well-known event.
    /// </summary>
    [Fact]
    public void FindSolarEclipses_2024_FindsTotalSolarEclipseInApril()
    {
        EclipseSeries series = _service.FindSolarEclipses(2024, 2024);

        series.SolarEclipses.Should().NotBeEmpty();

        EclipseResult april = series.Eclipses
            .First(e => e.MaximumUtc.Month == 4);

        april.Type.Should().Be("Total");
        april.Category.Should().Be("Solar");
        april.IsSolar.Should().BeTrue();
        april.MaximumUtc.Day.Should().Be(8);
        april.FullLabel.Should().Be("Total Solar Eclipse");
    }

    /// <summary>
    /// Total solar eclipse should have first contact, last contact, and the central phase's begin and end over the whole Earth.
    /// </summary>
    [Fact]
    public void FindSolarEclipses_TotalSolar_HasTotalityPhases()
    {
        EclipseSeries series = _service.FindSolarEclipses(2024, 2024);

        EclipseResult total = series.Eclipses
            .First(e => e.Type == "Total");

        total.FirstContactUtc.Should().NotBeNull();
        total.LastContactUtc.Should().NotBeNull();
        total.TotalityBeginUtc.Should().NotBeNull();
        total.TotalityEndUtc.Should().NotBeNull();
        total.TotalityDurationMinutes.Should().NotBeNull();
        total.TotalityDurationMinutes!.Value.Should().BeGreaterThan(0);
    }

    /// <summary>
    /// Solar eclipse Sun and Moon should be in the same sign (they are conjunct).
    /// </summary>
    [Fact]
    public void FindSolarEclipses_SunAndMoonInSameSign()
    {
        EclipseSeries series = _service.FindSolarEclipses(2024, 2024);

        EclipseResult eclipse = series.Eclipses.First();

        // Sun and Moon should be very close in longitude at a solar eclipse
        double separation = Math.Abs(eclipse.SunLongitude - eclipse.MoonLongitude);
        if (separation > 180) separation = 360 - separation;
        separation.Should().BeLessThan(2.0,
            "Sun and Moon should be nearly conjunct at a solar eclipse");
    }

    #endregion

    #region Known Lunar Eclipses

    /// <summary>
    /// March 14, 2025 — Total Lunar Eclipse. Widely reported as total (NASA, timeanddate.com).
    /// </summary>
    [Fact]
    public void FindLunarEclipses_2025_FindsTotalLunarEclipseInMarch()
    {
        EclipseSeries series = _service.FindLunarEclipses(2025, 2025);

        series.LunarEclipses.Should().NotBeEmpty();

        EclipseResult march = series.Eclipses
            .First(e => e.MaximumUtc.Month == 3);

        march.Category.Should().Be("Lunar");
        march.IsSolar.Should().BeFalse();
        march.MaximumUtc.Day.Should().Be(14);
        march.Type.Should().Be("Total");
        march.FullLabel.Should().Be("Total Lunar Eclipse");
    }

    /// <summary>
    /// Total lunar eclipse should have penumbral, partial, and totality phase times.
    /// </summary>
    [Fact]
    public void FindLunarEclipses_TotalLunar_HasAllPhases()
    {
        EclipseSeries series = _service.FindLunarEclipses(2025, 2025);

        EclipseResult total = series.Eclipses.First(e => e.Type == "Total");

        total.PenumbralBeginUtc.Should().NotBeNull();
        total.PenumbralEndUtc.Should().NotBeNull();
        total.FirstContactUtc.Should().NotBeNull("partial begin should be set");
        total.LastContactUtc.Should().NotBeNull("partial end should be set");
        total.TotalityBeginUtc.Should().NotBeNull();
        total.TotalityEndUtc.Should().NotBeNull();
        total.TotalityDurationMinutes.Should().NotBeNull();
    }

    /// <summary>
    /// September 7, 2025 — Total Lunar Eclipse (NASA).
    /// </summary>
    [Fact]
    public void FindLunarEclipses_2025_FindsTotalLunarEclipseInSeptember()
    {
        EclipseSeries series = _service.FindLunarEclipses(2025, 2025);

        EclipseResult sept = series.Eclipses
            .First(e => e.MaximumUtc.Month == 9);

        sept.Type.Should().Be("Total");
        sept.MaximumUtc.Day.Should().Be(7);
    }

    /// <summary>
    /// September 18, 2024 — Partial Lunar Eclipse (NASA).
    /// </summary>
    [Fact]
    public void FindLunarEclipses_2024_FindsPartialLunarEclipseInSeptember()
    {
        EclipseSeries series = _service.FindLunarEclipses(2024, 2024);

        EclipseResult sept = series.Eclipses
            .First(e => e.MaximumUtc.Month == 9);

        sept.Type.Should().Be("Partial");
        sept.MaximumUtc.Day.Should().Be(18);
    }

    /// <summary>
    /// March 25, 2024 — Penumbral Lunar Eclipse (NASA).
    /// </summary>
    [Fact]
    public void FindLunarEclipses_2024_FindsPenumbralLunarEclipseInMarch()
    {
        EclipseSeries series = _service.FindLunarEclipses(2024, 2024);

        EclipseResult march = series.Eclipses
            .First(e => e.MaximumUtc.Month == 3);

        march.Type.Should().Be("Penumbral");
        march.MaximumUtc.Day.Should().Be(25);
    }

    /// <summary>
    /// October 28, 2023 — Partial Lunar Eclipse (NASA).
    /// </summary>
    [Fact]
    public void FindLunarEclipses_2023_FindsPartialLunarEclipseInOctober()
    {
        EclipseSeries series = _service.FindLunarEclipses(2023, 2023);

        EclipseResult oct = series.Eclipses
            .First(e => e.MaximumUtc.Month == 10);

        oct.Type.Should().Be("Partial");
        oct.MaximumUtc.Day.Should().Be(28);
    }

    /// <summary>
    /// May 5, 2023 — Penumbral Lunar Eclipse (NASA).
    /// </summary>
    [Fact]
    public void FindLunarEclipses_2023_FindsPenumbralLunarEclipseInMay()
    {
        EclipseSeries series = _service.FindLunarEclipses(2023, 2023);

        EclipseResult may = series.Eclipses
            .First(e => e.MaximumUtc.Month == 5);

        may.Type.Should().Be("Penumbral");
        may.MaximumUtc.Day.Should().Be(5);
    }

    /// <summary>
    /// Lunar eclipse Moon should be opposite the Sun.
    /// </summary>
    [Fact]
    public void FindLunarEclipses_MoonOppositeSun()
    {
        EclipseSeries series = _service.FindLunarEclipses(2025, 2025);

        EclipseResult eclipse = series.Eclipses.First();

        // Moon should be roughly 180° from Sun
        double separation = Math.Abs(eclipse.MoonLongitude - eclipse.SunLongitude);
        if (separation > 180) separation = 360 - separation;
        separation.Should().BeGreaterThan(170.0,
            "Moon should be roughly opposite the Sun at a lunar eclipse");
    }

    #endregion

    #region Eclipse Count Per Year

    /// <summary>
    /// A 3-year window should produce a reasonable number of eclipses (typically 12-18).
    /// </summary>
    [Fact]
    public void FindEclipses_3YearWindow_FindsReasonableCount()
    {
        EclipseSeries series = _service.FindEclipses(2025, 2027);

        series.Count.Should().BeGreaterThanOrEqualTo(10,
            "3 years should have at least 10 eclipses");
        series.Count.Should().BeLessThanOrEqualTo(25,
            "3 years should not exceed 25 eclipses");
        series.SolarEclipses.Count.Should().BeGreaterThanOrEqualTo(4);
        series.LunarEclipses.Count.Should().BeGreaterThanOrEqualTo(4);
    }

    #endregion

    #region Penumbral Lunar Eclipses

    /// <summary>
    /// Penumbral lunar eclipses should be correctly identified and have penumbral
    /// times but no totality times.
    /// </summary>
    [Fact]
    public void FindLunarEclipses_Penumbral_NoTotalityTimes()
    {
        // Search a wider range to find a penumbral eclipse
        EclipseSeries series = _service.FindLunarEclipses(2023, 2027);

        EclipseResult? penumbral = series.Eclipses
            .FirstOrDefault(e => e.Type == "Penumbral");

        penumbral.Should().NotBeNull("should find at least one penumbral lunar eclipse in 5 years");

        penumbral!.PenumbralBeginUtc.Should().NotBeNull();
        penumbral.PenumbralEndUtc.Should().NotBeNull();
        penumbral.TotalityBeginUtc.Should().BeNull("penumbral has no totality");
        penumbral.TotalityEndUtc.Should().BeNull("penumbral has no totality");
        penumbral.TotalityDurationMinutes.Should().BeNull();
    }

    #endregion

    #region Chronological Ordering

    /// <summary>
    /// Combined solar and lunar eclipses should be sorted chronologically.
    /// </summary>
    [Fact]
    public void FindEclipses_ChronologicalOrder()
    {
        EclipseSeries series = _service.FindEclipses(2024, 2026);

        series.Count.Should().BeGreaterThan(1);

        for (int i = 1; i < series.Eclipses.Count; i++)
        {
            series.Eclipses[i].MaximumUtc.Should().BeAfter(series.Eclipses[i - 1].MaximumUtc,
                $"eclipse {i} should be after eclipse {i - 1}");
        }
    }

    /// <summary>
    /// Solar and lunar eclipses should interleave correctly in the combined results.
    /// </summary>
    [Fact]
    public void FindEclipses_SolarAndLunarInterleave()
    {
        EclipseSeries series = _service.FindEclipses(2024, 2026);

        series.SolarEclipses.Should().NotBeEmpty();
        series.LunarEclipses.Should().NotBeEmpty();

        // Both types should be present in the combined list
        series.Eclipses.Should().Contain(e => e.IsSolar);
        series.Eclipses.Should().Contain(e => !e.IsSolar);
    }

    #endregion

    #region Precision and Validity

    /// <summary>
    /// All eclipse maximum times should fall within the scan range.
    /// </summary>
    [Fact]
    public void FindEclipses_MaximumTimesWithinScanRange()
    {
        EclipseSeries series = _service.FindEclipses(2025, 2026);

        series.Eclipses.Should().AllSatisfy(e =>
        {
            e.MaximumUtc.Year.Should().BeGreaterThanOrEqualTo(2025);
            e.MaximumUtc.Year.Should().BeLessThanOrEqualTo(2026);
        });
    }

    /// <summary>
    /// All eclipses should have node distance less than 20 degrees.
    /// </summary>
    [Fact]
    public void FindEclipses_NodeDistanceLessThan20()
    {
        EclipseSeries series = _service.FindEclipses(2024, 2026);

        series.Eclipses.Should().AllSatisfy(e =>
            e.NodeDistance.Should().BeLessThan(20.0,
                $"eclipse at {e.MaximumUtc} should be near a node"));
    }

    /// <summary>
    /// All eclipses should have valid positional data.
    /// </summary>
    [Fact]
    public void FindEclipses_ValidPositionalData()
    {
        EclipseSeries series = _service.FindEclipses(2025, 2025);

        series.Eclipses.Should().AllSatisfy(e =>
        {
            e.Sign.Should().NotBeNullOrEmpty();
            e.PositionFormatted.Should().NotBeNullOrEmpty();
            e.Element.Should().NotBeNullOrEmpty();
            e.Modality.Should().NotBeNullOrEmpty();
            e.SunLongitude.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(360);
            e.MoonLongitude.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(360);
        });
    }

    #endregion

    #region Phase Time Consistency

    /// <summary>
    /// For total solar eclipses: first contact &lt; central begin &lt; maximum &lt; central end &lt; last contact
    /// (TotalityBeginUtc and TotalityEndUtc bound the central phase over the whole Earth).
    /// </summary>
    [Fact]
    public void FindSolarEclipses_TotalSolar_PhaseTimesInOrder()
    {
        EclipseSeries series = _service.FindSolarEclipses(2024, 2024);

        EclipseResult total = series.Eclipses.First(e => e.Type == "Total");

        total.FirstContactUtc.Should().NotBeNull();
        total.TotalityBeginUtc.Should().NotBeNull();
        total.TotalityEndUtc.Should().NotBeNull();
        total.LastContactUtc.Should().NotBeNull();

        total.FirstContactUtc!.Value.Should().BeBefore(total.TotalityBeginUtc!.Value);
        total.TotalityBeginUtc!.Value.Should().BeBefore(total.MaximumUtc);
        total.MaximumUtc.Should().BeBefore(total.TotalityEndUtc!.Value);
        total.TotalityEndUtc!.Value.Should().BeBefore(total.LastContactUtc!.Value);
    }

    /// <summary>
    /// For total lunar eclipses: penumbralBegin &lt; partialBegin &lt; totalBegin &lt; maximum &lt; ...
    /// </summary>
    [Fact]
    public void FindLunarEclipses_TotalLunar_PhaseTimesInOrder()
    {
        EclipseSeries series = _service.FindLunarEclipses(2018, 2030);

        EclipseResult? total = series.Eclipses.FirstOrDefault(e => e.Type == "Total");

        total.Should().NotBeNull("should find at least one Total lunar eclipse in 2018-2030");

        total!.PenumbralBeginUtc.Should().NotBeNull();
        total.FirstContactUtc.Should().NotBeNull();
        total.TotalityBeginUtc.Should().NotBeNull();
        total.TotalityEndUtc.Should().NotBeNull();
        total.LastContactUtc.Should().NotBeNull();
        total.PenumbralEndUtc.Should().NotBeNull();

        total.PenumbralBeginUtc!.Value.Should().BeBefore(total.FirstContactUtc!.Value);
        total.FirstContactUtc!.Value.Should().BeBefore(total.TotalityBeginUtc!.Value);
        total.TotalityBeginUtc!.Value.Should().BeBefore(total.MaximumUtc);
        total.MaximumUtc.Should().BeBefore(total.TotalityEndUtc!.Value);
        total.TotalityEndUtc!.Value.Should().BeBefore(total.LastContactUtc!.Value);
        total.LastContactUtc!.Value.Should().BeBefore(total.PenumbralEndUtc!.Value);
    }

    #endregion

    #region Guard Clauses

    /// <summary>
    /// Start year greater than end year should throw ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void FindEclipses_StartAfterEnd_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => _service.FindEclipses(2030, 2025);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    #endregion

    #region Series Metadata

    /// <summary>
    /// Series metadata should be correctly populated.
    /// </summary>
    [Fact]
    public void FindEclipses_SeriesMetadata_IsCorrectlyPopulated()
    {
        EclipseSeries series = _service.FindEclipses(2025, 2025);

        series.Count.Should().Be(series.Eclipses.Count);
        series.ScanStart.Year.Should().Be(2025);
        series.ScanEnd.Year.Should().Be(2025);
        series.EngineVersion.Should().StartWith("AstroCast");
        series.CalculatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region FindSolarEclipses / FindLunarEclipses Filtering

    /// <summary>
    /// FindSolarEclipses should return only solar eclipses.
    /// </summary>
    [Fact]
    public void FindSolarEclipses_ReturnsOnlySolar()
    {
        EclipseSeries series = _service.FindSolarEclipses(2024, 2026);

        series.Eclipses.Should().AllSatisfy(e =>
        {
            e.IsSolar.Should().BeTrue();
            e.Category.Should().Be("Solar");
        });
    }

    /// <summary>
    /// FindLunarEclipses should return only lunar eclipses.
    /// </summary>
    [Fact]
    public void FindLunarEclipses_ReturnsOnlyLunar()
    {
        EclipseSeries series = _service.FindLunarEclipses(2024, 2026);

        series.Eclipses.Should().AllSatisfy(e =>
        {
            e.IsSolar.Should().BeFalse();
            e.Category.Should().Be("Lunar");
        });
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="EclipseService.FindEclipses"/> honours
    /// the supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before any eclipse search iteration runs.
    /// </summary>
    [Fact]
    public void FindEclipses_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => _service.FindEclipses(2000, 2050, cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the eclipse sweep immediately");
    }

    #endregion
}
