namespace AstroCast.Core.Models;

/// <summary>
/// The Davison chart of two charts and the midpoints it was cast at (branch Davison, 1.7.0): a real chart, cast
/// by the chart engine for the moment halfway between the two birth instants, the mean of their Julian Days in
/// UT, at the place halfway between the two birthplaces, the mean latitude and the mean longitude by the shorter
/// arc round the globe, in Chart A's house system. Unlike a composite, whose bodies are midpoints of positions,
/// every position, angle and house here is the sky's own at that moment and place.
/// </summary>
public sealed class DavisonResult
{
    /// <summary>Source chart A, the first chart of the pair, whose house system the Davison chart takes.</summary>
    public required ChartResult ChartA { get; init; }

    /// <summary>Source chart B, the second chart of the pair.</summary>
    public required ChartResult ChartB { get; init; }

    /// <summary>The Davison chart, a real cast at the midpoint moment and place, of type "Davison".</summary>
    public required ChartResult DavisonChart { get; init; }

    /// <summary>The midpoint moment, the mean of the two birth instants, as a UTC instant (kind Utc).</summary>
    public required DateTime MidpointUtc { get; init; }

    /// <summary>The midpoint moment as a Julian Day (UT), the mean of the two charts' Julian Days.</summary>
    public required double MidpointJulianDay { get; init; }

    /// <summary>The midpoint place's latitude, the mean of the two birth latitudes, in degrees.</summary>
    public required double MidpointLatitude { get; init; }

    /// <summary>
    /// The midpoint place's longitude, in degrees in (−180, 180]: the mean of the two birth longitudes by the
    /// shorter arc round the globe, an exact 180° difference taking the eastward half from chart A.
    /// </summary>
    public required double MidpointLongitude { get; init; }

    /// <summary>The midpoint place's description, "Midpoint of {A} &amp; {B}" from the two birthplaces' names.</summary>
    public required string MidpointLocationDescription { get; init; }

    /// <summary>The engine version, the Davison chart's own.</summary>
    public required string EngineVersion { get; init; }

    /// <summary>When this result was calculated, UTC.</summary>
    public required DateTime CalculatedAt { get; init; }
}
