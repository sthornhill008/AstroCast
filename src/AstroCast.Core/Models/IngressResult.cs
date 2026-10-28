using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// The result of an ingress calculation — the exact moment a celestial body
/// enters a zodiac sign, with optional full chart data.
/// </summary>
public sealed class IngressResult
{
    /// <summary>The planet whose ingress this is.</summary>
    public required CelestialBodyId Planet { get; init; }

    /// <summary>The planet's display name.</summary>
    public required string PlanetName { get; init; }

    /// <summary>
    /// The sign being entered: the one that begins at <see cref="TargetLongitude"/> when the body
    /// crosses it moving forward, the one that ends there when it crosses retrograde.
    /// </summary>
    public required string Sign { get; init; }

    /// <summary>The sign's element (Fire, Earth, Air, Water).</summary>
    public required string Element { get; init; }

    /// <summary>The sign's modality (Cardinal, Fixed, Mutable).</summary>
    public required string Modality { get; init; }

    /// <summary>The target longitude (0° of the sign).</summary>
    public required double TargetLongitude { get; init; }

    /// <summary>The planet's actual longitude at the found moment (should match target to sub-arc-second).</summary>
    public required double ActualLongitude { get; init; }

    /// <summary>Precision in arc-seconds.</summary>
    public required double PrecisionArcSeconds { get; init; }

    /// <summary>The exact UTC date/time of the ingress.</summary>
    public required DateTime ExactUtc { get; init; }

    /// <summary>
    /// Whether this is a cardinal ingress (0° Aries, Cancer, Libra, Capricorn).
    /// Only applicable when the planet is the Sun.
    /// </summary>
    public required bool IsCardinalIngress { get; init; }

    /// <summary>
    /// Seasonal name for cardinal Sun ingresses (e.g., "Vernal Equinox", "Summer Solstice").
    /// Null for non-solar or non-cardinal ingresses.
    /// </summary>
    public string? SeasonName { get; init; }

    /// <summary>
    /// If a full chart was cast at this ingress moment, the chart result.
    /// Null if only the timing was requested.
    /// </summary>
    public ChartResult? Chart { get; init; }

    /// <summary>Engine version.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>When calculated.</summary>
    public required DateTime CalculatedAt { get; init; }
}

/// <summary>
/// A series of ingresses found within a date range.
/// </summary>
public sealed class IngressSeries
{
    /// <summary>The planet tracked.</summary>
    public required CelestialBodyId Planet { get; init; }

    /// <summary>Planet display name.</summary>
    public required string PlanetName { get; init; }

    /// <summary>All ingresses found, chronologically.</summary>
    public required IReadOnlyList<IngressResult> Ingresses { get; init; }

    /// <summary>Scan start.</summary>
    public required DateTime ScanStart { get; init; }

    /// <summary>Scan end.</summary>
    public required DateTime ScanEnd { get; init; }

    /// <summary>Count.</summary>
    public required int Count { get; init; }

    /// <summary>Engine version.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>When calculated.</summary>
    public required DateTime CalculatedAt { get; init; }
}
