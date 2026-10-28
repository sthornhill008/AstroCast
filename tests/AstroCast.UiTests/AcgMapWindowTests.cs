using System.Diagnostics;

using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;
using AstroCast.Services;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Tiling.Layers;
using Mapsui.UI.Avalonia;
using Mapsui.Widgets.BoxWidgets;
using Mapsui.Widgets.ButtonWidgets;

using NetTopologySuite.Geometries;

using MStyles = Mapsui.Styles;

namespace AstroCast.UiTests;

/// <summary>
/// Pins what the Avalonia map window draws (rule 4 of branch J, E3-8): each line as the segments
/// the shared geometry gives it, split where it crosses 180° so no stroke runs across the map, in
/// its present style (solid MC and ASC, dashed IC and DSC, one colour per body), with a one-line
/// legend saying which style is which. And its base map (rules 3 to 6 of the map-tiles branch):
/// CARTO Voyager tiles requested with the key and the app's User-Agent, the attribution's two
/// links, and with no key no tile layer, no request and a line saying so.
/// </summary>
/// <remarks>
/// The window is the real one, fed a synthetic result through the view model, as the map panel
/// feeds it; the drawing is read back from the map's own layers and widgets, not from pixels. The
/// WPF window mirrors this one line for line and is checked on Maple. Every window here fetches
/// its tiles through <see cref="RecordingTileHandler"/> and is given its key sources, so no test
/// reaches a tile server or depends on a key file on the machine.
/// </remarks>
public sealed class AcgMapWindowTests
{
    /// <summary>A made-up key with characters the URL must escape.</summary>
    private const string TestKey = "test key/ü";

    /// <summary><see cref="TestKey"/> as the tile URL carries it.</summary>
    private const string EscapedTestKey = "test%20key%2F%C3%BC";

    /// <summary>How long a test waits for the tile layer's first request.</summary>
    private static readonly TimeSpan RequestWait = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A Moon ASC line that crosses 180° is drawn as two solid strokes meeting the map's edge, the
    /// Moon DSC line as one dashed stroke, and the Sun MC line as one solid stroke from 85° S to
    /// 85° N; no stroke spans more than 180° of longitude.
    /// </summary>
    [AvaloniaFact]
    public void MapWindow_DrawsEachLineAsItsSegments_InItsPresentStyle()
    {
        using RecordingTileHandler handler = new();
        using HttpClient client = new(handler);
        (AcgMapWindow window, MapControl map) = Show(client, () => TestKey);

        List<(List<(double Lon, double Lat)> Points, MStyles.Pen Pen)> moon = Strokes(map, "ACG_Moon");
        List<(List<(double Lon, double Lat)> Points, MStyles.Pen Pen)> sun = Strokes(map, "ACG_Sun");

        Assert.Equal(3, moon.Count);
        Assert.Equal(2, moon.Count(s => s.Pen.PenStyle == MStyles.PenStyle.Solid && s.Pen.Width == 2.0));
        Assert.Equal(1, moon.Count(s => s.Pen.PenStyle == MStyles.PenStyle.Dash && s.Pen.Width == 1.5));
        Assert.Contains(moon, s => Math.Abs(s.Points[^1].Lon - 180.0) < 1e-6);
        Assert.Contains(moon, s => Math.Abs(s.Points[0].Lon + 180.0) < 1e-6);

        (List<(double Lon, double Lat)> points, MStyles.Pen pen) = Assert.Single(sun);
        Assert.Equal(MStyles.PenStyle.Solid, pen.PenStyle);
        Assert.Equal(-85.0, points[0].Lat, 6);
        Assert.Equal(85.0, points[^1].Lat, 6);

        foreach ((List<(double Lon, double Lat)> stroke, _) in moon.Concat(sun))
        {
            Assert.True(stroke.Max(p => p.Lon) - stroke.Min(p => p.Lon) <= 180.0 + 1e-9,
                "A stroke spans more than 180 degrees of longitude, so it runs across the whole map.");
        }

        window.Close();
    }

    /// <summary>
    /// The map carries one legend, on one line, saying which line style is which. It is the one
    /// widget that is a plain <see cref="TextBoxWidget"/>: Mapsui's own logging and performance
    /// widgets derive from that type, so a search by type alone finds them too.
    /// </summary>
    [AvaloniaFact]
    public void MapWindow_CarriesAOneLineLegend()
    {
        using RecordingTileHandler handler = new();
        using HttpClient client = new(handler);
        (AcgMapWindow window, MapControl map) = Show(client, () => TestKey);

        TextBoxWidget legend = Assert.IsType<TextBoxWidget>(
            Assert.Single(map.Map.Widgets, w => w.GetType() == typeof(TextBoxWidget)));
        Assert.Equal("Solid line: MC and ASC   |   Dashed line: IC and DSC   |   One colour per body", legend.Text);

        window.Close();
    }

    /// <summary>
    /// With a key, the window requests CARTO's keyed Voyager tiles: every request goes to
    /// basemaps.cartocdn.com under /rastertiles/voyager/, carries the key escaped as its one
    /// query parameter, and sends the app's User-Agent, "AstroCast/&lt;version&gt; (desktop app)".
    /// Until this branch the requests went to the {a,b,c,d} subdomains with no key and the
    /// User-Agent "AstroCast/1.0 (desktop; +local-only)".
    /// </summary>
    [AvaloniaFact]
    public async Task MapWindow_RequestsCartoTiles_WithTheKeyAndTheUserAgent()
    {
        using RecordingTileHandler handler = new();
        using HttpClient client = new(handler);
        (AcgMapWindow window, MapControl map) = Show(client, () => TestKey);

        await Pump(() => handler.Requests.Count > 0, RequestWait);

        TileLayer tiles = Assert.Single(map.Map.Layers.OfType<TileLayer>());
        Assert.Equal("CartoDB Voyager", tiles.Name);
        IReadOnlyList<RecordedTileRequest> requests = handler.Requests;
        Assert.NotEmpty(requests);
        Assert.All(requests, r =>
        {
            Assert.Equal("https", r.Uri.Scheme);
            Assert.Equal("basemaps.cartocdn.com", r.Uri.Host);
            Assert.Matches(@"^/rastertiles/voyager/\d+/\d+/\d+\.png$", r.Uri.AbsolutePath);
            Assert.Equal("?key=" + EscapedTestKey, r.Uri.Query);
            Assert.Equal(BaseMapSettings.UserAgent, r.UserAgent);
            Assert.Matches(@"^AstroCast/\d+\.\d+\.\d+ \(desktop app\)$", r.UserAgent);
        });

        window.Close();
    }

    /// <summary>
    /// An override key file in the data directory the window is given wins over the built-in
    /// key, as the head's data directory is meant to.
    /// </summary>
    [AvaloniaFact]
    public async Task MapWindow_OverrideKeyFileInTheDataDirectory_WinsOverTheBuiltInKey()
    {
        string dataDirectory = Path.Combine(Path.GetTempPath(), "astrocast-mapwindow-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataDirectory);
        try
        {
            File.WriteAllText(Path.Combine(dataDirectory, BaseMapKeyLoader.OverrideFileName), "override-key\n");
            using RecordingTileHandler handler = new();
            using HttpClient client = new(handler);
            (AcgMapWindow window, _) = Show(client, () => "built-in-key", dataDirectory);

            await Pump(() => handler.Requests.Count > 0, RequestWait);

            Assert.NotEmpty(handler.Requests);
            Assert.All(handler.Requests, r => Assert.Equal("?key=override-key", r.Uri.Query));

            window.Close();
        }
        finally
        {
            Directory.Delete(dataDirectory, recursive: true);
        }
    }

    /// <summary>
    /// The attribution is two links, as CARTO requires: "© OpenStreetMap contributors" to
    /// OpenStreetMap's copyright page and "© CARTO" to CARTO's attribution page, bottom right,
    /// in full-strength text, clear of the legend and of each other. The tile layer's own
    /// one-link attribution is off. Until this branch there was one link, to CARTO.
    /// </summary>
    [AvaloniaFact]
    public async Task MapWindow_ShowsBothAttributionLinks_BottomRight_ClearOfTheLegend()
    {
        using RecordingTileHandler handler = new();
        using HttpClient client = new(handler);
        (AcgMapWindow window, MapControl map) = Show(client, () => TestKey);

        await Pump(() => false, TimeSpan.FromMilliseconds(500));

        HyperlinkWidget[] links = ShownLinks(map);
        Assert.Equal(2, links.Length);
        HyperlinkWidget osm = Assert.Single(links, w => w.Text == "© OpenStreetMap contributors");
        Assert.Equal("https://www.openstreetmap.org/copyright", osm.Url);
        HyperlinkWidget carto = Assert.Single(links, w => w.Text == "© CARTO");
        Assert.Equal("https://carto.com/attribution/", carto.Url);
        Assert.False(Assert.Single(map.Map.Layers.OfType<TileLayer>()).Attribution.Enabled);

        TextBoxWidget legend = Legend(map);
        Assert.NotNull(legend.Envelope);
        foreach (HyperlinkWidget link in links)
        {
            Assert.True(link.Enabled);
            Assert.Equal(Mapsui.Widgets.HorizontalAlignment.Right, link.HorizontalAlignment);
            Assert.Equal(Mapsui.Widgets.VerticalAlignment.Bottom, link.VerticalAlignment);
            Assert.Equal(255, link.TextColor.A);
            Assert.True(link.TextSize >= 12);
            MRect envelope = Assert.IsType<MRect>(link.Envelope);
            Assert.True(envelope.MinX >= 0 && envelope.MaxX <= map.Bounds.Width
                && envelope.MinY >= 0 && envelope.MaxY <= map.Bounds.Height, "An attribution link is drawn off the map.");
            Assert.False(envelope.Intersects(legend.Envelope), "An attribution link overlaps the legend.");
        }

        Assert.False(Assert.IsType<MRect>(osm.Envelope).Intersects(carto.Envelope), "The two links overlap.");

        window.Close();
    }

    /// <summary>
    /// With no key (no override file and no built-in key), the window adds no tile layer, sends
    /// no request, shows "Base map unavailable: no CARTO key" in one line under the legend, and
    /// draws its lines and legend as before, with no attribution.
    /// </summary>
    [AvaloniaFact]
    public async Task MapWindow_WithNoKey_AddsNoTileLayer_RequestsNothing_AndSaysSoUnderTheLegend()
    {
        using RecordingTileHandler handler = new();
        using HttpClient client = new(handler);
        (AcgMapWindow window, MapControl map) = Show(client, () => null);

        await Pump(() => false, TimeSpan.FromSeconds(2));

        Assert.Empty(map.Map.Layers.OfType<TileLayer>());
        Assert.Empty(handler.Requests);
        Assert.Empty(ShownLinks(map));
        Assert.Equal(3, Strokes(map, "ACG_Moon").Count);

        TextBoxWidget legend = Legend(map);
        TextBoxWidget notice = Assert.Single(map.Map.Widgets.OfType<TextBoxWidget>(), w => w.Text == "Base map unavailable: no CARTO key");
        Assert.Equal(Mapsui.Widgets.HorizontalAlignment.Left, notice.HorizontalAlignment);
        Assert.Equal(Mapsui.Widgets.VerticalAlignment.Top, notice.VerticalAlignment);
        MRect legendEnvelope = Assert.IsType<MRect>(legend.Envelope);
        MRect noticeEnvelope = Assert.IsType<MRect>(notice.Envelope);
        Assert.True(noticeEnvelope.MinY >= legendEnvelope.MaxY, "The no-key line is not under the legend.");
        Assert.Equal(legendEnvelope.MinX, noticeEnvelope.MinX, 6);

        window.Close();
    }

    #region Helpers

    /// <summary>
    /// Opens the map window on a view model holding the synthetic result, with the given key
    /// sources, its tiles fetched through <paramref name="tileHttpClient"/>.
    /// </summary>
    private static (AcgMapWindow Window, MapControl Map) Show(
        HttpClient tileHttpClient, Func<string?> builtInKey, string? dataDirectory = null)
    {
        MainViewModel vm = MainViewModelFactory.Create();
        vm.Astrocartography.CurrentResult = SyntheticResult();
        vm.Astrocartography.HasResult = true;

        AcgMapWindow window = new(vm, dataDirectory, builtInKey, tileHttpClient) { Width = 1200, Height = 800 };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        return (window, window.GetVisualDescendants().OfType<MapControl>().Single());
    }

    /// <summary>
    /// Renders and runs the UI thread until <paramref name="done"/> holds or
    /// <paramref name="limit"/> passes, giving the tile layer time to fetch and the widgets
    /// their envelopes.
    /// </summary>
    private static async Task Pump(Func<bool> done, TimeSpan limit)
    {
        Stopwatch elapsed = Stopwatch.StartNew();
        do
        {
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs();
            await Task.Delay(20);
        }
        while (!done() && elapsed.Elapsed < limit);

        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// The links the map shows: its own widgets and its enabled layers' attributions, those
    /// with text. Every Mapsui layer carries an attribution widget, empty unless set, which
    /// draws nothing.
    /// </summary>
    private static HyperlinkWidget[] ShownLinks(MapControl map) => map.Map.GetWidgetsOfMapAndLayers()
        .OfType<HyperlinkWidget>()
        .Where(w => !string.IsNullOrEmpty(w.Text))
        .ToArray();

    /// <summary>The map's legend, found by its text.</summary>
    private static TextBoxWidget Legend(MapControl map) => Assert.Single(
        map.Map.Widgets.OfType<TextBoxWidget>(),
        w => w.Text == "Solid line: MC and ASC   |   Dashed line: IC and DSC   |   One colour per body");

    /// <summary>The line strokes of one body's layer, as longitude and latitude, with their pens.</summary>
    private static List<(List<(double Lon, double Lat)> Points, MStyles.Pen Pen)> Strokes(MapControl map, string layerName)
    {
        MemoryLayer layer = map.Map.Layers.OfType<MemoryLayer>().Single(l => l.Name == layerName);
        List<(List<(double Lon, double Lat)> Points, MStyles.Pen Pen)> strokes = [];
        foreach (GeometryFeature feature in layer.Features.OfType<GeometryFeature>())
        {
            if (feature.Geometry is LineString line
                && feature.Styles.OfType<MStyles.VectorStyle>().Single().Line is MStyles.Pen pen)
            {
                strokes.Add((line.Coordinates.Select(c => SphericalMercator.ToLonLat(c.X, c.Y)).Select(p => (p.lon, p.lat)).ToList(), pen));
            }
        }

        return strokes;
    }

    /// <summary>
    /// A result with a Moon ASC line that crosses 180° going east between 11° and 12° N, a Moon
    /// DSC line, and a Sun MC line.
    /// </summary>
    private static AcgResult SyntheticResult()
    {
        static AcgLine Line(CelestialBodyId body, string name, AcgLineType type, params (double Lat, double Lon)[] points) => new()
        {
            Body = body,
            BodyName = name,
            LineType = type,
            Coordinates = points.Select(p => new AcgCoordinate(p.Lat, p.Lon)).ToList(),
            PlanetLongitude = 0.0,
            PlanetLatitude = 0.0,
        };

        return new AcgResult
        {
            NatalChart = new ChartResult
            {
                Input = new ChartInput
                {
                    Name = "Map Window Test",
                    LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
                    TimeZone = "America/New_York",
                    Latitude = 40.7128,
                    Longitude = -74.006,
                    LocationName = "New York City",
                },
                JulianDay = 2451545.0,
                Obliquity = 23.4367,
                UtcDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
                Positions = [],
                Houses = new HouseData
                {
                    Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
                    Ascendant = 0.0,
                    Midheaven = 270.0,
                    Armc = 280.0,
                    Vertex = 0.0,
                },
                Aspects = [],
                Midpoints = [],
                MidpointActivations = [],
                Dignities = [],
                MoonCondition = null,
                ChartContext = null,
                FixedStarConjunctions = [],
                ResolvedUtcOffsetHours = -5.0,
                ResolvedOffsetLabel = "EST (UTC-5)",
                EngineVersion = "Test 1.0.0",
                CalculatedAt = DateTime.UtcNow,
            },
            Lines =
            [
                Line(CelestialBodyId.Moon, "Moon", AcgLineType.ASC, (10, 170), (11, 178), (12, -176), (13, -170)),
                Line(CelestialBodyId.Moon, "Moon", AcgLineType.DSC, (-20, 30), (0, 32), (20, 34)),
                Line(CelestialBodyId.Sun, "Sun", AcgLineType.MC, (-85, 0.82), (85, 0.82)),
            ],
            Obliquity = 23.4393,
            GastDegrees = 280.4606,
            LatitudeMin = -85.0,
            LatitudeMax = 85.0,
            LatitudeStep = 1.0,
        };
    }

    #endregion
}
