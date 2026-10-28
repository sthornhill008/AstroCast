using AstroCast.Core.Exceptions;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="PlanetaryHourCalculator"/> verifying
/// Chaldean planetary hour assignments against known astronomical rules.
/// </summary>
/// <remarks>
/// Test dates: Jan 4, 2026 (Sunday), Jan 3, 2026 (Saturday).
/// Location: London (51.5°N, 0.0°W).
/// London winter sunrise ~08:06 UTC, sunset ~16:02 UTC.
/// Daytime hour duration ≈ 39.7 minutes.
/// Uses the shared "Ephemeris" collection fixture.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class PlanetaryHourCalculatorTests
{
    /// <summary>London latitude.</summary>
    private const double Latitude = 51.5;

    /// <summary>London longitude.</summary>
    private const double Longitude = 0.0;

    /// <summary>Timezone for London in winter.</summary>
    private const string TimeZone = "UTC";

    private readonly PlanetaryHourCalculator _calculator;
    private readonly EphemerisService _ephemeris;

    /// <summary>
    /// Receives the shared ephemeris fixture from the collection.
    /// </summary>
    public PlanetaryHourCalculatorTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        _calculator = new PlanetaryHourCalculator(_ephemeris);
    }

    #region Sunday Hour 1

    [Fact]
    public void Calculate_Sunday_FirstDaytimeHour_Should_BeSun()
    {
        // Arrange — 2026-01-04 (Sunday), 08:15 UTC (~9 min after London sunrise)
        double jd = _ephemeris.DateToJulianDay(2026, 1, 4, 8.25);

        // Act
        PlanetaryHour result = _calculator.Calculate(jd, Latitude, Longitude, TimeZone);

        // Assert
        result.RulingPlanet.Should().Be("Sun",
            "Sunday's 1st daytime hour should be ruled by the Sun");
        result.HourNumber.Should().Be(1,
            "just after sunrise should be hour 1");
        result.IsDaytime.Should().BeTrue(
            "just after sunrise should be daytime");
    }

    #endregion

    #region Saturday Hour 1

    [Fact]
    public void Calculate_Saturday_FirstDaytimeHour_Should_BeSaturn()
    {
        // Arrange — 2026-01-03 (Saturday), 08:15 UTC (~9 min after London sunrise)
        double jd = _ephemeris.DateToJulianDay(2026, 1, 3, 8.25);

        // Act
        PlanetaryHour result = _calculator.Calculate(jd, Latitude, Longitude, TimeZone);

        // Assert
        result.RulingPlanet.Should().Be("Saturn",
            "Saturday's 1st daytime hour should be ruled by Saturn");
    }

    #endregion

    #region Hour Number Range

    [Fact]
    public void Calculate_HourNumber_Should_BeBetween1And12()
    {
        // Arrange — 2026-01-04 12:00 UTC (mid-day)
        double jd = _ephemeris.DateToJulianDay(2026, 1, 4, 12.0);

        // Act
        PlanetaryHour result = _calculator.Calculate(jd, Latitude, Longitude, TimeZone);

        // Assert
        result.HourNumber.Should().BeGreaterThanOrEqualTo(1,
            "hour number must be at least 1");
        result.HourNumber.Should().BeLessThanOrEqualTo(12,
            "hour number must be at most 12");
    }

    #endregion

    #region Hour Boundaries

    [Fact]
    public void Calculate_HourStart_Should_BeBeforeHourEnd()
    {
        // Arrange — 2026-01-04 12:00 UTC
        double jd = _ephemeris.DateToJulianDay(2026, 1, 4, 12.0);

        // Act
        PlanetaryHour result = _calculator.Calculate(jd, Latitude, Longitude, TimeZone);

        // Assert
        result.HourStart.Should().BeBefore(result.HourEnd,
            "hour start must be before hour end");
    }

    #endregion

    #region Nighttime

    [Fact]
    public void Calculate_Nighttime_Should_SetIsDaytimeFalse()
    {
        // Arrange — 2026-01-04 18:00 UTC (well after London sunset ~16:02)
        double jd = _ephemeris.DateToJulianDay(2026, 1, 4, 18.0);

        // Act
        PlanetaryHour result = _calculator.Calculate(jd, Latitude, Longitude, TimeZone);

        // Assert
        result.IsDaytime.Should().BeFalse(
            "18:00 UTC should be nighttime in London in January");
        result.HourNumber.Should().BeGreaterThanOrEqualTo(1)
            .And.BeLessThanOrEqualTo(12);
    }

    #endregion

    #region Chaldean Order Cycle

    [Fact]
    public void Calculate_ChaldeanOrder_Should_CycleCorrectly()
    {
        // Arrange — Sunday 2026-01-04, London
        // Chaldean order from Sun (index 3): Sun(3), Venus(4), Mercury(5)
        // Hours 1, 2, 3 should follow this sequence.
        //
        // London sunrise ~08:06, sunset ~16:02 UTC. Hour duration ≈ 39.7 min.
        // Hour 1: ~08:06–08:46 → use 08:15 (8.25h)
        // Hour 2: ~08:46–09:26 → use 09:00 (9.0h)
        // Hour 3: ~09:26–10:06 → use 09:40 (9.667h)
        double jd1 = _ephemeris.DateToJulianDay(2026, 1, 4, 8.25);
        double jd2 = _ephemeris.DateToJulianDay(2026, 1, 4, 9.0);
        double jd3 = _ephemeris.DateToJulianDay(2026, 1, 4, 9.667);

        // Act
        PlanetaryHour hour1 = _calculator.Calculate(jd1, Latitude, Longitude, TimeZone);
        PlanetaryHour hour2 = _calculator.Calculate(jd2, Latitude, Longitude, TimeZone);
        PlanetaryHour hour3 = _calculator.Calculate(jd3, Latitude, Longitude, TimeZone);

        // Assert — Chaldean order from Sunday: Sun → Venus → Mercury
        hour1.RulingPlanet.Should().Be("Sun", "Sunday hour 1 = Sun");
        hour2.RulingPlanet.Should().Be("Venus", "Sunday hour 2 = Venus");
        hour3.RulingPlanet.Should().Be("Mercury", "Sunday hour 3 = Mercury");
    }

    #endregion

    #region Nighttime Hour 1 Continuity

    [Fact]
    public void Calculate_NighttimeHour1_Should_ContinueFromDaytimeHour12()
    {
        // Arrange — Sunday 2026-01-04, London
        // Daytime hour 12: just before sunset (~15:40 UTC = 15.667h)
        // Nighttime hour 1: just after sunset (~16:20 UTC = 16.333h)
        //
        // Sunday: dayRulerIndex = 3 (Sun)
        // Daytime hour 12: (3 + 11) % 7 = 14 % 7 = 0 → Saturn
        // Nighttime hour 1: (3 + 12) % 7 = 15 % 7 = 1 → Jupiter
        double jdDay12 = _ephemeris.DateToJulianDay(2026, 1, 4, 15.667);
        double jdNight1 = _ephemeris.DateToJulianDay(2026, 1, 4, 16.333);

        // Act
        PlanetaryHour dayHour12 = _calculator.Calculate(jdDay12, Latitude, Longitude, TimeZone);
        PlanetaryHour nightHour1 = _calculator.Calculate(jdNight1, Latitude, Longitude, TimeZone);

        // Assert
        dayHour12.IsDaytime.Should().BeTrue("15:40 should be daytime");
        dayHour12.HourNumber.Should().Be(12, "should be the last daytime hour");
        dayHour12.RulingPlanet.Should().Be("Saturn", "Sunday daytime hour 12 = Saturn");

        nightHour1.IsDaytime.Should().BeFalse("16:20 should be nighttime");
        nightHour1.HourNumber.Should().Be(1, "should be the first nighttime hour");
        nightHour1.RulingPlanet.Should().Be("Jupiter",
            "Sunday nighttime hour 1 = Jupiter (follows Saturn in Chaldean order)");
    }

    #endregion

    #region Circumpolar Guard (HIGH-003 / CRIT-003)

    /// <summary>
    /// Verifies that <see cref="PlanetaryHourCalculator.Calculate"/> throws when
    /// called for a circumpolar location/date where sunrise does not occur.
    /// The <see cref="EphemerisCalculationException"/> from
    /// <see cref="EphemerisService.CalculateSunriseSunset"/> propagates through,
    /// preventing the division-by-zero that would otherwise occur.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Calculate_Should_ThrowEphemerisCalculationException_When_CircumpolarConditions()
    {
        // Arrange — Tromsø (70°N), 2024-Jun-21 (midnight sun — no sunrise/sunset)
        const double circumpolarLatitude = 70.0;
        const double tromsoLongitude = 19.0;
        double summerSolsticeJd = _ephemeris.DateToJulianDay(2024, 6, 21, 12.0);

        // Act & Assert — EphemerisCalculationException should propagate from
        // CalculateSunriseSunset through PlanetaryHourCalculator
        Action act = () => _calculator.Calculate(
            summerSolsticeJd, circumpolarLatitude, tromsoLongitude, "UTC");

        act.Should().Throw<EphemerisCalculationException>(
            "circumpolar conditions should throw before reaching division operations");
    }

    #endregion

    #region The sunrise and sunset around the instant (F9-1)

    // Every event instant below is typed from a direct swe_rise_trans call on the shipped
    // libswe.dylib (Tier B: SE_CALC_RISE or SE_CALC_SET | SE_BIT_DISC_CENTER, 1013.25 hPa,
    // 15 °C, altitude 0), never from AstroCast. The rulers come from the typed tables: the
    // day ruler of the sunrise's weekday (Sunday Sun, Monday Moon, Tuesday Mars, Wednesday
    // Mercury, Thursday Jupiter, Friday Venus, Saturday Saturn), then the Chaldean order
    // Saturn, Jupiter, Mars, Sun, Venus, Mercury, Moon, one step per hour, the night's
    // hours continuing the day's (night hour n is step 11 + n).

    /// <summary>Richmond, Virginia (the place the outputs harness uses): latitude.</summary>
    private const double RichmondLatitude = 37.5407;

    /// <summary>Richmond, Virginia: longitude, east positive.</summary>
    private const double RichmondLongitude = -77.4360;

    /// <summary>Richmond's zone, whose clock dates each sunrise.</summary>
    private const string RichmondZone = "America/New_York";

    /// <summary>Thirty seconds, in days.</summary>
    private const double ThirtySeconds = 30.0 / 86400.0;

    /// <summary>
    /// Sunrise at Richmond on Monday 2026-03-16 is 11:20:34 UT (JD 2461115.9726183442),
    /// 90 s earlier than the day before. Searching from one day earlier lands on Sunday's
    /// sunrise for 90 s after it, which the old code took as this day's: Venus, hour 9.
    /// Thirty seconds after sunrise it is Monday's first day hour: the Moon.
    /// </summary>
    [Fact]
    public void Calculate_ThirtySecondsAfterASunriseThatCameEarlier_Should_BeTheFirstDayHourOfThatDay()
    {
        const double sunrise = 2461115.9726183442; // 2026-03-16 11:20:34 UT

        PlanetaryHour result = _calculator.Calculate(
            sunrise + ThirtySeconds, RichmondLatitude, RichmondLongitude, RichmondZone);

        result.RulingPlanet.Should().Be("Moon", "Monday's first day hour is the Moon's");
        result.HourNumber.Should().Be(1);
        result.IsDaytime.Should().BeTrue();
        // Sunset 23:16:35 UT (JD 2461116.4698495925): the first twelfth of the day, local clock (EDT).
        result.HourStart.Should().Be(new DateTime(2026, 3, 16, 7, 20, 34));
        result.HourEnd.Should().Be(new DateTime(2026, 3, 16, 8, 20, 14));
    }

    /// <summary>
    /// Sunset at Richmond on Tuesday 2026-09-22 is 23:05:10 UT (JD 2461306.4619220565),
    /// earlier than the day before. Thirty seconds after it is the first night hour of
    /// Tuesday (Mars, step 12 from Mars: Saturn), not the old code's Jupiter, hour 9.
    /// </summary>
    [Fact]
    public void Calculate_ThirtySecondsAfterASunsetThatCameEarlier_Should_BeTheFirstNightHourOfThatDay()
    {
        const double sunset = 2461306.4619220565; // 2026-09-22 23:05:10 UT

        PlanetaryHour result = _calculator.Calculate(
            sunset + ThirtySeconds, RichmondLatitude, RichmondLongitude, RichmondZone);

        result.RulingPlanet.Should().Be("Saturn", "Tuesday's first night hour is Saturn's");
        result.HourNumber.Should().Be(1);
        result.IsDaytime.Should().BeFalse();
        // Next sunrise 2026-09-23 10:59:52 UT (JD 2461306.958237791): the first twelfth of the night.
        result.HourStart.Should().Be(new DateTime(2026, 9, 22, 19, 5, 10));
        result.HourEnd.Should().Be(new DateTime(2026, 9, 22, 20, 4, 44));
    }

    /// <summary>
    /// Sunrise at Richmond on Wednesday 2026-09-23 is 10:59:52 UT (JD 2461306.958237791),
    /// 51 s later than the day before. Thirty seconds before it the night still belongs to
    /// Tuesday, whose sunrise was 10:59:01 UT: hour 12 of the night, Mars step 23, Venus.
    /// The old code took Wednesday's sunrise and gave tomorrow's ruler, Saturn.
    /// </summary>
    [Fact]
    public void Calculate_ThirtySecondsBeforeASunriseThatComesLater_Should_KeepTheOldDaysRuler()
    {
        const double sunrise = 2461306.958237791; // 2026-09-23 10:59:52 UT

        PlanetaryHour result = _calculator.Calculate(
            sunrise - ThirtySeconds, RichmondLatitude, RichmondLongitude, RichmondZone);

        result.RulingPlanet.Should().Be("Venus", "Tuesday's twelfth night hour is Venus's");
        result.HourNumber.Should().Be(12);
        result.IsDaytime.Should().BeFalse();
        result.HourStart.Should().Be(new DateTime(2026, 9, 23, 6, 0, 18));
        result.HourEnd.Should().Be(new DateTime(2026, 9, 23, 6, 59, 52));
    }

    /// <summary>
    /// At 64° N (longitude 77.6238658° W, the foundations' scan point) the Sun sets at
    /// 03:35:57 UT on 2026-06-21 (JD 2461212.649968406), 23:35:57 on Saturday 2026-06-20
    /// by New York's clock. For up to 291 s before it the library's "next sunset" from the
    /// instant skips this sunset, and the old code called the instant night (Mercury,
    /// hour 1). Two minutes before sunset it is Saturday's twelfth day hour: Saturn step 11,
    /// Venus. Saturday's sunrise, found directly, was 06:48:26 UT on 2026-06-20.
    /// </summary>
    [Fact]
    public void Calculate_TwoMinutesBeforeASunsetTheLibrarySkips_Should_BeTheTwelfthDayHour()
    {
        const double latitude = 64.0;
        const double longitude = -77.6238658;
        const double sunset = 2461212.649968406; // 2026-06-21 03:35:57 UT

        PlanetaryHour result = _calculator.Calculate(
            sunset - (120.0 / 86400.0), latitude, longitude, RichmondZone);

        result.RulingPlanet.Should().Be("Venus", "Saturday's twelfth day hour is Venus's");
        result.HourNumber.Should().Be(12);
        result.IsDaytime.Should().BeTrue();
        result.HourStart.Should().Be(new DateTime(2026, 6, 20, 21, 52, 0));
        result.HourEnd.Should().Be(new DateTime(2026, 6, 20, 23, 35, 57));
    }

    #endregion

    #region A fixed offset's clock (F9-2)

    /// <summary>
    /// Chart 5 (Bill Clinton) is cast for 08:51 at an explicit UTC−6 in Hope, Arkansas:
    /// JD 2432052.11875 (14:51 UT), 33.6670616° N, 93.5915665° W. Found directly (Tier B):
    /// sunrise 11:40:08 UT (JD 2432051.9861990307), Monday 1946-08-19 by the chart's clock;
    /// sunset JD 2432052.5383652295. Day hour 3 runs from JD 2432052.07822673 to
    /// 2432052.1242405805, 13:52:39 to 14:58:54 UT, so 07:52:39 to 08:58:54 at UTC−6, and its
    /// ruler is Monday's Moon two steps on: Jupiter.
    /// </summary>
    [Fact]
    public void CalculateAtFixedOffset_Should_GiveTheHourOnThatOffsetsClock()
    {
        PlanetaryHour result = _calculator.CalculateAtFixedOffset(
            2432052.11875, 33.6670616, -93.5915665, -6.0);

        result.RulingPlanet.Should().Be("Jupiter", "Monday's third day hour is Jupiter's");
        result.HourNumber.Should().Be(3);
        result.IsDaytime.Should().BeTrue();
        result.HourStart.Should().Be(new DateTime(1946, 8, 19, 7, 52, 39));
        result.HourEnd.Should().Be(new DateTime(1946, 8, 19, 8, 58, 54));
    }

    #endregion
}
