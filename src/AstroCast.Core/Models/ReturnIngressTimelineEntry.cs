using AstroCast.Core.Enums;

namespace AstroCast.Core.Models;

/// <summary>
/// A single entry in the Return &amp; Ingress Timeline — either a planetary return
/// or a sign ingress, with precise UTC timing.
/// </summary>
/// <remarks>
/// <para>
/// Each entry records the exact UTC moment of the event, the planet involved,
/// and the ecliptic position at that moment. For returns, this is the natal
/// planet's longitude; for ingresses, it is 0° of the entered sign.
/// </para>
/// <para>
/// A return is numbered by its passage over the natal longitude, counted
/// from birth (T-D6, branch F, 1.3.4), and labelled "Return #N", or
/// "Return #N (first of 3 passes)" when a retrograde loop lies over the
/// degree. Until 1.3.4 the number was elapsed time over the orbital period,
/// printed "~#332".
/// </para>
/// </remarks>
public sealed class ReturnIngressTimelineEntry
{
    /// <summary>"Return" or "Ingress".</summary>
    public required string EventType { get; init; }

    /// <summary>The planet involved.</summary>
    public required CelestialBodyId Planet { get; init; }

    /// <summary>Planet display name.</summary>
    public required string PlanetName { get; init; }

    /// <summary>The exact UTC date/time of the event.</summary>
    public required DateTime ExactUtc { get; init; }

    /// <summary>
    /// For returns: the natal planet's longitude.
    /// For ingresses: the sign entry longitude (0° of sign).
    /// </summary>
    public required double Longitude { get; init; }

    /// <summary>Formatted position (e.g., "08°50′50″ Cancer" or "00°00′00″ Gemini").</summary>
    public required string PositionFormatted { get; init; }

    /// <summary>
    /// The zodiac sign. For an ingress, the sign entered: the one that begins at
    /// <see cref="Longitude"/> when the body crosses it moving forward, the one that ends there
    /// when it crosses retrograde.
    /// </summary>
    public required string Sign { get; init; }

    /// <summary>
    /// For returns: the return label (e.g., "Return #26" or "Return #4 (first of 3 passes)").
    /// For ingresses: the ingress label (e.g., "Sign Entry", "Vernal Equinox").
    /// </summary>
    public required string Label { get; init; }

    /// <summary>Precision in arc-seconds.</summary>
    public required double PrecisionArcSeconds { get; init; }
}
