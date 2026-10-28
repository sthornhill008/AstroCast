namespace AstroCast.Core.Models.Electional;

/// <summary>
/// Complete result of an electional calculation. Contains ranked candidate
/// windows, disqualified periods, and the Moon void-of-course calendar.
/// </summary>
public sealed class ElectionalResult
{
    /// <summary>The input parameters used for this calculation.</summary>
    public required ElectionalInput Input { get; init; }

    /// <summary>The client's natal chart.</summary>
    public required ChartResult NatalChart { get; init; }

    /// <summary>Ranked candidate windows (best first).</summary>
    public required IReadOnlyList<CandidateWindow> Candidates { get; init; }

    /// <summary>
    /// Number of candidate windows the caller asked for, recorded alongside the
    /// number actually delivered in <see cref="Candidates"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This duplicates <see cref="ElectionalInput.CandidateCount"/> on purpose.
    /// A search can legitimately deliver fewer candidates than requested: a
    /// narrow date range, aggressive time-of-day or day-of-week constraints, or
    /// a long Mercury-retrograde stretch can leave a window physically unable to
    /// supply the requested number of moments that are far enough apart to count
    /// as distinct. Before this field existed the caller received a short list
    /// with nothing to distinguish "the window only had three good moments" from
    /// "the engine lost two of them", and the export said only
    /// <c>Candidates: 3</c>. Making the request a first-class property of the
    /// result means every consumer -- formatter, view model, future JSON export
    /// -- can report the shortfall without reaching back into the input, and
    /// cannot forget that a shortfall is possible.
    /// </para>
    /// <para>
    /// Declared <c>required</c> rather than defaulted so that any future
    /// construction site must state the requested count explicitly; a silent
    /// default of zero would make <see cref="IsRequestUnmet"/> permanently
    /// false and hide exactly the condition this field exists to surface.
    /// </para>
    /// </remarks>
    public required int RequestedCandidateCount { get; init; }

    /// <summary>
    /// Gets a value indicating whether the search delivered fewer candidate
    /// windows than were requested.
    /// </summary>
    /// <value>
    /// <c>true</c> when <see cref="Candidates"/> holds fewer entries than
    /// <see cref="RequestedCandidateCount"/>; otherwise <c>false</c>.
    /// </value>
    /// <remarks>
    /// A <c>true</c> value is a statement about the search window, not an error:
    /// the engine returns every qualifying moment it can find that satisfies the
    /// minimum separation. Consumers should surface it so the user can widen the
    /// range or relax constraints, rather than silently presenting a short list.
    /// </remarks>
    public bool IsRequestUnmet => Candidates.Count < RequestedCandidateCount;

    /// <summary>
    /// Number of eligible samples the search did not score because they have no planetary hour.
    /// </summary>
    /// <remarks>
    /// <para>
    /// An eligible sample lies inside the range, the time and day limits, and outside every
    /// disqualified period. On a day when the Sun does not rise or set at the place (beyond the
    /// polar circles, around the solstices) there is no planetary hour, so a sample on that day
    /// is not scored, and is counted here. Every candidate in <see cref="Candidates"/> has a
    /// planetary hour. Until 1.3.3 the first such sample ended the whole search with the
    /// engine's exception (Steve's ruling 2 of 2026-10-09, branch O3).
    /// </para>
    /// <para>
    /// Declared <c>required</c> for the reason <see cref="RequestedCandidateCount"/> gives: every
    /// construction site must state the count, and a silent default of zero would hide exactly
    /// the samples this field exists to report.
    /// </para>
    /// </remarks>
    public required int SamplesWithoutPlanetaryHour { get; init; }

    /// <summary>Periods that were disqualified and why.</summary>
    public required IReadOnlyList<DisqualifiedPeriod> DisqualifiedPeriods { get; init; }

    /// <summary>Moon void-of-course windows across the date range.</summary>
    public required IReadOnlyList<VoidOfCoursePeriod> VoidOfCoursePeriods { get; init; }

    /// <summary>Timestamp when this calculation was performed.</summary>
    public required DateTime CalculatedAt { get; init; }
}
