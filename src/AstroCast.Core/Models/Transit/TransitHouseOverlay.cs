using AstroCast.Core.Enums;

namespace AstroCast.Core.Models.Transit;

/// <summary>
/// Snapshot of which natal house a transiting planet occupies at the
/// report midpoint date.
/// </summary>
public sealed class TransitHouseOverlay
{
    /// <summary>The transiting planet.</summary>
    public required TransitBody Planet { get; init; }

    /// <summary>Natal house number occupied (1–12).</summary>
    public required int HouseNumber { get; init; }

    /// <summary>
    /// True if the transiting planet falls within an intercepted sign
    /// in this natal house.
    /// </summary>
    public bool IsIntercepted { get; init; }
}
