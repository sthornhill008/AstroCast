using AstroCast.Core.Enums;

namespace AstroCast.Core.Services;

/// <summary>
/// Determines whether Mars qualifies for inclusion in a transit report.
/// Mars is included only when it stations retrograde/direct within the period,
/// OR when it is within orb of a natal aspect while moving slower than 0.3°/day.
/// </summary>
/// <remarks>
/// Mars moves at ~0.5–0.7°/day normally, but slows dramatically near stations.
/// When Mars is near station and aspecting a natal planet, its effect is
/// concentrated and significant — comparable to an outer planet transit.
/// </remarks>
public sealed class TransitMarsEvaluator
{
    /// <summary>Major aspect angles checked for Mars slow-motion evaluation.</summary>
    private static readonly double[] MajorAspectAngles = [0.0, 60.0, 90.0, 120.0, 180.0];

    private readonly TransitEphemerisHelper _ephemeris;
    private readonly TransitStationDetector _stationDetector;

    /// <summary>
    /// Initialises a new instance of <see cref="TransitMarsEvaluator"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris helper for position and motion lookups.</param>
    /// <param name="stationDetector">Station detector for Mars station checks.</param>
    public TransitMarsEvaluator(
        TransitEphemerisHelper ephemeris,
        TransitStationDetector stationDetector)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(stationDetector);
        _ephemeris = ephemeris;
        _stationDetector = stationDetector;
    }

    /// <summary>
    /// Evaluates whether Mars should be included in the transit report.
    /// </summary>
    /// <param name="startDate">Report start date.</param>
    /// <param name="endDate">Report end date.</param>
    /// <param name="natalTargetLongitudes">
    /// Longitudes of all natal targets (planets + angles) for aspect checking.
    /// </param>
    /// <returns>True if Mars qualifies for inclusion.</returns>
    public bool ShouldIncludeMars(
        DateOnly startDate,
        DateOnly endDate,
        IReadOnlyList<double> natalTargetLongitudes)
    {
        ArgumentNullException.ThrowIfNull(natalTargetLongitudes);

        // Condition 1: Mars stations within the report period
        IReadOnlyList<Models.Transit.TransitStation> marsStations =
            _stationDetector.FindStations(TransitBody.Mars, startDate, endDate);

        if (marsStations.Count > 0)
            return true;

        // Condition 2: Mars is slow AND aspecting a natal target
        DateOnly currentDate = startDate;
        while (currentDate <= endDate)
        {
            double jd = _ephemeris.DateToJulianDay(currentDate);
            double dailyMotion = _ephemeris.GetDailyMotion(TransitBody.Mars, jd);

            if (Math.Abs(dailyMotion) < TransitOrbs.MarsSlowMotionThreshold)
            {
                double marsLongitude = _ephemeris.GetLongitude(TransitBody.Mars, jd);

                if (IsWithinAspectOrb(marsLongitude, natalTargetLongitudes))
                    return true;
            }

            currentDate = currentDate.AddDays(1);
        }

        return false;
    }

    /// <summary>
    /// Checks whether a longitude is within orb of any major aspect to any natal target.
    /// </summary>
    /// <param name="marsLongitude">Mars's current ecliptic longitude.</param>
    /// <param name="natalTargetLongitudes">All natal target longitudes to check against.</param>
    /// <returns>True if Mars is within orb of at least one major aspect to a natal target.</returns>
    private static bool IsWithinAspectOrb(
        double marsLongitude,
        IReadOnlyList<double> natalTargetLongitudes)
    {
        foreach (double natalLongitude in natalTargetLongitudes)
        {
            double separation = AngularSeparation(marsLongitude, natalLongitude);

            foreach (double aspectAngle in MajorAspectAngles)
            {
                double orb = TransitOrbs.GetOrb(TransitBody.Mars, aspectAngle, false);
                double orbActual = Math.Abs(separation - aspectAngle);

                if (orbActual <= orb)
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Calculates the shortest angular separation between two ecliptic longitudes.
    /// Returns a value in the range [0, 180].
    /// </summary>
    /// <param name="lon1">First longitude (0–360).</param>
    /// <param name="lon2">Second longitude (0–360).</param>
    /// <returns>Angular separation in degrees (0–180).</returns>
    private static double AngularSeparation(double lon1, double lon2)
    {
        double diff = Math.Abs(lon1 - lon2);
        if (diff > 180.0)
            diff = 360.0 - diff;
        return diff;
    }
}
