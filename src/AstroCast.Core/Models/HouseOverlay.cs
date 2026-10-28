namespace AstroCast.Core.Models;

/// <summary>
/// Placement of one person's planet in the other person's house system.
/// Used in synastry to show where each person's planets fall in the
/// partner's chart.
/// </summary>
public sealed class HouseOverlay
{
    /// <summary>The body being placed (e.g., "A Sun").</summary>
    public required string BodyName { get; init; }

    /// <summary>Formatted position of the body (e.g., "09°45′12″ Libra").</summary>
    public required string FormattedPosition { get; init; }

    /// <summary>Ecliptic longitude of the body.</summary>
    public required double Longitude { get; init; }

    /// <summary>House number (1–12) in the other person's house system.</summary>
    public required int House { get; init; }
}
