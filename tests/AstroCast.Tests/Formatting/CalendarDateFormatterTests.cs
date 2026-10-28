using System.Globalization;

using AstroCast.Core.Formatting;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="CalendarDateFormatter"/>, the one formatter every printed instant goes through
/// (branch R2, 1.3.4; rulings R-D1 to R-D4): an instant before 1582-10-15 prints on the Julian calendar,
/// one from 1582-10-15 on the Gregorian, the weekday is the instant's, and the text is the same under
/// every culture. The values are Tier B by Meeus arithmetic (<c>r2-labels/r2_truth.out</c>).
/// </summary>
[Trait("Category", "Unit")]
public sealed class CalendarDateFormatterTests
{
    /// <summary>
    /// The ten values of R1: the clock as proleptic ticks (<c>yyyy-MM-ddTHH:mm:ss</c>), the pattern and
    /// the text on the calendar in force. The second before the switch is 1582-10-04 23:59:59; the
    /// switch itself is 1582-10-15; the three Julian leap days print as 29 February; the span's first
    /// instant is 1200-01-01; N1's natal instant is Julian 1300-03-15 12:00; Julian 1301-03-07 was a
    /// Tuesday and N1's 1301 return a Wednesday; a Gregorian instant prints as itself.
    /// </summary>
    public static TheoryData<string, string, string> Cases => new()
    {
        { "1582-10-14T23:59:59", "yyyy-MM-dd HH:mm:ss", "1582-10-04 23:59:59" },
        { "1582-10-15T00:00:00", "yyyy-MM-dd HH:mm:ss", "1582-10-15 00:00:00" },
        { "1300-03-08T11:12:20", "yyyy-MM-dd HH:mm:ss", "1300-02-29 11:12:20" },
        { "1400-03-09T04:16:00", "yyyy-MM-dd HH:mm:ss", "1400-02-29 04:16:00" },
        { "1500-03-10T06:21:53", "yyyy-MM-dd HH:mm:ss", "1500-02-29 06:21:53" },
        { "1200-01-08T04:54:46", "yyyy-MM-dd HH:mm:ss", "1200-01-01 04:54:46" },
        { "1300-03-23T12:00:00", "yyyy-MM-dd HH:mm:ss", "1300-03-15 12:00:00" },
        { "1301-03-15T00:00:00", "dddd, MMMM d, yyyy", "Tuesday, March 7, 1301" },
        { "1301-03-23T17:42:37", "ddd, dd MMM yyyy h:mm tt", "Wed, 15 Mar 1301 5:42 PM" },
        { "2026-04-15T12:00:00", "yyyy-MM-dd HH:mm:ss", "2026-04-15 12:00:00" },
    };

    /// <summary>
    /// <see cref="CalendarDateFormatter.Format(DateTime, string)"/> prints the clock on the calendar in
    /// force on its date, the weekday from the instant. Until 1.3.4 every instant before 1582-10-15
    /// printed on the proleptic Gregorian calendar, 7 to 10 days from the date in force.
    /// </summary>
    /// <param name="clock">The clock, proleptic ticks.</param>
    /// <param name="pattern">The pattern.</param>
    /// <param name="expected">The text.</param>
    [Theory]
    [MemberData(nameof(Cases))]
    public void Format_PrintsTheCalendarInForce(string clock, string pattern, string expected)
    {
        CalendarDateFormatter.Format(Clock(clock), pattern).Should().Be(expected);
    }

    /// <summary>
    /// <see cref="Format_PrintsTheCalendarInForce"/> under each of <see cref="CultureScope.OtherCultures"/>:
    /// the month and day names and the digits are invariant.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void Format_PrintsTheCalendarInForce_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        foreach (object[] row in Cases)
        {
            Format_PrintsTheCalendarInForce((string)row[0], (string)row[1], (string)row[2]);
        }
    }

    /// <summary>
    /// The named forms are the patterns they stand for: <see cref="CalendarDateFormatter.Date"/>
    /// <c>yyyy-MM-dd</c>, <see cref="CalendarDateFormatter.DateTime"/> <c>yyyy-MM-dd HH:mm:ss</c>,
    /// <see cref="CalendarDateFormatter.Minutes"/> <c>yyyy-MM-dd HH:mm</c>, and
    /// <see cref="CalendarDateFormatter.Format(DateOnly, string)"/> takes a <see cref="DateOnly"/>
    /// derived from an instant by its day number.
    /// </summary>
    [Fact]
    public void NamedForms_AreTheirPatterns()
    {
        DateTime n1Return = Clock("1301-03-23T17:42:37");

        CalendarDateFormatter.Date(n1Return).Should().Be("1301-03-15");
        CalendarDateFormatter.DateTime(n1Return).Should().Be("1301-03-15 17:42:37");
        CalendarDateFormatter.Minutes(n1Return).Should().Be("1301-03-15 17:42");
        CalendarDateFormatter.Format(DateOnly.FromDateTime(n1Return), "yyyy-MM-dd").Should().Be("1301-03-15");
        CalendarDateFormatter.Format(new DateOnly(2026, 4, 15), "dddd, MMMM d, yyyy").Should().Be("Wednesday, April 15, 2026");
    }

    /// <summary>
    /// <see cref="CalendarDateFormatter.Iso"/> is the <c>o</c> form on the same components, the text the
    /// JSON and the database hold (R-D3): N1's instant 1300-03-23T12:00:00Z writes
    /// "1300-03-15T12:00:00.0000000Z", a Utc kind with "Z" and an Unspecified kind without, and a
    /// Gregorian instant as <c>o</c> writes it.
    /// </summary>
    [Fact]
    public void Iso_IsTheRoundTripFormOnTheCalendarInForce()
    {
        CalendarDateFormatter.Iso(new DateTime(1300, 3, 23, 12, 0, 0, DateTimeKind.Utc))
            .Should().Be("1300-03-15T12:00:00.0000000Z");
        CalendarDateFormatter.Iso(new DateTime(1300, 3, 23, 12, 0, 0, DateTimeKind.Unspecified))
            .Should().Be("1300-03-15T12:00:00.0000000");

        DateTime gregorian = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc).AddTicks(1234567);
        CalendarDateFormatter.Iso(gregorian).Should().Be(gregorian.ToString("o", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// <see cref="CalendarDateFormatter.IsJulian"/> is true before 1582-10-15 00:00 and false from it;
    /// <see cref="CalendarDateFormatter.Reading"/> gives the clock on the calendar in force as a
    /// <see cref="DateTime"/> of <see cref="DateTimeKind.Unspecified"/> kind, the one label-valued
    /// <see cref="DateTime"/> (for <c>ChartInput.LocalDateTime</c>), and refuses a Julian leap day the
    /// type cannot hold (R-D5).
    /// </summary>
    [Fact]
    public void IsJulian_AndReading_FollowTheSwitch()
    {
        CalendarDateFormatter.IsJulian(Clock("1582-10-14T23:59:59")).Should().BeTrue();
        CalendarDateFormatter.IsJulian(Clock("1582-10-15T00:00:00")).Should().BeFalse();

        DateTime reading = CalendarDateFormatter.Reading(new DateTime(1300, 3, 23, 12, 0, 0, DateTimeKind.Utc));
        reading.Should().Be(new DateTime(1300, 3, 15, 12, 0, 0));
        reading.Kind.Should().Be(DateTimeKind.Unspecified);
        CalendarDateFormatter.Reading(new DateTime(2026, 4, 15, 12, 0, 0, DateTimeKind.Utc))
            .Should().Be(new DateTime(2026, 4, 15, 12, 0, 0));

        Action leapDay = () => CalendarDateFormatter.Reading(new DateTime(1300, 3, 8, 11, 12, 20, DateTimeKind.Utc));
        leapDay.Should().Throw<ArgumentOutOfRangeException>().WithMessage("*1300-02-29*");
    }

    /// <summary>
    /// <see cref="CalendarDateFormatter.ProlepticDay"/> reads a typed date to the day it denotes (D21; branch R3,
    /// 1.3.4, R-D7): on or after 1582-10-15 itself, on or before 1582-10-04 the Julian calendar's day as proleptic
    /// ticks (Tier B, r3_truth2.out): Julian 1582-09-25 is 1582-10-05, Julian 1582-10-04 is 1582-10-14, Julian
    /// 1300-02-27 is 1300-03-06, Julian 1330-03-15 is 1330-03-23.
    /// </summary>
    /// <param name="typed">The typed date.</param>
    /// <param name="proleptic">The day it denotes.</param>
    [Theory]
    [InlineData("1582-10-15", "1582-10-15")]
    [InlineData("1582-10-04", "1582-10-14")]
    [InlineData("1582-09-25", "1582-10-05")]
    [InlineData("1300-02-27", "1300-03-06")]
    [InlineData("1330-03-15", "1330-03-23")]
    public void ProlepticDay_ReadsTheTypedDateOnTheCalendarInForce(string typed, string proleptic)
    {
        CalendarDateFormatter.ProlepticDay(Day(typed)).Should().Be(Day(proleptic));
    }

    /// <summary>
    /// A typed date in 1582-10-05 to 1582-10-14 never occurred and is refused, with the sentence
    /// <c>ChartEngine.IsJulianDate</c> throws and the parameter named.
    /// </summary>
    /// <param name="typed">The typed date.</param>
    [Theory]
    [InlineData("1582-10-05")]
    [InlineData("1582-10-10")]
    [InlineData("1582-10-14")]
    public void ProlepticDay_RefusesTheTenDaysThatNeverOccurred(string typed)
    {
        Action act = () => CalendarDateFormatter.ProlepticDay(Day(typed));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage($"*{typed} does not exist: the Gregorian calendar began on 1582-10-15, the day after 1582-10-04 on the Julian calendar, so 1582-10-05 to 1582-10-14 never occurred.*")
            .And.ParamName.Should().Be("reading");
    }

    /// <summary>
    /// <see cref="CalendarDateFormatter.ProlepticClock"/> reads a typed clock the same way on its date, keeping the
    /// time of day and the kind: Julian 1582-10-04 23:59:59 is 1582-10-14 23:59:59, and a Gregorian clock is itself.
    /// </summary>
    /// <param name="typed">The typed clock, <c>yyyy-MM-ddTHH:mm:ss</c>.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="proleptic">The clock it denotes.</param>
    [Theory]
    [InlineData("1582-10-04T23:59:59", DateTimeKind.Unspecified, "1582-10-14T23:59:59")]
    [InlineData("2026-04-15T12:00:00", DateTimeKind.Utc, "2026-04-15T12:00:00")]
    public void ProlepticClock_ReadsTheTypedClockOnTheCalendarInForce_KeepingTimeAndKind(string typed, DateTimeKind kind, string proleptic)
    {
        DateTime reading = DateTime.SpecifyKind(DateTime.ParseExact(typed, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture), kind);

        DateTime clock = CalendarDateFormatter.ProlepticClock(reading);

        clock.Should().Be(DateTime.ParseExact(proleptic, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture));
        clock.Kind.Should().Be(kind);
    }

    /// <summary>A date from its <c>yyyy-MM-dd</c> text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The date.</returns>
    private static DateOnly Day(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A clock from its <c>yyyy-MM-ddTHH:mm:ss</c> text, proleptic ticks, Utc kind.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The clock.</returns>
    private static DateTime Clock(string text) =>
        DateTime.SpecifyKind(DateTime.ParseExact(text, "yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture), DateTimeKind.Utc);
}
