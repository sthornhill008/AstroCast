using System.ComponentModel;
using System.Globalization;
using System.Net.Http;

using GeoTimeZone;
using Serilog;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;
using AstroCast.Services;

namespace AstroCast.ViewModels;

/// <summary>
/// Child view model that owns the Electional (best-time) panel state: the
/// election type, goal description, date range, event location (with a nested
/// <see cref="LocationSearchViewModel"/>), candidate configuration, time
/// constraints, and the most recent <see cref="Core.Models.Electional.ElectionalResult"/>.
/// Extracted from <see cref="MainViewModel"/> by Session H-1.6 as the sixth
/// decomposition child — and the first to compose a nested
/// <see cref="LocationSearchViewModel"/> under its umbrella rather than as a
/// parent-owned sibling.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> This VM owns the Electional panel's data surface:
/// <see cref="ElectionType"/>, <see cref="Goal"/>, <see cref="StartDate"/>,
/// <see cref="EndDate"/>, <see cref="LocationName"/>, <see cref="LatitudeText"/>,
/// <see cref="LongitudeText"/>, <see cref="TimeZone"/>,
/// <see cref="SelectedTimezone"/>, <see cref="IsTimezoneValid"/>,
/// <see cref="FilteredTimezones"/>, <see cref="CandidateCount"/>,
/// <see cref="AvoidMercuryRetrograde"/>, <see cref="ConstraintNotes"/>,
/// <see cref="EarliestTimeText"/>, <see cref="LatestTimeText"/>,
/// the seven allowed-day flags (<see cref="AllowMonday"/> through
/// <see cref="AllowSunday"/>),
/// <see cref="SelectedNatalChart"/>, <see cref="ElectionTypes"/>, and the
/// <see cref="ElectionalResult"/> output. XAML bindings for these fields live
/// under <c>Electional.X</c> in <c>MainWindow.xaml</c>.
/// </para>
/// <para>
/// <b>Nested <see cref="LocationSearch"/>.</b> The electional panel's location
/// search was a parent-owned sibling (<c>ElectionalLocationSearch</c>) under
/// H-1.3. H-1.6 migrates it to a nested child under this VM. The child is
/// constructed with the shared <see cref="HttpClient"/> and
/// <see cref="NominatimRateLimiter"/> threaded through from the parent so the
/// Nominatim 1-request-per-second policy remains honoured globally across all
/// four location-search instances (Chart Entry, Electional, Mundane,
/// Relocation). Bindings on <c>ElectionalLocationSearch.X</c> are rewritten to
/// <c>Electional.LocationSearch.X</c>.
/// </para>
/// <para>
/// <b>Follow-on handler moved from parent (Rule #4 refinement).</b>
/// <see cref="OnLocationSearchPropertyChanged"/> was previously
/// <c>MainViewModel.OnElectionalLocationSearchPropertyChanged</c>. Cross-VM rule
/// #4 keeps commands on the parent when they write to sibling-owned state, but
/// this handler's writes (<see cref="LatitudeText"/>,
/// <see cref="LongitudeText"/>, <see cref="LocationName"/>,
/// <see cref="TimeZone"/>) are now all intra-VM — they target <c>this.*</c>
/// fields under the same umbrella. The handler therefore moves with its
/// owner into this child. <see cref="MainViewModel.StatusText"/> is still
/// written because status text is a parent-owned concern (rule #2 last
/// resort).
/// </para>
/// <para>
/// <b>Commands stay on parent (rule #4).</b>
/// <c>CalculateElectionalCommand</c>, <c>ExportElectionalMarkdownCommand</c>,
/// <c>LookupElectionalLocationCommand</c>, and <c>UseNatalLocationCommand</c>
/// remain on <see cref="MainViewModel"/> because they read the parent-owned
/// <c>LastResult</c> (natal chart) and <see cref="Parent"/>-side services
/// (<c>IChartEngine</c>, <c>IElectionalService</c>). The command methods read
/// inputs from this child and write the output back to
/// <see cref="ElectionalResult"/>.
/// </para>
/// <para>
/// <b>Command-state refresh.</b> The <see cref="Goal"/>,
/// <see cref="LocationName"/>, <see cref="SelectedNatalChart"/>, and
/// <see cref="ElectionalResult"/> setters raise
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the parent
/// re-evaluates <c>CanExecute</c> on all four electional commands. The other
/// setters do not raise the event because no command's <c>CanExecute</c>
/// depends on them.
/// </para>
/// <para>
/// <b>Timezone filter.</b> The substring-filter infrastructure behind
/// <see cref="FilteredTimezones"/> migrated wholesale from the parent into this
/// child under H-1.6 because it's electional-specific. The main Chart Entry
/// timezone ComboBox continues to use the parent-owned equivalent, and the two
/// hold independent filter state over one shared source list. Phase 3 of the
/// macOS port replaced the original WPF <c>CollectionViewSource</c> with plain
/// filter-and-notify logic; the observable behaviour is unchanged.
/// </para>
/// <para>
/// <b>DateTime.Today coupling.</b> The field initialisers for
/// <see cref="StartDate"/> and <see cref="EndDate"/> call
/// <see cref="DateTime.Today"/> directly, as did the original fields on
/// <see cref="MainViewModel"/>. Session Golden Refresh watch-item F-GR-2 tracks
/// migrating this to an injected <c>IClock</c> — Session H-1.6 preserves the
/// existing coupling to keep the extraction a pure refactor.
/// </para>
/// </remarks>
public sealed class ElectionalViewModel : ChildViewModelBase
{
    #region Fields

    private readonly ILogger _logger;
    private string _timezoneFilterText = string.Empty;
    private readonly ObservableRangeCollection<string> _filteredTimezones;

    private ElectionType _electionType = ElectionType.BusinessLaunch;
    private string _goal = string.Empty;
    private DateTime _startDate = DateTime.Today.AddDays(1);
    private DateTime _endDate = DateTime.Today.AddMonths(1);
    private string _locationName = string.Empty;
    private string _latitudeText = "0";
    private string _longitudeText = "0";
    private string _timeZone = string.Empty;
    private string? _selectedTimezone;
    private int _candidateCount = 3;
    private bool _avoidMercuryRetrograde = true;
    private string _constraintNotes = string.Empty;
    private string _earliestTimeText = string.Empty;
    private string _latestTimeText = string.Empty;
    private bool _allowMonday = true;
    private bool _allowTuesday = true;
    private bool _allowWednesday = true;
    private bool _allowThursday = true;
    private bool _allowFriday = true;
    private bool _allowSaturday = true;
    private bool _allowSunday = true;
    private ElectionalResult? _electionalResult;
    private ChartEntity? _selectedNatalChart;

    #endregion

    #region Constructor

    /// <summary>
    /// Initialises the electional view model and its nested
    /// <see cref="LocationSearch"/> child.
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
    public ElectionalViewModel(
        MainViewModel parent,
        HttpClient httpClient,
        NominatimRateLimiter rateLimiter,
        ILogger logger)
        : base(parent)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger.ForContext<ElectionalViewModel>();

        // Compose the nested LocationSearch. Construct BEFORE subscribing its
        // PropertyChanged so the handler wiring stays local to the constructor.
        LocationSearch = new LocationSearchViewModel(
            parent, httpClient, rateLimiter, logger, "electional location");
        LocationSearch.PropertyChanged += OnLocationSearchPropertyChanged;

        // Initialise the electional-specific substring filter over the parent's
        // shared AvailableTimezones list. The filter state is private to this
        // child, so the Electional and Chart Entry dropdowns narrow
        // independently despite reading one source list. No filter text yet, so
        // the collection starts as the full source list. This is the ONE
        // instance the Electional ComboBox binds for the lifetime of this view
        // model; refreshes mutate it in place (see RefreshFilteredTimezones).
        _filteredTimezones = new ObservableRangeCollection<string>(parent.AvailableTimezones);

        // Subscribe AFTER _filteredTimezones exists — the handler touches it on
        // the first notification. See OnSelfPropertyChanged for why the filter
        // refresh is driven from a notification rather than from the setter.
        PropertyChanged += OnSelfPropertyChanged;
    }

    #endregion

    #region Nested Children

    /// <summary>
    /// Nested location-search child for the Electional panel. Migrated from
    /// parent-owned sibling <c>MainViewModel.ElectionalLocationSearch</c> under
    /// Session H-1.6. Bindings previously on
    /// <c>ElectionalLocationSearch.X</c> are now on
    /// <c>Electional.LocationSearch.X</c>. Follow-on logic when the user picks
    /// a result lives in <see cref="OnLocationSearchPropertyChanged"/>.
    /// </summary>
    public LocationSearchViewModel LocationSearch { get; }

    #endregion

    #region Properties — Inputs

    /// <summary>Gets or sets the election type (Business Launch, Wedding, etc.).</summary>
    public ElectionType ElectionType
    {
        get => _electionType;
        set => SetProperty(ref _electionType, value);
    }

    /// <summary>Gets or sets the free-text description of the election goal.</summary>
    public string Goal
    {
        get => _goal;
        set
        {
            if (SetProperty(ref _goal, value))
                RequestCommandStateRefresh();
        }
    }

    /// <summary>Gets or sets the first acceptable date for the event.</summary>
    public DateTime StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    /// <summary>Gets or sets the last acceptable date for the event.</summary>
    public DateTime EndDate
    {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }

    /// <summary>Gets or sets the event location name.</summary>
    public string LocationName
    {
        get => _locationName;
        set
        {
            if (SetProperty(ref _locationName, value))
                RequestCommandStateRefresh();
        }
    }

    /// <summary>Gets or sets the event location latitude as text.</summary>
    public string LatitudeText
    {
        get => _latitudeText;
        set => SetProperty(ref _latitudeText, value);
    }

    /// <summary>Gets or sets the event location longitude as text.</summary>
    public string LongitudeText
    {
        get => _longitudeText;
        set => SetProperty(ref _longitudeText, value);
    }

    /// <summary>Gets or sets the IANA timezone for the event location.</summary>
    /// <remarks>
    /// <para>
    /// Setter validates against <see cref="MainViewModel.AvailableTimezones"/>
    /// (read via <see cref="ChildViewModelBase.Parent"/>) and syncs
    /// <see cref="SelectedTimezone"/> when a case-insensitive match is found.
    /// </para>
    /// <para>
    /// It also requests a command-state refresh: since 1.4.0 Calculate's
    /// <c>CanExecute</c> requires a non-blank zone, Natal's rule (port plan §9
    /// item 16).
    /// </para>
    /// <para>
    /// It does <b>not</b> re-apply the substring filter. That happens in
    /// <see cref="OnSelfPropertyChanged"/>, and the separation is load-bearing
    /// rather than stylistic — see the remarks there.
    /// </para>
    /// </remarks>
    public string TimeZone
    {
        get => _timeZone;
        set
        {
            if (SetProperty(ref _timeZone, value))
            {
                string trimmed = value?.Trim() ?? string.Empty;
                IsTimezoneValid = string.IsNullOrEmpty(trimmed)
                    || Parent.AvailableTimezones.Any(tz =>
                        tz.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
                OnPropertyChanged(nameof(IsTimezoneValid));

                // Sync SelectedTimezone for ComboBox SelectedItem binding
                string? match = Parent.AvailableTimezones.FirstOrDefault(tz =>
                    tz.Equals(trimmed, StringComparison.OrdinalIgnoreCase));
                if (match != null && _selectedTimezone != match)
                {
                    _selectedTimezone = match;
                    OnPropertyChanged(nameof(SelectedTimezone));
                }

                // Calculate's CanExecute requires a non-blank zone (1.4.0, port plan §9 item 16).
                // The raise is here, not in the callers: Use Natal Location sets LocationName,
                // which raises, before it sets the zone.
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets or sets the currently selected electional timezone from the
    /// dropdown list. Syncs with the <see cref="TimeZone"/> text property for
    /// programmatic selection.
    /// </summary>
    public string? SelectedTimezone
    {
        get => _selectedTimezone;
        set
        {
            if (SetProperty(ref _selectedTimezone, value) && value != null)
            {
                if (!string.Equals(_timeZone, value, StringComparison.OrdinalIgnoreCase))
                {
                    TimeZone = value;
                }
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the current electional timezone string
    /// is a recognized IANA timezone identifier from the available list.
    /// </summary>
    public bool IsTimezoneValid { get; private set; } = true;

    /// <summary>
    /// Gets the filtered list of timezone identifiers for the electional
    /// timezone ComboBox. Filters by case-insensitive substring match as the
    /// user types.
    /// </summary>
    /// <remarks>
    /// Formerly a WPF <c>ICollectionView</c> over a <c>CollectionViewSource</c>,
    /// now an <see cref="ObservableRangeCollection{T}"/> refreshed in place so
    /// this type carries no UI-framework dependency. <b>Returns one instance
    /// for the lifetime of the view model — instance stability is load-bearing;
    /// do not reassign it</b>: see the equivalent property on
    /// <see cref="MainViewModel"/> for the full selection-preservation
    /// rationale, pinned by <c>TimezoneFilterStabilityTests</c>. Filter state is
    /// private to this child, so narrowing the Electional dropdown never affects
    /// the Chart Entry one even though both read
    /// <see cref="MainViewModel.AvailableTimezones"/>.
    /// </remarks>
    public IReadOnlyList<string> FilteredTimezones => _filteredTimezones;

    /// <summary>Gets or sets the number of candidate windows (1–5).</summary>
    public int CandidateCount
    {
        get => _candidateCount;
        set => SetProperty(ref _candidateCount, Math.Clamp(value, 1, 5));
    }

    /// <summary>Gets or sets whether to avoid Mercury retrograde periods.</summary>
    public bool AvoidMercuryRetrograde
    {
        get => _avoidMercuryRetrograde;
        set => SetProperty(ref _avoidMercuryRetrograde, value);
    }

    /// <summary>Gets or sets the free-text constraint notes.</summary>
    public string ConstraintNotes
    {
        get => _constraintNotes;
        set => SetProperty(ref _constraintNotes, value);
    }

    /// <summary>Gets or sets the earliest acceptable time text (HH:mm format, empty = no constraint).</summary>
    public string EarliestTimeText
    {
        get => _earliestTimeText;
        set => SetProperty(ref _earliestTimeText, value);
    }

    /// <summary>Gets or sets the latest acceptable time text (HH:mm format, empty = no constraint).</summary>
    public string LatestTimeText
    {
        get => _latestTimeText;
        set => SetProperty(ref _latestTimeText, value);
    }

    /// <summary>Gets or sets whether Monday is an allowed candidate day.</summary>
    public bool AllowMonday
    {
        get => _allowMonday;
        set => SetProperty(ref _allowMonday, value);
    }

    /// <summary>Gets or sets whether Tuesday is an allowed candidate day.</summary>
    public bool AllowTuesday
    {
        get => _allowTuesday;
        set => SetProperty(ref _allowTuesday, value);
    }

    /// <summary>Gets or sets whether Wednesday is an allowed candidate day.</summary>
    public bool AllowWednesday
    {
        get => _allowWednesday;
        set => SetProperty(ref _allowWednesday, value);
    }

    /// <summary>Gets or sets whether Thursday is an allowed candidate day.</summary>
    public bool AllowThursday
    {
        get => _allowThursday;
        set => SetProperty(ref _allowThursday, value);
    }

    /// <summary>Gets or sets whether Friday is an allowed candidate day.</summary>
    public bool AllowFriday
    {
        get => _allowFriday;
        set => SetProperty(ref _allowFriday, value);
    }

    /// <summary>Gets or sets whether Saturday is an allowed candidate day.</summary>
    public bool AllowSaturday
    {
        get => _allowSaturday;
        set => SetProperty(ref _allowSaturday, value);
    }

    /// <summary>Gets or sets whether Sunday is an allowed candidate day.</summary>
    public bool AllowSunday
    {
        get => _allowSunday;
        set => SetProperty(ref _allowSunday, value);
    }

    /// <summary>
    /// Builds the day-of-week constraint for the electional search from the
    /// seven day checkboxes. Returns null when all days are allowed (the
    /// unrestricted default — no behaviour change for the engine), otherwise
    /// the list of allowed days in Monday-first order. An empty list (no days
    /// checked) is returned as-is; the search then yields no candidates and
    /// the export carries the no-candidates notice.
    /// </summary>
    /// <returns>The allowed days, or null when unrestricted.</returns>
    public IReadOnlyList<DayOfWeek>? GetAllowedDays()
    {
        (bool Allowed, DayOfWeek Day)[] flags =
        [
            (_allowMonday, DayOfWeek.Monday),
            (_allowTuesday, DayOfWeek.Tuesday),
            (_allowWednesday, DayOfWeek.Wednesday),
            (_allowThursday, DayOfWeek.Thursday),
            (_allowFriday, DayOfWeek.Friday),
            (_allowSaturday, DayOfWeek.Saturday),
            (_allowSunday, DayOfWeek.Sunday),
        ];

        List<DayOfWeek> allowed = flags
            .Where(f => f.Allowed)
            .Select(f => f.Day)
            .ToList();

        return allowed.Count == flags.Length ? null : allowed.AsReadOnly();
    }

    /// <summary>
    /// Gets or sets the natal chart the electional search scores against. When
    /// set, the electional engine casts this chart instead of using
    /// <c>MainViewModel.LastResult</c> or the Chart panel's fields. Bound to the
    /// "Natal Chart" ComboBox on the Electional panel, over
    /// <c>MainViewModel.AllSavedCharts</c>, whose refresh keeps this selection by
    /// Id. Each Load and each Calculate &amp; Save on the Chart panel sets it to
    /// the chart loaded or saved, so the picker follows the Chart panel; a pick
    /// by hand holds until the next of either (1.6.1, item C, branch
    /// ElectionalPicker), as the Relocation picker has since 1.4.0.
    /// </summary>
    public ChartEntity? SelectedNatalChart
    {
        get => _selectedNatalChart;
        set
        {
            if (SetProperty(ref _selectedNatalChart, value))
            {
                RequestCommandStateRefresh();
                OnPropertyChanged(nameof(ChartInUseText));
            }
        }
    }

    /// <summary>
    /// Gets the line under the Natal Chart picker naming the chart the search will score against: the picked
    /// chart, "{Name} — {type}, {reading}, {place}"; else the Chart panel's chart (<c>MainViewModel.LastResult</c>)
    /// the same way with " (the Chart panel's chart, not saved)"; else that there is none and that Calculate
    /// casts one from the Chart panel's fields (1.6.1, item C, branch ElectionalPicker).
    /// </summary>
    /// <remarks>
    /// The type and the reading come from <see cref="DisplayText"/>, as the saved-chart row's and the Relocation
    /// panel's line do: the type by name, the reading as typed, never through <c>CalendarDateFormatter</c>. Each
    /// Load and each Calculate &amp; Save picks the Chart panel's chart in the picker, so the Chart panel's chart
    /// is unsaved here only when nothing is picked: the search's own chart cast from the Chart panel's fields, or
    /// a picked chart since deleted. Raised by <see cref="SelectedNatalChart"/>'s setter and, through
    /// <see cref="OnChartPanelChartChanged"/>, by the parent's <c>LastResult</c> setter.
    /// </remarks>
    public string ChartInUseText
    {
        get
        {
            if (_selectedNatalChart is { } picked)
            {
                return $"{picked.Name} — {DisplayText.EventTypeName(picked.EventType)}, "
                    + $"{DisplayText.StoredReading(picked.LocalDateTime)}, {picked.LocationName}";
            }

            if (Parent.LastResult?.Input is { } chartPanel)
            {
                return $"{chartPanel.Name} — {DisplayText.EventTypeName(chartPanel.EventType)}, "
                    + $"{DisplayText.Reading(chartPanel.LocalDateTime)}, {chartPanel.LocationName} "
                    + "(the Chart panel's chart, not saved)";
            }

            return "No chart yet: pick a saved chart, or load or calculate one on the Chart panel; "
                + "otherwise Calculate casts one from the Chart panel's fields.";
        }
    }

    /// <summary>
    /// Raises <see cref="ChartInUseText"/> for a change of the Chart panel's chart, which the line names when
    /// no saved chart is picked. Called by the parent's <c>LastResult</c> setter.
    /// </summary>
    internal void OnChartPanelChartChanged() => OnPropertyChanged(nameof(ChartInUseText));

    /// <summary>Gets the available <see cref="Core.Enums.ElectionType"/> values for the ComboBox.</summary>
    public IReadOnlyList<ElectionType> ElectionTypes { get; } =
        Enum.GetValues<ElectionType>().ToList().AsReadOnly();

    #endregion

    #region Result

    /// <summary>
    /// Gets or sets the most recent
    /// <see cref="Core.Models.Electional.ElectionalResult"/> produced by
    /// <c>MainViewModel.CalculateElectionalAsync</c>. Setting this raises
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent re-evaluates <c>CanExecute</c> on the electional commands whose
    /// predicates depend on <see cref="HasResult"/>.
    /// </summary>
    public ElectionalResult? ElectionalResult
    {
        get => _electionalResult;
        set
        {
            if (SetProperty(ref _electionalResult, value))
            {
                OnPropertyChanged(nameof(HasResult));
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether an electional result is available for
    /// export. Derived from <see cref="ElectionalResult"/> — no separate
    /// backing field.
    /// </summary>
    public bool HasResult => _electionalResult is not null;

    #endregion

    #region Follow-on Handlers (migrated from parent per Rule #4 refinement)

    /// <summary>
    /// When the user picks a <see cref="LocationSearchViewModel.SelectedResult"/>
    /// from the nested electional location search, copy the coordinates and
    /// display name into this VM's own form fields, resolve the IANA timezone,
    /// and update the parent-owned status text. Previously
    /// <c>MainViewModel.OnElectionalLocationSearchPropertyChanged</c>; moved
    /// under H-1.6 because every write targets <c>this.*</c> fields now that
    /// the electional state is unified under this VM.
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

        Parent.StatusText = $"Electional location set: {value.DisplayName}";
        RequestCommandStateRefresh();
    }

    /// <summary>
    /// Re-applies the substring filter behind <see cref="FilteredTimezones"/>
    /// whenever <see cref="TimeZone"/> changes.
    /// </summary>
    /// <param name="sender">This view model; unused.</param>
    /// <param name="e">The change notification, filtered to <see cref="TimeZone"/>.</param>
    /// <remarks>
    /// <para>
    /// <b>Why this is a notification handler and not two lines in the setter.</b>
    /// It used to be the latter, and that shipped a defect: choosing a zone from
    /// the Electional drop-down displayed the chosen zone while
    /// <see cref="TimeZone"/> kept the fragment the user had typed, so the next
    /// search failed with
    /// <c>TimeZoneNotFoundException: The time zone ID 'York' was not found</c>.
    /// </para>
    /// <para>
    /// <b>The mechanism is ordering, not logic.</b> The filter refresh replaces
    /// the contents of the very collection the picker is bound to, raising a
    /// single <c>Reset</c> (see
    /// <see cref="ObservableRangeCollection{T}.ReplaceAll"/>). Because
    /// <c>SetProperty</c> raises <c>PropertyChanged</c> before it returns, a
    /// refresh written at the end of the setter body lands <i>after</i> the
    /// control has processed the text change — pulling the list out from under
    /// the drop-down's selection adapter mid-commit, so the adapter can no
    /// longer resolve the item the user chose. Raised from here the Reset
    /// arrives during the notification instead, and the adapter is rebuilt
    /// against a collection that is already settled.
    /// </para>
    /// <para>
    /// <b>This is the Chart Entry sequence, reproduced.</b> That panel's picker
    /// was never affected precisely because its refresh has always been a
    /// notification handler —
    /// <c>MainViewModel.OnChartEntryPropertyChanged</c> — rather than setter
    /// body. The two panels now behave identically, which is what
    /// <c>TimezoneCommitTests</c> asserts, on both, under the same keyboard
    /// commit.
    /// </para>
    /// <para>
    /// <b>Self-subscription rather than a parent handler.</b> Chart Entry's
    /// equivalent lives on the parent because the collection it refreshes is
    /// parent-owned. <see cref="FilteredTimezones"/> moved to this child under
    /// H-1.6, so the handler belongs here; routing it through
    /// <see cref="MainViewModel"/> would re-introduce exactly the cross-VM
    /// coupling that decomposition removed, for no gain.
    /// </para>
    /// </remarks>
    private void OnSelfPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TimeZone)) return;

        string trimmed = (TimeZone ?? string.Empty).Trim();
        string? match = Parent.AvailableTimezones.FirstOrDefault(tz =>
            tz.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

        // Substring-filter the dropdown: show all when empty or exact match
        _timezoneFilterText = (string.IsNullOrEmpty(trimmed) || match != null)
            ? string.Empty : trimmed;
        RefreshFilteredTimezones();
    }

    /// <summary>
    /// Recomputes <see cref="FilteredTimezones"/> from the current filter text
    /// and notifies the bound ComboBox.
    /// </summary>
    /// <remarks>
    /// Direct replacement for the former <c>CollectionViewSource.View.Refresh()</c>
    /// call, and must be invoked wherever <c>_timezoneFilterText</c> changes.
    /// The predicate — accept everything when the filter text is empty,
    /// otherwise <see cref="StringComparison.OrdinalIgnoreCase"/> substring
    /// containment — is preserved verbatim from the WPF filter callback it
    /// replaces (previously <c>MainViewModel.OnElectionalTimezoneFilter</c>).
    /// The bound collection is mutated in place — never replaced — so the
    /// ComboBox keeps its <c>ItemsSource</c> instance and the selector
    /// preserves the selected item across the single <c>Reset</c> that
    /// <see cref="ObservableRangeCollection{T}.ReplaceAll"/> raises. No
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> is raised for
    /// <see cref="FilteredTimezones"/> itself: the property reference does not
    /// change, and the collection does its own notifying. Covered by
    /// <c>TimezoneFilterTests</c> and <c>TimezoneFilterStabilityTests</c>.
    /// </remarks>
    private void RefreshFilteredTimezones()
    {
        if (string.IsNullOrEmpty(_timezoneFilterText))
        {
            _filteredTimezones.ReplaceAll(Parent.AvailableTimezones);
            return;
        }

        List<string> matches = [];
        foreach (string timezone in Parent.AvailableTimezones)
        {
            if (timezone.Contains(_timezoneFilterText, StringComparison.OrdinalIgnoreCase))
            {
                matches.Add(timezone);
            }
        }

        _filteredTimezones.ReplaceAll(matches);
    }

    #endregion

    #region Reset

    /// <summary>
    /// Resets every Electional panel field to its default value, including the
    /// nested <see cref="LocationSearch"/>. Uses the field-bypass pattern from
    /// H-1.2's <c>ChartEntryViewModel.PopulateFrom</c> so a single
    /// <see cref="ChildViewModelBase.RequestCommandStateRefresh"/> fires at the
    /// end of the reset rather than cascading once per setter. Called from the
    /// parent's <c>ClearForm</c> flow, replacing the 15 per-field resets that
    /// lived there pre-H-1.6.
    /// </summary>
    /// <remarks>
    /// The field-bypass pattern requires manually replicating any non-refresh
    /// side effects that the setters would have triggered. For
    /// <see cref="TimeZone"/> that means resetting
    /// <see cref="IsTimezoneValid"/> to <see langword="true"/> and clearing
    /// the substring filter; <see cref="SelectedTimezone"/> is left intact to
    /// match pre-H-1.6 <c>ClearForm</c> behaviour (the old flow wrote
    /// <c>ElectionalTimeZone = string.Empty</c> which did not clear the
    /// dropdown selection).
    /// </remarks>
    public void Clear()
    {
        _electionType = ElectionType.BusinessLaunch;
        OnPropertyChanged(nameof(ElectionType));

        _goal = string.Empty;
        OnPropertyChanged(nameof(Goal));

        _startDate = DateTime.Today.AddDays(1);
        OnPropertyChanged(nameof(StartDate));

        _endDate = DateTime.Today.AddMonths(1);
        OnPropertyChanged(nameof(EndDate));

        _locationName = string.Empty;
        OnPropertyChanged(nameof(LocationName));

        _latitudeText = "0";
        OnPropertyChanged(nameof(LatitudeText));

        _longitudeText = "0";
        OnPropertyChanged(nameof(LongitudeText));

        _timeZone = string.Empty;
        // The filter refresh rides on this notification — OnSelfPropertyChanged
        // clears the filter text and re-widens the list. Doing it explicitly
        // here as well would raise a second, identical Reset. Chart Entry's
        // field-bypass paths rely on the same notification for the same reason.
        OnPropertyChanged(nameof(TimeZone));

        // Replicate the TimeZone setter's remaining side effect without
        // re-entering the setter.
        IsTimezoneValid = true;
        OnPropertyChanged(nameof(IsTimezoneValid));

        _candidateCount = 3;
        OnPropertyChanged(nameof(CandidateCount));

        _avoidMercuryRetrograde = true;
        OnPropertyChanged(nameof(AvoidMercuryRetrograde));

        _constraintNotes = string.Empty;
        OnPropertyChanged(nameof(ConstraintNotes));

        _earliestTimeText = string.Empty;
        OnPropertyChanged(nameof(EarliestTimeText));

        _latestTimeText = string.Empty;
        OnPropertyChanged(nameof(LatestTimeText));

        _allowMonday = true;
        OnPropertyChanged(nameof(AllowMonday));
        _allowTuesday = true;
        OnPropertyChanged(nameof(AllowTuesday));
        _allowWednesday = true;
        OnPropertyChanged(nameof(AllowWednesday));
        _allowThursday = true;
        OnPropertyChanged(nameof(AllowThursday));
        _allowFriday = true;
        OnPropertyChanged(nameof(AllowFriday));
        _allowSaturday = true;
        OnPropertyChanged(nameof(AllowSaturday));
        _allowSunday = true;
        OnPropertyChanged(nameof(AllowSunday));

        _electionalResult = null;
        OnPropertyChanged(nameof(ElectionalResult));
        OnPropertyChanged(nameof(HasResult));

        // Clear the nested location search too — pre-H-1.6 the sibling
        // ElectionalLocationSearch was not reset from ClearForm, but under the
        // nested design LocationSearch is part of the electional umbrella and
        // clearing the panel clears its search state.
        LocationSearch.Clear();

        RequestCommandStateRefresh();
    }

    #endregion
}
