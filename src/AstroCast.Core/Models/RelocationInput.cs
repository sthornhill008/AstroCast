namespace AstroCast.Core.Models;

/// <summary>
/// Input parameters for a relocation chart calculation. Specifies the
/// target location where the natal chart will be recalculated.
/// </summary>
/// <remarks>
/// The relocation chart preserves the exact UTC moment of the natal chart
/// but recalculates house cusps, Ascendant, and Midheaven for the new
/// geographic coordinates. Planetary positions, signs, and the aspects
/// between planets remain identical; the house placements, aspects to the
/// angles, angular planets, Lots and parans follow the place, while sect,
/// the dignities, the planetary hour and the printed clock stay the birth
/// chart's (branch K2, 1.3.4).
/// </remarks>
public sealed class RelocationInput
{
    /// <summary>
    /// Geographic latitude of the relocation target in decimal degrees
    /// (north positive).
    /// </summary>
    public required double Latitude { get; init; }

    /// <summary>
    /// Geographic longitude of the relocation target in decimal degrees
    /// (east positive).
    /// </summary>
    public required double Longitude { get; init; }

    /// <summary>
    /// Human-readable name of the relocation target
    /// (e.g., "London, UK", "Tokyo, Japan").
    /// </summary>
    public required string LocationName { get; init; }
}
