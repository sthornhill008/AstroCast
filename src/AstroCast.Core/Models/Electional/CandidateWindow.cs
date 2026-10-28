namespace AstroCast.Core.Models.Electional;

/// <summary>
/// A single scored candidate time window for an election.
/// Contains the full chart, Moon condition, scoring breakdown,
/// and natal harmony analysis.
/// </summary>
public sealed class CandidateWindow
{
    /// <summary>Rank (1 = best). Set after sorting by score.</summary>
    public required int Rank { get; set; }

    /// <summary>Local date and time of the candidate election.</summary>
    public required DateTime LocalDateTime { get; init; }

    /// <summary>Full chart result calculated at this time and location.</summary>
    public required ChartResult Chart { get; init; }

    /// <summary>Moon condition at election time.</summary>
    public required MoonCondition Moon { get; init; }

    /// <summary>Planetary hour at election time.</summary>
    public required PlanetaryHour PlanetaryHour { get; init; }

    /// <summary>Overall score (0–100).</summary>
    public required double Score { get; init; }

    /// <summary>Breakdown of score by category.</summary>
    public required ScoreBreakdown ScoreBreakdown { get; init; }

    /// <summary>
    /// Inter-chart aspects between the candidate chart and the natal chart.
    /// Uses the same <see cref="InterChartAspect"/> model as synastry.
    /// </summary>
    public required IReadOnlyList<InterChartAspect> NatalHarmonyAspects { get; init; }

    /// <summary>Human-readable strengths of this window.</summary>
    public required IReadOnlyList<string> Strengths { get; init; }

    /// <summary>Human-readable compromises accepted in this window.</summary>
    public required IReadOnlyList<string> Compromises { get; init; }
}
