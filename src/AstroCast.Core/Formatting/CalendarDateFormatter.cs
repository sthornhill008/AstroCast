using System.Globalization;
using System.Text;

namespace AstroCast.Core.Formatting;

/// <summary>
/// Prints an instant, or a clock derived from one, on the calendar in force on its date: the Julian
/// calendar before 1582-10-15, the Gregorian from it (branch R2, 1.3.4; rulings R-D1 to R-D4). The one
/// formatter every printed instant goes through: a chart's UTC time, an event's instant, a local clock
/// read from an instant through a zone or an explicit offset, a <see cref="DateOnly"/> derived from
/// an instant. A value formatter in the manner of <see cref="PositionFormatter"/>, so services may
/// call it.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="System.DateTime"/> always denotes an instant: proleptic Gregorian ticks, as .NET and
/// the time-zone data count them. A calendar applies in two places only: when a typed reading is read
/// (D21, <c>ChartEngine.ResolveInstant</c>) and when an instant is printed, here. Before 1582-10-15
/// the year, month and day are the Julian calendar's (<see cref="JulianCalendar"/>), so the three
/// Julian leap days print as 29 February of 1300, 1400 and 1500; the hours, minutes and seconds are
/// the clock's; the weekday is the clock's <see cref="System.DateTime.DayOfWeek"/>, the true weekday on
/// either calendar. The month and day names are English and the digits invariant, under every
/// culture. Until 1.3.4 every instant before the switch printed on the proleptic Gregorian calendar,
/// 7 to 10 days from the date in force, beside a natal Date/Time line read on the Julian calendar.
/// </para>
/// <para>
/// A typed reading (<c>ChartInput.LocalDateTime</c>, a picked <see cref="DateOnly"/> bound, a file
/// name's date, a status text of now) prints as typed and does not come here.
/// </para>
/// </remarks>
public static class CalendarDateFormatter
{
    /// <summary>Invariant culture used for every digit.</summary>
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    /// <summary>
    /// The header line an export prints when it holds an instant before 1582-10-15 (R-D2): directly after
    /// a chart's Date/Time line, or after the Period or Return Date/Time line of a report whose earliest
    /// instant is before the switch. Never in the JSON.
    /// </summary>
    internal const string JulianCalendarLine = "**Calendar:** dates before 1582-10-15 are on the Julian calendar";

    /// <summary>The first day of the Gregorian calendar as a <see cref="DateOnly"/>, for a picked bound.</summary>
    internal static readonly DateOnly GregorianCalendarStartDate = new(1582, 10, 15);

    /// <summary>The Julian calendar, for the components of an instant before the switch.</summary>
    private static readonly JulianCalendar Julian = new();

    /// <summary>
    /// The first instant of the Gregorian calendar, 1582-10-15 00:00, as proleptic ticks; the instant
    /// before it is 1582-10-04 23:59:59 on the Julian calendar.
    /// </summary>
    private static readonly System.DateTime GregorianCalendarStart = new(1582, 10, 15, 0, 0, 0, DateTimeKind.Unspecified);

    /// <summary>The English month names, for <c>MMMM</c> and <c>MMM</c>.</summary>
    private static readonly string[] MonthNames =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    ];

    /// <summary>The English day names, for <c>dddd</c> and <c>ddd</c>, indexed by <see cref="DayOfWeek"/>.</summary>
    private static readonly string[] DayNames =
    [
        "Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday",
    ];

    /// <summary>The tokens a pattern may hold, longest first where one begins another.</summary>
    private static readonly string[] Tokens =
    [
        "yyyy", "MMMM", "MMM", "MM", "dddd", "ddd", "dd", "d", "HH", "mm", "ss", "tt", "h",
    ];

    /// <summary>
    /// Whether a clock's date is before 1582-10-15, so that it prints on the Julian calendar.
    /// </summary>
    /// <param name="clock">The clock, as proleptic ticks; its kind is not read.</param>
    /// <returns>True before 1582-10-15 00:00, false from it.</returns>
    public static bool IsJulian(System.DateTime clock) => clock.Ticks < GregorianCalendarStart.Ticks;

    /// <summary>
    /// Prints a clock on the calendar in force on its date.
    /// </summary>
    /// <param name="clock">
    /// The clock, as proleptic ticks: an instant in UT, or a local clock derived from one. Its kind is
    /// not read.
    /// </param>
    /// <param name="pattern">
    /// The pattern: <c>yyyy</c>, <c>MM</c>, <c>dd</c>, <c>d</c>, <c>HH</c>, <c>mm</c>, <c>ss</c>,
    /// <c>h</c>, <c>tt</c>, <c>MMMM</c>, <c>MMM</c>, <c>dddd</c> and <c>ddd</c> as .NET reads them;
    /// everything else is literal.
    /// </param>
    /// <returns>The clock as the pattern lays it out.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pattern"/> is null.</exception>
    public static string Format(System.DateTime clock, string pattern)
    {
        ArgumentNullException.ThrowIfNull(pattern);

        (int year, int month, int day) = IsJulian(clock)
            ? (Julian.GetYear(clock), Julian.GetMonth(clock), Julian.GetDayOfMonth(clock))
            : (clock.Year, clock.Month, clock.Day);

        StringBuilder sb = new(pattern.Length + 8);
        int i = 0;
        while (i < pattern.Length)
        {
            string? token = null;
            foreach (string candidate in Tokens)
            {
                if (string.CompareOrdinal(pattern, i, candidate, 0, candidate.Length) == 0)
                {
                    token = candidate;
                    break;
                }
            }

            if (token is null)
            {
                sb.Append(pattern[i]);
                i++;
                continue;
            }

            sb.Append(token switch
            {
                "yyyy" => year.ToString("D4", Inv),
                "MMMM" => MonthNames[month - 1],
                "MMM" => MonthNames[month - 1][..3],
                "MM" => month.ToString("D2", Inv),
                "dddd" => DayNames[(int)clock.DayOfWeek],
                "ddd" => DayNames[(int)clock.DayOfWeek][..3],
                "dd" => day.ToString("D2", Inv),
                "d" => day.ToString(Inv),
                "HH" => clock.Hour.ToString("D2", Inv),
                "mm" => clock.Minute.ToString("D2", Inv),
                "ss" => clock.Second.ToString("D2", Inv),
                "tt" => clock.Hour < 12 ? "AM" : "PM",
                _ => (clock.Hour % 12 == 0 ? 12 : clock.Hour % 12).ToString(Inv),
            });
            i += token.Length;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Prints a <see cref="DateOnly"/> derived from an instant (its day number's date) on the calendar
    /// in force on that date.
    /// </summary>
    /// <param name="date">The date, as a day number on the proleptic Gregorian calendar.</param>
    /// <param name="pattern">The pattern, as <see cref="Format(System.DateTime, string)"/> reads it.</param>
    /// <returns>The date as the pattern lays it out.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="pattern"/> is null.</exception>
    public static string Format(DateOnly date, string pattern) =>
        Format(date.ToDateTime(TimeOnly.MinValue), pattern);

    /// <summary>The clock as <c>yyyy-MM-dd</c>.</summary>
    /// <param name="clock">The clock, as proleptic ticks.</param>
    /// <returns>The date on the calendar in force.</returns>
    public static string Date(System.DateTime clock) => Format(clock, "yyyy-MM-dd");

    /// <summary>The clock as <c>yyyy-MM-dd HH:mm:ss</c>.</summary>
    /// <param name="clock">The clock, as proleptic ticks.</param>
    /// <returns>The date on the calendar in force and the time to the second.</returns>
    public static string DateTime(System.DateTime clock) => Format(clock, "yyyy-MM-dd HH:mm:ss");

    /// <summary>The clock as <c>yyyy-MM-dd HH:mm</c>.</summary>
    /// <param name="clock">The clock, as proleptic ticks.</param>
    /// <returns>The date on the calendar in force and the time to the minute.</returns>
    public static string Minutes(System.DateTime clock) => Format(clock, "yyyy-MM-dd HH:mm");

    /// <summary>
    /// The clock in the round-trip form (<c>o</c>) on the same components,
    /// <c>yyyy-MM-ddTHH:mm:ss.fffffffZ</c>: the text the chart JSON and the database hold (R-D3), a
    /// Julian label before the switch, with "Z" for a <see cref="DateTimeKind.Utc"/> kind and nothing
    /// for another.
    /// </summary>
    /// <param name="clock">The clock, as proleptic ticks.</param>
    /// <returns>The round-trip text.</returns>
    public static string Iso(System.DateTime clock)
    {
        long fraction = clock.Ticks % TimeSpan.TicksPerSecond;
        string suffix = clock.Kind == DateTimeKind.Utc ? "Z" : "";
        return string.Create(Inv, $"{Format(clock, "yyyy-MM-ddTHH:mm:ss")}.{fraction:D7}{suffix}");
    }

    /// <summary>
    /// The day a typed date denotes (D21): on or after 1582-10-15 the date itself; on or before
    /// 1582-10-04 the Julian calendar's day as proleptic ticks (Julian 1582-09-25 is 1582-10-05, Julian
    /// 1300-02-27 is 1300-03-06); the ten dates between never occurred and are refused. The inverse of
    /// <see cref="Format(DateOnly, string)"/>, and with <see cref="ProlepticClock"/> the one place a
    /// scan's typed bound is read (branch R3, 1.3.4; R-D7): it is read here once, at its entry point, and
    /// every step after it is a proleptic day or an instant. Two kinds of typed date are read elsewhere:
    /// a chart's reading, which <c>ChartEngine.ResolveInstant</c> reads inline, and the dates
    /// <c>IEphemerisService.DateToJulianDay</c> reads on the calendar in force, the solar return's
    /// birthday, the ingress year bounds, a lunation's date and the mundane series' bounds. Until 1.3.4
    /// the scans stepped the typed date on
    /// the proleptic Gregorian calendar and read each day back through the calendar switch, so across
    /// October 1582 ten days were scanned twice and a Julian 29 February was skipped.
    /// </summary>
    /// <param name="reading">The typed date.</param>
    /// <returns>The day it denotes, as a day number on the proleptic Gregorian calendar.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the date is one of 1582-10-05 to 1582-10-14, which never occurred.
    /// </exception>
    public static DateOnly ProlepticDay(DateOnly reading)
    {
        if (reading >= GregorianCalendarStartDate)
        {
            return reading;
        }

        if (reading > GregorianCalendarStartDate.AddDays(-11))
        {
            throw new ArgumentOutOfRangeException(nameof(reading),
                string.Create(Inv,
                    $"{reading:yyyy-MM-dd} does not exist: the Gregorian calendar began on 1582-10-15, the day after 1582-10-04 on the Julian calendar, so 1582-10-05 to 1582-10-14 never occurred."));
        }

        return DateOnly.FromDateTime(Julian.ToDateTime(reading.Year, reading.Month, reading.Day, 0, 0, 0, 0));
    }

    /// <summary>
    /// The clock a typed clock denotes: <see cref="ProlepticDay"/> on its date, the time of day and the
    /// <see cref="System.DateTime.Kind"/> kept (a lunar-return range's bounds; branch R3, 1.3.4).
    /// </summary>
    /// <param name="reading">The typed clock.</param>
    /// <returns>The clock it denotes, as proleptic ticks of the same kind.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the date is one of 1582-10-05 to 1582-10-14, which never occurred.
    /// </exception>
    public static System.DateTime ProlepticClock(System.DateTime reading) =>
        System.DateTime.SpecifyKind(
            ProlepticDay(DateOnly.FromDateTime(reading)).ToDateTime(TimeOnly.FromTimeSpan(reading.TimeOfDay)),
            reading.Kind);

    /// <summary>
    /// The clock on the calendar in force as a <see cref="System.DateTime"/> holding those components,
    /// of <see cref="DateTimeKind.Unspecified"/> kind: the one label-valued <see cref="System.DateTime"/>,
    /// for <c>ChartInput.LocalDateTime</c> only, which the engine reads under D21 (a relocated chart's
    /// input, from the natal instant at offset zero).
    /// </summary>
    /// <param name="utc">The instant.</param>
    /// <returns>The reading.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the instant falls on 29 February of 1300, 1400 or 1500 on the Julian calendar, a
    /// date a <see cref="System.DateTime"/> cannot hold (R-D5).
    /// </exception>
    public static System.DateTime Reading(System.DateTime utc)
    {
        if (!IsJulian(utc))
        {
            return System.DateTime.SpecifyKind(utc, DateTimeKind.Unspecified);
        }

        int year = Julian.GetYear(utc);
        int month = Julian.GetMonth(utc);
        int day = Julian.GetDayOfMonth(utc);
        if (month == 2 && day == 29 && !System.DateTime.IsLeapYear(year))
        {
            throw new ArgumentOutOfRangeException(nameof(utc),
                string.Create(Inv, $"The instant falls on {year:D4}-02-29 UT, a leap day of the Julian calendar that a date cannot hold."));
        }

        return new System.DateTime(year, month, day, utc.Hour, utc.Minute, utc.Second, DateTimeKind.Unspecified)
            .AddTicks(utc.Ticks % TimeSpan.TicksPerSecond);
    }
}
