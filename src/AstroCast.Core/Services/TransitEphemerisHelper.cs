using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Provides ephemeris calculation helpers for transit date-range scanning.
/// Wraps the existing <see cref="IEphemerisService"/> for transit-specific use cases,
/// mapping <see cref="TransitBody"/> to Swiss Ephemeris body IDs and providing
/// convenience methods for position snapshots, daily motion, and date conversions.
/// </summary>
public sealed class TransitEphemerisHelper
{
    private readonly IEphemerisService _ephemerisService;

    /// <summary>
    /// Initialises a new instance of <see cref="TransitEphemerisHelper"/>.
    /// </summary>
    /// <param name="ephemerisService">The shared ephemeris service for Swiss Ephemeris calls.</param>
    public TransitEphemerisHelper(IEphemerisService ephemerisService)
    {
        ArgumentNullException.ThrowIfNull(ephemerisService);
        _ephemerisService = ephemerisService;
    }

    /// <summary>
    /// Gets the ecliptic longitude for a transit body at a specific date.
    /// Converts <see cref="TransitBody"/> to the Swiss Ephemeris planet ID and calls
    /// the existing position calculation.
    /// </summary>
    /// <param name="body">The transiting body.</param>
    /// <param name="julianDay">Julian day number (UT).</param>
    /// <returns>Longitude in degrees (0–360).</returns>
    public double GetLongitude(TransitBody body, double julianDay)
    {
        CelestialBodyId bodyId = TransitBodyToCelestialBodyId(body);
        PlanetPosition position = _ephemerisService.CalculatePositionLongitudeOnly(julianDay, bodyId);
        return position.Longitude;
    }

    /// <summary>
    /// Gets a full position snapshot for a transit body at a specific date.
    /// </summary>
    /// <param name="body">The transiting body.</param>
    /// <param name="julianDay">Julian day number (UT).</param>
    /// <returns><see cref="TransitPlanetPosition"/> with all fields populated.</returns>
    public TransitPlanetPosition GetPosition(TransitBody body, double julianDay)
    {
        CelestialBodyId bodyId = TransitBodyToCelestialBodyId(body);
        PlanetPosition position = _ephemerisService.CalculatePosition(julianDay, bodyId);

        return new TransitPlanetPosition
        {
            Longitude = position.Longitude,
            Sign = LongitudeToSign(position.Longitude),
            Degrees = LongitudeToDegrees(position.Longitude),
            Minutes = LongitudeToMinutes(position.Longitude),
            IsRetrograde = position.IsRetrograde,
            DailyMotion = position.SpeedInLongitude,
        };
    }

    /// <summary>
    /// Calculates the daily motion (speed) of a transit body at a date.
    /// Uses the speed output from swe_calc_ut (SpeedInLongitude).
    /// </summary>
    /// <param name="body">The transiting body.</param>
    /// <param name="julianDay">Julian day number (UT).</param>
    /// <returns>Daily motion in degrees per day (negative = retrograde).</returns>
    public double GetDailyMotion(TransitBody body, double julianDay)
    {
        CelestialBodyId bodyId = TransitBodyToCelestialBodyId(body);
        PlanetPosition position = _ephemerisService.CalculatePositionLongitudeOnly(julianDay, bodyId);
        return position.SpeedInLongitude;
    }

    /// <summary>
    /// Determines if a transit body is retrograde at a specific date.
    /// </summary>
    /// <param name="body">The transiting body.</param>
    /// <param name="julianDay">Julian day number (UT).</param>
    /// <returns>True if the body has negative daily motion (retrograde).</returns>
    public bool IsRetrograde(TransitBody body, double julianDay)
    {
        return GetDailyMotion(body, julianDay) < 0;
    }

    /// <summary>
    /// Converts a proleptic day to its Julian Day number (UT, noon) by arithmetic
    /// (<see cref="JulianDayUtility.InstantToJulianDay"/>; branch R3, 1.3.4): the one conversion the
    /// scanners' day loops, bounds and neighbours go through. The day is a proleptic day — a typed
    /// bound has come through <see cref="Formatting.CalendarDateFormatter.ProlepticDay"/> first — so no
    /// calendar switch is consulted; until 1.3.4 the components were read through it, and across
    /// October 1582 ten days were scanned twice.
    /// </summary>
    /// <param name="date">The proleptic day.</param>
    /// <returns>Julian Day number at noon UT on the given day.</returns>
    public double DateToJulianDay(DateOnly date)
    {
        return JulianDayUtility.InstantToJulianDay(date.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Utc));
    }

    /// <summary>
    /// Converts a UTC <see cref="DateTime"/> to a Julian Day number with sub-day precision.
    /// Used by binary search refinement for exact pass time calculation.
    /// </summary>
    /// <param name="utcDateTime">DateTime in UTC (Kind must be Utc).</param>
    /// <returns>Julian Day number with fractional day.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="utcDateTime"/> does not have <see cref="DateTimeKind.Utc"/>.
    /// </exception>
    public double DateTimeToJulianDay(DateTime utcDateTime)
    {
        if (utcDateTime.Kind != DateTimeKind.Utc)
            throw new ArgumentException("DateTime must be UTC.", nameof(utcDateTime));

        // By arithmetic (branch R3, 1.3.4), never through the calendar switch.
        return JulianDayUtility.InstantToJulianDay(utcDateTime);
    }

    /// <summary>
    /// Converts a Julian Day number to a UTC <see cref="DateTime"/>, rounded to the
    /// nearest second. Used by binary search refinement to convert the refined Julian
    /// Day back to a timestamp for transit entries, exits, exact passes, midpoint
    /// activations, stations and transit-to-transit aspects.
    /// </summary>
    /// <remarks>
    /// Delegates to <see cref="JulianDayUtility.ToDateTimeUtc"/>, the single
    /// conversion every timed event in Core goes through. Truncating each component
    /// instead printed a time up to a second early, and a whole minute early when the
    /// true seconds were 59.5 or more or when the instant fell on an exact minute
    /// whose Julian Day carries floating-point noise (01:00:00 UT decomposes as
    /// 0.9999999962747097 h and truncated to 00:59:59).
    /// </remarks>
    /// <param name="julianDay">Julian Day number in Universal Time.</param>
    /// <returns>DateTime in UTC (Kind = <see cref="DateTimeKind.Utc"/>), rounded to the nearest second.</returns>
    public DateTime JulianDayToDateTime(double julianDay) =>
        JulianDayUtility.ToDateTimeUtc(_ephemerisService, julianDay);

    /// <summary>
    /// Gets the declination of a transit body at a specific Julian Day.
    /// Uses the full position calculation (not longitude-only) to obtain
    /// equatorial coordinates including declination.
    /// </summary>
    /// <param name="body">The transiting body.</param>
    /// <param name="julianDay">Julian day number (UT).</param>
    /// <returns>Declination in decimal degrees (positive = north, negative = south), or null if unavailable.</returns>
    public double? GetDeclination(TransitBody body, double julianDay)
    {
        CelestialBodyId bodyId = TransitBodyToCelestialBodyId(body);
        PlanetPosition position = _ephemerisService.CalculatePosition(julianDay, bodyId);
        return position.Declination;
    }

    /// <summary>
    /// Converts a <see cref="TransitBody"/> enum to the corresponding
    /// <see cref="CelestialBodyId"/> value for Swiss Ephemeris lookups.
    /// </summary>
    /// <param name="body">The transit body to convert.</param>
    /// <returns>The corresponding <see cref="CelestialBodyId"/>.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="body"/> is not a recognised transit body.
    /// </exception>
    public static CelestialBodyId TransitBodyToCelestialBodyId(TransitBody body) => body switch
    {
        TransitBody.Jupiter => CelestialBodyId.Jupiter,
        TransitBody.Saturn => CelestialBodyId.Saturn,
        TransitBody.Uranus => CelestialBodyId.Uranus,
        TransitBody.Neptune => CelestialBodyId.Neptune,
        TransitBody.Pluto => CelestialBodyId.Pluto,
        TransitBody.Chiron => CelestialBodyId.Chiron,
        TransitBody.NorthNode => CelestialBodyId.TrueNode,
        TransitBody.Mars => CelestialBodyId.Mars,
        TransitBody.Mercury => CelestialBodyId.Mercury,
        TransitBody.Venus => CelestialBodyId.Venus,
        _ => throw new ArgumentOutOfRangeException(nameof(body), body, "Unknown transit body")
    };

    /// <summary>
    /// Converts a <see cref="TransitBody"/> enum to the corresponding Swiss Ephemeris
    /// planet constant integer (SE_JUPITER=5, SE_SATURN=6, etc.).
    /// </summary>
    /// <param name="body">The transit body to convert.</param>
    /// <returns>The Swiss Ephemeris integer constant.</returns>
    public static int TransitBodyToSwissEph(TransitBody body) =>
        (int)TransitBodyToCelestialBodyId(body);

    /// <summary>
    /// Converts a longitude (0–360) to a zodiac sign name string.
    /// </summary>
    /// <param name="longitude">Ecliptic longitude in degrees (0–360).</param>
    /// <returns>The zodiac sign name (e.g., "Aries", "Taurus").</returns>
    public static string LongitudeToSign(double longitude)
    {
        ZodiacSign sign = (ZodiacSign)((int)(longitude % 360.0 / 30.0) + 1);
        return sign.ToString();
    }

    /// <summary>
    /// Gets the degree within sign (0–29) from a longitude.
    /// </summary>
    /// <param name="longitude">Ecliptic longitude in degrees (0–360).</param>
    /// <returns>Degree within the zodiac sign (0–29).</returns>
    public static int LongitudeToDegrees(double longitude) =>
        (int)(longitude % 30.0);

    /// <summary>
    /// Gets the arc-minutes within degree (0–59) from a longitude.
    /// </summary>
    /// <param name="longitude">Ecliptic longitude in degrees (0–360).</param>
    /// <returns>Arc-minutes within the degree (0–59).</returns>
    public static int LongitudeToMinutes(double longitude) =>
        (int)((longitude % 1.0) * 60.0);
}
