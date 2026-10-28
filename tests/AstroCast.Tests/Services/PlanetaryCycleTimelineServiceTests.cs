using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="PlanetaryCycleTimelineService"/> verifying phase point
/// computation, chronological ordering, retrograde handling, and snapshot generation.
/// Uses the shared "Ephemeris" collection fixture for integration testing.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class PlanetaryCycleTimelineServiceTests
{
    private readonly ChartEngine _engine;
    private readonly PlanetaryCycleTimelineService _service;
    private readonly EphemerisFixture _fixture;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds all required services.
    /// </summary>
    public PlanetaryCycleTimelineServiceTests(EphemerisFixture fixture)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        DignityService dignity = new();
        MoonConditionService moonCondition = new();
        ChartContextService chartContext = new(dignity, new SectService());
        FixedStarService fixedStars = new(fixture.EphemerisService, logger);

        _engine = new ChartEngine(
            fixture.EphemerisService, aspects, midpoints, dignity,
            moonCondition, chartContext, fixedStars, new DeclinationAspectService(),
            new PlanetConditionService(), new MoonMansionService(),
            new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(new DignityService()), new AntisciaService(),
            new StationService(), new ParanService(fixture.EphemerisService, logger),
            logger);

        PlanetaryReturnService returnService = new(
            fixture.EphemerisService, _engine, logger);

        _service = new PlanetaryCycleTimelineService(
            fixture.EphemerisService, returnService, logger);
        _fixture = fixture;
    }

    /// <summary>
    /// Standard test natal chart: J2000.0 epoch, New York City.
    /// </summary>
    private ChartResult CalculateNatalChart() => _engine.Calculate(new ChartInput
    {
        Name = "Test Person",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
    });

    #region Phase Point Count and Structure

    [Fact]
    public void Calculate_Mars_HasExactly5PhasePoints()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Mars, 1);

        timeline.PhasePoints.Should().HaveCount(5);
    }

    [Fact]
    public void Calculate_Mars_PhaseAnglesAre0_90_180_270_360()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Mars, 1);

        int[] expectedAngles = [0, 90, 180, 270, 360];
        timeline.PhasePoints.Select(p => p.PhaseAngle)
            .Should().BeEquivalentTo(expectedAngles, opts => opts.WithStrictOrdering());
    }

    [Fact]
    public void Calculate_Mars_PhaseNamesAreCorrect()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Mars, 1);

        string[] expectedNames = ["Return", "Opening Square", "Opposition", "Closing Square", "Next Return"];
        timeline.PhasePoints.Select(p => p.PhaseName)
            .Should().BeEquivalentTo(expectedNames, opts => opts.WithStrictOrdering());
    }

    #endregion

    #region Chronological Ordering

    [Fact]
    public void Calculate_Mars_PhasePointsAreChronological()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Mars, 1);

        for (int i = 1; i < timeline.PhasePoints.Count; i++)
        {
            timeline.PhasePoints[i].ExactUtc.Should().BeAfter(
                timeline.PhasePoints[i - 1].ExactUtc,
                $"phase point {i} should be after phase point {i - 1}");
        }
    }

    [Fact]
    public void Calculate_Jupiter_PhasePointsAreChronological()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Jupiter, 1);

        for (int i = 1; i < timeline.PhasePoints.Count; i++)
        {
            timeline.PhasePoints[i].ExactUtc.Should().BeAfter(
                timeline.PhasePoints[i - 1].ExactUtc);
        }
    }

    #endregion

    #region Cycle Duration

    [Fact]
    public void Calculate_Mars_CycleDurationApproximately687Days()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Mars, 1);

        timeline.CycleDurationDays.Should().BeInRange(500, 850,
            "Mars cycle ~687 days with significant retrograde variation");
    }

    [Fact]
    public void Calculate_Jupiter_CycleDurationApproximately4333Days()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Jupiter, 1);

        timeline.CycleDurationDays.Should().BeInRange(4000, 4700,
            "Jupiter cycle ~4333 days with retrograde variation");
    }

    #endregion

    #region DaysFromStart and CyclePercentage

    [Fact]
    public void Calculate_Mars_ReturnHasZeroDaysFromStart()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Mars, 1);

        timeline.PhasePoints[0].DaysFromStart.Should().Be(0);
        timeline.PhasePoints[0].CyclePercentage.Should().Be(0);
    }

    [Fact]
    public void Calculate_Mars_NextReturnHas100PercentCyclePercentage()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Mars, 1);

        timeline.PhasePoints[^1].CyclePercentage.Should().BeApproximately(100.0, 0.5);
    }

    [Fact]
    public void Calculate_Mars_OpeningSquareIsRoughly20To30Percent()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Mars, 1);

        timeline.PhasePoints[1].CyclePercentage.Should().BeInRange(15, 40,
            "opening square should be roughly 20-30% through the cycle");
    }

    #endregion

    #region Target Longitude

    [Fact]
    public void Calculate_Mars_TargetLongitudesAreCorrect()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Mars, 1);

        double natalLon = timeline.NatalLongitude;

        timeline.PhasePoints[0].TargetLongitude.Should().BeApproximately(natalLon, 0.001);
        timeline.PhasePoints[1].TargetLongitude.Should().BeApproximately(
            (natalLon + 90) % 360.0, 0.001);
        timeline.PhasePoints[2].TargetLongitude.Should().BeApproximately(
            (natalLon + 180) % 360.0, 0.001);
        timeline.PhasePoints[3].TargetLongitude.Should().BeApproximately(
            (natalLon + 270) % 360.0, 0.001);
        timeline.PhasePoints[4].TargetLongitude.Should().BeApproximately(natalLon, 0.001);
    }

    #endregion

    #region Retrograde Handling

    [Fact]
    public void Calculate_Mars_AllPassCountsAreAtLeast1()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Mars, 1);

        foreach (CyclePhasePoint point in timeline.PhasePoints)
        {
            point.PassCount.Should().BeGreaterThanOrEqualTo(1,
                $"{point.PhaseName} should have at least 1 pass");
            point.AllPasses.Should().HaveCount(point.PassCount);
        }
    }

    #endregion

    #region Snapshots

    [Fact]
    public void Calculate_WithoutSnapshots_SnapshotIsNull()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Mars, 1, includeSnapshots: false);

        foreach (CyclePhasePoint point in timeline.PhasePoints)
        {
            point.Snapshot.Should().BeNull();
        }
    }

    [Fact]
    public void Calculate_WithSnapshots_SnapshotContainsPlanets()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Mars, 1, includeSnapshots: true);

        foreach (CyclePhasePoint point in timeline.PhasePoints)
        {
            point.Snapshot.Should().NotBeNull();
            point.Snapshot!.Count.Should().BeGreaterThan(0,
                $"{point.PhaseName} snapshot should have planets");
        }
    }

    #endregion

    #region Metadata

    [Fact]
    public void Calculate_Mars_MetadataIsCorrect()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Mars, 1);

        timeline.Planet.Should().Be(CelestialBodyId.Mars);
        timeline.PlanetName.Should().Be("Mars");
        timeline.ReturnNumber.Should().Be(1);
        timeline.NativeName.Should().Be("Test Person");
        timeline.NatalPositionFormatted.Should().NotBeNullOrWhiteSpace();
        timeline.EngineVersion.Should().Contain("AstroCast");
    }

    #endregion

    #region Branch F — cycles between first passes (F5, F6)

    /// <summary>Tolerance on every Tier B instant.</summary>
    private static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);

    /// <summary>
    /// F5 (R7). The Jupiter cycle that holds 2026-10-03 is #2, from return #2's first pass,
    /// 2023-04-26 11:15:50 UT, to #3's, 2035-04-10 00:53:17 (Tier B): two passages have a first
    /// pass at or before the date. Until branch F the cycle for a date went through the return
    /// nearest the date's year, and 2026 has no Jupiter pass, so the call threw (T2-3).
    /// </summary>
    [Fact]
    public void CalculateForDate_Jupiter2026_IsCycle2()
    {
        PlanetaryCycleTimeline timeline = _service.CalculateForDate(
            GoldenFixtures.J2000Nyc(_fixture), CelestialBodyId.Jupiter, new DateTime(2026, 10, 3));

        timeline.ReturnNumber.Should().Be(2);
        timeline.PhasePoints[0].PhaseName.Should().Be("Return");
        timeline.PhasePoints[0].ExactUtc.Should().BeCloseTo(new DateTime(2023, 4, 26, 11, 15, 50, DateTimeKind.Utc), FiveSeconds);
        timeline.PhasePoints[^1].PhaseName.Should().Be("Next Return");
        timeline.PhasePoints[^1].ExactUtc.Should().BeCloseTo(new DateTime(2035, 4, 10, 0, 53, 17, DateTimeKind.Utc), FiveSeconds);
        timeline.CycleDurationDays.Should().BeApproximately(4366.6, 0.1);
    }

    /// <summary>
    /// F6 (R7). Saturn's cycle #1 runs from return #1's first pass, and its Return row carries the
    /// passage's three passes (2028-07-21 08:08:04, 2028-09-24 18:23:07 retrograde, 2029-04-01
    /// 21:20:28); the Next Return row, #2 at 2058-05-09 02:00:30, has one (Tier B). Until branch F
    /// both return rows always read one pass, the chart's instant (T5-3).
    /// </summary>
    [Fact]
    public void Calculate_Saturn1_ReturnRowCarriesThePassagesThreePasses()
    {
        PlanetaryCycleTimeline timeline = _service.Calculate(
            GoldenFixtures.J2000Nyc(_fixture), CelestialBodyId.Saturn, 1);

        CyclePhasePoint start = timeline.PhasePoints[0];
        start.PhaseName.Should().Be("Return");
        start.PassCount.Should().Be(3);
        start.AllPasses.Should().HaveCount(3);
        start.AllPasses[0].Should().BeCloseTo(new DateTime(2028, 7, 21, 8, 8, 4, DateTimeKind.Utc), FiveSeconds);
        start.AllPasses[1].Should().BeCloseTo(new DateTime(2028, 9, 24, 18, 23, 7, DateTimeKind.Utc), FiveSeconds);
        start.AllPasses[2].Should().BeCloseTo(new DateTime(2029, 4, 1, 21, 20, 28, DateTimeKind.Utc), FiveSeconds);
        start.ExactUtc.Should().BeCloseTo(start.AllPasses[0], FiveSeconds);

        CyclePhasePoint end = timeline.PhasePoints[^1];
        end.PhaseName.Should().Be("Next Return");
        end.PassCount.Should().Be(1);
        end.ExactUtc.Should().BeCloseTo(new DateTime(2058, 5, 9, 2, 0, 30, DateTimeKind.Utc), FiveSeconds);
    }

    #endregion

    #region CalculateForDate

    [Fact]
    public void CalculateForDate_MidCycle_FindsCorrectCycle()
    {
        ChartResult natal = CalculateNatalChart();

        // Mars return #1 is around age 2 (~2002). Use a date mid-cycle.
        PlanetaryCycleTimeline timeline = _service.CalculateForDate(
            natal, CelestialBodyId.Mars, new DateTime(2002, 6, 1));

        // The cycle should bracket mid-2002
        timeline.PhasePoints[0].ExactUtc.Should().BeBefore(new DateTime(2002, 6, 1));
        timeline.PhasePoints[^1].ExactUtc.Should().BeAfter(new DateTime(2002, 6, 1));
    }

    #endregion

    #region Guard Clauses

    [Fact]
    public void Calculate_UnsupportedPlanet_ThrowsArgumentException()
    {
        ChartResult natal = CalculateNatalChart();

        Action act = () => _service.Calculate(
            natal, CelestialBodyId.Moon, 1);

        act.Should().Throw<ArgumentException>()
           .WithParameterName("planet");
    }

    [Fact]
    public void Calculate_ReturnNumberLessThan1_ThrowsArgumentOutOfRangeException()
    {
        ChartResult natal = CalculateNatalChart();

        Action act = () => _service.Calculate(
            natal, CelestialBodyId.Mars, 0);

        act.Should().Throw<ArgumentOutOfRangeException>()
           .WithParameterName("returnNumber");
    }

    [Fact]
    public void Calculate_NullNatalChart_ThrowsArgumentNullException()
    {
        Action act = () => _service.Calculate(
            null!, CelestialBodyId.Mars, 1);

        act.Should().Throw<ArgumentNullException>()
           .WithParameterName("natalChart");
    }

    #endregion

    #region Saturn Cycle

    [Fact]
    public void Calculate_Saturn_HasFiveChronologicalPhasePoints()
    {
        ChartResult natal = CalculateNatalChart();

        PlanetaryCycleTimeline timeline = _service.Calculate(
            natal, CelestialBodyId.Saturn, 1);

        timeline.PhasePoints.Should().HaveCount(5);
        timeline.CycleDurationDays.Should().BeInRange(10000, 11500,
            "Saturn cycle ~10759 days");

        for (int i = 1; i < timeline.PhasePoints.Count; i++)
        {
            timeline.PhasePoints[i].ExactUtc.Should().BeAfter(
                timeline.PhasePoints[i - 1].ExactUtc);
        }
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="PlanetaryCycleTimelineService.Calculate"/> honours
    /// the supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before any phase-point computation runs.
    /// </summary>
    [Fact]
    public void Calculate_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        ChartResult natal = CalculateNatalChart();

        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => _service.Calculate(
            natal, CelestialBodyId.Saturn, 1, includeSnapshots: false, cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the phase-point computation immediately");
    }

    #endregion
}
