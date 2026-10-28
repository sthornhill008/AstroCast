using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Utilities;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates astrological aspects between planet pairs and between planets
/// and chart angles (ASC, MC). An aspect is applying when its orb is decreasing
/// at the chart's instant, by the speeds in longitude the positions carry, and
/// separating otherwise (<see cref="AspectDirection"/>); the angles are held still.
/// </summary>
public sealed class AspectService : IAspectService
{
    /// <summary>The orb of every minor-point aspect, in degrees, inclusive (R5, branch Asteroids, 1.6.0).</summary>
    internal const double MinorPointOrb = 2.0;

    /// <summary>
    /// The five Ptolemaic aspects, the definitions of <see cref="AspectDefinition.All"/> whose category is major,
    /// in that list's order: the aspects of the minor points, in a chart and across two (<see cref="SynastryService"/>).
    /// </summary>
    internal static readonly IReadOnlyList<AspectDefinition> MajorAspects =
        AspectDefinition.All.Where(d => d.Category == AspectCategory.Major).ToList().AsReadOnly();

    private readonly IEphemerisService _ephemerisService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="AspectService"/>.
    /// </summary>
    /// <param name="ephemerisService">
    /// Ephemeris service. Not read since 1.3.3, when the applying test stopped recalculating
    /// positions a time step ahead and began using the speeds the positions carry; it is kept,
    /// and still required, so that no caller changes.
    /// </param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    public AspectService(IEphemerisService ephemerisService, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(ephemerisService);
        ArgumentNullException.ThrowIfNull(logger);

        _ephemerisService = ephemerisService;
        _logger = logger.ForContext<AspectService>();
    }

    /// <inheritdoc />
    /// <param name="positions">Planet positions; each one's speed in longitude decides applying or separating.</param>
    /// <param name="julianDayUt">
    /// The chart's instant (Julian Day, UT). Not read: the positions' speeds give the orb's rate at
    /// the instant (<see cref="AspectDirection"/>).
    /// </param>
    public IReadOnlyList<AspectResult> CalculatePlanetAspects(
        IReadOnlyList<PlanetPosition> positions,
        double julianDayUt)
    {
        ArgumentNullException.ThrowIfNull(positions);

        return CalculatePlanetAspectsCore(positions);
    }

    /// <inheritdoc />
    /// <param name="positions">Planet positions; each one's speed in longitude decides applying or separating.</param>
    /// <param name="houseData">House data containing ASC and MC longitudes. The angles are held still (ruling D5).</param>
    /// <param name="julianDayUt">
    /// The chart's instant (Julian Day, UT). Not read: the positions' speeds give the orb's rate at
    /// the instant (<see cref="AspectDirection"/>).
    /// </param>
    public IReadOnlyList<AspectResult> CalculateAngleAspects(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houseData,
        double julianDayUt)
    {
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(houseData);

        return CalculateAngleAspectsCore(positions, houseData);
    }

    /// <inheritdoc />
    /// <param name="positions">Planet positions; each one's speed in longitude decides applying or separating.</param>
    /// <param name="houseData">House data containing ASC and MC longitudes. The angles are held still (ruling D5).</param>
    /// <param name="julianDayUt">
    /// The chart's instant (Julian Day, UT). Not read: the positions' speeds give the orb's rate at
    /// the instant (<see cref="AspectDirection"/>).
    /// </param>
    public IReadOnlyList<AspectResult> CalculateAllAspects(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houseData,
        double julianDayUt)
    {
        IReadOnlyList<AspectResult> planetAspects =
            CalculatePlanetAspectsCore(positions);
        IReadOnlyList<AspectResult> angleAspects =
            CalculateAngleAspectsCore(positions, houseData);

        List<AspectResult> combined = new(planetAspects.Count + angleAspects.Count);
        combined.AddRange(planetAspects);
        combined.AddRange(angleAspects);
        combined.Sort((a, b) => a.Orb.CompareTo(b.Orb));

        _logger.Debug(
            "Calculated {Total} total aspects ({Planet} planet-to-planet, {Angle} planet-to-angle)",
            combined.Count, planetAspects.Count, angleAspects.Count);

        return combined.AsReadOnly();
    }

    /// <inheritdoc />
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public IReadOnlyList<AspectResult> CalculateMinorPointAspects(
        IReadOnlyList<PlanetPosition> minorPoints,
        IReadOnlyList<PlanetPosition> positions,
        HouseData houses)
    {
        ArgumentNullException.ThrowIfNull(minorPoints);
        ArgumentNullException.ThrowIfNull(positions);
        ArgumentNullException.ThrowIfNull(houses);

        // The targets: the Sun to Pluto by body, in the positions' order, then the angles held still
        // (ruling D5). The True Node, Chiron and the five themselves are never targets, and the Part
        // of Fortune is a Lot, never a position.
        List<(string Name, double Longitude, double Speed)> targets = new(positions.Count + 2);
        foreach (PlanetPosition position in positions)
        {
            if (IsMinorPointTarget(position.Body))
            {
                targets.Add((position.Name, position.Longitude, position.SpeedInLongitude));
            }
        }

        targets.Add(("ASC", houses.Ascendant, 0.0));
        targets.Add(("MC", houses.Midheaven, 0.0));

        List<AspectResult> results = [];
        foreach (PlanetPosition point in minorPoints)
        {
            foreach ((string targetName, double targetLongitude, double targetSpeed) in targets)
            {
                AspectResult? aspect = FindAspect(
                    point.Name, point.Longitude,
                    targetName, targetLongitude,
                    point.SpeedInLongitude, targetSpeed,
                    MajorAspects, static _ => MinorPointOrb);

                if (aspect is not null)
                {
                    results.Add(aspect);
                    _logger.Debug(
                        "Detected {Aspect} between {Point} and {Target}: orb {Orb:F2}° ({AppSep})",
                        aspect.Aspect.Type, point.Name, targetName,
                        aspect.Orb, aspect.ApplyingSeparating);
                }
            }
        }

        results.Sort((a, b) => a.Orb.CompareTo(b.Orb));
        return results.AsReadOnly();
    }

    /// <summary>
    /// Whether a body is a target of the minor points' aspects: the Sun to Pluto (Swiss Ephemeris numbers 0 to
    /// 9), never the True Node, Chiron or one of the five. The synastry service reads it for the other chart's
    /// targets (branch AsteroidsExport, 1.6.0).
    /// </summary>
    /// <param name="body">The body.</param>
    /// <returns><see langword="true"/> for the Sun to Pluto.</returns>
    internal static bool IsMinorPointTarget(CelestialBodyId body) =>
        body >= CelestialBodyId.Sun && body <= CelestialBodyId.Pluto;

    /// <summary>
    /// Calculate the shortest angular separation between two ecliptic longitudes.
    /// Result is always 0–180°.
    /// </summary>
    /// <param name="lon1">First longitude in degrees (0–360).</param>
    /// <param name="lon2">Second longitude in degrees (0–360).</param>
    /// <returns>Shortest arc distance in degrees (0–180).</returns>
    internal static double AngularSeparation(double lon1, double lon2)
    {
        double diff = Math.Abs(lon1 - lon2);
        if (diff > 180.0)
        {
            diff = 360.0 - diff;
        }

        return diff;
    }

    /// <summary>
    /// Finds the aspect of a given type between two named chart points in a
    /// pre-computed aspect list, matching either body ordering.
    /// </summary>
    /// <remarks>
    /// Used by the export formatters to resolve the canonical orb for an
    /// aspect: the value carried by the chart's aspect table is the single
    /// source of truth for that aspect at that chart moment (2026-06-10
    /// live-run review, Defect C requirement C1).
    /// </remarks>
    /// <param name="aspects">The chart's computed aspect list.</param>
    /// <param name="body1">Name of the first chart point (e.g., "Moon").</param>
    /// <param name="body2">Name of the second chart point (e.g., "Jupiter").</param>
    /// <param name="aspectType">The aspect type to match.</param>
    /// <returns>The matching <see cref="AspectResult"/>, or null when the pair has no aspect of that type in the list.</returns>
    /// <exception cref="ArgumentNullException">Thrown when any argument is null.</exception>
    public static AspectResult? FindAspectBetween(
        IReadOnlyList<AspectResult> aspects,
        string body1,
        string body2,
        AspectType aspectType)
    {
        ArgumentNullException.ThrowIfNull(aspects);
        ArgumentNullException.ThrowIfNull(body1);
        ArgumentNullException.ThrowIfNull(body2);

        foreach (AspectResult aspect in aspects)
        {
            if (aspect.Aspect.Type != aspectType)
            {
                continue;
            }

            bool forwardMatch =
                string.Equals(aspect.Body1Name, body1, StringComparison.OrdinalIgnoreCase)
                && string.Equals(aspect.Body2Name, body2, StringComparison.OrdinalIgnoreCase);
            bool reverseMatch =
                string.Equals(aspect.Body1Name, body2, StringComparison.OrdinalIgnoreCase)
                && string.Equals(aspect.Body2Name, body1, StringComparison.OrdinalIgnoreCase);

            if (forwardMatch || reverseMatch)
            {
                return aspect;
            }
        }

        return null;
    }

    /// <summary>
    /// Core implementation of planet-to-planet aspect calculation; each pair's
    /// speeds in longitude decide applying or separating.
    /// </summary>
    /// <param name="positions">Current planet positions.</param>
    /// <returns>Planet-to-planet aspects sorted by orb.</returns>
    private IReadOnlyList<AspectResult> CalculatePlanetAspectsCore(
        IReadOnlyList<PlanetPosition> positions)
    {
        List<AspectResult> results = [];

        for (int i = 0; i < positions.Count; i++)
        {
            for (int j = i + 1; j < positions.Count; j++)
            {
                PlanetPosition body1 = positions[i];
                PlanetPosition body2 = positions[j];

                AspectResult? aspect = FindAspect(
                    body1.Name, body1.Longitude,
                    body2.Name, body2.Longitude,
                    body1.SpeedInLongitude,
                    body2.SpeedInLongitude);

                if (aspect is not null)
                {
                    results.Add(aspect);
                    _logger.Debug(
                        "Detected {Aspect} between {Body1} and {Body2}: orb {Orb:F2}° ({AppSep})",
                        aspect.Aspect.Type, body1.Name, body2.Name,
                        aspect.Orb, aspect.ApplyingSeparating);
                }
            }
        }

        results.Sort((a, b) => a.Orb.CompareTo(b.Orb));
        return results.AsReadOnly();
    }

    /// <summary>
    /// Core implementation of planet-to-angle aspect calculation; the planet's
    /// speed in longitude decides applying or separating, the angle held still.
    /// </summary>
    /// <param name="positions">Current planet positions.</param>
    /// <param name="houseData">House data containing ASC and MC longitudes.</param>
    /// <returns>Planet-to-angle aspects sorted by orb.</returns>
    private IReadOnlyList<AspectResult> CalculateAngleAspectsCore(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houseData)
    {
        // Define the angle points to check
        (string Name, double Longitude)[] angles =
        [
            ("ASC", houseData.Ascendant),
            ("MC", houseData.Midheaven),
        ];

        List<AspectResult> results = [];

        for (int i = 0; i < positions.Count; i++)
        {
            PlanetPosition planet = positions[i];

            foreach ((string angleName, double angleLongitude) in angles)
            {
                // For planet-to-angle, the angle is held still: speed 0 (ruling D5)
                AspectResult? aspect = FindAspect(
                    planet.Name, planet.Longitude,
                    angleName, angleLongitude,
                    planet.SpeedInLongitude,
                    0.0);

                if (aspect is not null)
                {
                    results.Add(aspect);
                    _logger.Debug(
                        "Detected {Aspect} between {Body} and {Angle}: orb {Orb:F2}° ({AppSep})",
                        aspect.Aspect.Type, planet.Name, angleName,
                        aspect.Orb, aspect.ApplyingSeparating);
                }
            }
        }

        results.Sort((a, b) => a.Orb.CompareTo(b.Orb));
        return results.AsReadOnly();
    }

    /// <summary>
    /// Checks all aspect definitions, each at its own orb, against the angular separation of two points.
    /// Returns the first matching aspect, or null if no aspect is within orb.
    /// </summary>
    /// <param name="name1">Name of the first point.</param>
    /// <param name="lon1">Current longitude of the first point.</param>
    /// <param name="name2">Name of the second point.</param>
    /// <param name="lon2">Current longitude of the second point.</param>
    /// <param name="speed1">Speed in longitude of the first point.</param>
    /// <param name="speed2">Speed in longitude of the second point; 0 for an angle.</param>
    /// <returns>An <see cref="AspectResult"/> if an aspect is found, otherwise null.</returns>
    private static AspectResult? FindAspect(
        string name1, double lon1,
        string name2, double lon2,
        double speed1, double speed2) =>
        FindAspect(name1, lon1, name2, lon2, speed1, speed2, AspectDefinition.All, static d => d.Orb);

    /// <summary>
    /// Checks the given aspect definitions, in their order, against the angular separation of two points,
    /// each at the orb the selector gives it. Returns the first matching aspect, or null if no aspect is
    /// within orb. The chart's own aspects pass every definition at its own orb; the minor points' pass the
    /// five Ptolemaic aspects at 2° (branch Asteroids, 1.6.0).
    /// </summary>
    /// <param name="name1">Name of the first point.</param>
    /// <param name="lon1">Current longitude of the first point.</param>
    /// <param name="name2">Name of the second point.</param>
    /// <param name="lon2">Current longitude of the second point.</param>
    /// <param name="speed1">Speed in longitude of the first point.</param>
    /// <param name="speed2">Speed in longitude of the second point; 0 for an angle.</param>
    /// <param name="definitions">The aspect definitions to check, in order; the first match wins.</param>
    /// <param name="orb">The orb allowed for a definition, in degrees, inclusive.</param>
    /// <returns>An <see cref="AspectResult"/> if an aspect is found, otherwise null.</returns>
    private static AspectResult? FindAspect(
        string name1, double lon1,
        string name2, double lon2,
        double speed1, double speed2,
        IReadOnlyList<AspectDefinition> definitions,
        Func<AspectDefinition, double> orb)
    {
        double separation = AngularSeparation(lon1, lon2);

        foreach (AspectDefinition definition in definitions)
        {
            double orbActual = Math.Abs(separation - definition.Angle);

            if (orbActual <= orb(definition))
            {
                // Applying when the orb is decreasing at the instant
                bool isApplying = AspectDirection.IsApplying(
                    lon1, speed1, lon2, speed2, definition.Angle);

                return new AspectResult
                {
                    Body1Name = name1,
                    Body1Longitude = lon1,
                    Body2Name = name2,
                    Body2Longitude = lon2,
                    Aspect = definition,
                    Orb = orbActual,
                    IsApplying = isApplying,
                };
            }
        }

        return null;
    }
}
