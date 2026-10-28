namespace AstroCast.Core.Models;

/// <summary>
/// A complete Return &amp; Ingress Timeline for a natal chart within a date range.
/// Contains all planetary returns and sign ingresses merged into a single
/// chronological sequence — a master timing calendar.
/// </summary>
/// <remarks>
/// <para>
/// Returns require a natal chart to define the target longitudes.
/// Ingresses are chart-independent — they record the moment a planet
/// crosses 0° of a zodiac sign.
/// </para>
/// <para>
/// For a 5-year window, expect ~30 return events (mostly Mercury and Venus)
/// and ~10–15 outer planet ingress events. All events are sorted by
/// <see cref="ReturnIngressTimelineEntry.ExactUtc"/>.
/// </para>
/// </remarks>
public sealed class ReturnIngressTimeline
{
    /// <summary>The native's name.</summary>
    public required string NativeName { get; init; }

    /// <summary>All events, sorted chronologically.</summary>
    public required IReadOnlyList<ReturnIngressTimelineEntry> Entries { get; init; }

    /// <summary>Return entries only.</summary>
    public IReadOnlyList<ReturnIngressTimelineEntry> Returns =>
        Entries.Where(e => e.EventType == "Return").ToList();

    /// <summary>Ingress entries only.</summary>
    public IReadOnlyList<ReturnIngressTimelineEntry> Ingresses =>
        Entries.Where(e => e.EventType == "Ingress").ToList();

    /// <summary>Scan start.</summary>
    public required DateTime ScanStart { get; init; }

    /// <summary>Scan end.</summary>
    public required DateTime ScanEnd { get; init; }

    /// <summary>Total entry count.</summary>
    public required int Count { get; init; }

    /// <summary>
    /// Which planets were included for returns; empty when the return search did not run
    /// (the export then prints "not searched", E2-3).
    /// </summary>
    public required IReadOnlyList<string> ReturnPlanets { get; init; }

    /// <summary>
    /// Which planets were included for ingresses; empty when the ingress search did not run
    /// (the export then prints "not searched", E2-3).
    /// </summary>
    public required IReadOnlyList<string> IngressPlanets { get; init; }

    /// <summary>Engine version.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>When calculated.</summary>
    public required DateTime CalculatedAt { get; init; }
}
