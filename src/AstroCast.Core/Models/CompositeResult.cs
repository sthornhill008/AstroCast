namespace AstroCast.Core.Models;

/// <summary>
/// Complete composite chart result containing the synthesised chart data
/// and references to the two source charts.
/// </summary>
/// <remarks>
/// A composite chart creates a single chart from two source charts by
/// calculating the short-arc midpoint of each corresponding planet pair, Mercury
/// and Venus taking the midpoint nearer the composite Sun (S-D2). Its MC is the
/// midpoint of the two Midheavens, and its Ascendant and cusps are cast for it at
/// the mean of the two birth latitudes in the house system both charts share, else
/// Placidus (S-D3, Q1; branch L3, 1.3.4).
/// </remarks>
public sealed class CompositeResult
{
    /// <summary>Source chart A (the first chart in the pair).</summary>
    public required ChartResult ChartA { get; init; }

    /// <summary>Source chart B (the second chart in the pair).</summary>
    public required ChartResult ChartB { get; init; }

    /// <summary>The composite chart data (synthetic positions, derived houses, computed aspects/midpoints).</summary>
    public required ChartResult CompositeChart { get; init; }

    /// <summary>
    /// The latitude the composite houses are cast at: always the mean of the two birth latitudes
    /// (S-D3, branch L3, 1.3.4). Until 1.3.4 a caller could set it, and the app set it to Person A's.
    /// </summary>
    public required double ReferenceLatitude { get; init; }

    /// <summary>
    /// The mean of the two birth longitudes, a label only: the composite houses are cast for the
    /// midpoint of the two Midheavens, not for a place.
    /// </summary>
    public required double ReferenceLongitude { get; init; }

    /// <summary>The reference location's description: always "Midpoint of {A} &amp; {B}".</summary>
    public required string ReferenceLocationDescription { get; init; }

    /// <summary>
    /// The names, in position order, of the bodies (Mercury, Venus) that took the midpoint nearer the
    /// composite Sun instead of their short-arc midpoint (ruling S-D2, branch L3, 1.3.4); empty when none did.
    /// The export notes each in its header.
    /// </summary>
    public IReadOnlyList<string> SunSideBodies { get; init; } = [];

    /// <summary>Engine version info.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>Timestamp when this composite was calculated.</summary>
    public required DateTime CalculatedAt { get; init; }
}
