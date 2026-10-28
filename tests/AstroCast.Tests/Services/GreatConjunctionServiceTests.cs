using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="GreatConjunctionService"/> verifying conjunction finding,
/// element/mutation analysis, and chart casting. Uses the shared "Ephemeris"
/// collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class GreatConjunctionServiceTests
{
    private readonly GreatConjunctionService _service;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the service.
    /// </summary>
    public GreatConjunctionServiceTests(EphemerisFixture fixture)
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

        _service = new GreatConjunctionService(
            fixture.EphemerisService, engine, logger);
    }

    #region FindConjunctions Tests

    [Fact]
    public void FindConjunctions_2000To2040_Returns2Conjunctions()
    {
        GreatConjunctionSeries series = _service.FindConjunctions(2000, 2040);

        // Should find ~2000 and ~2020 conjunctions
        series.Count.Should().BeGreaterThanOrEqualTo(2);
        series.Conjunctions.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void FindConjunctions_2020_ConjunctionInAquarius()
    {
        GreatConjunctionSeries series = _service.FindConjunctions(2019, 2022);

        series.Count.Should().BeGreaterThanOrEqualTo(1);

        GreatConjunctionResult conj2020 = series.Conjunctions
            .First(c => c.ExactUtc.Year == 2020);

        conj2020.Sign.Should().Be("Aquarius");
        conj2020.Element.Should().Be("Air");
        conj2020.Modality.Should().Be("Fixed");
        conj2020.ExactUtc.Month.Should().Be(12);
    }

    [Fact]
    public void FindConjunctions_2000_ConjunctionInTaurus()
    {
        GreatConjunctionSeries series = _service.FindConjunctions(1999, 2002);

        series.Count.Should().BeGreaterThanOrEqualTo(1);

        GreatConjunctionResult conj2000 = series.Conjunctions
            .First(c => c.ExactUtc.Year == 2000);

        conj2000.Sign.Should().Be("Taurus");
        conj2000.Element.Should().Be("Earth");
    }

    [Fact]
    public void FindConjunctions_SeriesChronologicallyOrdered()
    {
        GreatConjunctionSeries series = _service.FindConjunctions(1980, 2025);

        for (int i = 1; i < series.Conjunctions.Count; i++)
        {
            series.Conjunctions[i].ExactUtc.Should().BeAfter(
                series.Conjunctions[i - 1].ExactUtc);
        }
    }

    [Fact]
    public void FindConjunctions_IntervalBetweenDistinctConjunctions_Approximately20Years()
    {
        // Note: The 1980-81 great conjunction was a triple conjunction (3 exact passes
        // within ~10 months due to Jupiter retrograde). The aspect finder finds all 3
        // as separate entries. We verify that the interval between NON-TRIPLE passes
        // (year gap > 5) is roughly 20 years.
        GreatConjunctionSeries series = _service.FindConjunctions(1980, 2025);

        for (int i = 1; i < series.Conjunctions.Count; i++)
        {
            double gap = series.Conjunctions[i].YearsSincePrevious;
            if (gap > 5.0) // Skip triple-conjunction repeated passes
            {
                gap.Should().BeInRange(18, 22,
                    "great conjunctions occur roughly every 20 years");
            }
        }
    }

    [Fact]
    public void FindConjunctions_2020IsMutation_ElementChangeFromEarthToAir()
    {
        GreatConjunctionSeries series = _service.FindConjunctions(1998, 2022);

        // The 2000 conjunction is in Taurus (Earth), 2020 in Aquarius (Air)
        GreatConjunctionResult? conj2020 = series.Conjunctions
            .FirstOrDefault(c => c.ExactUtc.Year == 2020);

        conj2020.Should().NotBeNull();
        conj2020!.IsMutation.Should().BeTrue(
            "the 2020 conjunction shifted from Earth to Air");
    }

    [Fact]
    public void FindConjunctions_PrecisionReasonable()
    {
        GreatConjunctionSeries series = _service.FindConjunctions(2019, 2022);

        foreach (GreatConjunctionResult conj in series.Conjunctions)
        {
            conj.PrecisionArcSeconds.Should().BeLessThan(360,
                "Jupiter-Saturn separation should be < 6 arc-minutes at conjunction");
        }
    }

    #endregion

    #region FindNearest Tests

    [Fact]
    public void FindNearest_2015_Returns2020Conjunction()
    {
        GreatConjunctionResult nearest = _service.FindNearest(2015);

        // 2015 is between 2000 and 2020; closer to 2020
        nearest.ExactUtc.Year.Should().Be(2020);
    }

    [Fact]
    public void FindNearest_2005_Returns2000Conjunction()
    {
        GreatConjunctionResult nearest = _service.FindNearest(2005);

        // 2005 is between 2000 and 2020; closer to 2000
        nearest.ExactUtc.Year.Should().Be(2000);
    }

    #endregion

    #region CastConjunctionChart Tests

    [Fact]
    public void CastConjunctionChart_ReturnsValidChart()
    {
        GreatConjunctionResult conj = _service.FindNearest(2020);

        ChartResult chart = _service.CastConjunctionChart(
            conj, 38.8977, -77.0365, "Washington, DC", "America/New_York");

        chart.Should().NotBeNull();
        chart.Input.EventType.Should().Be("Great Conjunction");
        chart.Positions.Should().NotBeEmpty();
        chart.Aspects.Should().NotBeEmpty();
    }

    #endregion

    #region Guard Clause Tests

    [Fact]
    public void FindConjunctions_StartAfterEnd_ThrowsArgumentException()
    {
        Action act = () => _service.FindConjunctions(2025, 2020);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void CastConjunctionChart_NullConjunction_ThrowsArgumentNullException()
    {
        Action act = () => _service.CastConjunctionChart(
            null!, 38.8977, -77.0365, "Washington, DC", "America/New_York");

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void FindConjunctions_EngineVersionNotEmpty()
    {
        GreatConjunctionSeries series = _service.FindConjunctions(2019, 2022);

        series.EngineVersion.Should().Contain("AstroCast");
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="GreatConjunctionService.FindConjunctions"/> honours
    /// the supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before the century-sweep begins.
    /// </summary>
    [Fact]
    public void FindConjunctions_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => _service.FindConjunctions(1500, 2500, cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the Jupiter-Saturn sweep immediately");
    }

    #endregion
}
