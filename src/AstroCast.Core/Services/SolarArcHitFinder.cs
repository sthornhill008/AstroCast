using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.SolarArcs;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Finds the dated, exact solar arc hits in a period: every instant at which a natal point directed
/// by the solar arc makes an exact aspect to another natal point, or lands on a natal midpoint's 45°
/// dial position (branch Arcs, 1.4.0). Not yet wired into the transit report.
/// </summary>
/// <remarks>
/// <para>
/// The solar arc is the progressed Sun less the natal Sun, the progressed Sun read at the natal Julian
/// Day plus the elapsed days over 365.25 (<see cref="ProgressedDateCalculator"/>'s key), as
/// <see cref="ProgressionService"/> reads it. The arc never shrinks (the progressed Sun is never
/// retrograde) and grows about a degree a year, so instead of scanning, the finder inverts it: for every
/// directed point i and target the arc values A at which the contact is exact are
/// (target − natal_i) mod 360 for an aspect point, and (midpoint − natal_i) mod 45 plus multiples of 45°
/// for a midpoint on the 45° dial; the arcs at the window's two ends bracket the A's that fall in the
/// window, and the instant of each is bisected on the arc as a function of time until the bracket is
/// under a second. One crossing per A, so nothing is missed or listed twice.
/// </para>
/// <para>
/// The arc is unwrapped: a chart more than about 365 years old has an arc past 360°, and the window's
/// arcs are read as the wrapped arc at its start plus the progressed Sun's signed travel across the window
/// (so a window may not exceed <see cref="MaxWindowDays"/>), the whole revolutions counted from the Sun's
/// mean motion, which never strays more than a few degrees from the true one.
/// </para>
/// <para>
/// The aspects are the five majors with the semisquare and sesquiquadrate; a directed point never aspects
/// its own natal place, since every point would, at once, whenever the arc is an aspect. The midpoints are
/// the chart's own (<see cref="ChartResult.Midpoints"/>, every pair of the 12 bodies, the Ascendant and
/// the Midheaven), those holding the directed point included, as the transit activations take them. The
/// window is every instant whose UT date lies in [start, end]: from the start's 00:00 UT to the day after
/// the end's 00:00 UT, exclusive.
/// </para>
/// </remarks>
public sealed class SolarArcHitFinder
{
    #region Constants

    /// <summary>An aspect of the solar arc series: its name, abbreviation and angle.</summary>
    /// <param name="Name">The aspect's name.</param>
    /// <param name="Abbreviation">The aspect's abbreviation.</param>
    /// <param name="Angle">The aspect's angle in degrees.</param>
    private readonly record struct AspectDef(string Name, string Abbreviation, double Angle);

    /// <summary>The seven aspects a directed point makes to a natal point: the majors and the 45° series.</summary>
    private static readonly AspectDef[] Aspects =
    [
        new("Conjunction", "cnj", 0.0),
        new("Semisquare", "ssq", 45.0),
        new("Sextile", "sxt", 60.0),
        new("Square", "sqr", 90.0),
        new("Trine", "tri", 120.0),
        new("Sesquiquadrate", "ses", 135.0),
        new("Opposition", "opp", 180.0),
    ];

    /// <summary>The harmonics of the 45° dial a midpoint contact stands at, by angle.</summary>
    private static readonly AspectDef[] DialHarmonics =
    [
        new("Conjunction", "cnj", 0.0),
        new("Semisquare", "ssq", 45.0),
        new("Square", "sqr", 90.0),
        new("Sesquiquadrate", "ses", 135.0),
        new("Opposition", "opp", 180.0),
    ];

    /// <summary>The 45° dial's period.</summary>
    private const double DialDegrees = 45.0;

    /// <summary>The Sun's mean daily motion, 360° over a year of 365.25 days: counts the arc's whole revolutions.</summary>
    private const double SunMeanDailyMotion = 360.0 / 365.25;

    /// <summary>
    /// The longest window, in days: 100 years, over which the progressed Sun travels about 100°, far
    /// inside the half-turn its signed travel across the window is read as.
    /// </summary>
    private const int MaxWindowDays = 36_525;

    /// <summary>The bisection (<see cref="Bisection.Solve"/>) stops when its bracket is under a second.</summary>
    private const double SecondInDays = 1.0 / 86_400.0;

    #endregion

    private readonly IEphemerisService _ephemeris;

    /// <summary>
    /// Initialises a new instance of <see cref="SolarArcHitFinder"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for the progressed Sun.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ephemeris"/> is null.</exception>
    public SolarArcHitFinder(IEphemerisService ephemeris)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
    }

    /// <summary>
    /// Finds every solar arc hit whose UT date lies in the period: every directed point to every other of the
    /// 12 natal bodies, the Ascendant and the Midheaven, and to the chart's natal midpoints on the 45° dial.
    /// </summary>
    /// <param name="natalChart">The natal chart, with its positions, angles and midpoints.</param>
    /// <param name="start">The period's first day, a proleptic day (a typed bound read through <see cref="Formatting.CalendarDateFormatter.ProlepticDay"/>).</param>
    /// <param name="end">The period's last day, a proleptic day, on or after <paramref name="start"/>.</param>
    /// <returns>The hits, sorted by instant.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="natalChart"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="end"/> is before <paramref name="start"/>, or the period is longer than
    /// <see cref="MaxWindowDays"/>.
    /// </exception>
    public IReadOnlyList<SolarArcHit> FindHits(ChartResult natalChart, DateOnly start, DateOnly end)
    {
        ArgumentNullException.ThrowIfNull(natalChart);

        return FindHits(
            natalChart, start, end,
            MidpointService.BuildPointList(natalChart.Positions, natalChart.Houses), includeMidpoints: true);
    }

    /// <summary>
    /// Finds every solar arc hit whose UT date lies in the period to a list of natal targets of the caller's
    /// (branch AsteroidsTiming, 1.6.0; R7): the natal Ceres, Pallas, Juno, Vesta and Lilith, as
    /// <see cref="TransitService"/> passes them. The directed points are still the chart's 14, the aspects, the
    /// inversion and the window those of <see cref="FindHits(ChartResult, DateOnly, DateOnly)"/>; no midpoint
    /// contact is sought, and a directed point still skips a target of its own name.
    /// </summary>
    /// <param name="natalChart">The natal chart, with its positions and angles.</param>
    /// <param name="start">The period's first day, a proleptic day (a typed bound read through <see cref="Formatting.CalendarDateFormatter.ProlepticDay"/>).</param>
    /// <param name="end">The period's last day, a proleptic day, on or after <paramref name="start"/>.</param>
    /// <param name="natalTargets">The natal targets: each point's name and ecliptic longitude.</param>
    /// <returns>The hits, sorted by instant, none of them a midpoint contact.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="natalChart"/> or <paramref name="natalTargets"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="end"/> is before <paramref name="start"/>, or the period is longer than
    /// <see cref="MaxWindowDays"/>.
    /// </exception>
    public IReadOnlyList<SolarArcHit> FindHits(
        ChartResult natalChart, DateOnly start, DateOnly end, IReadOnlyList<(string Name, double Longitude)> natalTargets)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        ArgumentNullException.ThrowIfNull(natalTargets);

        return FindHits(natalChart, start, end, natalTargets, includeMidpoints: false);
    }

    /// <summary>
    /// Finds every solar arc hit whose UT date lies in the period: the chart's 14 points directed by the arc to
    /// the targets given, and, when asked, to the chart's natal midpoints on the 45° dial.
    /// </summary>
    /// <param name="natalChart">The natal chart.</param>
    /// <param name="start">The period's first day, a proleptic day.</param>
    /// <param name="end">The period's last day, a proleptic day.</param>
    /// <param name="natalTargets">The natal targets: each point's name and ecliptic longitude.</param>
    /// <param name="includeMidpoints">Whether the chart's natal midpoints are targets too.</param>
    /// <returns>The hits, sorted by instant.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="end"/> is before <paramref name="start"/>, or the period is longer than
    /// <see cref="MaxWindowDays"/>.
    /// </exception>
    private IReadOnlyList<SolarArcHit> FindHits(
        ChartResult natalChart, DateOnly start, DateOnly end,
        IReadOnlyList<(string Name, double Longitude)> natalTargets, bool includeMidpoints)
    {
        if (end < start)
            throw new ArgumentOutOfRangeException(nameof(end), end, "The period's end is before its start.");
        if (end.DayNumber - start.DayNumber >= MaxWindowDays)
            throw new ArgumentOutOfRangeException(nameof(end), end, "The period is longer than 100 years.");

        IReadOnlyList<(string Name, double Longitude)> points = MidpointService.BuildPointList(natalChart.Positions, natalChart.Houses);
        double natalJd = natalChart.JulianDay;
        double natalSun = points.Single(p => p.Name == "Sun").Longitude;

        // The window: the start's 00:00 UT to the day after the end's, exclusive; both proleptic days, by arithmetic
        double windowStart = JulianDayUtility.InstantToJulianDay(start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        double windowEnd = JulianDayUtility.InstantToJulianDay(end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        // The arc across the window: the wrapped arc at the start, then the Sun's signed travel from there
        double sunAtStart = ProgressedSun(natalJd, windowStart);
        double arcAtStart = Normalize(sunAtStart - natalSun);
        double arcAtEnd = arcAtStart + WrapSigned(ProgressedSun(natalJd, windowEnd) - sunAtStart);
        double ArcAt(double jd) => arcAtStart + WrapSigned(ProgressedSun(natalJd, jd) - sunAtStart);

        // The whole revolutions the wrapped arc has dropped, from the Sun's mean motion over the progressed days
        double progressedDays = ProgressedDateCalculator.RealDaysToProgressedDays(windowStart - natalJd);
        double revolutions = Math.Round((SunMeanDailyMotion * progressedDays - arcAtStart) / 360.0);
        double unwrapOffset = 360.0 * revolutions;

        List<SolarArcHit> hits = [];

        foreach ((string directedName, double directedLongitude) in points)
        {
            // Aspects to the natal targets: the arc at which the directed point stands at target ± angle
            foreach ((string targetName, double targetLongitude) in natalTargets)
            {
                if (string.Equals(targetName, directedName, StringComparison.Ordinal))
                    continue;

                foreach (AspectDef aspect in Aspects)
                {
                    foreach (double signedAngle in SignedAngles(aspect.Angle))
                    {
                        double arcValue = Normalize(targetLongitude + signedAngle - directedLongitude);
                        foreach (double arc in ArcsInWindow(arcValue, 360.0, arcAtStart, arcAtEnd))
                        {
                            double jd = Bisection.Solve(ArcAt, arc, windowStart, windowEnd, SecondInDays);
                            hits.Add(new SolarArcHit
                            {
                                DirectedPoint = directedName,
                                AspectAngle = aspect.Angle,
                                AspectName = aspect.Name,
                                AspectAbbreviation = aspect.Abbreviation,
                                Target = targetName,
                                Arc = arc + unwrapOffset,
                                JulianDayUt = jd,
                                InstantUtc = JulianDayUtility.JulianDayToInstant(jd),
                            });
                        }
                    }
                }
            }

            // The natal midpoints on the 45° dial: the arc at which the directed point stands at m + 45k
            if (!includeMidpoints)
                continue;

            foreach (Midpoint midpoint in natalChart.Midpoints)
            {
                double dialValue = Normalize(midpoint.Longitude - directedLongitude) % DialDegrees;
                foreach (double arc in ArcsInWindow(dialValue, DialDegrees, arcAtStart, arcAtEnd))
                {
                    double jd = Bisection.Solve(ArcAt, arc, windowStart, windowEnd, SecondInDays);
                    double separation = Math.Abs(WrapSigned(directedLongitude + arc - midpoint.Longitude));
                    AspectDef harmonic = DialHarmonics[(int)Math.Round(separation / DialDegrees)];
                    hits.Add(new SolarArcHit
                    {
                        DirectedPoint = directedName,
                        AspectAngle = harmonic.Angle,
                        AspectName = harmonic.Name,
                        AspectAbbreviation = harmonic.Abbreviation,
                        Target = midpoint.Notation,
                        MidpointBody1 = midpoint.Body1Name,
                        MidpointBody2 = midpoint.Body2Name,
                        Arc = arc + unwrapOffset,
                        JulianDayUt = jd,
                        InstantUtc = JulianDayUtility.JulianDayToInstant(jd),
                    });
                }
            }
        }

        hits.Sort((a, b) => a.JulianDayUt.CompareTo(b.JulianDayUt));
        return hits.AsReadOnly();
    }

    #region Private

    /// <summary>The signed angles an aspect is made at: both sides for an aspect between 0° and 180°, one for those two.</summary>
    /// <param name="angle">The aspect's angle in degrees.</param>
    /// <returns>The angles to add to the target's longitude.</returns>
    private static IEnumerable<double> SignedAngles(double angle)
    {
        yield return angle;
        if (angle > 0.0 && angle < 180.0)
            yield return -angle;
    }

    /// <summary>
    /// The arc values congruent to <paramref name="value"/> modulo <paramref name="period"/> that lie in
    /// [<paramref name="arcAtStart"/>, <paramref name="arcAtEnd"/>).
    /// </summary>
    /// <param name="value">The residue, in [0, period).</param>
    /// <param name="period">360° for an aspect point, 45° for the dial.</param>
    /// <param name="arcAtStart">The arc at the window's start.</param>
    /// <param name="arcAtEnd">The arc at the window's end.</param>
    /// <returns>The arc values, ascending.</returns>
    private static IEnumerable<double> ArcsInWindow(double value, double period, double arcAtStart, double arcAtEnd)
    {
        double arc = value + period * Math.Ceiling((arcAtStart - value) / period);
        for (; arc < arcAtEnd; arc += period)
        {
            if (arc >= arcAtStart)
                yield return arc;
        }
    }

    /// <summary>The progressed Sun's longitude for a real instant: the Sun at the natal Julian Day plus the elapsed days over 365.25.</summary>
    /// <param name="natalJd">The natal Julian Day (UT).</param>
    /// <param name="jd">The real Julian Day (UT).</param>
    /// <returns>The progressed Sun's ecliptic longitude.</returns>
    private double ProgressedSun(double natalJd, double jd)
    {
        double progressedJd = natalJd + ProgressedDateCalculator.RealDaysToProgressedDays(jd - natalJd);
        return _ephemeris.CalculatePositionLongitudeOnly(progressedJd, CelestialBodyId.Sun).Longitude;
    }

    /// <summary>A longitude brought into [0°, 360°).</summary>
    private static double Normalize(double longitude) => ((longitude % 360.0) + 360.0) % 360.0;

    /// <summary>An angle wrapped to [−180°, 180°).</summary>
    private static double WrapSigned(double angle) => Normalize(angle + 180.0) - 180.0;

    #endregion
}
