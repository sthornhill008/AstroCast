using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// A crossing of a target longitude by a body: its instant, and whether the body was moving
/// direct there (ruling T-D6, branch F, 1.3.4).
/// </summary>
/// <param name="JulianDay">The crossing's Julian Day (UT), bisected to the second.</param>
/// <param name="IsDirect">
/// Whether the body's speed in longitude at the crossing is positive; a speed of exactly zero
/// counts as retrograde.
/// </param>
internal readonly record struct ReturnCrossing(double JulianDay, bool IsDirect);

/// <summary>
/// Shared utility for finding exact moments when a celestial body returns to a
/// target ecliptic longitude, or when two bodies form an exact aspect.
/// Used by return services and conjunction/aspect finders.
/// </summary>
/// <remarks>
/// Uses a two-phase approach: daily step scan to bracket each event, then
/// 20-iteration binary search refinement for sub-arc-second precision.
/// The Sun moves ~1°/day and the Moon ~13°/day; daily steps are safe for
/// single-body tracking. For two-body aspects between slow planets (Jupiter,
/// Saturn), daily steps are also safe as their combined motion is &lt;0.3°/day.
/// </remarks>
internal static class ReturnChartFinder
{
    /// <summary>
    /// Number of binary search iterations for return time refinement.
    /// 24 hours / 2^20 ≈ 0.08 seconds precision.
    /// </summary>
    private const int BisectionIterations = 20;

    /// <summary>
    /// Finds all Julian Day moments within a range where a body's longitude
    /// matches a target longitude, using daily step scan + binary search.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for longitude lookups.</param>
    /// <param name="body">The celestial body to track (e.g., Sun, Moon).</param>
    /// <param name="targetLongitude">The target ecliptic longitude to match.</param>
    /// <param name="jdStart">Julian Day at the start of the scan range.</param>
    /// <param name="jdEnd">Julian Day at the end of the scan range.</param>
    /// <param name="filterConjunctionOnly">
    /// When true, only accept sign changes that represent a genuine conjunction
    /// (return) and reject opposition crossings. This is needed for both Sun
    /// and Moon because <see cref="SignedLongitudeDelta"/> wraps at ±180°,
    /// producing a false sign change at the opposition point.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of exact Julian Days for each return, sorted chronologically.</returns>
    internal static List<double> FindReturnJulianDays(
        IEphemerisService ephemeris,
        CelestialBodyId body,
        double targetLongitude,
        double jdStart,
        double jdEnd,
        bool filterConjunctionOnly = false,
        CancellationToken cancellationToken = default)
    {
        List<double> results = [];

        double jdPrev = jdStart;
        PlanetPosition posPrev = ephemeris.CalculatePositionLongitudeOnly(jdPrev, body);
        double prevDelta = SignedLongitudeDelta(posPrev.Longitude, targetLongitude);

        double jdCurr = jdPrev + 1.0; // daily steps

        while (jdCurr <= jdEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();

            PlanetPosition posCurr = ephemeris.CalculatePositionLongitudeOnly(jdCurr, body);
            double currDelta = SignedLongitudeDelta(posCurr.Longitude, targetLongitude);

            // Detect sign change (body crossed the target longitude between the two days)
            if ((prevDelta > 0 && currDelta < 0) || (prevDelta < 0 && currDelta > 0))
            {
                bool accept = true;

                if (filterConjunctionOnly)
                {
                    // Distinguish conjunction (return) from opposition:
                    // At conjunction (0° crossing), the sum of absolute deltas is
                    // small — at most ~26° for the Moon (~13°/day) or ~2° for the Sun.
                    // At opposition (±180° crossing), the sum exceeds 300°.
                    // A threshold of 90° cleanly separates the two cases.
                    accept = Math.Abs(prevDelta) + Math.Abs(currDelta) < 90.0;
                }

                if (accept)
                {
                    double jdExact = RefineReturnTime(
                        ephemeris, body, jdPrev, prevDelta, jdCurr, targetLongitude);
                    results.Add(jdExact);
                }
            }

            prevDelta = currDelta;
            jdPrev = jdCurr;
            jdCurr += 1.0;
        }

        return results;
    }

    /// <summary>
    /// Computes a signed delta between a current longitude and a target longitude,
    /// handling the 0°/360° wraparound. The result crosses zero when the current
    /// longitude equals the target, enabling sign-change detection for binary search.
    /// </summary>
    /// <param name="current">Current ecliptic longitude (0–360°).</param>
    /// <param name="target">Target ecliptic longitude (0–360°).</param>
    /// <returns>Signed delta in the range (-180°, 180°], crossing zero at exact match.</returns>
    internal static double SignedLongitudeDelta(double current, double target)
    {
        double delta = current - target;
        delta = ((delta % 360.0) + 360.0) % 360.0;
        if (delta > 180.0)
            delta -= 360.0;
        return delta;
    }

    /// <summary>
    /// Refines a crossing of the target longitude within a one-day bracket to the second and reads
    /// the body's direction there: direct when its speed in longitude at the crossing is positive.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for longitude and speed lookups.</param>
    /// <param name="body">The celestial body to track.</param>
    /// <param name="jdLow">Julian Day at the bracket start (where delta has one sign).</param>
    /// <param name="deltaLow">Signed longitude delta at <paramref name="jdLow"/>.</param>
    /// <param name="jdHigh">Julian Day at the bracket end (where delta has the opposite sign).</param>
    /// <param name="targetLongitude">Target ecliptic longitude (fixed).</param>
    /// <returns>The crossing: its Julian Day and whether the body was moving direct.</returns>
    internal static ReturnCrossing RefineCrossing(
        IEphemerisService ephemeris,
        CelestialBodyId body,
        double jdLow,
        double deltaLow,
        double jdHigh,
        double targetLongitude)
    {
        double jd = RefineReturnTime(ephemeris, body, jdLow, deltaLow, jdHigh, targetLongitude);
        bool isDirect = ephemeris.CalculatePositionLongitudeOnly(jd, body).SpeedInLongitude > 0.0;
        return new ReturnCrossing(jd, isDirect);
    }

    /// <summary>
    /// Uses binary search (bisection) to refine the exact return time
    /// within a one-day bracket to sub-second UTC precision.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for longitude lookups.</param>
    /// <param name="body">The celestial body to track.</param>
    /// <param name="jdLow">Julian Day at the bracket start (where delta has one sign).</param>
    /// <param name="deltaLow">Signed longitude delta at <paramref name="jdLow"/>.</param>
    /// <param name="jdHigh">Julian Day at the bracket end (where delta has the opposite sign).</param>
    /// <param name="targetLongitude">Target ecliptic longitude (fixed).</param>
    /// <returns>Julian Day of the exact return moment.</returns>
    private static double RefineReturnTime(
        IEphemerisService ephemeris,
        CelestialBodyId body,
        double jdLow,
        double deltaLow,
        double jdHigh,
        double targetLongitude)
    {
        for (int i = 0; i < BisectionIterations; i++)
        {
            double jdMid = (jdLow + jdHigh) / 2.0;
            PlanetPosition posMid = ephemeris.CalculatePositionLongitudeOnly(jdMid, body);
            double deltaMid = SignedLongitudeDelta(posMid.Longitude, targetLongitude);

            if (Math.Sign(deltaMid) == Math.Sign(deltaLow))
            {
                jdLow = jdMid;
                deltaLow = deltaMid;
            }
            else
            {
                jdHigh = jdMid;
            }
        }

        return (jdLow + jdHigh) / 2.0;
    }

    #region Two-Body Aspect Finder

    /// <summary>
    /// Finds all Julian Day moments within a range where the angular separation
    /// between two bodies equals a target aspect angle, using daily step scan
    /// + binary search refinement.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for longitude lookups.</param>
    /// <param name="body1">The first celestial body (typically the slower one).</param>
    /// <param name="body2">The second celestial body (typically the faster one).</param>
    /// <param name="targetAngle">The target angular separation: 0 = conjunction,
    /// 90 = square, 120 = trine, 180 = opposition. Range: 0–180°.</param>
    /// <param name="jdStart">Julian Day at the start of the scan range.</param>
    /// <param name="jdEnd">Julian Day at the end of the scan range.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>List of exact Julian Days for each aspect, sorted chronologically.</returns>
    internal static List<double> FindAspectJulianDays(
        IEphemerisService ephemeris,
        CelestialBodyId body1,
        CelestialBodyId body2,
        double targetAngle,
        double jdStart,
        double jdEnd,
        CancellationToken cancellationToken = default)
    {
        List<double> results = [];

        // Track two delta functions to detect the aspect from either direction:
        // deltaPlus: separation - targetAngle (body2 is targetAngle AHEAD of body1)
        // deltaMinus: separation + targetAngle (body2 is targetAngle BEHIND body1)
        // For conjunction (0°) and opposition (180°), only deltaPlus is needed
        // as the deltaMinus would produce duplicates.
        bool trackMinus = targetAngle > 0.5 && targetAngle < 179.5;

        double jdPrev = jdStart;
        (double prevDeltaPlus, double prevDeltaMinus) = ComputeAspectDeltas(
            ephemeris, body1, body2, targetAngle, jdPrev);

        double jdCurr = jdPrev + 1.0;

        while (jdCurr <= jdEnd)
        {
            cancellationToken.ThrowIfCancellationRequested();

            (double currDeltaPlus, double currDeltaMinus) = ComputeAspectDeltas(
                ephemeris, body1, body2, targetAngle, jdCurr);

            // Check the "plus" crossing (body2 is targetAngle ahead of body1)
            if (HasSignChange(prevDeltaPlus, currDeltaPlus) &&
                Math.Abs(prevDeltaPlus) + Math.Abs(currDeltaPlus) < 90.0)
            {
                double jdExact = RefineAspectTime(
                    ephemeris, body1, body2, targetAngle, true,
                    jdPrev, prevDeltaPlus, jdCurr);
                results.Add(jdExact);
            }

            // Check the "minus" crossing (body2 is targetAngle behind body1)
            if (trackMinus &&
                HasSignChange(prevDeltaMinus, currDeltaMinus) &&
                Math.Abs(prevDeltaMinus) + Math.Abs(currDeltaMinus) < 90.0)
            {
                double jdExact = RefineAspectTime(
                    ephemeris, body1, body2, targetAngle, false,
                    jdPrev, prevDeltaMinus, jdCurr);
                results.Add(jdExact);
            }

            prevDeltaPlus = currDeltaPlus;
            prevDeltaMinus = currDeltaMinus;
            jdPrev = jdCurr;
            jdCurr += 1.0;
        }

        results.Sort();
        return results;
    }

    /// <summary>
    /// Detects a sign change between two consecutive delta values.
    /// </summary>
    private static bool HasSignChange(double a, double b) =>
        (a > 0 && b < 0) || (a < 0 && b > 0);

    /// <summary>
    /// Computes the two delta functions for aspect detection between two moving bodies.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service.</param>
    /// <param name="body1">First body.</param>
    /// <param name="body2">Second body.</param>
    /// <param name="targetAngle">Target aspect angle (0–180°).</param>
    /// <param name="jd">Julian Day to evaluate.</param>
    /// <returns>
    /// Tuple of (deltaPlus, deltaMinus) where:
    /// deltaPlus = separation - targetAngle (crosses zero when body2 is targetAngle ahead),
    /// deltaMinus = separation + targetAngle (crosses zero when body2 is targetAngle behind).
    /// </returns>
    private static (double deltaPlus, double deltaMinus) ComputeAspectDeltas(
        IEphemerisService ephemeris,
        CelestialBodyId body1, CelestialBodyId body2,
        double targetAngle, double jd)
    {
        double lon1 = ephemeris.CalculatePositionLongitudeOnly(jd, body1).Longitude;
        double lon2 = ephemeris.CalculatePositionLongitudeOnly(jd, body2).Longitude;
        double separation = SignedLongitudeDelta(lon2, lon1);

        // Delta from +targetAngle
        double deltaPlus = separation - targetAngle;
        if (deltaPlus > 180.0) deltaPlus -= 360.0;
        if (deltaPlus < -180.0) deltaPlus += 360.0;

        // Delta from -targetAngle
        double deltaMinus = separation + targetAngle;
        if (deltaMinus > 180.0) deltaMinus -= 360.0;
        if (deltaMinus < -180.0) deltaMinus += 360.0;

        return (deltaPlus, deltaMinus);
    }

    /// <summary>
    /// Refines a two-body aspect time using binary search within a one-day bracket.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service.</param>
    /// <param name="body1">First body.</param>
    /// <param name="body2">Second body.</param>
    /// <param name="targetAngle">Target aspect angle.</param>
    /// <param name="usePlusDelta">If true, track the plus delta; otherwise, the minus delta.</param>
    /// <param name="jdLow">Julian Day at the bracket start.</param>
    /// <param name="deltaLow">Delta value at jdLow.</param>
    /// <param name="jdHigh">Julian Day at the bracket end.</param>
    /// <returns>Julian Day of the exact aspect moment.</returns>
    private static double RefineAspectTime(
        IEphemerisService ephemeris,
        CelestialBodyId body1, CelestialBodyId body2,
        double targetAngle, bool usePlusDelta,
        double jdLow, double deltaLow, double jdHigh)
    {
        for (int i = 0; i < BisectionIterations; i++)
        {
            double jdMid = (jdLow + jdHigh) / 2.0;
            (double dPlus, double dMinus) = ComputeAspectDeltas(
                ephemeris, body1, body2, targetAngle, jdMid);
            double deltaMid = usePlusDelta ? dPlus : dMinus;

            if (Math.Sign(deltaMid) == Math.Sign(deltaLow))
            {
                jdLow = jdMid;
                deltaLow = deltaMid;
            }
            else
            {
                jdHigh = jdMid;
            }
        }

        return (jdLow + jdHigh) / 2.0;
    }

    #endregion
}
