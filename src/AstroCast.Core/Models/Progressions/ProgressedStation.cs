namespace AstroCast.Core.Models.Progressions;

/// <summary>
/// A progressed planet stationing retrograde or direct within 1 year
/// of the report period. Rare but significant.
/// </summary>
public sealed class ProgressedStation
{
    /// <summary>The progressed body name.</summary>
    public required string BodyName { get; init; }

    /// <summary>Station type: "Retrograde" or "Direct".</summary>
    public required string StationType { get; init; }

    /// <summary>
    /// The date of the station, exact: the UT date of its instant (branch G, 1.3.4). The export's
    /// column header keeps "Approximate Date".
    /// </summary>
    public required DateOnly Date { get; init; }
}
