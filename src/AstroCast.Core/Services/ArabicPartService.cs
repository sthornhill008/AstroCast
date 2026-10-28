using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates Arabic Parts (Lots) and Hellenistic Lots from chart data.
/// Uses the formula <c>Base + Factor_A − Factor_B</c> with day/night
/// reversal, then assigns house and ruler to each Part.
/// </summary>
/// <remarks>
/// <para>
/// Parts are computed in priority order: Fortune and Spirit first (order 1),
/// then all remaining Parts (order 10). This ensures that Hermetic Lots
/// referencing Fortune or Spirit as factors can resolve those longitudes.
/// </para>
/// <para>
/// House assignment uses geometric cusp-range checking — the Part's
/// longitude is compared against each pair of adjacent house cusps to
/// determine which house contains it. This correctly handles the 360°/0°
/// wrap that occurs when a house cusp spans the Aries point.
/// </para>
/// </remarks>
public sealed class ArabicPartService : IArabicPartService
{
    private readonly IDignityService _dignity;

    /// <summary>
    /// Initializes a new instance of <see cref="ArabicPartService"/>.
    /// </summary>
    /// <param name="dignity">Dignity service for sign ruler lookups.</param>
    public ArabicPartService(IDignityService dignity)
    {
        ArgumentNullException.ThrowIfNull(dignity);
        _dignity = dignity;
    }

    /// <inheritdoc />
    public IReadOnlyList<ArabicPart> CalculateAll(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houses,
        bool isDayChart)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(houses);

        // Build a lookup of planet longitudes by name
        Dictionary<string, double> longitudes = new(StringComparer.OrdinalIgnoreCase);
        foreach (PlanetPosition pos in positions)
        {
            longitudes[pos.Name] = pos.Longitude;
        }

        // Add angle and cusp longitudes
        longitudes["Asc"] = houses.Ascendant;
        longitudes["Desc"] = houses.Descendant;
        longitudes["MC"] = houses.Midheaven;
        longitudes["IC"] = houses.ImumCoeli;

        if (houses.Cusps.Count > 7)
            longitudes["Cusp8"] = houses.Cusps[7];

        // Fixed degree points
        longitudes["19Ari"] = 19.0;

        // Computed Lot longitudes (Fortune, Spirit) populated during first pass
        Dictionary<string, double> computedLots = new(StringComparer.OrdinalIgnoreCase);

        // Sort definitions by compute order
        List<ArabicPartDefinition> ordered = ArabicPartCatalog.All
            .OrderBy(d => d.ComputeOrder)
            .ThenBy(d => d.Group)
            .ToList();

        List<ArabicPart> results = [];

        foreach (ArabicPartDefinition definition in ordered)
        {
            // Resolve base longitude
            double baseLon = ResolveBase(definition.Base, houses);

            // Resolve factors — try planet/angle lookup first, then computed lots
            if (!ResolveFactor(definition.FactorA, longitudes, computedLots, out double factorALon))
                continue; // Skip if factor cannot be resolved

            if (!ResolveFactor(definition.FactorB, longitudes, computedLots, out double factorBLon))
                continue;

            // Day/night reversal
            if (!isDayChart && definition.ReversesAtNight)
            {
                (factorALon, factorBLon) = (factorBLon, factorALon);
            }

            // Calculate Part longitude
            double longitude = Normalize360(baseLon + factorALon - factorBLon);

            // Build formula string for display
            string formulaBase = definition.Base == "MC" ? "MC" : "Asc";
            string displayA = !isDayChart && definition.ReversesAtNight ? definition.FactorB : definition.FactorA;
            string displayB = !isDayChart && definition.ReversesAtNight ? definition.FactorA : definition.FactorB;
            string formula = $"{formulaBase} + {displayA} \u2212 {displayB}";

            // House assignment
            int? house = HousePlacement.HouseOf(longitude, houses.Cusps);

            // Sign ruler
            ZodiacSign sign = LongitudeToSign(longitude);
            string ruler = _dignity.GetSignRuler(sign);

            ArabicPart part = new()
            {
                Name = definition.Name,
                Group = definition.Group,
                Longitude = longitude,
                House = house,
                Formula = formula,
                Domain = definition.Domain,
                Ruler = ruler,
            };

            results.Add(part);

            // Store Fortune and Spirit for Hermetic Lot reference
            if (string.Equals(definition.Name, "Part of Fortune", StringComparison.OrdinalIgnoreCase)
                || string.Equals(definition.Name, "Lot of Fortune", StringComparison.OrdinalIgnoreCase))
            {
                computedLots["Fortune"] = longitude;
            }
            else if (string.Equals(definition.Name, "Part of Spirit", StringComparison.OrdinalIgnoreCase)
                     || string.Equals(definition.Name, "Lot of Spirit", StringComparison.OrdinalIgnoreCase))
            {
                computedLots["Spirit"] = longitude;
            }
        }

        return results;
    }

    /// <summary>
    /// Resolves the base point longitude ("Asc" or "MC").
    /// </summary>
    private static double ResolveBase(string baseName, HouseData houses) =>
        string.Equals(baseName, "MC", StringComparison.OrdinalIgnoreCase)
            ? houses.Midheaven
            : houses.Ascendant;

    /// <summary>
    /// Resolves a factor name to an ecliptic longitude. Checks planet/angle
    /// longitudes first, then previously computed Lot longitudes.
    /// </summary>
    /// <param name="factorName">Factor name to resolve.</param>
    /// <param name="longitudes">Planet and angle longitudes.</param>
    /// <param name="computedLots">Previously computed Lot longitudes.</param>
    /// <param name="longitude">Resolved longitude (output).</param>
    /// <returns>True if resolved, false if the factor name is unknown.</returns>
    private static bool ResolveFactor(
        string factorName,
        Dictionary<string, double> longitudes,
        Dictionary<string, double> computedLots,
        out double longitude)
    {
        if (longitudes.TryGetValue(factorName, out longitude))
            return true;

        if (computedLots.TryGetValue(factorName, out longitude))
            return true;

        longitude = 0.0;
        return false;
    }

    /// <summary>
    /// Normalizes a longitude to the 0–360° range.
    /// </summary>
    private static double Normalize360(double longitude) =>
        ((longitude % 360.0) + 360.0) % 360.0;

    /// <summary>
    /// Converts an ecliptic longitude to a zodiac sign.
    /// </summary>
    private static ZodiacSign LongitudeToSign(double longitude)
    {
        double normalized = Normalize360(longitude);
        int signIndex = (int)(normalized / 30.0);
        if (signIndex >= 12) signIndex = 0;
        return (ZodiacSign)(signIndex + 1);
    }
}
