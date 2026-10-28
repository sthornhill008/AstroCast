namespace AstroCast.Core.Models.Transit;

/// <summary>
/// Position snapshot of a transiting planet at a specific date.
/// </summary>
public sealed class TransitPlanetPosition
{
    /// <summary>Ecliptic longitude in degrees (0–360).</summary>
    public required double Longitude { get; init; }

    /// <summary>Zodiac sign name.</summary>
    public required string Sign { get; init; }

    /// <summary>Degree within sign (0–29).</summary>
    public required int Degrees { get; init; }

    /// <summary>Arc-minutes within degree (0–59).</summary>
    public required int Minutes { get; init; }

    /// <summary>True if the planet is retrograde.</summary>
    public required bool IsRetrograde { get; init; }

    /// <summary>Daily motion in degrees (negative = retrograde).</summary>
    public required double DailyMotion { get; init; }
}
