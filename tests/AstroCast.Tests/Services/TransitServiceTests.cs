using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Models.Progressions;
using AstroCast.Core.Models.SolarArcs;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services.PrimaryDirections;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;
using AstroCast.Tests.Services.MinorPoints;

using FluentAssertions;

using Xunit.Abstractions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="TransitService"/> verifying end-to-end
/// transit calculation from a real natal chart. Uses the J2000.0 test chart
/// (New York City, 2000-01-01 07:00 EST) as the natal input and a one-month
/// report window in 2026.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class TransitServiceTests
{
    private readonly TransitService _transitService;
    private readonly ChartResult _natalChart;
    private readonly IEphemerisService _ephemeris;
    private readonly TransitEphemerisHelper _helper;
    private readonly ChartResult _natalWithFive;
    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Receives the shared ephemeris fixture, builds a ChartEngine to produce
    /// the natal chart, then constructs the TransitService.
    /// </summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    /// <param name="output">Receives which gate kept Mars off the five (branch AsteroidsTiming, 1.6.0).</param>
    public TransitServiceTests(EphemerisFixture fixture, ITestOutputHelper output)
    {
        _output = output;
        _natalWithFive = GoldenFixtures.CreateChartEngine(fixture).Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true));

        // Build a real natal chart via ChartEngine
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);
        ChartEngine engine = new(fixture.EphemerisService, aspects, midpoints, new DignityService(), new MoonConditionService(), new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);

        ChartInput input = new()
        {
            Name = "J2000.0 Test",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
        };

        _natalChart = engine.Calculate(input);
        _transitService = new TransitService(fixture.EphemerisService, logger);
        _ephemeris = fixture.EphemerisService;
        _helper = new TransitEphemerisHelper(fixture.EphemerisService);
    }

    /// <summary>Creates a standard one-month transit input for March 2026.</summary>
    private TransitInput CreateMarchInput(bool includeMinorAspects = false) => new()
    {
        NatalChart = _natalChart,
        ReportStartDate = new DateOnly(2026, 3, 1),
        ReportEndDate = new DateOnly(2026, 3, 31),
        IncludeMinorAspects = includeMinorAspects,
    };

    #region Result Structure Tests

    [Fact]
    public void Calculate_Should_ReturnCompleteResult()
    {
        TransitInput input = CreateMarchInput();

        TransitResult result = _transitService.Calculate(input);

        result.Should().NotBeNull();
        result.NatalChart.Should().BeSameAs(_natalChart,
            "natal chart should be the same object passed in");
        result.ReportStartDate.Should().Be(new DateOnly(2026, 3, 1));
        result.ReportEndDate.Should().Be(new DateOnly(2026, 3, 31));
        result.MidpointDate.Should().Be(new DateOnly(2026, 3, 16),
            "midpoint of March 1–31 is March 16");
    }

    [Fact]
    public void Calculate_TransitPositions_Should_IncludeStandardBodies()
    {
        TransitInput input = CreateMarchInput();

        TransitResult result = _transitService.Calculate(input);

        // At minimum, the 7 standard transit bodies should have positions
        result.TransitPositions.Should().ContainKey(TransitBody.Pluto);
        result.TransitPositions.Should().ContainKey(TransitBody.Neptune);
        result.TransitPositions.Should().ContainKey(TransitBody.Uranus);
        result.TransitPositions.Should().ContainKey(TransitBody.Saturn);
        result.TransitPositions.Should().ContainKey(TransitBody.Jupiter);
        result.TransitPositions.Should().ContainKey(TransitBody.Chiron);
        result.TransitPositions.Should().ContainKey(TransitBody.NorthNode);

        foreach (TransitPlanetPosition position in result.TransitPositions.Values)
        {
            position.Longitude.Should().BeInRange(0.0, 360.0,
                "transit longitude should be in valid range");
            position.Sign.Should().NotBeNullOrEmpty(
                "transit sign should be set");
        }
    }

    #endregion

    #region Active Transits Tests

    [Fact]
    public void Calculate_ActiveTransits_Should_NotBeEmpty()
    {
        TransitInput input = CreateMarchInput();

        TransitResult result = _transitService.Calculate(input);

        result.ActiveTransits.Should().NotBeEmpty(
            "at least one outer planet should be aspecting a natal target in March 2026");
    }

    [Fact]
    public void Calculate_ActiveTransits_Should_BeSortedByPlanetOrder()
    {
        TransitInput input = CreateMarchInput();

        TransitResult result = _transitService.Calculate(input);

        // Verify outermost-first ordering: each transit's planet sort key
        // should be <= the next one
        for (int i = 0; i < result.ActiveTransits.Count - 1; i++)
        {
            TransitBody current = result.ActiveTransits[i].TransitPlanet;
            TransitBody next = result.ActiveTransits[i + 1].TransitPlanet;

            int currentOrder = TransitBodySortOrderForTest(current);
            int nextOrder = TransitBodySortOrderForTest(next);

            currentOrder.Should().BeLessThanOrEqualTo(nextOrder,
                $"{current} should appear before or alongside {next} in the sorted output");
        }
    }

    [Fact]
    public void Calculate_ActiveTransits_WithMinorAspects_Should_IncludeMore()
    {
        TransitInput majorOnly = CreateMarchInput(includeMinorAspects: false);
        TransitInput withMinor = CreateMarchInput(includeMinorAspects: true);

        TransitResult majorResult = _transitService.Calculate(majorOnly);
        TransitResult minorResult = _transitService.Calculate(withMinor);

        minorResult.ActiveTransits.Count.Should().BeGreaterThanOrEqualTo(
            majorResult.ActiveTransits.Count,
            "including minor aspects should produce at least as many active transits");
    }

    #endregion

    #region House Overlays Tests

    [Fact]
    public void Calculate_HouseOverlays_Should_HaveValidEntries()
    {
        TransitInput input = CreateMarchInput();

        TransitResult result = _transitService.Calculate(input);

        result.HouseOverlays.Should().NotBeEmpty(
            "there should be at least one house overlay");

        foreach (TransitHouseOverlay overlay in result.HouseOverlays)
        {
            overlay.HouseNumber.Should().BeInRange(1, 12,
                $"{overlay.Planet} house overlay should be between 1 and 12");
        }
    }

    #endregion

    #region Stations, Ingresses, and Midpoints Tests

    [Fact]
    public void Calculate_Stations_Should_BeListType()
    {
        TransitInput input = CreateMarchInput();

        TransitResult result = _transitService.Calculate(input);

        // Stations may or may not exist in any given month; just verify the list is present
        result.Stations.Should().NotBeNull(
            "stations list should be present even if empty");

        foreach (TransitStation station in result.Stations)
        {
            station.StationType.Should().BeOneOf("Retrograde", "Direct",
                "station type should be Retrograde or Direct");
        }
    }

    [Fact]
    public void Calculate_MidpointActivations_Should_BePresent()
    {
        TransitInput input = CreateMarchInput();

        TransitResult result = _transitService.Calculate(input);

        // Midpoint activations are very specific (10' orb), so they may be empty
        // for a given month. Just verify the list is present and any found are valid.
        result.MidpointActivations.Should().NotBeNull(
            "midpoint activations list should be present even if empty");

        foreach (TransitMidpointActivation activation in result.MidpointActivations)
        {
            activation.MidpointBody1.Should().NotBeNullOrEmpty(
                "midpoint body 1 should be set");
            activation.MidpointBody2.Should().NotBeNullOrEmpty(
                "midpoint body 2 should be set");
            activation.Lifecycle.Should().NotBeEmpty(
                "midpoint activation should have lifecycle events");
        }
    }

    #endregion

    #region Techniques (branch Modes, 1.4.0 item 2)

    /// <summary>A transit input for March 2026 with the three techniques as given.</summary>
    /// <param name="transits">Whether to run the transit scan.</param>
    /// <param name="solarArcs">Whether to include the solar arcs.</param>
    /// <param name="progressions">Whether to include the secondary progressions.</param>
    /// <returns>The input.</returns>
    private TransitInput MarchInput(bool transits, bool solarArcs, bool progressions) => new()
    {
        NatalChart = _natalChart,
        ReportStartDate = new DateOnly(2026, 3, 1),
        ReportEndDate = new DateOnly(2026, 3, 31),
        IncludeTransits = transits,
        IncludeSolarArcs = solarArcs,
        IncludeSecondaryProgressions = progressions,
    };

    /// <summary>With both techniques on, both snapshots and both hit lists are populated.</summary>
    [Fact]
    public void Calculate_WithBothTechniques_PopulatesSnapshotsAndHits()
    {
        TransitResult result = _transitService.Calculate(MarchInput(true, true, true));

        result.IncludesTransits.Should().BeTrue();
        result.Progressions.Should().NotBeNull();
        result.SolarArcs.Should().NotBeNull();
        result.Progressions.Positions.Should().HaveCount(7);
        result.SolarArcs.Positions.Should().HaveCount(14);
        result.SolarArcHits.Should().NotBeEmpty("Tier B lists SA Uranus square Pluto on 2026-03-02 and SA Venus conjunct Mars/MC on 2026-03-03");
        result.ProgressedHits.Should().BeEmpty("Tier B lists no progressed hit in March 2026");
    }

    /// <summary>Solar arcs off: no solar arc snapshot and no solar arc hits; the progressions stay.</summary>
    [Fact]
    public void Calculate_SolarArcsOff_LeavesSolarArcsNull()
    {
        TransitResult result = _transitService.Calculate(MarchInput(true, false, true));

        result.SolarArcs.Should().BeNull();
        result.SolarArcHits.Should().BeEmpty();
        result.Progressions.Should().NotBeNull();
    }

    /// <summary>Progressions off: no progression snapshot and no progressed hits; the solar arcs stay.</summary>
    [Fact]
    public void Calculate_ProgressionsOff_LeavesProgressionsNull()
    {
        TransitResult result = _transitService.Calculate(MarchInput(true, true, false));

        result.Progressions.Should().BeNull();
        result.ProgressedHits.Should().BeEmpty();
        result.SolarArcs.Should().NotBeNull();
        result.SolarArcHits.Should().NotBeEmpty();
    }

    /// <summary>No technique at all is refused, the exception naming the four flags (the fourth since branch PrimaryExport).</summary>
    [Fact]
    public void Calculate_NoTechnique_Throws()
    {
        Action act = () => _transitService.Calculate(MarchInput(false, false, false));

        act.Should().Throw<ArgumentException>()
            .WithMessage("*IncludeTransits*IncludeSolarArcs*IncludeSecondaryProgressions*IncludePrimaryDirections*");
    }

    /// <summary>
    /// With the primary directions on and the other techniques off (branch PrimaryExport, 1.5.0), the result
    /// carries the whole-life calculation and the period's hits, which are exactly the engine's
    /// <c>FindHits</c> for the same window: J2000 NYC over 2026 has four.
    /// </summary>
    [Fact]
    public void Calculate_PrimaryDirectionsOn_PopulatesTheResultAndThePeriodsHits()
    {
        PrimaryDirectionSettings settings = new() { BirthTimeUncertaintyMinutes = 4 };
        TransitInput input = new()
        {
            NatalChart = _natalChart,
            ReportStartDate = new DateOnly(2026, 1, 1),
            ReportEndDate = new DateOnly(2026, 12, 31),
            IncludeTransits = false,
            IncludeSolarArcs = false,
            IncludeSecondaryProgressions = false,
            IncludePrimaryDirections = true,
            PrimaryDirectionSettings = settings,
        };

        TransitResult result = _transitService.Calculate(input);

        result.PrimaryDirections.Should().NotBeNull();
        result.PrimaryDirections?.Settings.Should().BeSameAs(settings);
        result.PrimaryDirections?.Hits.Count.Should().Be(572);
        IReadOnlyList<PrimaryDirectionHit> expected = new PrimaryDirectionService(_ephemeris)
            .FindHits(_natalChart, settings, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        result.PrimaryDirectionHits.Should().HaveCount(4);
        result.PrimaryDirectionHits.Select(h => (h.Motion, h.Significator, h.Aspect, h.AspectAngle, h.Promissor, h.JulianDayUt))
            .Should().Equal(expected.Select(h => (h.Motion, h.Significator, h.Aspect, h.AspectAngle, h.Promissor, h.JulianDayUt)));
    }

    /// <summary>With the primary directions off, the result holds none and no hits.</summary>
    [Fact]
    public void Calculate_PrimaryDirectionsOff_LeavesThemNull()
    {
        TransitResult result = _transitService.Calculate(MarchInput(false, true, false));

        result.PrimaryDirections.Should().BeNull();
        result.PrimaryDirectionHits.Should().BeEmpty();
    }

    /// <summary>
    /// Solar arcs alone skip the transit scan: no transiting position, station, ingress, transit, overlay,
    /// activation, transit-to-transit or declination row, and the result says so; the solar arc snapshot
    /// and hits are there and the progressions are not.
    /// </summary>
    [Fact]
    public void Calculate_SolarArcOnly_SkipsTheTransitScan()
    {
        TransitResult result = _transitService.Calculate(MarchInput(false, true, false));

        result.IncludesTransits.Should().BeFalse();
        result.TransitPositions.Should().BeEmpty();
        result.Stations.Should().BeEmpty();
        result.Ingresses.Should().BeEmpty();
        result.ActiveTransits.Should().BeEmpty();
        result.HouseOverlays.Should().BeEmpty();
        result.MidpointActivations.Should().BeEmpty();
        result.TransitToTransitAspects.Should().BeEmpty();
        result.TransitDeclinationAspects.Should().BeEmpty();
        result.MarsIncluded.Should().BeFalse();
        result.SolarArcs.Should().NotBeNull();
        result.SolarArcHits.Should().NotBeEmpty();
        result.Progressions.Should().BeNull();
        result.ProgressedHits.Should().BeEmpty();
    }

    /// <summary>Progressions alone: the progression snapshot, no solar arcs, no transit scan.</summary>
    [Fact]
    public void Calculate_ProgressionsOnly_HasNoSolarArcs()
    {
        TransitResult result = _transitService.Calculate(MarchInput(false, false, true));

        result.IncludesTransits.Should().BeFalse();
        result.ActiveTransits.Should().BeEmpty();
        result.Progressions.Should().NotBeNull();
        result.SolarArcs.Should().BeNull();
        result.SolarArcHits.Should().BeEmpty();
    }

    /// <summary>
    /// The snapshots are taken at the report's start, not its midpoint (scoping C2): the progressed
    /// Julian Day is the one <see cref="ProgressedDateCalculator"/> gives for the start day, and the
    /// solar arc is that progression's.
    /// </summary>
    [Fact]
    public void Calculate_Snapshots_AreAtTheStart()
    {
        TransitResult result = _transitService.Calculate(MarchInput(true, true, true));

        (DateTime instant, double progressedJd, _) = ProgressedDateCalculator.Calculate(_natalChart.JulianDay, new DateOnly(2026, 3, 1));
        (_, double midpointJd, _) = ProgressedDateCalculator.Calculate(_natalChart.JulianDay, result.MidpointDate);

        result.Progressions.Should().NotBeNull();
        result.SolarArcs.Should().NotBeNull();
        result.Progressions.ProgressedJulianDay.Should().Be(progressedJd);
        result.Progressions.ProgressedJulianDay.Should().NotBe(midpointJd, "the midpoint is no longer the reference day");
        result.Progressions.ProgressedDate.Should().Be(instant);
        result.SolarArcs.ArcValue.Should().Be(result.Progressions.SolarArcValue);
        result.MidpointDate.Should().Be(new DateOnly(2026, 3, 16), "the house overlays keep the midpoint");
    }

    #endregion

    #region Transit-to-Natal Declination Aspects Tests

    [Fact]
    public void Calculate_TransitDeclinationAspects_Should_BePresent()
    {
        TransitInput input = CreateMarchInput();

        TransitResult result = _transitService.Calculate(input);

        result.TransitDeclinationAspects.Should().NotBeNull(
            "transit declination aspects list should be present even if empty");
    }

    [Fact]
    public void Calculate_TransitDeclinationAspects_Should_HaveValidProperties()
    {
        TransitInput input = CreateMarchInput();

        TransitResult result = _transitService.Calculate(input);

        foreach (TransitDeclinationAspect tda in result.TransitDeclinationAspects)
        {
            tda.TransitPlanetName.Should().NotBeNullOrEmpty(
                "transit planet name should be set");
            tda.NatalPlanetName.Should().NotBeNullOrEmpty(
                "natal planet name should be set");
            tda.Orb.Should().BeLessThanOrEqualTo(1.0,
                "orb should be within the ±1° threshold");
            tda.Orb.Should().BeGreaterThanOrEqualTo(0.0,
                "orb should be non-negative");
            tda.Abbreviation.Should().BeOneOf("Par", "CPar",
                "abbreviation should be Par or CPar");
            tda.Date.Should().BeOnOrAfter(result.ReportStartDate.AddYears(-2),
                "exact pass date should be within a reasonable range of the report period");
        }
    }

    [Fact]
    public void Calculate_TransitDeclinationAspects_Should_BeSortedChronologically()
    {
        TransitInput input = CreateMarchInput();

        TransitResult result = _transitService.Calculate(input);

        if (result.TransitDeclinationAspects.Count < 2)
            return; // Not enough data to test ordering

        for (int i = 0; i < result.TransitDeclinationAspects.Count - 1; i++)
        {
            DateOnly current = result.TransitDeclinationAspects[i].Date;
            DateOnly next = result.TransitDeclinationAspects[i + 1].Date;

            current.Should().BeOnOrBefore(next,
                "transit declination aspects should be sorted chronologically");
        }
    }

    [Fact]
    public void Calculate_TransitDeclinationAspects_OnlyIncludesActiveTransitBodies()
    {
        TransitInput input = CreateMarchInput();

        TransitResult result = _transitService.Calculate(input);

        // Collect all transit body names that have active transits
        HashSet<string> activeTransitBodies = result.ActiveTransits
            .Select(at => at.TransitPlanet)
            .Distinct()
            .Select(FormatTransitBodyNameForTest)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (TransitDeclinationAspect tda in result.TransitDeclinationAspects)
        {
            activeTransitBodies.Should().Contain(tda.TransitPlanetName,
                $"transit declination aspect for {tda.TransitPlanetName} should correspond to an active transit");
        }
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Formats a <see cref="TransitBody"/> to display name for test assertions.
    /// Mirrors <c>TransitService.FormatTransitBodyName</c>.
    /// </summary>
    private static string FormatTransitBodyNameForTest(TransitBody body) => body switch
    {
        TransitBody.Jupiter => "Jupiter",
        TransitBody.Saturn => "Saturn",
        TransitBody.Uranus => "Uranus",
        TransitBody.Neptune => "Neptune",
        TransitBody.Pluto => "Pluto",
        TransitBody.Chiron => "Chiron",
        TransitBody.NorthNode => "North Node",
        TransitBody.Mars => "Mars",
        _ => body.ToString(),
    };

    /// <summary>
    /// Mirrors the sort order from TransitService for test verification.
    /// </summary>
    private static int TransitBodySortOrderForTest(TransitBody body) => body switch
    {
        TransitBody.Pluto => 0,
        TransitBody.Neptune => 1,
        TransitBody.Uranus => 2,
        TransitBody.Saturn => 3,
        TransitBody.Jupiter => 4,
        TransitBody.Chiron => 5,
        TransitBody.NorthNode => 6,
        TransitBody.Mars => 7,
        _ => 99,
    };

    #endregion
    #region Branch D — Mars, the node's stations and declination rows (T5–T8)

    /// <summary>Tolerance on every Tier B instant.</summary>
    private static readonly TimeSpan FiveSeconds = TimeSpan.FromSeconds(5);

    /// <summary>A transit input over the given dates: major aspects, no progressions.</summary>
    /// <param name="start">The report's first date.</param>
    /// <param name="end">The report's last date.</param>
    /// <returns>The input.</returns>
    private TransitInput Input(DateOnly start, DateOnly end) => new()
    {
        NatalChart = _natalChart,
        ReportStartDate = start,
        ReportEndDate = end,
        IncludeMinorAspects = false,
        IncludeSolarArcs = false,
        IncludeSecondaryProgressions = false,
    };

    /// <summary>The report dates from the lifecycle's entry to its exit, clipped to the report.</summary>
    /// <param name="lifecycle">A transit's or activation's lifecycle.</param>
    /// <param name="start">The report's first date.</param>
    /// <param name="end">The report's last date.</param>
    /// <returns>The dates, in order.</returns>
    private static IEnumerable<DateOnly> ReportDatesBetweenEntryAndExit(
        IReadOnlyList<TransitLifecycleEvent> lifecycle, DateOnly start, DateOnly end)
    {
        DateOnly entry = lifecycle.Single(e => e.EventType == TransitLifecycleEventType.Entry).DatePart;
        DateOnly exit = lifecycle.Single(e => e.EventType == TransitLifecycleEventType.Exit).DatePart;
        DateOnly from = entry > start ? entry : start;
        DateOnly to = exit < end ? exit : end;
        for (DateOnly date = from; date <= to; date = date.AddDays(1))
            yield return date;
    }

    /// <summary>
    /// Whether on at least one report date between the transit's entry and exit Mars is in orb of
    /// the transit's aspect at 12:00 UT and moving below
    /// <see cref="TransitOrbs.MarsSlowMotionThreshold"/> (rule R5), read from the ephemeris.
    /// </summary>
    /// <param name="transit">A Mars transit.</param>
    /// <param name="start">The report's first date.</param>
    /// <param name="end">The report's last date.</param>
    /// <returns>Whether such a date exists.</returns>
    private bool MarsIsSlowInOrbOnAReportDay(ActiveTransit transit, DateOnly start, DateOnly end)
    {
        TransitAspectFinder.TryGetAspect(transit.AspectName, out double angle, out bool isMinor)
            .Should().BeTrue($"{transit.AspectName} is one of the finder's aspects");
        double orb = TransitOrbs.GetOrb(TransitBody.Mars, angle, isMinor);

        return ReportDatesBetweenEntryAndExit(transit.Lifecycle, start, end).Any(date =>
        {
            double jd = _helper.DateToJulianDay(date);
            return TransitAspectFinder.IsWithinOrb(
                       _helper.GetLongitude(TransitBody.Mars, jd), transit.NatalLongitude, angle, orb)
                   && Math.Abs(_helper.GetDailyMotion(TransitBody.Mars, jd)) < TransitOrbs.MarsSlowMotionThreshold;
        });
    }

    /// <summary>
    /// Whether on at least one report date between the activation's entry and exit Mars is within
    /// <see cref="TransitOrbs.MidpointOrb"/> of the midpoint on the 45° dial at 12:00 UT and moving
    /// below <see cref="TransitOrbs.MarsSlowMotionThreshold"/> (rule R5), read from the ephemeris.
    /// </summary>
    /// <param name="activation">A Mars midpoint activation.</param>
    /// <param name="start">The report's first date.</param>
    /// <param name="end">The report's last date.</param>
    /// <returns>Whether such a date exists.</returns>
    private bool MarsIsSlowInOrbOfMidpointOnAReportDay(TransitMidpointActivation activation, DateOnly start, DateOnly end)
    {
        return ReportDatesBetweenEntryAndExit(activation.Lifecycle, start, end).Any(date =>
        {
            double jd = _helper.DateToJulianDay(date);
            double dial = TransitMidpointScanner.ReduceToDial45(_helper.GetLongitude(TransitBody.Mars, jd));
            return TransitMidpointScanner.Dial45Separation(dial, activation.NatalMidpointDial45Position) <= TransitOrbs.MidpointOrb
                   && Math.Abs(_helper.GetDailyMotion(TransitBody.Mars, jd)) < TransitOrbs.MarsSlowMotionThreshold;
        });
    }

    /// <summary>
    /// T5 (R5). Over 2026-01-01 to 2027-12-31 Mars is in the report (it stations retrograde on
    /// 2027-01-10 and direct on 2027-04-01), but a Mars transit or midpoint activation is listed only
    /// if on a report date between its entry and exit Mars is in orb at 12:00 UT and moving below
    /// 0.3°/day. The sextile to natal Sun, which Mars crosses at 0.6°/day and more, is not listed.
    /// Until branch D every Mars transit of the two years was.
    /// </summary>
    [Fact]
    public void Calculate_TwoYears_ListsAMarsTransitOnlyWhereMarsIsSlowInOrbOnAReportDay()
    {
        DateOnly start = new(2026, 1, 1);
        DateOnly end = new(2027, 12, 31);

        TransitResult result = _transitService.Calculate(Input(start, end));

        result.MarsIncluded.Should().BeTrue("Mars stations twice inside the report");
        result.Stations.Where(s => s.Planet == TransitBody.Mars).Select(s => s.DateTimeUtc).Should().HaveCount(2)
            .And.SatisfyRespectively(
                retrograde => retrograde.Should().NotBeNull().And.BeCloseTo(new DateTime(2027, 1, 10, 12, 59, 17, DateTimeKind.Utc), TimeSpan.FromMinutes(1)),
                direct => direct.Should().NotBeNull().And.BeCloseTo(new DateTime(2027, 4, 1, 14, 8, 15, DateTimeKind.Utc), TimeSpan.FromMinutes(1)));

        List<ActiveTransit> marsTransits = result.ActiveTransits.Where(t => t.TransitPlanet == TransitBody.Mars).ToList();
        marsTransits.Should().NotContain(t => t.NatalTarget == "Sun" && t.AspectName == "Sextile",
            "Mars crosses both sextile points (March 2026, September 2027) at 0.6°/day and more");
        marsTransits.Should().NotBeEmpty("the trines to natal Sun and Saturn hold Mars's January 2027 station");
        marsTransits.Should().OnlyContain(t => MarsIsSlowInOrbOnAReportDay(t, start, end),
            "a Mars transit is listed only where Mars is slow and in orb at a report noon between its entry and exit");

        List<TransitMidpointActivation> marsActivations = result.MidpointActivations.Where(a => a.TransitPlanet == TransitBody.Mars).ToList();
        marsActivations.Should().OnlyContain(a => MarsIsSlowInOrbOfMidpointOnAReportDay(a, start, end),
            "a Mars midpoint activation is listed only where Mars is slow and in orb at a report noon between its entry and exit");
    }

    /// <summary>
    /// T6 (R3, R5; a guard). Over 2026-12-27 to 2027-01-31 Mars stations retrograde on 2027-01-10 at
    /// 12:59:17 UT at 10°25′44″ Virgo, 0.06° from its trine to natal Sun and 0.03° from its trine to
    /// natal Saturn: both trines are listed, each with that station in its lifecycle.
    /// </summary>
    [Fact]
    public void Calculate_DecemberToJanuary_ListsMarsTrinesToNatalSunAndSaturnWithTheRetrogradeStation()
    {
        TransitResult result = _transitService.Calculate(Input(new(2026, 12, 27), new(2027, 1, 31)));

        foreach (string target in new[] { "Sun", "Saturn" })
        {
            ActiveTransit trine = result.ActiveTransits.Should().ContainSingle(
                t => t.TransitPlanet == TransitBody.Mars && t.NatalTarget == target && t.AspectName == "Trine",
                $"Mars stations within 0.1° of its trine to natal {target}").Subject;

            TransitLifecycleEvent station = trine.Lifecycle.Should().ContainSingle(
                e => e.EventType == TransitLifecycleEventType.Station,
                $"the retrograde station is in orb of the trine to natal {target}; the direct station of 2027-04-01 at 20°55′ Leo is not").Subject;

            station.StationType.Should().Be("Retrograde");
            station.Date.Should().BeCloseTo(new DateTime(2027, 1, 10, 12, 59, 17, DateTimeKind.Utc), FiveSeconds);
        }
    }

    /// <summary>
    /// T7 (R4). Over 2026-04-12 to 18 the Stations table holds no row for the North Node and no node
    /// transit's lifecycle holds a station: the true node's reversals are oscillation, not stations
    /// (T-D4). Until branch D one row was listed, 2026-04-13.
    /// </summary>
    [Fact]
    public void Calculate_April2026_ListsNoStationOfTheNorthNode()
    {
        TransitResult result = _transitService.Calculate(Input(new(2026, 4, 12), new(2026, 4, 18)));

        result.Stations.Should().NotContain(s => s.Planet == TransitBody.NorthNode,
            "the true node's reversals are not stations (T-D4)");

        result.ActiveTransits
            .Where(t => t.TransitPlanet == TransitBody.NorthNode)
            .SelectMany(t => t.Lifecycle)
            .Should().NotContain(e => e.EventType == TransitLifecycleEventType.Station,
                "a node transit's lifecycle lists no station (T-D4)");
    }

    /// <summary>
    /// T8 (R6; an invariant). Over 2026-04-12 to 18 no transit, natal body and date is listed as
    /// both a parallel and a contraparallel, and each row's kind agrees with the sides of the
    /// equator its two declinations lie on, 0° counting as north (D13). No fixture pair lies within
    /// 0.5° of the equator, so the rule is exercised in DeclinationAspectServiceTests; this holds it
    /// at the transit rows.
    /// </summary>
    [Fact]
    public void Calculate_April2026_ListsOneDeclinationAspectPerPairAndDateBySideOfTheEquator()
    {
        TransitResult result = _transitService.Calculate(Input(new(2026, 4, 12), new(2026, 4, 18)));

        result.TransitDeclinationAspects.Should().NotBeEmpty("Pluto and Chiron pass natal points in the window");

        result.TransitDeclinationAspects
            .GroupBy(a => (a.TransitPlanetName, a.NatalPlanetName, a.Date))
            .Should().OnlyContain(g => g.Select(a => a.IsParallel).Distinct().Count() == 1,
                "a pair forms one declination aspect on a date, never both (D13)");

        result.TransitDeclinationAspects.Should().OnlyContain(
            a => a.IsParallel == ((a.TransitDeclination >= 0.0) == (a.NatalDeclination >= 0.0)),
            "a parallel needs both declinations on the same side of the equator, a contraparallel opposite sides (D13)");
    }

    /// <summary>
    /// T11 (R8). Over 2026-04-12 to 18 Neptune activates the natal Venus/True Node midpoint: one
    /// passage over the dial longitude 2°45′ Aries, entering the 10′ orb on 2026-04-11 at 12:30:00 UT
    /// (before the report), exact on 2026-04-16 at 04:04:43, and, the passage holding Neptune's
    /// retrograde return over the same longitude (in orb again 2026-09-28 to 10-10 and 2027-02-10 to
    /// 21, gaps of 161 and 123 days), exact again on 2026-10-04 at 17:15:30 and 2027-02-16 at
    /// 03:31:28 and leaving on 2027-02-21 at 05:07:07 (beyond it), Tier B to the second. The branch
    /// task gave the first in-orb period's end, 2026-04-20 22:36:04, as the exit; under R8's gap rule,
    /// R1's, the passage is the three periods. Until branch D the entry and exit were the noons of
    /// the first and last in-orb report days, 2026-04-12 and 2026-04-20.
    /// </summary>
    [Fact]
    public void Calculate_April2026_NeptuneOverVenusTrueNodeMidpoint_IsOnePassageWithRefinedInstants()
    {
        TransitResult result = _transitService.Calculate(Input(new(2026, 4, 12), new(2026, 4, 18)));

        TransitMidpointActivation activation = result.MidpointActivations.Should().ContainSingle(
            a => a.TransitPlanet == TransitBody.Neptune && a.MidpointBody1 == "Venus" && a.MidpointBody2 == "True Node",
            "Neptune is within 10′ of the Venus/True Node midpoint on the dial during the report").Subject;

        activation.Lifecycle.Should().HaveCount(5);

        TransitLifecycleEvent entry = activation.Lifecycle[0];
        entry.EventType.Should().Be(TransitLifecycleEventType.Entry);
        entry.Date.Should().BeCloseTo(new DateTime(2026, 4, 11, 12, 30, 0, DateTimeKind.Utc), FiveSeconds);
        entry.BeforeReportPeriod.Should().BeTrue();

        (DateTime Utc, bool Beyond)[] passes =
        [
            (new DateTime(2026, 4, 16, 4, 4, 43, DateTimeKind.Utc), false),
            (new DateTime(2026, 10, 4, 17, 15, 30, DateTimeKind.Utc), true),
            (new DateTime(2027, 2, 16, 3, 31, 28, DateTimeKind.Utc), true),
        ];
        for (int i = 0; i < passes.Length; i++)
        {
            TransitLifecycleEvent exact = activation.Lifecycle[i + 1];
            exact.EventType.Should().Be(TransitLifecycleEventType.Exact);
            exact.Date.Should().BeCloseTo(passes[i].Utc, FiveSeconds, $"pass {i + 1} is at {passes[i].Utc:yyyy-MM-dd HH:mm:ss} UT");
            exact.PassNumber.Should().Be(i + 1);
            exact.BeforeReportPeriod.Should().BeFalse();
            exact.BeyondReportPeriod.Should().Be(passes[i].Beyond);
        }

        TransitLifecycleEvent exit = activation.Lifecycle[4];
        exit.EventType.Should().Be(TransitLifecycleEventType.Exit);
        exit.Date.Should().BeCloseTo(new DateTime(2027, 2, 21, 5, 7, 7, DateTimeKind.Utc), FiveSeconds);
        exit.BeyondReportPeriod.Should().BeTrue();
    }

    #endregion

    #region Branch AsteroidsTiming — the natal five as transit targets (R7)

    /// <summary>The transit golden's period, 2026-04-12 to 2026-04-18, and the timeline golden's, 2026-04-01 to 2026-04-30.</summary>
    public static TheoryData<string, string, string> TimingScans => new()
    {
        { "transit_20260412_18", "2026-04-12", "2026-04-18" },
        { "timeline_202604", "2026-04-01", "2026-04-30" },
    };

    /// <summary>A transit input over the given dates for the natal carrying the five: the transits alone.</summary>
    /// <param name="start">The report's first date.</param>
    /// <param name="end">The report's last date.</param>
    /// <param name="includeMinorAspects">Whether the chart's own transits take the minor aspects too.</param>
    /// <returns>The input.</returns>
    private TransitInput InputWithFive(DateOnly start, DateOnly end, bool includeMinorAspects = false) => new()
    {
        NatalChart = _natalWithFive,
        ReportStartDate = start,
        ReportEndDate = end,
        IncludeMinorAspects = includeMinorAspects,
        IncludeSolarArcs = false,
        IncludeSecondaryProgressions = false,
    };

    /// <summary>A typed date of the timing lists.</summary>
    /// <param name="date">The date, yyyy-MM-dd.</param>
    /// <returns>The date.</returns>
    private static DateOnly Date(string date) =>
        DateOnly.ParseExact(date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The symbol the timing list gives a transiting body.</summary>
    /// <param name="body">The transit body.</param>
    /// <returns>JU, SA, UR, NE, PL, CH, NN or MA.</returns>
    private static string SymbolForTest(TransitBody body) => body switch
    {
        TransitBody.Jupiter => "JU",
        TransitBody.Saturn => "SA",
        TransitBody.Uranus => "UR",
        TransitBody.Neptune => "NE",
        TransitBody.Pluto => "PL",
        TransitBody.Chiron => "CH",
        TransitBody.NorthNode => "NN",
        TransitBody.Mars => "MA",
        _ => body.ToString(),
    };

    /// <summary>The symbols the five's targets carry (scoping decision 7), by name.</summary>
    private static readonly Dictionary<string, string> MinorPointSymbols = new(StringComparer.Ordinal)
    {
        ["Ceres"] = "CER",
        ["Pallas"] = "PAL",
        ["Juno"] = "JUN",
        ["Vesta"] = "VES",
        ["Lilith"] = "LIL",
    };

    /// <summary>
    /// Tier B (ast_truth_timing.py): with the natal carrying the five, the transits to them over the transit golden's
    /// period and the timeline's are the list's rows for the bodies the gates keep, every row but Mars's (Mars
    /// crosses the five at speed, so the gates drop its rows): body, aspect and target equal, entry and exit within
    /// 60 s, every exact pass within 2 s, and, for the bodies the report prints stations for (not the node, whose
    /// reversals are oscillation, T-D4), every station's kind and minute.
    /// </summary>
    /// <param name="scan">The list's scan.</param>
    /// <param name="start">The report's first date.</param>
    /// <param name="end">The report's last date.</param>
    [Theory]
    [MemberData(nameof(TimingScans))]
    public void Calculate_SwitchOn_MinorPointTransits_MatchTierB(string scan, string start, string end)
    {
        List<MinorPointTierB.TimingTransitRow> expected = MinorPointTierB.LoadTimingTransits(scan)
            .Where(r => r.Body != "MA")
            .ToList();
        expected.Should().NotBeEmpty();

        TransitResult result = _transitService.Calculate(InputWithFive(Date(start), Date(end)));

        result.MinorPointTransits.Select(t => (SymbolForTest(t.TransitPlanet), t.AspectAbbreviation, t.NatalTarget))
            .Should().BeEquivalentTo(expected.Select(r => (r.Body, r.Aspect, r.Target)), "the five's transits are the list's rows, Mars's apart");

        foreach (MinorPointTierB.TimingTransitRow row in expected)
        {
            string because = $"of {row.Body} {row.Aspect} {row.Target} in {scan}";
            ActiveTransit transit = result.MinorPointTransits.Single(t =>
                SymbolForTest(t.TransitPlanet) == row.Body && t.AspectAbbreviation == row.Aspect && t.NatalTarget == row.Target);

            transit.Lifecycle.Single(e => e.EventType == TransitLifecycleEventType.Entry).Date
                .Should().BeCloseTo(row.EntryUtc, TimeSpan.FromSeconds(60), because);
            transit.Lifecycle.Single(e => e.EventType == TransitLifecycleEventType.Exit).Date
                .Should().BeCloseTo(row.ExitUtc, TimeSpan.FromSeconds(60), because);

            List<DateTime> passes = transit.Lifecycle.Where(e => e.EventType == TransitLifecycleEventType.Exact).Select(e => e.Date).ToList();
            passes.Should().HaveCount(row.Passes.Count, because);
            for (int i = 0; i < passes.Count; i++)
                passes[i].Should().BeCloseTo(row.Passes[i], TimeSpan.FromSeconds(2), $"{because}, pass {i + 1}");

            List<TransitLifecycleEvent> stations = transit.Lifecycle.Where(e => e.EventType == TransitLifecycleEventType.Station).ToList();
            if (transit.TransitPlanet == TransitBody.NorthNode)
            {
                stations.Should().BeEmpty("the node has no station rows (T-D4)");
                continue;
            }

            stations.Should().HaveCount(row.Stations.Count, because);
            for (int i = 0; i < stations.Count; i++)
            {
                stations[i].StationType.Should().Be(row.Stations[i].Kind, $"{because}, station {i + 1}");
                stations[i].Date.Should().BeCloseTo(row.Stations[i].InstantUtc, TimeSpan.FromSeconds(60), $"{because}, station {i + 1}");
            }
        }
    }

    /// <summary>
    /// The list holds Mars rows for both scans (Mars opposite Ceres, square Juno, trine Vesta in April 2026, each
    /// over in two or three days at about 0.7° a day), and the report lists none of them: Mars's presence is decided
    /// from the main natal targets (<c>TransitMarsEvaluator.ShouldIncludeMars</c>), and a Mars transit it admits is
    /// kept only where Mars is slow in orb on a report day (<c>KeepMarsTransitsWhereSlowInOrb</c>, T-D5). The output
    /// names the gate that acted in each period.
    /// </summary>
    [Fact]
    public void Calculate_SwitchOn_ListsNoMarsTransitToTheFive()
    {
        foreach ((string scan, string start, string end) in new[] { ("transit_20260412_18", "2026-04-12", "2026-04-18"), ("timeline_202604", "2026-04-01", "2026-04-30") })
        {
            MinorPointTierB.LoadTimingTransits(scan).Should().Contain(r => r.Body == "MA", "the list holds the Mars rows the gates drop");

            TransitResult result = _transitService.Calculate(InputWithFive(Date(start), Date(end)));

            result.MinorPointTransits.Should().NotBeEmpty(scan);
            result.MinorPointTransits.Should().NotContain(t => t.TransitPlanet == TransitBody.Mars, "Mars crosses the five at speed in {0}", scan);
            _output.WriteLine(result.MarsIncluded
                ? $"{scan}: Mars is in the report (MarsIncluded); KeepMarsTransitsWhereSlowInOrb dropped its rows to the five"
                : $"{scan}: Mars is not in the report (TransitMarsEvaluator.ShouldIncludeMars is false), so it has no rows to the five");
        }
    }

    /// <summary>The five's targets carry the symbols CER, PAL, JUN, VES and LIL (scoping decision 7), never the planets' map's two-letter fallback.</summary>
    [Fact]
    public void Calculate_SwitchOn_TargetsCarryTheFivesSymbols()
    {
        TransitResult result = _transitService.Calculate(InputWithFive(new(2026, 4, 1), new(2026, 4, 30)));

        result.MinorPointTransits.Should().NotBeEmpty();
        result.MinorPointTransits.Should().OnlyContain(t => MinorPointSymbols.ContainsKey(t.NatalTarget) && t.NatalTargetSymbol == MinorPointSymbols[t.NatalTarget]);
        result.MinorPointTransits.Select(t => t.NatalTargetSymbol).Distinct().Should().BeEquivalentTo(["CER", "JUN", "VES"],
            "April 2026 holds transits to Ceres, Juno and Vesta");
        result.MinorPointTransits.Should().NotContain(t => t.NatalTargetSymbol == "JU" || t.NatalTargetSymbol == "VE",
            "the two-letter fallback would give Juno and Vesta Jupiter's and Venus's symbols");
    }

    /// <summary>
    /// The five are transit targets only (R8): no chart transit, house overlay, midpoint activation, declination
    /// row or transit-to-transit row names one of them, and the chart's own transits are those of the natal without
    /// the five.
    /// </summary>
    [Fact]
    public void Calculate_SwitchOn_NoOtherRowNamesTheFive()
    {
        DateOnly start = new(2026, 4, 1);
        DateOnly end = new(2026, 4, 30);

        TransitResult result = _transitService.Calculate(InputWithFive(start, end));
        TransitResult plain = _transitService.Calculate(Input(start, end));

        result.MinorPointTransits.Should().NotBeEmpty();
        result.ActiveTransits.Should().NotContain(t => MinorPointTierB.Names.Contains(t.NatalTarget));
        result.MidpointActivations.Should().NotContain(a => MinorPointTierB.Names.Contains(a.MidpointBody1) || MinorPointTierB.Names.Contains(a.MidpointBody2));
        result.TransitDeclinationAspects.Should().NotContain(d => MinorPointTierB.Names.Contains(d.NatalPlanetName));
        result.TransitToTransitAspects.Should().NotContain(a => MinorPointTierB.Names.Contains(a.Planet1Name) || MinorPointTierB.Names.Contains(a.Planet2Name));
        result.HouseOverlays.Select(o => o.Planet).Should().BeEquivalentTo(plain.HouseOverlays.Select(o => o.Planet));
        result.ActiveTransits.Select(t => (t.TransitPlanet, t.AspectName, t.NatalTarget))
            .Should().Equal(plain.ActiveTransits.Select(t => (t.TransitPlanet, t.AspectName, t.NatalTarget)), "the switch changes nothing for the chart's own transits (R9)");
    }

    /// <summary>
    /// The five take the five Ptolemaic aspects at the bodies' major orbs even when the input asks for the minor
    /// aspects: the chart's own transits gain minor-aspect rows, the five's rows are the same as without them.
    /// </summary>
    [Fact]
    public void Calculate_SwitchOn_MinorAspectsRequested_TheFiveTakeThePtolemaicAspectsOnly()
    {
        DateOnly start = new(2026, 4, 1);
        DateOnly end = new(2026, 4, 30);

        TransitResult withMinor = _transitService.Calculate(InputWithFive(start, end, includeMinorAspects: true));
        TransitResult majorsOnly = _transitService.Calculate(InputWithFive(start, end));

        withMinor.IncludeMinorAspects.Should().BeTrue();
        withMinor.ActiveTransits.Should().Contain(t => IsMinorAspect(t.AspectName), "the chart's own transits take the minor aspects asked for");
        withMinor.MinorPointTransits.Should().NotBeEmpty();
        withMinor.MinorPointTransits.Select(t => t.AspectAbbreviation).Distinct().Should().BeSubsetOf(["cnj", "sxt", "sqr", "tri", "opp"]);
        withMinor.MinorPointTransits.Select(t => (t.TransitPlanet, t.AspectName, t.NatalTarget))
            .Should().Equal(majorsOnly.MinorPointTransits.Select(t => (t.TransitPlanet, t.AspectName, t.NatalTarget)));
    }

    /// <summary>Whether a transit aspect name is one of the finder's minor aspects.</summary>
    /// <param name="aspectName">The aspect's name as the finder writes it.</param>
    /// <returns>Whether it is minor.</returns>
    private static bool IsMinorAspect(string aspectName) =>
        TransitAspectFinder.TryGetAspect(aspectName, out _, out bool isMinor) && isMinor;

    /// <summary>
    /// With the switch off every list of the five is empty (R9), the techniques' included; with it on, over the
    /// same period, they are not.
    /// </summary>
    [Fact]
    public void Calculate_SwitchOff_MinorPointListsAreEmpty()
    {
        TransitInput Full(ChartResult natal) => new()
        {
            NatalChart = natal,
            ReportStartDate = new DateOnly(2026, 1, 1),
            ReportEndDate = new DateOnly(2026, 12, 31),
            IncludeMinorAspects = false,
            IncludeSolarArcs = true,
            IncludeSecondaryProgressions = true,
        };

        TransitResult off = _transitService.Calculate(Full(_natalChart));
        TransitResult on = _transitService.Calculate(Full(_natalWithFive));

        off.NatalChart.MinorPoints.Should().BeEmpty();
        off.MinorPointTransits.Should().BeEmpty();
        off.MinorPointProgressedHits.Should().BeEmpty();
        off.MinorPointSolarArcHits.Should().BeEmpty();
        off.Progressions.Should().NotBeNull();
        off.SolarArcs.Should().NotBeNull();
        if (off.Progressions is ProgressionResult progressions)
            progressions.MinorPointAspects.Should().BeEmpty();
        if (off.SolarArcs is SolarArcResult solarArcs)
            solarArcs.MinorPointAspects.Should().BeEmpty();

        on.MinorPointTransits.Should().NotBeEmpty();
        on.MinorPointProgressedHits.Should().NotBeEmpty("2026 holds P Moon sextile Juno and P Venus square Ceres");
        on.MinorPointSolarArcHits.Should().NotBeEmpty("2026 holds SA Pluto conjunct Juno and SA Venus sesquiquadrate Pallas");
        on.MinorPointSolarArcHits.Should().NotContain(h => h.IsMidpoint);
    }

    #endregion
    #region Branch InputChecks — a one-day report (N6)

    /// <summary>
    /// A report of one day, its start and end the same date, is a report (branch InputChecks, 1.7.2, finding N6):
    /// every transit and midpoint activation April 2026's report lists with an exact pass on a day of the month is
    /// listed by that day's own report, with the same pass, inside the period. Until 1.7.2 the aspect finder
    /// refused a range whose start was not strictly before its end, though every scan takes the end date as a
    /// whole day.
    /// </summary>
    [Fact]
    public void Calculate_OneDay_ListsTheTransitsAndActivationsExactThatDay()
    {
        TransitResult month = _transitService.Calculate(Input(new DateOnly(2026, 4, 1), new DateOnly(2026, 4, 30)));
        List<(string Row, DateTime Exact)> exactInTheMonth = [.. ExactPassesInThePeriod(month)];
        exactInTheMonth.Should().NotBeEmpty("April 2026 holds exact passes for J2000 NYC");
        exactInTheMonth.Select(pass => pass.Row).Should().Contain(row => row.Contains(" = ", StringComparison.Ordinal))
            .And.Contain(row => !row.Contains(" = ", StringComparison.Ordinal), "the month holds both kinds of row");

        Dictionary<DateOnly, List<(string Row, DateTime Exact)>> oneDayReports = [];
        foreach ((string row, DateTime exact) in exactInTheMonth)
        {
            DateOnly day = DateOnly.FromDateTime(exact);
            if (!oneDayReports.TryGetValue(day, out List<(string Row, DateTime Exact)>? listed))
            {
                TransitResult oneDay = _transitService.Calculate(Input(day, day));
                oneDay.ReportStartDate.Should().Be(day);
                oneDay.ReportEndDate.Should().Be(day);
                oneDay.MidpointDate.Should().Be(day);
                listed = [.. ExactPassesInThePeriod(oneDay)];
                oneDayReports[day] = listed;
            }

            listed.Should().Contain(
                pass => pass.Row == row && (pass.Exact - exact).Duration() <= FiveSeconds,
                $"{row} is exact on {exact:yyyy-MM-dd HH:mm:ss} UT, inside the one-day report of that date");
        }
    }

    /// <summary>
    /// The one-day report's Markdown names the day as its whole period, and the timeline and the daily horoscope
    /// made of the same result are written too, the daily horoscope with its one day.
    /// </summary>
    [Fact]
    public void Calculate_OneDay_TheReportsHeaderHasThatDay()
    {
        DateOnly day = new(2026, 4, 15);

        TransitResult oneDay = _transitService.Calculate(Input(day, day));
        string report = AstroCast.Core.Formatting.TransitMarkdownFormatter.ToMarkdown(oneDay);
        string timeline = AstroCast.Core.Formatting.TransitTimelineMarkdownFormatter.ToMarkdown(oneDay);
        IReadOnlyList<DailyHoroscopeDay> days =
            new DailyHoroscopeService(_ephemeris, new VoidOfCourseScanner(_ephemeris)).BuildDays(oneDay, "America/New_York");

        report.Should().Contain("**Report Period:** 2026-04-15 to 2026-04-15");
        report.Should().Contain("### At Report Start (2026-04-15)");
        oneDay.ActiveTransits.Should().NotBeEmpty("the slow planets are in orb of natal points at noon that day");
        timeline.Should().Contain("2026-04-15");
        days.Should().ContainSingle().Which.Date.Should().Be(day);
    }

    /// <summary>A period whose end is before its start is still refused by the scan.</summary>
    [Fact]
    public void Calculate_EndBeforeStart_IsRefused()
    {
        Action act = () => _transitService.Calculate(Input(new DateOnly(2026, 4, 15), new DateOnly(2026, 4, 14)));

        act.Should().Throw<ArgumentException>().WithMessage("*must not be after*");
    }

    /// <summary>
    /// The exact passes a result lists inside its own period: a transit's as "Saturn Square Sun", a midpoint
    /// activation's as "Saturn = Sun/ASC".
    /// </summary>
    /// <param name="result">The transit result.</param>
    /// <returns>Each row's name with the instant of one exact pass.</returns>
    private static IEnumerable<(string Row, DateTime Exact)> ExactPassesInThePeriod(TransitResult result)
    {
        static bool InPeriod(TransitLifecycleEvent e) =>
            e.EventType == TransitLifecycleEventType.Exact && !e.BeforeReportPeriod && !e.BeyondReportPeriod;

        foreach (ActiveTransit transit in result.ActiveTransits)
        {
            foreach (TransitLifecycleEvent e in transit.Lifecycle.Where(InPeriod))
                yield return ($"{transit.TransitPlanet} {transit.AspectName} {transit.NatalTarget}", e.Date);
        }

        foreach (TransitMidpointActivation activation in result.MidpointActivations)
        {
            foreach (TransitLifecycleEvent e in activation.Lifecycle.Where(InPeriod))
                yield return ($"{activation.TransitPlanet} = {activation.MidpointBody1}/{activation.MidpointBody2}", e.Date);
        }
    }

    #endregion
}
