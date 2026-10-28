using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Follows an applying aspect between two moving bodies forward in time to
/// find out whether it actually reaches exact, and finds the first exact
/// major aspect between two bodies inside a window.
/// </summary>
/// <remarks>
/// <para>
/// <b>The function searched.</b> For bodies A and B and a signed target
/// angle <c>T</c>, <c>h(t) = norm180(lon_A(t) − lon_B(t) − T)</c>. The aspect
/// is exact when <c>h = 0</c>; <c>|h|</c> is its orb. <c>T</c> carries the
/// orientation the pair has at the chart (<see cref="SignedTarget"/>), so a
/// sextile with A 60° ahead of B and one with A 60° behind are followed as
/// different applications.
/// </para>
/// <para>
/// <b>First-approach semantics.</b> <see cref="FollowFirstApproach"/> samples
/// hourly from the start instant. The first sign change of <c>h</c> means the
/// aspect perfects, and the crossing is bisected on the ephemeris to one
/// second. If instead <c>|h|</c> rises <see cref="TurnaroundToleranceDegrees"/>
/// above its running minimum, the application has turned around: the pair
/// came no closer than that minimum, and the finder reports the minimum orb
/// and its hourly sample. A tangency — <c>|h|</c> dipping without crossing —
/// is therefore a failed application, never a perfection. Samples that differ
/// by more than 180° straddle the function's own wrap, not a crossing, and
/// are ignored. Nothing is extrapolated from a speed.
/// </para>
/// <para>
/// <b>The turnaround instant.</b> The closest hourly sample can sit up to half
/// an hour from the true minimum, so a turnaround also reports the instant of
/// closest approach, refined between the samples either side of that one by
/// golden-section search on <c>|h|</c>. The refinement moves only that time:
/// the outcome and the orb are those of the hourly samples, and the sampled
/// orb exceeds the refined minimum by at most 0.0001° over 2000–2030.
/// </para>
/// <para>
/// <b>Why not a linear estimate.</b> A speed sampled at the chart does not
/// survive a station: Venus two days before turning retrograde closes on
/// Mercury faster after the turn, not slower, and an application that fails
/// does so because the orb starts to grow, which no single speed can show.
/// Measured over 2000–2030, 94% of applying direct aspects perfect on their
/// current approach, the longest within 95 days; the rest turn around within
/// 71 days. <see cref="MaxFollowDays"/> is a safety cap above both.
/// </para>
/// </remarks>
public static class AspectPerfectionFinder
{
    /// <summary>Sampling step while following an application: one hour.</summary>
    public const double SampleStepDays = 1.0 / 24.0;

    /// <summary>Safety cap on how far an application is followed, in days.</summary>
    public const double MaxFollowDays = 120.0;

    /// <summary>
    /// How far, in degrees, the orb must rise above its running minimum before
    /// the application is judged to have turned around.
    /// </summary>
    public const double TurnaroundToleranceDegrees = 0.1;

    /// <summary>
    /// Bisection of a perfection, and the search for a failed application's
    /// closest approach, stop once the bracket is no wider than this, in days
    /// (one second).
    /// </summary>
    public const double TimePrecisionDays = 1.0 / 86400.0;

    /// <summary>The five Ptolemaic aspect angles.</summary>
    public static readonly IReadOnlyList<double> MajorAspectAngles = [0.0, 60.0, 90.0, 120.0, 180.0];

    /// <summary>
    /// Chooses the signed target angle for an aspect from the pair's current
    /// orientation: the sign of <c>norm180(lon_A − lon_B)</c> for the three
    /// two-sided angles, and the angle itself for conjunction and opposition.
    /// </summary>
    /// <param name="aspectAngle">The aspect angle: 0, 60, 90, 120 or 180.</param>
    /// <param name="longitudeA">Current longitude of body A, in degrees.</param>
    /// <param name="longitudeB">Current longitude of body B, in degrees.</param>
    /// <returns>The signed target angle to follow.</returns>
    public static double SignedTarget(double aspectAngle, double longitudeA, double longitudeB)
    {
        if (aspectAngle is 0.0 or 180.0)
            return aspectAngle;

        int sign = Math.Sign(Norm180(longitudeA - longitudeB));
        return (sign == 0 ? 1 : sign) * aspectAngle;
    }

    /// <summary>
    /// Follows the application of body A to body B at the signed target angle,
    /// from <paramref name="julianDayUt"/> forward, until it perfects, turns
    /// around, or reaches <see cref="MaxFollowDays"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris used for every longitude sample.</param>
    /// <param name="bodyA">The first body.</param>
    /// <param name="bodyB">The second body.</param>
    /// <param name="signedTargetAngle">The signed target angle, as from <see cref="SignedTarget"/>.</param>
    /// <param name="julianDayUt">The instant to follow from, as a Julian Day in Universal Time.</param>
    /// <returns>The fate of the application.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ephemeris"/> is null.</exception>
    public static AspectApproach FollowFirstApproach(
        IEphemerisService ephemeris, CelestialBodyId bodyA, CelestialBodyId bodyB,
        double signedTargetAngle, double julianDayUt)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);

        double h0 = H(ephemeris, bodyA, bodyB, signedTargetAngle, julianDayUt);
        double orbAtStart = Math.Abs(h0);

        if (h0 == 0.0)
            return Build(AspectApproachOutcome.Perfects, perfectionJd: julianDayUt, closestOrb: 0.0, closestJd: julianDayUt);

        double previousJd = julianDayUt;
        double previousH = h0;
        double runningMinimum = orbAtStart;
        double runningMinimumJd = julianDayUt;

        for (int k = 1; ; k++)
        {
            double jd = julianDayUt + k * SampleStepDays;
            if (jd > julianDayUt + MaxFollowDays)
                return Build(AspectApproachOutcome.Capped, perfectionJd: null, closestOrb: runningMinimum, closestJd: runningMinimumJd);

            double h = H(ephemeris, bodyA, bodyB, signedTargetAngle, jd);
            double orb = Math.Abs(h);

            if (h == 0.0)
                return Build(AspectApproachOutcome.Perfects, perfectionJd: jd, closestOrb: 0.0, closestJd: jd);

            if (Math.Abs(h - previousH) < 180.0 && Math.Sign(h) != Math.Sign(previousH))
            {
                double exactJd = Bisect(previousJd, jd, t => H(ephemeris, bodyA, bodyB, signedTargetAngle, t));
                return Build(AspectApproachOutcome.Perfects, perfectionJd: exactJd, closestOrb: 0.0, closestJd: exactJd);
            }

            if (orb < runningMinimum)
            {
                runningMinimum = orb;
                runningMinimumJd = jd;
            }
            else if (orb > runningMinimum + TurnaroundToleranceDegrees)
            {
                // The closest sample is within an hour of the true minimum, and the samples
                // either side of it are no closer, so the minimum lies between those two.
                double turnaroundJd = RefineMinimum(
                    Math.Max(julianDayUt, runningMinimumJd - SampleStepDays),
                    runningMinimumJd + SampleStepDays,
                    runningMinimumJd,
                    runningMinimum,
                    t => H(ephemeris, bodyA, bodyB, signedTargetAngle, t));
                return Build(AspectApproachOutcome.TurnsAround, perfectionJd: null, closestOrb: runningMinimum, closestJd: runningMinimumJd, turnaroundJd: turnaroundJd);
            }

            previousJd = jd;
            previousH = h;
        }

        AspectApproach Build(AspectApproachOutcome outcome, double? perfectionJd, double closestOrb, double closestJd, double? turnaroundJd = null) => new()
        {
            BodyA = bodyA,
            BodyB = bodyB,
            SignedTargetAngle = signedTargetAngle,
            StartJulianDay = julianDayUt,
            OrbAtStart = orbAtStart,
            Outcome = outcome,
            PerfectionJulianDay = perfectionJd,
            PerfectionTimeUtc = perfectionJd is null ? null : JulianDayUtility.ToDateTimeUtc(ephemeris, perfectionJd.Value),
            ClosestOrb = closestOrb,
            ClosestJulianDay = closestJd,
            ClosestTimeUtc = JulianDayUtility.ToDateTimeUtc(ephemeris, closestJd),
            TurnaroundJulianDay = turnaroundJd,
            TurnaroundTimeUtc = turnaroundJd is null ? null : JulianDayUtility.ToDateTimeUtc(ephemeris, turnaroundJd.Value),
        };
    }

    /// <summary>
    /// Finds the earliest exact Ptolemaic aspect between two bodies strictly
    /// after <paramref name="jdStart"/> and no later than <paramref name="jdEnd"/>,
    /// considering all five angles in both orientations.
    /// </summary>
    /// <param name="ephemeris">Ephemeris used for every longitude sample.</param>
    /// <param name="bodyA">The first body.</param>
    /// <param name="bodyB">The second body.</param>
    /// <param name="jdStart">Start of the window (Julian Day, UT); an aspect exact at this instant does not count.</param>
    /// <param name="jdEnd">End of the window (Julian Day, UT).</param>
    /// <returns>The earliest exact aspect in the window, or null if there is none.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ephemeris"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="jdEnd"/> is before <paramref name="jdStart"/>.</exception>
    public static ExactAspectEvent? FindFirstExactMajorAspect(
        IEphemerisService ephemeris, CelestialBodyId bodyA, CelestialBodyId bodyB,
        double jdStart, double jdEnd)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        if (jdEnd < jdStart)
            throw new ArgumentOutOfRangeException(nameof(jdEnd), jdEnd, "The window must not end before it starts.");

        double[] targets = [0.0, 60.0, -60.0, 90.0, -90.0, 120.0, -120.0, 180.0];
        double[] previous = new double[targets.Length];

        (double lonA, double lonB) = Longitudes(ephemeris, bodyA, bodyB, jdStart);
        for (int i = 0; i < targets.Length; i++)
            previous[i] = Norm180(lonA - lonB - targets[i]);

        double previousJd = jdStart;
        ExactAspectEvent? best = null;

        for (int k = 1; ; k++)
        {
            double jd = Math.Min(jdStart + k * SampleStepDays, jdEnd);
            if (jd <= previousJd)
                break;
            if (best is not null && previousJd >= best.JulianDay)
                break;

            (lonA, lonB) = Longitudes(ephemeris, bodyA, bodyB, jd);

            for (int i = 0; i < targets.Length; i++)
            {
                double h = Norm180(lonA - lonB - targets[i]);
                double p = previous[i];
                bool crosses = (h == 0.0 && p != 0.0)
                    || (Math.Abs(h - p) < 180.0 && p != 0.0 && Math.Sign(h) != Math.Sign(p));

                if (crosses)
                {
                    double target = targets[i];
                    double exactJd = h == 0.0
                        ? jd
                        : Bisect(previousJd, jd, t => H(ephemeris, bodyA, bodyB, target, t));

                    if (best is null || exactJd < best.JulianDay)
                    {
                        best = new ExactAspectEvent
                        {
                            BodyA = bodyA,
                            BodyB = bodyB,
                            SignedTargetAngle = target,
                            AspectAngle = Math.Abs(target),
                            AspectType = AspectTypeFor(Math.Abs(target)),
                            JulianDay = exactJd,
                            TimeUtc = JulianDayUtility.ToDateTimeUtc(ephemeris, exactJd),
                        };
                    }
                }

                previous[i] = h;
            }

            previousJd = jd;
            if (jd >= jdEnd)
                break;
        }

        return best;
    }

    /// <summary>
    /// Finds the first instant after <paramref name="jdStart"/>, and no later than
    /// <paramref name="jdEnd"/>, at which a body is no longer in the sign it
    /// occupies at <paramref name="jdStart"/> — whether it moves on into the next
    /// sign or, retrograde, back into the previous one.
    /// </summary>
    /// <remarks>
    /// The body is sampled every hour from the start, the last sample clamped to
    /// the window's end, and the first sample outside the sign is bisected
    /// against the last one inside it until the bracket is no wider than
    /// <see cref="TimePrecisionDays"/>. The instant returned is the bracket's
    /// end: the first one known to lie outside the sign. A crossing and return
    /// within the same hour — possible only for a planet stationing within
    /// seconds of arc of a sign boundary — would not be seen.
    /// </remarks>
    /// <param name="ephemeris">Ephemeris used for every longitude sample.</param>
    /// <param name="body">The body to follow.</param>
    /// <param name="jdStart">Start of the window (Julian Day, UT); the body's sign here is the one it must leave.</param>
    /// <param name="jdEnd">End of the window (Julian Day, UT).</param>
    /// <returns>The exit instant (Julian Day, UT), or null if the body stays in its sign throughout the window.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ephemeris"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="jdEnd"/> is before <paramref name="jdStart"/>.</exception>
    public static double? FindSignExit(
        IEphemerisService ephemeris, CelestialBodyId body, double jdStart, double jdEnd)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        if (jdEnd < jdStart)
            throw new ArgumentOutOfRangeException(nameof(jdEnd), jdEnd, "The window must not end before it starts.");

        int startSign = SignIndex(ephemeris, body, jdStart);
        double insideJd = jdStart;

        for (int k = 1; ; k++)
        {
            double jd = Math.Min(jdStart + k * SampleStepDays, jdEnd);
            if (jd <= insideJd)
                return null;

            if (SignIndex(ephemeris, body, jd) != startSign)
            {
                double outsideJd = jd;
                while (outsideJd - insideJd > TimePrecisionDays)
                {
                    double mid = (insideJd + outsideJd) / 2.0;
                    if (SignIndex(ephemeris, body, mid) == startSign)
                        insideJd = mid;
                    else
                        outsideJd = mid;
                }

                return outsideJd;
            }

            insideJd = jd;
            if (jd >= jdEnd)
                return null;
        }
    }

    /// <summary>The 0-based index (0 = Aries … 11 = Pisces) of the sign a body occupies at an instant.</summary>
    private static int SignIndex(IEphemerisService ephemeris, CelestialBodyId body, double jd)
    {
        double longitude = ephemeris.CalculatePositionLongitudeOnly(jd, body).Longitude;
        return (int)((((longitude % 360.0) + 360.0) % 360.0) / 30.0);
    }

    /// <summary>Maps a Ptolemaic angle to its <see cref="AspectType"/>.</summary>
    /// <param name="aspectAngle">0, 60, 90, 120 or 180.</param>
    /// <returns>The matching aspect type.</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown for any other angle.</exception>
    public static AspectType AspectTypeFor(double aspectAngle) => aspectAngle switch
    {
        0.0 => AspectType.Conjunction,
        60.0 => AspectType.Sextile,
        90.0 => AspectType.Square,
        120.0 => AspectType.Trine,
        180.0 => AspectType.Opposition,
        _ => throw new ArgumentOutOfRangeException(nameof(aspectAngle), aspectAngle, "Not a Ptolemaic aspect angle."),
    };

    private static (double LonA, double LonB) Longitudes(IEphemerisService ephemeris, CelestialBodyId a, CelestialBodyId b, double jd) =>
        (ephemeris.CalculatePositionLongitudeOnly(jd, a).Longitude,
         ephemeris.CalculatePositionLongitudeOnly(jd, b).Longitude);

    private static double H(IEphemerisService ephemeris, CelestialBodyId a, CelestialBodyId b, double target, double jd)
    {
        (double lonA, double lonB) = Longitudes(ephemeris, a, b, jd);
        return Norm180(lonA - lonB - target);
    }

    /// <summary>
    /// Bisects <paramref name="f"/> on a sign change between two instants until
    /// the bracket is no wider than <see cref="TimePrecisionDays"/>. A midpoint
    /// that evaluates to exactly zero is returned at once.
    /// </summary>
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

    /// <summary>
    /// Narrows the instant at which <c>|f|</c> is least, between two instants
    /// that bracket it, by golden-section search until the bracket is no wider
    /// than <see cref="TimePrecisionDays"/>. If <c>|f|</c> there is larger than
    /// at the sample the bracket was built around, the sample is returned
    /// instead, so the result never has a larger <c>|f|</c> than the sample.
    /// </summary>
    private static double RefineMinimum(
        double earlierJd, double laterJd, double sampleJd, double sampleValue, Func<double, double> f)
    {
        const double InverseGoldenRatio = 0.6180339887498949;
        const int MaxIterations = 64;

        double lowerProbeJd = laterJd - InverseGoldenRatio * (laterJd - earlierJd);
        double upperProbeJd = earlierJd + InverseGoldenRatio * (laterJd - earlierJd);
        double lowerProbeValue = Math.Abs(f(lowerProbeJd));
        double upperProbeValue = Math.Abs(f(upperProbeJd));

        for (int i = 0; i < MaxIterations && laterJd - earlierJd > TimePrecisionDays; i++)
        {
            if (lowerProbeValue <= upperProbeValue)
            {
                laterJd = upperProbeJd;
                upperProbeJd = lowerProbeJd;
                upperProbeValue = lowerProbeValue;
                lowerProbeJd = laterJd - InverseGoldenRatio * (laterJd - earlierJd);
                lowerProbeValue = Math.Abs(f(lowerProbeJd));
            }
            else
            {
                earlierJd = lowerProbeJd;
                lowerProbeJd = upperProbeJd;
                lowerProbeValue = upperProbeValue;
                upperProbeJd = earlierJd + InverseGoldenRatio * (laterJd - earlierJd);
                upperProbeValue = Math.Abs(f(upperProbeJd));
            }
        }

        double refinedJd = (earlierJd + laterJd) / 2.0;
        return Math.Abs(f(refinedJd)) <= sampleValue ? refinedJd : sampleJd;
    }

    /// <summary>Normalises an angle to (−180, 180].</summary>
    private static double Norm180(double degrees)
    {
        double x = ((degrees % 360.0) + 360.0) % 360.0;
        return x > 180.0 ? x - 360.0 : x;
    }
}

/// <summary>How an application ended when followed by <see cref="AspectPerfectionFinder.FollowFirstApproach"/>.</summary>
public enum AspectApproachOutcome
{
    /// <summary>The orb reached zero: the aspect perfected on this approach.</summary>
    Perfects,

    /// <summary>The orb turned around before reaching zero: the application failed.</summary>
    TurnsAround,

    /// <summary>Neither happened within <see cref="AspectPerfectionFinder.MaxFollowDays"/>.</summary>
    Capped,
}

/// <summary>The fate of one application of body A to body B at a signed target angle.</summary>
public sealed class AspectApproach
{
    /// <summary>The first body.</summary>
    public required CelestialBodyId BodyA { get; init; }

    /// <summary>The second body.</summary>
    public required CelestialBodyId BodyB { get; init; }

    /// <summary>The signed target angle that was followed.</summary>
    public required double SignedTargetAngle { get; init; }

    /// <summary>The instant the application was followed from (Julian Day, UT).</summary>
    public required double StartJulianDay { get; init; }

    /// <summary>The orb at the start instant, in degrees.</summary>
    public required double OrbAtStart { get; init; }

    /// <summary>How the application ended.</summary>
    public required AspectApproachOutcome Outcome { get; init; }

    /// <summary>True when the aspect perfected on this approach.</summary>
    public bool Perfects => Outcome == AspectApproachOutcome.Perfects;

    /// <summary>Julian Day (UT) of exactness, to one second; null unless the aspect perfects.</summary>
    public double? PerfectionJulianDay { get; init; }

    /// <summary>UTC instant of exactness, rounded to the nearest second; null unless the aspect perfects.</summary>
    public DateTime? PerfectionTimeUtc { get; init; }

    /// <summary>
    /// The smallest orb reached, in degrees: zero when the aspect perfects,
    /// otherwise the closest the pair came before turning around (or before the cap).
    /// When the application fails this is the orb at the closest hourly sample,
    /// which can exceed the minimum at <see cref="TurnaroundJulianDay"/> by up to
    /// about 0.0001°.
    /// </summary>
    public required double ClosestOrb { get; init; }

    /// <summary>
    /// Julian Day (UT) at which <see cref="ClosestOrb"/> was reached; hourly resolution when the application fails.
    /// A turnaround's refined instant of closest approach is <see cref="TurnaroundJulianDay"/>.
    /// </summary>
    public required double ClosestJulianDay { get; init; }

    /// <summary>UTC instant at which <see cref="ClosestOrb"/> was reached.</summary>
    public required DateTime ClosestTimeUtc { get; init; }

    /// <summary>
    /// Julian Day (UT) of the closest approach of an application that turns
    /// around, refined between the hourly samples either side of
    /// <see cref="ClosestJulianDay"/> by golden-section search on the orb until
    /// the bracket is no wider than <see cref="AspectPerfectionFinder.TimePrecisionDays"/>;
    /// null unless <see cref="Outcome"/> is <see cref="AspectApproachOutcome.TurnsAround"/>.
    /// </summary>
    /// <remarks>
    /// Only the reported time is refined: <see cref="Outcome"/>,
    /// <see cref="ClosestOrb"/> and <see cref="ClosestJulianDay"/> still come from
    /// the hourly samples, so anything bounded by them — the horary analysis ends
    /// its station window at <see cref="ClosestJulianDay"/> — is unchanged by it.
    /// </remarks>
    public double? TurnaroundJulianDay { get; init; }

    /// <summary>
    /// UTC instant of <see cref="TurnaroundJulianDay"/>, rounded to the nearest
    /// second; null unless the application turns around.
    /// </summary>
    public DateTime? TurnaroundTimeUtc { get; init; }
}

/// <summary>An exact Ptolemaic aspect between two bodies, as found by <see cref="AspectPerfectionFinder.FindFirstExactMajorAspect"/>.</summary>
public sealed class ExactAspectEvent
{
    /// <summary>The first body.</summary>
    public required CelestialBodyId BodyA { get; init; }

    /// <summary>The second body.</summary>
    public required CelestialBodyId BodyB { get; init; }

    /// <summary>The aspect angle: 0, 60, 90, 120 or 180.</summary>
    public required double AspectAngle { get; init; }

    /// <summary>The signed target that was crossed: positive when A is ahead of B by the angle, negative when behind.</summary>
    public required double SignedTargetAngle { get; init; }

    /// <summary>The aspect type.</summary>
    public required AspectType AspectType { get; init; }

    /// <summary>Julian Day (UT) of exactness, to one second.</summary>
    public required double JulianDay { get; init; }

    /// <summary>UTC instant of exactness, rounded to the nearest second.</summary>
    public required DateTime TimeUtc { get; init; }
}
