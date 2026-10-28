using System.Globalization;
using System.Reflection;

using AstroCast.Core.Services;

namespace AstroCast.Services;

/// <summary>
/// The astrocartography map's base-map settings, held once for both heads: the CARTO
/// Voyager raster tiles, the layer name, the User-Agent sent with each tile request, and
/// the attribution CARTO requires. Both <c>AcgMapWindow</c> files build their tile source,
/// tile layer and attribution only from these.
/// </summary>
/// <remarks>
/// <para>
/// Voyager labels places in Latin script, with English names where they exist, which is
/// why the map uses it. Since 25 September 2026 CARTO serves raster tiles only with a key;
/// a request without one, or with a wrong one, is answered with an "API KEY REQUIRED"
/// placeholder sent as an ordinary HTTP 200. The key comes from
/// <see cref="BaseMapKeyLoader"/>.
/// </para>
/// <para>
/// The settings live in Presentation rather than Core because only the two heads draw
/// maps. Nothing here depends on Mapsui or BruTile; each head turns these values into its
/// own tile source and widgets.
/// </para>
/// </remarks>
public static class BaseMapSettings
{
    /// <summary>
    /// The tile URL template on CARTO's documented host for keyed raster tiles, with
    /// BruTile's placeholders: <c>{z}</c>, <c>{x}</c> and <c>{y}</c> for the tile, and
    /// <c>{k}</c> for the key, which <see cref="UrlFormatter"/> fills in escaped.
    /// </summary>
    /// <remarks>
    /// As CARTO's basemaps and API-key pages give it in October 2026. The single host
    /// replaces the four <c>{s}</c> subdomains the app used before keys were required.
    /// </remarks>
    public const string TileUrlTemplate =
        "https://basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png?key={k}";

    /// <summary>The name of the map's tile layer.</summary>
    public const string LayerName = "CartoDB Voyager";

    /// <summary>The OpenStreetMap part of the attribution.</summary>
    public const string OpenStreetMapAttributionText = "© OpenStreetMap contributors";

    /// <summary>The page the OpenStreetMap part of the attribution links to.</summary>
    public const string OpenStreetMapAttributionUrl = "https://www.openstreetmap.org/copyright";

    /// <summary>The CARTO part of the attribution.</summary>
    public const string CartoAttributionText = "© CARTO";

    /// <summary>The page the CARTO part of the attribution links to.</summary>
    public const string CartoAttributionUrl = "https://carto.com/attribution/";

    /// <summary>
    /// The one line a map window shows under its legend when there is no key, in place of
    /// the base map.
    /// </summary>
    public const string NoKeyMessage = "Base map unavailable: no CARTO key";

    /// <summary>The largest zoom level <see cref="TileUrl"/> accepts.</summary>
    private const int MaxZoomLevel = 30;

    /// <summary>
    /// The User-Agent sent with every tile request: <c>AstroCast/</c>, the engine's version
    /// as <see cref="ChartEngine"/> reads it (1.3.3 for this release), and
    /// <c>(desktop app)</c>. Both CARTO and the OpenStreetMap Foundation ask an application
    /// to identify itself with a stable User-Agent of its own.
    /// </summary>
    public static string UserAgent { get; } = $"AstroCast/{EngineVersion()} (desktop app)";

    /// <summary>
    /// Builds the URL formatter a tile source fills in for each tile:
    /// <see cref="TileUrlTemplate"/> with the key in place of <c>{k}</c>, URL-escaped, and
    /// <c>{z}</c>, <c>{x}</c> and <c>{y}</c> left for the tile.
    /// </summary>
    /// <param name="key">The key the tiles are requested with.</param>
    /// <returns>The URL formatter, carrying the key. Never log or display it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    public static string UrlFormatter(BaseMapKey key)
    {
        ArgumentNullException.ThrowIfNull(key);

        return TileUrlTemplate.Replace("{k}", Uri.EscapeDataString(key.Value), StringComparison.Ordinal);
    }

    /// <summary>
    /// Builds the URL of one tile, as the map's tile source requests it.
    /// </summary>
    /// <param name="zoom">The zoom level, from 0.</param>
    /// <param name="x">The tile's column, from 0 to 2^<paramref name="zoom"/> − 1.</param>
    /// <param name="y">The tile's row, from 0 to 2^<paramref name="zoom"/> − 1.</param>
    /// <param name="key">The key the tile is requested with.</param>
    /// <returns>The tile's URL, carrying the key. Never log or display it.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="key"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="zoom"/>, <paramref name="x"/> or <paramref name="y"/> is outside the
    /// tile pyramid. The message names the argument and its value, never the key.
    /// </exception>
    public static string TileUrl(int zoom, int x, int y, BaseMapKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentOutOfRangeException.ThrowIfNegative(zoom);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(zoom, MaxZoomLevel);
        int tilesPerSide = 1 << zoom;
        ArgumentOutOfRangeException.ThrowIfNegative(x);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(x, tilesPerSide);
        ArgumentOutOfRangeException.ThrowIfNegative(y);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(y, tilesPerSide);

        return UrlFormatter(key)
            .Replace("{z}", zoom.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{x}", x.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal)
            .Replace("{y}", y.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    /// <summary>
    /// The engine's version, read as <see cref="ChartEngine"/> reads it for its engine
    /// version: Core's informational version without the commit-hash suffix.
    /// </summary>
    private static string EngineVersion()
    {
        string version = typeof(ChartEngine).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        // Trim the git commit hash suffix (e.g., "1.0.0+abc123" -> "1.0.0")
        int plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version[..plusIndex];

        return version;
    }
}
