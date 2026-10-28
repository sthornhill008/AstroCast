using System.Collections.Concurrent;
using System.Globalization;
using System.Text;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Exceptions;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Interop;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Provides astronomical ephemeris calculations by wrapping the Swiss Ephemeris
/// native library via P/Invoke. Computes planetary positions, house cusps,
/// Julian Day conversions, and related astronomical data.
/// </summary>
/// <remarks>
/// This service initialises the Swiss Ephemeris on construction and releases
/// resources on disposal. All access to the native Swiss Ephemeris library is
/// serialised via an internal <see cref="SemaphoreSlim"/> to prevent concurrent
/// access to the library's global internal state, which is not thread-safe.
/// </remarks>
public sealed class EphemerisService : IEphemerisService
{
    /// <summary>Default calculation flags: Swiss Ephemeris mode + high-precision speed.</summary>
    private const int DefaultFlags =
        (int)(CalculationFlag.SwissEphemeris | CalculationFlag.Speed);

    /// <summary>Flags for equatorial coordinate calculation.</summary>
    private const int EquatorialFlags =
        (int)(CalculationFlag.SwissEphemeris | CalculationFlag.Speed | CalculationFlag.Equatorial);

    /// <summary>Default house system character: Placidus (ASCII 80 = 'P').</summary>
    private const int DefaultHouseSystem = 'P';

    /// <summary>All celestial bodies to calculate in <see cref="CalculateAllPositions"/>.</summary>
    private static readonly CelestialBodyId[] AllBodies =
    [
        CelestialBodyId.Sun,
        CelestialBodyId.Moon,
        CelestialBodyId.Mercury,
        CelestialBodyId.Venus,
        CelestialBodyId.Mars,
        CelestialBodyId.Jupiter,
        CelestialBodyId.Saturn,
        CelestialBodyId.Uranus,
        CelestialBodyId.Neptune,
        CelestialBodyId.Pluto,
        CelestialBodyId.TrueNode,
        CelestialBodyId.Chiron,
    ];

    /// <summary>
    /// Serialises access to the Swiss Ephemeris native library, which uses global
    /// internal state and is not thread-safe. All <c>swe_*</c> P/Invoke calls must
    /// be wrapped in this lock.
    /// </summary>
    private readonly SemaphoreSlim _ephemerisLock = new(1, 1);

    /// <summary>
    /// Thread-safe cache for planet display names. Each body's name is immutable
    /// in the Swiss Ephemeris, so caching avoids repeated P/Invoke + StringBuilder
    /// allocations.
    /// </summary>
    private static readonly ConcurrentDictionary<CelestialBodyId, string> s_planetNameCache = new();

    /// <summary>Reusable buffer for ecliptic coordinate results (guarded by <see cref="_ephemerisLock"/>).</summary>
    private readonly double[] _calcBuffer = new double[6];

    /// <summary>Reusable buffer for equatorial coordinate results (guarded by <see cref="_ephemerisLock"/>).</summary>
    private readonly double[] _eqBuffer = new double[6];

    /// <summary>Reusable error buffer for P/Invoke calls (guarded by <see cref="_ephemerisLock"/>).</summary>
    private readonly StringBuilder _errorBuffer = new(SwissEphInterop.MaxErrorLength);

    private readonly ILogger _logger;
    private bool _disposed;

    /// <summary>
    /// Initialises a new instance of <see cref="EphemerisService"/> and sets the
    /// ephemeris data file path.
    /// </summary>
    /// <param name="ephemerisPath">
    /// Absolute or relative path to the directory containing the .se1 ephemeris data files.
    /// </param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="ephemerisPath"/> is null or empty.
    /// </exception>
    /// <exception cref="DirectoryNotFoundException">
    /// Thrown when the specified ephemeris directory does not exist.
    /// </exception>
    public EphemerisService(string ephemerisPath, ILogger logger)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ephemerisPath);
        ArgumentNullException.ThrowIfNull(logger);

        string fullPath = Path.GetFullPath(ephemerisPath);
        if (!Directory.Exists(fullPath))
        {
            throw new DirectoryNotFoundException(
                $"Ephemeris data directory not found: {fullPath}");
        }

        _logger = logger.ForContext<EphemerisService>();

        _ephemerisLock.Wait();
        try
        {
            SwissEphInterop.swe_set_ephe_path(fullPath);
        }
        finally
        {
            _ephemerisLock.Release();
        }

        _logger.Debug("Swiss Ephemeris initialised with data path: {EphemerisPath}", fullPath);
    }

    /// <inheritdoc />
    public string Version
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            StringBuilder buffer = new(SwissEphInterop.MaxErrorLength);

            _ephemerisLock.Wait();
            try
            {
                SwissEphInterop.swe_version(buffer);
            }
            finally
            {
                _ephemerisLock.Release();
            }

            return buffer.ToString();
        }
    }

    /// <inheritdoc />
    public PlanetPosition CalculatePosition(double julianDayUt, CelestialBodyId body)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Calculate ecliptic coordinates using reusable buffers
        double longitude, latitude, distance, speedLon, speedLat, speedDist;
        string eclipticWarning;

        _ephemerisLock.Wait();
        try
        {
            _errorBuffer.Clear();
            int result = SwissEphInterop.swe_calc_ut(
                julianDayUt, (int)body, DefaultFlags, _calcBuffer, _errorBuffer);

            if (result < 0)
            {
                string errorMessage = _errorBuffer.ToString();
                _logger.Error(
                    "Swiss Ephemeris error calculating {Body} at JD {JulianDay}: {Error}",
                    body, julianDayUt, errorMessage);
                throw new EphemerisCalculationException(
                    $"Failed to calculate {body} at JD {julianDayUt}: {errorMessage}");
            }

            eclipticWarning = _errorBuffer.ToString();
            longitude = _calcBuffer[0];
            latitude = _calcBuffer[1];
            distance = _calcBuffer[2];
            speedLon = _calcBuffer[3];
            speedLat = _calcBuffer[4];
            speedDist = _calcBuffer[5];
        }
        finally
        {
            _ephemerisLock.Release();
        }

        if (!string.IsNullOrEmpty(eclipticWarning))
        {
            _logger.Warning(
                "Swiss Ephemeris warning for {Body} at JD {JulianDay}: {Warning}",
                body, julianDayUt, eclipticWarning);
        }

        // Calculate equatorial coordinates (right ascension, declination)
        double? rightAscension = null;
        double? declination = null;

        _ephemerisLock.Wait();
        try
        {
            _errorBuffer.Clear();
            int eqResult = SwissEphInterop.swe_calc_ut(
                julianDayUt, (int)body, EquatorialFlags, _eqBuffer, _errorBuffer);

            if (eqResult >= 0)
            {
                rightAscension = _eqBuffer[0];
                declination = _eqBuffer[1];

                string eqWarning = _errorBuffer.ToString();
                if (!string.IsNullOrEmpty(eqWarning))
                {
                    _logger.Warning(
                        "Swiss Ephemeris warning for {Body} equatorial at JD {JulianDay}: {Warning}",
                        body, julianDayUt, eqWarning);
                }
            }
            else
            {
                _logger.Warning(
                    "Failed to calculate equatorial coordinates for {Body} at JD {JulianDay}: {Error}",
                    body, julianDayUt, _errorBuffer.ToString());
            }
        }
        finally
        {
            _ephemerisLock.Release();
        }

        // Get the planet name (cached)
        string name = GetPlanetName(body);

        PlanetPosition position = new()
        {
            Body = body,
            Name = name,
            Longitude = longitude,
            Latitude = latitude,
            Distance = distance,
            SpeedInLongitude = speedLon,
            SpeedInLatitude = speedLat,
            SpeedInDistance = speedDist,
            RightAscension = rightAscension,
            Declination = declination,
        };

        _logger.Debug(
            "Calculated {Body}: {Position} {Direction} {Speed:F3}\u00b0/day",
            body, PositionFormatter.FormatPosition(position), position.DirectionCode,
            position.SpeedInLongitude);

        return position;
    }

    /// <inheritdoc />
    public PlanetPosition CalculatePositionLongitudeOnly(double julianDayUt, CelestialBodyId body)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Single P/Invoke call: ecliptic coordinates only (no equatorial)
        double longitude, latitude, distance, speedLon, speedLat, speedDist;

        _ephemerisLock.Wait();
        try
        {
            _errorBuffer.Clear();
            int result = SwissEphInterop.swe_calc_ut(
                julianDayUt, (int)body, DefaultFlags, _calcBuffer, _errorBuffer);

            if (result < 0)
            {
                string errorMessage = _errorBuffer.ToString();
                _logger.Error(
                    "Swiss Ephemeris error calculating {Body} at JD {JulianDay}: {Error}",
                    body, julianDayUt, errorMessage);
                throw new EphemerisCalculationException(
                    $"Failed to calculate {body} at JD {julianDayUt}: {errorMessage}");
            }

            longitude = _calcBuffer[0];
            latitude = _calcBuffer[1];
            distance = _calcBuffer[2];
            speedLon = _calcBuffer[3];
            speedLat = _calcBuffer[4];
            speedDist = _calcBuffer[5];
        }
        finally
        {
            _ephemerisLock.Release();
        }

        string name = GetPlanetName(body);

        return new PlanetPosition
        {
            Body = body,
            Name = name,
            Longitude = longitude,
            Latitude = latitude,
            Distance = distance,
            SpeedInLongitude = speedLon,
            SpeedInLatitude = speedLat,
            SpeedInDistance = speedDist,
            RightAscension = null,
            Declination = null,
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<PlanetPosition> CalculateAllPositions(double julianDayUt)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        List<PlanetPosition> positions = new(AllBodies.Length);

        foreach (CelestialBodyId body in AllBodies)
        {
            PlanetPosition position = CalculatePosition(julianDayUt, body);
            positions.Add(position);
        }

        _logger.Debug("Calculated {Count} body positions at JD {JulianDay}",
            positions.Count, julianDayUt);

        return positions.AsReadOnly();
    }

    /// <inheritdoc />
    public double DateToJulianDay(int year, int month, int day, double hour,
                                  CalendarType calendar = CalendarType.Gregorian)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // Auto-detect Julian calendar for dates before the Gregorian reform (15 Oct 1582)
        if (calendar == CalendarType.Gregorian
            && (year < 1582 || (year == 1582 && (month < 10 || (month == 10 && day < 15)))))
        {
            calendar = CalendarType.Julian;
            _logger.Information(
                "Pre-Gregorian date {Year}-{Month:D2}-{Day:D2}: using Julian calendar",
                year, month, day);
        }

        double jd;
        _ephemerisLock.Wait();
        try
        {
            jd = SwissEphInterop.swe_julday(year, month, day, hour, (int)calendar);
        }
        finally
        {
            _ephemerisLock.Release();
        }

        _logger.Debug("Converted {Year}-{Month:D2}-{Day:D2} {Hour:F2}h to JD {JulianDay:F6}",
            year, month, day, hour, jd);

        return jd;
    }

    /// <inheritdoc />
    public (int Year, int Month, int Day, double Hour) JulianDayToDate(
        double julianDay, CalendarType calendar = CalendarType.Gregorian)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        int year, month, day;
        double hour;

        _ephemerisLock.Wait();
        try
        {
            SwissEphInterop.swe_revjul(julianDay, (int)calendar,
                out year, out month, out day, out hour);
        }
        finally
        {
            _ephemerisLock.Release();
        }

        _logger.Debug("Converted JD {JulianDay:F6} to {Year}-{Month:D2}-{Day:D2} {Hour:F2}h",
            julianDay, year, month, day, hour);

        return (year, month, day, hour);
    }

    /// <inheritdoc />
    public HouseData CalculateHouses(double julianDayUt, double latitude, double longitude,
        char houseSystem = 'P')
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        double[] cusps = new double[13];
        double[] ascmc = new double[10];

        int result;
        _ephemerisLock.Wait();
        try
        {
            result = SwissEphInterop.swe_houses(
                julianDayUt, latitude, longitude, (int)houseSystem, cusps, ascmc);
        }
        finally
        {
            _ephemerisLock.Release();
        }

        if (result < 0)
        {
            _logger.Error(
                "House calculation failed (system={HouseSystem}) for JD {JulianDay}, lat={Latitude}, lon={Longitude}",
                houseSystem, julianDayUt, latitude, longitude);
            throw new EphemerisCalculationException(
                $"House calculation (system '{houseSystem}') failed for latitude {latitude:F4}\u00b0. " +
                "This typically occurs at extreme latitudes (above approximately 66\u00b0) with " +
                "time-based house systems (Placidus, Koch). Try Equal or Whole Sign houses.");
        }

        // Extract cusps[1..12] into a list (skip cusps[0] which is unused)
        List<double> cuspList = new(12);
        for (int i = 1; i <= 12; i++)
        {
            cuspList.Add(cusps[i]);
        }

        // The obliquity and latitude tell day from night inside the polar circles (ChartSect.IsDayChart).
        double obliquity = GetObliquity(julianDayUt);

        HouseData houseData = new()
        {
            Cusps = cuspList.AsReadOnly(),
            Ascendant = ascmc[0],
            Midheaven = ascmc[1],
            Armc = ascmc[2],
            Vertex = ascmc[3],
            Obliquity = obliquity,
            GeographicLatitude = latitude,
        };

        ValidateHouseCuspOrder(cuspList);

        _logger.Debug(
            "Calculated houses (system={HouseSystem}) for JD {JulianDay}, lat={Latitude}, lon={Longitude}",
            houseSystem, julianDayUt, latitude, longitude);

        return houseData;
    }

    /// <inheritdoc />
    public double GetObliquity(double julianDayUt)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        double[] xx = new double[6];
        StringBuilder serr = new(SwissEphInterop.MaxErrorLength);

        int result;
        _ephemerisLock.Wait();
        try
        {
            result = SwissEphInterop.swe_calc_ut(
                julianDayUt, SwissEphInterop.SE_ECL_NUT, DefaultFlags, xx, serr);
        }
        finally
        {
            _ephemerisLock.Release();
        }

        if (result < 0)
        {
            string errorMessage = serr.ToString();
            _logger.Error(
                "Swiss Ephemeris error calculating obliquity at JD {JulianDay}: {Error}",
                julianDayUt, errorMessage);
            throw new EphemerisCalculationException(
                $"Failed to calculate obliquity at JD {julianDayUt}: {errorMessage}");
        }

        // Check for warnings even on success
        string warning = serr.ToString();
        if (!string.IsNullOrEmpty(warning))
        {
            _logger.Warning(
                "Swiss Ephemeris warning for obliquity at JD {JulianDay}: {Warning}",
                julianDayUt, warning);
        }

        double obliquity = xx[0];

        _logger.Debug("Obliquity at JD {JulianDay}: {Obliquity:F6}\u00b0",
            julianDayUt, obliquity);

        return obliquity;
    }

    /// <summary>
    /// The value <c>swe_rise_trans()</c> returns when the body does not rise or set:
    /// "function return value -2 means that the body does not rise or set"
    /// (<c>lib/swisseph/src/swecl.c</c>, Swiss Ephemeris 2.10.03). An error is <c>-1</c>.
    /// </summary>
    private const int RiseTransNotFound = -2;

    /// <inheritdoc />
    public (double SunriseJd, double SunsetJd) CalculateSunriseSunset(
        double julianDayUt, double latitude, double longitude)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        // geopos array: [longitude, latitude, altitude]
        // Note: Swiss Ephemeris uses longitude first, then latitude
        double[] geopos = [longitude, latitude, 0.0];
        StringBuilder serr = new(SwissEphInterop.MaxErrorLength);

        double sunriseJd = 0.0;
        int riseResult;
        _ephemerisLock.Wait();
        try
        {
            riseResult = SwissEphInterop.swe_rise_trans(
                julianDayUt, SwissEphInterop.SE_SUN, null,
                (int)CalculationFlag.SwissEphemeris,
                SwissEphInterop.SE_CALC_RISE | SwissEphInterop.SE_BIT_DISC_CENTER,
                geopos, 1013.25, 15.0, ref sunriseJd, serr);
        }
        finally
        {
            _ephemerisLock.Release();
        }

        if (riseResult == RiseTransNotFound)
        {
            _logger.Warning(
                "Sunrise does not occur at JD {JulianDay}, lat={Latitude} (circumpolar conditions)",
                julianDayUt, latitude);
            throw new SunDoesNotRiseOrSetException(
                string.Create(CultureInfo.InvariantCulture,
                    $"Sunrise does not occur at latitude {latitude:F4}\u00b0 on the requested date. ") +
                "This location experiences continuous daylight or darkness (circumpolar conditions). " +
                "Planetary hour calculations are not available for this date and location.");
        }

        if (riseResult < 0)
        {
            string errorMessage = serr.ToString();
            _logger.Error(
                "Sunrise calculation failed at JD {JulianDay}, lat={Latitude}, lon={Longitude}: {Error}",
                julianDayUt, latitude, longitude, errorMessage);
            throw new EphemerisCalculationException(
                $"Sunrise calculation failed: {errorMessage}");
        }

        double sunsetJd = 0.0;
        int setResult;
        _ephemerisLock.Wait();
        try
        {
            setResult = SwissEphInterop.swe_rise_trans(
                julianDayUt, SwissEphInterop.SE_SUN, null,
                (int)CalculationFlag.SwissEphemeris,
                SwissEphInterop.SE_CALC_SET | SwissEphInterop.SE_BIT_DISC_CENTER,
                geopos, 1013.25, 15.0, ref sunsetJd, serr);
        }
        finally
        {
            _ephemerisLock.Release();
        }

        if (setResult == RiseTransNotFound)
        {
            _logger.Warning(
                "Sunset does not occur at JD {JulianDay}, lat={Latitude} (circumpolar conditions)",
                julianDayUt, latitude);
            throw new SunDoesNotRiseOrSetException(
                string.Create(CultureInfo.InvariantCulture,
                    $"Sunset does not occur at latitude {latitude:F4}\u00b0 on the requested date. ") +
                "This location experiences continuous daylight or darkness (circumpolar conditions). " +
                "Planetary hour calculations are not available for this date and location.");
        }

        if (setResult < 0)
        {
            string errorMessage = serr.ToString();
            _logger.Error(
                "Sunset calculation failed at JD {JulianDay}, lat={Latitude}, lon={Longitude}: {Error}",
                julianDayUt, latitude, longitude, errorMessage);
            throw new EphemerisCalculationException(
                $"Sunset calculation failed: {errorMessage}");
        }

        _logger.Debug(
            "Calculated sunrise/sunset for JD {JulianDay}, lat={Latitude}, lon={Longitude}: " +
            "rise={SunriseJd:F6}, set={SunsetJd:F6}",
            julianDayUt, latitude, longitude, sunriseJd, sunsetJd);

        return (sunriseJd, sunsetJd);
    }

    /// <inheritdoc />
    public HouseData CalculateHousesFromArmc(double armc, double latitude, double obliquity,
        char houseSystem = 'P')
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        double[] cusps = new double[13];
        double[] ascmc = new double[10];

        int result;
        _ephemerisLock.Wait();
        try
        {
            result = SwissEphInterop.swe_houses_armc(
                armc, latitude, obliquity, (int)houseSystem, cusps, ascmc);
        }
        finally
        {
            _ephemerisLock.Release();
        }

        if (result < 0)
        {
            _logger.Error(
                "House calculation from ARMC failed (system={HouseSystem}): ARMC={Armc}, lat={Latitude}, eps={Obliquity}",
                houseSystem, armc, latitude, obliquity);
            throw new EphemerisCalculationException(
                $"House calculation (system '{houseSystem}') from ARMC failed for latitude {latitude:F4}\u00b0. " +
                "This typically occurs at extreme latitudes (above approximately 66\u00b0) with " +
                "time-based house systems. Try Equal or Whole Sign houses.");
        }

        // Extract cusps[1..12] into a list (skip cusps[0] which is unused)
        List<double> cuspList = new(12);
        for (int i = 1; i <= 12; i++)
        {
            cuspList.Add(cusps[i]);
        }

        HouseData houseData = new()
        {
            Cusps = cuspList.AsReadOnly(),
            Ascendant = ascmc[0],
            Midheaven = ascmc[1],
            Armc = ascmc[2],
            Vertex = ascmc[3],
            Obliquity = obliquity,
            GeographicLatitude = latitude,
        };

        _logger.Debug(
            "Calculated houses (system={HouseSystem}) from ARMC={Armc}, lat={Latitude}, eps={Obliquity}",
            houseSystem, armc, latitude, obliquity);

        return houseData;
    }

    /// <inheritdoc />
    public double? CalculateRiseTransit(double julianDayUt, CelestialBodyId body,
                                        int rsmiFlag, double latitude, double longitude,
                                        string? starName = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        double[] geopos = [longitude, latitude, 0.0];
        StringBuilder serr = new(SwissEphInterop.MaxErrorLength);
        int ipl = starName is not null ? -1 : (int)body;
        StringBuilder? starBuffer = starName is not null ? new StringBuilder(starName, 64) : null;

        double tret = 0.0;
        int result;
        _ephemerisLock.Wait();
        try
        {
            result = SwissEphInterop.swe_rise_trans(
                julianDayUt, ipl, starBuffer,
                (int)CalculationFlag.SwissEphemeris,
                rsmiFlag, geopos, 1013.25, 15.0, ref tret, serr);
        }
        finally
        {
            _ephemerisLock.Release();
        }

        if (result < 0)
        {
            string errorMessage = serr.ToString();
            string bodyDesc = starName ?? body.ToString();
            _logger.Error(
                "Rise/transit calculation failed for {Body} (flag={Flag}) at JD {JulianDay}, " +
                "lat={Latitude}, lon={Longitude}: {Error}",
                bodyDesc, rsmiFlag, julianDayUt, latitude, longitude, errorMessage);
            throw new EphemerisCalculationException(
                $"Rise/transit calculation failed for {bodyDesc}: {errorMessage}");
        }

        if (result == 1)
        {
            string bodyDesc = starName ?? body.ToString();
            _logger.Debug(
                "Rise/transit event does not occur for {Body} (flag={Flag}) at JD {JulianDay}, " +
                "lat={Latitude} (circumpolar conditions)",
                bodyDesc, rsmiFlag, julianDayUt, latitude);
            return null;
        }

        return tret;
    }

    /// <inheritdoc />
    public (int Flags, double[] Tret) FindNextSolarEclipse(double jdStart)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        double[] tret = new double[10];
        int flags;

        _ephemerisLock.Wait();
        try
        {
            _errorBuffer.Clear();
            flags = SwissEphInterop.swe_sol_eclipse_when_glob(
                jdStart, SwissEphInterop.SEFLG_SWIEPH, 0, tret, 0, _errorBuffer);

            if (flags < 0)
            {
                string error = _errorBuffer.ToString();
                _logger.Error("Solar eclipse search error at JD {JD}: {Error}", jdStart, error);
            }
        }
        finally
        {
            _ephemerisLock.Release();
        }

        return (flags, tret);
    }

    /// <inheritdoc />
    public (int Flags, double[] Tret) FindNextLunarEclipse(double jdStart)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        double[] tret = new double[10];
        int flags;

        _ephemerisLock.Wait();
        try
        {
            _errorBuffer.Clear();
            flags = SwissEphInterop.swe_lun_eclipse_when(
                jdStart, SwissEphInterop.SEFLG_SWIEPH, 0, tret, 0, _errorBuffer);

            if (flags < 0)
            {
                string error = _errorBuffer.ToString();
                _logger.Error("Lunar eclipse search error at JD {JD}: {Error}", jdStart, error);
            }
        }
        finally
        {
            _ephemerisLock.Release();
        }

        return (flags, tret);
    }

    #region Coordinate Transformation

    /// <inheritdoc />
    public (double RightAscension, double Declination) EclipticToEquatorial(
        double eclipticLongitude, double eclipticLatitude, double obliquity)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        double[] xpo = [eclipticLongitude, eclipticLatitude, 1.0];
        double[] xpn = new double[3];

        // Swiss Ephemeris convention (from swephlib.c comments on swe_cotrans_sp):
        //   "for ecl. to equ. eps must be negative"
        //   "for equ. to ecl. eps must be positive"
        // Internal Swiss Ephemeris calls such as swe_cotrans(xra, xra, -eps_true)
        // in swecl.c confirm this — ecliptic-to-equatorial uses negative obliquity.
        _ephemerisLock.Wait();
        try
        {
            SwissEphInterop.swe_cotrans(xpo, xpn, -obliquity);
        }
        finally
        {
            _ephemerisLock.Release();
        }

        // xpn[0] = Right Ascension in degrees (may be negative, normalise to 0-360)
        double ra = ((xpn[0] % 360.0) + 360.0) % 360.0;
        double dec = xpn[1];

        _logger.Debug(
            "Ecliptic ({Lon:F4}\u00b0, {Lat:F4}\u00b0) -> Equatorial (RA {Ra:F4}\u00b0, Dec {Dec:F4}\u00b0) with eps={Eps:F4}\u00b0",
            eclipticLongitude, eclipticLatitude, ra, dec, obliquity);

        return (ra, dec);
    }

    #endregion

    #region Sidereal Time

    /// <inheritdoc />
    public double GetSiderealTimeDegrees(double julianDayUt)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        double sidtimeHours;

        _ephemerisLock.Wait();
        try
        {
            sidtimeHours = SwissEphInterop.swe_sidtime(julianDayUt);
        }
        finally
        {
            _ephemerisLock.Release();
        }

        // Convert hours (0-24) to degrees (0-360)
        double sidtimeDegrees = sidtimeHours * 15.0;

        _logger.Debug(
            "Sidereal time at JD {JulianDay}: {Hours:F6}h = {Degrees:F4}\u00b0",
            julianDayUt, sidtimeHours, sidtimeDegrees);

        return sidtimeDegrees;
    }

    #endregion

    /// <inheritdoc />
    public double CalculateFixedStarLongitude(string starName, double julianDayUt) =>
        FixedStarCoordinates(starName, julianDayUt)[0];

    /// <inheritdoc />
    public (double Longitude, double Latitude) CalculateFixedStarPosition(string starName, double julianDayUt)
    {
        double[] xx = FixedStarCoordinates(starName, julianDayUt);
        return (xx[0], xx[1]);
    }

    /// <summary>
    /// A fixed star's apparent ecliptic coordinates of date from one <c>swe_fixstar2_ut</c> call: longitude,
    /// latitude and distance, then their speeds. <see cref="CalculateFixedStarLongitude"/> reads the longitude
    /// alone, as it always has; <see cref="CalculateFixedStarPosition"/> the longitude and the latitude (branch
    /// Primary, 1.5.0, for the stars' right ascension and declination).
    /// </summary>
    /// <param name="starName">The star's name in sefstars.txt.</param>
    /// <param name="julianDayUt">Julian Day number in Universal Time.</param>
    /// <returns>The six coordinates the library returns.</returns>
    /// <exception cref="EphemerisCalculationException">Thrown when the star is not found or the calculation fails.</exception>
    private double[] FixedStarCoordinates(string starName, double julianDayUt)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(starName);

        // swe_fixstar2_ut modifies the star buffer in place, writing back the full
        // star record. Buffer must be at least 41 chars (SE_MAX_STNAME * 2 + 1).
        StringBuilder starBuffer = new(starName, 64);
        double[] xx = new double[6];

        _ephemerisLock.Wait();
        try
        {
            _errorBuffer.Clear();
            int result = SwissEphInterop.swe_fixstar2_ut(
                starBuffer, julianDayUt, SwissEphInterop.SEFLG_SWIEPH,
                xx, _errorBuffer);

            if (result < 0)
            {
                string errorMessage = _errorBuffer.ToString();
                _logger.Error(
                    "Swiss Ephemeris error calculating fixed star '{StarName}' at JD {JulianDay}: {Error}",
                    starName, julianDayUt, errorMessage);
                throw new EphemerisCalculationException(
                    $"Failed to calculate fixed star '{starName}' at JD {julianDayUt}: {errorMessage}");
            }

            return xx;
        }
        finally
        {
            _ephemerisLock.Release();
        }
    }

    /// <summary>
    /// Gets the display name for a celestial body from the Swiss Ephemeris.
    /// Results are cached in a static <see cref="ConcurrentDictionary{TKey,TValue}"/>
    /// since planet names are immutable.
    /// </summary>
    /// <param name="body">The celestial body identifier.</param>
    /// <returns>
    /// The body name as returned by the Swiss Ephemeris, two of them normalised: "True Node" for the true node
    /// (the library says "true Node") and "Lilith" for the mean apogee (the library says "mean Apogee";
    /// branch Asteroids, 1.6.0).
    /// </returns>
    private string GetPlanetName(CelestialBodyId body)
    {
        return s_planetNameCache.GetOrAdd(body, b =>
        {
            StringBuilder buffer = new(SwissEphInterop.MaxErrorLength);

            _ephemerisLock.Wait();
            try
            {
                SwissEphInterop.swe_get_planet_name((int)b, buffer);
            }
            finally
            {
                _ephemerisLock.Release();
            }

            string name = buffer.ToString();

            // Swiss Ephemeris returns "true Node" for SE_TRUE_NODE — normalise to "True Node"
            if (b == CelestialBodyId.TrueNode)
            {
                name = "True Node";
            }

            // Swiss Ephemeris returns "mean Apogee" for SE_MEAN_APOG — the app names it "Lilith",
            // Black Moon Lilith, the mean lunar apogee (R1, branch Asteroids, 1.6.0)
            if (b == CelestialBodyId.MeanApogee)
            {
                name = "Lilith";
            }

            return name;
        });
    }

    /// <summary>
    /// Validates that house cusps are in proper ecliptic (zodiacal) order.
    /// Each cusp should be greater than or equal to the previous one, allowing
    /// for the 360°/0° wraparound. Logs a warning if cusps are out of order.
    /// </summary>
    /// <remarks>
    /// This is a diagnostic aid, not a hard error. It fires for Regiomontanus and
    /// Campanus inside the polar circles, where the ecliptic's upper-meridian point
    /// is below the horizon and their cusps run backwards through the zodiac;
    /// <see cref="HousePlacement"/> follows such cusps in their own direction.
    /// </remarks>
    /// <param name="cusps">The 12 house cusps in order (houses 1–12).</param>
    private void ValidateHouseCuspOrder(List<double> cusps)
    {
        for (int i = 1; i < cusps.Count; i++)
        {
            double previous = cusps[i - 1];
            double current = cusps[i];

            // Allow for the 360°/0° wraparound: if current < previous,
            // the cusp has crossed 0° Aries. This is normal when e.g.
            // house 12 is in Pisces and house 1 wraps to Aries.
            // We only flag it as a warning if the gap is suspiciously large
            // (more than 180° difference when not wrapping).
            double diff = current - previous;
            if (diff < 0)
            {
                diff += 360.0;
            }

            if (diff > 180.0)
            {
                _logger.Warning(
                    "House cusps may be out of order: cusp {Current} ({CurrentDeg:F2}\u00b0) " +
                    "precedes cusp {Next} ({NextDeg:F2}\u00b0) with a gap of {Gap:F2}\u00b0",
                    i, previous, i + 1, current, diff);
            }
        }
    }

    /// <summary>
    /// Releases Swiss Ephemeris resources by calling <c>swe_close()</c> and
    /// disposes the internal <see cref="SemaphoreSlim"/>.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            SwissEphInterop.swe_close();
            _ephemerisLock.Dispose();
            _logger.Debug("Swiss Ephemeris closed");
            _disposed = true;
        }
    }
}
