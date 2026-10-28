using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Evaluates accidental conditions for each planet in a chart: combustion
/// hierarchy (cazimi, combust, under the beams), besiegement, and benefic/malefic
/// influence from conjunctions and applying aspects.
/// </summary>
/// <remarks>
/// <para>
/// <b>Combustion thresholds</b> are applied to the shortest arc between the
/// planet and the Sun:
/// <list type="bullet">
///   <item>Cazimi: 0°–0°17′ (0.2833°) → +5</item>
///   <item>Combust: 0°17′–8° → −5 (Moon exempt)</item>
///   <item>Under the Beams: 8°–17° → −2 (Moon exempt)</item>
/// </list>
/// </para>
/// <para>
/// <b>Besiegement</b> requires Mars and Saturn to bracket the planet by
/// zodiacal longitude with no benefic (Jupiter, Venus) in the enclosed arc.
/// Score: −5.
/// </para>
/// <para>
/// <b>Benefic/malefic influence</b> is scored from the chart's aspect list,
/// once per flag however many aspects qualify:
/// <list type="bullet">
///   <item>Conjunction with Jupiter or Venus: +2</item>
///   <item>Conjunction with Mars or Saturn: −2</item>
///   <item>Applying trine or sextile from Jupiter or Venus: +1</item>
///   <item>Applying square or opposition from Mars or Saturn: −1</item>
/// </list>
/// </para>
/// </remarks>
public sealed class PlanetConditionService : IPlanetConditionService
{
    /// <summary>Cazimi threshold: 0°17′ in decimal degrees.</summary>
    private const double CazimiThreshold = 17.0 / 60.0; // 0.2833°

    /// <summary>Combustion threshold in degrees.</summary>
    private const double CombustionThreshold = 8.0;

    /// <summary>Under the beams threshold in degrees.</summary>
    private const double UnderBeamsThreshold = 17.0;

    /// <summary>Benefic planet names.</summary>
    private static readonly HashSet<string> Benefics = new(StringComparer.OrdinalIgnoreCase)
    {
        "Jupiter", "Venus"
    };

    /// <summary>Malefic planet names.</summary>
    private static readonly HashSet<string> Malefics = new(StringComparer.OrdinalIgnoreCase)
    {
        "Mars", "Saturn"
    };

    /// <summary>
    /// Planets that are subject to combustion analysis (Sun cannot be combust
    /// by itself; outer transpersonals and nodes/Chiron are excluded from
    /// traditional combustion rules).
    /// </summary>
    private static readonly HashSet<string> CombustiblePlanets = new(StringComparer.OrdinalIgnoreCase)
    {
        "Moon", "Mercury", "Venus", "Mars", "Jupiter", "Saturn"
    };

    /// <inheritdoc />
    public IReadOnlyList<PlanetCondition> Analyze(
        IReadOnlyList<PlanetPosition> positions,
        IReadOnlyList<AspectResult> aspects)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(aspects);

        if (positions.Count == 0)
            return [];

        // Find the Sun for combustion calculations
        PlanetPosition? sun = positions.FirstOrDefault(
            p => string.Equals(p.Name, "Sun", StringComparison.OrdinalIgnoreCase));

        List<PlanetCondition> results = [];

        foreach (PlanetPosition planet in positions)
        {
            // Skip the Sun itself — it cannot have accidental conditions relative to itself
            if (string.Equals(planet.Name, "Sun", StringComparison.OrdinalIgnoreCase))
                continue;

            // Evaluate each condition
            (bool isCazimi, bool isCombust, bool isUnderBeams) =
                EvaluateCombustion(planet, sun);

            bool isBesieged = EvaluateBesiegement(planet, positions);

            (bool isConjunctBenefic, bool isConjunctMalefic,
             bool hasApplyingBenefic, bool hasApplyingMalefic) =
                EvaluateInfluence(planet, aspects);

            // Calculate score
            int score = 0;
            List<string> conditions = [];

            if (isCazimi)
            {
                score += 5;
                conditions.Add("Cazimi");
            }

            if (isCombust)
            {
                score -= 5;
                conditions.Add("Combust");
            }

            if (isUnderBeams)
            {
                score -= 2;
                conditions.Add("Under the Beams");
            }

            if (isBesieged)
            {
                score -= 5;
                conditions.Add("Besieged");
            }

            if (isConjunctBenefic)
            {
                score += 2;
                conditions.Add("Conjunct benefic");
            }

            if (isConjunctMalefic)
            {
                score -= 2;
                conditions.Add("Conjunct malefic");
            }

            if (hasApplyingBenefic)
            {
                score += 1;
                conditions.Add("Applying benefic aspect");
            }

            if (hasApplyingMalefic)
            {
                score -= 1;
                conditions.Add("Applying malefic aspect");
            }

            // Only include planets that have at least one condition
            if (conditions.Count > 0)
            {
                results.Add(new PlanetCondition
                {
                    PlanetName = planet.Name,
                    IsCazimi = isCazimi,
                    IsCombust = isCombust,
                    IsUnderTheBeams = isUnderBeams,
                    IsBesieged = isBesieged,
                    IsConjunctBenefic = isConjunctBenefic,
                    IsConjunctMalefic = isConjunctMalefic,
                    HasApplyingBeneficAspect = hasApplyingBenefic,
                    HasApplyingMaleficAspect = hasApplyingMalefic,
                    AccidentalScore = score,
                    Conditions = conditions,
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Evaluates the combustion hierarchy for a planet relative to the Sun.
    /// Returns (isCazimi, isCombust, isUnderBeams). At most one will be true.
    /// </summary>
    /// <param name="planet">The planet to evaluate.</param>
    /// <param name="sun">The Sun position, or null if the Sun is not in the chart.</param>
    /// <returns>Tuple of cazimi, combust, and under-the-beams flags.</returns>
    private static (bool IsCazimi, bool IsCombust, bool IsUnderBeams) EvaluateCombustion(
        PlanetPosition planet, PlanetPosition? sun)
    {
        // No Sun = no combustion analysis possible
        if (sun is null)
            return (false, false, false);

        // Only classical planets are subject to combustion
        if (!CombustiblePlanets.Contains(planet.Name))
            return (false, false, false);

        double separation = ShortestArc(planet.Longitude, sun.Longitude);

        bool isMoon = string.Equals(planet.Name, "Moon", StringComparison.OrdinalIgnoreCase);

        // Cazimi: within 0°17′ — applies to all combustible planets including Moon
        if (separation <= CazimiThreshold)
            return (true, false, false);

        // Moon is exempt from combust and under the beams
        if (isMoon)
            return (false, false, false);

        // Combust: within 8° (but outside cazimi)
        if (separation <= CombustionThreshold)
            return (false, true, false);

        // Under the Beams: 8°–17°
        if (separation <= UnderBeamsThreshold)
            return (false, false, true);

        return (false, false, false);
    }

    /// <summary>
    /// Evaluates whether a planet is besieged: enclosed between Mars and Saturn
    /// by zodiacal longitude with no benefic (Jupiter, Venus) intervening.
    /// </summary>
    /// <param name="planet">The planet to evaluate.</param>
    /// <param name="positions">All planetary positions in the chart.</param>
    /// <returns>True if the planet is besieged.</returns>
    private static bool EvaluateBesiegement(
        PlanetPosition planet, IReadOnlyList<PlanetPosition> positions)
    {
        // Sun, Mars, Saturn, Jupiter, Venus cannot be besieged in the traditional sense
        // (Mars and Saturn are the besiegers; Jupiter and Venus are protectors)
        if (Malefics.Contains(planet.Name) || Benefics.Contains(planet.Name))
            return false;
        if (string.Equals(planet.Name, "Sun", StringComparison.OrdinalIgnoreCase))
            return false;

        PlanetPosition? mars = positions.FirstOrDefault(
            p => string.Equals(p.Name, "Mars", StringComparison.OrdinalIgnoreCase));
        PlanetPosition? saturn = positions.FirstOrDefault(
            p => string.Equals(p.Name, "Saturn", StringComparison.OrdinalIgnoreCase));

        if (mars is null || saturn is null)
            return false;

        double planetLon = planet.Longitude;
        double marsLon = mars.Longitude;
        double saturnLon = saturn.Longitude;

        // Determine the two possible arcs: Mars→Saturn and Saturn→Mars (going forward)
        // The planet must be in the shorter arc between Mars and Saturn
        double lowerBound;
        double upperBound;

        // Normalise: find which malefic is "behind" and which is "ahead"
        // by checking the shorter arc
        double marsToSaturn = NormalizeAngle(saturnLon - marsLon);
        double saturnToMars = NormalizeAngle(marsLon - saturnLon);

        if (marsToSaturn <= saturnToMars)
        {
            // Shorter arc goes Mars → Saturn (forward)
            lowerBound = marsLon;
            upperBound = saturnLon;
        }
        else
        {
            // Shorter arc goes Saturn → Mars (forward)
            lowerBound = saturnLon;
            upperBound = marsLon;
        }

        // Check if planet is in the arc from lowerBound to upperBound (forward)
        if (!IsInArc(planetLon, lowerBound, upperBound))
            return false;

        // Check for intervening benefics (Jupiter or Venus) in the same arc
        foreach (PlanetPosition p in positions)
        {
            if (Benefics.Contains(p.Name) && IsInArc(p.Longitude, lowerBound, upperBound))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Evaluates benefic/malefic influence from conjunctions, from an applying trine or
    /// sextile of Jupiter or Venus, and from an applying square or opposition of Mars or Saturn.
    /// </summary>
    /// <param name="planet">The planet to evaluate.</param>
    /// <param name="aspects">All detected aspects in the chart.</param>
    /// <returns>
    /// Tuple of (isConjunctBenefic, isConjunctMalefic, hasApplyingBeneficAspect,
    /// hasApplyingMaleficAspect).
    /// </returns>
    private static (bool IsConjunctBenefic, bool IsConjunctMalefic,
                     bool HasApplyingBenefic, bool HasApplyingMalefic)
        EvaluateInfluence(PlanetPosition planet, IReadOnlyList<AspectResult> aspects)
    {
        bool conjunctBenefic = false;
        bool conjunctMalefic = false;
        bool applyingBenefic = false;
        bool applyingMalefic = false;

        foreach (AspectResult aspect in aspects)
        {
            // Find the "other" planet in this aspect
            string? otherName = null;
            if (string.Equals(aspect.Body1Name, planet.Name, StringComparison.OrdinalIgnoreCase))
                otherName = aspect.Body2Name;
            else if (string.Equals(aspect.Body2Name, planet.Name, StringComparison.OrdinalIgnoreCase))
                otherName = aspect.Body1Name;

            if (otherName is null)
                continue;

            bool otherIsBenefic = Benefics.Contains(otherName);
            bool otherIsMalefic = Malefics.Contains(otherName);

            if (!otherIsBenefic && !otherIsMalefic)
                continue;

            // Conjunction
            if (aspect.Aspect.Type == AspectType.Conjunction)
            {
                if (otherIsBenefic) conjunctBenefic = true;
                if (otherIsMalefic) conjunctMalefic = true;
            }
            // Applying aspect (non-conjunction): a trine or sextile from a benefic, or a
            // square or opposition from a malefic (the Techniques reference)
            else if (aspect.IsApplying)
            {
                bool isTrineOrSextile = aspect.Aspect.Type is AspectType.Trine or AspectType.Sextile;
                bool isSquareOrOpposition = aspect.Aspect.Type is AspectType.Square or AspectType.Opposition;
                if (otherIsBenefic && isTrineOrSextile) applyingBenefic = true;
                if (otherIsMalefic && isSquareOrOpposition) applyingMalefic = true;
            }
        }

        return (conjunctBenefic, conjunctMalefic, applyingBenefic, applyingMalefic);
    }

    /// <summary>
    /// Calculates the shortest arc between two ecliptic longitudes (0–180°).
    /// </summary>
    /// <param name="lon1">First longitude in decimal degrees (0–360).</param>
    /// <param name="lon2">Second longitude in decimal degrees (0–360).</param>
    /// <returns>Shortest arc in degrees (always positive, 0–180).</returns>
    private static double ShortestArc(double lon1, double lon2)
    {
        double diff = Math.Abs(lon1 - lon2);
        return diff > 180.0 ? 360.0 - diff : diff;
    }

    /// <summary>
    /// Normalizes an angle to the range [0, 360).
    /// </summary>
    /// <param name="angle">Angle in degrees.</param>
    /// <returns>Normalized angle in [0, 360).</returns>
    private static double NormalizeAngle(double angle)
    {
        double result = angle % 360.0;
        return result < 0 ? result + 360.0 : result;
    }

    /// <summary>
    /// Checks whether a longitude lies within the forward arc from
    /// <paramref name="start"/> to <paramref name="end"/> (exclusive of endpoints).
    /// </summary>
    /// <param name="longitude">The longitude to test.</param>
    /// <param name="start">Arc start longitude.</param>
    /// <param name="end">Arc end longitude.</param>
    /// <returns>True if the longitude is strictly within the arc.</returns>
    private static bool IsInArc(double longitude, double start, double end)
    {
        double arcLength = NormalizeAngle(end - start);
        double pointOffset = NormalizeAngle(longitude - start);

        // Point must be strictly between start and end (not at either endpoint)
        return pointOffset > 0.0 && pointOffset < arcLength;
    }
}
