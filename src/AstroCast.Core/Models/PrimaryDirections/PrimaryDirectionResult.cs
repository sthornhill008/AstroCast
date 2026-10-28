namespace AstroCast.Core.Models.PrimaryDirections;

/// <summary>
/// A chart's primary directions to an age (branch Primary, 1.5.0): the hits in time order, the settings they were
/// found with, and the count of directions left out for want of a semi-arc.
/// </summary>
public sealed class PrimaryDirectionResult
{
    /// <summary>The natal chart directed.</summary>
    public required ChartResult NatalChart { get; init; }

    /// <summary>The settings the directions were found with.</summary>
    public required PrimaryDirectionSettings Settings { get; init; }

    /// <summary>
    /// Every direction exact between birth and the age limit, sorted by instant, ties by significator (the
    /// Ascendant, Midheaven, Descendant, IC, Sun, Moon, Mercury to Pluto, then the Part of Fortune), promissor, aspect,
    /// aspect angle and motion.
    /// </summary>
    public required IReadOnlyList<PrimaryDirectionHit> Hits { get; init; }

    /// <summary>
    /// The directions left out because a semi-arc they use does not exist, the point never rising or setting at the
    /// place: one for each significator, promissor point and motion so affected, whatever age the direction would
    /// have fallen at (rule R1).
    /// </summary>
    public required int LeftOutCount { get; init; }
}
