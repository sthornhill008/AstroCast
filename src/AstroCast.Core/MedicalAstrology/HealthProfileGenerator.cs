using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.Core.MedicalAstrology;

/// <summary>
/// Generates a complete <see cref="HealthProfile"/> from a <see cref="ChartResult"/>
/// by combining temperament calculation, zodiacal anatomy mapping, medical aspect
/// analysis, and health house interpretation. This is the primary entry point for
/// the Medical Astrology Tier 1 feature.
/// </summary>
/// <remarks>
/// All analysis is based on traditional astrological correspondences and is
/// provided for educational purposes only. No medical diagnosis or advice is given.
/// </remarks>
public static class HealthProfileGenerator
{
    /// <summary>
    /// Hard aspect types considered medically significant.
    /// </summary>
    private static readonly HashSet<AspectType> HardAspectTypes =
    [
        AspectType.Conjunction,
        AspectType.Opposition,
        AspectType.Square,
        AspectType.Quincunx
    ];

    /// <summary>
    /// Generate a health profile from a calculated chart result.
    /// </summary>
    /// <param name="result">The calculated chart result containing positions, houses, and aspects.</param>
    /// <returns>A complete <see cref="HealthProfile"/> for display.</returns>
    public static HealthProfile Generate(ChartResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        TemperamentProfile temperament = TemperamentCalculator.Calculate(result);
        IReadOnlyList<SignEmphasis> signEmphases = BuildSignEmphases(result);
        IReadOnlyList<MedicalAspectInfo> medicalAspects = BuildMedicalAspects(result);

        // House analysis — Cusps is 0-indexed: Cusps[0] = House 1, Cusps[5] = House 6
        ZodiacSign sixthSign = GetSignFromLongitude(result.Houses.Cusps[5]);
        List<string> planetsInSixth = result.Positions
            .Where(p => p.House == 6)
            .Select(p => p.Name)
            .ToList();

        string sixthAnalysis = BuildHouseAnalysis(6, "daily health, acute illness, and health habits",
            sixthSign, planetsInSixth, result);
        string eighthAnalysis = BuildHouseAnalysis(8, "chronic conditions, surgery, and regenerative processes",
            GetSignFromLongitude(result.Houses.Cusps[7]),
            result.Positions.Where(p => p.House == 8).Select(p => p.Name).ToList(),
            result);
        string twelfthAnalysis = BuildHouseAnalysis(12, "hidden illness, hospitals, confinement, and immune sensitivity",
            GetSignFromLongitude(result.Houses.Cusps[11]),
            result.Positions.Where(p => p.House == 12).Select(p => p.Name).ToList(),
            result);

        return new HealthProfile
        {
            SubjectName = result.Input.Name,
            Temperament = temperament,
            SignEmphases = signEmphases,
            MedicalAspects = medicalAspects,
            SixthHouseAnalysis = sixthAnalysis,
            SixthHouseSign = sixthSign,
            PlanetsInSixth = planetsInSixth,
            EighthHouseAnalysis = eighthAnalysis,
            TwelfthHouseAnalysis = twelfthAnalysis
        };
    }

    /// <summary>
    /// Build the list of emphasised zodiac signs from the chart.
    /// Always includes the Ascendant sign, Sun sign, and Moon sign.
    /// Also includes any sign containing 2 or more planets (stellium).
    /// Deduplicates: if a sign appears for multiple reasons, combine reasons.
    /// </summary>
    private static IReadOnlyList<SignEmphasis> BuildSignEmphases(ChartResult result)
    {
        Dictionary<ZodiacSign, List<string>> signReasons = new();

        void AddReason(ZodiacSign sign, string reason)
        {
            if (!signReasons.TryGetValue(sign, out List<string>? reasons))
            {
                reasons = [];
                signReasons[sign] = reasons;
            }
            reasons.Add(reason);
        }

        // Ascendant sign — always included
        ZodiacSign ascSign = GetSignFromLongitude(result.Houses.Ascendant);
        AddReason(ascSign, "Ascendant");

        // Sun sign — always included
        PlanetPosition? sun = result.Positions.FirstOrDefault(p => p.Body == CelestialBodyId.Sun);
        if (sun is not null)
            AddReason(sun.Sign, "Sun sign");

        // Moon sign — always included
        PlanetPosition? moon = result.Positions.FirstOrDefault(p => p.Body == CelestialBodyId.Moon);
        if (moon is not null)
            AddReason(moon.Sign, "Moon sign");

        // Signs with 2+ planets (stellium detection)
        Dictionary<ZodiacSign, int> signCounts = new();
        foreach (PlanetPosition position in result.Positions)
        {
            signCounts.TryGetValue(position.Sign, out int count);
            signCounts[position.Sign] = count + 1;
        }

        foreach (KeyValuePair<ZodiacSign, int> kvp in signCounts)
        {
            if (kvp.Value >= 2 && !signReasons.ContainsKey(kvp.Key))
                AddReason(kvp.Key, string.Create(CultureInfo.InvariantCulture, $"{kvp.Value} planets"));
        }

        // Build the result list
        List<SignEmphasis> emphases = [];
        foreach (KeyValuePair<ZodiacSign, List<string>> kvp in signReasons)
        {
            emphases.Add(new SignEmphasis
            {
                Sign = kvp.Key,
                Reason = string.Join(", ", kvp.Value),
                BodyRegions = ZodiacBodyMap.GetBodyRegions(kvp.Key),
                HealthSummary = ZodiacBodyMap.GetHealthSummary(kvp.Key)
            });
        }

        return emphases;
    }

    /// <summary>
    /// Build medical aspect interpretations from the chart's hard aspects.
    /// Filters to Conjunction, Opposition, Square, and Quincunx only.
    /// Sorts by significance (High first) then by orb (tightest first).
    /// </summary>
    private static IReadOnlyList<MedicalAspectInfo> BuildMedicalAspects(ChartResult result)
    {
        List<MedicalAspectInfo> aspects = [];

        foreach (AspectResult aspect in result.Aspects)
        {
            if (!HardAspectTypes.Contains(aspect.Aspect.Type))
                continue;

            // Try to parse body names to CelestialBodyId for region lookups
            IReadOnlyList<BodyRegion> body1Regions = GetRegionsForBodyName(aspect.Body1Name);
            IReadOnlyList<BodyRegion> body2Regions = GetRegionsForBodyName(aspect.Body2Name);

            string significance = aspect.Aspect.Type switch
            {
                AspectType.Conjunction or AspectType.Opposition or AspectType.Square => "High",
                AspectType.Quincunx => "Moderate",
                _ => "Low"
            };

            string body1Summary = GetHealthRoleForBodyName(aspect.Body1Name);
            string body2Summary = GetHealthRoleForBodyName(aspect.Body2Name);

            string interpretation = $"{aspect.Body1Name} {aspect.Aspect.Type} {aspect.Body2Name}: " +
                $"Traditional focus on {FormatRegionNames(body1Regions)} ({aspect.Body1Name}) " +
                $"and {FormatRegionNames(body2Regions)} ({aspect.Body2Name}). " +
                $"This hard aspect may indicate an area requiring attention in " +
                $"{(string.IsNullOrEmpty(body1Summary) ? aspect.Body1Name + " functions" : TruncateRole(body1Summary))} " +
                $"and {(string.IsNullOrEmpty(body2Summary) ? aspect.Body2Name + " functions" : TruncateRole(body2Summary))}.";

            aspects.Add(new MedicalAspectInfo
            {
                AspectDescription = $"{aspect.Body1Name} {aspect.Aspect.Type} {aspect.Body2Name}",
                AspectType = aspect.Aspect.Type.ToString(),
                Orb = aspect.Orb,
                IsApplying = aspect.IsApplying,
                MedicalInterpretation = interpretation,
                Body1Regions = body1Regions,
                Body2Regions = body2Regions,
                Significance = significance
            });
        }

        // Sort by significance (High first) then by orb (tightest first)
        aspects.Sort((a, b) =>
        {
            int sigCompare = GetSignificanceOrder(a.Significance).CompareTo(GetSignificanceOrder(b.Significance));
            return sigCompare != 0 ? sigCompare : a.Orb.CompareTo(b.Orb);
        });

        return aspects;
    }

    /// <summary>
    /// Build a house analysis string for the specified house.
    /// </summary>
    private static string BuildHouseAnalysis(int houseNumber, string houseMeaning,
        ZodiacSign cuspSign, List<string> planetsInHouse, ChartResult result)
    {
        string analysis = $"The {GetOrdinal(houseNumber)} house cusp is in {cuspSign}, " +
            $"traditionally associated with {houseMeaning}. " +
            $"{cuspSign} on this cusp brings the focus of {ZodiacBodyMap.GetHealthSummary(cuspSign)}";

        if (planetsInHouse.Count > 0)
        {
            analysis += $" Planets in the {GetOrdinal(houseNumber)} house: {string.Join(", ", planetsInHouse)}. " +
                $"These placements may draw additional attention to this health area.";
        }
        else
        {
            analysis += $" No planets are placed in the {GetOrdinal(houseNumber)} house.";
        }

        return analysis;
    }

    /// <summary>
    /// Get body regions for a celestial body by its display name.
    /// Handles both planet names and angle names (ASC, MC).
    /// </summary>
    private static IReadOnlyList<BodyRegion> GetRegionsForBodyName(string bodyName)
    {
        if (TryParseCelestialBody(bodyName, out CelestialBodyId bodyId))
            return PlanetaryHealthMap.GetBodyRegions(bodyId);

        return [];
    }

    /// <summary>
    /// Get the health role string for a body by its display name.
    /// </summary>
    private static string GetHealthRoleForBodyName(string bodyName)
    {
        if (TryParseCelestialBody(bodyName, out CelestialBodyId bodyId))
            return PlanetaryHealthMap.GetHealthRole(bodyId);

        return string.Empty;
    }

    /// <summary>
    /// Try to parse a body name string to a <see cref="CelestialBodyId"/>.
    /// Handles common display names including "True Node" and "North Node".
    /// </summary>
    private static bool TryParseCelestialBody(string name, out CelestialBodyId bodyId)
    {
        bodyId = default;

        if (string.Equals(name, "True Node", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(name, "North Node", StringComparison.OrdinalIgnoreCase))
        {
            bodyId = CelestialBodyId.TrueNode;
            return true;
        }

        return Enum.TryParse(name, ignoreCase: true, out bodyId);
    }

    /// <summary>
    /// Format a list of body regions into a readable string of region names.
    /// </summary>
    private static string FormatRegionNames(IReadOnlyList<BodyRegion> regions)
    {
        if (regions.Count == 0) return "general functions";
        return string.Join(", ", regions.Select(r => r.Name.ToLowerInvariant()));
    }

    /// <summary>
    /// Truncate a health role description to the first sentence/phrase for brevity.
    /// </summary>
    private static string TruncateRole(string role)
    {
        int periodIndex = role.IndexOf(". ", StringComparison.Ordinal);
        return periodIndex > 0 ? role[..periodIndex].ToLowerInvariant() : role.ToLowerInvariant();
    }

    /// <summary>
    /// Get sort order for significance levels (lower = higher priority).
    /// </summary>
    private static int GetSignificanceOrder(string significance) => significance switch
    {
        "High" => 0,
        "Moderate" => 1,
        "Low" => 2,
        _ => 3
    };

    /// <summary>
    /// Get ordinal suffix for a house number.
    /// </summary>
    private static string GetOrdinal(int number) => number switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => string.Create(CultureInfo.InvariantCulture, $"{number}th")
    };

    /// <summary>
    /// Derive the zodiac sign from an ecliptic longitude (0-360 degrees).
    /// </summary>
    private static ZodiacSign GetSignFromLongitude(double longitude) =>
        (ZodiacSign)((int)(longitude % 360.0 / 30.0) + 1);
}
