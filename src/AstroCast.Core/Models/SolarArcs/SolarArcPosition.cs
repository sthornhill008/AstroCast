namespace AstroCast.Core.Models.SolarArcs;

/// <summary>
/// A natal body/angle position directed by the solar arc value.
/// </summary>
public sealed class SolarArcPosition
{
    /// <summary>Body or angle name (e.g., "Sun", "Moon", "ASC", "MC").</summary>
    public required string Name { get; init; }

    /// <summary>Natal ecliptic longitude in degrees.</summary>
    public required double NatalLongitude { get; init; }

    /// <summary>Natal formatted position (e.g., "02°27' Taurus").</summary>
    public required string NatalFormattedPosition { get; init; }

    /// <summary>Directed ecliptic longitude in degrees (natal + solar arc, mod 360).</summary>
    public required double DirectedLongitude { get; init; }

    /// <summary>Directed sign name.</summary>
    public required string DirectedSign { get; init; }

    /// <summary>Directed degree within sign (0–29).</summary>
    public required int DirectedDegrees { get; init; }

    /// <summary>Directed arc-minutes within degree (0–59).</summary>
    public required int DirectedMinutes { get; init; }

    /// <summary>Directed formatted position (e.g., "26°19' Gemini").</summary>
    public required string DirectedFormattedPosition { get; init; }
}
