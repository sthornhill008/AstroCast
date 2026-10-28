using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.Interfaces;

/// <summary>
/// Service interface for astronomical ephemeris calculations.
/// Wraps the Swiss Ephemeris native library and provides a clean C# API
/// for computing planetary positions, Julian Day conversions, house cusps,
/// and related operations.
/// </summary>
/// <remarks>
/// Implementations must call <c>swe_set_ephe_path()</c> on initialisation
/// and <c>swe_close()</c> on disposal.
/// </remarks>
public interface IEphemerisService : IDisposable
{
    /// <summary>Gets the Swiss Ephemeris library version string.</summary>
    string Version { get; }

    /// <summary>
    /// Calculate the position of a single celestial body at a given Julian Day UT.
    /// Returns ecliptic coordinates (longitude, latitude, distance) and daily speeds,
    /// plus equatorial coordinates (right ascension, declination).
    /// </summary>
    /// <param name="julianDayUt">Julian Day number in Universal Time.</param>
    /// <param name="body">The celestial body to calculate.</param>
    /// <returns>The calculated position and motion data.</returns>
    /// <exception cref="AstroCast.Core.Exceptions.EphemerisCalculationException">
    /// Thrown when the Swiss Ephemeris returns an error for the given body or date.
    /// </exception>
    PlanetPosition CalculatePosition(double julianDayUt, CelestialBodyId body);

    /// <summary>
    /// Calculate a lightweight position for a single celestial body, returning only
    /// ecliptic longitude and speed in longitude. Skips the equatorial coordinate
    /// calculation, making it roughly 2× faster than <see cref="CalculatePosition"/>.
    /// </summary>
    /// <param name="julianDayUt">Julian Day number in Universal Time.</param>
    /// <param name="body">The celestial body to calculate.</param>
    /// <returns>
    /// A <see cref="PlanetPosition"/> with <see cref="PlanetPosition.Longitude"/> and
    /// <see cref="PlanetPosition.SpeedInLongitude"/> populated. Equatorial fields
    /// (<see cref="PlanetPosition.RightAscension"/>, <see cref="PlanetPosition.Declination"/>)
    /// are null. Other ecliptic fields (Latitude, Distance, etc.) are populated.
    /// </returns>
    /// <exception cref="AstroCast.Core.Exceptions.EphemerisCalculationException">
    /// Thrown when the Swiss Ephemeris returns an error for the given body or date.
    /// </exception>
    PlanetPosition CalculatePositionLongitudeOnly(double julianDayUt, CelestialBodyId body);

    /// <summary>
    /// Calculate positions of all configured celestial bodies (Sun through Chiron)
    /// at a given Julian Day UT.
    /// </summary>
    /// <param name="julianDayUt">Julian Day number in Universal Time.</param>
    /// <returns>A read-only list of position data for all 12 celestial bodies.</returns>
    /// <exception cref="AstroCast.Core.Exceptions.EphemerisCalculationException">
    /// Thrown when the Swiss Ephemeris returns an error for any body.
    /// </exception>
    IReadOnlyList<PlanetPosition> CalculateAllPositions(double julianDayUt);

    /// <summary>
    /// Convert a calendar date and time to Julian Day UT.
    /// </summary>
    /// <param name="year">Calendar year.</param>
    /// <param name="month">Month (1–12).</param>
    /// <param name="day">Day of month (1–31).</param>
    /// <param name="hour">Hour as decimal (e.g., 13.5 = 13:30 UT).</param>
    /// <param name="calendar">Calendar system to use. Default is Gregorian.</param>
    /// <returns>The Julian Day number in Universal Time.</returns>
    double DateToJulianDay(int year, int month, int day, double hour,
                           CalendarType calendar = CalendarType.Gregorian);

    /// <summary>
    /// Convert a Julian Day UT back to a calendar date and time.
    /// </summary>
    /// <param name="julianDay">Julian Day number in Universal Time.</param>
    /// <param name="calendar">Calendar system to use. Default is Gregorian.</param>
    /// <returns>A tuple containing the year, month, day, and decimal hour.</returns>
    (int Year, int Month, int Day, double Hour) JulianDayToDate(
        double julianDay, CalendarType calendar = CalendarType.Gregorian);

    /// <summary>
    /// Calculate house cusps and angles for a given time and location.
    /// </summary>
    /// <param name="julianDayUt">Julian Day in Universal Time.</param>
    /// <param name="latitude">Geographic latitude (north positive).</param>
    /// <param name="longitude">Geographic longitude (east positive).</param>
    /// <param name="houseSystem">House system character for Swiss Ephemeris (default 'P' = Placidus).</param>
    /// <returns>House cusp and angle data.</returns>
    HouseData CalculateHouses(double julianDayUt, double latitude, double longitude,
        char houseSystem = 'P');

    /// <summary>
    /// Get the true obliquity of the ecliptic for a given Julian Day UT.
    /// </summary>
    /// <param name="julianDayUt">Julian Day in Universal Time.</param>
    /// <returns>True obliquity in decimal degrees.</returns>
    double GetObliquity(double julianDayUt);

    /// <summary>
    /// Calculates sunrise and sunset for a given date and location.
    /// Required for planetary hour calculation.
    /// </summary>
    /// <param name="julianDayUt">Julian day number (UT) for the date.</param>
    /// <param name="latitude">Geographic latitude (north positive).</param>
    /// <param name="longitude">Geographic longitude (east positive).</param>
    /// <returns>Tuple of Julian day numbers for sunrise and sunset.</returns>
    /// <exception cref="AstroCast.Core.Exceptions.SunDoesNotRiseOrSetException">
    /// Thrown when the Sun does not rise or does not set at the place within the
    /// library's search after the instant (the polar day or night: <c>swe_rise_trans</c>
    /// returns −2). It derives from <see cref="AstroCast.Core.Exceptions.EphemerisCalculationException"/>.
    /// </exception>
    /// <exception cref="AstroCast.Core.Exceptions.EphemerisCalculationException">
    /// Thrown if sunrise/sunset cannot be calculated because the library reports an error.
    /// </exception>
    (double SunriseJd, double SunsetJd) CalculateSunriseSunset(
        double julianDayUt, double latitude, double longitude);

    /// <summary>
    /// Calculate house cusps and angles from a pre-computed ARMC
    /// (sidereal time in degrees), geographic latitude, and obliquity.
    /// Used for composite charts where house cusps are derived from the
    /// composite MC rather than a specific moment in time, and for the
    /// progressed Ascendant, cast for the progressed Midheaven's sidereal time.
    /// </summary>
    /// <param name="armc">ARMC (sidereal time) in degrees (0–360).</param>
    /// <param name="latitude">Geographic latitude (north positive).</param>
    /// <param name="obliquity">True obliquity of the ecliptic in degrees.</param>
    /// <param name="houseSystem">House system character for Swiss Ephemeris (default 'P' = Placidus).</param>
    /// <returns>House cusp and angle data.</returns>
    HouseData CalculateHousesFromArmc(double armc, double latitude, double obliquity,
        char houseSystem = 'P');

    /// <summary>
    /// Calculate the next rise, set, upper meridian transit (MC), or lower meridian
    /// transit (IC) of a celestial body at a given geographic location.
    /// </summary>
    /// <param name="julianDayUt">Julian Day number (UT) to search from.</param>
    /// <param name="body">The celestial body to calculate. Use any value when
    /// <paramref name="starName"/> is non-null (the star name takes precedence).</param>
    /// <param name="rsmiFlag">Rise/set/transit flag composed from SE_CALC_RISE,
    /// SE_CALC_SET, SE_CALC_MTRANSIT, SE_CALC_ITRANSIT, and optionally
    /// SE_BIT_DISC_CENTER.</param>
    /// <param name="latitude">Geographic latitude (north positive).</param>
    /// <param name="longitude">Geographic longitude (east positive).</param>
    /// <param name="starName">Optional fixed star name. When non-null, the body
    /// parameter is ignored and the star is looked up in sefstars.txt.</param>
    /// <returns>
    /// The Julian Day (UT) of the event, or <c>null</c> if the event does not occur
    /// (circumpolar conditions for rise/set at extreme latitudes).
    /// </returns>
    /// <exception cref="AstroCast.Core.Exceptions.EphemerisCalculationException">
    /// Thrown when the Swiss Ephemeris returns a calculation error (result &lt; 0).
    /// </exception>
    double? CalculateRiseTransit(double julianDayUt, CelestialBodyId body,
                                 int rsmiFlag, double latitude, double longitude,
                                 string? starName = null);

    /// <summary>
    /// Find the next solar eclipse globally after a given Julian Day.
    /// Thread-safe wrapper around <c>swe_sol_eclipse_when_glob</c>.
    /// </summary>
    /// <param name="jdStart">Julian Day to start searching from.</param>
    /// <returns>
    /// A tuple of (flags, tret) where flags contains SE_ECL_* type bits
    /// (negative on error), and tret is a 10-element array of phase times:
    /// tret[0]=maximum, tret[2]=first contact, tret[3]=last contact,
    /// tret[4]=totality begin, tret[5]=totality end.
    /// </returns>
    (int Flags, double[] Tret) FindNextSolarEclipse(double jdStart);

    /// <summary>
    /// Find the next lunar eclipse after a given Julian Day.
    /// Thread-safe wrapper around <c>swe_lun_eclipse_when</c>.
    /// </summary>
    /// <param name="jdStart">Julian Day to start searching from.</param>
    /// <returns>
    /// A tuple of (flags, tret) where flags contains SE_ECL_* type bits
    /// (negative on error), and tret is a 10-element array of phase times:
    /// tret[0]=maximum, tret[2]=partial begin, tret[3]=partial end,
    /// tret[4]=totality begin, tret[5]=totality end,
    /// tret[6]=penumbral begin, tret[7]=penumbral end.
    /// </returns>
    (int Flags, double[] Tret) FindNextLunarEclipse(double jdStart);

    /// <summary>
    /// Calculate the ecliptic longitude of a fixed star for a given Julian Day UT.
    /// Uses <c>swe_fixstar2_ut()</c> to get the star's tropical zodiac position
    /// with proper precession correction for the requested date.
    /// </summary>
    /// <param name="starName">
    /// Star name to look up in sefstars.txt (e.g., "Regulus", "Algol", "Spica").
    /// Some stars require alternate names (e.g., "Deneb Kaitos" for Difda,
    /// "Deneb,alCyg" for Deneb Adige).
    /// </param>
    /// <param name="julianDayUt">Julian Day number in Universal Time.</param>
    /// <returns>Ecliptic longitude in decimal degrees (0-360).</returns>
    /// <exception cref="AstroCast.Core.Exceptions.EphemerisCalculationException">
    /// Thrown when the star is not found in sefstars.txt or calculation fails.
    /// </exception>
    double CalculateFixedStarLongitude(string starName, double julianDayUt);

    /// <summary>
    /// Calculate the ecliptic longitude and latitude of a fixed star for a given Julian Day UT, from the same
    /// <c>swe_fixstar2_ut()</c> call as <see cref="CalculateFixedStarLongitude"/>: the star's apparent place of
    /// date, with its latitude, so that its right ascension and declination follow by
    /// <see cref="EclipticToEquatorial"/> (branch Primary, 1.5.0).
    /// </summary>
    /// <param name="starName">Star name to look up in sefstars.txt, as for <see cref="CalculateFixedStarLongitude"/>.</param>
    /// <param name="julianDayUt">Julian Day number in Universal Time.</param>
    /// <returns>The ecliptic longitude in decimal degrees (0-360) and the ecliptic latitude in decimal degrees.</returns>
    /// <exception cref="AstroCast.Core.Exceptions.EphemerisCalculationException">
    /// Thrown when the star is not found in sefstars.txt or calculation fails.
    /// </exception>
    (double Longitude, double Latitude) CalculateFixedStarPosition(string starName, double julianDayUt);

    /// <summary>
    /// Converts ecliptic coordinates to equatorial coordinates using the obliquity.
    /// Wraps <c>swe_cotrans</c>, passing the obliquity with the sign required
    /// by the Swiss Ephemeris convention (negated internally so callers pass the
    /// positive obliquity as returned by <see cref="GetObliquity"/>).
    /// </summary>
    /// <param name="eclipticLongitude">Ecliptic longitude in degrees (0-360).</param>
    /// <param name="eclipticLatitude">Ecliptic latitude in degrees.</param>
    /// <param name="obliquity">True obliquity of the ecliptic in degrees (positive, as from <see cref="GetObliquity"/>).</param>
    /// <returns>
    /// Tuple of (Right Ascension in degrees 0-360, Declination in degrees -90 to +90).
    /// </returns>
    (double RightAscension, double Declination) EclipticToEquatorial(
        double eclipticLongitude, double eclipticLatitude, double obliquity);

    /// <summary>
    /// Calculates Greenwich Apparent Sidereal Time (GAST) for a given Julian Day UT.
    /// Wraps <c>swe_sidtime</c>.
    /// </summary>
    /// <param name="julianDayUt">Julian Day number in Universal Time.</param>
    /// <returns>Sidereal time in degrees (0-360). Multiply hours by 15 to convert.</returns>
    double GetSiderealTimeDegrees(double julianDayUt);
}
