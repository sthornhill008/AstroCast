using System.Globalization;

using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.SolarArcs;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates solar arc directions: directed positions and
/// directed-to-natal aspects.
/// </summary>
/// <remarks>
/// <para>
/// Solar arc directions apply the solar arc value (progressed Sun longitude
/// minus natal Sun longitude) uniformly to every natal body and angle.
/// The resulting "directed" positions are then checked against natal positions
/// for aspects.
/// </para>
/// <para>
/// This is pure math — no ephemeris lookups are needed beyond the arc value
/// itself, which is provided by <see cref="ProgressionService"/>.
/// </para>
/// </remarks>
public sealed class SolarArcService : ISolarArcService
{
    /// <summary>
    /// The aspects of a solar-arc-to-natal aspect calculation: the five majors and the 45° series,
    /// semisquare and sesquiquadrate (branch Modes, 1.4.0; scoping C3b), the same set as the dated hits of
    /// <see cref="SolarArcHitFinder"/>. The 45° series is hard, as the conjunction, square and opposition
    /// are (Ebertin's and Tyl's reading). Orb: 1° for all solar arc aspects.
    /// </summary>
    private static readonly (string Name, string Abbreviation, double Angle, bool IsHard)[] s_majorAspects =
    [
        ("Conjunction", "cnj", 0.0, true),
        ("Opposition", "opp", 180.0, true),
        ("Square", "sqr", 90.0, true),
        ("Semisquare", "ssq", 45.0, true),
        ("Sesquiquadrate", "ses", 135.0, true),
        ("Trine", "tri", 120.0, false),
        ("Sextile", "sxt", 60.0, false),
    ];

    /// <summary>Maximum orb for solar-arc-to-natal aspects in degrees.</summary>
    private const double SolarArcAspectOrb = 1.0;

    /// <summary>
    /// Speed of a directed point, in degrees of longitude per degree of arc: every point
    /// moves exactly as far as the arc grows, and the arc never shrinks (the progressed
    /// Sun is never retrograde). Only its sign enters <see cref="AspectDirection"/>.
    /// </summary>
    private const double DirectedPointSpeed = 1.0;

    /// <summary>
    /// Names that identify angles (ASC, MC) for the InvolvesAngle flag.
    /// </summary>
    private static readonly HashSet<string> s_angleNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "ASC", "MC"
    };

    /// <summary>
    /// Calculates complete solar arc direction data.
    /// </summary>
    /// <param name="natalChart">The natal <see cref="ChartResult"/>.</param>
    /// <param name="solarArcValue">
    /// Solar arc value in degrees (from ProgressionResult.SolarArcValue).
    /// </param>
    /// <returns>Complete <see cref="SolarArcResult"/>.</returns>
    public SolarArcResult Calculate(ChartResult natalChart, double solarArcValue)
    {
        ArgumentNullException.ThrowIfNull(natalChart);

        // Step 1: Format the arc value
        string arcFormatted = FormatArc(solarArcValue);

        // Step 2: Build natal targets (all bodies + ASC + MC)
        List<(string Name, double Longitude)> natalTargets = BuildNatalTargets(natalChart);

        // Step 3: Calculate directed positions
        List<SolarArcPosition> positions = [];
        foreach ((string name, double natalLon) in natalTargets)
        {
            double directedLon = NormaliseAngle(natalLon + solarArcValue);
            positions.Add(BuildDirectedPosition(name, natalLon, directedLon));
        }

        // Step 4: Calculate solar-arc-to-natal aspects: every directed point at every other of the 14, then at
        // the natal Ceres, Pallas, Juno, Vesta and Lilith as targets of their own when the chart carries them
        // (R7, branch AsteroidsTiming, 1.6.0); the five are never directed
        List<SolarArcAspect> aspects = CalculateAspects(natalTargets, natalTargets, solarArcValue);
        List<SolarArcAspect> minorPointAspects = CalculateAspects(natalTargets, MinorPointTargets(natalChart), solarArcValue);

        // Step 5: Sort aspects — hard first, then by orb ascending
        SortAspects(aspects);
        SortAspects(minorPointAspects);

        return new SolarArcResult
        {
            ArcValue = solarArcValue,
            ArcFormatted = arcFormatted,
            Positions = positions.AsReadOnly(),
            Aspects = aspects.AsReadOnly(),
            MinorPointAspects = minorPointAspects.AsReadOnly(),
        };
    }

    /// <summary>
    /// Sorts aspects in place: hard aspects first, then by orb ascending.
    /// </summary>
    /// <param name="aspects">The aspects to sort.</param>
    private static void SortAspects(List<SolarArcAspect> aspects)
    {
        aspects.Sort((a, b) =>
        {
            int hardCompare = b.IsHardAspect.CompareTo(a.IsHardAspect);
            if (hardCompare != 0) return hardCompare;
            return a.OrbDegrees.CompareTo(b.OrbDegrees);
        });
    }

    #region Position Helpers

    /// <summary>
    /// Builds the list of all natal targets: all chart positions plus ASC and MC.
    /// </summary>
    private static List<(string Name, double Longitude)> BuildNatalTargets(ChartResult natalChart)
    {
        List<(string Name, double Longitude)> targets = [];

        foreach (PlanetPosition pos in natalChart.Positions)
        {
            targets.Add((pos.Name, pos.Longitude));
        }

        targets.Add(("ASC", natalChart.Houses.Ascendant));
        targets.Add(("MC", natalChart.Houses.Midheaven));

        return targets;
    }

    /// <summary>
    /// The natal Ceres, Pallas, Juno, Vesta and Lilith as targets (R7, branch AsteroidsTiming, 1.6.0), in
    /// <see cref="ChartResult.MinorPoints"/>' order; none when the chart does not carry them.
    /// </summary>
    /// <param name="natalChart">The natal chart.</param>
    /// <returns>The five's names and longitudes, or none.</returns>
    private static List<(string Name, double Longitude)> MinorPointTargets(ChartResult natalChart) =>
        natalChart.MinorPoints.Select(p => (p.Name, p.Longitude)).ToList();

    /// <summary>
    /// Builds a <see cref="SolarArcPosition"/> for a directed body.
    /// </summary>
    private static SolarArcPosition BuildDirectedPosition(
        string name, double natalLon, double directedLon)
    {
        return new SolarArcPosition
        {
            Name = name,
            NatalLongitude = natalLon,
            NatalFormattedPosition = FormatLongitude(natalLon),
            DirectedLongitude = directedLon,
            DirectedSign = TransitEphemerisHelper.LongitudeToSign(directedLon),
            DirectedDegrees = TransitEphemerisHelper.LongitudeToDegrees(directedLon),
            DirectedMinutes = TransitEphemerisHelper.LongitudeToMinutes(directedLon),
            DirectedFormattedPosition = FormatLongitude(directedLon),
        };
    }

    #endregion

    #region Aspect Calculation

    /// <summary>
    /// Calculates solar-arc-to-natal aspects (1° orb; the majors and the 45° series) from every directed point
    /// to every target: the chart's own 14 as both, or the five minor points as targets
    /// (<see cref="MinorPointTargets"/>).
    /// </summary>
    /// <param name="directedPoints">The points directed by the arc: their names and natal longitudes.</param>
    /// <param name="natalTargets">The natal targets' names and longitudes.</param>
    /// <param name="solarArcValue">The solar arc, in degrees.</param>
    /// <returns>The aspects within orb, in the loops' order.</returns>
    private static List<SolarArcAspect> CalculateAspects(
        List<(string Name, double Longitude)> directedPoints,
        List<(string Name, double Longitude)> natalTargets,
        double solarArcValue)
    {
        List<SolarArcAspect> aspects = [];

        foreach ((string directedName, double natalLon) in directedPoints)
        {
            double directedLon = NormaliseAngle(natalLon + solarArcValue);

            foreach ((string targetName, double targetLon) in natalTargets)
            {
                // Skip self-aspects (SA Sun cannot aspect natal Sun, etc.)
                if (string.Equals(directedName, targetName, StringComparison.OrdinalIgnoreCase))
                    continue;

                foreach ((string aspectName, string abbr, double aspectAngle, bool isHard) in s_majorAspects)
                {
                    double orb = CalculateAspectOrb(directedLon, targetLon, aspectAngle);

                    if (orb <= SolarArcAspectOrb)
                    {
                        string direction = DetermineDirection(
                            natalLon, targetLon, solarArcValue, aspectAngle);

                        bool involvesAngle = s_angleNames.Contains(directedName)
                            || s_angleNames.Contains(targetName);

                        aspects.Add(new SolarArcAspect
                        {
                            DirectedBody = $"SA {directedName}",
                            AspectName = aspectName,
                            AspectAbbreviation = abbr,
                            NatalTarget = targetName,
                            OrbDegrees = Math.Round(orb, 4),
                            Direction = direction,
                            InvolvesAngle = involvesAngle,
                            IsHardAspect = isHard,
                        });
                    }
                }
            }
        }

        return aspects;
    }

    /// <summary>
    /// Calculates the orb between a directed longitude and a natal target
    /// for a given aspect angle.
    /// </summary>
    private static double CalculateAspectOrb(
        double directedLon, double natalTargetLon, double aspectAngle)
    {
        double diff = NormaliseAngle(directedLon - natalTargetLon);

        // Check both forward and backward angular distance
        double orb1 = Math.Abs(diff - aspectAngle);
        double orb2 = Math.Abs(diff - (360.0 - aspectAngle));

        // For conjunction (0°), check the wraparound case
        if (aspectAngle == 0.0)
        {
            double orbWrap = Math.Abs(360.0 - diff);
            return Math.Min(orb1, orbWrap);
        }

        return Math.Min(orb1, orb2);
    }

    /// <summary>
    /// Determines whether a solar arc aspect is applying or separating: applying when
    /// the orb is decreasing as the arc grows, the directed point moving at the arc's
    /// rate (<see cref="DirectedPointSpeed"/>) and the natal point held still
    /// (<see cref="AspectDirection"/>).
    /// </summary>
    /// <remarks>
    /// Until 1.3.3 this compared the orb now with the orb 0.01° of arc later, so an
    /// aspect closer to exact than half that step was called separating while still
    /// applying (finding T3-3).
    /// </remarks>
    private static string DetermineDirection(
        double natalLon, double natalTargetLon, double solarArcValue, double aspectAngle)
    {
        double currentDirectedLon = NormaliseAngle(natalLon + solarArcValue);

        return AspectDirection.IsApplying(
            currentDirectedLon, DirectedPointSpeed, natalTargetLon, 0.0, aspectAngle)
            ? "applying" : "separating";
    }

    #endregion

    #region Utility

    /// <summary>
    /// Normalises an angle to the range [0, 360).
    /// </summary>
    private static double NormaliseAngle(double angle)
    {
        double result = angle % 360.0;
        if (result < 0) result += 360.0;
        return result;
    }

    /// <summary>
    /// Formats a longitude as "DD°MM' Sign" (e.g., "26°19' Taurus").
    /// </summary>
    private static string FormatLongitude(double longitude)
    {
        int degrees = TransitEphemerisHelper.LongitudeToDegrees(longitude);
        int minutes = TransitEphemerisHelper.LongitudeToMinutes(longitude);
        string sign = TransitEphemerisHelper.LongitudeToSign(longitude);
        return string.Create(CultureInfo.InvariantCulture, $"{degrees:D2}\u00b0{minutes:D2}' {sign}");
    }

    /// <summary>
    /// Formats a solar arc value as degrees and minutes (e.g., "36°08'").
    /// </summary>
    private static string FormatArc(double arcDegrees)
    {
        int wholeDegrees = (int)arcDegrees;
        int minutes = (int)((arcDegrees - wholeDegrees) * 60.0);
        return string.Create(CultureInfo.InvariantCulture, $"{wholeDegrees}\u00b0{minutes:D2}'");
    }

    #endregion
}
