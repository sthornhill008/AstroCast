using AstroCast.Core.Enums;
using AstroCast.Core.Exceptions;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests verifying that <see cref="ChartEngine"/> correctly
/// computes the planetary hour and horary radicality for real chart data.
/// Uses the shared ephemeris fixture for actual planetary calculations.
/// </summary>
[Collection("Ephemeris")]
public sealed class PlanetaryHourIntegrationTests
{
    private readonly ChartEngine _engine;
    private readonly EphemerisService _ephemeris;

    /// <summary>
    /// Initialises the test class with a ChartEngine wired to the shared ephemeris.
    /// </summary>
    public PlanetaryHourIntegrationTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        AspectService aspects = new(fixture.EphemerisService, logger);
        MidpointService midpoints = new(logger);

        _engine = new ChartEngine(
            fixture.EphemerisService, aspects, midpoints,
            new DignityService(), new MoonConditionService(),
            new ChartContextService(new DignityService(), new SectService()),
            new FixedStarService(fixture.EphemerisService, logger),
            new DeclinationAspectService(), new PlanetConditionService(),
            new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
    }

    [Fact]
    public void Calculate_BirthChart_HasPlanetaryHourWithNoRadicality()
    {
        ChartInput input = new()
        {
            Name = "Birth Chart Hour Test",
            LocalDateTime = new DateTime(2000, 3, 21, 12, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 37.4138,
            Longitude = -79.1422,
            LocationName = "Lynchburg, VA",
            EventType = "Birth",
        };

        ChartResult result = _engine.Calculate(input);

        result.PlanetaryHour.Should().NotBeNull("planetary hour should be computed for birth charts");
        result.PlanetaryHour!.RulingPlanet.Should().NotBeNullOrWhiteSpace();
        result.PlanetaryHour.Hour.HourNumber.Should().BeGreaterThan(0);
        result.PlanetaryHour.IsRadical.Should().BeNull(
            "radicality is only computed for Horary charts");
        result.PlanetaryHour.RadicalityLevel.Should().BeNull();
        result.PlanetaryHour.RadicalityNote.Should().BeNull();
    }

    [Fact]
    public void Calculate_HoraryChart_HasPlanetaryHourWithRadicality()
    {
        ChartInput input = new()
        {
            Name = "Horary Hour Test",
            LocalDateTime = new DateTime(2025, 6, 15, 14, 30, 0),
            TimeZone = "America/New_York",
            Latitude = 37.4138,
            Longitude = -79.1422,
            LocationName = "Lynchburg, VA",
            EventType = "Horary",
            HoraryQuestion = "Will the project succeed?",
        };

        ChartResult result = _engine.Calculate(input);

        result.PlanetaryHour.Should().NotBeNull("planetary hour should be computed for horary charts");
        result.PlanetaryHour!.RulingPlanet.Should().NotBeNullOrWhiteSpace();
        result.PlanetaryHour.IsRadical.Should().NotBeNull(
            "radicality should be computed for Horary charts");
        result.PlanetaryHour.RadicalityLevel.Should().BeOneOf(
            "Radical", "Supportive", "Weak", "Not Radical");
        result.PlanetaryHour.RadicalityNote.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Calculate_AnyChart_HasMoonMansion()
    {
        ChartInput input = new()
        {
            Name = "Moon Mansion Test",
            LocalDateTime = new DateTime(2025, 1, 15, 10, 0, 0),
            TimeZone = "Europe/London",
            Latitude = 51.5074,
            Longitude = -0.1278,
            LocationName = "London, UK",
            EventType = "Event",
        };

        ChartResult result = _engine.Calculate(input);

        result.MoonMansion.Should().NotBeNull("Moon mansion should always be computed when Moon is present");
        result.MoonMansion!.Number.Should().BeInRange(1, 28);
        result.MoonMansion.Name.Should().NotBeNullOrWhiteSpace();
        result.MoonMansion.Nature.Should().BeOneOf("Benefic", "Malefic", "Neutral");
    }

    [Fact]
    public void Calculate_PlanetaryHour_HasValidTimeRange()
    {
        ChartInput input = new()
        {
            Name = "Hour Range Test",
            LocalDateTime = new DateTime(2025, 6, 21, 15, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 37.4138,
            Longitude = -79.1422,
            LocationName = "Lynchburg, VA",
            EventType = "Birth",
        };

        ChartResult result = _engine.Calculate(input);

        result.PlanetaryHour.Should().NotBeNull();
        result.PlanetaryHour!.Hour.HourStart.Should().BeBefore(result.PlanetaryHour.Hour.HourEnd);
        result.PlanetaryHour.Hour.HourNumber.Should().BeInRange(1, 24);
        result.PlanetaryHour.Hour.RulingPlanet.Should().BeOneOf(
            "Sun", "Moon", "Mars", "Mercury", "Jupiter", "Venus", "Saturn");
    }

    /// <summary>
    /// Chart 5 (Bill Clinton) exactly as saved: 08:51 on 1946-08-19 at an explicit UTC−6,
    /// with America/Chicago as its zone. That zone's clock was on daylight time (UTC−5) that
    /// day, an hour ahead of the chart's own.
    /// </summary>
    /// <returns>The saved chart's input.</returns>
    private static ChartInput SavedChart5() => new()
    {
        Name = "Bill Clinton",
        LocalDateTime = new DateTime(1946, 8, 19, 8, 51, 0),
        TimeZone = "America/Chicago",
        UseExplicitOffset = true,
        UtcOffsetHours = -6.0,
        Latitude = 33.6670616,
        Longitude = -93.5915665,
        LocationName = "Hope, Hempstead County, Arkansas, 71801, United States",
        EventType = "Birth",
    };

    /// <summary>
    /// An explicit-offset chart's hour is on its own clock: chart 5's period is 07:52:39 to
    /// 08:58:54 at UTC−6 (Tier B: day hour 3 from 13:52:39 to 14:58:54 UT, Jupiter), which
    /// contains its 08:51. On the zone's daylight clock it read 08:52:39 to 09:58:54, an hour
    /// after the chart.
    /// </summary>
    [Fact]
    public void Calculate_ExplicitOffsetChart_Should_GiveTheHourPeriodOnItsOwnClock()
    {
        ChartInput input = SavedChart5();

        ChartResult result = _engine.Calculate(input);

        PlanetaryHourInfo hour = result.PlanetaryHour.Should().BeOfType<PlanetaryHourInfo>().Subject;
        hour.RulingPlanet.Should().Be("Jupiter");
        hour.Hour.HourNumber.Should().Be(3);
        hour.Hour.IsDaytime.Should().BeTrue();
        hour.Hour.HourStart.Should().Be(new DateTime(1946, 8, 19, 7, 52, 39));
        hour.Hour.HourEnd.Should().Be(new DateTime(1946, 8, 19, 8, 58, 54));
        input.LocalDateTime.Should().BeOnOrAfter(hour.Hour.HourStart).And.BeBefore(hour.Hour.HourEnd,
            "the hour the chart falls in contains the chart's own clock time");
    }

    /// <summary>
    /// The export labels chart 5's Hour Period exactly as its Date/Time line labels the
    /// chart's clock: "UTC−6 (explicit)".
    /// </summary>
    [Fact]
    public void ToMarkdown_ExplicitOffsetChart_Should_LabelTheHourPeriodAsTheDateTimeLine()
    {
        string markdown = ChartMarkdownFormatter.ToMarkdown(_engine.Calculate(SavedChart5()));

        markdown.Should().Contain("**Date/Time:** 1946-08-19 08:51:00 UTC\u22126 (explicit)  ");
        markdown.Should().Contain("**Hour Period:** 07:52:39 \u2013 08:58:54 UTC\u22126 (explicit)");
    }

    /// <summary>
    /// A chart cast on its zone keeps the zone's clock and label. The J2000 New York chart
    /// (the chart golden's input) is in night hour 12, from 11:07:57 to 12:21:44 UT
    /// (Tier B), 06:07:57 to 07:21:44 EST.
    /// </summary>
    [Fact]
    public void ToMarkdown_ZoneChart_Should_KeepTheHourPeriodOnTheZonesClock()
    {
        ChartInput input = new()
        {
            Name = "Golden Test Chart",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        };

        string markdown = ChartMarkdownFormatter.ToMarkdown(_engine.Calculate(input));

        markdown.Should().Contain("**Hour Period:** 06:07:57 \u2013 07:21:44 (America/New_York)");
    }

    /// <summary>
    /// A zone chart from before the zone's first transition reads its hour on IANA's local
    /// mean time, to the second. New York 1880-06-15 12:00 is 16:56:02 UT on LMT \u22124:56:02
    /// (IANA 2026e). Its Chaldean day is a Tuesday, sunrise 04:29:36 and sunset 19:31:10
    /// LMT (Tier B, direct swe_rise_trans calls), so the instant falls in day hour 6, ruled
    /// by Saturn, from 10:45:15 to 12:00:23 LMT. On this Mac's own data the clock was a
    /// whole minute off, so the chart was cast 58 seconds late and its period printed on
    /// the wrong offset.
    /// </summary>
    [Fact]
    public void ToMarkdown_ZoneChartInLocalMeanTime_Should_ReadTheHourPeriodOnLocalMeanTime()
    {
        ChartInput input = new()
        {
            Name = "New York 1880",
            LocalDateTime = new DateTime(1880, 6, 15, 12, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
        };

        ChartResult result = _engine.Calculate(input);
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);

        PlanetaryHourInfo info = result.PlanetaryHour.Should().BeOfType<PlanetaryHourInfo>().Which;
        info.Hour.RulingPlanet.Should().Be("Saturn");
        info.Hour.HourNumber.Should().Be(6);
        info.Hour.IsDaytime.Should().BeTrue();
        info.Hour.HourStart.Should().Be(new DateTime(1880, 6, 15, 10, 45, 15));
        info.Hour.HourEnd.Should().Be(new DateTime(1880, 6, 15, 12, 0, 23));
        markdown.Should().Contain("**Hour Period:** 10:45:15 \u2013 12:00:23 (America/New_York)");
    }

    /// <summary>
    /// Tromsø at the June solstice with Equal houses: the Sun does not set (Tier B:
    /// swe_rise_trans returns −2), so the instant has no planetary hour. The chart returns
    /// without one and its export has no Planetary Hour section. Before, the whole chart
    /// failed with "Sunrise calculation failed: rise or set not found for planet 0".
    /// </summary>
    [Fact]
    public void Calculate_WhereTheSunDoesNotSet_Should_ReturnTheChartWithoutAPlanetaryHour()
    {
        ChartInput input = new()
        {
            Name = "Tromsø midsummer",
            LocalDateTime = new DateTime(2026, 6, 21, 12, 0, 0),
            TimeZone = "Europe/Oslo",
            Latitude = 69.6492,
            Longitude = 18.9553,
            LocationName = "Tromsø, Norway",
            EventType = "Event",
            HouseSystem = HouseSystem.Equal,
        };

        ChartResult result = _engine.Calculate(input);

        result.PlanetaryHour.Should().BeNull();
        result.Positions.Should().HaveCount(12);
        string markdown = ChartMarkdownFormatter.ToMarkdown(result);
        markdown.Should().NotContain("## Planetary Hour").And.NotContain("**Hour Ruler:**");
    }

    /// <summary>
    /// Chart 17 as saved (Equal houses) relocated to Longyearbyen, 78.2232° N: at its instant,
    /// 1980-11-16 18:00 UT, the Sun does not rise there (Tier B: −2), so no hour could be computed
    /// at the place. The relocation returns, and the relocated chart's planetary hour is the natal
    /// chart's own object: the birth's hour goes with the chart, none is computed at the place
    /// (branch K2, 1.3.4; ruling S-D6). Until 1.3.4 the relocated chart was cast as a whole chart at
    /// the place and returned without a planetary hour there.
    /// </summary>
    [Fact]
    public void Relocate_ToWhereTheSunDoesNotRise_Should_KeepTheBirthsPlanetaryHour()
    {
        ChartInput chart17 = new()
        {
            Name = "EWric Swalwell",
            LocalDateTime = new DateTime(1980, 11, 16, 12, 0, 0),
            TimeZone = "America/Chicago",
            Latitude = 42.4221155,
            Longitude = -94.9885843,
            LocationName = "Sac City, Sac County, Iowa, 50583, United States",
            EventType = "Birth",
            HouseSystem = HouseSystem.Equal,
        };
        RelocationService relocation = new(_engine, new Serilog.LoggerConfiguration().CreateLogger());

        RelocationResult result = relocation.Calculate(
            _engine.Calculate(chart17),
            new RelocationInput { Latitude = 78.2232, Longitude = 15.6267, LocationName = "Longyearbyen" });

        result.NatalChart.PlanetaryHour.Should().NotBeNull();
        result.RelocatedChart.PlanetaryHour.Should().NotBeNull()
            .And.BeSameAs(result.NatalChart.PlanetaryHour, "the birth's hour goes with the chart; none is computed where the Sun does not rise");
    }

    /// <summary>
    /// The year 1000, outside the ephemeris files (1200 to 2399), still throws, but is now
    /// refused before the ephemeris is consulted (branch F5, rule 4), where Chiron failed with
    /// a file message (Tier B: swe_calc_ut returns −1, "SwissEph file 'seas_06.se1' not
    /// found"). A real ephemeris failure propagating from the planetary hour is still
    /// covered, by <see cref="Calculate_WhenTheSunriseSearchFailsAnyOtherWay_Should_StillThrow"/>.
    /// </summary>
    [Fact]
    public void Calculate_OutsideTheEphemerisFiles_Should_StillThrow()
    {
        ChartInput input = new()
        {
            Name = "Year 1000",
            LocalDateTime = new DateTime(1000, 6, 1, 12, 0, 0),
            TimeZone = "UTC",
            Latitude = 37.5407,
            Longitude = -77.4360,
            LocationName = "Richmond, Virginia",
            EventType = "Event",
        };

        Action act = () => _engine.Calculate(input);

        act.Should().Throw<ArgumentOutOfRangeException>().WithMessage(
            "1000-06-01 is outside the years the ephemeris files cover, 1200 to 2399*");
    }

    /// <summary>
    /// The engine omits the hour only where the Sun does not rise or set: any other failure
    /// of the sunrise search still propagates out of <see cref="ChartEngine.Calculate"/>.
    /// </summary>
    [Fact]
    public void Calculate_WhenTheSunriseSearchFailsAnyOtherWay_Should_StillThrow()
    {
        ChartEngine engine = CreateEngine(new PlanetaryHourCalculator(new SunriseSearchFailure()));
        ChartInput input = new()
        {
            Name = "Sunrise search failure",
            LocalDateTime = new DateTime(2026, 3, 16, 12, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 37.5407,
            Longitude = -77.4360,
            LocationName = "Richmond, Virginia",
            EventType = "Event",
        };

        Action act = () => engine.Calculate(input);

        act.Should().Throw<EphemerisCalculationException>()
            .WithMessage(SunriseSearchFailure.Message);
    }

    /// <summary>
    /// Builds a chart engine on the shared ephemeris with the given planetary hour calculator.
    /// </summary>
    /// <param name="planetaryHour">The planetary hour calculator the engine uses.</param>
    /// <returns>The engine.</returns>
    private ChartEngine CreateEngine(PlanetaryHourCalculator planetaryHour)
    {
        Serilog.ILogger logger = new Serilog.LoggerConfiguration().CreateLogger();
        return new ChartEngine(
            _ephemeris, new AspectService(_ephemeris, logger), new MidpointService(logger),
            new DignityService(), new MoonConditionService(),
            new ChartContextService(new DignityService(), new SectService()),
            new FixedStarService(_ephemeris, logger),
            new DeclinationAspectService(), new PlanetConditionService(),
            new MoonMansionService(), planetaryHour,
            new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(),
            new ParanService(_ephemeris, logger), logger);
    }

    /// <summary>
    /// An ephemeris whose sunrise and sunset search fails with an ordinary
    /// <see cref="EphemerisCalculationException"/>, as a library error would. Nothing else
    /// is called by the planetary hour calculator before it, so every other member throws
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    private sealed class SunriseSearchFailure : IEphemerisService
    {
        /// <summary>The message of the failure.</summary>
        internal const string Message = "Sunrise calculation failed: probe failure";

        string IEphemerisService.Version => throw new NotSupportedException();

        (double SunriseJd, double SunsetJd) IEphemerisService.CalculateSunriseSunset(
            double julianDayUt, double latitude, double longitude) =>
            throw new EphemerisCalculationException(Message);

        PlanetPosition IEphemerisService.CalculatePosition(double julianDayUt, CelestialBodyId body) =>
            throw new NotSupportedException();

        PlanetPosition IEphemerisService.CalculatePositionLongitudeOnly(double julianDayUt, CelestialBodyId body) =>
            throw new NotSupportedException();

        IReadOnlyList<PlanetPosition> IEphemerisService.CalculateAllPositions(double julianDayUt) =>
            throw new NotSupportedException();

        double IEphemerisService.DateToJulianDay(int year, int month, int day, double hour, CalendarType calendar) =>
            throw new NotSupportedException();

        (int Year, int Month, int Day, double Hour) IEphemerisService.JulianDayToDate(
            double julianDay, CalendarType calendar) =>
            throw new NotSupportedException();

        HouseData IEphemerisService.CalculateHouses(double julianDayUt, double latitude, double longitude, char houseSystem) =>
            throw new NotSupportedException();

        double IEphemerisService.GetObliquity(double julianDayUt) => throw new NotSupportedException();

        HouseData IEphemerisService.CalculateHousesFromArmc(double armc, double latitude, double obliquity, char houseSystem) =>
            throw new NotSupportedException();

        double? IEphemerisService.CalculateRiseTransit(double julianDayUt, CelestialBodyId body,
            int rsmiFlag, double latitude, double longitude, string? starName) =>
            throw new NotSupportedException();

        (int Flags, double[] Tret) IEphemerisService.FindNextSolarEclipse(double jdStart) =>
            throw new NotSupportedException();

        (int Flags, double[] Tret) IEphemerisService.FindNextLunarEclipse(double jdStart) =>
            throw new NotSupportedException();

        double IEphemerisService.CalculateFixedStarLongitude(string starName, double julianDayUt) =>
            throw new NotSupportedException();

        (double Longitude, double Latitude) IEphemerisService.CalculateFixedStarPosition(string starName, double julianDayUt) =>
            throw new NotSupportedException();

        (double RightAscension, double Declination) IEphemerisService.EclipticToEquatorial(
            double eclipticLongitude, double eclipticLatitude, double obliquity) =>
            throw new NotSupportedException();

        double IEphemerisService.GetSiderealTimeDegrees(double julianDayUt) => throw new NotSupportedException();

        void IDisposable.Dispose()
        {
        }
    }
}
