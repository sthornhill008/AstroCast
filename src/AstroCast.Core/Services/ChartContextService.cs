using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Analyzes the interpretive context of a chart for event and natal interpretation.
/// Identifies chart ruler, sect, event significator, angular planets, and mutual receptions.
/// </summary>
/// <remarks>
/// All analysis is derived from data already computed by the chart engine —
/// no additional astronomical calculations are needed. Depends on
/// <see cref="IDignityService"/> for traditional sign-ruler lookups. A relocated
/// chart hands in the natal context, whose day or night and Planetary Sect table
/// it keeps (S-D6, branch K2, 1.3.4).
/// </remarks>
public sealed class ChartContextService : IChartContextService
{
    private readonly IDignityService _dignityService;
    private readonly ISectService _sectService;

    /// <summary>
    /// Traditional planets eligible for mutual reception by domicile.
    /// Only planets with traditional sign rulerships can participate.
    /// </summary>
    private static readonly string[] TraditionalPlanets =
        ["Sun", "Moon", "Mercury", "Venus", "Mars", "Jupiter", "Saturn"];

    /// <summary>
    /// Initializes a new instance of the <see cref="ChartContextService"/> class.
    /// </summary>
    /// <param name="dignityService">
    /// The dignity service used for ruler lookups and dignity calculations.
    /// </param>
    /// <param name="sectService">
    /// The sect service used for per-planet sect analysis.
    /// </param>
    public ChartContextService(IDignityService dignityService, ISectService sectService)
    {
        ArgumentNullException.ThrowIfNull(dignityService);
        ArgumentNullException.ThrowIfNull(sectService);
        _dignityService = dignityService;
        _sectService = sectService;
    }

    /// <inheritdoc />
    public ChartContextInfo Analyze(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houses,
        IReadOnlyList<DignityInfo> dignities,
        string? eventType,
        ChartContextInfo? sectFrom = null)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(houses);
        ArgumentNullException.ThrowIfNull(dignities);

        // 1. Chart ruler from Ascendant sign
        ZodiacSign ascSign = LongitudeToSign(houses.Ascendant);
        string chartRuler = _dignityService.GetSignRuler(ascSign);

        PlanetPosition? rulerPosition = positions.FirstOrDefault(p => p.Name == chartRuler);
        DignityInfo? rulerDignity = dignities.FirstOrDefault(d => d.PlanetName == chartRuler);

        // Fallback dignity if ruler not found in dignities list
        rulerDignity ??= _dignityService.GetDignity(chartRuler, ascSign);

        int rulerHouse = rulerPosition?.House ?? 1;
        bool rulerRetrograde = rulerPosition?.IsRetrograde ?? false;

        // 2. Day/Night sect: the birth's for a relocated chart (S-D6), else the Sun's
        PlanetPosition? sun = positions.FirstOrDefault(p => p.Name == "Sun");
        bool isDayChart = sectFrom?.IsDayChart ?? (sun is not null && ChartSect.IsDayChart(sun.Longitude, houses));

        // 3. Event significator
        (string? significator, int[] keyHouses) = GetEventMapping(eventType);
        DignityInfo? significatorDignity = null;

        if (significator is not null)
        {
            significatorDignity = dignities.FirstOrDefault(d => d.PlanetName == significator);
        }

        // 4. Angular planets — shared routine (single source of truth with the
        //    electional Malefic Avoidance scorer; see AngularityService)
        IReadOnlyList<AngularPlanet> angularPlanets =
            AngularityService.FindAngularPlanets(positions, houses);

        // 5. Mutual receptions (extended: domicile, exaltation, triplicity, mixed)
        IReadOnlyList<MutualReception> mutualReceptions = FindMutualReceptions(positions, isDayChart);

        // 6. Planetary sect analysis: the birth's table for a relocated chart (S-D6)
        IReadOnlyList<SectInfo> planetarySect = sectFrom?.PlanetarySect ?? _sectService.Analyze(positions, isDayChart, houses);

        // 7. Triplicity rulers for Ascendant sign
        TriplicityRulerInfo ascTriplicity = _dignityService.GetTriplicityRulers(ascSign, isDayChart);

        return new ChartContextInfo
        {
            ChartRuler = chartRuler,
            AscendantSign = ascSign,
            ChartRulerDignity = rulerDignity,
            ChartRulerHouse = rulerHouse,
            IsChartRulerRetrograde = rulerRetrograde,
            IsDayChart = isDayChart,
            EventSignificator = significator,
            EventSignificatorDignity = significatorDignity,
            KeyHouses = keyHouses,
            AngularPlanets = angularPlanets,
            MutualReceptions = mutualReceptions,
            PlanetarySect = planetarySect,
            AscendantTriplicityRulers = ascTriplicity,
        };
    }

    /// <summary>
    /// Converts an ecliptic longitude (0-360) to a <see cref="ZodiacSign"/>.
    /// </summary>
    /// <param name="longitude">Ecliptic longitude in decimal degrees.</param>
    /// <returns>The zodiac sign containing that longitude.</returns>
    private static ZodiacSign LongitudeToSign(double longitude)
    {
        double normalized = ((longitude % 360.0) + 360.0) % 360.0;
        int signIndex = (int)(normalized / 30.0);

        // Handle the edge case of exactly 360.0
        if (signIndex >= 12)
            signIndex = 0;

        // ZodiacSign enum is 1-based (Aries = 1)
        return (ZodiacSign)(signIndex + 1);
    }

    /// <summary>
    /// Maps an event type string to its natural planetary significator and key houses.
    /// Returns null significator for birth charts or unrecognized event types.
    /// </summary>
    /// <param name="eventType">The event type string from ChartInput.</param>
    /// <returns>Tuple of significator planet name and key house numbers.</returns>
    private static (string? Significator, int[] KeyHouses) GetEventMapping(string? eventType)
    {
        return eventType?.ToLowerInvariant() switch
        {
            "businesslaunch" => ("Jupiter", [1, 10, 2]),
            "wedding" or "marriage" => ("Venus", [1, 7, 5]),
            "contract" => ("Mercury", [1, 7, 3]),
            "realestate" => ("Moon", [1, 4, 7]),
            "surgery" => ("Mars", [1, 6, 8]),
            "travel" => ("Jupiter", [1, 9, 3]),
            "creativeproject" => ("Venus", [1, 5, 10]),
            "jobstart" or "careerstart" => ("Saturn", [1, 10, 6]),
            "legalfiling" => ("Jupiter", [1, 7, 9]),
            "technologylaunch" => ("Mercury", [1, 10, 11]),
            "political" => ("Sun", [1, 10, 7]),
            "organization" => ("Saturn", [1, 10, 11]),
            "investment" => ("Jupiter", [1, 2, 8]),
            "education" => ("Mercury", [1, 9, 3]),
            "relocation" => ("Moon", [1, 4, 7]),
            "birth" or null => (null, Array.Empty<int>()),
            _ => (null, Array.Empty<int>()),
        };
    }

    /// <summary>
    /// Finds all pairs of traditional planets in mutual reception by domicile,
    /// exaltation, triplicity, or mixed types. Each pair is reported once with
    /// the strongest applicable reception type.
    /// </summary>
    /// <param name="positions">All planetary positions.</param>
    /// <param name="isDayChart">Whether this is a day chart (affects triplicity rulers).</param>
    /// <returns>List of mutual reception pairs ordered by strength.</returns>
    private IReadOnlyList<MutualReception> FindMutualReceptions(
        IReadOnlyList<PlanetPosition> positions, bool isDayChart)
    {
        List<PlanetPosition> traditionalPositions = positions
            .Where(p => TraditionalPlanets.Contains(p.Name))
            .ToList();

        List<MutualReception> receptions = [];

        for (int i = 0; i < traditionalPositions.Count; i++)
        {
            for (int j = i + 1; j < traditionalPositions.Count; j++)
            {
                PlanetPosition p1 = traditionalPositions[i];
                PlanetPosition p2 = traditionalPositions[j];

                string? receptionType = CheckReceptionType(p1, p2, isDayChart);
                if (receptionType is not null)
                {
                    receptions.Add(new MutualReception
                    {
                        Planet1 = p1.Name,
                        Planet1Sign = p1.Sign,
                        Planet2 = p2.Name,
                        Planet2Sign = p2.Sign,
                        ReceptionType = receptionType,
                    });
                }
            }
        }

        return receptions;
    }

    /// <summary>
    /// Checks whether two planets are in mutual reception and returns the
    /// strongest applicable reception type. Returns null if no reception exists.
    /// Priority order: Domicile → Exaltation → Mixed D-E → Triplicity → Mixed D-T → Mixed E-T.
    /// </summary>
    /// <param name="p1">First planet position.</param>
    /// <param name="p2">Second planet position.</param>
    /// <param name="isDayChart">Whether this is a day chart.</param>
    /// <returns>Reception type string or null if no reception.</returns>
    private string? CheckReceptionType(PlanetPosition p1, PlanetPosition p2, bool isDayChart)
    {
        // Determine each planet's dignity type in its current sign
        bool p1InP2Domicile = _dignityService.GetSignRuler(p1.Sign) == p2.Name;
        bool p2InP1Domicile = _dignityService.GetSignRuler(p2.Sign) == p1.Name;
        bool p1InP2Exaltation = IsExaltedIn(p2.Name, p1.Sign);
        bool p2InP1Exaltation = IsExaltedIn(p1.Name, p2.Sign);
        bool p1InP2Triplicity = _dignityService.GetActiveTriplicityRuler(p1.Sign, isDayChart) == p2.Name;
        bool p2InP1Triplicity = _dignityService.GetActiveTriplicityRuler(p2.Sign, isDayChart) == p1.Name;

        // Check in priority order (strongest first)
        if (p1InP2Domicile && p2InP1Domicile)
            return "Domicile";

        if (p1InP2Exaltation && p2InP1Exaltation)
            return "Exaltation";

        if ((p1InP2Domicile && p2InP1Exaltation) || (p1InP2Exaltation && p2InP1Domicile))
            return "Mixed (Domicile-Exaltation)";

        if (p1InP2Triplicity && p2InP1Triplicity)
            return "Triplicity";

        if ((p1InP2Domicile && p2InP1Triplicity) || (p1InP2Triplicity && p2InP1Domicile))
            return "Mixed (Domicile-Triplicity)";

        if ((p1InP2Exaltation && p2InP1Triplicity) || (p1InP2Triplicity && p2InP1Exaltation))
            return "Mixed (Exaltation-Triplicity)";

        return null;
    }

    /// <summary>
    /// Checks whether a planet is exalted in the given sign using the
    /// traditional exaltation table.
    /// </summary>
    /// <param name="planetName">Planet name.</param>
    /// <param name="sign">Zodiac sign to check.</param>
    /// <returns>True if the planet is exalted in this sign.</returns>
    private static bool IsExaltedIn(string planetName, ZodiacSign sign)
    {
        return (planetName, sign) switch
        {
            ("Sun", ZodiacSign.Aries) => true,
            ("Moon", ZodiacSign.Taurus) => true,
            ("Mercury", ZodiacSign.Virgo) => true,
            ("Venus", ZodiacSign.Pisces) => true,
            ("Mars", ZodiacSign.Capricorn) => true,
            ("Jupiter", ZodiacSign.Cancer) => true,
            ("Saturn", ZodiacSign.Libra) => true,
            _ => false,
        };
    }
}
