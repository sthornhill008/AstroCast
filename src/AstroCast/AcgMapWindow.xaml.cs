using System.ComponentModel;
using System.Linq;
using System.Net.Http;
using System.Windows;

using AstroCast.Core.Enums;
using AstroCast.Core.Models.Astrocartography;
using AstroCast.Core.Services;
using AstroCast.Services;
using AstroCast.ViewModels;

using BruTile;
using BruTile.Predefined;
using BruTile.Web;

using Mapsui;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Mapsui.Tiling.Layers;
using Mapsui.Widgets.BoxWidgets;
using Mapsui.Widgets.ButtonWidgets;

using NetTopologySuite.Geometries;

namespace AstroCast;

/// <summary>
/// Floating window hosting the Astrocartography interactive map. Receives ACG
/// line data from <see cref="AstrocartographyViewModel.VisibleLines"/> on the
/// parent's <see cref="MainViewModel.Astrocartography"/> child (H-1.12) and
/// renders them as Mapsui line layers over OpenStreetMap tiles. Keeping the
/// map in its own resizable, optionally maximisable window solves the "narrow
/// sidebar squeezes the map" problem that the in-panel layout had. Window
/// lifecycle (creation, owner wiring, singleton tracking) remains on the
/// View layer in <c>MainWindow.xaml.cs.ShowAcgMapWindow</c> \u2014 Outcome A
/// of H-1.12's Audit Q3.
/// </summary>
public partial class AcgMapWindow : Window
{
    /// <summary>
    /// Prefix used for ACG line layers so they can be found and removed when
    /// the visible-lines collection changes.
    /// </summary>
    private const string AcgLayerNamePrefix = "ACG_";

    /// <summary>Name of the birth-location marker layer.</summary>
    private const string BirthLocationLayerName = "BirthLocation";

    /// <summary>
    /// Name of the active tile layer (CartoDB Voyager, English labels).
    /// </summary>
    private const string TileLayerName = BaseMapSettings.LayerName;

    /// <summary>
    /// Name used by an earlier version's tile layer; included in the cleanup
    /// predicate so a stale OSM layer left over from a pre-CartoDB build
    /// gets evicted on the next refresh.
    /// </summary>
    private const string LegacyOsmLayerName = "OpenStreetMap";

    /// <summary>
    /// The map's one-line legend: which line style is which. Colour is per body,
    /// as the toggles in the main window show.
    /// </summary>
    private const string LegendText =
        "Solid line: MC and ASC   |   Dashed line: IC and DSC   |   One colour per body";

    /// <summary>
    /// OSM zoom level used when automatically centring on the birth location
    /// after the first ACG computation. Level 4 gives a continent-scale view.
    /// </summary>
    private const int InitialZoomLevel = 4;

    /// <summary>
    /// Reference to the <see cref="AcgResult"/> we most recently auto-zoomed
    /// for. When the ViewModel produces a new result the reference changes
    /// and we re-centre; toggle-triggered refreshes preserve the user's pan/zoom.
    /// </summary>
    private object? _lastZoomedResult;

    private readonly MainViewModel _viewModel;

    /// <summary>
    /// The CARTO key the base map is requested with, or null when there is none, in which
    /// case the window adds no tile layer and says so under the legend.
    /// </summary>
    private readonly BaseMapKey? _baseMapKey;

    /// <summary>
    /// The HTTP client the tile layer fetches with, or null for the tile layer's own.
    /// </summary>
    private readonly HttpClient? _tileHttpClient;

    /// <summary>
    /// Initialises the ACG map window with the supplied ViewModel as its data source.
    /// </summary>
    /// <param name="viewModel">The main ViewModel providing ACG data and toggle state.</param>
    /// <param name="dataDirectory">
    /// The app's data directory, where an override key file
    /// (<see cref="BaseMapKeyLoader.OverrideFileName"/>) may be; null for none.
    /// </param>
    /// <param name="builtInKey">
    /// Supplies the built-in key; null reads the one built into the app
    /// (<see cref="BaseMapKeyLoader.ReadBuiltInKey()"/>). Tests pass their own.
    /// </param>
    /// <param name="tileHttpClient">
    /// The HTTP client the tile layer fetches with; null for the tile layer's own, as
    /// before. Tests pass one over a stub handler, so no test reaches a tile server.
    /// </param>
    public AcgMapWindow(
        MainViewModel viewModel,
        string? dataDirectory,
        Func<string?>? builtInKey = null,
        HttpClient? tileHttpClient = null)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        _viewModel = viewModel;

        // The base map's key: the override file in the data directory, then the built-in
        // key, then none.
        _baseMapKey = BaseMapKeyLoader.Load(dataDirectory, (builtInKey ?? BaseMapKeyLoader.ReadBuiltInKey)());
        _tileHttpClient = tileHttpClient;

        // Listen for toggle and result changes so the map stays in sync.
        // H-1.12: subscribe to the Astrocartography child rather than the
        // parent; every ACG-related property now lives under that child.
        _viewModel.Astrocartography.PropertyChanged += OnViewModelPropertyChanged;

        // Mapsui v5 Map.Tapped event signature is EventHandler<MapEventArgs>;
        // its WorldPosition gives SphericalMercator metres directly.
        AcgMapControl.Map.Tapped += OnMapTapped;

        // The legend is added once: RefreshMap replaces layers, never widgets.
        AcgMapControl.Map.Widgets.Enqueue(CreateLegend());

        // With no key there is no base map, and a line under the legend says so.
        if (_baseMapKey is null)
        {
            AcgMapControl.Map.Widgets.Enqueue(CreateNoKeyNotice());
        }

        // Initial render if data is already available (the typical entry path
        // when the user clicks Calculate ACG Map).
        if (_viewModel.Astrocartography.HasResult)
        {
            RefreshMap();
        }

        // Pre-populate the status bar with the last crossing report (if any).
        if (!string.IsNullOrEmpty(_viewModel.Astrocartography.CrossingReportText))
        {
            CrossingReportText.Text = SummariseCrossingReport(_viewModel.Astrocartography.CrossingReportText);
        }
    }

    /// <summary>
    /// Refresh the map when <see cref="AstrocartographyViewModel.VisibleLines"/>
    /// changes (toggle or new calculation), and refresh the status bar when
    /// <see cref="AstrocartographyViewModel.CrossingReportText"/> changes.
    /// </summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AstrocartographyViewModel.VisibleLines))
        {
            Dispatcher.Invoke(RefreshMap);
        }
        else if (e.PropertyName == nameof(AstrocartographyViewModel.CrossingReportText))
        {
            Dispatcher.Invoke(() =>
                CrossingReportText.Text = SummariseCrossingReport(_viewModel.Astrocartography.CrossingReportText));
        }
    }

    /// <summary>
    /// Handles map tap events: converts world position to lon/lat, asks the
    /// ViewModel for a crossing report, and updates the status bar. The
    /// PropertyChanged on <see cref="AstrocartographyViewModel.CrossingReportText"/>
    /// will also fire \u2014 both pathways converge on the same final UI state.
    /// </summary>
    private void OnMapTapped(object? sender, MapEventArgs e)
    {
        if (e.WorldPosition is null) return;

        (double lon, double lat) = SphericalMercator.ToLonLat(
            e.WorldPosition.X, e.WorldPosition.Y);

        _viewModel.UpdateCrossingReport(lat, lon);
        CrossingReportText.Text = SummariseCrossingReport(_viewModel.Astrocartography.CrossingReportText);
    }

    /// <summary>
    /// Rebuilds the ACG map layers from the current ViewModel visible-lines
    /// collection. Preserves the OSM base layer, replaces any ACG_* layers
    /// and the birth-location marker, and auto-centres on first sight of a
    /// new <see cref="AcgResult"/>.
    /// </summary>
    private void RefreshMap()
    {
        if (AcgMapControl is null) return;

        Map map = AcgMapControl.Map;

        // Remove existing ACG line layers, the birth-location marker, AND
        // any legacy OpenStreetMap layer left over from a pre-CartoDB build.
        // The new tile layer (TileLayerName) is intentionally NOT removed —
        // it's the bottom-most layer and we keep it across refreshes.
        map.Layers.Remove(layer =>
            layer.Name?.StartsWith(AcgLayerNamePrefix) == true
            || layer.Name == BirthLocationLayerName
            || layer.Name == LegacyOsmLayerName);

        // Ensure the English-label tile layer exists exactly once, with its attribution,
        // when there is a key; with none, no tile layer and no tile request.
        if (_baseMapKey is not null && !map.Layers.GetLayers(0).Any(l => l.Name == TileLayerName))
        {
            map.Layers.Add(CreateEnglishTileLayer(_baseMapKey, _tileHttpClient));
            foreach (HyperlinkWidget link in CreateAttribution())
            {
                map.Widgets.Enqueue(link);
            }
        }

        System.Collections.Generic.IReadOnlyList<AcgLine> visibleLines = _viewModel.Astrocartography.VisibleLines;

        // Add one MemoryLayer per celestial body so all four line types
        // share a consistent colour and can be toggled together.
        foreach (var bodyGroup in visibleLines.GroupBy(l => l.Body))
        {
            Color color = GetPlanetColor(bodyGroup.Key);
            System.Collections.Generic.List<IFeature> features = new();

            foreach (AcgLine line in bodyGroup)
            {
                bool isDashed = line.LineType is AcgLineType.IC or AcgLineType.DSC;

                // Draw the segments the shared geometry gives: the line split where it
                // crosses 180 degrees, so no stroke runs across the whole map.
                foreach (AcgSegment segment in AcgMapGeometry.Segments(line))
                {
                    // Convert (lat, lon) to SphericalMercator metres for the viewport.
                    Coordinate[] coords = segment.Points
                        .Select(c =>
                        {
                            (double x, double y) = SphericalMercator.FromLonLat(c.Longitude, c.Latitude);
                            return new Coordinate(x, y);
                        })
                        .ToArray();

                    LineString lineString = new(coords);
                    GeometryFeature feature = new(lineString);
                    feature.Styles.Add(new VectorStyle
                    {
                        Line = new Pen(color, isDashed ? 1.5 : 2.0)
                        {
                            PenStyle = isDashed ? PenStyle.Dash : PenStyle.Solid,
                        },
                    });
                    features.Add(feature);
                }
            }

            if (features.Count == 0) continue;

            MemoryLayer layer = new()
            {
                Name = $"{AcgLayerNamePrefix}{bodyGroup.Key}",
                Features = features,
            };
            map.Layers.Add(layer);
        }

        // Build text labels for every visible line so the user can identify
        // each line without consulting the colour legend.
        AddLabelLayer(map, visibleLines);

        // Add birth-location marker on top, and centre the viewport on first
        // sight of a given AcgResult.
        if (_viewModel.Astrocartography.HasResult && _viewModel.LastResult is not null)
        {
            double birthLat = _viewModel.LastResult.Input.Latitude;
            double birthLon = _viewModel.LastResult.Input.Longitude;
            (double bx, double by) = SphericalMercator.FromLonLat(birthLon, birthLat);

            PointFeature birthFeature = new(bx, by);
            birthFeature.Styles.Add(new SymbolStyle
            {
                SymbolScale = 0.6,
                Fill = new Brush(Color.FromArgb(255, 197, 165, 90)), // Gold
                Outline = new Pen(Color.FromArgb(255, 27, 42, 74), 2), // Navy
            });

            MemoryLayer birthLayer = new()
            {
                Name = BirthLocationLayerName,
                Features = [birthFeature],
            };
            map.Layers.Add(birthLayer);

            // Only auto-centre when the result itself is new; preserve the
            // user's pan/zoom on toggle-only refreshes.
            if (!ReferenceEquals(_lastZoomedResult, _viewModel.Astrocartography.CurrentResult))
            {
                _lastZoomedResult = _viewModel.Astrocartography.CurrentResult;
                CenterOnBirthLocation(bx, by);
            }
        }
    }

    /// <summary>
    /// Centres the map on the given SphericalMercator (x, y) at
    /// <see cref="InitialZoomLevel"/>. Clamped to the available resolutions
    /// so we never index out of the navigator's resolution table.
    /// </summary>
    private void CenterOnBirthLocation(double x, double y)
    {
        Navigator nav = AcgMapControl.Map.Navigator;
        System.Collections.Generic.IReadOnlyList<double> resolutions = nav.Resolutions;

        if (resolutions.Count == 0)
        {
            // Resolutions populate once the OSM layer attaches a scheme;
            // fall back to a simple CenterOn in that race.
            nav.CenterOn(x, y);
            return;
        }

        int level = System.Math.Clamp(InitialZoomLevel, 0, resolutions.Count - 1);
        nav.CenterOnAndZoomTo(new MPoint(x, y), resolutions[level]);
    }

    /// <summary>
    /// Compresses the multi-line crossing-report text into a single line
    /// suitable for the status bar by stripping the header and joining the
    /// remaining lines with " | ".
    /// </summary>
    private static string SummariseCrossingReport(string report)
    {
        if (string.IsNullOrWhiteSpace(report))
        {
            return "Click the map to see nearby angular lines.";
        }

        // Lines look like:
        //   "Crossing Report: 51.50°N, 0.13°W"
        //   ""
        //   "  Sun MC: 50 km"
        //   "  ..."
        // Or, with no crossings:
        //   "Crossing Report: 51.50°N, 0.13°W"
        //   "No angular lines within 334 km."
        string[] lines = report.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return "Click the map to see nearby angular lines.";
        }

        string header = lines[0].Trim();
        string[] body = lines.Skip(1).Select(l => l.Trim()).Where(l => l.Length > 0).ToArray();

        return body.Length == 0 ? header : $"{header}    {string.Join("  |  ", body)}";
    }

    /// <summary>
    /// Build a CartoDB Voyager tile layer (English/Latin labels) and return it
    /// ready to add to the map. The tile source is configured via BruTile's
    /// <see cref="HttpTileSource"/> from <see cref="BaseMapSettings"/>, with a
    /// custom HTTP request configurator that sets the User-Agent header, because
    /// BruTile 6 removed the older <c>AddHeader</c> method. The layer's own
    /// one-link attribution is switched off: the map carries CARTO's two links
    /// instead (<see cref="CreateAttribution"/>).
    /// </summary>
    /// <param name="key">The key the tiles are requested with.</param>
    /// <param name="httpClient">The client to fetch with; null for the layer's own.</param>
    private static TileLayer CreateEnglishTileLayer(BaseMapKey key, HttpClient? httpClient)
    {
        HttpTileSource tileSource = new(
            tileSchema: new GlobalSphericalMercator(0, 19),
            urlFormatter: BaseMapSettings.UrlFormatter(key),
            name: TileLayerName,
            configureHttpRequestMessage: req =>
                req.Headers.TryAddWithoutValidation("User-Agent", BaseMapSettings.UserAgent));

        TileLayer layer = new(tileSource, httpClient: httpClient)
        {
            Name = TileLayerName,
        };
        layer.Attribution.Enabled = false;
        return layer;
    }

    /// <summary>
    /// Build the map's one-line legend (<see cref="LegendText"/>) as a Mapsui widget in
    /// the top-left corner. It is Mapsui's own widget, not either UI framework's, so
    /// both heads draw it with the same code.
    /// </summary>
    private static TextBoxWidget CreateLegend() => new()
    {
        Text = LegendText,
        TextSize = 12,
        TextColor = Color.FromArgb(255, 27, 42, 74), // Navy
        BackColor = Color.FromArgb(220, 255, 255, 255),
        CornerRadius = 4,
        Padding = new MRect(6, 3, 6, 3),
        Margin = new MRect(8, 8, 8, 8),
        HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Left,
        VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Top,
    };

    /// <summary>
    /// Build the one-line notice shown under the legend when there is no key
    /// (<see cref="BaseMapSettings.NoKeyMessage"/>), in the legend's style with
    /// dark-red text.
    /// </summary>
    private static TextBoxWidget CreateNoKeyNotice() => new()
    {
        Text = BaseMapSettings.NoKeyMessage,
        TextSize = 12,
        TextColor = Color.FromArgb(255, 139, 0, 0), // DarkRed
        BackColor = Color.FromArgb(220, 255, 255, 255),
        CornerRadius = 4,
        Padding = new MRect(6, 3, 6, 3),
        // MRect is (left, bottom, right, top): the top margin, 36, clears the legend above.
        Margin = new MRect(8, 8, 8, 36),
        HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Left,
        VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Top,
    };

    /// <summary>
    /// Build the base map's attribution as CARTO requires it: "© OpenStreetMap
    /// contributors" and "© CARTO", each a link, in the bottom-right corner, one
    /// above the other, in full-strength text. Added with the tile layer, so it is
    /// shown whenever the tiles are.
    /// </summary>
    private static HyperlinkWidget[] CreateAttribution() =>
    [
        CreateAttributionLink(
            BaseMapSettings.OpenStreetMapAttributionText, BaseMapSettings.OpenStreetMapAttributionUrl, bottomMargin: 26),
        CreateAttributionLink(
            BaseMapSettings.CartoAttributionText, BaseMapSettings.CartoAttributionUrl, bottomMargin: 4),
    ];

    /// <summary>
    /// Build one attribution link, 20 pixels high, right-aligned at the bottom of
    /// the map, <paramref name="bottomMargin"/> pixels above its edge.
    /// </summary>
    private static HyperlinkWidget CreateAttributionLink(string text, string url, double bottomMargin) => new()
    {
        Text = text,
        Url = url,
        TextSize = 12,
        TextColor = Color.FromArgb(255, 27, 42, 74), // Navy
        BackColor = Color.FromArgb(220, 255, 255, 255),
        CornerRadius = 4,
        Height = 20,
        Padding = new MRect(6, 0, 6, 0),
        // MRect is (left, bottom, right, top); only the right and bottom margins apply here.
        Margin = new MRect(4, bottomMargin, 4, bottomMargin),
        HorizontalAlignment = Mapsui.Widgets.HorizontalAlignment.Right,
        VerticalAlignment = Mapsui.Widgets.VerticalAlignment.Bottom,
    };

    /// <summary>
    /// Build a single MemoryLayer carrying one or two text labels per visible
    /// ACG line so the user can read the planet symbol and angle type without
    /// having to consult the colour legend.
    /// </summary>
    private static void AddLabelLayer(Map map, IReadOnlyList<AcgLine> visibleLines)
    {
        List<IFeature> labelFeatures = new();

        foreach (AcgLine line in visibleLines)
        {
            string label = $"{GetPlanetSymbol(line.Body)} {line.LineType}";
            Color color = GetPlanetColor(line.Body);

            foreach (AcgCoordinate pos in AcgMapGeometry.LabelPositions(line))
            {
                (double x, double y) = SphericalMercator.FromLonLat(pos.Longitude, pos.Latitude);

                PointFeature feature = new(x, y);
                feature.Styles.Add(new LabelStyle
                {
                    Text = label,
                    ForeColor = color,
                    BackColor = new Brush(Color.FromArgb(200, 255, 255, 255)),
                    Font = new Font { Size = 11, Bold = true },
                    HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
                    VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center,
                    Offset = new Offset(0, -12),
                    BorderColor = color,
                    BorderThickness = 0.5,
                    CornerRounding = 3,
                });
                labelFeatures.Add(feature);
            }
        }

        if (labelFeatures.Count == 0) return;

        MemoryLayer labelLayer = new()
        {
            Name = $"{AcgLayerNamePrefix}Labels",
            Features = labelFeatures,
        };
        map.Layers.Add(labelLayer);
    }

    /// <summary>
    /// Returns a two-letter text abbreviation for a celestial body, used in
    /// ACG line labels. Originally tried the canonical Unicode astrological
    /// glyphs (☉ ☽ ☿ ♀ ♂ ♃ ♄ ♅ ♆ ♇ ⚷ ☊) but SkiaSharp's default font on
    /// Windows renders all of them as tofu boxes, so labels were unreadable.
    /// Two-letter codes ("Su", "Mo", "Ju", ...) render reliably and stay
    /// short enough that "Su MC" / "Ju ASC" still fit comfortably on the map.
    /// </summary>
    private static string GetPlanetSymbol(CelestialBodyId body) => body switch
    {
        CelestialBodyId.Sun => "Su",
        CelestialBodyId.Moon => "Mo",
        CelestialBodyId.Mercury => "Me",
        CelestialBodyId.Venus => "Ve",
        CelestialBodyId.Mars => "Ma",
        CelestialBodyId.Jupiter => "Ju",
        CelestialBodyId.Saturn => "Sa",
        CelestialBodyId.Uranus => "Ur",
        CelestialBodyId.Neptune => "Ne",
        CelestialBodyId.Pluto => "Pl",
        CelestialBodyId.Chiron => "Ch",
        CelestialBodyId.TrueNode => "No",
        _ => "??",
    };

    /// <summary>
    /// Returns the ACG line colour for a celestial body. These mirror the
    /// CheckBox Foreground values in <c>MainWindow.xaml</c> so the map and
    /// the legend in the main window agree.
    /// </summary>
    private static Color GetPlanetColor(CelestialBodyId body) => body switch
    {
        CelestialBodyId.Sun => Color.FromArgb(255, 255, 215, 0),       // Gold
        CelestialBodyId.Moon => Color.FromArgb(255, 192, 192, 192),    // Silver
        CelestialBodyId.Mercury => Color.FromArgb(255, 255, 140, 0),   // DarkOrange
        CelestialBodyId.Venus => Color.FromArgb(255, 50, 205, 50),     // LimeGreen
        CelestialBodyId.Mars => Color.FromArgb(255, 220, 20, 60),      // Crimson
        CelestialBodyId.Jupiter => Color.FromArgb(255, 138, 43, 226),  // BlueViolet
        CelestialBodyId.Saturn => Color.FromArgb(255, 0, 0, 139),      // DarkBlue
        CelestialBodyId.Uranus => Color.FromArgb(255, 0, 206, 209),    // DarkTurquoise
        CelestialBodyId.Neptune => Color.FromArgb(255, 0, 128, 128),   // Teal
        CelestialBodyId.Pluto => Color.FromArgb(255, 139, 0, 0),       // DarkRed
        CelestialBodyId.Chiron => Color.FromArgb(255, 139, 69, 19),    // SaddleBrown
        CelestialBodyId.TrueNode => Color.FromArgb(255, 105, 105, 105),// DimGray
        _ => Color.FromArgb(255, 128, 128, 128),
    };

    /// <summary>
    /// Unhook event handlers when the window closes to avoid retaining the
    /// ViewModel through the PropertyChanged subscription after the window
    /// is gone.
    /// </summary>
    protected override void OnClosed(EventArgs e)
    {
        _viewModel.Astrocartography.PropertyChanged -= OnViewModelPropertyChanged;
        AcgMapControl.Map.Tapped -= OnMapTapped;
        base.OnClosed(e);
    }
}
