using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Finds every station of a body inside a time window, with its type and
/// instant, from hourly speed samples bisected to one second.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="StationFinder"/> answers "which station is nearest this
/// instant?"; this finder answers "which stations fall between these two
/// instants?", which a horary judgment needs when it asks whether a
/// significator turns before its aspect perfects. The speed is sampled every
/// hour from the window's start, so a station lasting less than an hour on
/// either side of zero cannot be missed for any of the eight stationing
/// planets (Mercury's speed changes by at most ~0.01°/day per hour near a
/// station). Each sign change is bisected on the ephemeris, and the type is
/// read from the crossing itself: positive to negative is a station
/// retrograde, negative to positive a station direct.
/// </para>
/// </remarks>
public static class StationWindowFinder
{
    /// <summary>Sampling step: one hour.</summary>
    public const double SampleStepDays = 1.0 / 24.0;

    /// <summary>Bisection stops once the bracket is no wider than this, in days (one second).</summary>
    public const double TimePrecisionDays = 1.0 / 86400.0;

    /// <summary>
    /// Finds every station of <paramref name="body"/> in
    /// [<paramref name="jdStart"/>, <paramref name="jdEnd"/>], in time order.
    /// </summary>
    /// <param name="ephemeris">Ephemeris used for every speed sample.</param>
    /// <param name="body">The body to scan.</param>
    /// <param name="jdStart">Start of the window (Julian Day, UT).</param>
    /// <param name="jdEnd">End of the window (Julian Day, UT).</param>
    /// <returns>The stations in the window; empty when there are none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ephemeris"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="jdEnd"/> is before <paramref name="jdStart"/>.</exception>
    public static IReadOnlyList<WindowStation> FindStations(
        IEphemerisService ephemeris, CelestialBodyId body, double jdStart, double jdEnd)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        if (jdEnd < jdStart)
            throw new ArgumentOutOfRangeException(nameof(jdEnd), jdEnd, "The window must not end before it starts.");

        List<WindowStation> stations = [];

        double previousJd = jdStart;
        double previousSpeed = Speed(ephemeris, body, jdStart);

        if (previousSpeed == 0.0)
        {
            // Stopped exactly at the window's start: the type is the sign change across
            // it. A body that is not moving at all (possible only in a synthetic
            // ephemeris) has no station to report.
            double after = Speed(ephemeris, body, jdStart + SampleStepDays);
            if (after != 0.0)
                stations.Add(Make(ephemeris, body, jdStart, isRetrograde: after < 0.0));
        }

        for (int k = 1; ; k++)
        {
            double jd = Math.Min(jdStart + k * SampleStepDays, jdEnd);
            if (jd <= previousJd)
                break;

            double speed = Speed(ephemeris, body, jd);

            if (speed == 0.0)
            {
                // An exact stop at a sample is the station itself — but only on the way
                // in; a body still at zero on the next sample is not stationing again.
                if (previousSpeed != 0.0)
                    stations.Add(Make(ephemeris, body, jd, isRetrograde: previousSpeed > 0.0));
            }
            else if (previousSpeed != 0.0 && Math.Sign(speed) != Math.Sign(previousSpeed))
            {
                double stationJd = Bisect(previousJd, jd, t => Speed(ephemeris, body, t));
                stations.Add(Make(ephemeris, body, stationJd, isRetrograde: previousSpeed > 0.0));
            }

            previousJd = jd;
            previousSpeed = speed;
            if (jd >= jdEnd)
                break;
        }

        return stations;
    }

    private static WindowStation Make(IEphemerisService ephemeris, CelestialBodyId body, double jd, bool isRetrograde) => new()
    {
        Body = body,
        JulianDay = jd,
        TimeUtc = JulianDayUtility.ToDateTimeUtc(ephemeris, jd),
        IsRetrogradeStation = isRetrograde,
        Longitude = ephemeris.CalculatePositionLongitudeOnly(jd, body).Longitude,
    };

    private static double Speed(IEphemerisService ephemeris, CelestialBodyId body, double jd) =>
        ephemeris.CalculatePositionLongitudeOnly(jd, body).SpeedInLongitude;

    private static double Bisect(double earlierJd, double laterJd, Func<double, double> f)
    {
        double earlierValue = f(earlierJd);
        if (earlierValue == 0.0)
            return earlierJd;

        int earlierSign = Math.Sign(earlierValue);
        const int MaxIterations = 64;

        for (int i = 0; i < MaxIterations && laterJd - earlierJd > TimePrecisionDays; i++)
        {
            double mid = (earlierJd + laterJd) / 2.0;
            double value = f(mid);
            if (value == 0.0)
                return mid;
            if (Math.Sign(value) == earlierSign)
                earlierJd = mid;
            else
                laterJd = mid;
        }

        return (earlierJd + laterJd) / 2.0;
    }
}

/// <summary>A station found inside a window by <see cref="StationWindowFinder"/>.</summary>
public sealed class WindowStation
{
    /// <summary>The body that stations.</summary>
    public required CelestialBodyId Body { get; init; }

    /// <summary>Julian Day (UT) of the station, to one second.</summary>
    public required double JulianDay { get; init; }

    /// <summary>UTC instant of the station, rounded to the nearest second.</summary>
    public required DateTime TimeUtc { get; init; }

    /// <summary>True for a station retrograde (speed crosses from positive to negative); false for a station direct.</summary>
    public required bool IsRetrogradeStation { get; init; }

    /// <summary>Ecliptic longitude of the body at the station, in degrees.</summary>
    public required double Longitude { get; init; }

    /// <summary>"retrograde" or "direct", for messages.</summary>
    public string TypeWord => IsRetrogradeStation ? "retrograde" : "direct";
}
