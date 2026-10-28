namespace AstroCast.Core.Models.Electional;

/// <summary>
/// A period within the date range that was disqualified from candidacy.
/// </summary>
public sealed class DisqualifiedPeriod
{
    /// <summary>Start of the disqualified period (local).</summary>
    public required DateTime Start { get; init; }

    /// <summary>End of the disqualified period (local).</summary>
    public required DateTime End { get; init; }

    /// <summary>Human-readable reason for disqualification.</summary>
    public required string Reason { get; init; }

    /// <summary>
    /// Start of the disqualified period as an instant, <see cref="DateTimeKind.Utc"/>, to the second: the
    /// instant <see cref="Start"/> reads. A sample is disqualified when its instant lies in
    /// [<see cref="StartUtc"/>, <see cref="EndUtc"/>); a local reading the clocks repeat names two instants.
    /// </summary>
    public required DateTime StartUtc { get; init; }

    /// <summary>
    /// End of the disqualified period as an instant, <see cref="DateTimeKind.Utc"/>, to the second: the
    /// instant <see cref="End"/> reads.
    /// </summary>
    public required DateTime EndUtc { get; init; }
}
