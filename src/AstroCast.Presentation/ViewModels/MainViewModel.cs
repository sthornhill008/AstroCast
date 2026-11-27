using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;

using GeoTimeZone;
using Serilog;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;
using AstroCast.Core.Services.PrimaryDirections;
using AstroCast.Core.Utilities;
using AstroCast.Services;

namespace AstroCast.ViewModels;

/// <summary>
/// Main view model for the AstroCast WPF data entry form.
/// Manages chart input fields, calculation, database persistence,
/// and PDF/Markdown export. Implements INotifyPropertyChanged for
/// two-way data binding.
/// </summary>
/// <remarks>
/// This view model owns the full lifecycle of a chart entry workflow:
/// form input → calculate → save → export. It uses the ChartEngine for
/// calculations, IChartRepository for persistence, and the Core formatters
/// for file export.
/// </remarks>
public sealed class MainViewModel : INotifyPropertyChanged
{
    #region Fields

    private readonly IChartEngine _chartEngine;
    private readonly IChartRepository _repository;
    private readonly IEphemerisService _ephemeris;
    private readonly ISynastryService _synastryService;
    private readonly ICompositeService _compositeService;
    private readonly ISolarReturnService _solarReturnService;
    private readonly ILunarReturnService _lunarReturnService;
    private readonly IPlanetaryReturnService _planetaryReturnService;
    private readonly IPlanetaryCycleTimelineService _cycleTimelineService;
    private readonly IGreatConjunctionService _greatConjunctionService;
    private readonly IIngressService _ingressService;
    private readonly ILunationService _lunationService;
    private readonly IRelocationService _relocationService;
    private readonly IMundaneAspectService _mundaneAspectService;
    private readonly IEclipseService _eclipseService;
    private readonly ISynodicCycleService _synodicCycleService;
    private readonly IReturnIngressTimelineService _returnIngressTimelineService;
    private readonly IDialogService _dialogService;
    private readonly OperationCancellationRegistry _cancelRegistry;
    private readonly ILogger _logger;
    private readonly IUiDispatcher? _uiDispatcher;
    private readonly IFileDialogService _fileDialogService;
    private readonly ITransitService _transitService;
    private readonly IElectionalService _electionalService;
    private readonly IAstrocartographyService _astrocartographyService;
    private readonly IPrimaryDirectionService _primaryDirectionService;

    /// <summary>The Davison chart's service (branch DavisonPanel, 1.7.0): a real chart at the midpoint moment and place.</summary>
    private readonly IDavisonService _davisonService;

    private string _statusText = "Ready";
    private bool _hasResult;
    private ChartResult? _lastResult;
    private string _searchText = string.Empty;
    private string _filterEventType = string.Empty;
    private DateTime? _filterDateFrom;
    private DateTime? _filterDateTo;
    private ChartSortField _sortField = ChartSortField.Name;
    private bool _sortAscending = true;
    private string _timezoneFilterText = string.Empty;
    private readonly ObservableRangeCollection<string> _filteredTimezones;
    private string _medicalHealthContext = string.Empty;

    private bool _isCalculating;
    private NavigationPanel _activePanel = NavigationPanel.Chart;
    private RelayCommand<NavigationPanel>? _navigateToPanelCommand;

    /// <summary>
    /// Static HttpClient instance for Nominatim geocoding API calls.
    /// Per Microsoft guidelines, HttpClient should be used as a singleton.
    /// Shared with every <see cref="LocationSearchViewModel"/> composed
    /// on this parent (H-1.3).
    /// </summary>
    private static readonly HttpClient s_httpClient = CreateHttpClient();

    /// <summary>
    /// Shared rate-limit gate passed to every
    /// <see cref="LocationSearchViewModel"/> so that the Nominatim
    /// 1-request-per-second usage policy is honoured globally across
    /// all four panels, not just within a single panel (H-1.3 —
    /// previously a <c>DateTime _lastGeocodingRequest</c> field).
    /// </summary>
    private readonly NominatimRateLimiter _nominatimRateLimiter = new();

    #endregion

    #region Constructor

    /// <summary>
    /// Initialises a new instance of <see cref="MainViewModel"/>.
    /// </summary>
    /// <param name="chartEngine">Chart calculation engine.</param>
    /// <param name="repository">Chart persistence repository.</param>
    /// <param name="ephemeris">Ephemeris service for ChartMapper conversions.</param>
    /// <param name="synastryService">Synastry calculation service.</param>
    /// <param name="compositeService">Composite chart calculation service.</param>
    /// <param name="solarReturnService">Solar return calculation service.</param>
    /// <param name="lunarReturnService">Lunar return calculation service.</param>
    /// <param name="planetaryReturnService">Planetary return calculation service.</param>
    /// <param name="cycleTimelineService">Planetary cycle timeline service.</param>
    /// <param name="greatConjunctionService">Great conjunction service.</param>
    /// <param name="ingressService">Ingress calculation service.</param>
    /// <param name="lunationService">Lunation calculation service.</param>
    /// <param name="relocationService">Relocation service.</param>
    /// <param name="mundaneAspectService">Mundane aspect service.</param>
    /// <param name="eclipseService">Eclipse service.</param>
    /// <param name="returnIngressTimelineService">Return and ingress timeline service.</param>
    /// <param name="synodicCycleService">Synodic cycle service.</param>
    /// <param name="transitService">Transit calculation service.</param>
    /// <param name="electionalService">Electional (best-time) calculation service.</param>
    /// <param name="astrocartographyService">Astrocartography (ACG) calculation service.</param>
    /// <param name="dialogService">Dialog service for user confirmations.</param>
    /// <param name="cancelRegistry">
    /// Centralised cancellation registry that owns the
    /// <see cref="CancellationTokenSource"/> lifecycle for every long-running
    /// view-model operation. Injected as a singleton from <c>App.xaml.cs</c>.
    /// </param>
    /// <param name="logger">Serilog logger for diagnostic output.</param>
    /// <param name="uiDispatcher">
    /// Optional UI-thread marshaller used by <see cref="OnPropertyChanged"/> and,
    /// via <see cref="UiDispatcher"/>, by every <see cref="ChildViewModelBase"/>.
    /// Supplied by <c>App.xaml.cs</c> as <c>WpfUiDispatcher</c> in the running
    /// application. <see langword="null"/> means "no UI thread — raise change
    /// notifications inline", which is the headless/unit-test contract described
    /// on <see cref="IUiDispatcher"/>; the parameter is optional precisely so that
    /// test construction sites need no dispatcher. See H-2.
    /// </param>
    /// <param name="fileDialogService">
    /// Optional file-picker used by every export and import command. Supplied by
    /// <c>App.xaml.cs</c> as <c>WpfFileDialogService</c> in the running
    /// application. <see langword="null"/> means "no file picker is available"
    /// and substitutes <see cref="NullFileDialogService"/>, in which case each
    /// command takes the same path as a cancelled dialog and writes nothing; the
    /// parameter is optional so that test construction sites need no picker. No
    /// test exercises an export or import command. The stored field is
    /// non-nullable, so the call sites can <c>await</c> it directly.
    /// </param>
    /// <param name="timeZoneRules">
    /// Optional time-zone rules the form converts with: the live offset label, a new
    /// chart's default zone, "Set to now" and the zones the place lookups fill in.
    /// <see langword="null"/> means the IANA data the app carries
    /// (<see cref="TzdbTimeZoneRules"/>), the data every service uses by default, so the
    /// heads need not supply it.
    /// </param>
    /// <param name="primaryDirectionService">
    /// Optional primary-direction engine the Directions panel calculates with (branch PrimaryPanel, 1.5.0).
    /// <see langword="null"/> means a <see cref="PrimaryDirectionService"/> over <paramref name="ephemeris"/>, as a
    /// null <paramref name="timeZoneRules"/> means the default rules, so the test helpers that construct the view
    /// model need not supply it; both heads pass the one they build beside the transit service.
    /// </param>
    public MainViewModel(
        IChartEngine chartEngine,
        IChartRepository repository,
        IEphemerisService ephemeris,
        ISynastryService synastryService,
        ICompositeService compositeService,
        ISolarReturnService solarReturnService,
        ILunarReturnService lunarReturnService,
        IPlanetaryReturnService planetaryReturnService,
        IPlanetaryCycleTimelineService cycleTimelineService,
        IGreatConjunctionService greatConjunctionService,
        IIngressService ingressService,
        ILunationService lunationService,
        IRelocationService relocationService,
        IMundaneAspectService mundaneAspectService,
        IEclipseService eclipseService,
        IReturnIngressTimelineService returnIngressTimelineService,
        ISynodicCycleService synodicCycleService,
        ITransitService transitService,
        IElectionalService electionalService,
        IAstrocartographyService astrocartographyService,
        IDialogService dialogService,
        OperationCancellationRegistry cancelRegistry,
        ILogger logger,
        IUiDispatcher? uiDispatcher = null,
        IFileDialogService? fileDialogService = null,
        ITimeZoneRules? timeZoneRules = null,
        IPrimaryDirectionService? primaryDirectionService = null,
        IDavisonService? davisonService = null)
    {
        ArgumentNullException.ThrowIfNull(chartEngine);
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(ephemeris);
        ArgumentNullException.ThrowIfNull(synastryService);
        ArgumentNullException.ThrowIfNull(compositeService);
        ArgumentNullException.ThrowIfNull(solarReturnService);
        ArgumentNullException.ThrowIfNull(lunarReturnService);
        ArgumentNullException.ThrowIfNull(planetaryReturnService);
        ArgumentNullException.ThrowIfNull(cycleTimelineService);
        ArgumentNullException.ThrowIfNull(greatConjunctionService);
        ArgumentNullException.ThrowIfNull(ingressService);
        ArgumentNullException.ThrowIfNull(lunationService);
        ArgumentNullException.ThrowIfNull(relocationService);
        ArgumentNullException.ThrowIfNull(mundaneAspectService);
        ArgumentNullException.ThrowIfNull(eclipseService);
        ArgumentNullException.ThrowIfNull(returnIngressTimelineService);
        ArgumentNullException.ThrowIfNull(synodicCycleService);
        ArgumentNullException.ThrowIfNull(transitService);
        ArgumentNullException.ThrowIfNull(electionalService);
        ArgumentNullException.ThrowIfNull(astrocartographyService);
        ArgumentNullException.ThrowIfNull(dialogService);
        ArgumentNullException.ThrowIfNull(cancelRegistry);
        ArgumentNullException.ThrowIfNull(logger);

        _chartEngine = chartEngine;
        _repository = repository;
        _ephemeris = ephemeris;
        _synastryService = synastryService;
        _compositeService = compositeService;
        _solarReturnService = solarReturnService;
        _lunarReturnService = lunarReturnService;
        _planetaryReturnService = planetaryReturnService;
        _cycleTimelineService = cycleTimelineService;
        _greatConjunctionService = greatConjunctionService;
        _ingressService = ingressService;
        _lunationService = lunationService;
        _relocationService = relocationService;
        _mundaneAspectService = mundaneAspectService;
        _eclipseService = eclipseService;
        _returnIngressTimelineService = returnIngressTimelineService;
        _synodicCycleService = synodicCycleService;
        _dialogService = dialogService;
        _cancelRegistry = cancelRegistry;
        _logger = logger.ForContext<MainViewModel>();
        TimeZoneRules = timeZoneRules ?? new TzdbTimeZoneRules(logger);

        // H-2 UI-thread seam. Assigned before the child view models are
        // constructed below, because ChildViewModelBase reads it through
        // UiDispatcher the first time any child raises PropertyChanged.
        _uiDispatcher = uiDispatcher;

        // File-picker seam for the export/import commands. A null argument
        // substitutes the null object, so a missing picker still yields the
        // cancelled-dialog path — but the field stays non-nullable, which is what
        // lets the 26 call sites await it directly. A null-conditional await
        // would compile and then throw NullReferenceException at runtime.
        _fileDialogService = fileDialogService ?? NullFileDialogService.Instance;
        _transitService = transitService;
        _electionalService = electionalService;
        _astrocartographyService = astrocartographyService;
        _primaryDirectionService = primaryDirectionService ?? new PrimaryDirectionService(ephemeris);
        _davisonService = davisonService ?? new DavisonService(chartEngine, logger);

        // Initialise filtered timezone view for the Chart Entry panel's
        // substring search. The Electional panel's equivalent lives on the
        // Electional child since H-1.6 — construction is deferred to the
        // child's constructor below. No filter text yet, so the collection
        // starts as the full source list. This is the ONE instance the
        // ComboBox binds for the lifetime of this view model; refreshes
        // mutate it in place (see RefreshFilteredTimezones).
        _filteredTimezones = new ObservableRangeCollection<string>(AvailableTimezones);

        // Chart Entry — H-1.2. First real decomposition child (replaces the
        // H-1.1 scaffolding probe). Initialise BEFORE commands so lambdas
        // that reference ChartEntry.* can flow the non-null state through the
        // compiler. Name / TimeZone / LocationName setters gate CanCalculate,
        // so route the child's command-refresh signal to the parent-owned
        // calculate-side refresh. Listen for TimeZone changes separately so
        // the parent can re-apply the substring filter on its timezone list
        // without the child reaching into parent-private filter state.
        ChartEntry = new ChartEntryViewModel(this);
        ChartEntry.CommandStateRefreshRequested += (_, _) => RefreshCommandStates();
        ChartEntry.PropertyChanged += OnChartEntryPropertyChanged;

        // Wire per-panel location-search children (H-1.3). Initialise BEFORE
        // commands so lambdas referencing the children can flow the non-null
        // state through the compiler. The shared HttpClient and rate limiter
        // honour Nominatim's 1-request-per-second policy globally across all
        // four panels; each child owns its own per-instance lock registered
        // via IUiDispatcher.EnableCollectionSynchronization inside its
        // constructor. Follow-on logic when a result is selected (writing
        // coordinates into the form, resolving the IANA timezone, refreshing
        // command states) stays on this parent per cross-VM rule #4 for the
        // one remaining sibling (ChartEntryLocationSearch) — the handler
        // below fires from its PropertyChanged stream on SelectedResult
        // changes. The Electional, Mundane, and Relocation location-search
        // instances are composed inside their owning children (H-1.6 / H-1.9
        // / H-1.11 nested-child migrations) and their follow-on handlers now
        // live inside those children per the Rule #4 refinement (intra-VM
        // writes to this.* fields are not cross-VM sibling writes).
        ChartEntryLocationSearch = new LocationSearchViewModel(
            this, s_httpClient, _nominatimRateLimiter, _logger, "location");

        ChartEntryLocationSearch.PropertyChanged += OnChartEntryLocationSearchPropertyChanged;

        // Synastry & Composite — H-1.4. Owns Person B selection, cached Person
        // B chart, relationship metadata, and the two result records plus the
        // composite interpretation context. Initialised BEFORE commands so
        // lambdas that reference Synastry.* can flow the non-null state
        // through the compiler. A single CommandStateRefreshRequested handler
        // routes to both refreshers because every Synastry change affects
        // Composite's CanExecute (shared Person B plumbing) and vice versa.
        Synastry = new SynastryViewModel(this);
        Synastry.CommandStateRefreshRequested += (_, _) =>
        {
            RefreshSynastryCommandStates();
            RefreshCompositeCommandStates();
        };

        // Transits — H-1.5. Owns the report date range, the four technique
        // toggles and the minor-aspect toggle, free-text interpretation
        // context, and the current TransitResult. Initialised BEFORE commands
        // so the command lambdas at lines below can reference
        // Transits.HasResult through the non-null compiler flow.
        // CommandStateRefreshRequested routes to the transit refresher, which
        // raises Calculate as well as the three exports: since 1.4.0 (branch
        // ModesPanel) Calculate needs a technique checked as well as the
        // parent-owned HasResult (natal chart), and since 1.5.0 (branch
        // PrimaryPanel) the Directions panel's settings valid while the
        // Directions box is checked.
        Transits = new TransitsViewModel(this);
        Transits.CommandStateRefreshRequested += (_, _) => RefreshTransitCommandStates();

        // Electional — H-1.6. Sixth decomposition child and the FIRST to
        // compose a nested LocationSearchViewModel (previously the sibling
        // ElectionalLocationSearch). Initialised BEFORE commands so the
        // command lambdas below can reference Electional.* through the
        // non-null compiler flow. The follow-on handler
        // OnElectionalLocationSearchPropertyChanged migrated into the child
        // per the Rule #4 refinement (intra-VM writes to this.* fields are
        // not cross-VM sibling writes). The shared HttpClient and rate limiter
        // continue to flow through so the global Nominatim 1-per-second
        // policy is honoured.
        Electional = new ElectionalViewModel(
            this, s_httpClient, _nominatimRateLimiter, _logger);
        Electional.CommandStateRefreshRequested += (_, _) => RefreshElectionalCommandStates();

        // Returns — H-1.7. Seventh decomposition child and the second multi-
        // result child (after H-1.4 Synastry & Composite). Owns Solar / Lunar /
        // Planetary Return inputs and results plus the absorbed profection
        // display surface (ProfectionSummary, HasProfection, FirdariaSummary,
        // ZodiacalReleasingSummary) — Category C in the H-1.5 §1A audit, with
        // internal-set accessors written by this parent's chart-lifecycle
        // methods (CalculateAndSaveAsync, LoadChartAsync). Initialised BEFORE
        // commands so the lambdas at lines below can reference Returns.* through
        // the non-null compiler flow. A single CommandStateRefreshRequested
        // handler routes to all three per-domain refreshers because every
        // result-property change can affect any of the six return commands; the
        // matching pattern lives on H-1.4 Synastry whose handler likewise fans
        // out to two refreshers.
        Returns = new ReturnsViewModel(this);
        Returns.CommandStateRefreshRequested += (_, _) =>
        {
            RefreshSolarReturnCommandStates();
            RefreshLunarReturnCommandStates();
            RefreshPlanetaryReturnCommandStates();
        };

        // Cycles — H-1.8. Eighth decomposition child and the third multi-
        // result child (after H-1.4 Synastry/Composite and H-1.7 Returns).
        // Owns Cycle Timeline, Great Conjunctions, and Synodic Cycles inputs
        // and results. Initialised BEFORE commands so the lambdas below can
        // reference Cycles.* through the non-null compiler flow. A single
        // CommandStateRefreshRequested handler routes to all three per-domain
        // refreshers because every result-property change can affect any of
        // the six cycle commands; the matching pattern lives on H-1.4
        // Synastry and H-1.7 Returns whose handlers likewise fan out. The
        // cycle-timeline command on this parent additionally reads the four
        // Planetary-Return inputs on Returns (planet / mode / year / number)
        // via Parent mediation — Rule #2 last-resort shared-state read,
        // justified by the §1A audit (Outcome A).
        Cycles = new CyclesViewModel(this);
        Cycles.CommandStateRefreshRequested += (_, _) =>
        {
            RefreshCycleTimelineCommandStates();
            RefreshGCCommandStates();
            RefreshSynodicCycleCommandStates();
        };

        // Mundane — H-1.9. Ninth decomposition child and the SECOND to compose
        // a nested LocationSearchViewModel (after H-1.6 Electional). Owns the
        // Mundane panel's full data surface: mundane location + Ingress Charts
        // + Lunation Charts + Mundane Aspect Finder + Eclipse Calculator.
        // Initialised BEFORE commands so the lambdas below can reference
        // Mundane.* through the non-null compiler flow. The follow-on handler
        // OnMundaneLocationSearchPropertyChanged migrated into the child per
        // the Rule #4 refinement (intra-VM writes to this.* fields are not
        // cross-VM sibling writes), matching the H-1.6 Electional precedent.
        // A single CommandStateRefreshRequested handler fans out to all four
        // per-domain refreshers (Ingress / Lunation / MA / Eclipse) because
        // any HasX flip can affect any of those four command groups. The
        // shared HttpClient and rate limiter flow through so the global
        // Nominatim 1-per-second policy is honoured across every panel.
        Mundane = new MundaneViewModel(
            this, s_httpClient, _nominatimRateLimiter, _logger);
        Mundane.CommandStateRefreshRequested += (_, _) =>
        {
            RefreshIngressCommandStates();
            RefreshLunationCommandStates();
            RefreshMACommandStates();
            RefreshEclipseCommandStates();
        };

        // Return & Ingress Timeline child (H-1.10). Fully independent feature
        // — its CalculateRITimeline command reads parent-owned LastResult (the
        // natal chart) plus four child-owned inputs, with no sibling VM reads.
        // Child-owned HasResult flip fans out to the R/I Timeline command
        // states so the export command re-evaluates after a calculation.
        ReturnIngressTimeline = new ReturnIngressTimelineViewModel(this);
        ReturnIngressTimeline.CommandStateRefreshRequested += (_, _) =>
        {
            RefreshRITimelineCommandStates();
        };

        // Relocation — H-1.11. Eleventh decomposition child and the THIRD to
        // compose a nested LocationSearchViewModel (after H-1.6 Electional and
        // H-1.9 Mundane) — completes the sibling-to-nested migration for every
        // location-search child except ChartEntry. Owns the Relocation panel's
        // data surface: SelectedNatalChart + LocationName + LatitudeText +
        // LongitudeText + CurrentResult (derived HasResult per Audit Q2).
        // Initialised BEFORE commands so the lambdas below can reference
        // Relocation.* through the non-null compiler flow. The follow-on
        // handler OnRelocationLocationSearchPropertyChanged migrated into the
        // child per the Rule #4 refinement (intra-VM writes to this.* fields
        // are not cross-VM sibling writes). F-CS-Relocation closed inside the
        // child's handler: coordinates now formatted with
        // CultureInfo.InvariantCulture; matching CalculateRelocationAsync
        // parse path below also threads InvariantCulture through the
        // double.TryParse pair. Shared HttpClient and rate limiter flow
        // through so the global Nominatim 1-per-second policy is honoured.
        Relocation = new RelocationViewModel(
            this, s_httpClient, _nominatimRateLimiter, _logger);
        Relocation.CommandStateRefreshRequested += (_, _) =>
        {
            RefreshRelocationCommandStates();
        };

        // Directions (branch PrimaryPanel, 1.5.0). Owns the Directions panel's four settings and its
        // result; the two commands stay here (rule #4), reading LastResult and the primary-direction
        // service. Initialised BEFORE commands so the lambdas below can reference Directions.* through
        // the non-null compiler flow. Its refresh raises the panel's two commands
        // and the Transits panel's, whose Calculate reads these settings while
        // its Directions box is checked.
        Directions = new DirectionsViewModel(this);
        Directions.CommandStateRefreshRequested += (_, _) =>
        {
            RefreshDirectionsCommandStates();
            RefreshTransitCommandStates();
        };

        // Astrocartography panel child (H-1.12 \u2014 twelfth and final H-1
        // decomposition extraction). Owns the ACG panel's data surface: the
        // 12 planet toggles, 4 line-type toggles, CurrentResult + HasResult
        // (value-based per Audit Q2, preserving the pre-extraction
        // two-step write shape used by CalculateAcgAsync), CrossingReportText,
        // and the derived VisibleLines projection. No nested children and no
        // services \u2014 all three ACG commands and UpdateCrossingReport
        // stay on this parent per Rule #4 because they read
        // IAstrocartographyService. The floating AcgMapWindow lifecycle also
        // stays on the View layer (MainWindow.xaml.cs) \u2014 Outcome A of
        // Audit Q3. Initialised BEFORE commands so the CanExecute lambdas
        // below can reference Astrocartography.HasResult through the
        // non-null compiler flow. After H-1.12, MainViewModel owns services
        // + commands + chart lifecycle + cross-VM coordination only; every
        // user-visible panel is rooted in an owning child VM.
        Astrocartography = new AstrocartographyViewModel(this);
        Astrocartography.CommandStateRefreshRequested += (_, _) =>
        {
            RefreshAcgCommandStates();
        };

        // Initialise commands
        CalculateAndSaveCommand = new AsyncRelayCommand(CalculateAndSaveAsync, _ => CanCalculate());
        ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync, _ => HasResult);
        ExportMarkdownCommand = new AsyncRelayCommand(ExportMarkdownAsync, _ => HasResult);
        ExportChartJsonCommand = new AsyncRelayCommand(ExportChartJsonAsync, _ => HasResult);
        ExportBothCommand = new AsyncRelayCommand(ExportBothAsync, _ => HasResult);
        LoadChartCommand = new AsyncRelayCommand(LoadChartAsync);
        DeleteChartCommand = new AsyncRelayCommand(DeleteChartAsync);
        SearchChartsCommand = new AsyncRelayCommand(SearchChartsAsync);
        LookupLocationCommand = new AsyncRelayCommand(_ => ChartEntryLocationSearch.SearchAsync());
        ClearFormCommand = new RelayCommand(_ => ClearForm());
        NowCommand = new RelayCommand(_ => SetNow());
        SetToNowCommand = new RelayCommand(_ => SetNow());
        CalculateSynastryCommand = new AsyncRelayCommand(
            CalculateSynastryAsync, _ => HasResult && Synastry.PersonB is not null);
        ExportSynastryMarkdownCommand = new AsyncRelayCommand(
            ExportSynastryMarkdownAsync, _ => Synastry.HasSynastryResult);
        CalculateTransitCommand = new AsyncRelayCommand(
            CalculateTransitAsync,
            _ => HasResult && Transits.HasTechnique
                && (!Transits.IncludePrimaryDirections || Directions.HasValidSettings));
        ExportTransitMarkdownCommand = new AsyncRelayCommand(
            ExportTransitMarkdownAsync, _ => Transits.HasResult);
        // The daily digest and the timeline are made of transits, so a result
        // without them (solar arcs or progressions alone) has neither
        ExportDailyHoroscopeMarkdownCommand = new AsyncRelayCommand(
            ExportDailyHoroscopeMarkdownAsync, _ => Transits.ResultIncludesTransits);
        ExportTransitTimelineMarkdownCommand = new AsyncRelayCommand(
            ExportTransitTimelineMarkdownAsync, _ => Transits.ResultIncludesTransits);
        CalculateElectionalCommand = new AsyncRelayCommand(
            CalculateElectionalAsync,
            _ => (CanCalculate() || Electional.SelectedNatalChart is not null)
                && !string.IsNullOrWhiteSpace(Electional.Goal)
                && !string.IsNullOrWhiteSpace(Electional.LocationName)
                && !string.IsNullOrWhiteSpace(Electional.TimeZone));
        ExportElectionalMarkdownCommand = new AsyncRelayCommand(
            ExportElectionalMarkdownAsync, _ => Electional.HasResult);
        LookupElectionalLocationCommand = new AsyncRelayCommand(_ => Electional.LocationSearch.SearchAsync());
        UseNatalLocationCommand = new RelayCommand(
            _ => UseNatalLocation(),
            _ => !string.IsNullOrWhiteSpace(ChartEntry.LocationName) || Electional.SelectedNatalChart is not null);
        CalculateCompositeCommand = new AsyncRelayCommand(
            CalculateCompositeAsync, _ => HasResult && Synastry.PersonB is not null);
        ExportCompositeMarkdownCommand = new AsyncRelayCommand(
            ExportCompositeMarkdownAsync, _ => Synastry.HasCompositeResult);
        CalculateDavisonCommand = new AsyncRelayCommand(
            CalculateDavisonAsync, _ => HasResult && Synastry.PersonB is not null);
        ExportDavisonMarkdownCommand = new AsyncRelayCommand(
            ExportDavisonMarkdownAsync, _ => Synastry.HasDavisonResult);
        CalculateSolarReturnCommand = new AsyncRelayCommand(
            CalculateSolarReturnAsync, _ => HasResult);
        ExportSolarReturnMarkdownCommand = new AsyncRelayCommand(
            ExportSolarReturnMarkdownAsync, _ => Returns.HasSolarReturnResult);
        CalculateLunarReturnCommand = new AsyncRelayCommand(
            CalculateLunarReturnAsync, _ => HasResult);
        ExportLunarReturnMarkdownCommand = new AsyncRelayCommand(
            ExportLunarReturnMarkdownAsync, _ => Returns.HasLunarReturnResult);
        CalculatePlanetaryReturnCommand = new AsyncRelayCommand(
            CalculatePlanetaryReturnAsync, _ => HasResult);
        ExportPlanetaryReturnMarkdownCommand = new AsyncRelayCommand(
            ExportPlanetaryReturnMarkdownAsync, _ => Returns.HasPlanetaryReturnResult);
        CalculateCycleTimelineCommand = new AsyncRelayCommand(
            CalculateCycleTimelineAsync, _ => HasResult);
        ExportCycleTimelineMarkdownCommand = new AsyncRelayCommand(
            ExportCycleTimelineMarkdownAsync, _ => Cycles.HasCycleTimeline);
        FindGreatConjunctionsCommand = new AsyncRelayCommand(FindGreatConjunctionsAsync);
        ExportGCSeriesMarkdownCommand = new AsyncRelayCommand(
            ExportGCSeriesMarkdownAsync, _ => Cycles.HasGCSeries);
        FindSynodicCyclesCommand = new AsyncRelayCommand(FindSynodicCyclesAsync);
        ExportSynodicCycleMdCommand = new AsyncRelayCommand(
            ExportSynodicCycleMdAsync, _ => Cycles.HasSynodicCycleSeries);
        LookupMundaneLocationCommand = new AsyncRelayCommand(_ => Mundane.LocationSearch.SearchAsync());
        FindIngressesCommand = new AsyncRelayCommand(FindIngressesAsync);
        CalculateCardinalIngressesCommand = new AsyncRelayCommand(CalculateCardinalIngressesAsync);
        ExportIngressSeriesMarkdownCommand = new AsyncRelayCommand(
            ExportIngressSeriesMarkdownAsync, _ => Mundane.HasIngressSeries);
        ExportCardinalIngressesMarkdownCommand = new AsyncRelayCommand(
            ExportCardinalIngressesMarkdownAsync, _ => Mundane.HasCardinalIngresses);
        FindLunationsCommand = new AsyncRelayCommand(FindLunationsAsync);
        CalculateLunationChartsCommand = new AsyncRelayCommand(CalculateLunationChartsAsync);
        ExportLunationSeriesCommand = new AsyncRelayCommand(
            ExportLunationSeriesAsync, _ => Mundane.HasLunationSeries);
        FindMundaneAspectsCommand = new AsyncRelayCommand(FindMundaneAspectsAsync);
        ExportMASeriesCommand = new AsyncRelayCommand(
            ExportMASeriesAsync, _ => Mundane.HasMASeries);
        FindEclipsesCommand = new AsyncRelayCommand(FindEclipsesAsync);
        ExportEclipseSeriesCommand = new AsyncRelayCommand(
            ExportEclipseSeriesAsync, _ => Mundane.HasEclipseSeries);
        CalculateRITimelineCommand = new AsyncRelayCommand(
            CalculateRITimelineAsync, _ => HasResult);
        ExportRITimelineCommand = new AsyncRelayCommand(
            ExportRITimelineAsync, _ => ReturnIngressTimeline.HasResult);
        CalculateRelocationCommand = new AsyncRelayCommand(
            CalculateRelocationAsync,
            _ => (HasResult || Relocation.SelectedNatalChart is not null)
                && !string.IsNullOrWhiteSpace(Relocation.LocationName)
                && Relocation.HasValidCoordinates);
        ExportRelocationMarkdownCommand = new AsyncRelayCommand(
            ExportRelocationMarkdownAsync, _ => Relocation.HasResult);
        CalculateDirectionsCommand = new AsyncRelayCommand(
            CalculateDirectionsAsync, _ => HasResult && Directions.HasValidSettings);
        ExportDirectionsMarkdownCommand = new AsyncRelayCommand(
            ExportDirectionsMarkdownAsync, _ => Directions.HasResult);
        ExportMedicalMarkdownCommand = new AsyncRelayCommand(
            ExportMedicalMarkdownAsync, _ => HasResult);
        LookupRelocationLocationCommand = new AsyncRelayCommand(_ => Relocation.LocationSearch.SearchAsync());
        CalculateAcgCommand = new AsyncRelayCommand(
            CalculateAcgAsync, _ => HasResult);
        ExportAcgMarkdownCommand = new AsyncRelayCommand(
            ExportAcgMarkdownAsync, _ => Astrocartography.HasResult);
        OpenAcgMapWindowCommand = new RelayCommand(
            _ => OpenAcgMapWindowAction?.Invoke(),
            _ => Astrocartography.HasResult);
        OpenAboutWindowCommand = new RelayCommand(_ => OpenAboutWindowAction?.Invoke());
        CancelCommand = new RelayCommand(
            param => CancelCalculation(param as string),
            param => CanCancel(param as string));
        ExportDatabaseCommand = new AsyncRelayCommand(ExportDatabaseAsync);
        ImportDatabaseCommand = new AsyncRelayCommand(ImportDatabaseAsync);
        ToggleSortDirectionCommand = new AsyncRelayCommand(ToggleSortDirectionAsync);

        SavedCharts = new ObservableCollection<ChartEntity>();
        AllSavedCharts = new ObservableCollection<ChartEntity>();

        // Register every bound ObservableCollection for cross-thread access
        // (H-2). This lets the binding layer lock-read the collection when it
        // fires CollectionChanged on a non-UI thread, and is the
        // defence-in-depth guard behind the marshalling we added to
        // OnPropertyChanged. Each collection has its own lock object so
        // concurrent modifications to unrelated collections do not serialise
        // needlessly. H-1.3 moved the per-panel location-search collections
        // into LocationSearchViewModel, which registers its own Results
        // collection in its constructor.
        //
        // Routed through IUiDispatcher rather than calling WPF's
        // BindingOperations directly, so this file carries no WPF dependency.
        // Null-conditional: with no dispatcher injected there is no binding
        // layer to inform, and registration is correctly a no-op.
        _uiDispatcher?.EnableCollectionSynchronization(SavedCharts, _savedChartsLock);
        _uiDispatcher?.EnableCollectionSynchronization(AllSavedCharts, _allSavedChartsLock);

    }

    #region H-1.3 — Location-search follow-on handlers

    /// <summary>
    /// Chart Entry panel: when the user selects a search result, copy the
    /// coordinates and display name into <see cref="ChartEntry"/>, resolve
    /// the IANA timezone from the coordinates, and refresh the calculate
    /// command states. Pre-H-1.3 this ran inside the setter of
    /// <c>SelectedLocationResult</c>; extracted here to keep the child
    /// free of cross-VM writes to <see cref="ChartEntry"/>.
    /// </summary>
    private void OnChartEntryLocationSearchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LocationSearchViewModel.SelectedResult)) return;
        LocationSearchResult? value = ChartEntryLocationSearch.SelectedResult;
        if (value is null) return;

        ChartEntry.LatitudeText = value.Latitude.ToString(CultureInfo.InvariantCulture);
        ChartEntry.LongitudeText = value.Longitude.ToString(CultureInfo.InvariantCulture);
        ChartEntry.LocationName = value.DisplayName;

        try
        {
            // The answer as offered, else its canonical zone; an answer the app does not
            // offer is left in the field for the user to correct.
            TimeZoneResult tzResult = TimeZoneLookup.GetTimeZone(value.Latitude, value.Longitude);
            if (!string.IsNullOrEmpty(tzResult.Result))
                ChartEntry.TimeZone = TimezoneProvider.FindOfferedZone(tzResult.Result, TimeZoneRules) ?? tzResult.Result;
        }
        catch (Exception ex)
        {
            _logger.Warning(ex,
                "Failed to determine timezone for {Lat}, {Lon}",
                value.Latitude, value.Longitude);
        }

        RefreshCommandStates();
    }

    #endregion

    /// <summary>
    /// Handles <see cref="ChartEntryViewModel.PropertyChanged"/> for two properties.
    /// <c>TimeZone</c>: re-applies the substring filter behind
    /// <see cref="FilteredTimezones"/> for the Chart Entry panel (the
    /// Electional panel's equivalent infrastructure lives on
    /// <see cref="ElectionalViewModel"/> since H-1.6, so this handler drives
    /// the Chart Entry filter only). <c>IncludeMinorPoints</c>: recasts the
    /// Chart panel's chart with the Points box's new value
    /// (<see cref="RecastForMinorPoints"/>; branch AsteroidsPanel, 1.6.0).
    /// </summary>
    private void OnChartEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChartEntryViewModel.IncludeMinorPoints))
        {
            RecastForMinorPoints();
            return;
        }

        if (e.PropertyName != nameof(ChartEntryViewModel.TimeZone))
            return;

        string trimmed = (ChartEntry.TimeZone ?? string.Empty).Trim();
        string? match = AvailableTimezones.FirstOrDefault(tz =>
            tz.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

        // Substring-filter the dropdown: show all when empty or exact match
        _timezoneFilterText = (string.IsNullOrEmpty(trimmed) || match != null)
            ? string.Empty : trimmed;
        RefreshFilteredTimezones();
    }

    /// <summary>
    /// Recasts the Chart panel's chart when the Points box changes, so the chart on screen, which the chart
    /// exports print as it was cast, carries the asteroids and Lilith exactly when the box says so (Steve's
    /// choice of 2026-10-20; branch AsteroidsPanel, 1.6.0).
    /// </summary>
    /// <remarks>
    /// Nothing happens without a chart, or when the chart already agrees with the box. Otherwise the chart is
    /// recast from its own input with the switch set (<see cref="ChartInput.WithIncludeMinorPoints"/>),
    /// synchronously as Calculate &amp; Save casts, and never saved: nothing reaches the repository and
    /// <see cref="HasResult"/> is unchanged, while the <see cref="LastResult"/> setter refreshes the Relocation
    /// panel's chart line as for any chart. The other panels take the new chart at their next Calculate, and the
    /// status line says so, naming the chart. A horary chart is recast all the same, and its line says a horary
    /// chart never carries the five (the engine's gate on the event type). An engine error leaves the chart as it
    /// was, puts the box back to the chart's value and reports the message with where the box went, "Points
    /// error: {message}; the box is back to off." (1.6.1, item E, branch ElectionalPicker; until then the box
    /// kept its value and disagreed with the chart): the box's write re-enters this method, which returns at
    /// its early exit since the chart equals the box again. An operation already running keeps the
    /// chart it started with, except the electional search's form fallback, which calls this once its cast has
    /// landed so a box moved during the cast is honoured (1.6.1, item D, branch ElectionalPicker).
    /// </remarks>
    private void RecastForMinorPoints()
    {
        ChartResult? current = LastResult;
        bool includeMinorPoints = ChartEntry.IncludeMinorPoints;
        if (current is null || current.Input.IncludeMinorPoints == includeMinorPoints)
            return;

        string state = includeMinorPoints ? "on" : "off";
        try
        {
            ChartResult recast = _chartEngine.Calculate(current.Input.WithIncludeMinorPoints(includeMinorPoints));
            LastResult = recast;

            string name = recast.Input.Name;
            bool horary = string.Equals(recast.Input.EventType, "Horary", StringComparison.OrdinalIgnoreCase);
            StatusText = horary
                ? $"Points {state}: a horary chart never carries the asteroids or Lilith."
                : includeMinorPoints
                    ? $"Points on: {name} now carries the asteroids and Lilith; recalculate other panels to include them."
                    : $"Points off: {name} no longer carries the asteroids and Lilith; recalculate other panels to leave them out.";
        }
        catch (Exception ex)
        {
            // The box goes back to the chart's value, so the two agree; the write re-enters this method, which
            // returns at its early exit. Then the line, so the error has the last word.
            string chartState = current.Input.IncludeMinorPoints ? "on" : "off";
            ChartEntry.IncludeMinorPoints = current.Input.IncludeMinorPoints;
            StatusText = $"Points error: {DisplayText.ErrorMessage(ex)}; the box is back to {chartState}.";
            _logger.Error(
                ex,
                "Failed to recast the chart for the Points box ({State}); the box is back to {ChartState}",
                state,
                chartState);
        }
    }

    #region UI-Thread Marshalling (H-2)

    /// <summary>
    /// Per-collection lock objects used with
    /// <see cref="IUiDispatcher.EnableCollectionSynchronization(System.Collections.IEnumerable, object)"/>.
    /// Each bound <see cref="ObservableCollection{T}"/> has its own lock so
    /// concurrent edits on unrelated collections never serialise on a shared
    /// monitor.
    /// </summary>
    /// <remarks>
    /// Today all collection mutations happen on the WPF UI thread after an
    /// <c>await</c> boundary, so taking the lock before modifying is optional.
    /// Callers that deliberately mutate from a background thread (e.g., a
    /// <c>Task.Run</c> lambda) must <c>lock</c> on the matching object before
    /// calling <c>Add</c>/<c>Clear</c>/<c>Remove</c> — the binding layer will
    /// then observe a consistent snapshot.
    /// </remarks>
    private readonly object _savedChartsLock = new();

    /// <inheritdoc cref="_savedChartsLock"/>
    private readonly object _allSavedChartsLock = new();

    #endregion

    #endregion

    #region H-1 Decomposition — Child View Models

    /// <summary>
    /// Chart-entry form state — subject name, date/time, timezone, location,
    /// event type, gender, notes, horary and house-system selections.
    /// Extracted from <see cref="MainViewModel"/> in Session H-1.2 as the
    /// first real decomposition child.
    /// </summary>
    /// <remarks>
    /// XAML bindings for these fields live on <c>ChartEntry.X</c>. Commands
    /// that act on chart-entry state stay on this parent: the child raises
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> from its
    /// setters and the parent routes that signal to
    /// <see cref="RefreshCommandStates"/> in the constructor.
    /// </remarks>
    public ChartEntryViewModel ChartEntry { get; }

    /// <summary>
    /// Location-search state for the Chart Entry panel. H-1.3 —
    /// first of four sibling instances of
    /// <see cref="LocationSearchViewModel"/>. Follow-on logic when a
    /// result is selected (writing coordinates into
    /// <see cref="ChartEntry"/> and resolving the IANA timezone from
    /// those coordinates) stays on this parent per cross-VM rule #4 and
    /// runs from <see cref="OnChartEntryLocationSearchPropertyChanged"/>.
    /// </summary>
    public LocationSearchViewModel ChartEntryLocationSearch { get; }

    /// <summary>
    /// Synastry &amp; Composite panel state — Person B selection and cached
    /// chart, relationship metadata, synastry and composite results, and the
    /// composite interpretation context. Extracted in Session H-1.4 as a
    /// single child because both panels share Person B plumbing. Commands
    /// (<see cref="CalculateSynastryCommand"/>,
    /// <see cref="ExportSynastryMarkdownCommand"/>,
    /// <see cref="CalculateCompositeCommand"/>,
    /// <see cref="ExportCompositeMarkdownCommand"/>) stay on this parent
    /// because each one reads the parent-owned <see cref="LastResult"/>
    /// (Person A), calls parent-owned services, and writes
    /// <see cref="StatusText"/> — per cross-VM rule #4.
    /// </summary>
    public SynastryViewModel Synastry { get; }

    /// <summary>
    /// Transits panel state — report date range, the technique and
    /// minor-aspects toggles, interpretation context, and the current
    /// <see cref="Core.Models.Transit.TransitResult"/>. Extracted in Session H-1.5.
    /// Commands (<see cref="CalculateTransitCommand"/>,
    /// <see cref="ExportTransitMarkdownCommand"/>,
    /// <see cref="ExportDailyHoroscopeMarkdownCommand"/>,
    /// <see cref="ExportTransitTimelineMarkdownCommand"/>) stay on this parent
    /// because each one reads the parent-owned <see cref="LastResult"/>, calls
    /// the parent-owned <c>ITransitService</c>, and writes
    /// <see cref="StatusText"/> — per cross-VM rule #4. Profection display
    /// state migrated into <see cref="Returns"/> as absorbed Category C state
    /// in Session H-1.7.
    /// </summary>
    public TransitsViewModel Transits { get; }

    /// <summary>
    /// Electional (best-time) panel state — election type, goal, date range,
    /// event location (with a nested
    /// <see cref="LocationSearchViewModel"/>), candidate configuration, time
    /// constraints, selected natal chart, and the current
    /// <see cref="Core.Models.Electional.ElectionalResult"/>. Extracted in
    /// Session H-1.6 as the sixth decomposition child and the first to compose
    /// a nested <see cref="LocationSearchViewModel"/> under its own umbrella
    /// rather than as a parent-owned sibling. Commands
    /// (<see cref="CalculateElectionalCommand"/>,
    /// <see cref="ExportElectionalMarkdownCommand"/>,
    /// <see cref="LookupElectionalLocationCommand"/>,
    /// <see cref="UseNatalLocationCommand"/>) stay on this parent because each
    /// reads the parent-owned <see cref="LastResult"/>, calls the parent-owned
    /// <c>IElectionalService</c> and <c>IChartEngine</c>, and writes
    /// <see cref="StatusText"/> — per cross-VM rule #4.
    /// </summary>
    public ElectionalViewModel Electional { get; }

    /// <summary>
    /// Returns panel state — Solar Return inputs and result, Lunar Return date
    /// range plus result list and selection, Planetary Return planet/mode/year/
    /// number selection plus result, and the absorbed profection display
    /// surface (profection / firdaria / zodiacal releasing summaries plus the
    /// <see cref="ReturnsViewModel.HasProfection"/> visibility flag).
    /// Extracted in Session H-1.7 as the seventh decomposition child following
    /// the H-1.4 multi-result precedent — three independently-calculated
    /// returns share the panel surface and the profection display state was
    /// classified as Category C in the H-1.5 §1A audit so it travelled here
    /// with internal-set accessors written by the parent's chart-lifecycle
    /// methods. Commands (<see cref="CalculateSolarReturnCommand"/>,
    /// <see cref="ExportSolarReturnMarkdownCommand"/>,
    /// <see cref="CalculateLunarReturnCommand"/>,
    /// <see cref="ExportLunarReturnMarkdownCommand"/>,
    /// <see cref="CalculatePlanetaryReturnCommand"/>,
    /// <see cref="ExportPlanetaryReturnMarkdownCommand"/>) stay on this parent
    /// because each one reads the parent-owned <see cref="LastResult"/>, calls
    /// a parent-owned return service, threads the parent-owned cancellation
    /// registry, and writes <see cref="StatusText"/> — per cross-VM rule #4.
    /// </summary>
    public ReturnsViewModel Returns { get; }

    /// <summary>
    /// Cycles panel state — Planetary Cycle Timeline input toggle and result,
    /// Great Conjunction year-range inputs and result, Synodic Cycle pair /
    /// year-range inputs and result. Extracted in Session H-1.8 as the eighth
    /// decomposition child, following the H-1.4 / H-1.7 multi-result precedent
    /// across three independent cycle scans sharing a single panel surface.
    /// Commands (<see cref="CalculateCycleTimelineCommand"/>,
    /// <see cref="ExportCycleTimelineMarkdownCommand"/>,
    /// <see cref="FindGreatConjunctionsCommand"/>,
    /// <see cref="ExportGCSeriesMarkdownCommand"/>,
    /// <see cref="FindSynodicCyclesCommand"/>,
    /// <see cref="ExportSynodicCycleMdCommand"/>) stay on this parent because
    /// each one reads the parent-owned <see cref="LastResult"/> (for the cycle
    /// timeline command), calls a parent-owned cycle service
    /// (<c>IPlanetaryCycleTimelineService</c>, <c>IGreatConjunctionService</c>,
    /// <c>ISynodicCycleService</c>), threads the parent-owned cancellation
    /// registry, and writes <see cref="StatusText"/> — per cross-VM rule #4.
    /// The cycle-timeline command additionally reads
    /// <see cref="ReturnsViewModel.SelectedReturnPlanet"/>,
    /// <see cref="ReturnsViewModel.SelectedReturnFindMode"/>,
    /// <see cref="ReturnsViewModel.ReturnYear"/>, and
    /// <see cref="ReturnsViewModel.ReturnNumber"/> from the sibling
    /// <see cref="Returns"/> VM via this parent as mediator — Rule #2
    /// last-resort shared-state read justified by the H-1.8 §1A audit
    /// (Outcome A): those four inputs belong to Planetary Return and the
    /// Cycle Timeline feature intentionally reuses them so the user doesn't
    /// retype planet / year / number twice.
    /// </summary>
    public CyclesViewModel Cycles { get; }

    /// <summary>
    /// Mundane panel state — mundane location (shared by Ingress Charts and
    /// Lunation Charts), Ingress Charts inputs and results (including the
    /// cardinal-ingress chart batch), Lunation Charts inputs and results,
    /// Mundane Aspect Finder inputs and results, and Eclipse Calculator
    /// inputs and results. Extracted in Session H-1.9 as the ninth
    /// decomposition child and the second application of the H-1.6 nested
    /// <see cref="LocationSearchViewModel"/> pattern (previously the sibling
    /// <c>MundaneLocationSearch</c>, now <see cref="MundaneViewModel.LocationSearch"/>).
    /// Commands (<see cref="LookupMundaneLocationCommand"/>,
    /// <see cref="FindIngressesCommand"/>,
    /// <see cref="CalculateCardinalIngressesCommand"/>,
    /// <see cref="ExportIngressSeriesMarkdownCommand"/>,
    /// <see cref="ExportCardinalIngressesMarkdownCommand"/>,
    /// <see cref="FindLunationsCommand"/>,
    /// <see cref="CalculateLunationChartsCommand"/>,
    /// <see cref="ExportLunationSeriesCommand"/>,
    /// <see cref="FindMundaneAspectsCommand"/>,
    /// <see cref="ExportMASeriesCommand"/>,
    /// <see cref="FindEclipsesCommand"/>,
    /// <see cref="ExportEclipseSeriesCommand"/>) stay on this parent because
    /// each one calls a parent-owned service (<c>IIngressService</c>,
    /// <c>ILunationService</c>, <c>IMundaneAspectService</c>,
    /// <c>IEclipseService</c>), threads the parent-owned cancellation
    /// registry, and writes <see cref="StatusText"/> — per cross-VM rule #4.
    /// The follow-on handler for the nested location-search selection
    /// migrated into the child per the Rule #4 refinement (intra-VM writes
    /// to this.* fields are not cross-VM sibling writes).
    /// </summary>
    public MundaneViewModel Mundane { get; }

    /// <summary>
    /// Return &amp; Ingress Timeline panel state: the scan window (start year,
    /// end year) and the event-type toggles (include returns, include
    /// ingresses) plus the most recent <see cref="ReturnIngressTimeline"/>
    /// output. Extracted by Session H-1.10 as the tenth decomposition child.
    /// <b>Outcome A (fully independent):</b> the calculate command reads
    /// parent-owned <c>LastResult</c> (the natal chart, required by the
    /// service) plus the four child-owned inputs, and performs no sibling
    /// VM reads. Services remain on <see cref="MainViewModel"/> per cross-VM
    /// rule #4.
    /// </summary>
    public ReturnIngressTimelineViewModel ReturnIngressTimeline { get; }

    /// <summary>
    /// Relocation panel state: the resolved natal chart selection, target
    /// location fields (name + lat/long text), and the most recent
    /// <c>RelocationResult</c>. Extracted by Session H-1.11 as the eleventh
    /// decomposition child (third and final sibling-to-nested
    /// <c>LocationSearchViewModel</c> migration — joining H-1.6 Electional
    /// and H-1.9 Mundane). <b>Outcome A (fully independent):</b> the calculate
    /// command reads parent-owned <c>LastResult</c>/<c>_chartEngine</c> plus
    /// child-owned inputs only, and performs no sibling VM reads. The
    /// relocation service remains on <see cref="MainViewModel"/> per cross-VM
    /// rule #4. <b>F-CS-Relocation</b> watch item resolved in the same pass:
    /// coordinates now format <i>and</i> parse through
    /// <c>CultureInfo.InvariantCulture</c> on both sides of the round-trip.
    /// </summary>
    public RelocationViewModel Relocation { get; }

    /// <summary>
    /// Astrocartography panel state: 12 celestial-body visibility toggles, 4
    /// line-type toggles, the most recent <see cref="AcgResult"/>, the
    /// derived <c>VisibleLines</c> projection, and the formatted map-click
    /// crossing report. Extracted by Session H-1.12 as the twelfth and
    /// final H-1 decomposition child; after this extraction
    /// <see cref="MainViewModel"/> owns services, commands, chart lifecycle,
    /// and cross-VM coordination only \u2014 every user-visible panel is
    /// rooted in an owning child VM. <b>Outcome A (fully independent):</b>
    /// the calculate command reads parent-owned <see cref="LastResult"/>
    /// plus the parent-owned <c>IAstrocartographyService</c> and writes
    /// output back through this child; the floating <c>AcgMapWindow</c>
    /// lifecycle remains on the View layer. <b>Audit Q5:</b> the open ACG
    /// antipode watch item is service-layer and has zero touchpoints in
    /// this VM (filters only by <c>Body</c> and <c>LineType</c>, never by
    /// coordinate geometry) \u2014 the watch item remains open.
    /// </summary>
    public AstrocartographyViewModel Astrocartography { get; }

    /// <summary>
    /// Gets the Directions panel's child (branch PrimaryPanel, 1.5.0): the four primary-direction settings,
    /// the result, and the result line and list shown of it. <c>CalculateDirectionsCommand</c> and
    /// <c>ExportDirectionsMarkdownCommand</c> stay on this parent (rule #4); the Transits panel's fourth
    /// technique reads its settings.
    /// </summary>
    public DirectionsViewModel Directions { get; }

    #endregion

    #region Properties — State

    /// <summary>Gets or sets the status bar text.</summary>
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    /// <summary>Gets a value indicating whether a chart result is available for export.</summary>
    public bool HasResult
    {
        get => _hasResult;
        private set
        {
            if (SetProperty(ref _hasResult, value))
                RefreshExportCommandStates();
        }
    }

    /// <summary>Gets the most recent chart calculation result.</summary>
    /// <remarks>
    /// The Chart panel's chart. Its change raises the Relocation and Electional panels' chart-in-use lines,
    /// which name it when no saved chart is picked there.
    /// </remarks>
    public ChartResult? LastResult
    {
        get => _lastResult;
        private set
        {
            if (SetProperty(ref _lastResult, value))
            {
                Relocation.OnChartPanelChartChanged();
                Electional.OnChartPanelChartChanged();
            }
        }
    }

    /// <summary>Gets or sets the search text for filtering saved charts.</summary>
    public string SearchText
    {
        get => _searchText;
        set => SetProperty(ref _searchText, value);
    }

    /// <summary>Gets or sets the event type filter for the saved charts list.</summary>
    public string FilterEventType
    {
        get => _filterEventType;
        set => SetProperty(ref _filterEventType, value);
    }

    /// <summary>Gets or sets the start date filter for the saved charts list.</summary>
    public DateTime? FilterDateFrom
    {
        get => _filterDateFrom;
        set => SetProperty(ref _filterDateFrom, value);
    }

    /// <summary>Gets or sets the end date filter for the saved charts list.</summary>
    public DateTime? FilterDateTo
    {
        get => _filterDateTo;
        set => SetProperty(ref _filterDateTo, value);
    }

    /// <summary>Gets or sets the sort field for the saved charts list.</summary>
    public ChartSortField SortField
    {
        get => _sortField;
        set => SetProperty(ref _sortField, value);
    }

    /// <summary>Gets or sets whether the saved charts list is sorted ascending.</summary>
    public bool SortAscending
    {
        get => _sortAscending;
        set => SetProperty(ref _sortAscending, value);
    }

    /// <summary>Gets the list of sort field values for the sort ComboBox.</summary>
    public IReadOnlyList<ChartSortField> SortFieldValues { get; } =
        [ChartSortField.Name, ChartSortField.Date, ChartSortField.EventType, ChartSortField.CreatedAt];

    /// <summary>
    /// Gets the list of core event types for the chart entry ComboBox.
    /// Only the four primary types (Birth, Event, Horary, Electional) are shown.
    /// Extended event types can be referenced in Notes or Keywords.
    /// </summary>
    public IReadOnlyList<EventTypeDisplayItem> EventTypes { get; } = EventTypeHelper.GetCoreDisplayItems();

    /// <summary>
    /// Gets the list of event type filter options for the filter ComboBox.
    /// The first entry is "All" (no filter), followed by the four core chart types.
    /// </summary>
    public IReadOnlyList<EventTypeDisplayItem> EventTypeFilterOptions { get; } =
        new[] { new EventTypeDisplayItem("", "All") }
            .Concat(EventTypeHelper.GetCoreDisplayItems())
            .ToList()
            .AsReadOnly();

    /// <summary>Gets the list of Gender enum values for the ComboBox.</summary>
    public IReadOnlyList<Gender> GenderValues { get; } =
        [Gender.Unspecified, Gender.Male, Gender.Female, Gender.Other];

    /// <summary>
    /// Gets the list of all available IANA timezone identifiers.
    /// Used internally for validation; the ComboBox binds to
    /// <see cref="FilteredTimezones"/> instead.
    /// </summary>
    public IReadOnlyList<string> AvailableTimezones { get; } = TimezoneProvider.GetTimezoneIds();

    /// <summary>
    /// Gets the time-zone rules the form converts with, the same IANA data the chart
    /// engine casts with by default. The child view models read them from here.
    /// </summary>
    public ITimeZoneRules TimeZoneRules { get; }

    /// <summary>
    /// Gets the filtered list of timezone identifiers for the main timezone
    /// ComboBox. Filters by case-insensitive substring match as the user types.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Formerly a WPF <c>ICollectionView</c> over a <c>CollectionViewSource</c>
    /// (<c>WindowsBase</c>/<c>PresentationFramework</c>, no cross-platform
    /// equivalent), now an <see cref="ObservableRangeCollection{T}"/> refreshed
    /// in place. The ComboBox binds the same property name and sees the same
    /// entries in the same order.
    /// </para>
    /// <para>
    /// <b>This property returns one instance for the lifetime of the view model
    /// — instance stability is load-bearing, do not reassign it.</b> The
    /// ComboBox binds <c>SelectedItem</c> two-way, and a UI framework's
    /// selector preserves the selection across an in-place
    /// <c>Reset</c> notification (the shape <c>Refresh()</c> produced) but
    /// drops it — nulling the bound <c>SelectedTimezone</c> — when the
    /// <c>ItemsSource</c> instance itself is replaced. Each filter change
    /// therefore mutates this one collection via
    /// <see cref="ObservableRangeCollection{T}.ReplaceAll"/>, raising exactly
    /// one <c>Reset</c> and no per-item events across the ~600 entries.
    /// Pinned by <c>TimezoneFilterStabilityTests</c>.
    /// </para>
    /// </remarks>
    public IReadOnlyList<string> FilteredTimezones => _filteredTimezones;

    /// <summary>
    /// Gets the collection of saved charts displayed in the saved-charts list (filtered and sorted).
    /// Search, the sort toggle and every refresh refill it.
    /// </summary>
    public ObservableCollection<ChartEntity> SavedCharts { get; }

    /// <summary>
    /// Gets the unfiltered collection of all saved charts, the list the Person B, Electional
    /// natal-chart and Relocation natal-chart pickers bind (1.4.0).
    /// </summary>
    /// <remarks>
    /// Only <see cref="RefreshSavedChartsAsync"/> refills it, keeping the three selections by Id,
    /// so a search, a sort or the saved-charts filter does not reach the pickers, and only
    /// deleting a chart empties its picker. Until 1.4.0 the pickers bound
    /// <see cref="SavedCharts"/>: every refill emptied them, since the bound ComboBoxes wrote
    /// null back when the list was cleared, and the filter narrowed them.
    /// </remarks>
    public ObservableCollection<ChartEntity> AllSavedCharts { get; }

    /// <summary>Application version string for display (e.g., "1.0.0").</summary>
    public string AppVersion { get; } = GetAppVersion();

    /// <summary>Window title including version (e.g., "AstroCast v1.0.0").</summary>
    public string WindowTitle { get; } = $"AstroCast v{GetAppVersion()}";

    /// <summary>
    /// Gets the application version from the assembly's InformationalVersion attribute.
    /// Trims any git commit hash suffix appended by the build system.
    /// </summary>
    /// <returns>Version string (e.g., "1.0.0").</returns>
    private static string GetAppVersion()
    {
        string version = typeof(MainViewModel).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion ?? "1.0.0";

        // Trim the git commit hash suffix (e.g., "1.0.0+abc123" -> "1.0.0")
        int plusIndex = version.IndexOf('+');
        if (plusIndex >= 0)
            version = version[..plusIndex];

        return version;
    }

    #endregion

    #region Properties — Calculation State

    /// <summary>
    /// Gets a value indicating whether a long-running calculation (transit,
    /// synastry, or electional) is currently in progress.
    /// </summary>
    public bool IsCalculating
    {
        get => _isCalculating;
        private set
        {
            if (SetProperty(ref _isCalculating, value))
            {
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    #endregion

    #region Cancellation Registry

    /// <summary>
    /// Stable string identifiers used to register long-running operations with
    /// <see cref="OperationCancellationRegistry"/>. The constants are also used
    /// directly as <c>CommandParameter</c> string literals in panel XAML so per-panel
    /// Cancel buttons can target a single operation.
    /// </summary>
    /// <remarks>
    /// Keep these values in sync with the literal strings in
    /// <see cref="s_operationDisplayNames"/> and in the per-panel XAML files.
    /// </remarks>
    public static class OperationKeys
    {
        /// <summary>Transit search and aspect timeline.</summary>
        public const string Transits = "transits";

        /// <summary>Electional search across a date range.</summary>
        public const string Electional = "electional";

        /// <summary>Lunar return chart series.</summary>
        public const string LunarReturn = "lunarReturn";

        /// <summary>Single planetary return chart calculation.</summary>
        public const string PlanetaryReturn = "planetaryReturn";

        /// <summary>Planetary cycle timeline between successive returns.</summary>
        public const string PlanetaryCycle = "planetaryCycle";

        /// <summary>Great-conjunction series search.</summary>
        public const string GreatConjunctions = "greatConjunctions";

        /// <summary>Generic synodic-cycle series search.</summary>
        public const string SynodicCycle = "synodicCycle";

        /// <summary>Single-planet ingress series.</summary>
        public const string IngressSeries = "ingressSeries";

        /// <summary>Cardinal Sun ingress chart batch (4 charts).</summary>
        public const string CardinalIngresses = "cardinalIngresses";

        /// <summary>Lunation (New/Full Moon) series search.</summary>
        public const string LunationSeries = "lunationSeries";

        /// <summary>Lunation chart batch (charts at each lunation moment).</summary>
        public const string LunationCharts = "lunationCharts";

        /// <summary>Mundane planet-pair aspect series search.</summary>
        public const string MundaneAspects = "mundaneAspects";

        /// <summary>Solar / lunar eclipse series search.</summary>
        public const string Eclipses = "eclipses";

        /// <summary>Combined returns + ingresses timeline.</summary>
        public const string ReturnIngressTimeline = "returnIngressTimeline";

        /// <summary>Astrocartography line and crossing computation.</summary>
        public const string Astrocartography = "astrocartography";

        /// <summary>PDF chart export via QuestPDF (single or combined PDF + Markdown).</summary>
        public const string PdfSave = "pdfSave";

        /// <summary>Synastry inter-chart calculation between Person A and Person B.</summary>
        public const string Synastry = "synastry";

        /// <summary>Composite midpoint chart calculation between Person A and Person B.</summary>
        public const string Composite = "composite";

        /// <summary>The Davison chart, a real chart at the midpoint moment and place of Person A and Person B (branch DavisonPanel, 1.7.0).</summary>
        public const string Davison = "davison";

        /// <summary>Solar return chart calculation for a specific year.</summary>
        public const string SolarReturn = "solarReturn";

        /// <summary>Relocation chart recast to a different geographic location.</summary>
        public const string Relocation = "relocation";

        /// <summary>A chart's primary directions to the age limit (branch PrimaryPanel, 1.5.0).</summary>
        public const string Directions = "directions";
    }

    /// <summary>
    /// Maps every <see cref="OperationKeys"/> value to a human-readable label
    /// for the status bar and Cancel button tooltips.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> s_operationDisplayNames =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [OperationKeys.Transits] = "Transits",
            [OperationKeys.Electional] = "Electional",
            [OperationKeys.LunarReturn] = "Lunar Return",
            [OperationKeys.PlanetaryReturn] = "Planetary Return",
            [OperationKeys.PlanetaryCycle] = "Cycle Timeline",
            [OperationKeys.GreatConjunctions] = "Great Conjunctions",
            [OperationKeys.SynodicCycle] = "Synodic Cycles",
            [OperationKeys.IngressSeries] = "Ingresses",
            [OperationKeys.CardinalIngresses] = "Cardinal Ingresses",
            [OperationKeys.LunationSeries] = "Lunations",
            [OperationKeys.LunationCharts] = "Lunation Charts",
            [OperationKeys.MundaneAspects] = "Mundane Aspects",
            [OperationKeys.Eclipses] = "Eclipses",
            [OperationKeys.ReturnIngressTimeline] = "Return-Ingress Timeline",
            [OperationKeys.Astrocartography] = "Astrocartography",
            [OperationKeys.PdfSave] = "PDF Export",
            [OperationKeys.Synastry] = "Synastry",
            [OperationKeys.Composite] = "Composite",
            [OperationKeys.Davison] = "Davison",
            [OperationKeys.SolarReturn] = "Solar Return",
            [OperationKeys.Relocation] = "Relocation",
            [OperationKeys.Directions] = "Primary Directions",
        };

    /// <summary>
    /// Gets the human-readable label for an operation key, falling back to the
    /// raw key when no display name has been registered.
    /// </summary>
    /// <param name="operationKey">The registry key to look up.</param>
    private static string GetOperationDisplayName(string operationKey) =>
        s_operationDisplayNames.TryGetValue(operationKey, out string? name) ? name : operationKey;

    /// <summary>
    /// Gets a value indicating whether the cancellation registry currently
    /// holds at least one active operation.
    /// </summary>
    /// <remarks>
    /// Updated explicitly via <see cref="RaiseActiveOperationChanged"/> after
    /// each <see cref="OperationCancellationRegistry.StartOperation"/> /
    /// <see cref="OperationCancellationRegistry.CompleteOperation(string)"/>
    /// call so WPF bindings refresh on the UI thread.
    /// </remarks>
    public bool HasActiveOperation => _cancelRegistry.ActiveOperationKeys.Count > 0;

    /// <summary>
    /// Gets the registry key of the single active operation, or <c>null</c> when
    /// either no operation is active or more than one is active. Per-panel Cancel
    /// buttons target a specific key directly, so this is purely informational.
    /// </summary>
    public string? ActiveOperationKey
    {
        get
        {
            IReadOnlyCollection<string> keys = _cancelRegistry.ActiveOperationKeys;
            return keys.Count == 1 ? keys.First() : null;
        }
    }

    /// <summary>
    /// Gets the human-readable label of the single active operation, or
    /// <c>null</c> when none or several are active.
    /// </summary>
    public string? ActiveOperationDisplayName
    {
        get
        {
            string? key = ActiveOperationKey;
            return key is null ? null : GetOperationDisplayName(key);
        }
    }

    /// <summary>
    /// Raises <see cref="INotifyPropertyChanged.PropertyChanged"/> for every
    /// registry-derived property and re-evaluates <see cref="CancelCommand"/>'s
    /// can-execute state. Must be invoked on the WPF UI thread immediately after
    /// every registry mutation so per-panel and global Cancel buttons toggle
    /// instantly.
    /// </summary>
    private void RaiseActiveOperationChanged()
    {
        OnPropertyChanged(nameof(HasActiveOperation));
        OnPropertyChanged(nameof(ActiveOperationKey));
        OnPropertyChanged(nameof(ActiveOperationDisplayName));
        CancelCommand.RaiseCanExecuteChanged();
    }

    #endregion

    #region Navigation

    /// <summary>
    /// Gets or sets the currently active navigation panel.
    /// Changing this value causes the corresponding content panel to become visible
    /// in the main window, while all other panels collapse.
    /// </summary>
    public NavigationPanel ActivePanel
    {
        get => _activePanel;
        set
        {
            if (SetProperty(ref _activePanel, value))
            {
                OnPropertyChanged(nameof(IsChartPanelVisible));
                OnPropertyChanged(nameof(IsSynastryPanelVisible));
                OnPropertyChanged(nameof(IsTransitsPanelVisible));
                OnPropertyChanged(nameof(IsReturnsPanelVisible));
                OnPropertyChanged(nameof(IsElectionalPanelVisible));
                OnPropertyChanged(nameof(IsMedicalPanelVisible));
                OnPropertyChanged(nameof(IsMapPanelVisible));
                OnPropertyChanged(nameof(IsExportPanelVisible));
                OnPropertyChanged(nameof(IsRelocationPanelVisible));
                OnPropertyChanged(nameof(IsDirectionsPanelVisible));
            }
        }
    }

    /// <summary>Visibility of the Chart input panel.</summary>
    public bool IsChartPanelVisible => ActivePanel == NavigationPanel.Chart;

    /// <summary>Visibility of the Synastry &amp; Composite panel.</summary>
    public bool IsSynastryPanelVisible => ActivePanel == NavigationPanel.Synastry;

    /// <summary>Visibility of the Transits panel.</summary>
    public bool IsTransitsPanelVisible => ActivePanel == NavigationPanel.Transits;

    /// <summary>Visibility of the Returns (Solar + Lunar) panel.</summary>
    public bool IsReturnsPanelVisible => ActivePanel == NavigationPanel.Returns;

    /// <summary>Visibility of the Electional panel.</summary>
    public bool IsElectionalPanelVisible => ActivePanel == NavigationPanel.Electional;

    /// <summary>Visibility of the Medical Astrology panel.</summary>
    public bool IsMedicalPanelVisible => ActivePanel == NavigationPanel.Medical;

    /// <summary>
    /// Gets or sets the free-text health context for the medical astrology report.
    /// When provided, this is included verbatim in the report header so that an AI
    /// model can take it into account during interpretation.
    /// </summary>
    public string MedicalHealthContext
    {
        get => _medicalHealthContext;
        set => SetProperty(ref _medicalHealthContext, value);
    }

    /// <summary>
    /// Visibility of the Astrocartography Map panel. The map lives in its own
    /// top-level panel (not inside the Returns scroll-view) so that the
    /// SkiaSharp rendering surface is visible at navigation time — Mapsui
    /// does not initialise its GPU surface while its host is collapsed or
    /// off-screen.
    /// </summary>
    public bool IsMapPanelVisible => ActivePanel == NavigationPanel.Map;

    /// <summary>Visibility of the consolidated Export panel.</summary>
    public bool IsExportPanelVisible => ActivePanel == NavigationPanel.Export;

    /// <summary>Visibility of the Relocation panel.</summary>
    public bool IsRelocationPanelVisible => ActivePanel == NavigationPanel.Relocation;

    /// <summary>Visibility of the Directions panel (branch PrimaryPanel, 1.5.0).</summary>
    public bool IsDirectionsPanelVisible => ActivePanel == NavigationPanel.Directions;

    /// <summary>
    /// Command to switch the active navigation panel.
    /// Bound to the sidebar icon buttons via CommandParameter.
    /// </summary>
    public ICommand NavigateToPanelCommand =>
        _navigateToPanelCommand ??= new RelayCommand<NavigationPanel>(panel => ActivePanel = panel);

    #endregion

    #region Commands

    /// <summary>Calculate the chart from form data and save to database.</summary>
    public AsyncRelayCommand CalculateAndSaveCommand { get; }

    /// <summary>Export the last result as PDF via the file-save dialog.</summary>
    public AsyncRelayCommand ExportPdfCommand { get; }

    /// <summary>Export the last result as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportMarkdownCommand { get; }

    /// <summary>Export the last result as the chart JSON via the file-save dialog (O-D2 option b).</summary>
    public AsyncRelayCommand ExportChartJsonCommand { get; }

    /// <summary>Export the last result as both PDF and Markdown.</summary>
    public AsyncRelayCommand ExportBothCommand { get; }

    /// <summary>Load a selected chart from the saved list into the form.</summary>
    public AsyncRelayCommand LoadChartCommand { get; }

    /// <summary>Delete a selected chart from the database.</summary>
    public AsyncRelayCommand DeleteChartCommand { get; }

    /// <summary>Search/filter saved charts by name.</summary>
    public AsyncRelayCommand SearchChartsCommand { get; }

    /// <summary>Look up a location using the Nominatim geocoding API.</summary>
    public AsyncRelayCommand LookupLocationCommand { get; }

    /// <summary>Clear the form to its default state.</summary>
    public RelayCommand ClearFormCommand { get; }

    /// <summary>Set the date and time fields to the current date and time.</summary>
    public RelayCommand NowCommand { get; }

    /// <summary>Set the date and time fields to now (horary convenience button).</summary>
    public RelayCommand SetToNowCommand { get; }

    /// <summary>Calculate synastry between Person A (current chart) and selected Person B.</summary>
    public AsyncRelayCommand CalculateSynastryCommand { get; }

    /// <summary>Export the synastry result as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportSynastryMarkdownCommand { get; }

    /// <summary>
    /// Calculate the Transits panel's report for the current natal chart over the specified date
    /// range, with the techniques checked; disabled while none is.
    /// </summary>
    public AsyncRelayCommand CalculateTransitCommand { get; }

    /// <summary>Export the transit result as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportTransitMarkdownCommand { get; }

    /// <summary>
    /// Export daily horoscope Markdown for the current transit result; disabled for a result
    /// without transits.
    /// </summary>
    public AsyncRelayCommand ExportDailyHoroscopeMarkdownCommand { get; }

    /// <summary>
    /// Export a chronological transit timeline as Markdown via the file-save dialog; disabled for a
    /// result without transits.
    /// </summary>
    public AsyncRelayCommand ExportTransitTimelineMarkdownCommand { get; }

    /// <summary>Calculate electional chart from form data.</summary>
    public AsyncRelayCommand CalculateElectionalCommand { get; }

    /// <summary>Export the electional result as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportElectionalMarkdownCommand { get; }

    /// <summary>Look up an electional location using the Nominatim geocoding API.</summary>
    public AsyncRelayCommand LookupElectionalLocationCommand { get; }

    /// <summary>Copy natal chart location to electional location fields.</summary>
    public RelayCommand UseNatalLocationCommand { get; }

    /// <summary>
    /// Cancel the current long-running calculation (transit, synastry, or electional).
    /// </summary>
    /// <summary>Calculate composite chart between Person A and selected Person B.</summary>
    public AsyncRelayCommand CalculateCompositeCommand { get; }

    /// <summary>Export the composite result as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportCompositeMarkdownCommand { get; }

    /// <summary>
    /// Cast the Davison chart of the Chart panel's chart and the selected Person B: a real chart at the midpoint
    /// moment and place (branch DavisonPanel, 1.7.0). Enabled with a chart on screen and a Person B picked, as
    /// the Composite's command is.
    /// </summary>
    public AsyncRelayCommand CalculateDavisonCommand { get; }

    /// <summary>Export the Davison result, the chart complete, as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportDavisonMarkdownCommand { get; }

    /// <summary>Calculate solar return chart for the current natal chart and specified year.</summary>
    public AsyncRelayCommand CalculateSolarReturnCommand { get; }

    /// <summary>Export the solar return result as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportSolarReturnMarkdownCommand { get; }

    /// <summary>Calculate lunar returns for the current natal chart over the specified date range.</summary>
    public AsyncRelayCommand CalculateLunarReturnCommand { get; }

    /// <summary>Export the selected lunar return result as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportLunarReturnMarkdownCommand { get; }

    /// <summary>Calculate a planetary return chart for the current natal chart and selected planet.</summary>
    public AsyncRelayCommand CalculatePlanetaryReturnCommand { get; }

    /// <summary>Export the planetary return result as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportPlanetaryReturnMarkdownCommand { get; }

    /// <summary>Calculate the cycle timeline between this return and the next.</summary>
    public AsyncRelayCommand CalculateCycleTimelineCommand { get; }

    /// <summary>Export the cycle timeline as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportCycleTimelineMarkdownCommand { get; }

    /// <summary>Find all Jupiter-Saturn great conjunctions within the specified year range.</summary>
    public AsyncRelayCommand FindGreatConjunctionsCommand { get; }

    /// <summary>Export the great conjunction series as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportGCSeriesMarkdownCommand { get; }

    /// <summary>Find all conjunctions of the selected planet pair within the year range.</summary>
    public AsyncRelayCommand FindSynodicCyclesCommand { get; }

    /// <summary>Export the synodic cycle series as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportSynodicCycleMdCommand { get; }

    /// <summary>Look up a mundane location by name using Nominatim geocoding.</summary>
    public AsyncRelayCommand LookupMundaneLocationCommand { get; }

    /// <summary>Find all ingresses of a planet within a year.</summary>
    public AsyncRelayCommand FindIngressesCommand { get; }

    /// <summary>Calculate full charts for all four cardinal Sun ingresses in a year.</summary>
    public AsyncRelayCommand CalculateCardinalIngressesCommand { get; }

    /// <summary>Export the ingress series as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportIngressSeriesMarkdownCommand { get; }

    /// <summary>Export the cardinal ingresses as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportCardinalIngressesMarkdownCommand { get; }

    /// <summary>Find all New Moons and Full Moons for the specified year.</summary>
    public AsyncRelayCommand FindLunationsCommand { get; }

    /// <summary>Calculate full charts at all lunations using the mundane location.</summary>
    public AsyncRelayCommand CalculateLunationChartsCommand { get; }

    /// <summary>Export the lunation series as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportLunationSeriesCommand { get; }

    /// <summary>Find all exact aspects between two bodies within the specified year range.</summary>
    public AsyncRelayCommand FindMundaneAspectsCommand { get; }

    /// <summary>Export the mundane aspect series as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportMASeriesCommand { get; }

    /// <summary>Find all solar and lunar eclipses within the specified year range.</summary>
    public AsyncRelayCommand FindEclipsesCommand { get; }

    /// <summary>Export the eclipse series as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportEclipseSeriesCommand { get; }

    /// <summary>Calculate the return &amp; ingress timeline for the current natal chart.</summary>
    public AsyncRelayCommand CalculateRITimelineCommand { get; }

    /// <summary>Export the return &amp; ingress timeline as Markdown via the file-save dialog.</summary>
    public AsyncRelayCommand ExportRITimelineCommand { get; }

    /// <summary>Calculate a relocation chart for the current natal chart.</summary>
    public AsyncRelayCommand CalculateRelocationCommand { get; }

    /// <summary>Export the current relocation result as Markdown.</summary>
    public AsyncRelayCommand ExportRelocationMarkdownCommand { get; }

    /// <summary>
    /// Calculate the primary directions of the Chart panel's chart with the Directions panel's settings. Needs a
    /// chart and settings within the engine's limits.
    /// </summary>
    public AsyncRelayCommand CalculateDirectionsCommand { get; }

    /// <summary>Export the current primary-direction result as the whole-life Primary Directions Report in Markdown.</summary>
    public AsyncRelayCommand ExportDirectionsMarkdownCommand { get; }

    /// <summary>Export a medical astrology report for the current natal chart as Markdown.</summary>
    public AsyncRelayCommand ExportMedicalMarkdownCommand { get; }

    /// <summary>Look up a relocation target location by name.</summary>
    public AsyncRelayCommand LookupRelocationLocationCommand { get; }

    /// <summary>Compute astrocartography lines for the current natal chart.</summary>
    public AsyncRelayCommand CalculateAcgCommand { get; }

    /// <summary>Export the current astrocartography result as Markdown.</summary>
    public AsyncRelayCommand ExportAcgMarkdownCommand { get; }

    /// <summary>
    /// Open (or focus) the floating Astrocartography map window. Disabled
    /// until ACG has been calculated.
    /// </summary>
    public RelayCommand OpenAcgMapWindowCommand { get; }

    /// <summary>
    /// Callback the main window assigns at startup. The ViewModel does not
    /// create or own WPF windows itself — it just invokes the action when
    /// the user asks for the ACG map. The main window is responsible for
    /// instantiating <c>AcgMapWindow</c> with the correct Owner and
    /// for reusing an existing window if one is already open.
    /// </summary>
    public Action? OpenAcgMapWindowAction { get; set; }

    /// <summary>
    /// Open the About window: the version, the licence notice, the source and the
    /// third-party notices. Always enabled.
    /// </summary>
    public RelayCommand OpenAboutWindowCommand { get; }

    /// <summary>
    /// Callback each head's main window assigns, as it assigns
    /// <see cref="OpenAcgMapWindowAction"/>: the view model invokes it when the
    /// user asks for the About window, and the main window shows its own modal
    /// About window over itself.
    /// </summary>
    public Action? OpenAboutWindowAction { get; set; }

    /// <summary>
    /// The UI-thread marshaller shared with every composed
    /// <see cref="ChildViewModelBase"/>, or <see langword="null"/> when none was
    /// injected (headless/unit-test context — raise notifications inline).
    /// </summary>
    /// <remarks>
    /// Exposed as <c>internal</c> rather than <c>public</c> deliberately: this is
    /// infrastructure for the H-2 marshalling contract, not a bindable surface, so
    /// it must not appear to XAML. Children read it through
    /// <see cref="ChildViewModelBase"/>'s existing <c>Parent</c> back-reference
    /// instead of taking their own constructor parameter, which keeps the single
    /// dispatcher instance shared across the whole view-model tree and avoids
    /// threading a parameter through all eleven child constructors for no
    /// behavioural gain.
    /// </remarks>
    internal IUiDispatcher? UiDispatcher => _uiDispatcher;

    public RelayCommand CancelCommand { get; }

    /// <summary>Export all saved charts to a JSON file via the file-save dialog.</summary>
    public AsyncRelayCommand ExportDatabaseCommand { get; }

    /// <summary>Import charts from a JSON file via the file-open dialog.</summary>
    public AsyncRelayCommand ImportDatabaseCommand { get; }

    /// <summary>Toggle the sort direction and refresh the chart list.</summary>
    public AsyncRelayCommand ToggleSortDirectionCommand { get; }

    #endregion

    #region Public Methods

    /// <summary>
    /// Initialise the repository and load all saved charts.
    /// Called once during application startup.
    /// </summary>
    public async Task InitializeAsync()
    {
        try
        {
            await _repository.InitializeAsync();
            await RefreshSavedChartsAsync();
            StatusText = "Database initialised. Ready.";
            _logger.Information("MainViewModel initialised, {Count} charts loaded", SavedCharts.Count);
        }
        catch (Exception ex)
        {
            StatusText = $"Database error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to initialise database");
        }
    }

    #endregion

    #region Command Handlers

    /// <summary>
    /// Cancel a long-running calculation through the
    /// <see cref="OperationCancellationRegistry"/>.
    /// </summary>
    /// <param name="operationKey">
    /// The registry key of the operation to cancel (see
    /// <see cref="OperationKeys"/>). When null, empty, or whitespace, every
    /// currently registered operation is cancelled — used by the global
    /// status-bar Cancel button.
    /// </param>
    private void CancelCalculation(string? operationKey)
    {
        if (string.IsNullOrWhiteSpace(operationKey))
        {
            // Snapshot first because CancelOperation does not remove the entry,
            // but later CompleteOperation calls in finally blocks will.
            string[] activeKeys = [.. _cancelRegistry.ActiveOperationKeys];

            if (activeKeys.Length == 0)
            {
                return;
            }

            foreach (string key in activeKeys)
            {
                _cancelRegistry.CancelOperation(key);
            }

            StatusText = activeKeys.Length == 1
                ? $"Cancelling {GetOperationDisplayName(activeKeys[0])}..."
                : $"Cancelling {activeKeys.Length} operations...";

            _logger.Information(
                "User requested global cancellation; {Count} active operation(s)",
                activeKeys.Length);
            return;
        }

        if (_cancelRegistry.CancelOperation(operationKey))
        {
            StatusText = $"Cancelling {GetOperationDisplayName(operationKey)}...";
            _logger.Information(
                "User requested cancellation of operation {OperationKey}",
                operationKey);
        }
    }

    /// <summary>
    /// CanExecute predicate for <see cref="CancelCommand"/>. Returns true when a
    /// specific operation is active (per-panel Cancel) or any operation is
    /// active (global Cancel with null/empty parameter).
    /// </summary>
    /// <param name="operationKey">
    /// Optional registry key. When null/empty, the global cancel is enabled
    /// whenever <see cref="HasActiveOperation"/> is true.
    /// </param>
    private bool CanCancel(string? operationKey)
    {
        if (string.IsNullOrWhiteSpace(operationKey))
        {
            return HasActiveOperation;
        }

        return _cancelRegistry.IsOperationActive(operationKey);
    }

    /// <summary>
    /// Export all saved charts to a JSON file using the file-save dialog.
    /// Provides data backup and portability.
    /// </summary>
    private async Task ExportDatabaseAsync(object? _)
    {
        try
        {
            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Charts Database",
                FileDialogFileTypes.JsonOnly,
                $"AstroCast_Charts_{FileNameDate(DateOnly.FromDateTime(DateTime.Now))}.json",
                ".json");

            if (path is not null)
            {
                StatusText = "Exporting charts...";
                string json = await _repository.ExportAllAsJsonAsync();
                await File.WriteAllTextAsync(path, json);

                // Count the charts in the file just written, every saved chart: SavedCharts is
                // the list the saved-charts filter narrows (E3-6).
                int written;
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    written = document.RootElement.GetArrayLength();
                }

                StatusText = $"Exported {written} charts to {path}";
                _logger.Information("Charts exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export charts database");
        }
    }

    /// <summary>The status line for an Import Backup file that is one chart's JSON export.</summary>
    internal const string ImportSingleChartExportMessage = "This file is one chart's JSON export, not a chart backup.";

    /// <summary>The status line for an Import Backup file that is JSON but not a chart backup.</summary>
    internal const string ImportNotABackupMessage = "This file is not an AstroCast chart backup.";

    /// <summary>The status line for an Import Backup file that is not JSON at all.</summary>
    internal const string ImportNotJsonMessage = "This file is not valid JSON.";

    /// <summary>
    /// Says, in the user's words, what a file is that Import Backup could not read as a chart backup (branch
    /// InputChecks, 1.7.2, finding N17).
    /// </summary>
    /// <remarks>
    /// A backup is the array of saved charts Export Backup writes. One chart's JSON export, which Export JSON
    /// writes, is an object holding a <c>chart</c> object and a <c>positions</c> array, and is named for what it
    /// is; any other JSON is not a backup; text that does not parse is not JSON. Until 1.7.2 all three read
    /// "Import error: Invalid JSON format" followed by the serializer's own message.
    /// </remarks>
    /// <param name="text">The file's text.</param>
    /// <returns>The sentence for the status line.</returns>
    internal static string DescribeFileThatIsNotABackup(string text)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(text);
            JsonElement root = document.RootElement;
            bool singleChartExport = root.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("chart", out JsonElement chart) && chart.ValueKind == JsonValueKind.Object
                && root.TryGetProperty("positions", out JsonElement positions) && positions.ValueKind == JsonValueKind.Array;

            return singleChartExport ? ImportSingleChartExportMessage : ImportNotABackupMessage;
        }
        catch (JsonException)
        {
            return ImportNotJsonMessage;
        }
    }

    /// <summary>
    /// Import charts from a JSON file using the file-open dialog.
    /// Upserts each chart (updates existing by name, inserts new).
    /// Shows a confirmation dialog before importing.
    /// </summary>
    /// <remarks>
    /// A file that is not a chart backup imports nothing, and the status line says what it is
    /// (<see cref="DescribeFileThatIsNotABackup"/>); the log keeps the serializer's exception. The repository
    /// reads the whole file before it saves the first chart, so a backup with one broken chart imports none.
    /// </remarks>
    private async Task ImportDatabaseAsync(object? _)
    {
        // The file's text, kept for the refusal's wording when it is not a backup
        string? json = null;

        try
        {
            string? path = await _fileDialogService.ShowOpenAsync(
                "Import Charts Database",
                FileDialogFileTypes.JsonOnly,
                ".json");

            if (path is not null)
            {
                bool confirmed = await _dialogService.ConfirmAsync(
                    "Import charts from this file?\n\n" +
                    "Charts with matching names will be updated.\n" +
                    "New charts will be added.\n\n" +
                    "It is recommended to export your current data first.",
                    "Import Charts");

                if (!confirmed) return;

                StatusText = "Importing charts...";
                json = await File.ReadAllTextAsync(path);

                // An empty file is no JSON at all, and the repository refuses it with an argument error
                if (string.IsNullOrWhiteSpace(json))
                {
                    StatusText = ImportNotJsonMessage;
                    _logger.Warning("Import refused: {Path} is empty", path);
                    return;
                }

                int count = await _repository.ImportFromJsonAsync(json);
                await RefreshSavedChartsAsync();
                StatusText = $"Imported {count} chart(s) from {path}";
                _logger.Information("Imported {Count} charts from {Path}", count, path);
            }
        }
        catch (System.Text.Json.JsonException ex)
        {
            // Not a chart backup: the sentence says what the file is, the log keeps the serializer's detail
            StatusText = DescribeFileThatIsNotABackup(json ?? string.Empty);
            _logger.Error(ex, "Failed to import charts: {Reason}", StatusText);
        }
        catch (Exception ex)
        {
            StatusText = $"Import error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to import charts database");
        }
    }

    /// <summary>
    /// Toggle the sort direction and refresh the saved charts list.
    /// </summary>
    private async Task ToggleSortDirectionAsync(object? _)
    {
        SortAscending = !SortAscending;
        await SearchChartsAsync(null);
    }

    /// <summary>
    /// Builds the chart input the Chart panel's form describes, from the values its boxes were read as: the one
    /// place a form becomes a <see cref="ChartInput"/> (branch InputChecks, 1.7.2, finding N5).
    /// </summary>
    /// <remarks>
    /// Calculate &amp; Save and the electional search's cast from the form both call it, so the two build the same
    /// input from the same form: its name, clock, place, type, gender, notes, horary question and category (a
    /// horary chart's only), house system and the session's Points switch. Until 1.7.2 the electional search built
    /// an input of its own, always "Birth" in Placidus houses with no horary fields, and that chart became the
    /// Chart panel's chart while the form said otherwise. The caller reads and checks the boxes, in its own order
    /// and with its own messages; nothing here can fail.
    /// </remarks>
    /// <param name="localDateTime">The form's date at the time of day read from the Time box.</param>
    /// <param name="latitude">The latitude read from its box, in decimal degrees.</param>
    /// <param name="longitude">The longitude read from its box, in decimal degrees.</param>
    /// <param name="utcOffsetHours">The explicit offset read from its box, or null when the override is off.</param>
    /// <returns>The chart input the form describes.</returns>
    private ChartInput BuildChartInputFromForm(
        DateTime localDateTime, double latitude, double longitude, double? utcOffsetHours) =>
        new()
        {
            Name = ChartEntry.Name.Trim(),
            LocalDateTime = localDateTime,
            TimeZone = ChartEntry.TimeZone.Trim(),
            Latitude = latitude,
            Longitude = longitude,
            LocationName = ChartEntry.LocationName.Trim(),
            EventType = ChartEntry.EventType,
            Gender = ChartEntry.Gender,
            SourceNotes = string.IsNullOrWhiteSpace(ChartEntry.SourceNotes) ? null : ChartEntry.SourceNotes.Trim(),
            UseExplicitOffset = ChartEntry.UseExplicitOffset,
            UtcOffsetHours = utcOffsetHours,
            Notes = string.IsNullOrWhiteSpace(ChartEntry.Notes) ? null : ChartEntry.Notes.Trim(),
            Keywords = string.IsNullOrWhiteSpace(ChartEntry.Keywords) ? null : ChartEntry.Keywords.Trim(),
            HoraryQuestion = ChartEntry.IsHorary && !string.IsNullOrWhiteSpace(ChartEntry.HoraryQuestion)
                ? ChartEntry.HoraryQuestion.Trim()
                : null,
            HoraryCategory = ChartEntry.IsHorary && !string.IsNullOrWhiteSpace(ChartEntry.HoraryCategory)
                ? ChartEntry.HoraryCategory.Trim()
                : null,
            HouseSystem = ChartEntry.SelectedHouseSystem,
            IncludeMinorPoints = ChartEntry.IncludeMinorPoints,
        };

    /// <summary>
    /// Build a <see cref="ChartInput"/> from the form, calculate the chart,
    /// save to database, and update the saved charts list.
    /// </summary>
    private async Task CalculateAndSaveAsync(object? _)
    {
        try
        {
            StatusText = "Calculating...";

            // Parse time from the text field: a time of day only, the one check the Time box goes through
            if (!ChartEntryViewModel.TryParseTimeOfDay(ChartEntry.EventTime, out TimeSpan time, out string? timeError))
            {
                StatusText = timeError;
                return;
            }

            // Parse latitude and longitude from text fields: the one check every coordinate box goes through
            if (!ChartEntryViewModel.TryParseLatitude(ChartEntry.LatitudeText, out double latitude, out string? latitudeError))
            {
                StatusText = latitudeError;
                return;
            }

            if (!ChartEntryViewModel.TryParseLongitude(ChartEntry.LongitudeText, out double longitude, out string? longitudeError))
            {
                StatusText = longitudeError;
                return;
            }

            if (ChartEntry.Name.Trim().Length > 200)
            {
                StatusText = "Chart name is too long (maximum 200 characters).";
                return;
            }

            DateTime localDateTime = ChartEntry.EventDate.Date.Add(time);

            // Parse explicit UTC offset if enabled: decimal hours with a point, within ±14
            double? utcOffsetHours = null;
            if (ChartEntry.UseExplicitOffset)
            {
                if (!ChartEntryViewModel.TryParseUtcOffsetHours(ChartEntry.UtcOffsetHoursText, out double offset, out string? offsetError))
                {
                    StatusText = offsetError;
                    return;
                }
                utcOffsetHours = offset;
            }

            ChartInput input = BuildChartInputFromForm(localDateTime, latitude, longitude, utcOffsetHours);

            // Calculate chart (synchronous — Swiss Ephemeris is fast)
            ChartResult result = _chartEngine.Calculate(input);

            // Save to database — check for existing chart with the same name
            ChartEntity entity = ChartMapper.ToEntity(input, _ephemeris);
            ChartEntity? existing = await _repository.GetByNameAsync(input.Name);
            long id;

            if (existing is not null)
            {
                bool overwrite = await _dialogService.ConfirmAsync(
                    $"A chart named \"{input.Name}\" already exists.\n\nOverwrite it?",
                    "Chart Exists");

                if (!overwrite)
                {
                    StatusText = "Save cancelled — chart not overwritten.";
                    return;
                }

                // Update existing record, preserving original creation date
                entity.Id = existing.Id;
                entity.CreatedAt = existing.CreatedAt;
                await _repository.UpdateAsync(entity);
                id = existing.Id;

                _logger.Information("Chart updated (ID {Id})", id);
                _logger.Debug("Chart '{Name}' updated (ID {Id})", input.Name, id);
            }
            else
            {
                id = await _repository.SaveAsync(entity);

                _logger.Information("Chart saved as ID {Id}", id);
                _logger.Debug("Chart '{Name}' saved as ID {Id}", input.Name, id);
            }

            LastResult = result;
            HasResult = true;

            // Compute profection, firdaria, and zodiacal releasing if this is a birth chart.
            // Display state lives on Returns child (Category C absorbed in H-1.7);
            // setters are internal so this lifecycle method (same assembly) can write directly.
            if (string.Equals(result.Input.EventType, "Birth", StringComparison.OrdinalIgnoreCase))
            {
                ProfectionYearResult profection = ProfectionAnalyzer.Analyze(result, DateTime.Today);
                Returns.ProfectionSummary = profection.Summary;
                Returns.HasProfection = true;

                FirdariaResult firdaria = FirdariaAnalyzer.Analyze(result, DateTime.Today);
                Returns.FirdariaSummary = $"Firdaria: {firdaria.CurrentMajorRuler} period " +
                                          $"({CalendarDateFormatter.Format(firdaria.CurrentMajorStart, "yyyy")}\u2013{CalendarDateFormatter.Format(firdaria.CurrentMajorEnd, "yyyy")}), " +
                                          (firdaria.CurrentSubRuler.Length == 0 ? "undivided" : $"{firdaria.CurrentSubRuler} sub-period");

                ZodiacalReleasingResult zr = ZodiacalReleasingAnalyzer.Analyze(result, DateTime.Today);
                Returns.ZodiacalReleasingSummary = zr.Level1Periods.Count > 0
                    ? $"ZR from Spirit: {zr.CurrentL1Sign} ({zr.CurrentL1Angularity}) \u2192 {zr.CurrentL2Sign} sub"
                    : string.Empty;
            }
            else
            {
                Returns.ProfectionSummary = string.Empty;
                Returns.HasProfection = false;
                Returns.FirdariaSummary = string.Empty;
                Returns.ZodiacalReleasingSummary = string.Empty;
            }

            await RefreshSavedChartsAsync();

            // The Relocation and Electional pickers follow the Chart panel's chart, after the refresh, which
            // would otherwise restore the earlier picks by their Ids.
            Relocation.SelectedNatalChart = FindSavedChart(id);
            Electional.SelectedNatalChart = FindSavedChart(id);

            StatusText = $"Chart calculated and saved (ID {id}). " +
                         $"{result.Positions.Count} positions, {result.Aspects.Count} aspects, " +
                         $"{result.MidpointActivations.Count} midpoint activations. " +
                         $"Offset: {result.ResolvedOffsetLabel}.";
        }
        catch (TimeZoneNotFoundException ex)
        {
            StatusText = $"Invalid timezone: {DisplayText.ErrorMessage(ex)}";
            _logger.Warning(ex, "Invalid timezone specified");
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate and save chart");
        }
    }

    /// <summary>
    /// Export the last chart result as a PDF file using the file-save dialog.
    /// </summary>
    private async Task ExportPdfAsync(object? _)
    {
        if (LastResult is null) return;

        string defaultName = SanitiseFileName(LastResult.Input.Name) + ".pdf";
        string? path = await _fileDialogService.ShowSaveAsync(
            "Export Chart as PDF",
            FileDialogFileTypes.PdfOnly,
            defaultName,
            ".pdf");

        if (path is null) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.PdfSave);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            StatusText = "Exporting PDF...";
            ChartResult resultSnapshot = LastResult;
            string filePath = path;
            await Task.Run(() => ChartPdfFormatter.Save(resultSnapshot, filePath, token), token);
            StatusText = $"PDF exported: {filePath}";
            _logger.Information("PDF exported to {Path}", filePath);
        }
        catch (OperationCanceledException)
        {
            StatusText = "PDF export cancelled.";
            _logger.Information("PDF export cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"PDF export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export PDF");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.PdfSave);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the last chart result as a Markdown file using the file-save dialog.
    /// </summary>
    private async Task ExportMarkdownAsync(object? _)
    {
        if (LastResult is null) return;

        try
        {
            string defaultName = SanitiseFileName(LastResult.Input.Name) + ".md";
            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Chart as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting Markdown...";

                await ChartMarkdownFormatter.SaveAsync(LastResult, path);
                StatusText = $"Markdown exported: {path}";
                _logger.Information("Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Markdown export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export Markdown");
        }
    }

    /// <summary>
    /// Export the last chart result as the chart JSON (<see cref="ChartJsonFormatter"/>) using the
    /// file-save dialog: the chart's own JSON, not the database backup (O-D2 option b, branch O-6, 1.3.4).
    /// </summary>
    private async Task ExportChartJsonAsync(object? _)
    {
        if (LastResult is null) return;

        try
        {
            string defaultName = SanitiseFileName(LastResult.Input.Name) + ".json";
            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Chart as JSON",
                FileDialogFileTypes.JsonOnly,
                defaultName,
                ".json");

            if (path is not null)
            {
                StatusText = "Exporting JSON...";

                await ChartJsonFormatter.SaveAsync(LastResult, path);
                StatusText = $"JSON exported: {path}";
                _logger.Information("Chart JSON exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"JSON export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export the chart JSON");
        }
    }

    /// <summary>
    /// Export the last chart result as both PDF and Markdown to the same directory.
    /// Prompts for the PDF first, then saves the Markdown beside it under the PDF's
    /// own name with <c>.md</c>. When that Markdown file already exists it asks
    /// before replacing it, and on No writes the PDF only and says so in the status line.
    /// </summary>
    private async Task ExportBothAsync(object? _)
    {
        if (LastResult is null) return;

        string baseName = SanitiseFileName(LastResult.Input.Name);
        string? path = await _fileDialogService.ShowSaveAsync(
            "Export Chart (PDF + Markdown)",
            FileDialogFileTypes.PdfOnly,
            baseName + ".pdf",
            ".pdf");

        if (path is null) return;

        string mdPath = Path.ChangeExtension(path, ".md");
        bool writeMarkdown = !File.Exists(mdPath)
            || await _dialogService.ConfirmAsync(
                $"\"{Path.GetFileName(mdPath)}\" already exists.\n\nReplace it?",
                "Export Chart (PDF + Markdown)");

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.PdfSave);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            StatusText = "Exporting PDF + Markdown...";

            string directory = Path.GetDirectoryName(path)!;
            string pdfPath = path;
            ChartResult resultSnapshot = LastResult;

            await Task.Run(() => ChartPdfFormatter.Save(resultSnapshot, pdfPath, token), token);
            token.ThrowIfCancellationRequested();
            if (writeMarkdown)
            {
                await ChartMarkdownFormatter.SaveAsync(resultSnapshot, mdPath);

                StatusText = $"Exported to: {directory}";
                _logger.Information(
                    "Both formats exported to {Directory}: {Pdf}, {Md}",
                    directory, Path.GetFileName(pdfPath), Path.GetFileName(mdPath));
            }
            else
            {
                StatusText = $"PDF exported: {pdfPath} (Markdown not written: {Path.GetFileName(mdPath)} already exists)";
                _logger.Information(
                    "PDF exported to {Pdf}; the existing {Md} was kept", pdfPath, mdPath);
            }
        }
        catch (OperationCanceledException)
        {
            StatusText = "PDF + Markdown export cancelled.";
            _logger.Information("PDF + Markdown export cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export both formats");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.PdfSave);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Load a chart from the saved list into the form fields.
    /// The parameter should be a <see cref="ChartEntity"/>.
    /// </summary>
    private Task LoadChartAsync(object? parameter)
    {
        if (parameter is not ChartEntity entity) return Task.CompletedTask;

        try
        {
            // Populate form fields from entity
            ChartEntry.PopulateFrom(entity);

            // Clear location lookup state
            ChartEntryLocationSearch.Clear();

            // Recalculate for export readiness, with the session's minor-points switch, which the row does not hold
            ChartInput input = ChartMapper.ToInput(entity, ChartEntry.IncludeMinorPoints);
            ChartResult result = _chartEngine.Calculate(input);
            LastResult = result;
            HasResult = true;

            // The Relocation and Electional pickers follow the Chart panel's chart: AllSavedCharts' instance
            // with the loaded Id, never the saved-charts list's instance Load was handed, which the pickers'
            // items do not hold.
            Relocation.SelectedNatalChart = FindSavedChart(entity.Id);
            Electional.SelectedNatalChart = FindSavedChart(entity.Id);

            // Compute profection, firdaria, and zodiacal releasing if this is a birth chart.
            // Display state lives on Returns child (Category C absorbed in H-1.7);
            // setters are internal so this lifecycle method (same assembly) can write directly.
            if (string.Equals(result.Input.EventType, "Birth", StringComparison.OrdinalIgnoreCase))
            {
                ProfectionYearResult profection = ProfectionAnalyzer.Analyze(result, DateTime.Today);
                Returns.ProfectionSummary = profection.Summary;
                Returns.HasProfection = true;

                FirdariaResult firdaria = FirdariaAnalyzer.Analyze(result, DateTime.Today);
                Returns.FirdariaSummary = $"Firdaria: {firdaria.CurrentMajorRuler} period " +
                                          $"({CalendarDateFormatter.Format(firdaria.CurrentMajorStart, "yyyy")}\u2013{CalendarDateFormatter.Format(firdaria.CurrentMajorEnd, "yyyy")}), " +
                                          (firdaria.CurrentSubRuler.Length == 0 ? "undivided" : $"{firdaria.CurrentSubRuler} sub-period");

                ZodiacalReleasingResult zr = ZodiacalReleasingAnalyzer.Analyze(result, DateTime.Today);
                Returns.ZodiacalReleasingSummary = zr.Level1Periods.Count > 0
                    ? $"ZR from Spirit: {zr.CurrentL1Sign} ({zr.CurrentL1Angularity}) \u2192 {zr.CurrentL2Sign} sub"
                    : string.Empty;
            }
            else
            {
                Returns.ProfectionSummary = string.Empty;
                Returns.HasProfection = false;
                Returns.FirdariaSummary = string.Empty;
                Returns.ZodiacalReleasingSummary = string.Empty;
            }

            StatusText = $"Loaded '{entity.Name}' — {result.Positions.Count} positions, " +
                         $"{result.Aspects.Count} aspects. Offset: {result.ResolvedOffsetLabel}.";

            _logger.Information("Loaded chart (ID {Id})", entity.Id);
            _logger.Debug("Loaded chart '{Name}' (ID {Id})", entity.Name, entity.Id);
        }
        catch (Exception ex)
        {
            StatusText = $"Load error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to load chart {Id}", entity.Id);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Delete a chart from the database. The parameter should be a <see cref="ChartEntity"/>.
    /// Shows a confirmation dialog before deleting.
    /// </summary>
    private async Task DeleteChartAsync(object? parameter)
    {
        if (parameter is not ChartEntity entity) return;

        bool confirmed = await _dialogService.ConfirmAsync(
            $"Delete chart '{entity.Name}'?\n\nThis cannot be undone.",
            "Confirm Delete");

        if (!confirmed) return;

        try
        {
            bool deleted = await _repository.DeleteAsync(entity.Id);

            if (deleted)
            {
                await RefreshSavedChartsAsync();
                StatusText = $"Deleted '{entity.Name}'.";
                _logger.Information("Deleted chart (ID {Id})", entity.Id);
                _logger.Debug("Deleted chart '{Name}' (ID {Id})", entity.Name, entity.Id);
            }
            else
            {
                StatusText = $"Chart '{entity.Name}' not found.";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Delete error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to delete chart {Id}", entity.Id);
        }
    }

    /// <summary>
    /// Search/filter saved charts using the current filter and sort settings.
    /// Builds a <see cref="ChartQueryOptions"/> from the filter properties
    /// and delegates to <see cref="IChartRepository.QueryAsync"/>.
    /// </summary>
    private async Task SearchChartsAsync(object? _)
    {
        try
        {
            ChartQueryOptions options = new()
            {
                KeywordSearch = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
                EventType = string.IsNullOrWhiteSpace(FilterEventType) ? null : FilterEventType,
                DateFrom = FilterDateFrom,
                DateTo = FilterDateTo,
                SortField = SortField,
                SortAscending = SortAscending,
            };

            IReadOnlyList<ChartEntity> charts = await _repository.QueryAsync(options);

            SavedCharts.Clear();
            foreach (ChartEntity chart in charts)
            {
                SavedCharts.Add(chart);
            }

            StatusText = $"Found {charts.Count} chart(s).";
        }
        catch (Exception ex)
        {
            StatusText = $"Search error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to search charts");
        }
    }

    /// <summary>
    /// Calculate synastry between the current chart (Person A) and the selected
    /// Person B chart from the saved charts list.
    /// </summary>
    private async Task CalculateSynastryAsync(object? _)
    {
        if (LastResult is null || Synastry.PersonB is null) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.Synastry);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            StatusText = "Calculating synastry...";

            // Person A is the currently calculated chart
            ChartResult personAResult = LastResult;

            // Person B: snapshot selection, convert to ChartInput, calculate.
            // Snapshot prevents races if the user swaps Person B mid-flight.
            ChartEntity selectedPersonB = Synastry.PersonB;
            ChartInput personBInput = ChartMapper.ToInput(selectedPersonB, ChartEntry.IncludeMinorPoints);
            RelationshipType relationshipType = Synastry.SelectedRelationshipType;
            string? relationshipNotes = string.IsNullOrWhiteSpace(Synastry.RelationshipNotes)
                ? null : Synastry.RelationshipNotes.Trim();

            // Offload CPU-bound work to a background thread
            (ChartResult personBResult, SynastryResult synastryResult) = await Task.Run(() =>
            {
                ChartResult bResult = _chartEngine.Calculate(personBInput, token);
                SynastryResult sResult = _synastryService.Calculate(
                    personAResult, bResult,
                    relationshipType,
                    relationshipNotes,
                    token);
                return (bResult, sResult);
            }, token);

            // Cache Person B's computed chart on the child so the composite
            // command (and future callers) can avoid recomputing. Write only
            // if Person B still matches the snapshot — a selection change
            // while this task was in flight must not overwrite the new one.
            if (ReferenceEquals(Synastry.PersonB, selectedPersonB))
                Synastry.PersonBChart = personBResult;

            Synastry.SynastryResult = synastryResult;

            StatusText = $"Synastry calculated: {personAResult.Input.Name} & " +
                         $"{personBResult.Input.Name} \u2014 " +
                         $"{synastryResult.InterChartAspects.Count} inter-chart aspects.";

            _logger.Information(
                "Synastry calculated: '{PersonA}' & '{PersonB}' — {Count} inter-chart aspects",
                personAResult.Input.Name, personBResult.Input.Name,
                synastryResult.InterChartAspects.Count);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Synastry calculation cancelled.";
            _logger.Information("Synastry calculation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Synastry error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate synastry");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.Synastry);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current synastry result as a Markdown file using the file-save dialog.
    /// </summary>
    private async Task ExportSynastryMarkdownAsync(object? _)
    {
        SynastryResult? current = Synastry.SynastryResult;
        if (current is null) return;

        try
        {
            string personAName = SanitiseFileName(current.PersonA.Input.Name);
            string personBName = SanitiseFileName(current.PersonB.Input.Name);
            string defaultName = $"{personAName}_{personBName}_Synastry.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Synastry as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting synastry Markdown...";
                await SynastryMarkdownFormatter.SaveAsync(current, path);
                StatusText = $"Synastry exported: {path}";
                _logger.Information("Synastry Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Synastry export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export synastry Markdown");
        }
    }

    /// <summary>
    /// Calculate transits for the current natal chart over the specified date range.
    /// Uses the TransitService (which only requires IEphemerisService).
    /// </summary>
    private async Task CalculateTransitAsync(object? _)
    {
        if (LastResult is null) return;

        // The panel's own check, before anything starts; the same date twice is a report of one day (1.7.2)
        if (RefuseEndDateBeforeStart(Transits.StartDate, Transits.EndDate)) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.Transits);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            StatusText = "Calculating transits...";

            TransitInput input = new()
            {
                NatalChart = LastResult,
                ReportStartDate = DateOnly.FromDateTime(Transits.StartDate),
                ReportEndDate = DateOnly.FromDateTime(Transits.EndDate),
                // The minor aspects are the transits' only: their box is disabled while Transits is
                // unchecked, and its value is not passed then (branch ModesPanel, 1.4.0 item 2)
                IncludeMinorAspects = Transits.IncludeTransits && Transits.IncludeMinorAspects,
                IncludeTransits = Transits.IncludeTransits,
                IncludeSolarArcs = Transits.IncludeSolarArcs,
                IncludeSecondaryProgressions = Transits.IncludeSecondaryProgressions,
                // The Directions panel's settings, so the period's hits and the whole-life list
                // agree (branch PrimaryPanel, 1.5.0)
                IncludePrimaryDirections = Transits.IncludePrimaryDirections,
                PrimaryDirectionSettings = Transits.IncludePrimaryDirections
                    ? Directions.Settings
                    : PrimaryDirectionSettings.Default,
            };

            TransitResult transitResult = await Task.Run(
                () => _transitService.Calculate(input, token), token);
            Transits.TransitResult = transitResult;

            // A result without transits is named for its techniques and counts its hits
            // ("Solar Arcs calculated: 37 solar arc hits.")
            StatusText = transitResult.IncludesTransits
                ? $"Transits calculated: {transitResult.ActiveTransits.Count} active transits, " +
                  $"{transitResult.MidpointActivations.Count} midpoint activations."
                : $"{TransitReportNaming.DialogSubject(transitResult)} calculated: {DirectionHitCounts(transitResult)}.";

            _logger.Information(
                "Transits calculated for '{Name}': {Transits} active, {Midpoints} midpoints",
                LastResult.Input.Name, transitResult.ActiveTransits.Count,
                transitResult.MidpointActivations.Count);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Transit calculation cancelled.";
            _logger.Information("Transit calculation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Transit error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate transits");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.Transits);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// The hit counts of a result's solar arcs, secondary progressions and primary directions, for the
    /// status line of a result without transits: "37 solar arc hits", "1 progressed hit", "4 primary
    /// direction hits", comma-joined, in the report's order.
    /// </summary>
    /// <param name="result">The transit result.</param>
    /// <returns>The counts.</returns>
    private static string DirectionHitCounts(TransitResult result)
    {
        List<string> counts = [];
        if (result.SolarArcs is not null)
        {
            int count = result.SolarArcHits.Count;
            counts.Add(count == 1 ? "1 solar arc hit" : $"{count} solar arc hits");
        }

        if (result.Progressions is not null)
        {
            int count = result.ProgressedHits.Count;
            counts.Add(count == 1 ? "1 progressed hit" : $"{count} progressed hits");
        }

        if (result.PrimaryDirections is not null)
        {
            int count = result.PrimaryDirectionHits.Count;
            counts.Add(count == 1 ? "1 primary direction hit" : $"{count} primary direction hits");
        }

        return string.Join(", ", counts);
    }

    /// <summary>
    /// Export the current transit result as a Markdown file using the file-save dialog.
    /// </summary>
    private async Task ExportTransitMarkdownAsync(object? _)
    {
        TransitResult? currentResult = Transits.TransitResult;
        if (currentResult is null) return;

        try
        {
            string personName = SanitiseFileName(currentResult.NatalChart.Input.Name);
            string startDate = FileNameDate(currentResult.ReportStartDate);
            string endDate = FileNameDate(currentResult.ReportEndDate);
            // The stem and the dialog's subject name the techniques the result holds (branch Modes,
            // 1.4.0 item 2): _Transits_ whenever the scan ran, else _SolarArcs_, _Progressions_ or
            // _SolarArcsProgressions_
            string defaultName = $"{personName}_{TransitReportNaming.FileStem(currentResult)}_{startDate}_{endDate}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                $"Export {TransitReportNaming.DialogSubject(currentResult)} as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting transit Markdown...";

                string? birthTimeSource = currentResult.NatalChart.Input.SourceNotes;
                string? context = string.IsNullOrWhiteSpace(Transits.InterpretationContext)
                    ? null
                    : Transits.InterpretationContext.Trim();

                await TransitMarkdownFormatter.SaveAsync(
                    currentResult, path, birthTimeSource, context);

                StatusText = $"Transit Markdown exported: {path}";
                _logger.Information("Transit Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Transit export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export transit Markdown");
        }
    }

    /// <summary>
    /// Export a daily horoscope Markdown file from the current transit result.
    /// Builds per-day data including Moon position, phase, VoC, and active transit
    /// snapshots, then renders as a per-day digest for AI interpretation.
    /// </summary>
    private async Task ExportDailyHoroscopeMarkdownAsync(object? _)
    {
        TransitResult? currentResult = Transits.TransitResult;
        if (currentResult is null) return;

        try
        {
            string personName = SanitiseFileName(currentResult.NatalChart.Input.Name);
            string startDate = FileNameDate(currentResult.ReportStartDate);
            string endDate = FileNameDate(currentResult.ReportEndDate);
            string defaultName = $"{personName}_DailyHoroscope_{startDate}_{endDate}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Daily Horoscope as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Building daily horoscope data...";

                string timeZoneId = currentResult.NatalChart.Input.TimeZone;
                VoidOfCourseScanner vocScanner = new(_ephemeris);

                IReadOnlyList<DailyHoroscopeDay> days =
                    new DailyHoroscopeService(_ephemeris, vocScanner).BuildDays(currentResult, timeZoneId);

                string? birthTimeSource = currentResult.NatalChart.Input.SourceNotes;
                string? context = string.IsNullOrWhiteSpace(Transits.InterpretationContext)
                    ? null
                    : Transits.InterpretationContext.Trim();

                StatusText = "Exporting daily horoscope Markdown...";

                await DailyHoroscopeMarkdownFormatter.SaveAsync(
                    currentResult, days, path,
                    birthTimeSource, context, timeZoneId);

                StatusText = $"Daily horoscope exported: {path}";
                _logger.Information("Daily horoscope Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Daily horoscope export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export daily horoscope Markdown");
        }
    }

    /// <summary>
    /// Export a chronological transit timeline as a Markdown file using the file-save dialog.
    /// Produces a date-sorted table of exact passes, stations, ingresses, and midpoint
    /// activations from the current transit result.
    /// </summary>
    /// <remarks>
    /// The header carries the natal chart's birth-time source and the Transits panel's
    /// interpretation context, trimmed, or none when blank, as the transit export from the same
    /// panel does (E2-6). Until 1.3.3 the timeline export passed neither.
    /// </remarks>
    /// <param name="_">The command parameter (unused).</param>
    private async Task ExportTransitTimelineMarkdownAsync(object? _)
    {
        TransitResult? currentResult = Transits.TransitResult;
        if (currentResult is null) return;

        try
        {
            string personName = SanitiseFileName(currentResult.NatalChart.Input.Name);
            string startDate = FileNameDate(currentResult.ReportStartDate);
            string endDate = FileNameDate(currentResult.ReportEndDate);
            string defaultName = $"{personName}_TransitTimeline_{startDate}_{endDate}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Transit Timeline as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting transit timeline...";

                // The birth-time source and the panel's context, as the transit export passes them (E2-6).
                string? birthTimeSource = currentResult.NatalChart.Input.SourceNotes;
                string? context = string.IsNullOrWhiteSpace(Transits.InterpretationContext)
                    ? null
                    : Transits.InterpretationContext.Trim();

                await TransitTimelineMarkdownFormatter.SaveAsync(
                    currentResult, path, birthTimeSource, context);

                StatusText = $"Transit timeline exported: {path}";
                _logger.Information("Transit timeline Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Transit timeline export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export transit timeline Markdown");
        }
    }

    /// <summary>
    /// Calculate electional chart using the form fields.
    /// Requires a natal chart to be calculated first.
    /// </summary>
    private async Task CalculateElectionalAsync(object? _)
    {
        // The panel's own check, before anything starts; the same date twice is a search of one day (1.7.2)
        if (RefuseEndDateBeforeStart(Electional.StartDate, Electional.EndDate)) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.Electional);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            // Resolve natal chart: dropdown selection → LastResult → form fields fallback
            ChartResult? resolvedNatal = null;

            if (Electional.SelectedNatalChart is not null)
            {
                StatusText = "Calculating natal chart from selection...";
                ChartInput dropdownInput = ChartMapper.ToInput(Electional.SelectedNatalChart, ChartEntry.IncludeMinorPoints);
                resolvedNatal = await Task.Run(() => _chartEngine.Calculate(dropdownInput), token);

                _logger.Information(
                    "Natal chart calculated from dropdown selection '{Name}' (electional prerequisite)",
                    Electional.SelectedNatalChart.Name);
            }
            else if (LastResult is not null)
            {
                resolvedNatal = LastResult;
            }
            else
            {
                // Fall back: auto-calculate from form fields (original behaviour). The cast runs off the UI
                // thread, so the Points box can move while it runs; the landed chart is then recast at once
                // (RecastForMinorPoints, below) and the search scores the chart on screen, the run's own status
                // lines overwriting the recast's (1.6.1, item D, branch ElectionalPicker).
                StatusText = "Calculating natal chart from form fields...";

                // The same check as Calculate & Save's, with this path's own wording
                if (!ChartEntryViewModel.TryParseTimeOfDay(ChartEntry.EventTime, out TimeSpan natalTime, out string? _))
                {
                    StatusText = "Invalid natal time format. Use HH:mm:ss.";
                    return;
                }

                if (!ChartEntryViewModel.TryParseLatitude(ChartEntry.LatitudeText, out double natalLat, out string? natalLatError))
                {
                    StatusText = $"Natal form: {natalLatError}";
                    return;
                }

                if (!ChartEntryViewModel.TryParseLongitude(ChartEntry.LongitudeText, out double natalLon, out string? natalLonError))
                {
                    StatusText = $"Natal form: {natalLonError}";
                    return;
                }

                double? utcOffsetHours = null;
                if (ChartEntry.UseExplicitOffset)
                {
                    if (!ChartEntryViewModel.TryParseUtcOffsetHours(ChartEntry.UtcOffsetHoursText, out double offset, out string? offsetError))
                    {
                        StatusText = offsetError;
                        return;
                    }
                    utcOffsetHours = offset;
                }

                // The form's own chart, as Calculate & Save builds it: its type, house system and horary fields
                ChartInput natalInput = BuildChartInputFromForm(
                    ChartEntry.EventDate.Date.Add(natalTime), natalLat, natalLon, utcOffsetHours);

                resolvedNatal = await Task.Run(() => _chartEngine.Calculate(natalInput), token);
                LastResult = resolvedNatal;
                HasResult = true;

                // A box moved during the cast recasts the landed chart; an unmoved one returns with no cast.
                RecastForMinorPoints();
                resolvedNatal = LastResult ?? resolvedNatal;

                _logger.Information(
                    "Natal chart auto-calculated for '{Name}' (electional prerequisite)", ChartEntry.Name);
            }

            if (resolvedNatal is null)
            {
                StatusText = "Select a natal chart from the dropdown, calculate one, or fill in the natal form.";
                return;
            }

            StatusText = "Calculating electional chart (this may take a moment)...";

            // Parse electional latitude and longitude, within ±90 and ±180 since 1.4.0
            if (!ChartEntryViewModel.TryParseLatitude(Electional.LatitudeText, out double elLat, out string? elLatError))
            {
                StatusText = $"Electional location: {elLatError}";
                return;
            }

            if (!ChartEntryViewModel.TryParseLongitude(Electional.LongitudeText, out double elLon, out string? elLonError))
            {
                StatusText = $"Electional location: {elLonError}";
                return;
            }

            // The time window: both boxes empty for none, both filled for the window; anything else stops here
            if (!TryReadElectionalTimeWindow(out TimeConstraint? timeConstraint, out string? windowError))
            {
                StatusText = windowError;
                return;
            }

            ElectionalInput input = new()
            {
                NatalChart = resolvedNatal,
                ElectionType = Electional.ElectionType,
                ElectionGoal = Electional.Goal.Trim(),
                RangeStart = DateOnly.FromDateTime(Electional.StartDate),
                RangeEnd = DateOnly.FromDateTime(Electional.EndDate),
                Latitude = elLat,
                Longitude = elLon,
                LocationName = Electional.LocationName.Trim(),
                TimeZone = Electional.TimeZone.Trim(),
                CandidateCount = Electional.CandidateCount,
                AvoidMercuryRetrograde = Electional.AvoidMercuryRetrograde,
                ConstraintNotes = string.IsNullOrWhiteSpace(Electional.ConstraintNotes)
                    ? null : Electional.ConstraintNotes.Trim(),
                TimeConstraint = timeConstraint,
                AllowedDays = Electional.GetAllowedDays(),
            };

            // Run on background thread — this can take 10+ seconds
            ElectionalResult calculated = await Task.Run(
                () => _electionalService.Calculate(input, token), token);
            Electional.ElectionalResult = calculated;

            CandidateWindow? best = calculated.Candidates.FirstOrDefault();
            StatusText = best is not null
                ? $"Electional complete — best: {best.LocalDateTime:ddd, dd MMM yyyy h:mm tt} (score {best.Score:0.##})"
                : "Electional complete — no qualifying candidates found.";

            _logger.Information(
                "Electional calculated: {Count} candidates for '{Name}'",
                calculated.Candidates.Count, ChartEntry.Name);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Electional calculation cancelled.";
            _logger.Information("Electional calculation cancelled by user");
        }
        catch (Exception ex)
        {
            // An ArgumentException naming its parameter ends its message with the framework's own
            // " (Parameter '…')", which says nothing to the user; the line gives the message without it,
            // through the one helper every status line uses since 1.7.2.
            StatusText = $"Electional error: {DisplayText.ErrorMessage(ex)}";
            Electional.ElectionalResult = null;
            _logger.Error(ex, "Failed to calculate electional chart");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.Electional);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the electional result as a Markdown file via the file-save dialog,
    /// offering a default name from the natal chart the search was cast for.
    /// </summary>
    private async Task ExportElectionalMarkdownAsync(object? _)
    {
        ElectionalResult? result = Electional.ElectionalResult;
        if (result is null) return;

        try
        {
            string baseName = SanitiseFileName(result.NatalChart.Input.Name);
            string startDate = FileNameDate(DateOnly.FromDateTime(Electional.StartDate));
            string endDate = FileNameDate(DateOnly.FromDateTime(Electional.EndDate));
            string defaultName = $"{baseName}_Electional_{startDate}_{endDate}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Electional as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting electional Markdown...";
                await ElectionalMarkdownFormatter.SaveAsync(result, path);
                StatusText = $"Electional exported: {path}";
                _logger.Information("Electional Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Electional export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export electional Markdown");
        }
    }

    /// <summary>
    /// Copy the natal chart's location fields to the electional location fields: the picked chart's place and
    /// its own time zone, else the Chart panel's fields.
    /// </summary>
    /// <remarks>
    /// The picked chart's zone is the chart's own, as the form branch copies the Chart panel's; the GeoTimeZone
    /// lookup from its coordinates runs only where the stored zone is blank (1.6.1, item C, decision 4, branch
    /// ElectionalPicker). Since the picker follows Load and Calculate &amp; Save, the picked branch is the usual
    /// one. Until 1.6.1 the zone was always looked up.
    /// </remarks>
    private void UseNatalLocation()
    {
        // If a natal chart is selected in the dropdown, use its location
        if (Electional.SelectedNatalChart is not null)
        {
            Electional.LocationName = Electional.SelectedNatalChart.LocationName;
            Electional.LatitudeText = Electional.SelectedNatalChart.Latitude
                .ToString(CultureInfo.InvariantCulture);
            Electional.LongitudeText = Electional.SelectedNatalChart.Longitude
                .ToString(CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(Electional.SelectedNatalChart.TimeZone))
            {
                Electional.TimeZone = Electional.SelectedNatalChart.TimeZone;
            }
            else
            {
                try
                {
                    GeoTimeZone.TimeZoneResult tzResult = GeoTimeZone.TimeZoneLookup.GetTimeZone(
                        Electional.SelectedNatalChart.Latitude,
                        Electional.SelectedNatalChart.Longitude);
                    if (!string.IsNullOrEmpty(tzResult.Result))
                        Electional.TimeZone = TimezoneProvider.FindOfferedZone(tzResult.Result, TimeZoneRules) ?? tzResult.Result;
                }
                catch (Exception ex)
                {
                    _logger.Warning(ex, "Failed to determine timezone for saved chart location");
                }
            }

            Electional.LocationSearch.ClearResults();
            StatusText = $"Using location from {Electional.SelectedNatalChart.Name}.";
            return;
        }

        Electional.LocationSearch.SearchText = ChartEntry.LocationName;
        Electional.LatitudeText = ChartEntry.LatitudeText;
        Electional.LongitudeText = ChartEntry.LongitudeText;
        Electional.LocationName = ChartEntry.LocationName;
        Electional.TimeZone = ChartEntry.TimeZone;
        Electional.LocationSearch.ClearResults();
        StatusText = "Electional location copied from natal chart.";
    }

    /// <summary>
    /// Calculate a composite (midpoint) chart between Person A (current chart)
    /// and selected Person B. No place is passed: the composite's houses are
    /// cast for the midpoint of the two Midheavens at the mean birth latitude
    /// (branch L3, 1.3.4).
    /// </summary>
    private async Task CalculateCompositeAsync(object? _)
    {
        if (LastResult is null || Synastry.PersonB is null) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.Composite);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            StatusText = "Calculating composite chart...";

            ChartResult personAResult = LastResult;
            ChartEntity selectedPersonB = Synastry.PersonB;
            ChartInput personBInput = ChartMapper.ToInput(selectedPersonB, ChartEntry.IncludeMinorPoints);

            (ChartResult personBResult, CompositeResult compositeResult) = await Task.Run(() =>
            {
                ChartResult bResult = _chartEngine.Calculate(personBInput, token);
                CompositeResult cResult = _compositeService.Calculate(personAResult, bResult, token);
                return (bResult, cResult);
            }, token);

            // Cache Person B's computed chart on the child so subsequent
            // synastry runs can reuse it. Only overwrite if Person B still
            // matches the snapshot we computed against.
            if (ReferenceEquals(Synastry.PersonB, selectedPersonB))
                Synastry.PersonBChart = personBResult;

            Synastry.CompositeResult = compositeResult;

            StatusText = $"Composite calculated: {personAResult.Input.Name} & " +
                         $"{personBResult.Input.Name} \u2014 " +
                         $"{compositeResult.CompositeChart.Positions.Count} positions, " +
                         $"{compositeResult.CompositeChart.Aspects.Count} aspects.";

            _logger.Information(
                "Composite calculated: '{PersonA}' & '{PersonB}'",
                personAResult.Input.Name, personBResult.Input.Name);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Composite calculation cancelled.";
            _logger.Information("Composite calculation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Composite error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate composite chart");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.Composite);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current composite result as a Markdown file using the file-save dialog.
    /// </summary>
    private async Task ExportCompositeMarkdownAsync(object? _)
    {
        CompositeResult? current = Synastry.CompositeResult;
        if (current is null) return;

        try
        {
            string personAName = SanitiseFileName(current.ChartA.Input.Name);
            string personBName = SanitiseFileName(current.ChartB.Input.Name);
            string defaultName = $"{personAName}_{personBName}_Composite.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Composite as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting composite Markdown...";

                string? compositeContext =
                    string.IsNullOrWhiteSpace(Synastry.CompositeInterpretationContext)
                        ? null
                        : Synastry.CompositeInterpretationContext.Trim();

                await CompositeMarkdownFormatter.SaveAsync(
                    current, path, compositeContext);
                StatusText = $"Composite exported: {path}";
                _logger.Information("Composite Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Composite export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export composite Markdown");
        }
    }

    /// <summary>
    /// Cast the Davison chart of Person A (the Chart panel's chart) and the selected Person B: a real chart for the
    /// midpoint moment of the two births at the midpoint place, cast by <see cref="IDavisonService"/> with the
    /// Points box's switch (branch DavisonPanel, 1.7.0). Person B is cast inside the same <c>Task.Run</c> with the
    /// Points box, as the Composite does, and cached when B is unchanged.
    /// </summary>
    private async Task CalculateDavisonAsync(object? _)
    {
        if (LastResult is null || Synastry.PersonB is null) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.Davison);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            StatusText = "Calculating Davison chart...";

            ChartResult personAResult = LastResult;
            ChartEntity selectedPersonB = Synastry.PersonB;
            bool includeMinorPoints = ChartEntry.IncludeMinorPoints;
            ChartInput personBInput = ChartMapper.ToInput(selectedPersonB, includeMinorPoints);

            (ChartResult personBResult, DavisonResult davisonResult) = await Task.Run(() =>
            {
                ChartResult bResult = _chartEngine.Calculate(personBInput, token);
                DavisonResult dResult = _davisonService.Calculate(personAResult, bResult, includeMinorPoints, token);
                return (bResult, dResult);
            }, token);

            if (ReferenceEquals(Synastry.PersonB, selectedPersonB))
                Synastry.PersonBChart = personBResult;

            Synastry.DavisonResult = davisonResult;

            StatusText = $"Davison chart calculated for {personAResult.Input.Name} & {personBResult.Input.Name}: " +
                         $"{CalendarDateFormatter.DateTime(davisonResult.MidpointUtc)} UTC at {davisonResult.MidpointLocationDescription}.";

            _logger.Information(
                "Davison chart calculated: '{PersonA}' & '{PersonB}'",
                personAResult.Input.Name, personBResult.Input.Name);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Davison calculation cancelled.";
            _logger.Information("Davison calculation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Davison error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate the Davison chart");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.Davison);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current Davison result as a Markdown file using the file-save dialog, under
    /// <c>{A}_{B}_Davison.md</c> (branch DavisonPanel, 1.7.0).
    /// </summary>
    private async Task ExportDavisonMarkdownAsync(object? _)
    {
        DavisonResult? current = Synastry.DavisonResult;
        if (current is null) return;

        try
        {
            string personAName = SanitiseFileName(current.ChartA.Input.Name);
            string personBName = SanitiseFileName(current.ChartB.Input.Name);
            string defaultName = $"{personAName}_{personBName}_Davison.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Davison as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting Davison Markdown...";

                string? davisonContext =
                    string.IsNullOrWhiteSpace(Synastry.DavisonInterpretationContext)
                        ? null
                        : Synastry.DavisonInterpretationContext.Trim();

                await DavisonMarkdownFormatter.SaveAsync(current, path, davisonContext);
                StatusText = $"Davison exported: {path}";
                _logger.Information("Davison Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Davison export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export Davison Markdown");
        }
    }

    /// <summary>
    /// Calculate a solar return chart for the current natal chart and the
    /// specified year at the natal location.
    /// </summary>
    private async Task CalculateSolarReturnAsync(object? _)
    {
        if (LastResult is null) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.SolarReturn);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            StatusText = $"Calculating solar return for {Returns.SolarReturnYear}...";

            ChartResult natalResult = LastResult;
            int year = Returns.SolarReturnYear;

            SolarReturnResult solarReturn = await Task.Run(() =>
                _solarReturnService.Calculate(
                    natalResult, year,
                    natalResult.Input.Latitude,
                    natalResult.Input.Longitude,
                    natalResult.Input.LocationName,
                    natalResult.Input.TimeZone,
                    token), token);

            Returns.CurrentSolarReturnResult = solarReturn;

            StatusText = $"Solar return {year}: " +
                         $"{solarReturn.ReturnChart.Input.Name} \u2014 " +
                         $"{solarReturn.ReturnChart.Positions.Count} positions, " +
                         $"{solarReturn.ReturnChart.Aspects.Count} aspects.";

            _logger.Information(
                "Solar return calculated for '{Name}' year {Year}",
                natalResult.Input.Name, year);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Solar return calculation cancelled.";
            _logger.Information("Solar return calculation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Solar return error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate solar return");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.SolarReturn);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current solar return result as a Markdown file using the file-save dialog.
    /// </summary>
    private async Task ExportSolarReturnMarkdownAsync(object? _)
    {
        SolarReturnResult? solarReturn = Returns.CurrentSolarReturnResult;
        if (solarReturn is null) return;

        try
        {
            string name = SanitiseFileName(solarReturn.NatalChart.Input.Name);
            string defaultName = $"{name}_SolarReturn_{solarReturn.Year}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Solar Return as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting solar return Markdown...";

                string? solarContext = string.IsNullOrWhiteSpace(Returns.SolarReturnInterpretationContext)
                    ? null
                    : Returns.SolarReturnInterpretationContext.Trim();

                await SolarReturnMarkdownFormatter.SaveAsync(
                    solarReturn, path, solarContext);
                StatusText = $"Solar return exported: {path}";
                _logger.Information("Solar return Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Solar return export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export solar return Markdown");
        }
    }

    /// <summary>
    /// Calculate lunar returns for the current natal chart over the specified
    /// date range at the natal location.
    /// </summary>
    private async Task CalculateLunarReturnAsync(object? _)
    {
        if (LastResult is null) return;

        // The panel's own check, before anything starts (1.7.2). The range's bounds are two midnights, so the
        // same date twice holds no time and stays refused, as the engine refuses it, in the user's words.
        if (RefuseEndDateBeforeStart(Returns.LunarReturnStartDate, Returns.LunarReturnEndDate)) return;
        if (Returns.LunarReturnEndDate <= Returns.LunarReturnStartDate)
        {
            StatusText = EndDateNotAfterStartMessage;
            return;
        }

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.LunarReturn);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            StatusText = "Calculating lunar returns...";

            ChartResult natalResult = LastResult;
            DateTime startDate = Returns.LunarReturnStartDate;
            DateTime endDate = Returns.LunarReturnEndDate;

            IReadOnlyList<LunarReturnResult> results = await Task.Run(() =>
                _lunarReturnService.Calculate(
                    natalResult, startDate, endDate,
                    natalResult.Input.Latitude,
                    natalResult.Input.Longitude,
                    natalResult.Input.LocationName,
                    natalResult.Input.TimeZone,
                    token),
                token);

            Returns.LunarReturnResults = results;
            Returns.SelectedLunarReturn = results.Count > 0 ? results[0] : null;

            StatusText = $"Found {results.Count} lunar return(s) from " +
                         $"{startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}.";

            _logger.Information(
                "Lunar returns calculated for '{Name}': {Count} returns",
                natalResult.Input.Name, results.Count);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Lunar return calculation cancelled.";
            _logger.Information("Lunar return calculation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Lunar return error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate lunar returns");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.LunarReturn);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the selected lunar return result as a Markdown file using the file-save dialog.
    /// </summary>
    private async Task ExportLunarReturnMarkdownAsync(object? _)
    {
        LunarReturnResult? selected = Returns.SelectedLunarReturn;
        if (selected is null) return;

        try
        {
            string name = SanitiseFileName(selected.NatalChart.Input.Name);
            // The return's local date as typed, with invariant digits on the Gregorian calendar (1.7.2, N7)
            string dateStr = selected.ReturnChart.Input.LocalDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            string defaultName = $"{name}_LunarReturn_{dateStr}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Lunar Return as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting lunar return Markdown...";

                string? lunarContext = string.IsNullOrWhiteSpace(Returns.LunarReturnInterpretationContext)
                    ? null
                    : Returns.LunarReturnInterpretationContext.Trim();

                await LunarReturnMarkdownFormatter.SaveAsync(
                    selected, path, lunarContext);
                StatusText = $"Lunar return exported: {path}";
                _logger.Information("Lunar return Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Lunar return export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export lunar return Markdown");
        }
    }

    /// <summary>
    /// Calculate a planetary return chart for the current natal chart using the
    /// selected planet and return number, at the natal location.
    /// </summary>
    private async Task CalculatePlanetaryReturnAsync(object? _)
    {
        if (LastResult is null) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.PlanetaryReturn);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            CelestialBodyId planet = Returns.SelectedReturnPlanet;
            string planetName = planet.ToString();
            bool byYear = Returns.SelectedReturnFindMode == ReturnFindMode.ByYear;

            StatusText = byYear
                ? $"Calculating {planetName} return for year {Returns.ReturnYear}..."
                : $"Calculating {planetName} return #{Returns.ReturnNumber}...";

            ChartResult natalResult = LastResult;

            PlanetaryReturnResult result = byYear
                ? await Task.Run(() =>
                    _planetaryReturnService.CalculateForYear(
                        natalResult, planet, Returns.ReturnYear,
                        natalResult.Input.Latitude,
                        natalResult.Input.Longitude,
                        natalResult.Input.LocationName,
                        natalResult.Input.TimeZone,
                        token),
                    token)
                : await Task.Run(() =>
                    _planetaryReturnService.Calculate(
                        natalResult, planet, Returns.ReturnNumber,
                        natalResult.Input.Latitude,
                        natalResult.Input.Longitude,
                        natalResult.Input.LocationName,
                        natalResult.Input.TimeZone,
                        token),
                    token);

            Returns.CurrentPlanetaryReturnResult = result;

            StatusText = $"{planetName} return #{result.ReturnNumber} calculated " +
                         $"(age {result.AgeAtReturn}, " +
                         $"{CalendarDateFormatter.Date(result.ReturnChart.UtcDateTime)} UTC).";

            _logger.Information(
                "Planetary return calculated: {Planet} #{N} for '{Name}' (age {Age})",
                planetName, result.ReturnNumber, natalResult.Input.Name, result.AgeAtReturn);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Planetary return calculation cancelled.";
            _logger.Information("Planetary return calculation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Planetary return error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate planetary return");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.PlanetaryReturn);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current planetary return result as a Markdown file using the file-save dialog.
    /// </summary>
    private async Task ExportPlanetaryReturnMarkdownAsync(object? _)
    {
        PlanetaryReturnResult? planetaryReturn = Returns.CurrentPlanetaryReturnResult;
        if (planetaryReturn is null) return;

        try
        {
            string name = SanitiseFileName(planetaryReturn.NatalChart.Input.Name);
            string planetName = planetaryReturn.PlanetName;
            int returnNum = planetaryReturn.ReturnNumber;
            string defaultName = $"{name}_{planetName}Return_{returnNum}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                $"Export {planetName} Return as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = $"Exporting {planetName} return Markdown...";

                string? context = string.IsNullOrWhiteSpace(Returns.PlanetaryReturnInterpContext)
                    ? null
                    : Returns.PlanetaryReturnInterpContext.Trim();

                await PlanetaryReturnMarkdownFormatter.SaveAsync(
                    planetaryReturn, path, context);
                StatusText = $"{planetName} return exported: {path}";
                _logger.Information("{Planet} return Markdown exported to {Path}",
                    planetName, path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Planetary return export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export planetary return Markdown");
        }
    }

    /// <summary>
    /// Calculate the planetary cycle timeline between the selected return and
    /// the next, using the same planet, return number / year, and find mode as
    /// the planetary return UI. Reads <see cref="ReturnsViewModel.SelectedReturnPlanet"/>,
    /// <see cref="ReturnsViewModel.SelectedReturnFindMode"/>,
    /// <see cref="ReturnsViewModel.ReturnYear"/>, and
    /// <see cref="ReturnsViewModel.ReturnNumber"/> from the sibling
    /// <see cref="Returns"/> VM via this parent — Rule #2 last-resort
    /// shared-state read justified by the H-1.8 §1A audit (Outcome A).
    /// </summary>
    private async Task CalculateCycleTimelineAsync(object? _)
    {
        if (LastResult is null) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.PlanetaryCycle);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            CelestialBodyId planet = Returns.SelectedReturnPlanet;
            string planetName = planet.ToString();
            bool byYear = Returns.SelectedReturnFindMode == ReturnFindMode.ByYear;
            bool snapshots = Cycles.IncludeCycleSnapshots;

            StatusText = byYear
                ? $"Calculating {planetName} cycle timeline for year {Returns.ReturnYear}..."
                : $"Calculating {planetName} cycle timeline for return #{Returns.ReturnNumber}...";

            ChartResult natalResult = LastResult;

            PlanetaryCycleTimeline timeline = byYear
                ? await Task.Run(() =>
                    _cycleTimelineService.CalculateForDate(
                        natalResult, planet,
                        new DateTime(Returns.ReturnYear, 7, 1, 0, 0, 0, DateTimeKind.Utc),
                        snapshots, token),
                    token)
                : await Task.Run(() =>
                    _cycleTimelineService.Calculate(
                        natalResult, planet, Returns.ReturnNumber,
                        snapshots, token),
                    token);

            Cycles.CurrentCycleTimeline = timeline;

            StatusText = $"{planetName} cycle timeline: Return #{timeline.ReturnNumber} " +
                         $"\u2192 #{timeline.ReturnNumber + 1} " +
                         $"({timeline.PhasePoints.Count} phase points, " +
                         $"{timeline.CycleDurationDays:F0} days).";

            _logger.Information(
                "Cycle timeline calculated: {Planet} #{N} for '{Name}' ({Days:F0} days)",
                planetName, timeline.ReturnNumber, natalResult.Input.Name,
                timeline.CycleDurationDays);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cycle timeline calculation cancelled.";
            _logger.Information("Cycle timeline calculation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Cycle timeline error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate cycle timeline");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.PlanetaryCycle);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current cycle timeline as a Markdown file using the file-save dialog.
    /// </summary>
    private async Task ExportCycleTimelineMarkdownAsync(object? _)
    {
        PlanetaryCycleTimeline? timeline = Cycles.CurrentCycleTimeline;
        if (timeline is null) return;

        try
        {
            string name = SanitiseFileName(timeline.NativeName);
            string planetName = timeline.PlanetName;
            int returnNum = timeline.ReturnNumber;
            string defaultName = $"{name}_{planetName}CycleTimeline_{returnNum}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                $"Export {planetName} Cycle Timeline as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = $"Exporting {planetName} cycle timeline Markdown...";

                await PlanetaryCycleTimelineFormatter.SaveAsync(
                    timeline, path);
                StatusText = $"{planetName} cycle timeline exported: {path}";
                _logger.Information("{Planet} cycle timeline Markdown exported to {Path}",
                    planetName, path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Cycle timeline export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export cycle timeline Markdown");
        }
    }

    /// <summary>
    /// Find all Jupiter-Saturn great conjunctions in the specified year range.
    /// Does not require a natal chart.
    /// </summary>
    private async Task FindGreatConjunctionsAsync(object? _)
    {
        // The panel's own check, before anything starts (1.7.2)
        if (RefuseEndYearBeforeStart(Cycles.GCStartYear, Cycles.GCEndYear)) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.GreatConjunctions);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            int startYear = Cycles.GCStartYear;
            int endYear = Cycles.GCEndYear;
            StatusText = $"Finding great conjunctions from {startYear} to {endYear}...";

            GreatConjunctionSeries series = await Task.Run(() =>
                _greatConjunctionService.FindConjunctions(startYear, endYear, token),
                token);

            Cycles.CurrentGCSeries = series;
            Cycles.HasGCSeries = series.Count > 0;

            StatusText = $"Found {series.Count} great conjunction(s) from {startYear} to {endYear}.";

            _logger.Information(
                "Great conjunctions found: {Count} from {Start} to {End}",
                series.Count, startYear, endYear);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Great conjunction search cancelled.";
            _logger.Information("Great conjunction search cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Great conjunction error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to find great conjunctions");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.GreatConjunctions);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current great conjunction series as a Markdown file.
    /// </summary>
    private async Task ExportGCSeriesMarkdownAsync(object? _)
    {
        GreatConjunctionSeries? series = Cycles.CurrentGCSeries;
        if (series is null) return;

        try
        {
            string defaultName = $"GreatConjunctions_{series.ScanStart.Year}-{series.ScanEnd.Year}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Great Conjunction Series as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting great conjunction series Markdown...";
                await GreatConjunctionFormatter.SaveSeriesAsync(
                    series, path);
                StatusText = $"Great conjunction series exported: {path}";
                _logger.Information("Great conjunction series exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Great conjunction export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export great conjunction series");
        }
    }

    #region Synodic Cycles

    /// <summary>
    /// Find all conjunctions of the selected planet pair within the year range.
    /// </summary>
    private async Task FindSynodicCyclesAsync(object? _)
    {
        // The panel's own check, before anything starts (1.7.2)
        if (RefuseEndYearBeforeStart(Cycles.SynodicStartYear, Cycles.SynodicEndYear)) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.SynodicCycle);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            CelestialBodyId bodyA = Cycles.SynodicBodyA;
            CelestialBodyId bodyB = Cycles.SynodicBodyB;
            int startYear = Cycles.SynodicStartYear;
            int endYear = Cycles.SynodicEndYear;

            StatusText = $"Finding {bodyA}\u2013{bodyB} conjunctions from {startYear} to {endYear}...";

            SynodicCycleSeries series = await Task.Run(() =>
                _synodicCycleService.FindConjunctions(bodyA, bodyB, startYear, endYear, token),
                token);

            Cycles.CurrentSynodicCycleSeries = series;
            Cycles.HasSynodicCycleSeries = series.Count > 0;

            string avgInfo = series.AverageSynodicPeriodYears.HasValue
                ? $", avg period {series.AverageSynodicPeriodYears:F2} yr"
                : "";
            StatusText = $"Found {series.Count} {series.BodyAName}\u2013{series.BodyBName} conjunction(s){avgInfo}.";

            _logger.Information(
                "Synodic cycles found: {Count} {BodyA}–{BodyB} conjunctions from {Start} to {End}",
                series.Count, series.BodyAName, series.BodyBName, startYear, endYear);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Synodic cycle search cancelled.";
            _logger.Information("Synodic cycle search cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Synodic cycle error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to find synodic cycles");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.SynodicCycle);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current synodic cycle series as a Markdown file.
    /// </summary>
    private async Task ExportSynodicCycleMdAsync(object? _)
    {
        SynodicCycleSeries? series = Cycles.CurrentSynodicCycleSeries;
        if (series is null) return;

        try
        {
            string defaultName =
                $"SynodicCycles_{series.BodyAName}-{series.BodyBName}_" +
                $"{series.StartDate.Year}-{series.EndDate.Year}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Synodic Cycle Series as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting synodic cycle series Markdown...";
                await SynodicCycleFormatter.SaveSeriesAsync(
                    series, path);
                StatusText = $"Synodic cycle series exported: {path}";
                _logger.Information("Synodic cycle series exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Synodic cycle export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export synodic cycle series");
        }
    }

    #endregion

    /// <summary>
    /// Find all ingresses of a planet within the specified year.
    /// Does not require a natal chart.
    /// </summary>
    private async Task FindIngressesAsync(object? _)
    {
        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.IngressSeries);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            int year = Mundane.IngressYear;
            CelestialBodyId planet = Mundane.IngressPlanet;
            bool cardinal = Mundane.CardinalOnly;
            StatusText = $"Finding {planet} ingresses for {year}...";

            IngressSeries series = await Task.Run(() =>
                _ingressService.FindIngresses(planet, year, year, cardinal, token),
                token);

            Mundane.CurrentIngressSeries = series;
            Mundane.HasIngressSeries = series.Count > 0;

            StatusText = $"Found {series.Count} {planet} ingress(es) for {year}.";

            _logger.Information(
                "Ingresses found: {Count} {Planet} for {Year}",
                series.Count, planet, year);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Ingress search cancelled.";
            _logger.Information("Ingress search cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Ingress error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to find ingresses");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.IngressSeries);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Calculate full charts for all four cardinal Sun ingresses in the specified year.
    /// Does not require a natal chart.
    /// </summary>
    private async Task CalculateCardinalIngressesAsync(object? _)
    {
        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.CardinalIngresses);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            int year = Mundane.IngressYear;
            double lat = double.Parse(Mundane.LatitudeText, CultureInfo.InvariantCulture);
            double lon = double.Parse(Mundane.LongitudeText, CultureInfo.InvariantCulture);
            string locName = Mundane.LocationName;
            string tz = Mundane.TimeZone;

            StatusText = $"Calculating cardinal Sun ingresses for {year} at {locName}...";

            IReadOnlyList<IngressResult> results = await Task.Run(() =>
                _ingressService.CalculateCardinalIngresses(
                    year, lat, lon, locName, tz, token),
                token);

            Mundane.CurrentCardinalIngresses = results;
            Mundane.HasCardinalIngresses = results.Count > 0;

            StatusText = $"Calculated {results.Count} cardinal ingress chart(s) for {year}.";

            _logger.Information(
                "Cardinal ingresses calculated: {Count} for {Year} at {Location}",
                results.Count, year, locName);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cardinal ingress calculation cancelled.";
            _logger.Information("Cardinal ingress calculation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Cardinal ingress error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate cardinal ingresses");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.CardinalIngresses);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current ingress series as a Markdown file.
    /// </summary>
    private async Task ExportIngressSeriesMarkdownAsync(object? _)
    {
        IngressSeries? series = Mundane.CurrentIngressSeries;
        if (series is null) return;

        try
        {
            string defaultName = $"{series.PlanetName}_Ingresses_{series.ScanStart.Year}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Ingress Series as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting ingress series Markdown...";
                await IngressFormatter.SaveSeriesAsync(
                    series, path);
                StatusText = $"Ingress series exported: {path}";
                _logger.Information("Ingress series exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Ingress series export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export ingress series");
        }
    }

    /// <summary>
    /// Export the current cardinal ingresses as a Markdown file. Its default name and
    /// title take the year of the calculated results, the UTC year of the first
    /// ingress, not the form's year at export time.
    /// </summary>
    private async Task ExportCardinalIngressesMarkdownAsync(object? _)
    {
        IReadOnlyList<IngressResult>? results = Mundane.CurrentCardinalIngresses;
        if (results is null || results.Count == 0) return;

        try
        {
            int year = results[0].ExactUtc.Year;
            string defaultName = $"CardinalIngresses_{year}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Cardinal Ingresses as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting cardinal ingresses Markdown...";
                await IngressFormatter.SaveCardinalAsync(
                    results, year, path);
                StatusText = $"Cardinal ingresses exported: {path}";
                _logger.Information("Cardinal ingresses exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Cardinal ingress export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export cardinal ingresses");
        }
    }

    /// <summary>
    /// Refresh the can-execute state of ingress commands.
    /// </summary>
    private void RefreshIngressCommandStates()
    {
        FindIngressesCommand.RaiseCanExecuteChanged();
        CalculateCardinalIngressesCommand.RaiseCanExecuteChanged();
        ExportIngressSeriesMarkdownCommand.RaiseCanExecuteChanged();
        ExportCardinalIngressesMarkdownCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Find all New Moons and Full Moons for the specified year (timing only).
    /// Does not require a natal chart.
    /// </summary>
    private async Task FindLunationsAsync(object? _)
    {
        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.LunationSeries);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            int year = Mundane.LunationYear;
            StatusText = $"Finding lunations for {year}...";

            LunationSeries series = await Task.Run(() =>
                _lunationService.FindLunations(year, year, token),
                token);

            Mundane.CurrentLunationSeries = series;
            Mundane.HasLunationSeries = series.Count > 0;

            StatusText = $"Found {series.Count} lunation(s) for {year} " +
                $"({series.NewMoons.Count} New, {series.FullMoons.Count} Full, " +
                $"{series.PossibleEclipses.Count} possible eclipses).";

            _logger.Information(
                "Lunations found: {Count} for {Year}",
                series.Count, year);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Lunation search cancelled.";
            _logger.Information("Lunation search cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Lunation error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to find lunations");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.LunationSeries);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Calculate full charts at all lunations using the mundane location.
    /// Does not require a natal chart.
    /// </summary>
    private async Task CalculateLunationChartsAsync(object? _)
    {
        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.LunationCharts);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            int year = Mundane.LunationYear;
            double lat = double.Parse(Mundane.LatitudeText, CultureInfo.InvariantCulture);
            double lon = double.Parse(Mundane.LongitudeText, CultureInfo.InvariantCulture);
            string locName = Mundane.LocationName;
            string tz = Mundane.TimeZone;

            StatusText = $"Calculating lunation charts for {year} at {locName}...";

            LunationSeries series = await Task.Run(() =>
                _lunationService.CalculateLunationsForYear(
                    year, lat, lon, locName, tz, token),
                token);

            Mundane.CurrentLunationSeries = series;
            Mundane.HasLunationSeries = series.Count > 0;

            StatusText = $"Calculated {series.Count} lunation chart(s) for {year}.";

            _logger.Information(
                "Lunation charts calculated: {Count} for {Year} at {Location}",
                series.Count, year, locName);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Lunation chart calculation cancelled.";
            _logger.Information("Lunation chart calculation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Lunation chart error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate lunation charts");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.LunationCharts);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current lunation series as a Markdown file.
    /// </summary>
    private async Task ExportLunationSeriesAsync(object? _)
    {
        LunationSeries? series = Mundane.CurrentLunationSeries;
        if (series is null) return;

        try
        {
            string defaultName = $"Lunations_{series.ScanStart.Year}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Lunation Series as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting lunation series Markdown...";
                await LunationFormatter.SaveSeriesAsync(
                    series, path);
                StatusText = $"Lunation series exported: {path}";
                _logger.Information("Lunation series exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Lunation export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export lunation series");
        }
    }

    /// <summary>
    /// Refresh the can-execute state of lunation commands.
    /// </summary>
    private void RefreshLunationCommandStates()
    {
        FindLunationsCommand.RaiseCanExecuteChanged();
        CalculateLunationChartsCommand.RaiseCanExecuteChanged();
        ExportLunationSeriesCommand.RaiseCanExecuteChanged();
    }

    #endregion

    #region Mundane Aspect Finder

    /// <summary>
    /// Find all exact aspects between two bodies within the specified year range.
    /// Not gated on HasResult — this is a standalone mundane feature.
    /// </summary>
    private async Task FindMundaneAspectsAsync(object? _)
    {
        // The panel's own check, before anything starts (1.7.2)
        if (RefuseEndYearBeforeStart(Mundane.MAStartYear, Mundane.MAEndYear)) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.MundaneAspects);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            CelestialBodyId body1 = Mundane.MABody1;
            CelestialBodyId body2 = Mundane.MABody2;
            string harmonicMode = Mundane.MAHarmonicMode;
            int startYear = Mundane.MAStartYear;
            int endYear = Mundane.MAEndYear;

            StatusText = $"Finding {body1}-{body2} aspects ({harmonicMode}) from {startYear} to {endYear}...";

            MundaneAspectSeries series = await Task.Run(() =>
                _mundaneAspectService.FindAspects(body1, body2, harmonicMode, startYear, endYear, token),
                token);

            Mundane.CurrentMASeries = series;
            Mundane.HasMASeries = series.Count > 0;

            StatusText = $"Found {series.Count} {body1}-{body2} aspect(s) ({harmonicMode}) from {startYear} to {endYear}.";

            _logger.Information(
                "Mundane aspects found: {Count} {Body1}-{Body2} ({Mode}) from {Start} to {End}",
                series.Count, body1, body2, harmonicMode, startYear, endYear);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Mundane aspect search cancelled.";
            _logger.Information("Mundane aspect search cancelled by user");
        }
        catch (ArgumentException ex)
        {
            StatusText = $"Mundane aspect error: {DisplayText.ErrorMessage(ex)}";
            _logger.Warning(ex, "Invalid mundane aspect parameters");
        }
        catch (Exception ex)
        {
            StatusText = $"Mundane aspect error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to find mundane aspects");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.MundaneAspects);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current mundane aspect series as a Markdown file.
    /// </summary>
    private async Task ExportMASeriesAsync(object? _)
    {
        MundaneAspectSeries? series = Mundane.CurrentMASeries;
        if (series is null) return;

        try
        {
            string modeSuffix = series.HarmonicMode.Replace("°", "").Replace(" ", "_");
            string defaultName = $"{series.Body1Name}_{series.Body2Name}" +
                $"_{modeSuffix}_{series.ScanStart.Year}_{series.ScanEnd.Year}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Mundane Aspect Series as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting mundane aspect series Markdown...";
                await MundaneAspectFormatter.SaveAsync(
                    series, path);
                StatusText = $"Mundane aspect series exported: {path}";
                _logger.Information("Mundane aspect series exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Mundane aspect export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export mundane aspect series");
        }
    }

    /// <summary>
    /// Refresh the can-execute state of mundane aspect commands.
    /// </summary>
    private void RefreshMACommandStates()
    {
        FindMundaneAspectsCommand.RaiseCanExecuteChanged();
        ExportMASeriesCommand.RaiseCanExecuteChanged();
    }

    #endregion

    #region Eclipse Calculator

    /// <summary>
    /// Find all solar and lunar eclipses within the specified year range.
    /// Not gated on HasResult — this is a standalone mundane feature.
    /// </summary>
    private async Task FindEclipsesAsync(object? _)
    {
        // The panel's own check, before anything starts (1.7.2)
        if (RefuseEndYearBeforeStart(Mundane.EclipseStartYear, Mundane.EclipseEndYear)) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.Eclipses);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            int startYear = Mundane.EclipseStartYear;
            int endYear = Mundane.EclipseEndYear;

            StatusText = $"Finding eclipses from {startYear} to {endYear}...";

            EclipseSeries series = await Task.Run(() =>
                _eclipseService.FindEclipses(startYear, endYear, token),
                token);

            Mundane.CurrentEclipseSeries = series;
            Mundane.HasEclipseSeries = series.Count > 0;

            StatusText = $"Found {series.Count} eclipse(s) from {startYear} to {endYear} " +
                $"({series.SolarEclipses.Count} Solar, {series.LunarEclipses.Count} Lunar).";

            _logger.Information(
                "Eclipses found: {Count} from {Start} to {End}",
                series.Count, startYear, endYear);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Eclipse search cancelled.";
            _logger.Information("Eclipse search cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Eclipse error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to find eclipses");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.Eclipses);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current eclipse series as a Markdown file.
    /// </summary>
    private async Task ExportEclipseSeriesAsync(object? _)
    {
        EclipseSeries? series = Mundane.CurrentEclipseSeries;
        if (series is null) return;

        try
        {
            string defaultName = $"Eclipses_{series.ScanStart.Year}_{series.ScanEnd.Year}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Eclipse Series as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting eclipse series Markdown...";
                await EclipseFormatter.SaveSeriesAsync(
                    series, path);
                StatusText = $"Eclipse series exported: {path}";
                _logger.Information("Eclipse series exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Eclipse export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export eclipse series");
        }
    }

    /// <summary>
    /// Refresh the can-execute state of eclipse commands.
    /// </summary>
    private void RefreshEclipseCommandStates()
    {
        FindEclipsesCommand.RaiseCanExecuteChanged();
        ExportEclipseSeriesCommand.RaiseCanExecuteChanged();
    }

    #endregion

    #region Return & Ingress Timeline

    /// <summary>
    /// Calculate the return &amp; ingress timeline for the current natal chart.
    /// </summary>
    private async Task CalculateRITimelineAsync(object? _)
    {
        if (LastResult is null) return;

        // The panel's own check, before anything starts (1.7.2)
        if (RefuseEndYearBeforeStart(this.ReturnIngressTimeline.StartYear, this.ReturnIngressTimeline.EndYear)) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.ReturnIngressTimeline);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            int startYear = this.ReturnIngressTimeline.StartYear;
            int endYear = this.ReturnIngressTimeline.EndYear;
            bool includeReturns = this.ReturnIngressTimeline.IncludeReturns;
            bool includeIngresses = this.ReturnIngressTimeline.IncludeIngresses;
            ChartResult natalResult = LastResult;

            string mode = (includeReturns, includeIngresses) switch
            {
                (true, true) => "returns + ingresses",
                (true, false) => "returns only",
                (false, true) => "ingresses only",
                _ => "none",
            };

            StatusText = $"Calculating timeline ({mode}) from {startYear} to {endYear}...";

            ReturnIngressTimeline timeline = await Task.Run(() =>
                _returnIngressTimelineService.Calculate(
                    natalResult,
                    startYear,
                    endYear,
                    includeReturns,
                    includeIngresses,
                    cancellationToken: token),
                token);

            this.ReturnIngressTimeline.CurrentResult = timeline;
            this.ReturnIngressTimeline.HasResult = timeline.Count > 0;

            int returnCount = timeline.Returns.Count;
            int ingressCount = timeline.Ingresses.Count;

            StatusText = $"Timeline: {timeline.Count} events " +
                $"({returnCount} returns, {ingressCount} ingresses) " +
                $"from {startYear} to {endYear}.";

            _logger.Information(
                "Return & Ingress Timeline: {Total} events ({Returns} returns, {Ingresses} ingresses) from {Start} to {End}",
                timeline.Count, returnCount, ingressCount, startYear, endYear);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Timeline calculation cancelled.";
            _logger.Information("Return & Ingress Timeline cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Timeline error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate Return & Ingress Timeline");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.ReturnIngressTimeline);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current return &amp; ingress timeline as a Markdown file.
    /// </summary>
    private async Task ExportRITimelineAsync(object? _)
    {
        ReturnIngressTimeline? timeline = this.ReturnIngressTimeline.CurrentResult;
        if (timeline is null) return;

        try
        {
            string safeName = timeline.NativeName.Replace(" ", "_");
            string defaultName = $"RI_Timeline_{safeName}_{timeline.ScanStart.Year}_{timeline.ScanEnd.Year}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Return & Ingress Timeline as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting Return & Ingress Timeline Markdown...";
                await ReturnIngressTimelineFormatter.SaveAsync(
                    timeline, path);
                StatusText = $"Timeline exported: {path}";
                _logger.Information("Return & Ingress Timeline exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Timeline export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export Return & Ingress Timeline");
        }
    }

    /// <summary>
    /// Refresh the can-execute state of return &amp; ingress timeline commands.
    /// </summary>
    private void RefreshRITimelineCommandStates()
    {
        CalculateRITimelineCommand.RaiseCanExecuteChanged();
        ExportRITimelineCommand.RaiseCanExecuteChanged();
    }

    #endregion

    /// <summary>
    /// Set the date and time fields to the current date and time on the chart's own
    /// clock: its explicit offset's, or its zone's.
    /// </summary>
    private void SetNow()
    {
        // The current instant on the chart's own clock (card 8): its explicit offset, or
        // its zone's through the app's time-zone data. The machine's clock is never used:
        // read in another zone it is the wrong instant, so a form naming no clock that can
        // be read is left as it is.
        DateTime utcNow = DateTime.UtcNow;
        string zone = ChartEntry.TimeZone?.Trim() ?? string.Empty;
        DateTime now;
        if (ChartEntry.UseExplicitOffset)
        {
            // The box is read as Calculate & Save reads it; a bad offset is refused, not
            // passed over for the zone's clock the chart will not be cast on.
            if (!ChartEntryViewModel.TryParseUtcOffsetHours(ChartEntry.UtcOffsetHoursText, out double offsetHours, out string? offsetError))
            {
                StatusText = offsetError;
                return;
            }

            now = DateTime.SpecifyKind(utcNow.AddHours(offsetHours), DateTimeKind.Unspecified);
        }
        else if (TimeZoneRules.Canonicalize(zone) is not null)
        {
            now = TimeZoneRules.ToLocal(utcNow, zone).LocalDateTime;
        }
        else
        {
            StatusText = "Choose the chart's time zone, or an explicit offset, before setting the time to now.";
            return;
        }

        // The time with a colon in every culture, as the Time box is read (1.7.2, N7): the machine's own
        // separator, a dot under fi-FI or da-DK, is refused by the box's check
        ChartEntry.EventDate = now.Date;
        ChartEntry.EventTime = now.ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        StatusText = $"Date/time set to now: {now:yyyy-MM-dd HH:mm:ss}";
    }

    /// <summary>
    /// Clear the form to its default state: the Chart panel's fields and chart, the child panels' own fields,
    /// and the two saved-chart pickers that follow the Chart panel's chart.
    /// </summary>
    /// <remarks>
    /// The Electional and Relocation pickers are emptied through their setters, since each follows the Chart
    /// panel's chart and Clear Form empties the Chart panel (Steve's decision of 2026-10-21; 1.6.1, branch
    /// ElectionalPicker): the Relocation setter clears its result and raises its line, the Electional setter
    /// raises its line, and each requests one refresh. The Relocation panel's place fields are left as they are
    /// (<c>Relocation.Clear()</c> is not called), and <c>Electional.Clear()</c> keeps its own scope. Until 1.6.1
    /// both pickers kept their pick, the Electional one since 1.4.0 and the Relocation one since branch Relocate.
    /// </remarks>
    private void ClearForm()
    {
        ChartEntry.Clear();
        ChartEntryLocationSearch.Clear();
        Synastry.Clear();
        Transits.Clear();
        Directions.Clear();
        Electional.Clear();
        Returns.Clear();
        Cycles.Clear();
        LastResult = null;
        HasResult = false;

        // The pickers follow the Chart panel's chart, which Clear Form has just emptied (1.6.1, branch
        // ElectionalPicker; until then both kept their pick).
        Relocation.SelectedNatalChart = null;
        Electional.SelectedNatalChart = null;

        StatusText = "Form cleared.";
    }

    #region Helper Methods

    /// <summary>
    /// Determine whether the form has enough data to calculate a chart.
    /// </summary>
    private bool CanCalculate() =>
        !string.IsNullOrWhiteSpace(ChartEntry.Name) &&
        !string.IsNullOrWhiteSpace(ChartEntry.TimeZone) &&
        !string.IsNullOrWhiteSpace(ChartEntry.LocationName);

    /// <summary>
    /// A date as a default file name carries it: <c>yyyyMMdd</c> in the invariant culture, so on the Gregorian
    /// calendar with ASCII digits whatever the machine's regional settings (branch InputChecks, 1.7.2, finding
    /// N7). Until 1.7.2 each name took its dates in the machine's culture: under th-TH, whose calendar is the Thai
    /// Buddhist one, 2026-04-20 read "25690420".
    /// </summary>
    /// <param name="date">The date, as typed or as the result holds it.</param>
    /// <returns>The date's eight digits.</returns>
    private static string FileNameDate(DateOnly date) => date.ToString("yyyyMMdd", CultureInfo.InvariantCulture);

    /// <summary>The status line for a date range whose end date is before its start date.</summary>
    internal const string EndDateBeforeStartMessage = "The end date is before the start date.";

    /// <summary>
    /// The status line for a date range that needs time between its bounds and has none: the lunar return
    /// search's, whose bounds are two midnights.
    /// </summary>
    internal const string EndDateNotAfterStartMessage = "The end date must be after the start date.";

    /// <summary>The status line for a year range whose end year is before its start year.</summary>
    internal const string EndYearBeforeStartMessage = "The end year is before the start year.";

    /// <summary>
    /// Checks a panel's two dates before its command starts (branch InputChecks, 1.7.2, finding N6): when the end
    /// date is before the start date the status line says so and the caller returns without calling the engine.
    /// </summary>
    /// <remarks>
    /// The same date twice is a range of one day, which the transit report and the electional search both take:
    /// each reads its end date as a whole day. Until 1.7.2 no panel checked its dates, and a reversed range was
    /// refused by the engine in the code's words.
    /// </remarks>
    /// <param name="start">The panel's start date.</param>
    /// <param name="end">The panel's end date.</param>
    /// <returns><see langword="true"/> when the range was refused and the status line set.</returns>
    private bool RefuseEndDateBeforeStart(DateTime start, DateTime end)
    {
        if (end.Date >= start.Date)
            return false;

        StatusText = EndDateBeforeStartMessage;
        return true;
    }

    /// <summary>
    /// Checks a panel's two years before its command starts (branch InputChecks, 1.7.2, finding N6): when the end
    /// year is before the start year the status line says so and the caller returns without calling the engine.
    /// The same year twice is a range of one year.
    /// </summary>
    /// <param name="startYear">The panel's start year.</param>
    /// <param name="endYear">The panel's end year.</param>
    /// <returns><see langword="true"/> when the range was refused and the status line set.</returns>
    private bool RefuseEndYearBeforeStart(int startYear, int endYear)
    {
        if (endYear >= startYear)
            return false;

        StatusText = EndYearBeforeStartMessage;
        return true;
    }

    /// <summary>
    /// Reads the Electional panel's two time boxes as its time window (branch InputChecks, 1.7.2, finding N6).
    /// </summary>
    /// <remarks>
    /// Both boxes empty is no window; both holding a time of day is the window; anything else is refused with a
    /// message naming the box at fault. A time of day is read as the Chart panel's Time box is
    /// (<see cref="ChartEntryViewModel.TryParseTimeOfDay"/>): hours and minutes with a colon, seconds optional, in
    /// every culture. A window whose earliest time is after its latest would wrap midnight, which the search does
    /// not support, and keeps its own refusal. Until 1.7.2 the boxes were read in the machine's culture, and a
    /// window with one box empty or mistyped was dropped in silence, so the whole day was searched.
    /// </remarks>
    /// <param name="window">The window, or null when both boxes are empty or the boxes were refused.</param>
    /// <param name="error">The message for the status line when the boxes were refused; otherwise null.</param>
    /// <returns><see langword="true"/> when the boxes were read; <see langword="false"/> when they were refused.</returns>
    private bool TryReadElectionalTimeWindow(out TimeConstraint? window, [NotNullWhen(false)] out string? error)
    {
        window = null;
        error = null;

        bool earliestEmpty = string.IsNullOrWhiteSpace(Electional.EarliestTimeText);
        bool latestEmpty = string.IsNullOrWhiteSpace(Electional.LatestTimeText);
        if (earliestEmpty && latestEmpty)
            return true;

        if (earliestEmpty || latestEmpty)
        {
            error = $"The {(earliestEmpty ? "Earliest" : "Latest")} time is empty. "
                + "Fill in both Earliest and Latest, or leave both empty.";
            return false;
        }

        if (!ChartEntryViewModel.TryParseTimeOfDay(Electional.EarliestTimeText, out TimeSpan earliest, out string? _))
        {
            error = "The Earliest time is not a time of day. Use HH:mm.";
            return false;
        }

        if (!ChartEntryViewModel.TryParseTimeOfDay(Electional.LatestTimeText, out TimeSpan latest, out string? _))
        {
            error = "The Latest time is not a time of day. Use HH:mm.";
            return false;
        }

        // Midnight-wrapping windows are not supported by the candidate sampler: reject with a
        // validation message instead of running a search that can never match.
        if (earliest > latest)
        {
            error = "Invalid time window: earliest must not be later than latest (windows may not wrap midnight).";
            return false;
        }

        window = new TimeConstraint
        {
            Earliest = TimeOnly.FromTimeSpan(earliest),
            Latest = TimeOnly.FromTimeSpan(latest),
        };
        return true;
    }

    /// <summary>
    /// Refresh the saved charts list from the database using current filters,
    /// and refresh the unfiltered <see cref="AllSavedCharts"/> collection the
    /// three chart pickers bind, keeping their selections by Id.
    /// </summary>
    /// <remarks>
    /// <see cref="ChartEntity"/> compares by reference and the database hands out
    /// new instances, so the refill holds none of the instances the pickers held,
    /// and a bound ComboBox writes null back when the list is cleared. The three
    /// Ids are therefore read before the clear, and each selection is pointed at
    /// the refilled chart with the same Id, or at nothing when that chart is gone.
    /// The selection setters only request a command-state refresh, so the null
    /// written in between leaves nothing else behind.
    /// </remarks>
    private async Task RefreshSavedChartsAsync()
    {
        // Filtered list for the ListView
        ChartQueryOptions options = new()
        {
            KeywordSearch = string.IsNullOrWhiteSpace(SearchText) ? null : SearchText.Trim(),
            EventType = string.IsNullOrWhiteSpace(FilterEventType) ? null : FilterEventType,
            DateFrom = FilterDateFrom,
            DateTo = FilterDateTo,
            SortField = SortField,
            SortAscending = SortAscending,
        };

        IReadOnlyList<ChartEntity> filtered = await _repository.QueryAsync(options);

        SavedCharts.Clear();
        foreach (ChartEntity chart in filtered)
        {
            SavedCharts.Add(chart);
        }

        // Unfiltered list, bound by the Person B, Electional and Relocation pickers
        IReadOnlyList<ChartEntity> all = await _repository.ListAllAsync();

        long? personBId = Synastry.PersonB?.Id;
        long? electionalId = Electional.SelectedNatalChart?.Id;
        long? relocationId = Relocation.SelectedNatalChart?.Id;

        AllSavedCharts.Clear();
        foreach (ChartEntity chart in all)
        {
            AllSavedCharts.Add(chart);
        }

        Synastry.PersonB = FindSavedChart(personBId);
        Electional.SelectedNatalChart = FindSavedChart(electionalId);
        Relocation.SelectedNatalChart = FindSavedChart(relocationId);
    }

    /// <summary>
    /// Finds the chart with the given Id in <see cref="AllSavedCharts"/>.
    /// </summary>
    /// <param name="id">The chart's Id, or <see langword="null"/> for no selection.</param>
    /// <returns>The chart, or <see langword="null"/> when there is no Id or no chart with it.</returns>
    private ChartEntity? FindSavedChart(long? id) =>
        id is { } wanted ? AllSavedCharts.FirstOrDefault(c => c.Id == wanted) : null;

    /// <summary>
    /// Refresh the can-execute state of the calculate command and
    /// dependent commands whose predicates reference natal form fields.
    /// </summary>
    private void RefreshCommandStates()
    {
        CalculateAndSaveCommand.RaiseCanExecuteChanged();
        CalculateCompositeCommand.RaiseCanExecuteChanged();
        CalculateDavisonCommand.RaiseCanExecuteChanged();
        CalculateSolarReturnCommand.RaiseCanExecuteChanged();
        CalculateLunarReturnCommand.RaiseCanExecuteChanged();
        CalculatePlanetaryReturnCommand.RaiseCanExecuteChanged();
        CalculateCycleTimelineCommand.RaiseCanExecuteChanged();
        RefreshElectionalCommandStates();
    }

    /// <summary>
    /// Refresh the can-execute state of all export commands.
    /// </summary>
    private void RefreshExportCommandStates()
    {
        ExportPdfCommand.RaiseCanExecuteChanged();
        ExportMarkdownCommand.RaiseCanExecuteChanged();
        ExportChartJsonCommand.RaiseCanExecuteChanged();
        ExportBothCommand.RaiseCanExecuteChanged();
        CalculateSynastryCommand.RaiseCanExecuteChanged();
        CalculateTransitCommand.RaiseCanExecuteChanged();
        CalculateCompositeCommand.RaiseCanExecuteChanged();
        CalculateDavisonCommand.RaiseCanExecuteChanged();
        CalculateSolarReturnCommand.RaiseCanExecuteChanged();
        CalculateLunarReturnCommand.RaiseCanExecuteChanged();
        CalculatePlanetaryReturnCommand.RaiseCanExecuteChanged();
        CalculateCycleTimelineCommand.RaiseCanExecuteChanged();
        CalculateRelocationCommand.RaiseCanExecuteChanged();
        CalculateRITimelineCommand.RaiseCanExecuteChanged();
        CalculateAcgCommand.RaiseCanExecuteChanged();
        ExportMedicalMarkdownCommand.RaiseCanExecuteChanged();
        ExportSynodicCycleMdCommand.RaiseCanExecuteChanged();
        RefreshElectionalCommandStates();
        // The Relocation child's commands follow HasResult too: Calculate Relocation reads it as the
        // Chart panel's chart. Until 1.4.0 only the Relocation child's own refresh raised them
        // (port plan §9 item 6).
        RefreshRelocationCommandStates();
        // Calculate Directions reads HasResult as the Chart panel's chart (branch PrimaryPanel, 1.5.0).
        RefreshDirectionsCommandStates();
    }

    /// <summary>
    /// Refresh the can-execute state of synastry commands.
    /// </summary>
    private void RefreshSynastryCommandStates()
    {
        CalculateSynastryCommand.RaiseCanExecuteChanged();
        ExportSynastryMarkdownCommand.RaiseCanExecuteChanged();
        CalculateCompositeCommand.RaiseCanExecuteChanged();
        CalculateDavisonCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Refresh the can-execute state of transit commands: Calculate, which reads the techniques
    /// checked (since 1.4.0, branch ModesPanel) and, while Directions is checked, the Directions
    /// panel's settings (since 1.5.0, branch PrimaryPanel), and the three exports, which read the result.
    /// </summary>
    private void RefreshTransitCommandStates()
    {
        CalculateTransitCommand.RaiseCanExecuteChanged();
        ExportTransitMarkdownCommand.RaiseCanExecuteChanged();
        ExportDailyHoroscopeMarkdownCommand.RaiseCanExecuteChanged();
        ExportTransitTimelineMarkdownCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Refresh the can-execute state of electional commands.
    /// </summary>
    private void RefreshElectionalCommandStates()
    {
        CalculateElectionalCommand.RaiseCanExecuteChanged();
        ExportElectionalMarkdownCommand.RaiseCanExecuteChanged();
        UseNatalLocationCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Refresh the can-execute state of the composite commands and, since 1.7.0 (branch DavisonPanel), the
    /// Davison pair beside them: both follow the Synastry child's Person B and results.
    /// </summary>
    private void RefreshCompositeCommandStates()
    {
        CalculateCompositeCommand.RaiseCanExecuteChanged();
        ExportCompositeMarkdownCommand.RaiseCanExecuteChanged();
        CalculateDavisonCommand.RaiseCanExecuteChanged();
        ExportDavisonMarkdownCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Refresh the can-execute state of solar return commands.
    /// </summary>
    private void RefreshSolarReturnCommandStates()
    {
        CalculateSolarReturnCommand.RaiseCanExecuteChanged();
        ExportSolarReturnMarkdownCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Refresh the can-execute state of lunar return commands.
    /// </summary>
    private void RefreshLunarReturnCommandStates()
    {
        CalculateLunarReturnCommand.RaiseCanExecuteChanged();
        ExportLunarReturnMarkdownCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Refresh the can-execute state of planetary return commands.
    /// </summary>
    private void RefreshPlanetaryReturnCommandStates()
    {
        CalculatePlanetaryReturnCommand.RaiseCanExecuteChanged();
        ExportPlanetaryReturnMarkdownCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Refresh the can-execute state of cycle timeline commands. Split out
    /// from <see cref="RefreshPlanetaryReturnCommandStates"/> in Session H-1.8
    /// so the cycle-timeline commands are refreshed by
    /// <see cref="CyclesViewModel"/>'s command-state signal, not by
    /// <see cref="ReturnsViewModel"/>'s. The old shared refresher coupled two
    /// unrelated result surfaces; no observable behaviour changes because
    /// neither cycle-timeline command's <c>CanExecute</c> ever depended on
    /// planetary-return state.
    /// </summary>
    private void RefreshCycleTimelineCommandStates()
    {
        CalculateCycleTimelineCommand.RaiseCanExecuteChanged();
        ExportCycleTimelineMarkdownCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Refresh the can-execute state of great conjunction commands.
    /// </summary>
    private void RefreshGCCommandStates()
    {
        FindGreatConjunctionsCommand.RaiseCanExecuteChanged();
        ExportGCSeriesMarkdownCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Refresh the can-execute state of synodic cycle commands.
    /// </summary>
    private void RefreshSynodicCycleCommandStates()
    {
        FindSynodicCyclesCommand.RaiseCanExecuteChanged();
        ExportSynodicCycleMdCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Calculates a relocation chart from the current natal result and the
    /// relocation target location fields.
    /// </summary>
    /// <remarks>
    /// The natal chart is the dropdown's, else <see cref="LastResult"/>. A chart
    /// from the dropdown is cast inside the one <c>Task.Run</c>, under the
    /// relocation operation's token, as Synastry casts Person B, so it runs off
    /// the UI thread and the panel's Cancel reaches it (1.4.0, port plan §9 item
    /// 10); until then it was cast synchronously before the operation started.
    /// The cheap checks, a natal source and valid coordinates, come first.
    /// </remarks>
    private async Task CalculateRelocationAsync(object? _)
    {
        // Resolve natal chart: dropdown selection takes priority over LastResult
        ChartEntity? selected = Relocation.SelectedNatalChart;
        ChartResult? calculated = LastResult;

        if (selected is null && calculated is null)
        {
            StatusText = "No chart to relocate: pick a birth chart, or load or calculate one on the Chart panel.";
            return;
        }

        // The command is disabled while either box is invalid or blank; this is the same check, for a caller
        // that runs it regardless.
        if (!ChartEntryViewModel.TryParseLatitude(Relocation.LatitudeText, out double latitude, out string? latitudeError))
        {
            StatusText = latitudeError;
            return;
        }

        if (!ChartEntryViewModel.TryParseLongitude(Relocation.LongitudeText, out double longitude, out string? longitudeError))
        {
            StatusText = longitudeError;
            return;
        }

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.Relocation);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            StatusText = $"Calculating relocation to {Relocation.LocationName}...";
            RelocationInput target = new()
            {
                Latitude = latitude,
                Longitude = longitude,
                LocationName = Relocation.LocationName,
            };

            // The session's minor-points switch, read on the UI thread for the cast below.
            bool includeMinorPoints = ChartEntry.IncludeMinorPoints;

            (ChartResult natalResult, RelocationResult relocation) = await Task.Run(() =>
            {
                ChartResult natal = selected is null
                    ? calculated ?? throw new InvalidOperationException("No natal chart to relocate.")
                    : _chartEngine.Calculate(ChartMapper.ToInput(selected, includeMinorPoints), token);
                return (natal, _relocationService.Calculate(natal, target, token));
            }, token);

            Relocation.CurrentResult = relocation;

            int houseChanges = relocation.HouseChanges.Count;
            string ascNote = relocation.AscendantSignChanged
                ? $"ASC: {relocation.NatalAscendantSign}\u2192{relocation.RelocatedAscendantSign}"
                : "ASC unchanged";

            StatusText = $"Relocation to {Relocation.LocationName}: " +
                         $"{houseChanges} house changes, {ascNote}.";

            _logger.Information(
                "Relocation calculated for '{Name}' to {Location}",
                natalResult.Input.Name, Relocation.LocationName);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Relocation calculation cancelled.";
            _logger.Information("Relocation calculation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Relocation error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate relocation");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.Relocation);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current relocation result as a Markdown file using the file-save dialog.
    /// </summary>
    private async Task ExportRelocationMarkdownAsync(object? _)
    {
        RelocationResult? current = Relocation.CurrentResult;
        if (current is null) return;

        try
        {
            string name = SanitiseFileName(current.NatalChart.Input.Name);
            string location = SanitiseFileName(current.RelocationTarget.LocationName);
            string defaultName = $"{name}_Relocation_{location}.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Relocation Chart as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting relocation Markdown...";

                await RelocationMarkdownFormatter.SaveAsync(
                    current, path);
                StatusText = $"Relocation exported: {path}";
                _logger.Information("Relocation Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Relocation export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export relocation Markdown");
        }
    }

    /// <summary>
    /// Refreshes the can-execute state of relocation commands.
    /// </summary>
    private void RefreshRelocationCommandStates()
    {
        CalculateRelocationCommand.RaiseCanExecuteChanged();
        ExportRelocationMarkdownCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Calculate the primary directions of the Chart panel's chart with the Directions panel's settings, read
    /// once at the start, on a background thread under the directions operation key (branch PrimaryPanel, 1.5.0).
    /// </summary>
    private async Task CalculateDirectionsAsync(object? _)
    {
        if (LastResult is null || !Directions.HasValidSettings) return;

        ChartResult chart = LastResult;
        PrimaryDirectionSettings settings = Directions.Settings;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.Directions);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            StatusText = "Calculating primary directions...";

            PrimaryDirectionResult result = await Task.Run(
                () => _primaryDirectionService.Calculate(chart, settings, token), token);
            Directions.CurrentResult = result;

            int count = result.Hits.Count;
            string directions = count == 1 ? "1 direction" : $"{count} directions";
            string leftOut = result.LeftOutCount > 0 ? $" ({result.LeftOutCount} left out)" : string.Empty;
            StatusText = $"Primary directions calculated: {directions} to age {settings.AgeLimitYears}{leftOut}.";

            _logger.Information(
                "Primary directions calculated for '{Name}': {Count} to age {Age}, {LeftOut} left out",
                chart.Input.Name, count, settings.AgeLimitYears, result.LeftOutCount);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Primary directions calculation cancelled.";
            _logger.Information("Primary directions calculation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Primary directions error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate primary directions");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.Directions);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Export the current primary-direction result as the whole-life Primary Directions Report, under the
    /// default name "{chart}_PrimaryDirections_0-{limit}.md", using the file-save dialog.
    /// </summary>
    private async Task ExportDirectionsMarkdownAsync(object? _)
    {
        PrimaryDirectionResult? current = Directions.CurrentResult;
        if (current is null) return;

        try
        {
            string name = SanitiseFileName(current.NatalChart.Input.Name);
            string defaultName = string.Create(
                CultureInfo.InvariantCulture, $"{name}_PrimaryDirections_0-{current.Settings.AgeLimitYears}.md");

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Primary Directions as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting primary directions Markdown...";

                await PrimaryDirectionMarkdownFormatter.SaveAsync(current, path);
                StatusText = $"Primary directions exported: {path}";
                _logger.Information("Primary directions Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Primary directions export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export primary directions Markdown");
        }
    }

    /// <summary>
    /// Refreshes the can-execute state of the Directions panel's two commands (branch PrimaryPanel, 1.5.0).
    /// </summary>
    private void RefreshDirectionsCommandStates()
    {
        CalculateDirectionsCommand.RaiseCanExecuteChanged();
        ExportDirectionsMarkdownCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Compute astrocartography lines for the current natal chart on a background
    /// thread and update the ViewModel state so the map can bind.
    /// </summary>
    private async Task CalculateAcgAsync(object? _)
    {
        if (LastResult is null) return;

        CancellationToken token = _cancelRegistry.StartOperation(OperationKeys.Astrocartography);
        RaiseActiveOperationChanged();
        IsCalculating = true;

        try
        {
            StatusText = "Calculating astrocartography lines...";

            ChartResult natal = LastResult;
            AcgResult acgResult = await Task.Run(
                () => _astrocartographyService.Calculate(natal, token), token);

            // H-1.12: write through the Astrocartography child. CurrentResult
            // is assigned first (which raises VisibleLines), then HasResult
            // is set to true (which triggers the parent command-state
            // refresh via the child's RequestCommandStateRefresh signal).
            // This preserves the pre-extraction two-step write shape.
            Astrocartography.CurrentResult = acgResult;
            Astrocartography.HasResult = true;

            StatusText = $"Astrocartography: {acgResult.Lines.Count} lines computed.";

            _logger.Information(
                "Astrocartography computed for '{Name}': {Lines} lines",
                natal.Input.Name, acgResult.Lines.Count);

            // Open (or focus) the floating map window so the user immediately
            // sees the result. The action no-ops if the host hasn't supplied
            // a callback (e.g. in unit tests).
            OpenAcgMapWindowAction?.Invoke();
        }
        catch (OperationCanceledException)
        {
            StatusText = "Astrocartography calculation cancelled.";
            _logger.Information("Astrocartography calculation cancelled by user");
        }
        catch (Exception ex)
        {
            StatusText = $"Astrocartography error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to calculate astrocartography");
        }
        finally
        {
            _cancelRegistry.CompleteOperation(OperationKeys.Astrocartography);
            RaiseActiveOperationChanged();
            IsCalculating = false;
        }
    }

    /// <summary>
    /// Save the current astrocartography result as a Markdown file via
    /// <see cref="IFileDialogService"/>.
    /// </summary>
    private async Task ExportAcgMarkdownAsync(object? _)
    {
        AcgResult? current = Astrocartography.CurrentResult;
        if (current is null) return;

        try
        {
            string personName = SanitiseFileName(current.NatalChart.Input.Name);
            string defaultName = $"{personName}_Astrocartography.md";

            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Astrocartography as Markdown",
                FileDialogFileTypes.MarkdownAndText,
                defaultName,
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting astrocartography Markdown...";

                await AstrocartographyFormatter.SaveAsync(
                    current, path);

                StatusText = $"Astrocartography exported: {path}";
                _logger.Information(
                    "Astrocartography Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Astrocartography export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export astrocartography Markdown");
        }
    }

    /// <summary>
    /// Compute a crossing report for a map click at the given geographic
    /// coordinates and update
    /// <see cref="AstrocartographyViewModel.CrossingReportText"/>. The report is
    /// Core's: <see cref="IAstrocartographyService.GetCrossings"/> with its default
    /// orb (<see cref="AstrocartographyService.DefaultCrossingOrbKilometres"/>, 334 km),
    /// each line listed with its great-circle distance to the place in kilometres.
    /// Safe to call before ACG is computed (returns without side effects).
    /// </summary>
    /// <param name="latitude">Geographic latitude of the clicked point.</param>
    /// <param name="longitude">Geographic longitude of the clicked point.</param>
    public void UpdateCrossingReport(double latitude, double longitude)
    {
        // Reads parent-owned IAstrocartographyService and writes output
        // through the Astrocartography child (H-1.12). Stays on parent per
        // cross-VM rule #4: service readers do not migrate into children.
        AcgResult? current = Astrocartography.CurrentResult;
        if (current is null) return;

        string hemisphereNs = latitude >= 0 ? "N" : "S";
        string hemisphereEw = longitude >= 0 ? "E" : "W";
        string locationName = string.Format(
            CultureInfo.InvariantCulture,
            "{0:F2}\u00b0{1}, {2:F2}\u00b0{3}",
            Math.Abs(latitude), hemisphereNs,
            Math.Abs(longitude), hemisphereEw);

        AcgCrossingReport report = _astrocartographyService.GetCrossings(
            current, latitude, longitude, locationName);

        if (report.Crossings.Count == 0)
        {
            Astrocartography.CrossingReportText =
                $"Crossing Report: {locationName}\nNo angular lines within {report.OrbKilometres.ToString("N0", CultureInfo.InvariantCulture)} km.";
            return;
        }

        System.Text.StringBuilder sb = new();
        sb.Append("Crossing Report: ").AppendLine(locationName);
        sb.AppendLine();
        foreach (AcgCrossing crossing in report.Crossings)
        {
            sb.Append("  ").Append(crossing.BodyName)
              .Append(' ').Append(crossing.LineType)
              .Append(": ")
              .Append(crossing.DistanceKilometres.ToString("N0", CultureInfo.InvariantCulture))
              .AppendLine(" km");
        }

        Astrocartography.CrossingReportText = sb.ToString();
    }

    /// <summary>
    /// Refreshes the can-execute state of astrocartography commands.
    /// Invoked by the <see cref="AstrocartographyViewModel"/> child's
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> signal
    /// whenever <see cref="AstrocartographyViewModel.HasResult"/> flips.
    /// </summary>
    private void RefreshAcgCommandStates()
    {
        ExportAcgMarkdownCommand.RaiseCanExecuteChanged();
        OpenAcgMapWindowCommand.RaiseCanExecuteChanged();
    }

    /// <summary>
    /// Export a medical astrology Markdown report for the current natal chart via the file-save dialog,
    /// offering the chart's sanitised name with <c>_Medical.md</c>.
    /// </summary>
    private async Task ExportMedicalMarkdownAsync(object? _)
    {
        if (LastResult is null) return;

        try
        {
            string? path = await _fileDialogService.ShowSaveAsync(
                "Export Medical Astrology Report",
                FileDialogFileTypes.MarkdownAndAllFiles,
                SanitiseFileName(LastResult.Input.Name) + "_Medical.md",
                ".md");

            if (path is not null)
            {
                StatusText = "Exporting medical report...";

                AstroCast.Core.MedicalAstrology.HealthProfile profile =
                    AstroCast.Core.MedicalAstrology.HealthProfileGenerator.Generate(LastResult);

                string? healthContext = string.IsNullOrWhiteSpace(MedicalHealthContext)
                    ? null
                    : MedicalHealthContext.Trim();
                await AstroCast.Core.Formatting.MedicalMarkdownFormatter.SaveAsync(
                    LastResult, profile, path, healthContext: healthContext);
                StatusText = $"Medical report exported: {path}";
                _logger.Information("Medical Markdown exported to {Path}", path);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Medical export error: {DisplayText.ErrorMessage(ex)}";
            _logger.Error(ex, "Failed to export medical Markdown");
        }
    }

    /// <summary>
    /// Selects the timezone entries matching <paramref name="filterText"/> using
    /// case-insensitive substring matching. Returns <paramref name="source"/>
    /// itself when the filter text is empty; the caller copies the result into
    /// the stable bound collection either way, so this only saves the
    /// intermediate list allocation.
    /// </summary>
    /// <param name="source">The full list of available timezone identifiers.</param>
    /// <param name="filterText">
    /// The substring the user has typed, or empty to show every entry.
    /// </param>
    /// <returns>The entries to display, in source order.</returns>
    /// <remarks>
    /// This is the exact predicate the former WPF <c>CollectionViewSource</c>
    /// filter callback applied — <see cref="StringComparison.OrdinalIgnoreCase"/>
    /// substring containment, everything accepted when the filter is empty —
    /// re-expressed as a pure function over the list. Changing the comparison
    /// here changes what the user sees while typing, and is covered by
    /// <c>TimezoneFilterTests</c>.
    /// </remarks>
    private static IReadOnlyList<string> SelectMatchingTimezones(
        IReadOnlyList<string> source,
        string filterText)
    {
        if (string.IsNullOrEmpty(filterText))
        {
            return source;
        }

        List<string> matches = [];
        foreach (string timezone in source)
        {
            if (timezone.Contains(filterText, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(timezone);
            }
        }

        return matches;
    }

    /// <summary>
    /// Recomputes <see cref="FilteredTimezones"/> from the current filter text
    /// and notifies the bound ComboBox.
    /// </summary>
    /// <remarks>
    /// Direct replacement for the former <c>CollectionViewSource.View.Refresh()</c>
    /// call, and must be invoked wherever <c>_timezoneFilterText</c> changes.
    /// The bound collection is mutated in place — never replaced — so the
    /// ComboBox keeps its <c>ItemsSource</c> instance and the selector
    /// preserves the selected item across the single <c>Reset</c> that
    /// <see cref="ObservableRangeCollection{T}.ReplaceAll"/> raises. No
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> is raised for
    /// <see cref="FilteredTimezones"/> itself: the property reference does not
    /// change, and the collection does its own notifying.
    /// </remarks>
    private void RefreshFilteredTimezones()
    {
        _filteredTimezones.ReplaceAll(SelectMatchingTimezones(AvailableTimezones, _timezoneFilterText));
    }

    /// <summary>
    /// Sanitise a name for use as a file name. Delegates to the shared
    /// <see cref="FileNameSanitiser"/> utility which also removes path
    /// traversal sequences and replaces spaces with underscores.
    /// </summary>
    private static string SanitiseFileName(string name) =>
        FileNameSanitiser.Sanitise(name);

    /// <summary>
    /// Create a configured HttpClient for Nominatim API requests.
    /// Sets a User-Agent header as required by Nominatim usage policy,
    /// and limits response size to prevent excessive memory consumption.
    /// </summary>
    private static HttpClient CreateHttpClient()
    {
        HttpClient client = new()
        {
            Timeout = TimeSpan.FromSeconds(10),
            // Nominatim returns compact JSON (~1-5 KB per response for typical queries).
            // The 1 MB cap defends against malformed or malicious oversized responses while
            // leaving ample headroom for legitimate multi-result search payloads.
            MaxResponseContentBufferSize = 1_000_000,
        };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("AstroCast/1.0");
        return client;
    }

    #endregion

    #region INotifyPropertyChanged

    /// <inheritdoc />
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raise the <see cref="PropertyChanged"/> event for the specified property.
    /// </summary>
    /// <param name="propertyName">Name of the changed property (auto-detected by compiler).</param>
    /// <remarks>
    /// H-2: If invoked from a non-UI thread (background <c>Task.Run</c> work, timer callbacks,
    /// or async continuations that escaped the UI sync context) the event is marshalled to the
    /// UI thread through <see cref="IUiDispatcher"/>. Without this, a binding that listens to
    /// the event can trigger cross-thread access on UI elements and throw
    /// <see cref="InvalidOperationException"/>. When no dispatcher was injected, or the
    /// injected one reports no UI thread (e.g., under xUnit where no UI framework is
    /// bootstrapped), the event is raised inline — the headless contract on
    /// <see cref="IUiDispatcher"/>. Any future edit to this method must preserve both the
    /// marshalling and that inline fallback.
    /// </remarks>
    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChangedEventHandler? handler = PropertyChanged;
        if (handler is null)
        {
            return;
        }

        PropertyChangedEventArgs args = new(propertyName);
        if (_uiDispatcher is null || _uiDispatcher.CheckAccess())
        {
            handler(this, args);
        }
        else
        {
            _uiDispatcher.Post(() => handler(this, args));
        }
    }

    /// <summary>
    /// Set a backing field and raise <see cref="PropertyChanged"/> if the value changed.
    /// </summary>
    /// <typeparam name="T">The property type.</typeparam>
    /// <param name="field">Reference to the backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="propertyName">Name of the changed property (auto-detected by compiler).</param>
    /// <returns>True if the value changed, false if it was already equal.</returns>
    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion

}
