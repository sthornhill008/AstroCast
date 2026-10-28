using System.Reflection;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates a relocation chart: the natal chart recast at a different place at the same
/// birth instant, through <see cref="IChartEngine.CalculateRelocated"/>, with the house
/// changes between the two charts.
/// </summary>
/// <remarks>
/// <para>
/// <b>The instant and the clock:</b> the engine casts the relocation from the natal input
/// with the place changed, read through the birth's own clock, so the reading resolves to the
/// natal instant, the same Julian Day bit for bit, and the chart prints the birth clock
/// (branch K2, 1.3.4; ruling S-D6). Until 1.3.4 the service built an input at an explicit
/// offset of zero and the chart was a whole chart for the place on a UTC clock.
/// </para>
/// <para>
/// <b>What follows the place:</b> the angles, cusps, houses and placements, the altitudes,
/// the aspects to the angles, the angular planets, the Lots and the parans, and so the chart
/// ruler, mutual receptions and the Ascendant's triplicity rulers. <b>What stays the birth
/// chart's:</b> day or night, and with it the triplicity dignities and the Planetary Sect table;
/// the planetary hour; the printed clock; and, in the export, the time lords. Planetary
/// positions, signs, planet-to-planet aspects, midpoints and declination aspects are the same
/// at any place. The relocated chart keeps the natal chart's house system, and a horary chart
/// its category, judged at the place. In the house changes a planet is angular within 5° of an
/// angle (Q11), never by house number.
/// </para>
/// <para>
/// The service composes <see cref="IChartEngine"/> externally — it is
/// not injected into ChartEngine's constructor. This follows the same
/// pattern as <see cref="SynastryService"/>.
/// </para>
/// </remarks>
public sealed class RelocationService : IRelocationService
{
    private readonly IChartEngine _chartEngine;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="RelocationService"/>.
    /// </summary>
    /// <param name="chartEngine">Chart engine for computing the relocated chart.</param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    public RelocationService(IChartEngine chartEngine, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(chartEngine);
        ArgumentNullException.ThrowIfNull(logger);
        _chartEngine = chartEngine;
        _logger = logger.ForContext<RelocationService>();
    }

    /// <inheritdoc />
    public RelocationResult Calculate(
        ChartResult natalChart,
        RelocationInput target,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(natalChart);
        ArgumentNullException.ThrowIfNull(target);

        _logger.Debug(
            "Calculating relocation for '{Name}' to {Location} ({Lat:F4}, {Lon:F4})",
            natalChart.Input.Name, target.LocationName, target.Latitude, target.Longitude);

        // The engine casts the relocation: the natal input with the place changed, read through the
        // birth's own clock, keeping the birth's sect, dignities, planetary hour and horary category
        // (S-D6, branch K2, 1.3.4). Until 1.3.4 the service built an input at an explicit offset of
        // zero and cast a whole chart for the place at the natal Julian Day.
        ChartResult relocatedChart = _chartEngine.CalculateRelocated(natalChart, target, cancellationToken);

        // Build house change comparison: houses and angularity, within 5° of an angle (Q11)
        IReadOnlyList<HouseChange> houseChanges = CompareHouses(natalChart, relocatedChart);

        // Extract Ascendant and Midheaven signs
        ZodiacSign natalAscSign = LongitudeToSign(natalChart.Houses.Ascendant);
        ZodiacSign relocAscSign = LongitudeToSign(relocatedChart.Houses.Ascendant);
        ZodiacSign natalMcSign = LongitudeToSign(natalChart.Houses.Midheaven);
        ZodiacSign relocMcSign = LongitudeToSign(relocatedChart.Houses.Midheaven);

        // Engine version
        string asmVersion = typeof(RelocationService).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";
        int plusIndex = asmVersion.IndexOf('+');
        if (plusIndex >= 0) asmVersion = asmVersion[..plusIndex];

        _logger.Debug(
            "Relocation complete: {Changes} house changes, ASC {NatalAsc}\u2192{RelocAsc}, MC {NatalMc}\u2192{RelocMc}",
            houseChanges.Count, natalAscSign, relocAscSign, natalMcSign, relocMcSign);

        return new RelocationResult
        {
            NatalChart = natalChart,
            RelocatedChart = relocatedChart,
            RelocationTarget = target,
            HouseChanges = houseChanges,
            NatalAscendantSign = natalAscSign,
            RelocatedAscendantSign = relocAscSign,
            NatalMidheavenSign = natalMcSign,
            RelocatedMidheavenSign = relocMcSign,
            EngineVersion = $"AstroCast {asmVersion}",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Compares the two charts' house placements and angularity. A planet is angular in a chart when
    /// <see cref="AngularityService.FindAngularPlanets"/> lists it, within
    /// <see cref="AngularityService.AngularOrb"/> of an angle (Q11): "became angular" is angular at the place
    /// and not natally, "left angle" the reverse. Every planet whose house or angularity changed is listed —
    /// an angularity change with the same house number is a row with equal houses — ordered by
    /// significance: became angular first, then left angle, then by name. Until 1.3.4 the flags were by
    /// house number 1, 4, 7 and 10, and only a changed house made a row (Y3-6).
    /// </summary>
    /// <param name="natalChart">The natal chart.</param>
    /// <param name="relocatedChart">The relocated chart.</param>
    /// <returns>List of house changes, ordered by significance.</returns>
    private static IReadOnlyList<HouseChange> CompareHouses(ChartResult natalChart, ChartResult relocatedChart)
    {
        HashSet<string> natalAngular = AngularPlanetNames(natalChart);
        HashSet<string> relocatedAngular = AngularPlanetNames(relocatedChart);

        // Build lookup of relocated houses by planet name
        Dictionary<string, int?> relocatedHouses = new(StringComparer.OrdinalIgnoreCase);
        foreach (PlanetPosition pos in relocatedChart.Positions)
        {
            relocatedHouses[pos.Name] = pos.House;
        }

        List<HouseChange> changes = [];

        foreach (PlanetPosition natal in natalChart.Positions)
        {
            if (!natal.House.HasValue)
                continue;

            if (!relocatedHouses.TryGetValue(natal.Name, out int? relocHouse) || !relocHouse.HasValue)
                continue;

            bool wasAngular = natalAngular.Contains(natal.Name);
            bool isAngular = relocatedAngular.Contains(natal.Name);

            if (natal.House.Value == relocHouse.Value && wasAngular == isAngular)
                continue; // No change

            changes.Add(new HouseChange
            {
                PlanetName = natal.Name,
                NatalHouse = natal.House.Value,
                RelocatedHouse = relocHouse.Value,
                BecameAngular = !wasAngular && isAngular,
                LeftAngle = wasAngular && !isAngular,
            });
        }

        // Sort: became angular first, then left angle, then by name
        return changes
            .OrderByDescending(c => c.BecameAngular)
            .ThenByDescending(c => c.LeftAngle)
            .ThenBy(c => c.PlanetName)
            .ToList();
    }

    /// <summary>
    /// The names of the planets within <see cref="AngularityService.AngularOrb"/> of an angle of a chart.
    /// </summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The angular planets' names.</returns>
    private static HashSet<string> AngularPlanetNames(ChartResult chart) =>
        AngularityService.FindAngularPlanets(chart.Positions, chart.Houses)
            .Select(a => a.PlanetName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Converts an ecliptic longitude to a zodiac sign.
    /// </summary>
    private static ZodiacSign LongitudeToSign(double longitude)
    {
        double normalized = ((longitude % 360.0) + 360.0) % 360.0;
        int signIndex = (int)(normalized / 30.0);
        if (signIndex >= 12) signIndex = 0;
        return (ZodiacSign)(signIndex + 1);
    }
}
