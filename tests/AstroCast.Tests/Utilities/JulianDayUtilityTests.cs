using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Utilities;

/// <summary>
/// Tests for <see cref="JulianDayUtility"/> verifying Julian Day to DateTime
/// conversion produces correct UTC and local time results.
/// Uses the shared "Ephemeris" collection fixture.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class JulianDayUtilityTests
{
    /// <summary>J2000.0 epoch — Julian Day 2451545.0 = 2000-Jan-1 12:00 UT.</summary>
    private const double J2000 = 2451545.0;

    private readonly EphemerisService _ephemeris;

    /// <summary>
    /// Receives the shared ephemeris fixture from the collection.
    /// </summary>
    public JulianDayUtilityTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
    }

    /// <summary>
    /// A Julian Day built from an exact clock time must convert back to that
    /// same clock time, not to the second before it.
    /// </summary>
    /// <param name="hour">Whole UT hour to round-trip.</param>
    /// <param name="minute">Minute to round-trip.</param>
    /// <remarks>
    /// <para>
    /// <see cref="IEphemerisService.JulianDayToDate"/> returns the time of day as
    /// a fractional hour, and that value carries floating-point error: a Julian
    /// Day built from 04:00 UT comes back as <c>3.9999999962747097</c> hours,
    /// about thirteen microseconds early. Decomposing it by truncation promotes
    /// that to a full second, labelling the instant 03:59:59 instead of 04:00:00.
    /// </para>
    /// <para>
    /// The J2000 case above did not catch it because 2451545.0 is a whole Julian
    /// Day plus a half and decomposes exactly. Off-grid values do not.
    /// </para>
    /// <para>
    /// <b>Which values bite -- measured, and previously recorded wrongly.</b> An
    /// earlier revision of this remark stated that "00:00 and 04:15 decompose
    /// exactly, 04:00 does not". It is wrong about 04:15. Sweeping all 96
    /// quarter-hour slots of 2026-04-20: 32 are bit-exact -- the multiples of 45
    /// minutes, among them 00:00, 08:15 and 13:30 -- and the other 64 all carry
    /// the identical magnitude, 13.411045074462891 microseconds, differing only
    /// in sign. 04:15, 17:45 and 23:45 are among those 64; they come back late
    /// rather than early, so truncation happened to survive them and they were
    /// mistaken for exact. The 32 early values are the ones truncation loses a
    /// second on. Only the sign decides, which is why the whole grid is swept
    /// rather than one representative hour. Measured on Windows x64 and on macOS
    /// arm64, agreeing slot for slot.
    /// </para>
    /// <para>
    /// This was never a cosmetic rounding preference. Grid times carry zero
    /// seconds, so a second rolled backwards crosses the <b>minute</b> boundary
    /// as well: a third of grid-derived times printed a minute early at the
    /// precision the exports display, :14 for :15. And 04:00 UT is local midnight
    /// in <c>America/New_York</c> under EDT, so there the lost second additionally
    /// crosses a <b>date</b> boundary: 2026-04-20 00:00 becomes 2026-04-19 23:59.
    /// That is what made an electional candidate at the first instant of a
    /// requested range print as the day before the range began.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(0, 0)]
    [InlineData(4, 0)]
    [InlineData(4, 15)]
    [InlineData(8, 15)]
    [InlineData(13, 30)]
    [InlineData(17, 45)]
    [InlineData(23, 45)]
    public void ToDateTimeUtc_Should_RoundTripAnExactClockTime(int hour, int minute)
    {
        // Arrange
        double decimalHour = hour + (minute / 60.0);
        double julianDay = _ephemeris.DateToJulianDay(2026, 4, 20, decimalHour);

        // Act
        DateTime result = JulianDayUtility.ToDateTimeUtc(_ephemeris, julianDay);

        // Assert
        DateTime expected = new(2026, 4, 20, hour, minute, 0, DateTimeKind.Utc);

        result.Should().Be(expected,
            "a Julian Day built from {0:00}:{1:00} UT must convert back to that clock time; "
            + "the fractional hour carries floating-point error, so decomposing it by "
            + "truncation reports the second before -- and at 00:00 that is the previous day",
            hour, minute);
    }

    [Fact]
    public void ToDateTimeUtc_Should_ReturnCorrectUtcDateTime_When_J2000Epoch()
    {
        // Act
        DateTime result = JulianDayUtility.ToDateTimeUtc(_ephemeris, J2000);

        // Assert — J2000.0 = 2000-Jan-1 12:00:00 UTC
        result.Year.Should().Be(2000);
        result.Month.Should().Be(1);
        result.Day.Should().Be(1);
        result.Hour.Should().Be(12);
        result.Minute.Should().Be(0);
        result.Second.Should().Be(0);
        result.Kind.Should().Be(DateTimeKind.Utc,
            "the result should have UTC DateTimeKind");
    }

    [Fact]
    public void ToLocalDateTime_Should_ConvertToLocalTime_When_EasternTimezone()
    {
        // Arrange — J2000.0 = 2000-Jan-1 12:00 UTC, EST = UTC-5 in January
        TzdbTimeZoneRules rules = new();

        // Act
        DateTime result = JulianDayUtility.ToLocalDateTime(_ephemeris, J2000, rules, "America/New_York");

        // Assert — 12:00 UTC = 07:00 EST (January, no DST)
        result.Year.Should().Be(2000);
        result.Month.Should().Be(1);
        result.Day.Should().Be(1);
        result.Hour.Should().Be(7,
            "12:00 UTC should convert to 07:00 EST (UTC-5 in January)");
        result.Minute.Should().Be(0);
    }
}
