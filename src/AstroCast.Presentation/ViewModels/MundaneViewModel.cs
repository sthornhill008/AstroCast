using System.ComponentModel;
using System.Globalization;
using System.Net.Http;

using GeoTimeZone;
using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;
using AstroCast.Services;

namespace AstroCast.ViewModels;

/// <summary>
/// Child view model that owns the Mundane panel state: the shared mundane
/// location (address, coordinates, and IANA timezone) plus the four
/// independent sub-features that use that location — Ingress Charts,
/// Lunation Charts, Mundane Aspect Finder, and Eclipse Calculator. Extracted
/// from <see cref="MainViewModel"/> by Session H-1.9 as the ninth
/// decomposition child and the second application of the H-1.6 nested
/// LocationSearch migration pattern.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> This VM owns the Mundane panel's data surface:
/// <see cref="LocationName"/>, <see cref="LatitudeText"/>,
/// <see cref="LongitudeText"/>, <see cref="TimeZone"/>;
/// <see cref="IngressPlanet"/>, <see cref="IngressYear"/>,
/// <see cref="CardinalOnly"/>, <see cref="CurrentIngressSeries"/>,
/// <see cref="HasIngressSeries"/>, <see cref="CurrentCardinalIngresses"/>,
/// <see cref="HasCardinalIngresses"/>;
/// <see cref="LunationYear"/>, <see cref="CurrentLunationSeries"/>,
/// <see cref="HasLunationSeries"/>;
/// <see cref="MABody1"/>, <see cref="MABody2"/>, <see cref="MAHarmonicMode"/>,
/// <see cref="MAStartYear"/>, <see cref="MAEndYear"/>,
/// <see cref="CurrentMASeries"/>, <see cref="HasMASeries"/>;
/// <see cref="EclipseStartYear"/>, <see cref="EclipseEndYear"/>,
/// <see cref="CurrentEclipseSeries"/>, <see cref="HasEclipseSeries"/>; and
/// the read-only option lists <see cref="AvailableIngressPlanets"/>,
/// <see cref="AvailableMundanePlanets"/>, and
/// <see cref="AvailableHarmonicModes"/>. XAML bindings for every one of these
/// fields live under <c>Mundane.X</c> in <c>MainWindow.xaml</c>.
/// </para>
/// <para>
/// <b>Multi-result pattern (H-1.4 / H-1.7 / H-1.8 precedent).</b> Five
/// independent result surfaces — ingress series
/// (<see cref="CurrentIngressSeries"/>), cardinal-ingress chart batch
/// (<see cref="CurrentCardinalIngresses"/>), lunation series
/// (<see cref="CurrentLunationSeries"/>), mundane-aspect series
/// (<see cref="CurrentMASeries"/>), and eclipse series
/// (<see cref="CurrentEclipseSeries"/>) — each expose their own
/// <c>HasXxx</c> flag. There is deliberately no aggregate
/// <c>HasResult</c> property: the five calculations are independent and gate
/// independent export commands. Each result setter raises
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> for itself, and the
/// companion <c>HasXxx</c> flag is written explicitly by the parent command
/// (see <b>HasX semantics</b> below).
/// </para>
/// <para>
/// <b>Nested <see cref="LocationSearch"/> (H-1.6 second application).</b>
/// The mundane panel's location search was a parent-owned sibling
/// (<c>MundaneLocationSearch</c>) under H-1.3. H-1.9 migrates it to a nested
/// child under this VM — the same play H-1.6 ran for the Electional panel.
/// The child is constructed with the shared <see cref="HttpClient"/> and
/// <see cref="NominatimRateLimiter"/> threaded through from the parent so
/// the Nominatim 1-request-per-second policy remains honoured globally
/// across all four location-search instances (Chart Entry, Electional,
/// Mundane, Relocation). The search text is seeded with the historical
/// pre-H-1.3 default "Washington, DC". Bindings on
/// <c>MundaneLocationSearch.X</c> are rewritten to
/// <c>Mundane.LocationSearch.X</c>.
/// </para>
/// <para>
/// <b>Follow-on handler moved from parent (Rule #4 refinement).</b>
/// <see cref="OnLocationSearchPropertyChanged"/> was previously
/// <c>MainViewModel.OnMundaneLocationSearchPropertyChanged</c>. Cross-VM
/// rule #4 keeps commands on the parent when they write to sibling-owned
/// state, but this handler's writes (<see cref="LatitudeText"/>,
/// <see cref="LongitudeText"/>, <see cref="LocationName"/>,
/// <see cref="TimeZone"/>) are now all intra-VM — they target
/// <c>this.*</c> fields under the same umbrella. The handler therefore
/// moves with its owner into this child, matching the H-1.6 precedent.
/// <see cref="MainViewModel.StatusText"/> is still written because status
/// text is a parent-owned concern (rule #2 last resort).
/// </para>
/// <para>
/// <b>Commands stay on parent (rule #4).</b>
/// <c>LookupMundaneLocationCommand</c>, <c>FindIngressesCommand</c>,
/// <c>CalculateCardinalIngressesCommand</c>,
/// <c>ExportIngressSeriesMarkdownCommand</c>,
/// <c>ExportCardinalIngressesMarkdownCommand</c>,
/// <c>FindLunationsCommand</c>, <c>CalculateLunationChartsCommand</c>,
/// <c>ExportLunationSeriesCommand</c>, <c>FindMundaneAspectsCommand</c>,
/// <c>ExportMASeriesCommand</c>, <c>FindEclipsesCommand</c>, and
/// <c>ExportEclipseSeriesCommand</c> all remain on
/// <see cref="MainViewModel"/>. Each reads a parent-owned service
/// (<c>IIngressService</c>, <c>ILunationService</c>,
/// <c>IMundaneAspectService</c>, <c>IEclipseService</c>), threads the
/// parent-owned <c>OperationCancellationRegistry</c> token, and writes
/// status text on the parent. The command methods read inputs from this
/// child and write the computed result back through the public result
/// setters plus the companion <c>HasXxx</c> flag.
/// </para>
/// <para>
/// <b>HasX semantics — all five are value-based (H-1.8 pattern).</b> Every
/// mundane service (<c>IIngressService</c>, <c>ILunationService</c>,
/// <c>IMundaneAspectService</c>, <c>IEclipseService</c>) returns a
/// non-null series even when the scan yields zero matches — the returned
/// object carries an empty collection and <c>Count == 0</c>. The
/// pre-extraction <see cref="MainViewModel"/> code therefore wrote
/// <c>HasXxx = series.Count &gt; 0</c> explicitly after each scan, and
/// <see cref="HasIngressSeries"/>, <see cref="HasCardinalIngresses"/>,
/// <see cref="HasLunationSeries"/>, <see cref="HasMASeries"/>, and
/// <see cref="HasEclipseSeries"/> intentionally do <i>not</i> mirror
/// <c>CurrentXxx is not null</c>. A scan that returns zero results sets
/// the current-series reference to a non-null empty series but leaves the
/// corresponding flag at <see langword="false"/>, gating the export
/// command off. Each flag is a plain <see cref="bool"/> property written
/// explicitly by the parent command; the setter fires
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> for XAML
/// button re-evaluation. This matches the H-1.8 <c>HasGCSeries</c> /
/// <c>HasSynodicCycleSeries</c> pattern verbatim.
/// </para>
/// <para>
/// <b>F-GR-2 watch item.</b> Two field initialisers call
/// <see cref="DateTime.Today"/> directly: <see cref="IngressYear"/> and
/// <see cref="LunationYear"/>. These carry forward the same clock-coupling
/// that the pre-extraction <see cref="MainViewModel"/> had. Session
/// Golden Refresh watch-item F-GR-2 tracks migrating this to an injected
/// <c>IClock</c>; Session H-1.9 preserves the existing coupling to keep
/// the extraction a pure refactor and adds <see cref="MundaneViewModel"/>
/// to the watch item's VM list alongside
/// <see cref="ChartEntryViewModel"/>, <see cref="TransitsViewModel"/>,
/// <see cref="ElectionalViewModel"/>, and
/// <see cref="ReturnsViewModel"/>.
/// </para>
/// </remarks>
public sealed class MundaneViewModel : ChildViewModelBase
{
    #region Fields

    private readonly ILogger _logger;

    // Mundane Location — shared by Ingress (cardinal charts), Lunation
    // (chart batch) and, potentially, future per-location mundane features.
    private string _locationName = "Washington, DC";
    private string _latitudeText = "38.8977";
    private string _longitudeText = "-77.0365";
    private string _timeZone = "America/New_York";

    // Ingress Charts
    private int _ingressYear = DateTime.Today.Year;
    private CelestialBodyId _ingressPlanet = CelestialBodyId.Sun;
    private bool _cardinalOnly = true;
    private IngressSeries? _currentIngressSeries;
    private IReadOnlyList<IngressResult>? _currentCardinalIngresses;
    private bool _hasIngressSeries;
    private bool _hasCardinalIngresses;

    // Lunation Charts
    private int _lunationYear = DateTime.Today.Year;
    private LunationSeries? _currentLunationSeries;
    private bool _hasLunationSeries;

    // Mundane Aspect Finder
    private CelestialBodyId _maBody1 = CelestialBodyId.Saturn;
    private CelestialBodyId _maBody2 = CelestialBodyId.Mars;
    private string _maHarmonicMode = "90\u00b0 Harmonic";
    private int _maStartYear = 2025;
    private int _maEndYear = 2035;
    private MundaneAspectSeries? _currentMASeries;
    private bool _hasMASeries;

    // Eclipse Calculator
    private int _eclipseStartYear = 2025;
    private int _eclipseEndYear = 2027;
    private EclipseSeries? _currentEclipseSeries;
    private bool _hasEclipseSeries;

    #endregion

    #region Constructor

    /// <summary>
    /// Initialises the mundane view model and its nested
    /// <see cref="LocationSearch"/> child, seeded with the historical
    /// pre-H-1.3 default search text "Washington, DC".
    /// </summary>
    /// <param name="parent">The composing <see cref="MainViewModel"/>.</param>
    /// <param name="httpClient">
    /// Shared singleton <see cref="HttpClient"/> threaded through from the
    /// parent so the same instance is reused across every panel's location
    /// search.
    /// </param>
    /// <param name="rateLimiter">
    /// Shared <see cref="NominatimRateLimiter"/> enforcing the Nominatim
    /// 1-request-per-second policy globally across all four location-search
    /// children.
    /// </param>
    /// <param name="logger">Serilog logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if any argument is <see langword="null"/>.
    /// </exception>
    public MundaneViewModel(
        MainViewModel parent,
        HttpClient httpClient,
        NominatimRateLimiter rateLimiter,
        ILogger logger)
        : base(parent)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger.ForContext<MundaneViewModel>();

        // Compose the nested LocationSearch. Construct BEFORE subscribing its
        // PropertyChanged so the handler wiring stays local to the constructor.
        // Seed the search text with the historical pre-H-1.3 "Washington, DC"
        // default — the Mundane panel has always pre-populated this field.
        LocationSearch = new LocationSearchViewModel(
            parent, httpClient, rateLimiter, logger, "mundane location",
            initialSearchText: "Washington, DC");
        LocationSearch.PropertyChanged += OnLocationSearchPropertyChanged;
    }

    #endregion

    #region Nested Children

    /// <summary>
    /// Nested location-search child for the Mundane panel. Migrated from
    /// parent-owned sibling <c>MainViewModel.MundaneLocationSearch</c> under
    /// Session H-1.9 (second application of the H-1.6 nested-child play).
    /// Bindings previously on <c>MundaneLocationSearch.X</c> are now on
    /// <c>Mundane.LocationSearch.X</c>. Follow-on logic when the user picks
    /// a result lives in <see cref="OnLocationSearchPropertyChanged"/>.
    /// </summary>
    public LocationSearchViewModel LocationSearch { get; }

    #endregion

    #region Properties \u2014 Mundane Location

    /// <summary>Gets or sets the mundane chart location name.</summary>
    public string LocationName
    {
        get => _locationName;
        set => SetProperty(ref _locationName, value);
    }

    /// <summary>Gets or sets the mundane chart latitude as text.</summary>
    public string LatitudeText
    {
        get => _latitudeText;
        set => SetProperty(ref _latitudeText, value);
    }

    /// <summary>Gets or sets the mundane chart longitude as text.</summary>
    public string LongitudeText
    {
        get => _longitudeText;
        set => SetProperty(ref _longitudeText, value);
    }

    /// <summary>Gets or sets the mundane chart IANA timezone.</summary>
    public string TimeZone
    {
        get => _timeZone;
        set => SetProperty(ref _timeZone, value);
    }

    #endregion

    #region Properties \u2014 Ingress Charts

    /// <summary>
    /// Gets the list of planets available for ingress calculation. Covers the
    /// traditional nine (Sun plus Mercury through Pluto); the Moon, True Node,
    /// and Chiron are omitted because sign-ingress analysis is defined for
    /// slow-enough-to-be-mundane bodies only.
    /// </summary>
    public static IReadOnlyList<CelestialBodyId> AvailableIngressPlanets { get; } =
    [
        CelestialBodyId.Sun,
        CelestialBodyId.Mercury,
        CelestialBodyId.Venus,
        CelestialBodyId.Mars,
        CelestialBodyId.Jupiter,
        CelestialBodyId.Saturn,
        CelestialBodyId.Uranus,
        CelestialBodyId.Neptune,
        CelestialBodyId.Pluto,
    ];

    /// <summary>Gets or sets the planet for ingress search.</summary>
    public CelestialBodyId IngressPlanet
    {
        get => _ingressPlanet;
        set => SetProperty(ref _ingressPlanet, value);
    }

    /// <summary>Gets or sets the year for ingress search.</summary>
    public int IngressYear
    {
        get => _ingressYear;
        set => SetProperty(ref _ingressYear, value);
    }

    /// <summary>Gets or sets whether to show only cardinal ingresses.</summary>
    public bool CardinalOnly
    {
        get => _cardinalOnly;
        set => SetProperty(ref _cardinalOnly, value);
    }

    /// <summary>
    /// Gets or sets the most recent <see cref="Core.Models.IngressSeries"/>
    /// produced by <c>MainViewModel.FindIngressesAsync</c>. Changes to this
    /// property do not themselves flip <see cref="HasIngressSeries"/> \u2014 the
    /// parent command writes both explicitly to preserve the pre-extraction
    /// "empty-series zeroes the flag" semantics.
    /// </summary>
    public IngressSeries? CurrentIngressSeries
    {
        get => _currentIngressSeries;
        set => SetProperty(ref _currentIngressSeries, value);
    }

    /// <summary>
    /// Gets or sets the most recent cardinal-ingress chart batch produced by
    /// <c>MainViewModel.CalculateCardinalIngressesAsync</c>. Changes to this
    /// property do not themselves flip <see cref="HasCardinalIngresses"/>
    /// \u2014 the parent command writes both explicitly.
    /// </summary>
    public IReadOnlyList<IngressResult>? CurrentCardinalIngresses
    {
        get => _currentCardinalIngresses;
        set => SetProperty(ref _currentCardinalIngresses, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether ingress series results are
    /// available for export. Written explicitly by the parent command as
    /// <c>series.Count &gt; 0</c> so a scan that yields zero ingresses
    /// leaves the export command gated off even though
    /// <see cref="CurrentIngressSeries"/> is non-null. Setter fires
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent re-evaluates the ingress command states.
    /// </summary>
    public bool HasIngressSeries
    {
        get => _hasIngressSeries;
        set
        {
            if (SetProperty(ref _hasIngressSeries, value))
            {
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether cardinal-ingress charts are
    /// available for export. Written explicitly by the parent command as
    /// <c>results.Count &gt; 0</c>. Setter fires
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>.
    /// </summary>
    public bool HasCardinalIngresses
    {
        get => _hasCardinalIngresses;
        set
        {
            if (SetProperty(ref _hasCardinalIngresses, value))
            {
                RequestCommandStateRefresh();
            }
        }
    }

    #endregion

    #region Properties \u2014 Lunation Charts

    /// <summary>Gets or sets the year for lunation search.</summary>
    public int LunationYear
    {
        get => _lunationYear;
        set => SetProperty(ref _lunationYear, value);
    }

    /// <summary>
    /// Gets or sets the most recent <see cref="Core.Models.LunationSeries"/>
    /// produced by <c>MainViewModel.FindLunationsAsync</c> or
    /// <c>CalculateLunationChartsAsync</c>. Changes to this property do not
    /// themselves flip <see cref="HasLunationSeries"/> \u2014 the parent
    /// command writes both explicitly.
    /// </summary>
    public LunationSeries? CurrentLunationSeries
    {
        get => _currentLunationSeries;
        set => SetProperty(ref _currentLunationSeries, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether lunation series results are
    /// available for export. Written explicitly by the parent command as
    /// <c>series.Count &gt; 0</c>. Setter fires
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>.
    /// </summary>
    public bool HasLunationSeries
    {
        get => _hasLunationSeries;
        set
        {
            if (SetProperty(ref _hasLunationSeries, value))
            {
                RequestCommandStateRefresh();
            }
        }
    }

    #endregion

    #region Properties \u2014 Mundane Aspect Finder

    /// <summary>Gets or sets the first body for the mundane aspect search.</summary>
    public CelestialBodyId MABody1
    {
        get => _maBody1;
        set => SetProperty(ref _maBody1, value);
    }

    /// <summary>Gets or sets the second body for the mundane aspect search.</summary>
    public CelestialBodyId MABody2
    {
        get => _maBody2;
        set => SetProperty(ref _maBody2, value);
    }

    /// <summary>Gets or sets the harmonic mode for the mundane aspect search.</summary>
    public string MAHarmonicMode
    {
        get => _maHarmonicMode;
        set => SetProperty(ref _maHarmonicMode, value);
    }

    /// <summary>Gets or sets the start year for the mundane aspect search.</summary>
    public int MAStartYear
    {
        get => _maStartYear;
        set => SetProperty(ref _maStartYear, value);
    }

    /// <summary>Gets or sets the end year for the mundane aspect search.</summary>
    public int MAEndYear
    {
        get => _maEndYear;
        set => SetProperty(ref _maEndYear, value);
    }

    /// <summary>
    /// Gets or sets the most recent
    /// <see cref="Core.Models.MundaneAspectSeries"/> produced by
    /// <c>MainViewModel.FindMundaneAspectsAsync</c>. Changes to this property
    /// do not themselves flip <see cref="HasMASeries"/> \u2014 the parent
    /// command writes both explicitly.
    /// </summary>
    public MundaneAspectSeries? CurrentMASeries
    {
        get => _currentMASeries;
        set => SetProperty(ref _currentMASeries, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether mundane aspect series results
    /// are available for export. Written explicitly by the parent command as
    /// <c>series.Count &gt; 0</c>. Setter fires
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>.
    /// </summary>
    public bool HasMASeries
    {
        get => _hasMASeries;
        set
        {
            if (SetProperty(ref _hasMASeries, value))
            {
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>Available harmonic modes for the ComboBox.</summary>
    public IReadOnlyList<string> AvailableHarmonicModes { get; } =
    [
        "45\u00b0 Harmonic",
        "90\u00b0 Harmonic",
    ];

    /// <summary>Available planets for the mundane aspect ComboBoxes (all bodies except nodes).</summary>
    public IReadOnlyList<CelestialBodyId> AvailableMundanePlanets { get; } =
    [
        CelestialBodyId.Sun, CelestialBodyId.Moon,
        CelestialBodyId.Mercury, CelestialBodyId.Venus, CelestialBodyId.Mars,
        CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
        CelestialBodyId.Uranus, CelestialBodyId.Neptune, CelestialBodyId.Pluto,
        CelestialBodyId.Chiron,
    ];

    #endregion

    #region Properties \u2014 Eclipse Calculator

    /// <summary>Gets or sets the start year for eclipse search.</summary>
    public int EclipseStartYear
    {
        get => _eclipseStartYear;
        set => SetProperty(ref _eclipseStartYear, value);
    }

    /// <summary>Gets or sets the end year for eclipse search.</summary>
    public int EclipseEndYear
    {
        get => _eclipseEndYear;
        set => SetProperty(ref _eclipseEndYear, value);
    }

    /// <summary>
    /// Gets or sets the most recent <see cref="Core.Models.EclipseSeries"/>
    /// produced by <c>MainViewModel.FindEclipsesAsync</c>. Changes to this
    /// property do not themselves flip <see cref="HasEclipseSeries"/> \u2014
    /// the parent command writes both explicitly.
    /// </summary>
    public EclipseSeries? CurrentEclipseSeries
    {
        get => _currentEclipseSeries;
        set => SetProperty(ref _currentEclipseSeries, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether eclipse series results are
    /// available for export. Written explicitly by the parent command as
    /// <c>series.Count &gt; 0</c>. Setter fires
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>.
    /// </summary>
    public bool HasEclipseSeries
    {
        get => _hasEclipseSeries;
        set
        {
            if (SetProperty(ref _hasEclipseSeries, value))
            {
                RequestCommandStateRefresh();
            }
        }
    }

    #endregion

    #region Follow-on Handlers (migrated from parent per Rule #4 refinement)

    /// <summary>
    /// When the user picks a
    /// <see cref="LocationSearchViewModel.SelectedResult"/> from the nested
    /// mundane location search, copy the coordinates and display name into
    /// this VM's own form fields, resolve the IANA timezone, and update the
    /// parent-owned status text. Previously
    /// <c>MainViewModel.OnMundaneLocationSearchPropertyChanged</c>; moved
    /// under H-1.9 because every write targets <c>this.*</c> fields now
    /// that the mundane state is unified under this VM \u2014 the same
    /// Rule #4 refinement H-1.6 applied to the Electional panel.
    /// </summary>
    private void OnLocationSearchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LocationSearchViewModel.SelectedResult)) return;
        LocationSearchResult? value = LocationSearch.SelectedResult;
        if (value is null) return;

        LatitudeText = value.Latitude.ToString(CultureInfo.InvariantCulture);
        LongitudeText = value.Longitude.ToString(CultureInfo.InvariantCulture);
        LocationName = value.DisplayName;

        try
        {
            // The answer as offered, else its canonical zone; an answer the app does not
            // offer is left in the field for the user to correct.
            TimeZoneResult tzResult = TimeZoneLookup.GetTimeZone(value.Latitude, value.Longitude);
            if (!string.IsNullOrEmpty(tzResult.Result))
                TimeZone = TimezoneProvider.FindOfferedZone(tzResult.Result, Parent.TimeZoneRules) ?? tzResult.Result;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex,
                "Failed to determine timezone for {Lat}, {Lon}",
                value.Latitude, value.Longitude);
        }

        Parent.StatusText = $"Mundane location set: {value.DisplayName}";
    }

    #endregion

    #region Reset

    /// <summary>
    /// Resets every Mundane panel field to its default value, including the
    /// nested <see cref="LocationSearch"/>. Uses the field-bypass pattern
    /// from H-1.2's <c>ChartEntryViewModel.PopulateFrom</c> so a single
    /// <see cref="ChildViewModelBase.RequestCommandStateRefresh"/> fires at
    /// the end of the reset rather than cascading once per setter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The Mundane Location defaults to "Washington, DC" with its
    /// historical coordinates and <c>America/New_York</c> timezone; the
    /// five sub-feature result surfaces are nulled out and their
    /// <c>HasXxx</c> flags cleared. Year fields re-read
    /// <see cref="DateTime.Today"/> on reset so a user who holds the
    /// application open across midnight sees the year roll forward when
    /// they clear the form (F-GR-2 watch item).
    /// </para>
    /// <para>
    /// Not currently invoked from <c>MainViewModel.ClearForm</c> because the
    /// pre-extraction flow did not reset these mundane-panel fields on
    /// natal-chart clear; provided for future use and for symmetry with the
    /// other H-1 children that each expose a <c>Clear()</c> method.
    /// </para>
    /// </remarks>
    public void Clear()
    {
        // Mundane Location
        _locationName = "Washington, DC";
        OnPropertyChanged(nameof(LocationName));

        _latitudeText = "38.8977";
        OnPropertyChanged(nameof(LatitudeText));

        _longitudeText = "-77.0365";
        OnPropertyChanged(nameof(LongitudeText));

        _timeZone = "America/New_York";
        OnPropertyChanged(nameof(TimeZone));

        // Ingress Charts
        _ingressYear = DateTime.Today.Year;
        OnPropertyChanged(nameof(IngressYear));

        _ingressPlanet = CelestialBodyId.Sun;
        OnPropertyChanged(nameof(IngressPlanet));

        _cardinalOnly = true;
        OnPropertyChanged(nameof(CardinalOnly));

        _currentIngressSeries = null;
        OnPropertyChanged(nameof(CurrentIngressSeries));

        _currentCardinalIngresses = null;
        OnPropertyChanged(nameof(CurrentCardinalIngresses));

        _hasIngressSeries = false;
        OnPropertyChanged(nameof(HasIngressSeries));

        _hasCardinalIngresses = false;
        OnPropertyChanged(nameof(HasCardinalIngresses));

        // Lunation Charts
        _lunationYear = DateTime.Today.Year;
        OnPropertyChanged(nameof(LunationYear));

        _currentLunationSeries = null;
        OnPropertyChanged(nameof(CurrentLunationSeries));

        _hasLunationSeries = false;
        OnPropertyChanged(nameof(HasLunationSeries));

        // Mundane Aspect Finder
        _maBody1 = CelestialBodyId.Saturn;
        OnPropertyChanged(nameof(MABody1));

        _maBody2 = CelestialBodyId.Mars;
        OnPropertyChanged(nameof(MABody2));

        _maHarmonicMode = "90\u00b0 Harmonic";
        OnPropertyChanged(nameof(MAHarmonicMode));

        _maStartYear = 2025;
        OnPropertyChanged(nameof(MAStartYear));

        _maEndYear = 2035;
        OnPropertyChanged(nameof(MAEndYear));

        _currentMASeries = null;
        OnPropertyChanged(nameof(CurrentMASeries));

        _hasMASeries = false;
        OnPropertyChanged(nameof(HasMASeries));

        // Eclipse Calculator
        _eclipseStartYear = 2025;
        OnPropertyChanged(nameof(EclipseStartYear));

        _eclipseEndYear = 2027;
        OnPropertyChanged(nameof(EclipseEndYear));

        _currentEclipseSeries = null;
        OnPropertyChanged(nameof(CurrentEclipseSeries));

        _hasEclipseSeries = false;
        OnPropertyChanged(nameof(HasEclipseSeries));

        // Clear the nested location search too \u2014 matches the H-1.6
        // Electional precedent.
        LocationSearch.Clear();

        RequestCommandStateRefresh();
    }

    #endregion
}
