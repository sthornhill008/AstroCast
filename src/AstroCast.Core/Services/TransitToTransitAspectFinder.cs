using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Models.Transit;

namespace AstroCast.Core.Services;

/// <summary>
/// Detects major aspects between pairs of transiting planets within a report
/// period and refines exact times using binary search. Produces
/// <see cref="TransitToTransitAspect"/> records for mundane sky events.
/// </summary>
/// <remarks>
/// <para>
/// Only major Ptolemaic aspects (conjunction 0°, sextile 60°, square 90°,
/// trine 120°, opposition 180°) are detected. North Node is excluded from
/// transit-to-transit scanning because its oscillating motion produces
/// frequent spurious contacts.
/// </para>
/// <para>
/// For each unique pair of bodies and each aspect angle, the algorithm scans
/// day-by-day for sign changes in the signed aspect separation, then refines
/// using 20-iteration binary search (identical precision to transit-to-natal).
/// </para>
/// </remarks>
internal sealed class TransitToTransitAspectFinder
{
    private readonly TransitEphemerisHelper _ephemeris;

    /// <summary>
    /// Number of binary search iterations for exact pass refinement.
    /// 24 hours / 2^20 ≈ 0.08 seconds precision.
    /// </summary>
    private const int BisectionIterations = 20;

    /// <summary>Major Ptolemaic aspect angles and their names/abbreviations.</summary>
    private static readonly (double Angle, string Name, string Abbreviation)[] MajorAspects =
    [
        (0.0, "Conjunction", "cnj"),
        (60.0, "Sextile", "sxt"),
        (90.0, "Square", "sqr"),
        (120.0, "Trine", "tri"),
        (180.0, "Opposition", "opp"),
    ];

    /// <summary>
    /// Initialises a new <see cref="TransitToTransitAspectFinder"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris helper for longitude lookups and date conversion.</param>
    public TransitToTransitAspectFinder(TransitEphemerisHelper ephemeris)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
    }

    /// <summary>
    /// Finds all major transit-to-transit aspects that reach exact during
    /// the specified date range.
    /// </summary>
    /// <param name="bodies">
    /// Transit bodies to scan. North Node should be excluded by the caller.
    /// </param>
    /// <param name="startDate">Report start date.</param>
    /// <param name="endDate">Report end date.</param>
    /// <returns>
    /// Transit-to-transit aspects sorted chronologically by exact date/time.
    /// </returns>
    public IReadOnlyList<TransitToTransitAspect> FindAspects(
        IReadOnlyList<TransitBody> bodies, DateOnly startDate, DateOnly endDate)
    {
        List<TransitToTransitAspect> results = [];

        // Generate unique pairs (i < j to avoid duplicates)
        for (int i = 0; i < bodies.Count; i++)
        {
            for (int j = i + 1; j < bodies.Count; j++)
            {
                TransitBody body1 = bodies[i];
                TransitBody body2 = bodies[j];

                foreach ((double angle, string name, string abbreviation) in MajorAspects)
                {
                    // Skip conjunction of a body with itself (impossible for different bodies)
                    // and skip opposition of a body with itself (also impossible)
                    FindExactAspects(body1, body2, angle, name, abbreviation,
                        startDate, endDate, results);
                }
            }
        }

        // Sort chronologically
        results.Sort((a, b) => a.ExactDateTimeUtc.CompareTo(b.ExactDateTimeUtc));

        return results;
    }

    #region Private — Exact Aspect Detection

    /// <summary>
    /// Scans day-by-day for exact aspect passes between two transiting bodies.
    /// For non-symmetric aspects (not 0° or 180°), scans both the primary angle
    /// and its complement (360° - angle) to detect the aspect regardless of
    /// which planet is ahead in ecliptic longitude.
    /// </summary>
    private void FindExactAspects(
        TransitBody body1, TransitBody body2,
        double aspectAngle, string aspectName, string aspectAbbreviation,
        DateOnly startDate, DateOnly endDate,
        List<TransitToTransitAspect> results)
    {
        // Scan for the primary angle
        ScanForTargetAngle(body1, body2, aspectAngle, aspectName, aspectAbbreviation,
            startDate, endDate, results);

        // For non-symmetric aspects, also scan the complementary angle (360 - angle)
        // to catch the aspect when the other planet leads in longitude.
        // Conjunction (0°) and opposition (180°) are symmetric — no complement needed.
        double complement = 360.0 - aspectAngle;
        if (complement > 0.0 && complement < 360.0 && Math.Abs(complement - aspectAngle) > 0.01)
        {
            ScanForTargetAngle(body1, body2, complement, aspectName, aspectAbbreviation,
                startDate, endDate, results);
        }
    }

    /// <summary>
    /// Scans day-by-day for a specific target separation angle between two bodies.
    /// Uses a continuous signed delta function that changes sign exactly at the
    /// target angle, enabling reliable binary search refinement.
    /// </summary>
    private void ScanForTargetAngle(
        TransitBody body1, TransitBody body2,
        double targetAngle, string aspectName, string aspectAbbreviation,
        DateOnly startDate, DateOnly endDate,
        List<TransitToTransitAspect> results)
    {
        DateOnly currentDate = startDate;
        double jdPrev = _ephemeris.DateToJulianDay(currentDate);
        double lon1Prev = _ephemeris.GetLongitude(body1, jdPrev);
        double lon2Prev = _ephemeris.GetLongitude(body2, jdPrev);
        double prevDelta = SignedAspectSeparation(lon1Prev, lon2Prev, targetAngle);

        currentDate = currentDate.AddDays(1);

        while (currentDate <= endDate)
        {
            double jdCurr = _ephemeris.DateToJulianDay(currentDate);
            double lon1Curr = _ephemeris.GetLongitude(body1, jdCurr);
            double lon2Curr = _ephemeris.GetLongitude(body2, jdCurr);
            double currDelta = SignedAspectSeparation(lon1Curr, lon2Curr, targetAngle);

            // Detect sign change (exact aspect occurred between the two days).
            // Guard: both deltas must be < 90° in magnitude to exclude false
            // crossings caused by the ±180° wrap-around discontinuity.
            if ((prevDelta > 0 && currDelta < 0) || (prevDelta < 0 && currDelta > 0))
            if (Math.Abs(prevDelta) < 90.0 && Math.Abs(currDelta) < 90.0)
            {
                (DateTime exactTime, double residualOrb, double lon1Exact, double lon2Exact) =
                    RefineExactTime(jdPrev, prevDelta, jdCurr, body1, body2, targetAngle);

                // Record the canonical aspect angle (e.g., 60° for sextile),
                // not the target angle which may be the complement (300°)
                double canonicalAngle = Math.Min(targetAngle, 360.0 - targetAngle);

                results.Add(new TransitToTransitAspect
                {
                    Planet1 = body1,
                    Planet1Name = FormatPlanetName(body1),
                    Planet1Longitude = lon1Exact,
                    Planet1Position = FormatPosition(lon1Exact),
                    Planet2 = body2,
                    Planet2Name = FormatPlanetName(body2),
                    Planet2Longitude = lon2Exact,
                    Planet2Position = FormatPosition(lon2Exact),
                    AspectName = aspectName,
                    AspectAbbreviation = aspectAbbreviation,
                    AspectAngle = canonicalAngle,
                    ExactDateTimeUtc = exactTime,
                    ExactOrbDegrees = residualOrb,
                });
            }

            prevDelta = currDelta;
            jdPrev = jdCurr;
            currentDate = currentDate.AddDays(1);
        }
    }

    /// <summary>
    /// Uses binary search to refine the exact moment of a transit-to-transit aspect.
    /// Both planet longitudes are computed at each midpoint since both are moving.
    /// </summary>
    /// <returns>
    /// Tuple of (exact UTC time, residual orb, planet 1 longitude, planet 2 longitude).
    /// </returns>
    private (DateTime DateTimeUtc, double ResidualOrb, double Lon1, double Lon2) RefineExactTime(
        double jdLow, double deltaLow, double jdHigh,
        TransitBody body1, TransitBody body2, double targetAngle)
    {
        for (int i = 0; i < BisectionIterations; i++)
        {
            double jdMid = (jdLow + jdHigh) / 2.0;
            double lon1 = _ephemeris.GetLongitude(body1, jdMid);
            double lon2 = _ephemeris.GetLongitude(body2, jdMid);
            double deltaMid = SignedAspectSeparation(lon1, lon2, targetAngle);

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

        double jdExact = (jdLow + jdHigh) / 2.0;
        double lon1Exact = _ephemeris.GetLongitude(body1, jdExact);
        double lon2Exact = _ephemeris.GetLongitude(body2, jdExact);
        double residualOrb = Math.Abs(SignedAspectSeparation(lon1Exact, lon2Exact, targetAngle));

        return (_ephemeris.JulianDayToDateTime(jdExact), residualOrb, lon1Exact, lon2Exact);
    }

    /// <summary>
    /// Computes a continuous signed separation between two longitudes relative
    /// to a target angle. The result changes sign smoothly when the separation
    /// passes through the target angle, enabling reliable binary search.
    /// </summary>
    /// <remarks>
    /// The raw difference (lon1 − lon2) is normalized to [0, 360), then the
    /// delta from the target angle is normalized to (−180, 180]. This produces
    /// a single continuous function per target angle, avoiding discontinuities
    /// that would cause false zero crossings in the bisection algorithm.
    /// </remarks>
    private static double SignedAspectSeparation(double lon1, double lon2, double targetAngle)
    {
        // Normalize lon1 - lon2 to [0, 360)
        double normalized = ((lon1 - lon2) % 360.0 + 360.0) % 360.0;

        // Delta from target angle, normalized to (-180, 180]
        double delta = normalized - targetAngle;
        if (delta > 180.0) delta -= 360.0;
        if (delta < -180.0) delta += 360.0;

        return delta;
    }

    #endregion

    #region Formatting Helpers

    /// <summary>
    /// Formats a <see cref="TransitBody"/> enum to its display name.
    /// </summary>
    private static string FormatPlanetName(TransitBody body) => body switch
    {
        TransitBody.Jupiter => "Jupiter",
        TransitBody.Saturn => "Saturn",
        TransitBody.Uranus => "Uranus",
        TransitBody.Neptune => "Neptune",
        TransitBody.Pluto => "Pluto",
        TransitBody.Chiron => "Chiron",
        TransitBody.NorthNode => "North Node",
        TransitBody.Mars => "Mars",
        TransitBody.Mercury => "Mercury",
        TransitBody.Venus => "Venus",
        _ => body.ToString(),
    };

    /// <summary>
    /// Formats an ecliptic longitude as "DD°MM′ Sign" (e.g., "5°12′ Aquarius").
    /// </summary>
    private static string FormatPosition(double longitude)
    {
        int degrees = TransitEphemerisHelper.LongitudeToDegrees(longitude);
        int minutes = TransitEphemerisHelper.LongitudeToMinutes(longitude);
        string sign = TransitEphemerisHelper.LongitudeToSign(longitude);
        return string.Create(CultureInfo.InvariantCulture, $"{degrees}°{minutes:D2}' {sign}");
    }

    #endregion
}
