namespace AstroCast.Core.Models.Progressions;

/// <summary>
/// Position of a progressed planet or angle at the report midpoint.
/// </summary>
public sealed class ProgressedPosition
{
    /// <summary>Body name (e.g., "Sun", "Moon", "Mercury", "ASC", "MC").</summary>
    public required string Name { get; init; }

    /// <summary>Natal ecliptic longitude in degrees.</summary>
    public required double NatalLongitude { get; init; }

    /// <summary>Natal formatted position (e.g., "02°27' Taurus").</summary>
    public required string NatalFormattedPosition { get; init; }

    /// <summary>Progressed ecliptic longitude in degrees.</summary>
    public required double ProgressedLongitude { get; init; }

    /// <summary>Progressed sign name.</summary>
    public required string ProgressedSign { get; init; }

    /// <summary>Progressed degree within sign (0–29).</summary>
    public required int ProgressedDegrees { get; init; }

    /// <summary>Progressed arc-minutes within degree (0–59).</summary>
    public required int ProgressedMinutes { get; init; }

    /// <summary>Progressed formatted position (e.g., "26°19' Taurus").</summary>
    public required string ProgressedFormattedPosition { get; init; }

    /// <summary>Natal house the progressed body occupies (1–12). Null for angles.</summary>
    public int? NatalHouse { get; init; }

    /// <summary>
    /// Total arc moved from natal position in degrees.
    /// For display: "23°52' from natal".
    /// </summary>
    public required double MovementFromNatal { get; init; }

    /// <summary>
    /// True if this body is retrograde at the progressed date.
    /// Relevant for Mercury, Venus, Mars. Null for angles.
    /// </summary>
    public bool? IsRetrograde { get; init; }

    /// <summary>
    /// Direction code: "D" for direct, "R" for retrograde. Null for angles.
    /// </summary>
    public string? DirectionCode { get; init; }
}
