using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// Complete synastry analysis result containing both natal charts and all
/// inter-chart calculations.
/// </summary>
/// <remarks>
/// Produced by <see cref="AstroCast.Core.Services.SynastryService"/> which
/// compares two <see cref="ChartResult"/> instances, computing inter-chart
/// aspects with synastry-specific orbs and house overlays in both directions.
/// </remarks>
public sealed class SynastryResult
{
    /// <summary>Complete natal chart result for Person A.</summary>
    public required ChartResult PersonA { get; init; }

    /// <summary>Complete natal chart result for Person B.</summary>
    public required ChartResult PersonB { get; init; }

    /// <summary>Inter-chart aspects (A's bodies to B's bodies+angles and vice versa).</summary>
    public required IReadOnlyList<InterChartAspect> InterChartAspects { get; init; }

    /// <summary>
    /// The minor points' inter-chart aspects (branch AsteroidsExport, 1.6.0; R6, scoping decision 8): Person A's
    /// Ceres, Pallas, Juno, Vesta and Lilith to Person B's Sun to Pluto, Ascendant and Midheaven, then Person B's
    /// five to Person A's, names prefixed "A " and "B " as <see cref="InterChartAspects"/> prefixes them; the five
    /// Ptolemaic aspects at an orb of 2°, inclusive, never through <see cref="Configuration.SynastryOrbs"/>; the
    /// note "exact" below ten minutes, else none; sorted by orb. Never five to five, nor to the True Node, Chiron
    /// or the Part of Fortune. Empty unless both charts carry the five (<see cref="ChartResult.MinorPoints"/>).
    /// </summary>
    public IReadOnlyList<InterChartAspect> MinorPointAspects { get; init; } = [];

    /// <summary>Person A's planets placed in Person B's house system.</summary>
    public required IReadOnlyList<HouseOverlay> PersonAInPersonBHouses { get; init; }

    /// <summary>Person B's planets placed in Person A's house system.</summary>
    public required IReadOnlyList<HouseOverlay> PersonBInPersonAHouses { get; init; }

    /// <summary>The type of relationship being analyzed.</summary>
    public required RelationshipType RelationshipType { get; init; }

    /// <summary>Optional notes about the relationship (e.g., "married 15 years", "divorced 2020").</summary>
    public string? RelationshipNotes { get; init; }

    /// <summary>Engine version info.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>Timestamp when this analysis was performed.</summary>
    public required DateTime CalculatedAt { get; init; }
}
