namespace AstroCast.Core.Models;

/// <summary>
/// Complete firdaria timeline for a natal chart, showing the current
/// major and sub-period, and the full period sequence for context.
/// </summary>
/// <remarks>
/// Firdaria (from the Arabic <em>firdar</em>) divides life into major
/// planetary periods in a fixed sequence that differs for day and night
/// charts. A planet's major period is divided into seven sub-periods in
/// Chaldean order from its lord; a node's period is not divided (T-D8,
/// branch H, 1.3.4). The full cycle spans 75 years, then repeats.
/// </remarks>
public sealed class FirdariaResult
{
    /// <summary>Whether this is a day or night chart.</summary>
    public required bool IsDayChart { get; init; }

    /// <summary>The native's birth date.</summary>
    public required DateTime BirthDate { get; init; }

    /// <summary>The reference date (typically today).</summary>
    public required DateTime ReferenceDate { get; init; }

    /// <summary>The native's age at the reference date.</summary>
    public required int Age { get; init; }

    /// <summary>The current major period ruler.</summary>
    public required string CurrentMajorRuler { get; init; }

    /// <summary>Start date of the current major period.</summary>
    public required DateTime CurrentMajorStart { get; init; }

    /// <summary>End date of the current major period.</summary>
    public required DateTime CurrentMajorEnd { get; init; }

    /// <summary>Duration of the current major period in years.</summary>
    public required int CurrentMajorYears { get; init; }

    /// <summary>The current sub-period ruler; empty in a node's period, which is not divided.</summary>
    public required string CurrentSubRuler { get; init; }

    /// <summary>Start date of the current sub-period; in a node's period, the major period's start.</summary>
    public required DateTime CurrentSubStart { get; init; }

    /// <summary>End date of the current sub-period; in a node's period, the major period's end.</summary>
    public required DateTime CurrentSubEnd { get; init; }

    /// <summary>The complete major period timeline.</summary>
    public required IReadOnlyList<FirdariaPeriod> Periods { get; init; }

    /// <summary>Narrative summary of the current period.</summary>
    public required string Summary { get; init; }
}

/// <summary>
/// A single major period in the firdaria sequence.
/// </summary>
public sealed class FirdariaPeriod
{
    /// <summary>The planet ruling this major period.</summary>
    public required string Ruler { get; init; }

    /// <summary>Duration in years.</summary>
    public required int Years { get; init; }

    /// <summary>Start date of this period.</summary>
    public required DateTime StartDate { get; init; }

    /// <summary>End date of this period.</summary>
    public required DateTime EndDate { get; init; }

    /// <summary>True if this is the current major period.</summary>
    public required bool IsCurrent { get; init; }

    /// <summary>Sub-periods within this major period: seven for a planet's, none for a node's.</summary>
    public required IReadOnlyList<FirdariaSubPeriod> SubPeriods { get; init; }
}

/// <summary>
/// A sub-period within a firdaria major period.
/// </summary>
public sealed class FirdariaSubPeriod
{
    /// <summary>The planet ruling this sub-period.</summary>
    public required string Ruler { get; init; }

    /// <summary>Start date.</summary>
    public required DateTime StartDate { get; init; }

    /// <summary>End date.</summary>
    public required DateTime EndDate { get; init; }

    /// <summary>True if this is the current sub-period.</summary>
    public required bool IsCurrent { get; init; }
}
