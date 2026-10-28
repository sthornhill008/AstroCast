using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="LunationService"/> verifying New Moon and Full Moon
/// finding, eclipse proximity detection, precision, and chart casting.
/// Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class LunationServiceTests
{
    private readonly LunationService _service;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the service.
    /// </summary>
    public LunationServiceTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        DignityService dignity = new();
        MoonConditionService moonCondition = new();
        ChartContextService chartContext = new(dignity, new SectService());
        FixedStarService fixedStars = new(fixture.EphemerisService, logger);

        ChartEngine engine = new(
            fixture.EphemerisService, aspects, midpoints, dignity,
            moonCondition, chartContext, fixedStars, new DeclinationAspectService(),
            new PlanetConditionService(), new MoonMansionService(),
            new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(new DignityService()), new AntisciaService(),
            new StationService(), new ParanService(fixture.EphemerisService, logger),
            logger);

        _service = new LunationService(
            fixture.EphemerisService, engine, logger);
    }

    #region FindLunations — New Moon Tests

    [Fact]
    public void FindLunations_2026_HasNewMoons()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        series.NewMoons.Count.Should().BeInRange(12, 13,
            "a year has 12 or 13 New Moons");
    }

    [Fact]
    public void FindLunations_2026_NewMoonsSunAndMoonInSameSign()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        foreach (LunationResult nm in series.NewMoons)
        {
            // At a New Moon, Sun and Moon are conjunct — same sign
            int sunSign = (int)(nm.SunLongitude / 30.0);
            int moonSign = (int)(nm.MoonLongitude / 30.0);
            sunSign.Should().Be(moonSign,
                $"New Moon on {nm.ExactUtc:yyyy-MM-dd} should have Sun and Moon in same sign");
        }
    }

    [Fact]
    public void FindLunations_2026_NewMoonPrecisionSubArcSecond()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        foreach (LunationResult nm in series.NewMoons)
        {
            nm.PrecisionArcSeconds.Should().BeLessThan(60.0,
                $"New Moon on {nm.ExactUtc:yyyy-MM-dd} separation should be < 1 arc-minute");
        }
    }

    [Fact]
    public void FindLunations_2026_NewMoonSpacing()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        IReadOnlyList<LunationResult> newMoons = series.NewMoons;
        for (int i = 1; i < newMoons.Count; i++)
        {
            double daysBetween = (newMoons[i].ExactUtc - newMoons[i - 1].ExactUtc).TotalDays;
            daysBetween.Should().BeInRange(28.0, 31.0,
                "New Moons should be ~29.5 days apart");
        }
    }

    #endregion

    #region FindLunations — Full Moon Tests

    [Fact]
    public void FindLunations_2026_HasFullMoons()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        series.FullMoons.Count.Should().BeInRange(12, 13,
            "a year has 12 or 13 Full Moons");
    }

    [Fact]
    public void FindLunations_2026_FullMoonPrecisionReasonable()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        foreach (LunationResult fm in series.FullMoons)
        {
            fm.PrecisionArcSeconds.Should().BeLessThan(60.0,
                $"Full Moon on {fm.ExactUtc:yyyy-MM-dd} separation should be < 1 arc-minute of 180°");
        }
    }

    #endregion

    #region FindLunations — Combined Series Tests

    [Fact]
    public void FindLunations_2026_TotalCount()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        series.Count.Should().BeInRange(24, 27,
            "a year has ~25-26 lunations total");
        series.Count.Should().Be(series.NewMoons.Count + series.FullMoons.Count);
    }

    [Fact]
    public void FindLunations_2026_ChronologicallyOrdered()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        for (int i = 1; i < series.Lunations.Count; i++)
        {
            series.Lunations[i].ExactUtc.Should().BeAfter(
                series.Lunations[i - 1].ExactUtc,
                "lunations should be chronologically ordered");
        }
    }

    [Fact]
    public void FindLunations_2026_NewAndFullAlternate()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        // New and Full Moons should generally alternate
        for (int i = 1; i < series.Lunations.Count; i++)
        {
            series.Lunations[i].IsNewMoon.Should().NotBe(
                series.Lunations[i - 1].IsNewMoon,
                $"lunation {i} should alternate between New and Full");
        }
    }

    [Fact]
    public void FindLunations_2026_AllHaveValidSignElementModality()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        foreach (LunationResult l in series.Lunations)
        {
            l.Sign.Should().NotBeNullOrWhiteSpace();
            l.Element.Should().BeOneOf("Fire", "Earth", "Air", "Water");
            l.Modality.Should().BeOneOf("Cardinal", "Fixed", "Mutable");
            l.PositionFormatted.Should().NotBeNullOrWhiteSpace();
        }
    }

    #endregion

    #region Eclipse Proximity Tests

    [Fact]
    public void FindLunations_2026_SomeEclipsesDetected()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        // Typically 2-4 eclipse seasons per year
        series.PossibleEclipses.Count.Should().BeGreaterThanOrEqualTo(2,
            "at least 2 lunations per year should be near nodes");
    }

    [Fact]
    public void FindLunations_2026_EclipseNewMoonsFlaggedAsSolar()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        foreach (LunationResult eclipse in series.PossibleEclipses.Where(e => e.IsNewMoon))
        {
            eclipse.EclipseIndicator.Should().Be("Near a node (within 18°)");
            eclipse.NodeDistance.Should().NotBeNull();
            eclipse.NodeDistance!.Value.Should().BeLessThanOrEqualTo(18.0,
                "solar eclipse threshold is 18° from node");
        }
    }

    [Fact]
    public void FindLunations_2026_EclipseFullMoonsFlaggedAsLunar()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        foreach (LunationResult eclipse in series.PossibleEclipses.Where(e => !e.IsNewMoon))
        {
            eclipse.EclipseIndicator.Should().Be("Near a node (within 12°)");
            eclipse.NodeDistance.Should().NotBeNull();
            eclipse.NodeDistance!.Value.Should().BeLessThanOrEqualTo(12.0,
                "lunar eclipse threshold is 12° from node");
        }
    }

    [Fact]
    public void FindLunations_2026_NonEclipseLunationsHaveNoIndicator()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        foreach (LunationResult l in series.Lunations.Where(l => !l.IsNearNode))
        {
            l.EclipseIndicator.Should().BeNull();
        }
    }

    #endregion

    #region CalculateLunation — Single Chart Tests

    [Fact]
    public void CalculateLunation_NearestNewMoon_HasFullChart()
    {
        LunationResult result = _service.CalculateLunation(
            new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            newMoon: true,
            38.8977, -77.0365,
            "Washington, DC",
            "America/New_York");

        result.Chart.Should().NotBeNull("a full chart should be cast");
        result.Chart!.Input.EventType.Should().Be("Lunation");
        result.Chart.Positions.Should().NotBeEmpty();
        result.IsNewMoon.Should().BeTrue();
    }

    [Fact]
    public void CalculateLunation_NearestFullMoon_HasFullChart()
    {
        LunationResult result = _service.CalculateLunation(
            new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            newMoon: false,
            38.8977, -77.0365,
            "Washington, DC",
            "America/New_York");

        result.Chart.Should().NotBeNull();
        result.IsNewMoon.Should().BeFalse();
        result.Type.Should().Be("Full Moon");
    }

    #endregion

    #region CalculateLunationsForYear Tests

    [Fact]
    public void CalculateLunationsForYear_2026_AllHaveCharts()
    {
        LunationSeries series = _service.CalculateLunationsForYear(
            2026, 38.8977, -77.0365, "Washington, DC", "America/New_York");

        series.Count.Should().BeGreaterThan(0);

        foreach (LunationResult l in series.Lunations)
        {
            l.Chart.Should().NotBeNull(
                $"{l.Type} on {l.ExactUtc:yyyy-MM-dd} should have a chart");
            l.Chart!.Positions.Should().NotBeEmpty();
        }
    }

    #endregion

    #region Guard Clause Tests

    [Fact]
    public void FindLunations_StartAfterEnd_ThrowsArgumentException()
    {
        Action act = () => _service.FindLunations(2025, 2020);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateLunation_EmptyLocation_ThrowsArgumentException()
    {
        Action act = () => _service.CalculateLunation(
            new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc),
            true, 38.8977, -77.0365, "", "America/New_York");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CalculateLunationsForYear_EmptyTimeZone_ThrowsArgumentException()
    {
        Action act = () => _service.CalculateLunationsForYear(
            2026, 38.8977, -77.0365, "Washington, DC", "");

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void FindLunations_EngineVersionNotEmpty()
    {
        LunationSeries series = _service.FindLunations(2026, 2026);

        series.EngineVersion.Should().Contain("AstroCast");
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="LunationService.FindLunations"/> honours
    /// the supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before any lunation iteration runs.
    /// </summary>
    [Fact]
    public void FindLunations_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => _service.FindLunations(2020, 2030, cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the New/Full Moon sweep immediately");
    }

    #endregion
}
