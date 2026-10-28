namespace AstroCast.Core.Models.Transit;

/// <summary>
/// A natal chart target for transit aspect calculations.
/// Includes planets, points, and angles from the natal chart.
/// </summary>
public sealed class NatalTarget
{
    /// <summary>Display name (e.g., "Sun", "Moon", "Ascendant").</summary>
    public required string Name { get; init; }

    /// <summary>Symbol abbreviation (e.g., "SU", "MO", "ASC").</summary>
    public required string Symbol { get; init; }

    /// <summary>Ecliptic longitude in degrees (0–360).</summary>
    public required double Longitude { get; init; }

    /// <summary>Natal house number (1–12).</summary>
    public required int HouseNumber { get; init; }
}
