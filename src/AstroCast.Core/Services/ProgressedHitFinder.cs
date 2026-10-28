using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Progressions;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Finds the dated, exact secondary progression hits in a period: every instant at which a progressed
/// body or angle makes an exact major aspect to a natal point (branch Arcs, 1.4.0). Not yet wired into
/// the transit report.
/// </summary>
/// <remarks>
/// <para>
/// The progressed points are the Sun, Moon, Mercury, Venus and Mars at the progressed instant (the natal
/// Julian Day plus the elapsed days over 365.25, <see cref="ProgressedDateCalculator"/>'s key), and the
/// Midheaven and Ascendant by solar arc as <see cref="ProgressionService"/> casts them (ruling T-D1): the
/// natal Midheaven plus the arc, and the Ascendant for that Midheaven at the birth latitude with the natal
/// obliquity. Mercury, Venus and Mars can station in progressed time, so the points are scanned, not
/// inverted: each is sampled at 00:00 UT on every day from the day before the window to the day after it,
/// a hit is a change of sign of the signed separation to an aspect point between two samples both within
/// 90° of it (the transit pass rule, which keeps the wrap at the point opposite out), and the instant is
/// bisected to within a second. The fastest point, the Moon, moves under 0.05° a real day, so a daily
/// grid cannot step over a crossing; a station that crosses a point twice within one day is not seen.
/// </para>
/// <para>
/// The aspects are the five majors to the 12 natal bodies, the Ascendant and the Midheaven. A body may
/// aspect its own natal place (the progressed Moon's return to the natal Moon, the progressed Sun's
/// sextile to the natal Sun); the progressed angles skip theirs, the progressed Midheaven being the solar
/// arc Midheaven, whose aspect to itself is the arc. The window is every instant whose UT date lies in
/// [start, end]: from the start's 00:00 UT to the day after the end's 00:00 UT, exclusive.
/// </para>
/// </remarks>
public sealed class ProgressedHitFinder
{
    #region Constants

    /// <summary>An aspect: its name, abbreviation and angle.</summary>
    /// <param name="Name">The aspect's name.</param>
    /// <param name="Abbreviation">The aspect's abbreviation.</param>
    /// <param name="Angle">The aspect's angle in degrees.</param>
    private readonly record struct AspectDef(string Name, string Abbreviation, double Angle);

    /// <summary>The five major aspects.</summary>
    private static readonly AspectDef[] MajorAspects =
    [
        new("Conjunction", "cnj", 0.0),
        new("Sextile", "sxt", 60.0),
        new("Square", "sqr", 90.0),
        new("Trine", "tri", 120.0),
        new("Opposition", "opp", 180.0),
    ];

    /// <summary>The progressed bodies, in the order the hits name them.</summary>
    private static readonly (CelestialBodyId Id, string Name)[] ProgressedBodies =
    [
        (CelestialBodyId.Sun, "Sun"),
        (CelestialBodyId.Moon, "Moon"),
        (CelestialBodyId.Mercury, "Mercury"),
        (CelestialBodyId.Venus, "Venus"),
        (CelestialBodyId.Mars, "Mars"),
    ];

    /// <summary>The progressed angles, directed by the solar arc.</summary>
    private static readonly string[] ProgressedAngles = ["ASC", "MC"];

    /// <summary>
    /// A sign change counts only when both samples lie within this many degrees of the aspect point:
    /// the guard against the wrap of the signed separation at the point opposite.
    /// </summary>
    private const double PassGuardDegrees = 90.0;

    /// <summary>The bisection stops when its bracket is under a second.</summary>
    private const double SecondInDays = 1.0 / 86_400.0;

    #endregion

    private readonly IEphemerisService _ephemeris;
    private readonly ProgressionService _progressions;

    /// <summary>
    /// Initialises a new instance of <see cref="ProgressedHitFinder"/>.
    /// </summary>
    /// <param name="ephemeris">Ephemeris service for the progressed bodies and the Ascendant.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ephemeris"/> is null.</exception>
    public ProgressedHitFinder(IEphemerisService ephemeris)
    {
        ArgumentNullException.ThrowIfNull(ephemeris);
        _ephemeris = ephemeris;
        _progressions = new ProgressionService(ephemeris);
    }

    /// <summary>
    /// Finds every progressed hit whose UT date lies in the period: the progressed points to the 12 natal
    /// bodies, the Ascendant and the Midheaven.
    /// </summary>
    /// <param name="natalChart">The natal chart, with its positions, angles and obliquity.</param>
    /// <param name="start">The period's first day, a proleptic day (a typed bound read through <see cref="Formatting.CalendarDateFormatter.ProlepticDay"/>).</param>
    /// <param name="end">The period's last day, a proleptic day, on or after <paramref name="start"/>.</param>
    /// <returns>The hits, sorted by instant.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="natalChart"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="end"/> is before <paramref name="start"/>.</exception>
    public IReadOnlyList<ProgressedHit> FindHits(ChartResult natalChart, DateOnly start, DateOnly end)
    {
        ArgumentNullException.ThrowIfNull(natalChart);

        return FindHits(natalChart, start, end, MidpointService.BuildPointList(natalChart.Positions, natalChart.Houses));
    }

    /// <summary>
    /// Finds every progressed hit whose UT date lies in the period to a list of natal targets of the caller's
    /// (branch AsteroidsTiming, 1.6.0; R7): the natal Ceres, Pallas, Juno, Vesta and Lilith, as
    /// <see cref="TransitService"/> passes them. The progressed points, the aspects, the grid and the window are
    /// those of <see cref="FindHits(ChartResult, DateOnly, DateOnly)"/>; a progressed angle still skips a target
    /// of its own name.
    /// </summary>
    /// <param name="natalChart">The natal chart, with its positions, angles and obliquity.</param>
    /// <param name="start">The period's first day, a proleptic day (a typed bound read through <see cref="Formatting.CalendarDateFormatter.ProlepticDay"/>).</param>
    /// <param name="end">The period's last day, a proleptic day, on or after <paramref name="start"/>.</param>
    /// <param name="natalPoints">The natal targets: each point's name and ecliptic longitude.</param>
    /// <returns>The hits, sorted by instant.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="natalChart"/> or <paramref name="natalPoints"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="end"/> is before <paramref name="start"/>.</exception>
    public IReadOnlyList<ProgressedHit> FindHits(
        ChartResult natalChart, DateOnly start, DateOnly end, IReadOnlyList<(string Name, double Longitude)> natalPoints)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        ArgumentNullException.ThrowIfNull(natalPoints);
        if (end < start)
            throw new ArgumentOutOfRangeException(nameof(end), end, "The period's end is before its start.");

        // The window: the start's 00:00 UT to the day after the end's, exclusive; both proleptic days, by arithmetic
        double windowStart = JulianDayUtility.InstantToJulianDay(start.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));
        double windowEnd = JulianDayUtility.InstantToJulianDay(end.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

        // The grid: 00:00 UT of every day from the day before the window to the day after it
        int sampleCount = (int)Math.Round(windowEnd - windowStart) + 3;
        double[] grid = new double[sampleCount];
        for (int i = 0; i < sampleCount; i++)
            grid[i] = windowStart - 1.0 + i;

        string[] pointNames = [.. ProgressedBodies.Select(b => b.Name), .. ProgressedAngles];
        Dictionary<string, double[]> samples = pointNames.ToDictionary(n => n, _ => new double[sampleCount], StringComparer.Ordinal);
        for (int i = 0; i < sampleCount; i++)
        {
            IReadOnlyDictionary<string, double> progressed = ProgressedPointsAt(natalChart, grid[i]);
            foreach (string name in pointNames)
                samples[name][i] = progressed[name];
        }

        List<ProgressedHit> hits = [];

        foreach (string pointName in pointNames)
        {
            bool isAngle = Array.IndexOf(ProgressedAngles, pointName) >= 0;
            double[] pointSamples = samples[pointName];

            foreach ((string natalName, double natalLongitude) in natalPoints)
            {
                // The progressed angles skip their own natal place; a body may aspect its own
                if (isAngle && string.Equals(natalName, pointName, StringComparison.Ordinal))
                    continue;

                foreach (AspectDef aspect in MajorAspects)
                {
                    foreach (double signedAngle in SignedAngles(aspect.Angle))
                    {
                        double aspectPoint = natalLongitude + signedAngle;
                        double Separation(double jd) => WrapSigned(ProgressedPointAt(natalChart, pointName, jd) - aspectPoint);

                        double previous = WrapSigned(pointSamples[0] - aspectPoint);
                        for (int i = 1; i < sampleCount; i++)
                        {
                            double current = WrapSigned(pointSamples[i] - aspectPoint);
                            if (IsPass(previous, current))
                            {
                                double jd = Bisect(Separation, grid[i - 1], grid[i], previous);
                                if (jd >= windowStart && jd < windowEnd)
                                {
                                    hits.Add(new ProgressedHit
                                    {
                                        ProgressedPoint = pointName,
                                        AspectAngle = aspect.Angle,
                                        AspectName = aspect.Name,
                                        AspectAbbreviation = aspect.Abbreviation,
                                        NatalPoint = natalName,
                                        JulianDayUt = jd,
                                        InstantUtc = JulianDayUtility.JulianDayToInstant(jd),
                                    });
                                }
                            }

                            previous = current;
                        }
                    }
                }
            }
        }

        hits.Sort((a, b) => a.JulianDayUt.CompareTo(b.JulianDayUt));
        return hits.AsReadOnly();
    }

    #region Private

    /// <summary>The signed angles an aspect is made at: both sides for an aspect between 0° and 180°, one for those two.</summary>
    /// <param name="angle">The aspect's angle in degrees.</param>
    /// <returns>The angles to add to the natal point's longitude.</returns>
    private static IEnumerable<double> SignedAngles(double angle)
    {
        yield return angle;
        if (angle > 0.0 && angle < 180.0)
            yield return -angle;
    }

    /// <summary>
    /// Whether two consecutive signed separations straddle the aspect point: a change of sign with both
    /// within <see cref="PassGuardDegrees"/> of it.
    /// </summary>
    /// <param name="previous">The separation at the earlier sample.</param>
    /// <param name="current">The separation at the later sample.</param>
    /// <returns>Whether a pass lies between the samples.</returns>
    private static bool IsPass(double previous, double current)
    {
        if (Math.Abs(previous) >= PassGuardDegrees || Math.Abs(current) >= PassGuardDegrees)
            return false;

        return (previous > 0.0 && current <= 0.0) || (previous < 0.0 && current >= 0.0);
    }

    /// <summary>
    /// The instant at which the separation changes sign between two samples: bisection keeping the earlier
    /// sample's sign on the low side, until the bracket is under a second.
    /// </summary>
    /// <param name="separation">The signed separation as a function of the Julian Day.</param>
    /// <param name="low">The earlier sample's Julian Day.</param>
    /// <param name="high">The later sample's Julian Day.</param>
    /// <param name="separationAtLow">The separation at <paramref name="low"/>.</param>
    /// <returns>The Julian Day (UT) of the pass.</returns>
    private static double Bisect(Func<double, double> separation, double low, double high, double separationAtLow)
    {
        bool lowIsPositive = separationAtLow > 0.0;
        while (high - low > SecondInDays)
        {
            double mid = (low + high) / 2.0;
            double atMid = separation(mid);
            if (atMid != 0.0 && (atMid > 0.0) == lowIsPositive)
                low = mid;
            else
                high = mid;
        }

        return (low + high) / 2.0;
    }

    /// <summary>The progressed Julian Day for a real instant: the natal Julian Day plus the elapsed days over 365.25.</summary>
    /// <param name="natalChart">The natal chart.</param>
    /// <param name="jd">The real Julian Day (UT).</param>
    /// <returns>The progressed Julian Day.</returns>
    private static double ProgressedJulianDay(ChartResult natalChart, double jd) =>
        natalChart.JulianDay + ProgressedDateCalculator.RealDaysToProgressedDays(jd - natalChart.JulianDay);

    /// <summary>
    /// Every progressed point's longitude at a real instant: the five bodies at the progressed Julian Day,
    /// the Midheaven by solar arc and the Ascendant for it (ruling T-D1).
    /// </summary>
    /// <param name="natalChart">The natal chart.</param>
    /// <param name="jd">The real Julian Day (UT).</param>
    /// <returns>The longitudes by point name.</returns>
    private IReadOnlyDictionary<string, double> ProgressedPointsAt(ChartResult natalChart, double jd)
    {
        double progressedJd = ProgressedJulianDay(natalChart, jd);
        Dictionary<string, double> points = new(StringComparer.Ordinal);
        foreach ((CelestialBodyId id, string name) in ProgressedBodies)
            points[name] = _ephemeris.CalculatePositionLongitudeOnly(progressedJd, id).Longitude;

        double natalSun = natalChart.Positions.Single(p => p.Body == CelestialBodyId.Sun).Longitude;
        double midheaven = Normalize(natalChart.Houses.Midheaven + Normalize(points["Sun"] - natalSun));
        points["MC"] = midheaven;
        points["ASC"] = _progressions.AscendantForMidheaven(midheaven, natalChart);
        return points;
    }

    /// <summary>One progressed point's longitude at a real instant, for the bisection.</summary>
    /// <param name="natalChart">The natal chart.</param>
    /// <param name="pointName">The progressed point's name.</param>
    /// <param name="jd">The real Julian Day (UT).</param>
    /// <returns>The longitude.</returns>
    private double ProgressedPointAt(ChartResult natalChart, string pointName, double jd)
    {
        double progressedJd = ProgressedJulianDay(natalChart, jd);
        if (Array.IndexOf(ProgressedAngles, pointName) < 0)
        {
            CelestialBodyId id = ProgressedBodies.Single(b => string.Equals(b.Name, pointName, StringComparison.Ordinal)).Id;
            return _ephemeris.CalculatePositionLongitudeOnly(progressedJd, id).Longitude;
        }

        double natalSun = natalChart.Positions.Single(p => p.Body == CelestialBodyId.Sun).Longitude;
        double progressedSun = _ephemeris.CalculatePositionLongitudeOnly(progressedJd, CelestialBodyId.Sun).Longitude;
        double midheaven = Normalize(natalChart.Houses.Midheaven + Normalize(progressedSun - natalSun));
        return string.Equals(pointName, "MC", StringComparison.Ordinal)
            ? midheaven
            : _progressions.AscendantForMidheaven(midheaven, natalChart);
    }

    /// <summary>A longitude brought into [0°, 360°).</summary>
    private static double Normalize(double longitude) => ((longitude % 360.0) + 360.0) % 360.0;

    /// <summary>An angle wrapped to [−180°, 180°).</summary>
    private static double WrapSigned(double angle) => Normalize(angle + 180.0) - 180.0;

    #endregion
}
