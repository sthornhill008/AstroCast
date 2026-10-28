using System.Globalization;
using System.Text.Json;

using AstroCast.Core.Data;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// How a chart's time input becomes its instant (branch F5): an explicit offset and a
/// reading are taken to the whole second, and the instant the chart is cast at, its
/// <see cref="ChartResult.UtcDateTime"/> and its <see cref="ChartResult.JulianDay"/> are one
/// and the same; a saved chart stores that instant.
/// </summary>
/// <remarks>
/// Every expected Julian Day is Meeus's (Astronomical Algorithms, ch. 7, formula 7.1),
/// worked in exact fractions outside the code (<c>f5-time/truth/meeus.py</c>). The app's
/// value is a double, so it is compared to within 1e-8 day (under a millisecond); a second
/// is 1.16e-5 day.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartEngineTimeInputTests
{
    /// <summary>A Julian Day the app computes is within this of Meeus's exact value.</summary>
    private const double JdTolerance = 1e-8;

    private readonly EphemerisFixture _fixture;
    private readonly ChartEngine _engine;

    /// <summary>Receives the shared ephemeris fixture and builds a fully wired engine.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ChartEngineTimeInputTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _engine = GoldenFixtures.CreateChartEngine(fixture);
    }

    #region Offsets and readings to the whole second (F0-4)

    /// <summary>
    /// An explicit offset entered as decimal hours keeps its seconds: it is taken to the
    /// nearest second, so 12:00 at −5:50:36 is 17:50:36 UT exactly, not 17:50:35.9999999
    /// cast as 17:50:35. The result's instant and Julian Day agree, and a saved chart
    /// stores the same Julian Day.
    /// </summary>
    /// <param name="offsetHours">The explicit offset, as the form's decimal hours.</param>
    /// <param name="label">The offset's label.</param>
    /// <param name="expectedUtc">The instant, as <c>yyyy-MM-ddTHH:mm:ss</c>.</param>
    /// <param name="meeusJd">Meeus's Julian Day of that instant.</param>
    [Theory]
    [InlineData(-5.0 - 50 / 60.0 - 36 / 3600.0, "UTC−5:50:36 (explicit)", "1950-06-15T17:50:36", 2433448.2434722222)]
    [InlineData(-4.0 - 56 / 60.0 - 2 / 3600.0, "UTC−4:56:02 (explicit)", "1950-06-15T16:56:02", 2433448.205578704)]
    [InlineData(9 / 60.0 + 21 / 3600.0, "UTC+0:09:21 (explicit)", "1950-06-15T11:50:39", 2433447.9935069443)]
    public void Calculate_ExplicitOffset_Should_KeepItsSeconds(
        double offsetHours, string label, string expectedUtc, double meeusJd)
    {
        ChartInput input = Input(new DateTime(1950, 6, 15, 12, 0, 0), "America/New_York", offsetHours);

        ChartResult result = _engine.Calculate(input);

        AssertInstant(result, input, expectedUtc, meeusJd);
        result.ResolvedOffsetLabel.Should().Be(label);
        result.ResolvedUtcOffsetHours.Should().Be(
            Math.Round(offsetHours * 3600.0, MidpointRounding.AwayFromZero) / 3600.0,
            "the offset applied is the entered one taken to the nearest second");
    }

    /// <summary>
    /// A reading with a fraction of a second is taken to the nearest second, through a zone
    /// or at an explicit offset: 12:00:00.6 EDT is 16:00:01 UT in the instant and in the
    /// Julian Day alike, where the instant kept the fraction and the Julian Day dropped it.
    /// </summary>
    /// <param name="milliseconds">The fraction of the reading's second.</param>
    /// <param name="explicitOffset">An explicit offset, or <see langword="null"/> for the zone's.</param>
    /// <param name="expectedUtc">The instant, as <c>yyyy-MM-ddTHH:mm:ss</c>.</param>
    /// <param name="meeusJd">Meeus's Julian Day of that instant.</param>
    [Theory]
    [InlineData(600, null, "2026-06-15T16:00:01", 2461207.1666782405)]
    [InlineData(400, null, "2026-06-15T16:00:00", 2461207.1666666665)]
    [InlineData(600, -4.0, "2026-06-15T16:00:01", 2461207.1666782405)]
    public void Calculate_ReadingWithAFractionOfASecond_Should_TakeTheNearestSecond(
        int milliseconds, double? explicitOffset, string expectedUtc, double meeusJd)
    {
        ChartInput input = Input(new DateTime(2026, 6, 15, 12, 0, 0).AddMilliseconds(milliseconds), "America/New_York", explicitOffset);

        ChartResult result = _engine.Calculate(input);

        AssertInstant(result, input, expectedUtc, meeusJd);
    }

    #endregion

    #region The calendar, decided on the local date (F0-5, D21)

    /// <summary>
    /// The calendar is decided on the local date, then the offset applied: a local date on
    /// or after 1582-10-15 is Gregorian, on or before 1582-10-04 Julian. 1582-10-15 00:30 at
    /// UTC+1 is 1582-10-04 23:30 UT on the Julian calendar, JD 2299160.479, where deciding it
    /// on the UT date read 14 October as Julian and cast it ten days late, at 2299170.479.
    /// The instant is held on the calendar in force on its own date, as the Julian Day is
    /// read.
    /// </summary>
    /// <param name="local">The local reading, on its own calendar.</param>
    /// <param name="offsetHours">The explicit offset.</param>
    /// <param name="expectedUtc">The instant, on the calendar in force on its date.</param>
    /// <param name="meeusJd">Meeus's Julian Day.</param>
    [Theory]
    [InlineData("1582-10-15T00:30:00", 1.0, "1582-10-04T23:30:00", 2299160.4791666665)]
    [InlineData("1582-10-04T23:00:00", 0.0, "1582-10-04T23:00:00", 2299160.4583333335)]
    [InlineData("1582-10-15T00:00:00", 0.0, "1582-10-15T00:00:00", 2299160.5)]
    [InlineData("1582-10-04T23:30:00", -1.0, "1582-10-15T00:30:00", 2299160.5208333335)]
    public void Calculate_Should_DecideTheCalendarOnTheLocalDate(
        string local, double offsetHours, string expectedUtc, double meeusJd)
    {
        ChartInput input = Input(DateTime.Parse(local, CultureInfo.InvariantCulture), "Europe/Rome", offsetHours);

        ChartResult result = _engine.Calculate(input);

        AssertCalendarInstant(result, input, expectedUtc, meeusJd);
    }

    /// <summary>
    /// A zone's reading before the switch is read on its own calendar too, and the zone's
    /// offset then is its local mean time, from the time-zone data for the same instant:
    /// Rome's +0:49:56 takes 1582-10-15 00:30 to 1582-10-04 23:40:04 UT on the Julian
    /// calendar, and London's −0:01:15 takes 1500-03-10 12:00 to 12:01:15 UT.
    /// </summary>
    /// <param name="zone">The IANA zone identifier.</param>
    /// <param name="local">The local reading, on its own calendar.</param>
    /// <param name="expectedUtc">The instant, on the calendar in force on its date.</param>
    /// <param name="meeusJd">Meeus's Julian Day.</param>
    /// <param name="label">The offset label: zoneinfo's local mean time.</param>
    /// <param name="note">The Time Note, which prints the instant on the calendar in force.</param>
    [Theory]
    [InlineData("Europe/Rome", "1582-10-15T00:30:00", "1582-10-04T23:40:04", 2299160.4861574075, "LMT (UTC+0:49:56)",
        "Before standard time, the time in Europe/Rome is the local mean time of Rome, UTC+0:49:56; the chart is cast for 1582-10-04 23:40:04 UT. An explicit offset gives the birthplace's own mean time.")]
    [InlineData("Europe/Rome", "1582-10-04T23:00:00", "1582-10-04T22:10:04", 2299160.4236574075, "LMT (UTC+0:49:56)",
        "Before standard time, the time in Europe/Rome is the local mean time of Rome, UTC+0:49:56; the chart is cast for 22:10:04 UT. An explicit offset gives the birthplace's own mean time.")]
    [InlineData("Europe/London", "1500-03-10T12:00:00", "1500-03-10T12:01:15", 2269002.0008680555, "LMT (UTC−0:01:15)",
        "Before standard time, the time in Europe/London is the local mean time of London, UTC−0:01:15; the chart is cast for 12:01:15 UT. An explicit offset gives the birthplace's own mean time.")]
    public void Calculate_ZoneReadingBeforeTheSwitch_Should_TakeLocalMeanTime_OnItsOwnCalendar(
        string zone, string local, string expectedUtc, double meeusJd, string label, string note)
    {
        DateTime reading = DateTime.Parse(local, CultureInfo.InvariantCulture);
        ChartInput input = Input(reading, zone, null);

        ChartResult result = _engine.Calculate(input);

        AssertCalendarInstant(result, input, expectedUtc, meeusJd);
        result.ResolvedOffsetLabel.Should().Be(label);
        result.ResolvedLocalDateTime.Should().Be(reading, "the chart is cast at its reading, on its own calendar");
        result.TimeNote.Should().Be(note);
    }

    /// <summary>
    /// 1582-10-05 to 1582-10-14 never occurred: the day after 1582-10-04 (Julian) was
    /// 1582-10-15 (Gregorian). Such a date is refused, by the engine and by the save, with a
    /// message that says so, where it was read as Julian and gave the chart of ten days later.
    /// </summary>
    /// <param name="local">A local reading in the ten missing days.</param>
    /// <param name="explicitOffset">An explicit offset, or <see langword="null"/> for the zone's.</param>
    [Theory]
    [InlineData("1582-10-05T12:00:00", 1.0)]
    [InlineData("1582-10-10T12:00:00", 1.0)]
    [InlineData("1582-10-10T12:00:00", null)]
    [InlineData("1582-10-14T23:59:59", 0.0)]
    public void Calculate_Should_RefuseTheTenDaysThatNeverOccurred(string local, double? explicitOffset)
    {
        DateTime reading = DateTime.Parse(local, CultureInfo.InvariantCulture);
        ChartInput input = Input(reading, "Europe/Rome", explicitOffset);
        string expected = string.Create(CultureInfo.InvariantCulture,
            $"{reading:yyyy-MM-dd} does not exist: the Gregorian calendar began on 1582-10-15, the day after 1582-10-04 on the Julian calendar, so 1582-10-05 to 1582-10-14 never occurred.*");

        Action cast = () => _engine.Calculate(input);
        Action save = () => ChartMapper.ToEntity(input, _fixture.EphemerisService);

        cast.Should().Throw<ArgumentOutOfRangeException>().WithMessage(expected);
        save.Should().Throw<ArgumentOutOfRangeException>().WithMessage(expected);
    }

    /// <summary>
    /// An instant whose UT date is 29 February of 1300, 1400 or 1500 on the Julian calendar
    /// (a leap day there, not on the Gregorian calendar) cannot be held as a date, so it is
    /// refused with a message that says so, where it was cast a day early, on 28 February.
    /// </summary>
    /// <param name="local">The local reading, on the Julian calendar.</param>
    /// <param name="offsetHours">The explicit offset that takes it to 29 February UT.</param>
    [Theory]
    [InlineData("1500-03-01T00:30:00", 1.0)]
    [InlineData("1500-02-28T23:30:00", -1.0)]
    public void Calculate_Should_RefuseAnInstantOnAJulianLeapDayADateCannotHold(string local, double offsetHours)
    {
        ChartInput input = Input(DateTime.Parse(local, CultureInfo.InvariantCulture), "Europe/London", offsetHours);

        Action cast = () => _engine.Calculate(input);

        cast.Should().Throw<ArgumentOutOfRangeException>().WithMessage(
            "The chart's moment falls on 1500-02-29 UT, a leap day of the Julian calendar that the app cannot hold as a date.*");
    }

    #endregion

    #region Only the years the files cover (F0-6)

    /// <summary>The refusal's text after the instant it names.</summary>
    private const string OutsideTheFiles =
        " is outside the years the ephemeris files cover, 1200 to 2399: a chart can be cast from " +
        "1200-01-01 04:54:46 UT (Julian calendar) to 2399-12-31 23:51:20 UT.*";

    /// <summary>
    /// An instant just inside either edge of the shipped files' span is cast, every body read
    /// from the files. The span is where direct <c>swe_calc_ut</c> calls, each from a freshly
    /// opened library, give all twelve bodies from the files with no error and no fall-back to
    /// the Moshier model: from 1200-01-01 04:54:45.32 UT (Julian; Pluto, whose light time
    /// reaches back before the first file's start) to 2399-12-31 23:51:20.42 UT (Chiron, whose
    /// file for the year 2400 is not shipped) (<c>f5-time/truth/span.py</c>).
    /// </summary>
    /// <param name="local">The UT reading, at an explicit offset of zero.</param>
    /// <param name="meeusJd">Meeus's Julian Day.</param>
    [Theory]
    [InlineData("1200-01-01T04:54:46", 2159357.704699074)]
    [InlineData("2399-12-31T23:51:20", 2597641.4939814815)]
    public void Calculate_JustInsideTheFiles_Should_Cast(string local, double meeusJd)
    {
        ChartResult result = _engine.Calculate(Input(DateTime.Parse(local, CultureInfo.InvariantCulture), "UTC", 0.0));

        result.JulianDay.Should().BeApproximately(meeusJd, JdTolerance);
        result.Positions.Should().HaveCount(12);
    }

    /// <summary>
    /// An instant outside the files' span is refused, with a message naming the span in
    /// years, before the ephemeris is consulted: a second before the first edge (where Pluto
    /// came silently from the Moshier model), a second after the last (where Chiron failed
    /// with a file message), and further out. The test is on the instant, not the local year:
    /// 23:00 on 2399-12-31 in New York is 2400-01-01 04:00 UT, and midnight on 1200-01-01 in
    /// Tokyo, on its local mean time of +9:18:59 (zoneinfo), is 1199-12-31 14:41:01 UT.
    /// </summary>
    /// <param name="local">The local reading.</param>
    /// <param name="zone">The IANA zone identifier.</param>
    /// <param name="explicitOffset">An explicit offset, or <see langword="null"/> for the zone's.</param>
    /// <param name="named">The instant or date the refusal names.</param>
    [Theory]
    [InlineData("1200-01-01T04:54:45", "UTC", 0.0, "1200-01-01 04:54:45 UT")]
    [InlineData("2399-12-31T23:51:21", "UTC", 0.0, "2399-12-31 23:51:21 UT")]
    [InlineData("2399-12-31T23:00:00", "America/New_York", null, "2400-01-01 04:00:00 UT")]
    [InlineData("1200-01-01T00:00:00", "Asia/Tokyo", null, "1199-12-31 14:41:01 UT")]
    [InlineData("2400-06-15T12:00:00", "UTC", null, "2400-06-15 12:00:00 UT")]
    [InlineData("1000-06-01T12:00:00", "UTC", null, "1000-06-01")]
    [InlineData("6000-01-01T12:00:00", "UTC", null, "6000-01-01")]
    public void Calculate_OutsideTheFiles_Should_BeRefused(string local, string zone, double? explicitOffset, string named)
    {
        ChartInput input = Input(DateTime.Parse(local, CultureInfo.InvariantCulture), zone, explicitOffset);

        Action cast = () => _engine.Calculate(input);

        cast.Should().Throw<ArgumentOutOfRangeException>().WithMessage(named + OutsideTheFiles);
    }

    /// <summary>
    /// An electional candidate cast at a given instant is held to the same span.
    /// </summary>
    [Fact]
    public void CalculateForElectional_AtAnInstantOutsideTheFiles_Should_BeRefused()
    {
        ChartInput input = Input(new DateTime(2399, 12, 31, 23, 0, 0), "America/New_York", null);

        Action cast = () => _engine.CalculateForElectional(input, new DateTime(2400, 1, 1, 4, 0, 0, DateTimeKind.Utc));

        cast.Should().Throw<ArgumentOutOfRangeException>().WithMessage("2400-01-01 04:00:00 UT" + OutsideTheFiles);
    }

    #endregion

    #region Stored UTC is UTC (F0-7, E1-9)

    /// <summary>
    /// <see cref="ChartMapper.ToEntity"/> writes an explicit-offset chart's UTC instant with a
    /// <c>Z</c> whatever the input's <see cref="DateTimeKind"/>, where it wrote the input's own
    /// kind: no suffix, or the saving machine's offset, which as an ISO instant is hours wrong
    /// (known issue 3). The Julian Day is the same in every kind.
    /// </summary>
    /// <param name="kind">The kind of the input's local date and time.</param>
    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Utc)]
    public void ToEntity_ExplicitOffset_Should_WriteTheUtcInstantWithAZ_WhateverTheKind(DateTimeKind kind)
    {
        ChartInput input = Input(DateTime.SpecifyKind(new DateTime(1946, 6, 14, 10, 54, 0), kind), "America/New_York", -4.0);
        ChartInput unspecified = Input(new DateTime(1946, 6, 14, 10, 54, 0), "America/New_York", -4.0);

        ChartEntity entity = ChartMapper.ToEntity(input, _fixture.EphemerisService);

        entity.UtcDateTime.Should().Be("1946-06-14T14:54:00.0000000Z");
        entity.JulianDay.Should().Be(ChartMapper.ToEntity(unspecified, _fixture.EphemerisService).JulianDay);
    }

    /// <summary>
    /// A chart's <see cref="ChartResult.UtcDateTime"/> is always UTC, at an explicit offset as
    /// through a zone, so the chart JSON's <c>utcDateTime</c> ends in <c>Z</c> for both.
    /// </summary>
    /// <param name="explicitOffset">An explicit offset, or <see langword="null"/> for the zone's.</param>
    [Theory]
    [InlineData(-4.0)]
    [InlineData(null)]
    public void Calculate_Should_HoldTheInstantAsUtc_AndTheJsonShouldSaySo(double? explicitOffset)
    {
        ChartResult result = _engine.Calculate(Input(new DateTime(1946, 6, 14, 10, 54, 0), "America/New_York", explicitOffset));

        result.UtcDateTime.Kind.Should().Be(DateTimeKind.Utc);
        using JsonDocument json = JsonDocument.Parse(ChartJsonFormatter.ToJson(result));
        json.RootElement.GetProperty("chart").GetProperty("utcDateTime").GetString().Should().Be("1946-06-14T14:54:00.0000000Z");
    }

    #endregion

    /// <summary>
    /// The result is cast at the expected instant, held as proleptic ticks on every calendar (R-D4,
    /// branch R2, 1.3.4): a label before 1582-10-15 names a date on the Julian calendar, and the
    /// instant is that date's (Julian 1300-03-15 12:00 is 1300-03-23T12:00:00Z as ticks); its Julian
    /// Day is Meeus's, the instant converts to it by arithmetic, it prints as the label on the
    /// calendar in force, and saving the chart stores it. Until 1.3.4 the ticks were the Julian
    /// label's own, 7 to 10 days from the instant.
    /// </summary>
    /// <param name="result">The chart.</param>
    /// <param name="input">Its input.</param>
    /// <param name="expectedUtc">The instant, as <c>yyyy-MM-ddTHH:mm:ss</c> on the calendar in force.</param>
    /// <param name="meeusJd">Meeus's Julian Day of that instant.</param>
    private void AssertCalendarInstant(ChartResult result, ChartInput input, string expectedUtc, double meeusJd)
    {
        DateTime label = DateTime.Parse(expectedUtc, CultureInfo.InvariantCulture);
        DateTime utc = label < new DateTime(1582, 10, 15)
            ? new JulianCalendar().ToDateTime(label.Year, label.Month, label.Day, label.Hour, label.Minute, label.Second, 0)
            : label;
        DateTime cast = result.UtcDateTime;

        cast.Ticks.Should().Be(utc.Ticks, "the chart is cast at the instant of {0} UT on the calendar in force", expectedUtc);
        cast.Kind.Should().Be(DateTimeKind.Utc);
        result.JulianDay.Should().BeApproximately(meeusJd, JdTolerance);
        JulianDayUtility.InstantToJulianDay(cast)
            .Should().Be(result.JulianDay, "the instant and the Julian Day are the same instant");
        CalendarDateFormatter.DateTime(cast).Should().Be(
            label.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), "the instant prints as its label on the calendar in force");
        ChartMapper.ToEntity(input, _fixture.EphemerisService).JulianDay.Should().Be(
            result.JulianDay, "a saved chart stores the instant the engine cast it at");
    }

    /// <summary>
    /// The result is cast at the expected instant to the tick, its Julian Day is Meeus's for
    /// that instant, the two convert into each other, and saving the chart stores the same
    /// Julian Day.
    /// </summary>
    /// <param name="result">The chart.</param>
    /// <param name="input">Its input.</param>
    /// <param name="expectedUtc">The instant, as <c>yyyy-MM-ddTHH:mm:ss</c>.</param>
    /// <param name="meeusJd">Meeus's Julian Day of that instant.</param>
    private void AssertInstant(ChartResult result, ChartInput input, string expectedUtc, double meeusJd)
    {
        DateTime utc = DateTime.Parse(expectedUtc, CultureInfo.InvariantCulture);

        result.UtcDateTime.Ticks.Should().Be(utc.Ticks, "the chart is cast at {0} UT exactly", expectedUtc);
        result.JulianDay.Should().BeApproximately(meeusJd, JdTolerance);
        JulianDayUtility.ToDateTimeUtc(_fixture.EphemerisService, result.JulianDay).Ticks.Should().Be(
            result.UtcDateTime.Ticks, "the instant and the Julian Day are the same instant");
        ChartMapper.ToEntity(input, _fixture.EphemerisService).JulianDay.Should().Be(
            result.JulianDay, "a saved chart stores the instant the engine cast it at");
    }

    /// <summary>Builds a chart input at New York's coordinates.</summary>
    /// <param name="local">The wall-clock reading.</param>
    /// <param name="zone">The IANA zone identifier.</param>
    /// <param name="explicitOffset">An explicit offset in hours, or <see langword="null"/> to cast through the zone.</param>
    /// <returns>The input.</returns>
    private static ChartInput Input(DateTime local, string zone, double? explicitOffset) => new()
    {
        Name = "Time input",
        LocalDateTime = local,
        TimeZone = zone,
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York",
        EventType = "Event",
        UseExplicitOffset = explicitOffset.HasValue,
        UtcOffsetHours = explicitOffset,
    };
}
