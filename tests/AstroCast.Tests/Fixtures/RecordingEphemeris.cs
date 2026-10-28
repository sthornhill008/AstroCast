using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Tests.Fixtures;

/// <summary>
/// An <see cref="IEphemerisService"/> decorator that records the earliest and latest Julian Day
/// each request for the sky is made at, and passes every call to the service it wraps.
/// </summary>
/// <remarks>
/// A request for the sky is any member that takes an instant and asks the library for a body, a
/// house, the obliquity, the sidereal time, a rising or an eclipse search. Calendar conversions
/// (<see cref="DateToJulianDay"/>, <see cref="JulianDayToDate"/>) are arithmetic, not requests, and
/// are not recorded: a series computes the Julian Day of 1 January after its last year whether or
/// not it asks for the sky there. Disposing the decorator leaves the wrapped service open; the
/// fixture that made it owns it.
/// </remarks>
public sealed class RecordingEphemeris : IEphemerisService
{
    private readonly IEphemerisService _inner;

    /// <summary>
    /// Wraps <paramref name="inner"/>.
    /// </summary>
    /// <param name="inner">The service every call is passed to.</param>
    public RecordingEphemeris(IEphemerisService inner)
    {
        ArgumentNullException.ThrowIfNull(inner);
        _inner = inner;
    }

    /// <summary>Gets the number of requests recorded.</summary>
    public long Count { get; private set; }

    /// <summary>Gets the earliest Julian Day requested, or <see cref="double.MaxValue"/> before any request.</summary>
    public double Earliest { get; private set; } = double.MaxValue;

    /// <summary>Gets the latest Julian Day requested, or <see cref="double.MinValue"/> before any request.</summary>
    public double Latest { get; private set; } = double.MinValue;

    /// <inheritdoc />
    public string Version => _inner.Version;

    /// <inheritdoc />
    public PlanetPosition CalculatePosition(double julianDayUt, CelestialBodyId body)
    {
        Record(julianDayUt);
        return _inner.CalculatePosition(julianDayUt, body);
    }

    /// <inheritdoc />
    public PlanetPosition CalculatePositionLongitudeOnly(double julianDayUt, CelestialBodyId body)
    {
        Record(julianDayUt);
        return _inner.CalculatePositionLongitudeOnly(julianDayUt, body);
    }

    /// <inheritdoc />
    public IReadOnlyList<PlanetPosition> CalculateAllPositions(double julianDayUt)
    {
        Record(julianDayUt);
        return _inner.CalculateAllPositions(julianDayUt);
    }

    /// <inheritdoc />
    public double DateToJulianDay(int year, int month, int day, double hour,
        CalendarType calendar = CalendarType.Gregorian) =>
        _inner.DateToJulianDay(year, month, day, hour, calendar);

    /// <inheritdoc />
    public (int Year, int Month, int Day, double Hour) JulianDayToDate(
        double julianDay, CalendarType calendar = CalendarType.Gregorian) =>
        _inner.JulianDayToDate(julianDay, calendar);

    /// <inheritdoc />
    public HouseData CalculateHouses(double julianDayUt, double latitude, double longitude, char houseSystem = 'P')
    {
        Record(julianDayUt);
        return _inner.CalculateHouses(julianDayUt, latitude, longitude, houseSystem);
    }

    /// <inheritdoc />
    public double GetObliquity(double julianDayUt)
    {
        Record(julianDayUt);
        return _inner.GetObliquity(julianDayUt);
    }

    /// <inheritdoc />
    public (double SunriseJd, double SunsetJd) CalculateSunriseSunset(double julianDayUt, double latitude, double longitude)
    {
        Record(julianDayUt);
        return _inner.CalculateSunriseSunset(julianDayUt, latitude, longitude);
    }

    /// <inheritdoc />
    public HouseData CalculateHousesFromArmc(double armc, double latitude, double obliquity, char houseSystem = 'P') =>
        _inner.CalculateHousesFromArmc(armc, latitude, obliquity, houseSystem);

    /// <inheritdoc />
    public double? CalculateRiseTransit(double julianDayUt, CelestialBodyId body, int rsmiFlag,
        double latitude, double longitude, string? starName = null)
    {
        Record(julianDayUt);
        return _inner.CalculateRiseTransit(julianDayUt, body, rsmiFlag, latitude, longitude, starName);
    }

    /// <inheritdoc />
    public (int Flags, double[] Tret) FindNextSolarEclipse(double jdStart)
    {
        Record(jdStart);
        return _inner.FindNextSolarEclipse(jdStart);
    }

    /// <inheritdoc />
    public (int Flags, double[] Tret) FindNextLunarEclipse(double jdStart)
    {
        Record(jdStart);
        return _inner.FindNextLunarEclipse(jdStart);
    }

    /// <inheritdoc />
    public double CalculateFixedStarLongitude(string starName, double julianDayUt)
    {
        Record(julianDayUt);
        return _inner.CalculateFixedStarLongitude(starName, julianDayUt);
    }

    /// <inheritdoc />
    public (double Longitude, double Latitude) CalculateFixedStarPosition(string starName, double julianDayUt)
    {
        Record(julianDayUt);
        return _inner.CalculateFixedStarPosition(starName, julianDayUt);
    }

    /// <inheritdoc />
    public (double RightAscension, double Declination) EclipticToEquatorial(
        double eclipticLongitude, double eclipticLatitude, double obliquity) =>
        _inner.EclipticToEquatorial(eclipticLongitude, eclipticLatitude, obliquity);

    /// <inheritdoc />
    public double GetSiderealTimeDegrees(double julianDayUt)
    {
        Record(julianDayUt);
        return _inner.GetSiderealTimeDegrees(julianDayUt);
    }

    /// <summary>Leaves the wrapped service open: the fixture that made it owns it.</summary>
    public void Dispose()
    {
    }

    private void Record(double julianDayUt)
    {
        Count++;
        if (julianDayUt < Earliest) Earliest = julianDayUt;
        if (julianDayUt > Latest) Latest = julianDayUt;
    }
}
