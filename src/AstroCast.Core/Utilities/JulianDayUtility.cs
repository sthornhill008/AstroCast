using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Utilities;

/// <summary>
/// Shared utility for converting Julian Day numbers to <see cref="DateTime"/> values.
/// Centralises the fractional-hour decomposition that was previously duplicated
/// across five service classes.
/// </summary>
public static class JulianDayUtility
{
    /// <summary>
    /// Converts a Julian Day number to a UTC <see cref="DateTime"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service providing the JD-to-date conversion.</param>
    /// <param name="julianDay">Julian Day number in Universal Time.</param>
    /// <returns>
    /// A UTC <see cref="DateTime"/> with <see cref="DateTimeKind.Utc"/>, resolved to the
    /// nearest whole second.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="ephemeris"/> is null.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>Why the seconds are rounded rather than truncated.</b>
    /// <see cref="IEphemerisService.JulianDayToDate"/> returns the time of day as a
    /// fractional hour, and a Julian Day is a large double whose fractional part cannot
    /// represent every clock time exactly. A Julian Day built from 04:00 UT comes back as
    /// <c>3.9999999962747097</c> hours -- roughly thirteen microseconds early. Decomposing
    /// that by truncation promotes the error to a full second and yields 03:59:59.
    /// </para>
    /// <para>
    /// <b>04:00 is not a special value; the sign of the error is what matters.</b>
    /// Sweeping all 96 quarter-hour slots of 2026-04-20 through the round trip, only 32
    /// decompose bit-exactly -- the multiples of 45 minutes, 45 minutes being 1/32 of a
    /// day and therefore a dyadic fraction the double can hold. The other 64 all carry
    /// the identical magnitude, 13.411045074462891 microseconds, and differ only in sign:
    /// 32 come back early, and truncation loses a second on every one of them; 32 come
    /// back late, and truncation happens to survive. Nothing else distinguishes a grid
    /// value that bites from one that does not, which is why the whole grid is swept
    /// rather than one representative hour. Rounding is correct at all 96. Measured on
    /// Windows x64 and on macOS arm64, agreeing slot for slot including the 04:00 value
    /// above -- this is IEEE 754 arithmetic, not a platform trait.
    /// </para>
    /// <para>
    /// <b>The consequence was never cosmetic.</b> Grid times carry zero seconds, so a
    /// second rolled backwards crosses the <b>minute</b> boundary as well: a third of all
    /// grid-derived times -- the 32 early ones -- printed a minute early at the precision
    /// the exports actually display, :14 for :15 and :59 for :00. At a midnight instant
    /// the same lost second crosses a <b>date</b> boundary, so a moment at local midnight
    /// is attributed to the previous day: an electional candidate at the first instant of
    /// a requested range printed as the day before the range began. Rounding to the
    /// nearest second keeps the label on the correct side of both boundaries, and is well
    /// within the accuracy the ephemeris itself offers.
    /// </para>
    /// <para>
    /// <see cref="DateTime.AddSeconds(double)"/> is applied to midnight of the returned
    /// calendar date rather than composing hour, minute and second separately, so a value
    /// that rounds up to exactly 24:00:00 rolls into the next day instead of throwing.
    /// </para>
    /// </remarks>
    public static DateTime ToDateTimeUtc(IEphemerisService ephemeris, double julianDay)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);

        (int year, int month, int day, double hour) = ephemeris.JulianDayToDate(julianDay);

        return new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc)
            .AddSeconds(Math.Round(hour * 3600.0, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// The Julian Day (UT) of 0001-01-01 00:00 on the proleptic Gregorian calendar: the instant
    /// <see cref="DateTime"/> counts its ticks from.
    /// </summary>
    private const double JulianDayOfTickZero = 1721425.5;

    /// <summary>
    /// The Julian Day (UT) of 10000-01-01 00:00 on the proleptic Gregorian calendar, the first instant
    /// after the last a <see cref="DateTime"/> can hold.
    /// </summary>
    private const double JulianDayAfterLastTick = 5373484.5;

    /// <summary>
    /// Converts a Julian Day to the instant it denotes, to the nearest whole second, by arithmetic.
    /// </summary>
    /// <param name="julianDayUt">Julian Day number in Universal Time.</param>
    /// <returns>
    /// The instant, with <see cref="DateTimeKind.Utc"/>: its ticks count on the proleptic Gregorian
    /// calendar, as <see cref="DateTime"/>'s always do, whatever calendar was in force on its date.
    /// </returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when the Julian Day denotes no instant a <see cref="DateTime"/> can hold (before
    /// 0001-01-01 or after 9999-12-31), or is not a number.
    /// </exception>
    /// <remarks>
    /// <para>
    /// No calendar is consulted: the day is the Julian Day's whole days from tick zero, and the time
    /// 86,400 times its fraction past midnight. The second is rounded as <see cref="ToDateTimeUtc"/>
    /// rounds it (the nearest, halves away from zero), from the same fraction, so the two give the
    /// same <see cref="DateTime"/> for every Julian Day. They differ in what they promise:
    /// <see cref="ToDateTimeUtc"/> gives the label an event is printed with; this gives the instant
    /// a chart is cast at (D4).
    /// </para>
    /// <para>
    /// It is not <see cref="IEphemerisService.JulianDayToDate"/> with a calendar: before 1582-10-15
    /// the date it gives is a label that <see cref="IEphemerisService.DateToJulianDay"/> reads back on
    /// the Julian calendar, 7 to 10 days from the instant.
    /// </para>
    /// </remarks>
    public static DateTime JulianDayToInstant(double julianDayUt)
    {
        if (!(julianDayUt >= JulianDayOfTickZero && julianDayUt < JulianDayAfterLastTick))
        {
            throw new ArgumentOutOfRangeException(nameof(julianDayUt),
                "The Julian Day denotes no instant a DateTime can hold, 0001-01-01 to 9999-12-31 UT.");
        }

        double dayStart = Math.Floor(julianDayUt + 0.5);
        double hour = (julianDayUt - dayStart + 0.5) * 24.0;
        long days = (long)(dayStart - JulianDayOfTickZero - 0.5);

        return new DateTime(days * TimeSpan.TicksPerDay, DateTimeKind.Utc)
            .AddSeconds(Math.Round(hour * 3600.0, MidpointRounding.AwayFromZero));
    }

    /// <summary>
    /// Converts an instant to its Julian Day (UT) by arithmetic, from its whole seconds.
    /// </summary>
    /// <param name="instant">
    /// The instant, read as UTC whatever its <see cref="DateTime.Kind"/>. Its fraction of a second is
    /// not read, as the engine reads a wall clock's.
    /// </param>
    /// <returns>The Julian Day number in Universal Time.</returns>
    /// <remarks>
    /// <para>
    /// No calendar is consulted: the Julian Day of the instant's midnight is tick zero's plus its whole
    /// days, and the time of day is added as hours over 24. Before 1582-10-15 that is the instant's
    /// own Julian Day, where <see cref="IEphemerisService.DateToJulianDay"/> would read its proleptic
    /// Gregorian date as a Julian one, 7 to 10 days late.
    /// </para>
    /// <para>
    /// On or after 1582-10-15 it is the Julian Day <see cref="IEphemerisService.DateToJulianDay"/>
    /// gives for the instant's date and decimal hour, to the bit: the decimal hour is built as the
    /// engine builds it (hours, plus minutes over 60, plus seconds over 3,600), and
    /// <c>swe_julday</c> rounds once, adding that hour over 24 to a whole number of days, exactly as
    /// this does (measured on 400,000 instants across the files' span).
    /// </para>
    /// </remarks>
    public static double InstantToJulianDay(DateTime instant)
    {
        double decimalHour = instant.Hour + instant.Minute / 60.0 + instant.Second / 3600.0;

        return JulianDayOfTickZero + instant.Ticks / TimeSpan.TicksPerDay + decimalHour / 24.0;
    }

    /// <summary>
    /// Converts a Julian Day number to a local <see cref="DateTime"/> in the specified timezone.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service providing the JD-to-date conversion.</param>
    /// <param name="julianDay">Julian Day number in Universal Time.</param>
    /// <param name="timeZoneRules">The time-zone rules to read the instant with.</param>
    /// <param name="zoneId">Target timezone for local conversion.</param>
    /// <returns>
    /// The wall clock in the specified timezone, to the nearest second, with
    /// <see cref="DateTimeKind.Unspecified"/>.
    /// </returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="ephemeris"/> or <paramref name="timeZoneRules"/> is null.
    /// </exception>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when <paramref name="zoneId"/> names no zone the rules hold.
    /// </exception>
    public static DateTime ToLocalDateTime(
        IEphemerisService ephemeris, double julianDay, ITimeZoneRules timeZoneRules, string zoneId)
    {
        ArgumentNullException.ThrowIfNull(timeZoneRules);

        DateTime utc = ToDateTimeUtc(ephemeris, julianDay);
        return timeZoneRules.ToLocal(utc, zoneId).LocalDateTime;
    }

    /// <summary>
    /// Converts a local wall-clock <see cref="DateTime"/> in a given timezone to a
    /// Julian Day number in Universal Time: the zone's UTC instant, then
    /// <see cref="InstantToJulianDay"/> (branch R3, 1.3.4).
    /// </summary>
    /// <param name="ephemeris">
    /// Ephemeris service; kept for the callers' signatures, the Julian Day no longer reads a date
    /// through it.
    /// </param>
    /// <param name="localDateTime">
    /// Local wall-clock time, as proleptic ticks: a typed reading comes through
    /// <see cref="Formatting.CalendarDateFormatter.ProlepticClock"/> first. Any
    /// <see cref="DateTimeKind"/> is accepted; the value is reinterpreted as
    /// <see cref="DateTimeKind.Unspecified"/> before conversion, because it describes a reading on
    /// a clock in <paramref name="zoneId"/> rather than an instant.
    /// </param>
    /// <param name="timeZoneRules">The time-zone rules to convert with.</param>
    /// <param name="zoneId">The timezone the wall-clock time belongs to.</param>
    /// <returns>The Julian Day number (UT) of the instant that local time denotes.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="ephemeris"/> or <paramref name="timeZoneRules"/> is null.
    /// </exception>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when <paramref name="zoneId"/> names no zone the rules hold.
    /// </exception>
    /// <remarks>
    /// <para>
    /// <b>Why the conversion must go through the zone.</b>
    /// <see cref="IEphemerisService.DateToJulianDay"/> interprets its hour argument as
    /// <b>Universal Time</b>. Passing a local calendar date with hour <c>0.0</c> therefore
    /// does not produce local midnight: it produces the instant that is midnight in
    /// Greenwich, which in <c>America/New_York</c> is 20:00 the previous evening and in
    /// <c>Asia/Tokyo</c> is 09:00 the same morning. A scan window built that way is
    /// displaced by the location's UTC offset. Converting the wall-clock time through
    /// <paramref name="zoneId"/> first, and only then handing UT components to the
    /// ephemeris, is what makes the resulting Julian Day denote the intended local instant.
    /// </para>
    /// <para>
    /// <b>Why each bound must convert independently.</b> A range spanning a daylight-saving
    /// transition has a different UTC offset at each end. Resolving one offset and applying
    /// it to both bounds displaces the window <i>and</i> resizes it. Each bound is therefore
    /// converted on its own date, through the zone's rules for that date.
    /// </para>
    /// <para>
    /// <b>DST edge cases.</b> This is the same conversion, by the same rule (D17), as
    /// <c>ChartEngine.ConvertInputToJulianDay</c>, so a boundary instant and a chart cast for
    /// the same wall-clock time resolve identically:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Repeated</b> (clocks going back; the local time occurs twice) resolves to the
    /// later of the two instants.
    /// </description></item>
    /// <item><description>
    /// <b>Skipped</b> (clocks going forward; the local time never occurs) resolves to the
    /// first instant after the gap.
    /// </description></item>
    /// </list>
    /// <para>
    /// Both cases are reachable here even though the callers ask for midnight, because some
    /// zones transition at midnight rather than at 02:00.
    /// </para>
    /// </remarks>
    public static double LocalDateTimeToJulianDay(
        IEphemerisService ephemeris, DateTime localDateTime, ITimeZoneRules timeZoneRules, string zoneId)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(timeZoneRules);

        ZoneInstant resolved = timeZoneRules.ToUniversal(localDateTime, zoneId);

        return InstantToJulianDay(resolved.UtcDateTime);
    }

    /// <summary>
    /// Converts local midnight at the start of a calendar date to a Julian Day number in
    /// Universal Time.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service providing the date-to-JD conversion.</param>
    /// <param name="localDate">The local calendar date whose 00:00 boundary is wanted.</param>
    /// <param name="timeZoneRules">The time-zone rules to convert with.</param>
    /// <param name="zoneId">The timezone the calendar date belongs to.</param>
    /// <returns>The Julian Day number (UT) of local midnight opening that date.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="ephemeris"/> or <paramref name="timeZoneRules"/> is null.
    /// </exception>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when <paramref name="zoneId"/> names no zone the rules hold.
    /// </exception>
    /// <remarks>
    /// This is the boundary a user-facing date picker denotes. A range of calendar dates
    /// running inclusively from <c>first</c> to <c>last</c> is the half-open instant interval
    /// <c>[LocalMidnightToJulianDay(first), LocalMidnightToJulianDay(last.AddDays(1)))</c>,
    /// which covers the whole of <c>last</c> through to its final second.
    /// See <see cref="LocalDateTimeToJulianDay"/> for the conversion's rationale and its
    /// daylight-saving resolution policy. Where midnight itself repeats this is its later
    /// instant (D17, the rule for a reading); the first instant of such a date is
    /// <see cref="LocalDayStartToJulianDay"/>'s.
    /// </remarks>
    public static double LocalMidnightToJulianDay(
        IEphemerisService ephemeris, DateOnly localDate, ITimeZoneRules timeZoneRules, string zoneId)
    {
        return LocalDateTimeToJulianDay(
            ephemeris, localDate.ToDateTime(TimeOnly.MinValue), timeZoneRules, zoneId);
    }

    /// <summary>
    /// Converts the start of a local calendar date — the first instant its midnight reads — to a
    /// Julian Day number in Universal Time: the zone's UTC instant, then
    /// <see cref="InstantToJulianDay"/> (branch R3, 1.3.4).
    /// </summary>
    /// <param name="ephemeris">
    /// Ephemeris service; kept for the callers' signatures, the Julian Day no longer reads a date
    /// through it.
    /// </param>
    /// <param name="localDate">
    /// The local day whose first instant is wanted, as a proleptic day: a typed date comes through
    /// <see cref="Formatting.CalendarDateFormatter.ProlepticDay"/> first.
    /// </param>
    /// <param name="timeZoneRules">The time-zone rules to convert with.</param>
    /// <param name="zoneId">The timezone the calendar date belongs to.</param>
    /// <returns>The Julian Day number (UT) of the first instant of that date.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="ephemeris"/> or <paramref name="timeZoneRules"/> is null.
    /// </exception>
    /// <exception cref="TimeZoneNotFoundException">
    /// Thrown when <paramref name="zoneId"/> names no zone the rules hold.
    /// </exception>
    /// <remarks>
    /// <para>
    /// A day's edge, not a reading. Where the clocks go back at midnight, so that 00:00 occurs
    /// twice, the date begins at the <b>earlier</b> instant: the hour between the two midnights
    /// belongs to this date, not to the one before (Havana, 1 November 2026: 00:00 CDT, 04:00 UT,
    /// not 00:00 CST). Where the clocks go forward at midnight, so that 00:00 never occurs, the
    /// date begins at the first instant after the gap, as <see cref="LocalMidnightToJulianDay"/>
    /// has it. Everywhere else the two agree, to the bit.
    /// </para>
    /// <para>
    /// A range of calendar dates running inclusively from <c>first</c> to <c>last</c> is the
    /// half-open instant interval <c>[LocalDayStartToJulianDay(first),
    /// LocalDayStartToJulianDay(last.AddDays(1)))</c>, which holds every instant whose reading
    /// falls on one of its dates and no other. D17 (a repeated reading is the later instant)
    /// stays the rule for a chart's reading.
    /// </para>
    /// </remarks>
    public static double LocalDayStartToJulianDay(
        IEphemerisService ephemeris, DateOnly localDate, ITimeZoneRules timeZoneRules, string zoneId)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(timeZoneRules);

        ZoneInstant midnight = timeZoneRules.ToUniversal(localDate.ToDateTime(TimeOnly.MinValue), zoneId);
        DateTime utc = midnight.Mapping == LocalTimeMapping.Repeated && midnight.EarlierUtcDateTime is DateTime earlier
            ? earlier
            : midnight.UtcDateTime;

        return InstantToJulianDay(utc);
    }
}
