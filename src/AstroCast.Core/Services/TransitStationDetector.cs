using AstroCast.Core.Enums;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Detects planetary stations (retrograde and direct) within a date range
/// by scanning for sign changes in daily motion.
/// </summary>
/// <remarks>
/// A station occurs when a planet's daily motion changes sign: positive to
/// negative indicates a retrograde station, negative to positive indicates
/// a direct station. Day-level precision is sufficient for monthly transit reports.
/// </remarks>
public sealed class TransitStationDetector
{
    private readonly TransitEphemerisHelper _ephemeris;

    /// <summary>
    /// Initialises a new instance of <see cref="TransitStationDetector"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris helper for position and motion lookups.</param>
    public TransitStationDetector(TransitEphemerisHelper ephemeris)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
    }

    /// <summary>
    /// Finds all stations for a given transit body within the date range.
    /// A station occurs when the daily motion changes sign (positive to negative
    /// = retrograde station, negative to positive = direct station).
    /// </summary>
    /// <param name="body">The transiting body to scan.</param>
    /// <param name="startDate">Start of scan range.</param>
    /// <param name="endDate">End of scan range.</param>
    /// <returns>List of stations found, chronologically ordered.</returns>
    public IReadOnlyList<TransitStation> FindStations(
        TransitBody body, DateOnly startDate, DateOnly endDate)
    {
        List<TransitStation> stations = [];

        DateOnly currentDate = startDate;
        double previousMotion = _ephemeris.GetDailyMotion(
            body, _ephemeris.DateToJulianDay(currentDate));

        currentDate = currentDate.AddDays(1);

        while (currentDate <= endDate)
        {
            double jd = _ephemeris.DateToJulianDay(currentDate);
            double currentMotion = _ephemeris.GetDailyMotion(body, jd);

            // Detect sign change in daily motion
            if (HasMotionSignChange(previousMotion, currentMotion))
            {
                // Station date is the day where motion is slowest (smallest absolute value)
                DateOnly stationDate = Math.Abs(currentMotion) < Math.Abs(previousMotion)
                    ? currentDate
                    : currentDate.AddDays(-1);

                // Determine station type
                string stationType = previousMotion > 0 && currentMotion < 0
                    ? "Retrograde"
                    : "Direct";

                // Refine to sub-minute UTC precision via binary search on daily motion
                double jdPrev = _ephemeris.DateToJulianDay(currentDate.AddDays(-1));
                double jdCurr = _ephemeris.DateToJulianDay(currentDate);
                (DateTime refinedTime, double refinedJd) = RefineStationTime(body, jdPrev, previousMotion, jdCurr);

                // Get position at refined station time, at the Julian Day the refinement holds (RT6):
                // the printed instant read back through the date's calendar switch was a Julian date
                // before 1582-10-15, and the longitude the body's 7 to 10 days after its station.
                double longitude = _ephemeris.GetLongitude(body, refinedJd);

                stations.Add(new TransitStation
                {
                    Planet = body,
                    StationType = stationType,
                    Date = stationDate,
                    Sign = TransitEphemerisHelper.LongitudeToSign(longitude),
                    Degrees = TransitEphemerisHelper.LongitudeToDegrees(longitude),
                    Minutes = TransitEphemerisHelper.LongitudeToMinutes(longitude),
                    DateTimeUtc = refinedTime,
                });
            }

            previousMotion = currentMotion;
            currentDate = currentDate.AddDays(1);
        }

        return stations.AsReadOnly();
    }

    /// <summary>
    /// Number of binary search iterations for station time refinement.
    /// 24 hours / 2^20 ≈ 0.08 seconds precision.
    /// </summary>
    private const int StationBisectionIterations = 20;

    /// <summary>
    /// Refines the station time to sub-minute UTC precision using binary search
    /// on daily motion. The station occurs when daily motion crosses zero.
    /// </summary>
    /// <param name="body">The transiting body.</param>
    /// <param name="jdBefore">Julian Day where motion has the previous sign.</param>
    /// <param name="motionBefore">Daily motion at <paramref name="jdBefore"/>.</param>
    /// <param name="jdAfter">Julian Day where motion has the opposite sign.</param>
    /// <returns>The station moment to the whole second, and its Julian Day, taken to that second.</returns>
    private (DateTime Time, double JulianDay) RefineStationTime(
        TransitBody body, double jdBefore, double motionBefore, double jdAfter)
    {
        double jdLow = jdBefore;
        double motionLow = motionBefore;

        for (int i = 0; i < StationBisectionIterations; i++)
        {
            double jdMid = (jdLow + jdAfter) / 2.0;
            double motionMid = _ephemeris.GetDailyMotion(body, jdMid);

            if (Math.Sign(motionMid) == Math.Sign(motionLow))
            {
                jdLow = jdMid;
                motionLow = motionMid;
            }
            else
            {
                jdAfter = jdMid;
            }
        }

        double jdStation = (jdLow + jdAfter) / 2.0;
        return (_ephemeris.JulianDayToDateTime(jdStation), JulianDayUtility.InstantToJulianDay(JulianDayUtility.JulianDayToInstant(jdStation)));
    }

    /// <summary>
    /// Determines whether a sign change occurred between two daily motion values.
    /// Handles the case where motion passes through zero.
    /// </summary>
    /// <param name="previous">Previous day's daily motion.</param>
    /// <param name="current">Current day's daily motion.</param>
    /// <returns>True if the motion sign changed (positive→negative or vice versa).</returns>
    private static bool HasMotionSignChange(double previous, double current)
    {
        return (previous > 0 && current < 0) || (previous < 0 && current > 0);
    }
}
