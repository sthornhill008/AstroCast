using System.Globalization;

using AstroCast.Core.Interfaces;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates the Chaldean planetary hour at a given moment.
/// Requires sunrise and sunset times to divide daylight and nighttime
/// into 12 equal hours each, then assigns rulers in the Chaldean order
/// starting from the day ruler (determined by the day of the week).
/// </summary>
/// <remarks>
/// The Chaldean "day" runs from sunrise to the next sunrise.
/// Daytime (sunrise to sunset) is divided into 12 equal planetary hours.
/// Nighttime (sunset to next sunrise) is divided into 12 equal planetary hours.
/// The 1st daytime hour is ruled by the day's planetary ruler.
/// Subsequent hours follow the Chaldean order: Saturn, Jupiter, Mars, Sun,
/// Venus, Mercury, Moon (repeating).
/// </remarks>
public sealed class PlanetaryHourCalculator
{
    /// <summary>
    /// The Chaldean planetary order, starting with Saturn.
    /// Each subsequent hour advances one position in this sequence.
    /// </summary>
    private static readonly string[] ChaldeanOrder =
        ["Saturn", "Jupiter", "Mars", "Sun", "Venus", "Mercury", "Moon"];

    /// <summary>
    /// Maps each day of the week to its planetary ruler.
    /// The ruler of the day governs the 1st daytime planetary hour.
    /// </summary>
    private static readonly Dictionary<DayOfWeek, string> DayRulers = new()
    {
        [DayOfWeek.Sunday] = "Sun",
        [DayOfWeek.Monday] = "Moon",
        [DayOfWeek.Tuesday] = "Mars",
        [DayOfWeek.Wednesday] = "Mercury",
        [DayOfWeek.Thursday] = "Jupiter",
        [DayOfWeek.Friday] = "Venus",
        [DayOfWeek.Saturday] = "Saturn",
    };

    private readonly IEphemerisService _ephemeris;
    private readonly ITimeZoneRules _timeZoneRules;

    /// <summary>
    /// Initialises a new instance of <see cref="PlanetaryHourCalculator"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for sunrise/sunset and Julian Day calculations.</param>
    /// <param name="timeZoneRules">
    /// The time-zone rules instants are read on a zone's clock with. Optional: when omitted,
    /// the IANA data the app carries (<see cref="TzdbTimeZoneRules"/>).
    /// </param>
    public PlanetaryHourCalculator(IEphemerisService ephemeris, ITimeZoneRules? timeZoneRules = null)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
        _timeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules();
    }

    /// <summary>
    /// Calculates the planetary hour at a given Julian Day for a location.
    /// </summary>
    /// <param name="julianDay">Julian Day (UT) to evaluate.</param>
    /// <param name="latitude">Geographic latitude (north positive).</param>
    /// <param name="longitude">Geographic longitude (east positive).</param>
    /// <param name="timeZoneId">IANA timezone for local time conversion.</param>
    /// <returns>The planetary hour at the given moment.</returns>
    /// <remarks>
    /// The zone's clock is read on the app's own IANA data, to the second, so a chart from
    /// before the zone's first transition reads its hour on local mean time.
    /// </remarks>
    public PlanetaryHour Calculate(
        double julianDay, double latitude, double longitude,
        string timeZoneId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(timeZoneId);

        return Calculate(julianDay, latitude, longitude,
            jd => JulianDayUtility.ToLocalDateTime(_ephemeris, jd, _timeZoneRules, timeZoneId));
    }

    /// <summary>
    /// Calculates the planetary hour at a given Julian Day for a location, on the clock of a
    /// fixed UTC offset rather than a time zone's: the clock of a chart cast with an explicit
    /// offset. The sunrise's weekday, which picks the day ruler, and the hour's start and end
    /// are read on that clock.
    /// </summary>
    /// <param name="julianDay">Julian Day (UT) to evaluate.</param>
    /// <param name="latitude">Geographic latitude (north positive).</param>
    /// <param name="longitude">Geographic longitude (east positive).</param>
    /// <param name="utcOffsetHours">The clock's offset from UTC in hours (e.g. −6 for UTC−6).</param>
    /// <returns>The planetary hour at the given moment, its start and end on that clock.</returns>
    public PlanetaryHour CalculateAtFixedOffset(
        double julianDay, double latitude, double longitude,
        double utcOffsetHours)
    {
        return Calculate(julianDay, latitude, longitude,
            jd => DateTime.SpecifyKind(
                JulianDayUtility.ToDateTimeUtc(_ephemeris, jd).AddHours(utcOffsetHours),
                DateTimeKind.Unspecified));
    }

    /// <summary>
    /// Calculates the planetary hour at a given Julian Day for a location, reading the
    /// sunrise's weekday and the hour's start and end on the clock <paramref name="toClock"/>
    /// gives.
    /// </summary>
    /// <param name="julianDay">Julian Day (UT) to evaluate.</param>
    /// <param name="latitude">Geographic latitude (north positive).</param>
    /// <param name="longitude">Geographic longitude (east positive).</param>
    /// <param name="toClock">Converts a Julian Day (UT) to a reading on the chart's clock.</param>
    /// <returns>The planetary hour at the given moment.</returns>
    private PlanetaryHour Calculate(
        double julianDay, double latitude, double longitude,
        Func<double, DateTime> toClock)
    {
        // The Chaldean day that contains the instant: the latest sunrise at or before it, the
        // sunset after that sunrise, and the next sunrise. Each is found directly and checked
        // against the instant; the instant is in the day if it falls before that sunset.
        (double todaySunrise, double todaySunset, double nightEnd) = FindChaldeanDay(
            julianDay, latitude, longitude);

        bool isDaytime = julianDay < todaySunset;

        // Determine the day of the week from the sunrise that started this Chaldean day.
        // The Chaldean day is identified by the local date of sunrise.
        DayOfWeek dayOfWeek = toClock(todaySunrise).DayOfWeek;

        // Get the day ruler and its position in the Chaldean order
        string dayRuler = DayRulers[dayOfWeek];
        int dayRulerIndex = Array.IndexOf(ChaldeanOrder, dayRuler);

        // Calculate hour number (1–12) and hour boundaries
        int hourNumber;
        double hourStartJd;
        double hourEndJd;

        if (isDaytime)
        {
            double daytimeTotalDuration = todaySunset - todaySunrise;
            if (daytimeTotalDuration <= 0.0)
            {
                throw new InvalidOperationException(
                    "Cannot calculate planetary hours: daytime duration is zero or negative. " +
                    "This may occur at extreme latitudes with circumpolar conditions where " +
                    "sunrise and sunset times are equal or invalid.");
            }

            double daytimeHourDuration = daytimeTotalDuration / 12.0;
            hourNumber = Math.Clamp(
                (int)((julianDay - todaySunrise) / daytimeHourDuration) + 1, 1, 12);
            hourStartJd = todaySunrise + (hourNumber - 1) * daytimeHourDuration;
            hourEndJd = todaySunrise + hourNumber * daytimeHourDuration;
        }
        else
        {
            double nighttimeTotalDuration = nightEnd - todaySunset;
            if (nighttimeTotalDuration <= 0.0)
            {
                throw new InvalidOperationException(
                    "Cannot calculate planetary hours: nighttime duration is zero or negative. " +
                    "This may occur at extreme latitudes with circumpolar conditions.");
            }

            double nighttimeHourDuration = nighttimeTotalDuration / 12.0;
            hourNumber = Math.Clamp(
                (int)((julianDay - todaySunset) / nighttimeHourDuration) + 1, 1, 12);
            hourStartJd = todaySunset + (hourNumber - 1) * nighttimeHourDuration;
            hourEndJd = todaySunset + hourNumber * nighttimeHourDuration;
        }

        // Calculate the ruling planet.
        // Daytime hours: offset 0–11 from the day ruler.
        // Nighttime hours: offset 12–23 from the day ruler (continue the sequence).
        int totalHourOffset = isDaytime ? (hourNumber - 1) : (12 + hourNumber - 1);
        int rulerIndex = (dayRulerIndex + totalHourOffset) % 7;
        string rulingPlanet = ChaldeanOrder[rulerIndex];

        // Convert hour boundaries to local time
        DateTime hourStartLocal = toClock(hourStartJd);
        DateTime hourEndLocal = toClock(hourEndJd);

        return new PlanetaryHour
        {
            RulingPlanet = rulingPlanet,
            HourNumber = hourNumber,
            IsDaytime = isDaytime,
            HourStart = hourStartLocal,
            HourEnd = hourEndLocal,
            HourStartUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, hourStartJd),
            HourEndUtc = JulianDayUtility.ToDateTimeUtc(_ephemeris, hourEndJd),
        };
    }

    /// <summary>
    /// How far after a sunrise the search for the next events starts: one second, in days.
    /// The library returns the first event after its start, and a search started on an
    /// event can find that event again.
    /// </summary>
    private const double SearchMargin = 1.0 / 86400.0;

    /// <summary>
    /// The most days the search steps back looking for a sunrise at or before the instant.
    /// Wherever the Sun rises at all, consecutive sunrises are about a day apart.
    /// </summary>
    private const int MaxDaysBack = 3;

    /// <summary>
    /// Finds the Chaldean day that contains <paramref name="julianDay"/>: the latest sunrise
    /// at or before it, the sunset after that sunrise, and the next sunrise after it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// No search assumes a time of day. Taking the first sunrise after one day earlier lands
    /// on the previous day's sunrise for as long after a sunrise as sunrise came earlier than
    /// the day before (up to 90 s at Richmond), and the first sunset after the instant can
    /// skip a sunset a few minutes ahead at high latitude (up to 291 s at 64° N in June).
    /// </para>
    /// <para>
    /// So each search starts well away from the event it looks for — one day back, or just
    /// after the previous sunrise — and the events found are checked against the instant:
    /// sunrise ≤ instant &lt; next sunrise, and sunrise &lt; sunset &lt; next sunrise.
    /// Where the Sun does not rise or set, the ephemeris service's exception propagates.
    /// </para>
    /// </remarks>
    /// <param name="julianDay">Julian Day (UT) to place.</param>
    /// <param name="latitude">Geographic latitude (north positive).</param>
    /// <param name="longitude">Geographic longitude (east positive).</param>
    /// <returns>The day's sunrise, its sunset and the next sunrise, as Julian Days (UT).</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no sunrise at or before the instant is found within
    /// <see cref="MaxDaysBack"/> days, or when the events found do not bracket the instant.
    /// </exception>
    private (double Sunrise, double Sunset, double NextSunrise) FindChaldeanDay(
        double julianDay, double latitude, double longitude)
    {
        // A sunrise at or before the instant: the first sunrise after a start one day back,
        // moving the start back a day at a time while that sunrise is after the instant.
        double searchFrom = julianDay - 1.0;
        (double sunrise, _) = _ephemeris.CalculateSunriseSunset(searchFrom, latitude, longitude);
        for (int daysBack = 1; sunrise > julianDay; daysBack++)
        {
            if (daysBack > MaxDaysBack)
            {
                throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                    $"Cannot calculate planetary hours: no sunrise found at or before JD {julianDay:F6} " +
                    $"within {MaxDaysBack} days."));
            }

            searchFrom -= 1.0;
            (sunrise, _) = _ephemeris.CalculateSunriseSunset(searchFrom, latitude, longitude);
        }

        // Then the latest one: from just after a sunrise the search gives the next sunrise and
        // that day's sunset; move on while that next sunrise is still at or before the instant.
        (double nextSunrise, double sunset) = _ephemeris.CalculateSunriseSunset(
            sunrise + SearchMargin, latitude, longitude);
        while (nextSunrise <= julianDay)
        {
            sunrise = nextSunrise;
            (nextSunrise, sunset) = _ephemeris.CalculateSunriseSunset(
                sunrise + SearchMargin, latitude, longitude);
        }

        if (!(sunrise <= julianDay && julianDay < nextSunrise && sunrise < sunset && sunset < nextSunrise))
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture,
                $"Cannot calculate planetary hours: the sunrise (JD {sunrise:F6}), sunset (JD {sunset:F6}) " +
                $"and next sunrise (JD {nextSunrise:F6}) found do not bracket JD {julianDay:F6}."));
        }

        return (sunrise, sunset, nextSunrise);
    }
}
