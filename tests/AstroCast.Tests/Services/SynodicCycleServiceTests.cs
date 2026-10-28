using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="SynodicCycleService"/> verifying conjunction finding,
/// synodic period computation, body ordering, and edge cases. Uses the shared
/// "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class SynodicCycleServiceTests
{
    private readonly SynodicCycleService _service;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the service.
    /// </summary>
    public SynodicCycleServiceTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        _service = new SynodicCycleService(fixture.EphemerisService, logger);
    }

    #region Jupiter–Saturn (Great Conjunction Cross-Reference)

    /// <summary>
    /// Jupiter–Saturn 2000–2100: should find approximately 5 conjunctions with
    /// an average synodic period near 19.86 years.
    /// </summary>
    [Fact]
    public void FindConjunctions_JupiterSaturn_2000To2100_FindsExpectedCount()
    {
        SynodicCycleSeries series = _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2000, 2100);

        series.Count.Should().BeInRange(4, 6,
            "Jupiter-Saturn should have ~5 conjunctions in 100 years");
        series.BodyAName.Should().Be("Jupiter");
        series.BodyBName.Should().Be("Saturn");
    }

    /// <summary>
    /// Jupiter–Saturn average synodic period should be near 19.86 years.
    /// </summary>
    [Fact]
    public void FindConjunctions_JupiterSaturn_AveragePeriod_NearExpected()
    {
        SynodicCycleSeries series = _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2000, 2100);

        series.AverageSynodicPeriodYears.Should().NotBeNull();
        series.AverageSynodicPeriodYears!.Value.Should().BeApproximately(19.86, 1.0,
            "Jupiter-Saturn average synodic period should be near 19.86 years");
    }

    /// <summary>
    /// Jupiter–Saturn 2020–2021: should find the Dec 21, 2020 Great Conjunction.
    /// </summary>
    [Fact]
    public void FindConjunctions_JupiterSaturn_2020_FindsDecember2020Conjunction()
    {
        SynodicCycleSeries series = _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2020, 2021);

        series.Count.Should().BeGreaterThanOrEqualTo(1);

        SynodicConjunction dec2020 = series.Conjunctions
            .First(c => c.DateTimeUtc.Year == 2020 && c.DateTimeUtc.Month == 12);

        dec2020.DateTimeUtc.Day.Should().Be(21,
            "the 2020 Great Conjunction was on December 21");
        dec2020.Sign.Should().Be("Aquarius",
            "the 2020 Great Conjunction was at ~0° Aquarius");
    }

    #endregion

    #region Mars–Jupiter

    /// <summary>
    /// Mars–Jupiter 2020–2030: should find approximately 4–5 conjunctions.
    /// </summary>
    [Fact]
    public void FindConjunctions_MarsJupiter_2020To2030_FindsExpectedCount()
    {
        SynodicCycleSeries series = _service.FindConjunctions(
            CelestialBodyId.Mars, CelestialBodyId.Jupiter, 2020, 2030);

        series.Count.Should().BeInRange(3, 7,
            "Mars-Jupiter should have ~4-5 conjunctions in 10 years");
        series.BodyAName.Should().Be("Mars");
        series.BodyBName.Should().Be("Jupiter");
    }

    #endregion

    #region Saturn–Uranus

    /// <summary>
    /// Saturn–Uranus 1980–2080: should find approximately 2 conjunctions
    /// (~45 year period).
    /// </summary>
    [Fact]
    public void FindConjunctions_SaturnUranus_1980To2080_FindsExpectedCount()
    {
        SynodicCycleSeries series = _service.FindConjunctions(
            CelestialBodyId.Saturn, CelestialBodyId.Uranus, 1980, 2080);

        series.Count.Should().BeInRange(1, 8,
            "Saturn-Uranus should have ~2 conjunctions in 100 years (may include triple passes)");
    }

    #endregion

    #region Body Ordering

    /// <summary>
    /// Passing bodies in reverse order (slower first) should produce the same
    /// result as passing them in the correct order (faster first).
    /// </summary>
    [Fact]
    public void FindConjunctions_SwapsBodiesToFasterFirst()
    {
        SynodicCycleSeries forward = _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2000, 2050);
        SynodicCycleSeries reversed = _service.FindConjunctions(
            CelestialBodyId.Saturn, CelestialBodyId.Jupiter, 2000, 2050);

        forward.Count.Should().Be(reversed.Count);
        forward.BodyAName.Should().Be(reversed.BodyAName);
        forward.BodyBName.Should().Be(reversed.BodyBName);

        for (int i = 0; i < forward.Count; i++)
        {
            forward.Conjunctions[i].DateTimeUtc.Should().Be(
                reversed.Conjunctions[i].DateTimeUtc);
        }
    }

    #endregion

    #region Synodic Period Computation

    /// <summary>
    /// First conjunction in a series should have null synodic period;
    /// subsequent ones should have computed values.
    /// </summary>
    [Fact]
    public void FindConjunctions_FirstHasNullPeriod_SubsequentHaveValues()
    {
        SynodicCycleSeries series = _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2000, 2100);

        series.Count.Should().BeGreaterThanOrEqualTo(2);

        series.Conjunctions[0].SynodicPeriodDays.Should().BeNull(
            "first conjunction has no predecessor");
        series.Conjunctions[0].SynodicPeriodYears.Should().BeNull();

        series.Conjunctions[1].SynodicPeriodDays.Should().NotBeNull(
            "second conjunction should have a computed period");
        series.Conjunctions[1].SynodicPeriodDays!.Value.Should().BeGreaterThan(0);
        series.Conjunctions[1].SynodicPeriodYears.Should().NotBeNull();
    }

    /// <summary>
    /// Statistics (avg, min, max) should be populated when multiple conjunctions exist.
    /// </summary>
    [Fact]
    public void FindConjunctions_MultipleConjunctions_HasStatistics()
    {
        SynodicCycleSeries series = _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2000, 2100);

        series.AverageSynodicPeriodDays.Should().NotBeNull();
        series.AverageSynodicPeriodYears.Should().NotBeNull();
        series.MinSynodicPeriodDays.Should().NotBeNull();
        series.MaxSynodicPeriodDays.Should().NotBeNull();

        series.MinSynodicPeriodDays!.Value.Should()
            .BeLessThanOrEqualTo(series.MaxSynodicPeriodDays!.Value);
    }

    #endregion

    #region Edge Cases

    /// <summary>
    /// A very narrow range may find no conjunctions — should return empty list.
    /// </summary>
    [Fact]
    public void FindConjunctions_NarrowRange_NoConjunctions_ReturnsEmptyList()
    {
        // Jupiter-Saturn: ~20 year period. 2 years with no conjunction inside.
        SynodicCycleSeries series = _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2022, 2023);

        series.Count.Should().Be(0);
        series.Conjunctions.Should().BeEmpty();
        series.AverageSynodicPeriodDays.Should().BeNull();
    }

    /// <summary>
    /// Same body for both parameters should throw ArgumentException.
    /// </summary>
    [Fact]
    public void FindConjunctions_SameBody_ThrowsArgumentException()
    {
        Action act = () => _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Jupiter, 2020, 2030);

        act.Should().Throw<ArgumentException>();
    }

    /// <summary>
    /// Start year greater than end year should throw ArgumentException.
    /// </summary>
    [Fact]
    public void FindConjunctions_StartAfterEnd_ThrowsArgumentException()
    {
        Action act = () => _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2030, 2020);

        act.Should().Throw<ArgumentException>();
    }

    #endregion

    #region Conjunction Metadata

    /// <summary>
    /// Each conjunction should have valid positional data.
    /// </summary>
    [Fact]
    public void FindConjunctions_AllConjunctions_HaveValidPositionalData()
    {
        SynodicCycleSeries series = _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2000, 2100);

        series.Conjunctions.Should().AllSatisfy(c =>
        {
            c.Longitude.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(360);
            c.Sign.Should().NotBeNullOrEmpty();
            c.Position.Should().NotBeNullOrEmpty();
            c.Element.Should().NotBeNullOrEmpty();
            c.Modality.Should().NotBeNullOrEmpty();
            c.PrecisionArcSeconds.Should().BeLessThan(60,
                "conjunction precision should be well under 1 arcminute");
        });
    }

    /// <summary>
    /// Conjunctions should be numbered sequentially starting from 1.
    /// </summary>
    [Fact]
    public void FindConjunctions_NumberedSequentially()
    {
        SynodicCycleSeries series = _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2000, 2100);

        for (int i = 0; i < series.Count; i++)
        {
            series.Conjunctions[i].Number.Should().Be(i + 1);
        }
    }

    #endregion

    #region Series Metadata

    /// <summary>
    /// Series metadata should be correctly populated.
    /// </summary>
    [Fact]
    public void FindConjunctions_SeriesMetadata_IsCorrectlyPopulated()
    {
        SynodicCycleSeries series = _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2000, 2050);

        series.StartDate.Year.Should().Be(2000);
        series.EndDate.Year.Should().Be(2050);
        series.EngineVersion.Should().StartWith("AstroCast");
        series.CalculatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
    }

    #endregion

    #region Formatter

    /// <summary>
    /// ToMarkdown should produce valid output with a timeline table and statistics.
    /// </summary>
    [Fact]
    public void ToMarkdown_ProducesValidOutput_WithTimelineAndStatistics()
    {
        SynodicCycleSeries series = _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2000, 2100);

        string md = SynodicCycleFormatter.ToMarkdown(series);

        md.Should().Contain("# Synodic Cycles: Jupiter\u2013Saturn");
        md.Should().Contain("**Conjunctions Found:**");
        md.Should().Contain("## Conjunction Timeline");
        md.Should().Contain("| # | Date (UTC)");
        md.Should().Contain("Average Synodic Period");
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="SynodicCycleService.FindConjunctions"/> honours
    /// the supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before any conjunction iteration runs.
    /// </summary>
    [Fact]
    public void FindConjunctions_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => _service.FindConjunctions(
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2000, 2100, cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the synodic-cycle sweep immediately");
    }

    #endregion
}
