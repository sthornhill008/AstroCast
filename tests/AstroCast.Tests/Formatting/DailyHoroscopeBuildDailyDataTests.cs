using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Integration tests for <see cref="DailyHoroscopeService.BuildDays"/>
/// verifying per-day data construction with real ephemeris calculations.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class DailyHoroscopeBuildDailyDataTests
{
    private readonly EphemerisService _ephemeris;
    private readonly TransitService _transitService;
    private readonly ChartResult _natalChart;

    /// <summary>
    /// Receives the shared ephemeris fixture, builds a natal chart, and constructs
    /// the TransitService for integration testing.
    /// </summary>
    public DailyHoroscopeBuildDailyDataTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        ILogger logger = new LoggerConfiguration().CreateLogger();
        AspectService aspects = new(_ephemeris, logger);
        MidpointService midpoints = new(logger);
        ChartEngine engine = new(
            _ephemeris, aspects, midpoints,
            new DignityService(), new MoonConditionService(),
            new ChartContextService(new DignityService(), new SectService()),
            new FixedStarService(_ephemeris, logger),
            new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(_ephemeris), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);

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
        _transitService = new TransitService(_ephemeris, logger);
    }

    /// <summary>Creates a one-week transit result for testing.</summary>
    private TransitResult CalculateOneWeekTransits()
    {
        TransitInput input = new()
        {
            NatalChart = _natalChart,
            ReportStartDate = new DateOnly(2026, 3, 13),
            ReportEndDate = new DateOnly(2026, 3, 19),
            IncludeMinorAspects = false,
        };

        return _transitService.Calculate(input);
    }

    [Fact]
    public void BuildDailyData_OneWeekRange_ProducesSevenDays()
    {
        TransitResult result = CalculateOneWeekTransits();
        VoidOfCourseScanner vocScanner = new(_ephemeris);

        IReadOnlyList<DailyHoroscopeDay> days =
            new DailyHoroscopeService(_ephemeris, vocScanner).BuildDays(result, "America/New_York");

        days.Should().HaveCount(7,
            "a 7-day report range (March 13–19) should produce exactly 7 days");
    }

    [Fact]
    public void BuildDailyData_MoonSignPopulated()
    {
        TransitResult result = CalculateOneWeekTransits();
        VoidOfCourseScanner vocScanner = new(_ephemeris);

        IReadOnlyList<DailyHoroscopeDay> days =
            new DailyHoroscopeService(_ephemeris, vocScanner).BuildDays(result, "America/New_York");

        foreach (DailyHoroscopeDay day in days)
        {
            day.MoonSign.Should().NotBe(default(ZodiacSign),
                $"Moon sign on {day.Date} should be populated");
        }
    }

    [Fact]
    public void BuildDailyData_ActiveTransitsFiltered()
    {
        TransitResult result = CalculateOneWeekTransits();
        VoidOfCourseScanner vocScanner = new(_ephemeris);

        IReadOnlyList<DailyHoroscopeDay> days =
            new DailyHoroscopeService(_ephemeris, vocScanner).BuildDays(result, "America/New_York");

        // At least one day should have active transits (outer planets
        // should have multi-week orbits that cover a 7-day window)
        days.Should().Contain(d => d.ActiveTransits.Count > 0,
            "at least some days should have active transits");
    }

    [Fact]
    public void BuildDailyData_ExactPassDetected()
    {
        TransitResult result = CalculateOneWeekTransits();
        VoidOfCourseScanner vocScanner = new(_ephemeris);

        IReadOnlyList<DailyHoroscopeDay> days =
            new DailyHoroscopeService(_ephemeris, vocScanner).BuildDays(result, "America/New_York");

        // Check if any exact passes exist in the transit result
        bool anyExactInResult = result.ActiveTransits
            .SelectMany(t => t.Lifecycle)
            .Any(e => e.EventType == TransitLifecycleEventType.Exact
                && e.DatePart >= result.ReportStartDate
                && e.DatePart <= result.ReportEndDate);

        if (anyExactInResult)
        {
            days.Should().Contain(d => d.ExactToday.Count > 0,
                "when exact passes exist in the transit result, at least one day should detect them");
        }
    }

    [Fact]
    public void BuildDailyData_VocPeriodsPresent()
    {
        TransitResult result = CalculateOneWeekTransits();
        VoidOfCourseScanner vocScanner = new(_ephemeris);

        IReadOnlyList<DailyHoroscopeDay> days =
            new DailyHoroscopeService(_ephemeris, vocScanner).BuildDays(result, "America/New_York");

        // Over a 7-day period the Moon passes through ~2-3 signs,
        // so there should be at least one VoC period
        days.Should().Contain(d => d.VoidOfCoursePeriods.Count > 0,
            "at least some days should have VoC periods during a 7-day window");
    }
}
