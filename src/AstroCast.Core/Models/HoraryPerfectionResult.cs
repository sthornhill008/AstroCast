namespace AstroCast.Core.Models;

/// <summary>
/// The result of horary perfection analysis: whether the chart shows the
/// querent's matter coming to completion, the mechanism of perfection or
/// denial, reception quality, and the Moon's testimony.
/// </summary>
public sealed class HoraryPerfectionResult
{
    // ── Question Context ──

    /// <summary>The horary question text.</summary>
    public required string Question { get; init; }

    /// <summary>The question category (e.g., "Career", "Relationship").</summary>
    public required string QuestionCategory { get; init; }

    /// <summary>The house number of the quesited matter.</summary>
    public required int QuesitedHouseNumber { get; init; }

    // ── Significators ──

    /// <summary>Querent significator (L1) — traditional ruler of the Ascendant sign.</summary>
    public required string QuerentSignificator { get; init; }

    /// <summary>Quesited significator (Lx) — traditional ruler of the quesited house cusp sign.</summary>
    public required string QuesitedSignificator { get; init; }

    /// <summary>Sign occupied by L1.</summary>
    public required string QuerentSign { get; init; }

    /// <summary>Sign occupied by Lx.</summary>
    public required string QuesitedSign { get; init; }

    /// <summary>House occupied by L1.</summary>
    public required int QuerentHouse { get; init; }

    /// <summary>House occupied by Lx.</summary>
    public required int QuesitedHouse { get; init; }

    /// <summary>L1's essential dignity label and score.</summary>
    public required string QuerentDignity { get; init; }

    /// <summary>Lx's essential dignity label and score.</summary>
    public required string QuesitedDignity { get; init; }

    // ── Perfection ──

    /// <summary>True if the chart shows perfection (the matter will come to completion).</summary>
    public required bool IsPerfected { get; init; }

    /// <summary>The perfection mechanism: "Direct Aspect", "Translation of Light", "Collection of Light", "Mutual Reception", "Antiscia", or null if not perfected.</summary>
    public string? PerfectionMechanism { get; init; }

    /// <summary>The aspect type for direct perfection (e.g., "Trine", "Square"), or null.</summary>
    public string? PerfectionAspect { get; init; }

    /// <summary>The orb of the perfecting aspect, or null.</summary>
    public double? PerfectionOrb { get; init; }

    /// <summary>
    /// The translator for translation of light — one of the seven traditional
    /// planets, whose application completed — or null.
    /// </summary>
    public string? Translator { get; init; }

    /// <summary>
    /// The collector for collection of light — one of the seven traditional
    /// planets, both of whose received applications completed — or null.
    /// </summary>
    public string? Collector { get; init; }

    // ── The direct application, followed in time ──

    /// <summary>
    /// The significator pair whose applying major aspect was followed, as
    /// "L1–Lx" (e.g., "Venus–Mercury"), or null when no applying major aspect
    /// existed at the chart.
    /// </summary>
    public string? ApplyingPair { get; init; }

    /// <summary>The followed aspect's type (e.g., "Conjunction"), or null when none existed.</summary>
    public string? ApplyingAspect { get; init; }

    /// <summary>The followed aspect's orb at the chart, in degrees, or null when none existed.</summary>
    public double? ApplyingAspectOrb { get; init; }

    /// <summary>
    /// The body doing the applying — the significator with the larger daily
    /// motion at the chart — or null when no applying major aspect existed.
    /// </summary>
    public string? Applier { get; init; }

    /// <summary>
    /// UTC instant at which the followed aspect becomes exact, rounded to the
    /// nearest second. Null when no applying major aspect existed, when the
    /// application fails, or when it becomes exact only after a significator has
    /// left its sign with no reception between them — it then does not perfect,
    /// and the instant is named in <see cref="Notes"/> instead.
    /// </summary>
    public DateTime? PerfectionTimeUtc { get; init; }

    /// <summary>
    /// UTC instant, rounded to the nearest second, at which the followed
    /// application came closest and turned around without reaching exact: the
    /// refined instant of closest approach, not the hourly sample it was found
    /// near. Null unless the application fails.
    /// </summary>
    public DateTime? ApplicationFailsTimeUtc { get; init; }

    /// <summary>The closest orb the failed application reached, in degrees. Null unless the application fails.</summary>
    public double? ApplicationClosestOrb { get; init; }

    /// <summary>
    /// True when the followed application neither perfected nor turned around
    /// within the finder's cap; it is then neither perfection nor refranation.
    /// </summary>
    public bool ApplicationCapped { get; init; }

    /// <summary>
    /// Stations of L1 or Lx between the chart and the point where the followed
    /// application ends (its perfection, or its turnaround); for a failing
    /// application, also each significator's first station after the
    /// turnaround when it falls before the linear perfection estimate (the
    /// instant the chart's separation would close at the chart's relative
    /// speed), which makes the failure a refranation. Empty when there are none
    /// or when no application was followed.
    /// </summary>
    public IReadOnlyList<HorarySignificatorStation> SignificatorStations { get; init; } = [];

    /// <summary>
    /// Exact major aspects of Mars or Saturn to L1 or Lx that fall strictly
    /// between the chart and the perfection — the followed aspect's, or the
    /// completion of a translation or collection (its intermediary excepted) —
    /// one per malefic, the earliest. Empty when nothing perfects or no
    /// malefic intervenes.
    /// </summary>
    public IReadOnlyList<HoraryProhibitionEvent> ProhibitionEvents { get; init; } = [];

    /// <summary>
    /// Informational notes that are not denials: a significator stationing
    /// before an aspect that still perfects, an application that fails with
    /// no station before its turnaround or, after it, before its linear
    /// perfection estimate, one still applying at the cap, a direct aspect that becomes
    /// exact only after a significator leaves its sign (naming the sign
    /// changes), a translation or collection that does not complete (naming the
    /// application that failed), a retrograde translator or collector, or a
    /// separating antiscion or contra-antiscion, which is testimony rather than
    /// perfection. An unrecognised question category, for which the 7th house
    /// is assumed, is named first.
    /// </summary>
    public IReadOnlyList<string> Notes { get; init; } = [];

    // ── Denial ──

    /// <summary>True if the chart shows denial (the matter will not come to completion).</summary>
    public required bool IsDenied { get; init; }

    /// <summary>Reasons for denial (e.g., "Moon Void of Course", "L1 combust", "Prohibition by Saturn").</summary>
    public required IReadOnlyList<string> DenialReasons { get; init; }

    // ── Reception ──

    /// <summary>
    /// Reception quality: "Mutual", "Querent favoured", "Quesited favoured", "Mixed" (one direction
    /// favoured and the other hostile), "Hostile" (in either direction or both), "Neutral"; or, when
    /// mutual reception is the perfection mechanism, its type: "Mutual by domicile", "Mutual by
    /// exaltation" or "Mutual by domicile and exaltation".
    /// </summary>
    public required string ReceptionQuality { get; init; }

    /// <summary>
    /// Human-readable detail about the reception, worded from the side of each direction it
    /// names (e.g., "Mars is in a sign of Venus's detriment or fall (Aries) — the quesited is
    /// averse to the querent."); a mixed or two-way hostile line names both directions.
    /// </summary>
    public string? ReceptionDetail { get; init; }

    // ── Moon ──

    /// <summary>The Moon's first applying planet and aspect type, or null if VoC.</summary>
    public string? MoonFirstApplying { get; init; }

    /// <summary>True if the Moon is void of course.</summary>
    public required bool MoonVoC { get; init; }

    /// <summary>Summary of the Moon's testimony regarding the question.</summary>
    public required string MoonTestimony { get; init; }

    // ── Judgment ──

    /// <summary>Overall judgment: "Yes", "No", or "Uncertain".</summary>
    public required string Judgment { get; init; }

    /// <summary>Human-readable explanation of the judgment.</summary>
    public required string JudgmentNote { get; init; }
}

/// <summary>
/// A station of a horary significator inside the window of its followed
/// application.
/// </summary>
public sealed class HorarySignificatorStation
{
    /// <summary>The significator's name (e.g., "Mercury").</summary>
    public required string Significator { get; init; }

    /// <summary>"L1" or "Lx".</summary>
    public required string Label { get; init; }

    /// <summary>True for a station retrograde, false for a station direct.</summary>
    public required bool IsRetrogradeStation { get; init; }

    /// <summary>Julian Day (UT) of the station, to one second.</summary>
    public required double JulianDay { get; init; }

    /// <summary>UTC instant of the station, rounded to the nearest second.</summary>
    public required DateTime TimeUtc { get; init; }
}

/// <summary>
/// An exact major aspect of a malefic to a significator that falls before the
/// significators' own aspect perfects.
/// </summary>
public sealed class HoraryProhibitionEvent
{
    /// <summary>"Mars" or "Saturn".</summary>
    public required string Malefic { get; init; }

    /// <summary>The significator the malefic reaches (e.g., "Mercury").</summary>
    public required string Target { get; init; }

    /// <summary>"L1" or "Lx" — which significator <see cref="Target"/> is.</summary>
    public required string TargetLabel { get; init; }

    /// <summary>The aspect's type (e.g., "Square").</summary>
    public required string AspectType { get; init; }

    /// <summary>Julian Day (UT) of exactness, to one second.</summary>
    public required double JulianDay { get; init; }

    /// <summary>UTC instant of exactness, rounded to the nearest second.</summary>
    public required DateTime ExactTimeUtc { get; init; }
}
