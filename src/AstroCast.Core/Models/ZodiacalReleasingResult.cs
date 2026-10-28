namespace AstroCast.Core.Models;

/// <summary>
/// Zodiacal releasing timeline from the Lot of Spirit, showing Level 1
/// (major) and Level 2 (sub) periods with peak/angular period markers.
/// </summary>
/// <remarks>
/// <para>
/// Zodiacal releasing (Vettius Valens, 2nd century) divides life into periods
/// based on the Lot of Spirit. Each zodiac sign rules a period whose length
/// is determined by the "minor years" table, the year 360 days and the month
/// 30. Level 1 periods advance through the zodiac from the Spirit sign, each
/// for its years; Level 2 sub-periods run within the current Level 1 period,
/// each sign for its years as 30-day months in zodiacal order from the Level 1
/// sign, passing after one round of 211 months to the sign opposite the Level 1
/// sign (the loosing of the bond), the last clamped to the Level 1 end.
/// </para>
/// <para>
/// Signs angular to the Lot of Fortune (conjunction, square, opposition) mark
/// "peak" or "angular" periods — the most productive phases. Signs cadent to
/// Fortune are "quiet" periods.
/// </para>
/// </remarks>
public sealed class ZodiacalReleasingResult
{
    /// <summary>The Lot of Spirit's zodiac sign (starting point for releasing).</summary>
    public required string LotOfSpiritSign { get; init; }

    /// <summary>The Lot of Spirit's longitude.</summary>
    public required double LotOfSpiritLongitude { get; init; }

    /// <summary>The Lot of Fortune's zodiac sign (determines peak periods).</summary>
    public required string LotOfFortuneSign { get; init; }

    /// <summary>Birth date.</summary>
    public required DateTime BirthDate { get; init; }

    /// <summary>Reference date.</summary>
    public required DateTime ReferenceDate { get; init; }

    /// <summary>Current Level 1 period sign.</summary>
    public required string CurrentL1Sign { get; init; }

    /// <summary>Current Level 1 traditional ruler.</summary>
    public required string CurrentL1Ruler { get; init; }

    /// <summary>Start date of current Level 1 period.</summary>
    public required DateTime CurrentL1Start { get; init; }

    /// <summary>End date of current Level 1 period.</summary>
    public required DateTime CurrentL1End { get; init; }

    /// <summary>Duration of current Level 1 period in years.</summary>
    public required int CurrentL1Years { get; init; }

    /// <summary>Whether the current Level 1 period is a "peak" (angular to Fortune).</summary>
    public required bool CurrentL1IsPeak { get; init; }

    /// <summary>Angularity description: "Angular", "Succedent", "Cadent" relative to Fortune.</summary>
    public required string CurrentL1Angularity { get; init; }

    /// <summary>Current Level 2 sub-period sign.</summary>
    public required string CurrentL2Sign { get; init; }

    /// <summary>Current Level 2 traditional ruler.</summary>
    public required string CurrentL2Ruler { get; init; }

    /// <summary>Start date of current Level 2 sub-period.</summary>
    public required DateTime CurrentL2Start { get; init; }

    /// <summary>End date of current Level 2 sub-period.</summary>
    public required DateTime CurrentL2End { get; init; }

    /// <summary>Whether the current Level 2 period is a peak.</summary>
    public required bool CurrentL2IsPeak { get; init; }

    /// <summary>Level 2 angularity relative to Fortune.</summary>
    public required string CurrentL2Angularity { get; init; }

    /// <summary>The complete Level 1 period timeline.</summary>
    public required IReadOnlyList<ZRPeriod> Level1Periods { get; init; }

    /// <summary>Level 2 sub-periods within the current Level 1 period.</summary>
    public required IReadOnlyList<ZRPeriod> CurrentL2Periods { get; init; }

    /// <summary>Narrative summary.</summary>
    public required string Summary { get; init; }
}

/// <summary>
/// A single period in a zodiacal releasing timeline.
/// </summary>
public sealed class ZRPeriod
{
    /// <summary>The zodiac sign ruling this period.</summary>
    public required string Sign { get; init; }

    /// <summary>Traditional ruler of the sign.</summary>
    public required string Ruler { get; init; }

    /// <summary>Duration in years (Level 1) or fractional years (Level 2).</summary>
    public required double Years { get; init; }

    /// <summary>Start date.</summary>
    public required DateTime StartDate { get; init; }

    /// <summary>End date.</summary>
    public required DateTime EndDate { get; init; }

    /// <summary>True if this is the current period.</summary>
    public required bool IsCurrent { get; init; }

    /// <summary>True if this period is angular to the Lot of Fortune (peak period).</summary>
    public required bool IsPeak { get; init; }

    /// <summary>"Angular", "Succedent", or "Cadent" relative to the Lot of Fortune.</summary>
    public required string Angularity { get; init; }
}
