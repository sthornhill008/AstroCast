using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// An <see cref="IEphemerisService"/> over <see cref="StubEphemerisService"/> that records every Julian Day it is
/// asked about and gives the Sun a right ascension of <see cref="SunDegreesPerDay"/> × the days since a birth
/// (1.6.1, item F, branch TiesAndGuards): for the tests that read how far past a birth the solar-arc key reads.
/// </summary>
/// <param name="birthJulianDayUt">The birth instant, as a Julian Day (UT), from which the Sun's advance is counted.</param>
public sealed class RecordingEphemerisService(double birthJulianDayUt) : IEphemerisService
{
    /// <summary>The Sun's advance in right ascension, in degrees a day: about its mean motion.</summary>
    public const double SunDegreesPerDay = 0.9856;

    private readonly StubEphemerisService _inner = new();
    private readonly List<double> _julianDaysAsked = [];

    /// <summary>Gets the birth instant the Sun's advance is counted from.</summary>
    public double BirthJulianDayUt { get; } = birthJulianDayUt;

    /// <summary>Gets every Julian Day any member was asked about, in call order.</summary>
    public IReadOnlyList<double> JulianDaysAsked => _julianDaysAsked.AsReadOnly();

    /// <summary>Gets the latest Julian Day asked about, or null before the first call.</summary>
    public double? MaxJulianDay => _julianDaysAsked.Count == 0 ? null : _julianDaysAsked.Max();

    /// <inheritdoc />
    public string Version => _inner.Version;

    /// <inheritdoc />
    public PlanetPosition CalculatePosition(double julianDayUt, CelestialBodyId body)
    {
        _julianDaysAsked.Add(julianDayUt);
        return new PlanetPosition
        {
            Body = body,
            Name = body.ToString(),
            Longitude = 0.0,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = 1.0,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
            RightAscension = SunDegreesPerDay * (julianDayUt - BirthJulianDayUt),
            Declination = 0.0,
        };
    }

    /// <inheritdoc />
    public PlanetPosition CalculatePositionLongitudeOnly(double julianDayUt, CelestialBodyId body) =>
        CalculatePosition(julianDayUt, body);

    /// <inheritdoc />
    public IReadOnlyList<PlanetPosition> CalculateAllPositions(double julianDayUt)
    {
        _julianDaysAsked.Add(julianDayUt);
        return _inner.CalculateAllPositions(julianDayUt);
    }

    /// <inheritdoc />
    public double DateToJulianDay(int year, int month, int day, double hour, CalendarType calendar = CalendarType.Gregorian) =>
        _inner.DateToJulianDay(year, month, day, hour, calendar);

    /// <inheritdoc />
    public (int Year, int Month, int Day, double Hour) JulianDayToDate(double julianDay, CalendarType calendar = CalendarType.Gregorian)
    {
        _julianDaysAsked.Add(julianDay);
        return _inner.JulianDayToDate(julianDay, calendar);
    }

    /// <inheritdoc />
    public HouseData CalculateHouses(double julianDayUt, double latitude, double longitude, char houseSystem = 'P')
    {
        _julianDaysAsked.Add(julianDayUt);
        return _inner.CalculateHouses(julianDayUt, latitude, longitude, houseSystem);
    }

    /// <inheritdoc />
    public double GetObliquity(double julianDayUt)
    {
        _julianDaysAsked.Add(julianDayUt);
        return _inner.GetObliquity(julianDayUt);
    }

    /// <inheritdoc />
    public (double SunriseJd, double SunsetJd) CalculateSunriseSunset(double julianDayUt, double latitude, double longitude)
    {
        _julianDaysAsked.Add(julianDayUt);
        return _inner.CalculateSunriseSunset(julianDayUt, latitude, longitude);
    }

    /// <inheritdoc />
    public HouseData CalculateHousesFromArmc(double armc, double latitude, double obliquity, char houseSystem = 'P') =>
        _inner.CalculateHousesFromArmc(armc, latitude, obliquity, houseSystem);

    /// <inheritdoc />
    public double? CalculateRiseTransit(double julianDayUt, CelestialBodyId body, int rsmiFlag, double latitude, double longitude, string? starName = null)
    {
        _julianDaysAsked.Add(julianDayUt);
        return _inner.CalculateRiseTransit(julianDayUt, body, rsmiFlag, latitude, longitude, starName);
    }

    /// <inheritdoc />
    public double CalculateFixedStarLongitude(string starName, double julianDayUt)
    {
        _julianDaysAsked.Add(julianDayUt);
        return _inner.CalculateFixedStarLongitude(starName, julianDayUt);
    }

    /// <inheritdoc />
    public (double Longitude, double Latitude) CalculateFixedStarPosition(string starName, double julianDayUt)
    {
        _julianDaysAsked.Add(julianDayUt);
        return _inner.CalculateFixedStarPosition(starName, julianDayUt);
    }

    /// <inheritdoc />
    public (double RightAscension, double Declination) EclipticToEquatorial(double eclipticLongitude, double eclipticLatitude, double obliquity) =>
        _inner.EclipticToEquatorial(eclipticLongitude, eclipticLatitude, obliquity);

    /// <inheritdoc />
    public double GetSiderealTimeDegrees(double julianDayUt)
    {
        _julianDaysAsked.Add(julianDayUt);
        return _inner.GetSiderealTimeDegrees(julianDayUt);
    }

    /// <inheritdoc />
    public (int Flags, double[] Tret) FindNextSolarEclipse(double jdStart)
    {
        _julianDaysAsked.Add(jdStart);
        return _inner.FindNextSolarEclipse(jdStart);
    }

    /// <inheritdoc />
    public (int Flags, double[] Tret) FindNextLunarEclipse(double jdStart)
    {
        _julianDaysAsked.Add(jdStart);
        return _inner.FindNextLunarEclipse(jdStart);
    }

    /// <inheritdoc />
    public void Dispose() => _inner.Dispose();
}
