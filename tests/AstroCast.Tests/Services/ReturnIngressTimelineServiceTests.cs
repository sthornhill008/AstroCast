using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="ReturnIngressTimelineService"/> verifying return detection,
/// ingress detection, combined timeline ordering, precision, return number estimation,
/// and guard clauses. Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ReturnIngressTimelineServiceTests
{
    private readonly ChartEngine _engine;
    private readonly ReturnIngressTimelineService _service;
    private readonly EphemerisFixture _fixture;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds all required services.
    /// </summary>
    public ReturnIngressTimelineServiceTests(EphemerisFixture fixture)
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

        _service = new ReturnIngressTimelineService(
            fixture.EphemerisService, logger);
        _fixture = fixture;
    }

    /// <summary>
    /// Standard test natal chart: J2000.0 epoch, New York City.
    /// Born 2000-01-01 07:00 EST (UTC-5).
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

    /// <summary>
    /// Natal chart for Donald Trump: 1946-06-14 10:54 EDT, Jamaica, NY.
    /// Used for realistic return number estimation tests.
    /// </summary>
    private ChartResult CalculateTrumpNatalChart() => _engine.Calculate(new ChartInput
    {
        Name = "D. Trump",
        LocalDateTime = new DateTime(1946, 6, 14, 10, 54, 0),
        TimeZone = "America/New_York",
        Latitude = 40.6898,
        Longitude = -73.8055,
        LocationName = "Jamaica, NY",
        EventType = "Birth",
    });

    #region Returns Tests

    /// <summary>
    /// Mercury returns to its natal degree about once a year as seen from the Earth, so a calendar
    /// year holds one or two (branch F, T2-2); 2026 holds #26 (3 January) and #27 (26 December).
    /// Until branch F the test expected two to five, the crossings of the window.
    /// </summary>
    [Fact]
    public void Calculate_MercuryReturns1Year_FindsOneOrTwoReturns()
    {
        ChartResult natal = CalculateNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2026, 2026,
            includeReturns: true, includeIngresses: false,
            returnPlanets: [CelestialBodyId.Mercury]);

        timeline.Returns.Count.Should().BeInRange(1, 2,
            "Mercury returns to its natal degree about once a year as seen from the Earth");
        timeline.Ingresses.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_MarsReturns5Years_Finds2To4Returns()
    {
        ChartResult natal = CalculateNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2025, 2029,
            includeReturns: true, includeIngresses: false,
            returnPlanets: [CelestialBodyId.Mars]);

        timeline.Returns.Count.Should().BeInRange(2, 4,
            "Mars completes ~1 return every 2 years");
    }

    [Fact]
    public void Calculate_JupiterReturns5Years_Finds0Or1Returns()
    {
        ChartResult natal = CalculateNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2025, 2029,
            includeReturns: true, includeIngresses: false,
            returnPlanets: [CelestialBodyId.Jupiter]);

        timeline.Returns.Count.Should().BeInRange(0, 1,
            "Jupiter completes ~1 return every 12 years");
    }

    [Fact]
    public void Calculate_ReturnEntries_HaveCorrectEventType()
    {
        ChartResult natal = CalculateNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2026, 2026,
            includeReturns: true, includeIngresses: false,
            returnPlanets: [CelestialBodyId.Mercury]);

        timeline.Returns.Should().AllSatisfy(
            entry => entry.EventType.Should().Be("Return"));
    }

    [Fact]
    public void Calculate_ReturnEntries_HaveValidLabels()
    {
        ChartResult natal = CalculateNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2026, 2026,
            includeReturns: true, includeIngresses: false,
            returnPlanets: [CelestialBodyId.Mercury]);

        timeline.Returns.Should().AllSatisfy(
            entry => entry.Label.Should().StartWith("Return #"));
    }

    [Fact]
    public void Calculate_ReturnPositions_MatchNatalLongitude()
    {
        ChartResult natal = CalculateNatalChart();
        PlanetPosition? mercuryNatal = natal.Positions
            .FirstOrDefault(p => p.Body == CelestialBodyId.Mercury);
        mercuryNatal.Should().NotBeNull();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2026, 2026,
            includeReturns: true, includeIngresses: false,
            returnPlanets: [CelestialBodyId.Mercury]);

        timeline.Returns.Should().AllSatisfy(entry =>
        {
            double diff = Math.Abs(entry.Longitude - mercuryNatal!.Longitude);
            diff.Should().BeLessThan(0.01,
                "return longitude should match natal longitude");
        });
    }

    #endregion

    #region Ingress Tests

    [Fact]
    public void Calculate_JupiterIngresses3Years_Finds2To4Ingresses()
    {
        ReturnIngressTimeline timeline = _service.Calculate(
            null, 2025, 2027,
            includeReturns: false, includeIngresses: true,
            ingressPlanets: [CelestialBodyId.Jupiter]);

        timeline.Ingresses.Count.Should().BeInRange(2, 4,
            "Jupiter changes sign ~every 12 months");
        timeline.Returns.Should().BeEmpty();
    }

    [Fact]
    public void Calculate_SaturnIngresses5Years_FindsSeveralIngresses()
    {
        ReturnIngressTimeline timeline = _service.Calculate(
            null, 2025, 2029,
            includeReturns: false, includeIngresses: true,
            ingressPlanets: [CelestialBodyId.Saturn]);

        timeline.Ingresses.Count.Should().BeInRange(1, 6,
            "Saturn changes sign ~every 2.5 years (retrogrades produce multiple crossings)");
    }

    [Fact]
    public void Calculate_UranusIngresses3Years_Finds0To3Ingresses()
    {
        ReturnIngressTimeline timeline = _service.Calculate(
            null, 2025, 2027,
            includeReturns: false, includeIngresses: true,
            ingressPlanets: [CelestialBodyId.Uranus]);

        timeline.Ingresses.Count.Should().BeInRange(0, 3,
            "Uranus may cross a sign boundary with retrogrades producing multiples");
    }

    [Fact]
    public void Calculate_IngressEntries_HaveCorrectEventType()
    {
        ReturnIngressTimeline timeline = _service.Calculate(
            null, 2026, 2026,
            includeReturns: false, includeIngresses: true,
            ingressPlanets: [CelestialBodyId.Jupiter]);

        timeline.Ingresses.Should().AllSatisfy(
            entry => entry.EventType.Should().Be("Ingress"));
    }

    [Fact]
    public void Calculate_IngressEntries_HaveLongitudeAt0OfSign()
    {
        ReturnIngressTimeline timeline = _service.Calculate(
            null, 2025, 2027,
            includeReturns: false, includeIngresses: true,
            ingressPlanets: [CelestialBodyId.Jupiter]);

        timeline.Ingresses.Should().AllSatisfy(entry =>
        {
            double remainder = entry.Longitude % 30.0;
            remainder.Should().BeLessThan(0.001,
                "ingress longitude should be at 0° of a sign");
        });
    }

    #endregion

    #region Combined Timeline Tests

    [Fact]
    public void Calculate_BothReturnsAndIngresses_ContainsBothTypes()
    {
        ChartResult natal = CalculateNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2025, 2027,
            includeReturns: true, includeIngresses: true,
            returnPlanets: [CelestialBodyId.Mercury],
            ingressPlanets: [CelestialBodyId.Jupiter]);

        timeline.Returns.Should().NotBeEmpty("Mercury returns should exist");
        timeline.Ingresses.Should().NotBeEmpty("Jupiter ingresses should exist");
        timeline.Count.Should().Be(timeline.Returns.Count + timeline.Ingresses.Count);
    }

    [Fact]
    public void Calculate_CombinedTimeline_IsSortedChronologically()
    {
        ChartResult natal = CalculateNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2025, 2027,
            includeReturns: true, includeIngresses: true,
            returnPlanets: [CelestialBodyId.Mercury],
            ingressPlanets: [CelestialBodyId.Jupiter]);

        IReadOnlyList<ReturnIngressTimelineEntry> entries = timeline.Entries;

        for (int i = 1; i < entries.Count; i++)
        {
            entries[i].ExactUtc.Should().BeOnOrAfter(entries[i - 1].ExactUtc,
                "timeline entries must be sorted chronologically");
        }
    }

    [Fact]
    public void Calculate_ReturnsOnlyMode_HasNoIngresses()
    {
        ChartResult natal = CalculateNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2026, 2026,
            includeReturns: true, includeIngresses: false);

        timeline.Ingresses.Should().BeEmpty(
            "ingresses should not appear when includeIngresses is false");
    }

    [Fact]
    public void Calculate_IngressOnlyMode_HasNoReturns()
    {
        ReturnIngressTimeline timeline = _service.Calculate(
            null, 2026, 2026,
            includeReturns: false, includeIngresses: true);

        timeline.Returns.Should().BeEmpty(
            "returns should not appear when includeReturns is false");
    }

    #endregion

    #region Precision Tests

    [Fact]
    public void Calculate_AllEntries_HavePrecisionUnder1ArcSecond()
    {
        ChartResult natal = CalculateNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2026, 2026,
            includeReturns: true, includeIngresses: true,
            returnPlanets: [CelestialBodyId.Mercury, CelestialBodyId.Venus],
            ingressPlanets: [CelestialBodyId.Jupiter]);

        timeline.Entries.Should().AllSatisfy(entry =>
        {
            entry.PrecisionArcSeconds.Should().BeLessThan(1.0,
                $"{entry.PlanetName} {entry.EventType} at {entry.ExactUtc} should have sub-arc-second precision");
        });
    }

    #endregion

    #region Guard Clause Tests

    [Fact]
    public void Calculate_ReturnsWithNullNatalChart_ThrowsArgumentNullException()
    {
        Action act = () => _service.Calculate(
            null, 2026, 2026,
            includeReturns: true, includeIngresses: false);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("natalChart");
    }

    [Fact]
    public void Calculate_StartGreaterThanEnd_ThrowsArgumentException()
    {
        ChartResult natal = CalculateNatalChart();

        Action act = () => _service.Calculate(
            natal, 2028, 2025);

        act.Should().Throw<ArgumentException>()
            .WithParameterName("startYear");
    }

    [Fact]
    public void Calculate_NullNatalChartIngressOnly_WorksFine()
    {
        ReturnIngressTimeline timeline = _service.Calculate(
            null, 2026, 2026,
            includeReturns: false, includeIngresses: true);

        timeline.Should().NotBeNull();
        timeline.NativeName.Should().Be("Unknown");
    }

    #endregion

    #region Branch F — passages at their first pass (F4)

    /// <summary>
    /// F4 (R6). Over 2026 to 2027 the J2000 NYC fixture's five return planets make six returns,
    /// one entry per passage at its first pass, each labelled with the passage's number from birth
    /// (Tier B): Mercury #26, Mars #14, Mercury #27, Venus #27, Venus #28 and Mercury #28, none
    /// with a retrograde loop over the degree. Until branch F the labels read "Return ~#108" and
    /// the like, elapsed time over the sidereal period.
    /// </summary>
    [Fact]
    public void Calculate_J2000Returns2026To2027_AreSixPassagesNumberedFromBirth()
    {
        ReturnIngressTimeline timeline = _service.Calculate(
            GoldenFixtures.J2000Nyc(_fixture), 2026, 2027,
            includeReturns: true, includeIngresses: false);

        (string Planet, string Label, DateTime FirstPass)[] expected =
        [
            ("Mercury", "Return #26", new DateTime(2026, 1, 3, 2, 43, 54, DateTimeKind.Utc)),
            ("Mars", "Return #14", new DateTime(2026, 2, 28, 0, 13, 43, DateTimeKind.Utc)),
            ("Mercury", "Return #27", new DateTime(2026, 12, 26, 23, 7, 41, DateTimeKind.Utc)),
            ("Venus", "Return #27", new DateTime(2027, 1, 8, 20, 48, 21, DateTimeKind.Utc)),
            ("Venus", "Return #28", new DateTime(2027, 11, 2, 13, 50, 3, DateTimeKind.Utc)),
            ("Mercury", "Return #28", new DateTime(2027, 12, 19, 16, 34, 28, DateTimeKind.Utc)),
        ];

        timeline.Returns.Select(e => (e.PlanetName, e.Label)).Should().Equal(expected.Select(e => (e.Planet, e.Label)));
        for (int i = 0; i < expected.Length; i++)
        {
            timeline.Returns[i].ExactUtc.Should().BeCloseTo(expected[i].FirstPass, TimeSpan.FromSeconds(5),
                $"{expected[i].Planet} {expected[i].Label}");
        }
    }

    #endregion

    #region Return Number Estimation Tests

    [Fact]
    public void Calculate_TrumpMercuryReturns_HasReasonableReturnNumbers()
    {
        ChartResult natal = CalculateTrumpNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2026, 2026,
            includeReturns: true, includeIngresses: false,
            returnPlanets: [CelestialBodyId.Mercury]);

        // Born 1946, so in 2026 about 80 years old; Mercury returns about once a year (T2-2),
        // so the number counts passages from birth and stays close to the age. Until branch F the
        // label read "Return ~#" with elapsed time over 88 days, about 330.
        timeline.Returns.Should().AllSatisfy(entry =>
        {
            string numberPart = entry.Label.Replace("Return #", "").Split(' ')[0];
            int returnNumber = int.Parse(numberPart, System.Globalization.CultureInfo.InvariantCulture);
            returnNumber.Should().BeInRange(76, 84,
                "Mercury return numbers for an ~80-year-old count about one passage a year");
        });
    }

    [Fact]
    public void Calculate_TrumpMarsReturns_HasReasonableReturnNumbers()
    {
        ChartResult natal = CalculateTrumpNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2025, 2029,
            includeReturns: true, includeIngresses: false,
            returnPlanets: [CelestialBodyId.Mars]);

        // Mars returns about every 1.9 years, so an ~80-year-old is near #42. Until branch F the
        // label read "Return ~#" with elapsed time over the sidereal period.
        timeline.Returns.Should().AllSatisfy(entry =>
        {
            string numberPart = entry.Label.Replace("Return #", "").Split(' ')[0];
            int returnNumber = int.Parse(numberPart, System.Globalization.CultureInfo.InvariantCulture);
            returnNumber.Should().BeInRange(38, 48,
                "Mars return numbers for an ~80-year-old count about one passage every two years");
        });
    }

    #endregion

    #region Metadata Tests

    [Fact]
    public void Calculate_Timeline_HasCorrectMetadata()
    {
        ChartResult natal = CalculateNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2025, 2027);

        timeline.NativeName.Should().Be("Test Person");
        timeline.ScanStart.Year.Should().Be(2025);
        timeline.ScanEnd.Year.Should().Be(2027);
        timeline.EngineVersion.Should().Contain("AstroCast");
        timeline.CalculatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        timeline.ReturnPlanets.Should().NotBeEmpty();
        timeline.IngressPlanets.Should().NotBeEmpty();
    }

    [Fact]
    public void Calculate_DefaultPlanets_Uses5ReturnAnd6IngressPlanets()
    {
        ChartResult natal = CalculateNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2026, 2026);

        timeline.ReturnPlanets.Should().HaveCount(5,
            "default return planets are Mercury, Venus, Mars, Jupiter, Saturn");
        timeline.IngressPlanets.Should().HaveCount(6,
            "default ingress planets are Jupiter, Saturn, Uranus, Neptune, Pluto, Chiron");
    }

    /// <summary>
    /// A search that did not run leaves its planet list empty, and the export says it was not searched, rather than
    /// naming planets that were never looked at (E2-3).
    /// </summary>
    /// <param name="includeReturns">Whether returns are searched.</param>
    /// <param name="includeIngresses">Whether ingresses are searched.</param>
    /// <param name="line">The expected header line.</param>
    [Theory]
    [InlineData(false, true, "**Return Planets:** not searched  ")]
    [InlineData(true, false, "**Ingress Planets:** not searched  ")]
    public void ReturnIngressTimeline_SearchOff_SaysNotSearched(bool includeReturns, bool includeIngresses, string line)
    {
        ChartResult natal = CalculateNatalChart();

        ReturnIngressTimeline timeline = _service.Calculate(
            natal, 2026, 2026, includeReturns: includeReturns, includeIngresses: includeIngresses);
        List<string> lines = [.. ReturnIngressTimelineFormatter.ToMarkdown(timeline).Split('\n').Select(l => l.TrimEnd('\r'))];

        (includeReturns ? timeline.IngressPlanets : timeline.ReturnPlanets).Should().BeEmpty();
        lines.Should().Contain(line);
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Verifies that <see cref="ReturnIngressTimelineService.Calculate"/> honours
    /// the supplied <see cref="CancellationToken"/> by throwing
    /// <see cref="OperationCanceledException"/> when the token is already cancelled
    /// before any planet-sweep iteration runs.
    /// </summary>
    [Fact]
    public void Calculate_PreCancelledToken_Should_ThrowOperationCanceledException()
    {
        ChartResult natal = CalculateNatalChart();

        using CancellationTokenSource cts = new();
        cts.Cancel();

        Action act = () => _service.Calculate(
            natal, 2020, 2030,
            includeReturns: true, includeIngresses: true,
            returnPlanets: null, ingressPlanets: null,
            cancellationToken: cts.Token);

        act.Should().Throw<OperationCanceledException>(
            "a pre-cancelled token must abort the combined-timeline sweep immediately");
    }

    #endregion
}
