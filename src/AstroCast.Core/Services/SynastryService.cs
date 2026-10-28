using System.Reflection;

using Serilog;

using AstroCast.Core.Configuration;
using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;

namespace AstroCast.Core.Services;

/// <summary>
/// Calculates synastry (inter-chart comparison) data between two natal charts.
/// Produces inter-chart aspects using synastry-specific orbs and determines
/// house overlays in both directions.
/// </summary>
/// <remarks>
/// <para>
/// Inter-chart aspects differ from natal aspects in two key ways:
/// (1) orbs vary by planet category (luminaries vs personal vs outer vs angles), and
/// (2) there is no applying/separating concept (static chart comparison).
/// </para>
/// <para>
/// House overlays place each person's planets into the other person's own house
/// system, any of the nine, showing where planetary energies are activated in the partner's chart.
/// </para>
/// <para>
/// When both charts carry Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsExport, 1.6.0; R6, scoping
/// decision 8), each chart's five are aspected to the other chart's Sun to Pluto, Ascendant and Midheaven with
/// the five Ptolemaic aspects at an orb of 2°, in a list of their own (<see cref="SynastryResult.MinorPointAspects"/>);
/// never five to five, never through <see cref="SynastryOrbs"/>, and never in the house overlays.
/// </para>
/// </remarks>
public sealed class SynastryService : ISynastryService
{
    /// <summary>Orb threshold (in degrees) below which an aspect is annotated "exact".</summary>
    private const double ExactOrbThreshold = 10.0 / 60.0; // 10 arcminutes = 0.1667°

    /// <summary>Orb threshold (in degrees) above which an aspect is annotated "wide".</summary>
    private const double WideOrbThreshold = 5.0;

    private readonly ILogger _logger;

    /// <summary>
    /// Initialises a new instance of <see cref="SynastryService"/>.
    /// </summary>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    public SynastryService(ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger.ForContext<SynastryService>();
    }

    /// <inheritdoc />
    public SynastryResult Calculate(
        ChartResult personA,
        ChartResult personB,
        RelationshipType relationshipType = RelationshipType.Unspecified,
        string? relationshipNotes = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(personA);
        ArgumentNullException.ThrowIfNull(personB);

        cancellationToken.ThrowIfCancellationRequested();

        _logger.Debug(
            "Calculating synastry: '{PersonA}' & '{PersonB}'",
            personA.Input.Name, personB.Input.Name);

        // Step 1: Calculate inter-chart aspects
        IReadOnlyList<InterChartAspect> interChartAspects =
            CalculateInterChartAspects(personA, personB, cancellationToken);

        // Step 1b: the minor points' inter-chart aspects, only when both charts carry the five (R6, decision 8)
        IReadOnlyList<InterChartAspect> minorPointAspects =
            personA.MinorPoints.Count > 0 && personB.MinorPoints.Count > 0
                ? CalculateMinorPointAspects(personA, personB, cancellationToken)
                : [];

        // Step 2: Calculate house overlays
        IReadOnlyList<HouseOverlay> aInBHouses =
            CalculateHouseOverlays(personA.Positions, personB.Houses, "A");
        IReadOnlyList<HouseOverlay> bInAHouses =
            CalculateHouseOverlays(personB.Positions, personA.Houses, "B");

        // Step 3: Build version string
        string engineVersion = BuildVersionString(personA);

        _logger.Information(
            "Synastry complete: {AspectCount} inter-chart aspects, {MinorPointAspectCount} minor-point aspects, " +
            "{AinBCount} A-in-B overlays, {BinACount} B-in-A overlays",
            interChartAspects.Count, minorPointAspects.Count, aInBHouses.Count, bInAHouses.Count);

        return new SynastryResult
        {
            PersonA = personA,
            PersonB = personB,
            InterChartAspects = interChartAspects,
            MinorPointAspects = minorPointAspects,
            PersonAInPersonBHouses = aInBHouses,
            PersonBInPersonAHouses = bInAHouses,
            RelationshipType = relationshipType,
            RelationshipNotes = string.IsNullOrWhiteSpace(relationshipNotes) ? null : relationshipNotes,
            EngineVersion = engineVersion,
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Calculate all inter-chart aspects between Person A's bodies and
    /// Person B's bodies + angles, plus Person B's bodies against Person A's angles.
    /// </summary>
    /// <param name="personA">Person A's chart result.</param>
    /// <param name="personB">Person B's chart result.</param>
    /// <param name="cancellationToken">
    /// Checked at the top of each outer point-pair loop so a pending cancel
    /// request aborts the sweep quickly without leaving half-built results.
    /// </param>
    /// <returns>Sorted list of inter-chart aspects (tightest orb first).</returns>
    private IReadOnlyList<InterChartAspect> CalculateInterChartAspects(
        ChartResult personA, ChartResult personB, CancellationToken cancellationToken)
    {
        // Build point lists with prefixed names
        List<(string Name, double Longitude)> aBodyPoints = personA.Positions
            .Select(p => ($"A {p.Name}", p.Longitude))
            .ToList();

        List<(string Name, double Longitude)> bBodyPoints = personB.Positions
            .Select(p => ($"B {p.Name}", p.Longitude))
            .ToList();

        List<(string Name, double Longitude)> bAnglePoints =
        [
            ("B ASC", personB.Houses.Ascendant),
            ("B MC", personB.Houses.Midheaven),
        ];

        List<(string Name, double Longitude)> aAnglePoints =
        [
            ("A ASC", personA.Houses.Ascendant),
            ("A MC", personA.Houses.Midheaven),
        ];

        List<InterChartAspect> results = [];

        // A's 12 bodies × B's 14 points (12 bodies + ASC + MC)
        List<(string Name, double Longitude)> bAllPoints = [.. bBodyPoints, .. bAnglePoints];
        foreach ((string aName, double aLon) in aBodyPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach ((string bName, double bLon) in bAllPoints)
            {
                InterChartAspect? aspect = FindInterChartAspect(aName, aLon, bName, bLon);
                if (aspect is not null)
                    results.Add(aspect);
            }
        }

        // B's 12 bodies × A's 2 angles (A ASC, A MC)
        // Only check aspects not already found (A body→B angle covers A angle→B body indirectly,
        // but B body→A angle is a different direction)
        foreach ((string bName, double bLon) in bBodyPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach ((string aName, double aLon) in aAnglePoints)
            {
                InterChartAspect? aspect = FindInterChartAspect(bName, bLon, aName, aLon);
                if (aspect is not null)
                    results.Add(aspect);
            }
        }

        // Sort by orb ascending (tightest first), a tie by the names then the angle
        results.Sort(CompareByOrbThenNames);

        _logger.Debug("Found {Count} inter-chart aspects", results.Count);
        return results.AsReadOnly();
    }

    /// <summary>
    /// Check all aspect definitions against the angular separation of two points,
    /// using synastry-specific orbs based on planet category.
    /// </summary>
    /// <param name="name1">Prefixed name of the first point (e.g., "A Sun").</param>
    /// <param name="lon1">Ecliptic longitude of the first point.</param>
    /// <param name="name2">Prefixed name of the second point (e.g., "B Venus").</param>
    /// <param name="lon2">Ecliptic longitude of the second point.</param>
    /// <returns>An <see cref="InterChartAspect"/> if found, otherwise null.</returns>
    private static InterChartAspect? FindInterChartAspect(
        string name1, double lon1,
        string name2, double lon2)
    {
        double separation = AspectService.AngularSeparation(lon1, lon2);

        foreach (AspectDefinition definition in AspectDefinition.All)
        {
            double synastryOrb = SynastryOrbs.GetOrb(definition.Type, name1, name2);
            double orbActual = Math.Abs(separation - definition.Angle);

            if (orbActual <= synastryOrb)
            {
                // Determine note
                string? note = null;
                if (orbActual < ExactOrbThreshold)
                    note = "exact";
                else if (orbActual > WideOrbThreshold)
                    note = "wide";

                return new InterChartAspect
                {
                    Body1Name = name1,
                    Body1Longitude = lon1,
                    Body2Name = name2,
                    Body2Longitude = lon2,
                    Aspect = definition,
                    Orb = orbActual,
                    Note = note,
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Calculate the minor points' inter-chart aspects (branch AsteroidsExport, 1.6.0; R6, scoping decision 8):
    /// Person A's five to Person B's Sun to Pluto, "B ASC" and "B MC", then Person B's five to Person A's Sun to
    /// Pluto, "A ASC" and "A MC", the names prefixed as the inter-chart aspects are; the five Ptolemaic aspects
    /// (<see cref="AspectService.MajorAspects"/>, in their order, the first match winning) at the minor points' orb
    /// (<see cref="AspectService.MinorPointOrb"/>, 2°, inclusive), never through <see cref="SynastryOrbs"/>, which
    /// files an unknown name under Outer; the note "exact" below <see cref="ExactOrbThreshold"/>, else none; sorted
    /// by orb, tightest first. Never five to five, nor to the True Node, Chiron or the Part of Fortune.
    /// </summary>
    /// <param name="personA">Person A's chart, carrying the five.</param>
    /// <param name="personB">Person B's chart, carrying the five.</param>
    /// <param name="cancellationToken">Checked once per minor point.</param>
    /// <returns>The aspects, sorted by orb.</returns>
    private IReadOnlyList<InterChartAspect> CalculateMinorPointAspects(
        ChartResult personA, ChartResult personB, CancellationToken cancellationToken)
    {
        List<InterChartAspect> results = [];
        AddMinorPointAspects(results, personA.MinorPoints, "A", personB, "B", cancellationToken);
        AddMinorPointAspects(results, personB.MinorPoints, "B", personA, "A", cancellationToken);

        results.Sort(CompareByOrbThenNames);

        _logger.Debug("Found {Count} minor-point inter-chart aspects", results.Count);
        return results.AsReadOnly();
    }

    /// <summary>
    /// The order of both inter-chart lists: by <see cref="InterChartAspect.Orb"/>, tightest first, and a tied orb by
    /// <see cref="InterChartAspect.Body1Name"/>, then <see cref="InterChartAspect.Body2Name"/>, both ordinal, then
    /// the aspect's angle, so the order is defined by the code (1.6.1, item G, branch TiesAndGuards). Until 1.6.1 a
    /// tie came out as <see cref="List{T}.Sort(Comparison{T})"/>'s introsort left it: the same on every run of one
    /// build, but undefined by the code and free to move with a sort change or an upstream reorder.
    /// </summary>
    /// <param name="a">The first aspect.</param>
    /// <param name="b">The second aspect.</param>
    /// <returns>Negative when <paramref name="a"/> sorts first, positive when <paramref name="b"/> does, zero when they are one.</returns>
    /// <exception cref="ArgumentNullException">Thrown when either aspect is null.</exception>
    internal static int CompareByOrbThenNames(InterChartAspect a, InterChartAspect b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);

        int byOrb = a.Orb.CompareTo(b.Orb);
        if (byOrb != 0)
            return byOrb;
        int byBody1 = string.CompareOrdinal(a.Body1Name, b.Body1Name);
        if (byBody1 != 0)
            return byBody1;
        int byBody2 = string.CompareOrdinal(a.Body2Name, b.Body2Name);
        return byBody2 != 0 ? byBody2 : a.Aspect.Angle.CompareTo(b.Aspect.Angle);
    }

    /// <summary>
    /// Adds one chart's minor points' aspects to the other chart's targets: its Sun to Pluto by body
    /// (<see cref="AspectService.IsMinorPointTarget"/>), in its positions' order, then its Ascendant and Midheaven.
    /// </summary>
    /// <param name="results">The list to add to.</param>
    /// <param name="minorPoints">One chart's five.</param>
    /// <param name="pointPrefix">That chart's prefix, "A" or "B".</param>
    /// <param name="other">The other chart.</param>
    /// <param name="targetPrefix">The other chart's prefix, "B" or "A".</param>
    /// <param name="cancellationToken">Checked once per minor point.</param>
    private static void AddMinorPointAspects(
        List<InterChartAspect> results,
        IReadOnlyList<PlanetPosition> minorPoints,
        string pointPrefix,
        ChartResult other,
        string targetPrefix,
        CancellationToken cancellationToken)
    {
        List<(string Name, double Longitude)> targets = new(other.Positions.Count + 2);
        foreach (PlanetPosition position in other.Positions)
        {
            if (AspectService.IsMinorPointTarget(position.Body))
                targets.Add(($"{targetPrefix} {position.Name}", position.Longitude));
        }

        targets.Add(($"{targetPrefix} ASC", other.Houses.Ascendant));
        targets.Add(($"{targetPrefix} MC", other.Houses.Midheaven));

        foreach (PlanetPosition point in minorPoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach ((string targetName, double targetLongitude) in targets)
            {
                InterChartAspect? aspect = FindMinorPointAspect(
                    $"{pointPrefix} {point.Name}", point.Longitude, targetName, targetLongitude);
                if (aspect is not null)
                    results.Add(aspect);
            }
        }
    }

    /// <summary>
    /// Check the five Ptolemaic aspects, in their order, against the angular separation of a minor point and a
    /// target, each at the minor points' orb of 2°, inclusive; the first match wins. The row's orb is the
    /// separation found, never the definition's own.
    /// </summary>
    /// <param name="name1">The minor point's prefixed name (e.g., "A Ceres").</param>
    /// <param name="lon1">The minor point's longitude.</param>
    /// <param name="name2">The target's prefixed name (e.g., "B Uranus", "B MC").</param>
    /// <param name="lon2">The target's longitude.</param>
    /// <returns>The aspect, or null when none is within orb.</returns>
    private static InterChartAspect? FindMinorPointAspect(
        string name1, double lon1,
        string name2, double lon2)
    {
        double separation = AspectService.AngularSeparation(lon1, lon2);

        foreach (AspectDefinition definition in AspectService.MajorAspects)
        {
            double orbActual = Math.Abs(separation - definition.Angle);

            if (orbActual <= AspectService.MinorPointOrb)
            {
                return new InterChartAspect
                {
                    Body1Name = name1,
                    Body1Longitude = lon1,
                    Body2Name = name2,
                    Body2Longitude = lon2,
                    Aspect = definition,
                    Orb = orbActual,
                    Note = orbActual < ExactOrbThreshold ? "exact" : null,
                };
            }
        }

        return null;
    }

    /// <summary>
    /// Calculate house overlays: place one person's planets into the other
    /// person's house system.
    /// </summary>
    /// <param name="positions">Planetary positions to place.</param>
    /// <param name="houses">Target house system to place planets into.</param>
    /// <param name="prefix">Name prefix ("A" or "B") for the body names.</param>
    /// <returns>List of house overlay placements.</returns>
    private static IReadOnlyList<HouseOverlay> CalculateHouseOverlays(
        IReadOnlyList<PlanetPosition> positions,
        HouseData houses,
        string prefix)
    {
        List<HouseOverlay> overlays = new(positions.Count);

        foreach (PlanetPosition planet in positions)
        {
            int house = HousePlacement.HouseOf(planet.Longitude, houses.Cusps) ?? 1;

            overlays.Add(new HouseOverlay
            {
                BodyName = $"{prefix} {planet.Name}",
                FormattedPosition = PositionFormatter.FormatPosition(planet),
                Longitude = planet.Longitude,
                House = house,
            });
        }

        return overlays.AsReadOnly();
    }

    /// <summary>
    /// Build the engine version string from Person A's chart result
    /// (both charts use the same engine).
    /// </summary>
    /// <param name="chart">Any chart result to extract version info from.</param>
    /// <returns>Version string (e.g., "AstroCast 1.0.0 (SwissEph 2.10.03)").</returns>
    private static string BuildVersionString(ChartResult chart) => chart.EngineVersion;
}
