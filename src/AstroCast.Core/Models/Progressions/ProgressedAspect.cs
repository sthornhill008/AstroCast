namespace AstroCast.Core.Models.Progressions;

/// <summary>
/// An aspect between a progressed body and a natal body/point.
/// </summary>
public sealed class ProgressedAspect
{
    /// <summary>The progressed body (e.g., "P Sun", "P Moon").</summary>
    public required string ProgressedBody { get; init; }

    /// <summary>Aspect name (e.g., "Conjunction", "Square").</summary>
    public required string AspectName { get; init; }

    /// <summary>Aspect abbreviation (e.g., "cnj", "sqr").</summary>
    public required string AspectAbbreviation { get; init; }

    /// <summary>The natal target name (e.g., "Sun", "ASC").</summary>
    public required string NatalTarget { get; init; }

    /// <summary>Orb at report midpoint in degrees.</summary>
    public required double OrbDegrees { get; init; }

    /// <summary>Whether the aspect is applying or separating at report midpoint.</summary>
    public required string Direction { get; init; }

    /// <summary>Entry date into a 1° orb. Never set: <c>ProgressionService</c> leaves it null, and no export reads it.</summary>
    public DateOnly? EntryDate { get; init; }

    /// <summary>Exact date. Never set: <c>ProgressionService</c> leaves it null, and no export reads it.</summary>
    public DateOnly? ExactDate { get; init; }

    /// <summary>Exit date from a 1° orb. Never set: <c>ProgressionService</c> leaves it null, and no export reads it.</summary>
    public DateOnly? ExitDate { get; init; }
}
