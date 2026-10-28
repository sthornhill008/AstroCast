using System.Text.RegularExpressions;

using AstroCast.Core.Models;
using AstroCast.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the base map's settings (rule 1 of the map-tiles branch): CARTO's documented URL for
/// keyed Voyager raster tiles with the key escaped into it, the unchanged layer name, the
/// User-Agent naming the engine's version, and the attribution CARTO requires, with both
/// links. Until this branch both map windows held their own copies, without a key, with the
/// User-Agent "AstroCast/1.0 (desktop; +local-only)" and one attribution link.
/// </summary>
/// <remarks>
/// The expected URL is CARTO's own, from its basemaps and API-key pages
/// (<c>https://basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png?key=YOUR_KEY</c>),
/// and the attribution texts and links are those of CARTO's attribution page. The keys here
/// are made up; no test reads a key file.
/// </remarks>
[Collection("Ephemeris")]
public sealed class BaseMapSettingsTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>
    /// Receives the shared ephemeris fixture, which the User-Agent test casts a chart with.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public BaseMapSettingsTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>The template is CARTO's documented host and path, with the key as the query.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void TileUrlTemplate_IsCartosDocumentedKeyedRasterUrl()
    {
        BaseMapSettings.TileUrlTemplate.Should().Be(
            "https://basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png?key={k}");
    }

    /// <summary>Tile 2/1/1 for the key <c>abc</c>.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void TileUrl_Tile211_WithKeyAbc()
    {
        BaseMapSettings.TileUrl(2, 1, 1, Key("abc")).Should().Be(
            "https://basemaps.cartocdn.com/rastertiles/voyager/2/1/1.png?key=abc");
    }

    /// <summary>
    /// A key with characters a query cannot carry as they are is escaped: space, slash, plus,
    /// ampersand, equals, question mark, hash, percent and a non-ASCII letter. The URL parses,
    /// and its key parameter unescapes to the key.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void TileUrl_Tile211_WithAKeyNeedingEscaping_EscapesIt()
    {
        const string key = "a b/c+d&e=f?g#h%ü";

        string url = BaseMapSettings.TileUrl(2, 1, 1, Key(key));

        url.Should().Be("https://basemaps.cartocdn.com/rastertiles/voyager/2/1/1.png?key=a%20b%2Fc%2Bd%26e%3Df%3Fg%23h%25%C3%BC");
        Uri uri = new(url);
        uri.Host.Should().Be("basemaps.cartocdn.com");
        uri.AbsolutePath.Should().Be("/rastertiles/voyager/2/1/1.png");
        uri.Query.Should().StartWith("?key=");
        Uri.UnescapeDataString(uri.Query["?key=".Length..]).Should().Be(key);
    }

    /// <summary>
    /// The formatter a tile source fills in keeps the tile placeholders and carries the key,
    /// escaped.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void UrlFormatter_KeepsTheTilePlaceholders_AndEscapesTheKey()
    {
        BaseMapSettings.UrlFormatter(Key("a b")).Should().Be(
            "https://basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}.png?key=a%20b");
    }

    /// <summary>
    /// The User-Agent is <c>AstroCast/</c>, the version the engine prints in a chart's engine
    /// version, and <c>(desktop app)</c>; no longer "AstroCast/1.0 (desktop; +local-only)".
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void UserAgent_IsAstroCastSlashTheEngineVersion_DesktopApp()
    {
        ChartResult chart = GoldenFixtures.J2000Nyc(_fixture);
        Match match = Regex.Match(chart.EngineVersion, @"^AstroCast (\S+) \(");
        match.Success.Should().BeTrue("the engine version reads \"AstroCast <version> (SwissEph ...)\"");

        BaseMapSettings.UserAgent.Should().Be($"AstroCast/{match.Groups[1].Value} (desktop app)");
    }

    /// <summary>
    /// The attribution is "© OpenStreetMap contributors" linked to OpenStreetMap's copyright
    /// page and "© CARTO" linked to CARTO's attribution page.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void Attribution_NamesOpenStreetMapAndCarto_EachWithItsLink()
    {
        BaseMapSettings.OpenStreetMapAttributionText.Should().Be("© OpenStreetMap contributors");
        BaseMapSettings.OpenStreetMapAttributionUrl.Should().Be("https://www.openstreetmap.org/copyright");
        BaseMapSettings.CartoAttributionText.Should().Be("© CARTO");
        BaseMapSettings.CartoAttributionUrl.Should().Be("https://carto.com/attribution/");
    }

    /// <summary>The layer keeps its name, and the no-key line says what is missing.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void LayerName_IsUnchanged_AndTheNoKeyMessageNamesTheKey()
    {
        BaseMapSettings.LayerName.Should().Be("CartoDB Voyager");
        BaseMapSettings.NoKeyMessage.Should().Be("Base map unavailable: no CARTO key");
    }

    /// <summary>
    /// A tile outside the pyramid is refused with <see cref="ArgumentOutOfRangeException"/>, and
    /// the message names the argument, never the key.
    /// </summary>
    /// <param name="zoom">The zoom level.</param>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(-1, 0, 0)]
    [InlineData(31, 0, 0)]
    [InlineData(2, 4, 0)]
    [InlineData(2, 0, 4)]
    [InlineData(2, -1, 0)]
    [InlineData(2, 0, -1)]
    public void TileUrl_OutsideThePyramid_IsRefusedWithoutNamingTheKey(int zoom, int x, int y)
    {
        const string key = "secret-test-key-7f3a";

        Action act = () => BaseMapSettings.TileUrl(zoom, x, y, Key(key));

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.Message.Should().NotContain(key);
    }

    /// <summary>A key made through the loader, as the app makes it.</summary>
    private static BaseMapKey Key(string value) =>
        BaseMapKeyLoader.Load(null, value) ?? throw new InvalidOperationException("The loader returned no key.");
}
