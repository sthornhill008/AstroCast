using AstroCast.Core.Enums;

namespace AstroCast.Core.Models.Transit;

/// <summary>
/// A planetary station (retrograde or direct) occurring during the report period.
/// </summary>
public sealed class TransitStation
{
    /// <summary>The planet that stations.</summary>
    public required TransitBody Planet { get; init; }

    /// <summary>Type of station: "Retrograde" or "Direct".</summary>
    public required string StationType { get; init; }

    /// <summary>Date of the station.</summary>
    public required DateOnly Date { get; init; }

    /// <summary>Zodiac sign at station.</summary>
    public required string Sign { get; init; }

    /// <summary>Degree within sign (0–29).</summary>
    public required int Degrees { get; init; }

    /// <summary>Arc-minutes within degree (0–59).</summary>
    public required int Minutes { get; init; }

    /// <summary>
    /// Precise UTC date/time of the station, refined by binary search on daily motion.
    /// Null for stations detected before this enhancement was added.
    /// When present, provides sub-minute precision (~0.08 seconds).
    /// </summary>
    public DateTime? DateTimeUtc { get; init; }
}
