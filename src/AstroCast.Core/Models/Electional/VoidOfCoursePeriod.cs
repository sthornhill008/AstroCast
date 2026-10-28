namespace AstroCast.Core.Models.Electional;

/// <summary>
/// A period during which the Moon is void-of-course (has made its last
/// major aspect in the current sign and will not make another before
/// changing signs).
/// </summary>
public sealed class VoidOfCoursePeriod
{
    /// <summary>Start of the void-of-course period (local time).</summary>
    public required DateTime StartLocal { get; init; }

    /// <summary>End of the void-of-course period — Moon enters next sign (local).</summary>
    public required DateTime EndLocal { get; init; }

    /// <summary>The Moon's sign during this void-of-course period.</summary>
    public required string MoonSign { get; init; }

    /// <summary>
    /// Start of the void-of-course period as an instant, <see cref="DateTimeKind.Utc"/>, to the second:
    /// the instant <see cref="StartLocal"/> reads. Compare instants with it; a local reading the clocks
    /// repeat names two instants.
    /// </summary>
    public required DateTime StartUtc { get; init; }

    /// <summary>
    /// End of the void-of-course period as an instant, <see cref="DateTimeKind.Utc"/>, to the second:
    /// the instant <see cref="EndLocal"/> reads.
    /// </summary>
    public required DateTime EndUtc { get; init; }
}
