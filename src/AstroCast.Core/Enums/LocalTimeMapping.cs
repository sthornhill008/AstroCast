namespace AstroCast.Core.Enums;

/// <summary>
/// How a wall-clock reading in a time zone maps to instants: once, twice, or not at all.
/// </summary>
/// <remarks>
/// The app resolves the two ambiguous cases by one rule (D17): a repeated reading denotes
/// the later of its two instants, and a skipped reading denotes the first instant after
/// the gap.
/// </remarks>
public enum LocalTimeMapping
{
    /// <summary>The reading occurs exactly once.</summary>
    Unique,

    /// <summary>
    /// The reading occurs twice, because the clocks went back over it (the end of
    /// daylight saving time, for instance). The later instant is taken.
    /// </summary>
    Repeated,

    /// <summary>
    /// The reading never occurs, because the clocks went forward over it (the start of
    /// daylight saving time, for instance). The first instant after the gap is taken.
    /// </summary>
    Skipped,
}
