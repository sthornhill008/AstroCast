using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Tests.Fixtures;

/// <summary>
/// An <see cref="IEphemerisService"/> double whose daily speed in longitude is
/// a polynomial in time for each configured body:
/// <c>v(t) = v₀ + a·(t − t₀) + c·(t − t₀)²</c>. It lets station tests state a
/// speed, an acceleration and, optionally, a curvature directly, which the real
/// ephemeris cannot, and so express every row of the speed-sign × speed-trend
/// truth table — including a speed of exactly zero at the reference instant
/// and a body whose speed never changes sign.
/// </summary>
/// <remarks>
/// <para>
/// Only the members a station search touches are implemented:
/// <see cref="CalculatePosition"/>, <see cref="CalculatePositionLongitudeOnly"/>,
/// <see cref="CalculateAllPositions"/> and the Julian Day conversions. The
/// calendar conversions are the standard Gregorian algorithms, so
/// <c>JulianDayUtility.ToDateTimeUtc</c> works against this double. Everything
/// else throws <see cref="NotSupportedException"/>.
/// </para>
/// <para>
/// With zero curvature the speed is a straight line and the finder's Newton
/// seed <c>t − v/a</c> lands exactly on the zero crossing, so a station search
/// resolves through the exact-zero probe without bracketing or bisecting. A
/// non-zero curvature moves the crossing off the seed (the central difference
/// the finder uses still returns exactly <c>a</c>, because the quadratic term
/// cancels), which forces the bracket-and-bisect path.
/// </para>
/// <para>
/// Bodies that have not been configured move at a constant +1.0°/day, which no
/// station threshold admits, so they are never flagged.
/// </para>
/// </remarks>
public sealed class SyntheticSpeedEphemeris : IEphemerisService
{
    private readonly Dictionary<CelestialBodyId, (double Speed, double Acceleration, double Curvature, double Longitude)> _bodies = new();

    /// <summary>The instant at which the configured speeds hold (Julian Day, UT).</summary>
    public double ReferenceJulianDay { get; }

    /// <summary>
    /// Creates a double whose configured speeds hold at <paramref name="referenceJulianDay"/>.
    /// </summary>
    /// <param name="referenceJulianDay">Julian Day (UT) at which each body has its configured speed.</param>
    public SyntheticSpeedEphemeris(double referenceJulianDay)
    {
        ReferenceJulianDay = referenceJulianDay;
    }

    /// <summary>
    /// Configures a body's motion: its speed at the reference instant, the
    /// rate at which that speed changes, an optional curvature of the speed,
    /// and its longitude at the reference instant.
    /// </summary>
    /// <param name="body">The body to configure.</param>
    /// <param name="speedAtReference">Daily speed in longitude at the reference instant (°/day).</param>
    /// <param name="accelerationPerDay">Change in daily speed per day at the reference instant (°/day²).</param>
    /// <param name="longitudeAtReference">Ecliptic longitude at the reference instant (degrees).</param>
    /// <param name="speedCurvature">
    /// Coefficient <c>c</c> of the quadratic term of the speed (°/day³). Zero,
    /// the default, gives a straight-line speed; a non-zero value forces the
    /// finder to bracket and bisect rather than land on the crossing.
    /// </param>
    /// <returns>This instance, for chaining.</returns>
    public SyntheticSpeedEphemeris WithBody(
        CelestialBodyId body, double speedAtReference, double accelerationPerDay,
        double longitudeAtReference = 100.0, double speedCurvature = 0.0)
    {
        _bodies[body] = (speedAtReference, accelerationPerDay, speedCurvature, longitudeAtReference);
        return this;
    }

    /// <inheritdoc />
    public string Version => "Synthetic";

    /// <inheritdoc />
    public PlanetPosition CalculatePosition(double julianDayUt, CelestialBodyId body)
    {
        double dt = julianDayUt - ReferenceJulianDay;
        (double v0, double a, double c, double lon0) = _bodies.TryGetValue(body, out var cfg) ? cfg : (1.0, 0.0, 0.0, 100.0);

        double speed = v0 + a * dt + c * dt * dt;
        double longitude = ((lon0 + v0 * dt + a * dt * dt / 2.0 + c * dt * dt * dt / 3.0) % 360.0 + 360.0) % 360.0;

        return new PlanetPosition
        {
            Body = body,
            Name = body == CelestialBodyId.TrueNode ? "True Node" : body.ToString(),
            Longitude = longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = speed,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
        };
    }

    /// <inheritdoc />
    public PlanetPosition CalculatePositionLongitudeOnly(double julianDayUt, CelestialBodyId body) =>
        CalculatePosition(julianDayUt, body);

    /// <inheritdoc />
    public IReadOnlyList<PlanetPosition> CalculateAllPositions(double julianDayUt) =>
        _bodies.Keys.Select(b => CalculatePosition(julianDayUt, b)).ToList();

    /// <inheritdoc />
    public double DateToJulianDay(int year, int month, int day, double hour,
        CalendarType calendar = CalendarType.Gregorian)
    {
        if (month <= 2)
        {
            year--;
            month += 12;
        }

        int a = year / 100;
        int b = 2 - a + a / 4;

        return Math.Floor(365.25 * (year + 4716)) + Math.Floor(30.6001 * (month + 1))
            + day + b - 1524.5 + hour / 24.0;
    }

    /// <inheritdoc />
    public (int Year, int Month, int Day, double Hour) JulianDayToDate(
        double julianDay, CalendarType calendar = CalendarType.Gregorian)
    {
        double z = Math.Floor(julianDay + 0.5);
        double f = julianDay + 0.5 - z;
        double alpha = Math.Floor((z - 1867216.25) / 36524.25);
        double a = z + 1 + alpha - Math.Floor(alpha / 4);
        double b = a + 1524;
        double c = Math.Floor((b - 122.1) / 365.25);
        double d = Math.Floor(365.25 * c);
        double e = Math.Floor((b - d) / 30.6001);

        int day = (int)(b - d - Math.Floor(30.6001 * e));
        int month = e < 14 ? (int)e - 1 : (int)e - 13;
        int year = month > 2 ? (int)c - 4716 : (int)c - 4715;

        return (year, month, day, f * 24.0);
    }

    /// <inheritdoc />
    public HouseData CalculateHouses(double julianDayUt, double latitude, double longitude,
        char houseSystem = 'P') => throw new NotSupportedException();

    /// <inheritdoc />
    public double GetObliquity(double julianDayUt) => throw new NotSupportedException();

    /// <inheritdoc />
    public (double SunriseJd, double SunsetJd) CalculateSunriseSunset(
        double julianDayUt, double latitude, double longitude) => throw new NotSupportedException();

    /// <inheritdoc />
    public HouseData CalculateHousesFromArmc(double armc, double latitude, double obliquity,
        char houseSystem = 'P') => throw new NotSupportedException();

    /// <inheritdoc />
    public double? CalculateRiseTransit(double julianDayUt, CelestialBodyId body,
        int rsmiFlag, double latitude, double longitude, string? starName = null) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public (int Flags, double[] Tret) FindNextSolarEclipse(double jdStart) => throw new NotSupportedException();

    /// <inheritdoc />
    public (int Flags, double[] Tret) FindNextLunarEclipse(double jdStart) => throw new NotSupportedException();

    /// <inheritdoc />
    public double CalculateFixedStarLongitude(string starName, double julianDayUt) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public (double Longitude, double Latitude) CalculateFixedStarPosition(string starName, double julianDayUt) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public (double RightAscension, double Declination) EclipticToEquatorial(
        double eclipticLongitude, double eclipticLatitude, double obliquity) =>
        throw new NotSupportedException();

    /// <inheritdoc />
    public double GetSiderealTimeDegrees(double julianDayUt) => throw new NotSupportedException();

    /// <inheritdoc />
    public void Dispose()
    {
    }
}
