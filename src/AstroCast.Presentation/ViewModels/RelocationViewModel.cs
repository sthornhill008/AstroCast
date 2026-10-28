using System.ComponentModel;
using System.Globalization;
using System.Net.Http;

using Serilog;

using AstroCast.Core.Data;
using AstroCast.Core.Models;
using AstroCast.Services;

namespace AstroCast.ViewModels;

/// <summary>
/// Child view model that owns the Relocation panel state: the natal-chart
/// selection, the target-location form (location name, latitude text,
/// longitude text), the most recent <see cref="RelocationResult"/> output,
/// and the nested location-search child used to populate those fields via
/// Nominatim geocoding. Extracted from <see cref="MainViewModel"/> by
/// Session H-1.11 as the eleventh decomposition child and the third
/// application of the H-1.6 nested LocationSearch migration pattern.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> This VM owns the Relocation panel's data surface:
/// <see cref="SelectedNatalChart"/> and <see cref="ChartInUseText"/> (the
/// Birth Chart section), <see cref="LatitudeText"/>,
/// <see cref="LongitudeText"/>, <see cref="LocationName"/> (the New Location
/// section), <see cref="CurrentResult"/>, and <see cref="HasResult"/>. XAML bindings
/// for every one of these fields live under <c>Relocation.X</c> in
/// <c>MainWindow.xaml</c>. Property names are the pre-extraction names with
/// the <c>Relocation</c> prefix stripped inside the child, matching the
/// H-1.2 / H-1.5 / H-1.6 / H-1.9 / H-1.10 precedent.
/// </para>
/// <para>
/// <b>Audit Question 1 \u2014 single-result.</b>
/// <see cref="Core.Interfaces.IRelocationService"/> produces one relocated
/// chart per invocation from one set of target coordinates; there is no
/// multi-result scan analogue. <see cref="CurrentResult"/> is therefore a
/// single nullable reference paired with a single <see cref="HasResult"/>
/// flag \u2014 the H-1.5 Transits and H-1.10 R/I Timeline shape, not the
/// H-1.4 / H-1.7 / H-1.8 / H-1.9 multi-result shape.
/// </para>
/// <para>
/// <b>Audit Question 2 \u2014 derived <c>HasResult</c>.</b>
/// <c>IRelocationService.Calculate</c> returns a non-nullable
/// <see cref="RelocationResult"/> \u2014 it throws on failure rather than
/// returning a sentinel empty value, so there is no "zero-match empty
/// series" analogue to justify the value-based pattern that H-1.8 / H-1.9 /
/// H-1.10 use. The pre-extraction code wrote
/// <c>HasRelocationResult = true</c> immediately after assigning the result,
/// which is equivalent to the derived <c>HasResult =&gt; _result is not
/// null</c> form. H-1.11 canonicalises to the derived pattern established by
/// H-1.7 <c>HasSolarReturnResult</c>: the <see cref="CurrentResult"/>
/// setter raises <see cref="INotifyPropertyChanged.PropertyChanged"/> for
/// both itself and <see cref="HasResult"/>, and requests a single command
/// refresh. The pre-extraction backing-only <c>_currentRelocationResult</c>
/// field is promoted to a public <see cref="CurrentResult"/> property for
/// testability \u2014 same canonicalisation H-1.10 applied to the R/I
/// Timeline child.
/// </para>
/// <para>
/// <b>Audit Question 3 \u2014 F-CS-Relocation / CultureInfo fix included.</b>
/// The pre-H-1.11 <c>OnRelocationLocationSearchPropertyChanged</c> handler
/// wrote <c>value.Latitude.ToString("F4")</c> and
/// <c>value.Longitude.ToString("F4")</c> without an
/// <see cref="IFormatProvider"/>, which made the decimal separator
/// locale-dependent (e.g. <c>"34,0522"</c> on <c>de-DE</c>). The paired
/// <c>double.TryParse</c> calls in <c>MainViewModel.CalculateRelocationAsync</c>
/// likewise read the texts without a culture, completing the round-trip on
/// English locales but leaving the pair inconsistent with the third write
/// site, the command that copied the chart's birth place (removed in 1.4.0, branch Relocate),
/// which already used <see cref="CultureInfo.InvariantCulture"/>. Audit-confirmed trivial
/// scope: four single-argument additions across one write path (this VM's
/// <see cref="OnLocationSearchPropertyChanged"/> handler) plus one parse
/// path (<c>MainViewModel.CalculateRelocationAsync</c>). H-1.11 threads
/// <see cref="CultureInfo.InvariantCulture"/> through both call sites in
/// the handler so all three Relocation formatter call sites agree on
/// culture; a matching pair of <c>double.TryParse(..., NumberStyles.Float,
/// CultureInfo.InvariantCulture, out ...)</c> overloads closes the
/// round-trip on the parent. The <c>RelocationViewModelTests.cs</c> file
/// includes a dedicated regression test that flips
/// <see cref="CultureInfo.CurrentCulture"/> to <c>de-DE</c> and asserts the
/// written string uses <c>.</c> rather than <c>,</c>. Closes the Section 11
/// F-CS-Relocation watch item in v1.0.226.
/// </para>
/// <para>
/// <b>Audit Question 4 \u2014 the parent's chart reads, Outcome A.</b>
/// Calculate Relocation reads <see cref="SelectedNatalChart"/> or, when none
/// is picked, <see cref="MainViewModel.LastResult"/> (the parent-owned chart
/// of the Chart panel); it reads no sibling VM state (<c>ChartEntry.*</c>,
/// <c>Returns.*</c>, etc.), so it stays on the parent per Rule #4. The
/// panel's command that copied that chart's birth place into this VM left
/// the panel in 1.4.0 (branch Relocate): relocating a chart to its own
/// birthplace gives the birth chart back.
/// </para>
/// <para>
/// <b>Nested <see cref="LocationSearch"/> (H-1.6 / H-1.9 third
/// application).</b> The Relocation location search was a parent-owned
/// sibling (<c>MainViewModel.RelocationLocationSearch</c>) under H-1.3.
/// H-1.11 migrates it to a nested child under this VM \u2014 the third and
/// final application of the H-1.6 Electional / H-1.9 Mundane nested-child
/// play. After H-1.11 the location-search topology is: one parent-owned
/// sibling (<see cref="MainViewModel.ChartEntryLocationSearch"/>) and
/// three nested children
/// (<c>Electional.LocationSearch</c>, <c>Mundane.LocationSearch</c>,
/// <c>Relocation.LocationSearch</c>). The child is constructed with the
/// shared <see cref="HttpClient"/> and <see cref="NominatimRateLimiter"/>
/// threaded through from the parent so the Nominatim 1-request-per-second
/// policy remains honoured globally across all four instances. The
/// Relocation panel had no historical pre-populated search default, so the
/// nested constructor is called without an <c>initialSearchText</c>
/// argument (the default empty string carries forward).
/// </para>
/// <para>
/// <b>Follow-on handler moved from parent (Rule #4 refinement \u2014 H-1.6
/// third application).</b> <see cref="OnLocationSearchPropertyChanged"/>
/// was previously <c>MainViewModel.OnRelocationLocationSearchPropertyChanged</c>.
/// Cross-VM rule #4 keeps commands on the parent when they write to
/// sibling-owned state, but this handler's writes
/// (<see cref="LatitudeText"/>, <see cref="LongitudeText"/>,
/// <see cref="LocationName"/>) are now all intra-VM \u2014 they target
/// <c>this.*</c> fields under the same umbrella. The handler therefore
/// moves with its owner into this child, matching the H-1.6 Electional and
/// H-1.9 Mundane precedents. Unlike those two handlers, Relocation does
/// <b>not</b> resolve an IANA timezone because the relocated chart reuses
/// the natal UTC moment at the target coordinates; the pre-H-1.3 behaviour
/// is preserved verbatim.
/// </para>
/// <para>
/// <b>Commands stay on parent (rule #4).</b>
/// <c>CalculateRelocationCommand</c>, <c>ExportRelocationMarkdownCommand</c>
/// and <c>LookupRelocationLocationCommand</c> all remain on
/// <see cref="MainViewModel"/>. Each reads parent-owned services
/// (<c>IRelocationService</c>, <c>IChartEngine</c>), threads the
/// parent-owned <c>OperationCancellationRegistry</c> token, and writes
/// status text on the parent. The command methods read inputs from this
/// child and write results back through <see cref="CurrentResult"/>.
/// </para>
/// </remarks>
public sealed class RelocationViewModel : ChildViewModelBase
{
    #region Fields

    private readonly ILogger _logger;

    private ChartEntity? _selectedNatalChart;
    private string _locationName = string.Empty;
    private string _latitudeText = string.Empty;
    private string _longitudeText = string.Empty;
    private string? _latitudeError;
    private string? _longitudeError;
    private RelocationResult? _currentResult;

    #endregion

    #region Constructor

    /// <summary>
    /// Initialises the Relocation view model, composing its nested
    /// <see cref="LocationSearch"/> child with the shared
    /// <see cref="HttpClient"/> and <see cref="NominatimRateLimiter"/> so the
    /// global Nominatim 1-request-per-second policy is honoured.
    /// </summary>
    /// <param name="parent">The composing <see cref="MainViewModel"/>.</param>
    /// <param name="httpClient">Shared HTTP client for Nominatim geocoding.</param>
    /// <param name="rateLimiter">Shared Nominatim rate limiter.</param>
    /// <param name="logger">Serilog logger for diagnostics.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if any argument is <see langword="null"/>.
    /// </exception>
    public RelocationViewModel(
        MainViewModel parent,
        HttpClient httpClient,
        NominatimRateLimiter rateLimiter,
        ILogger logger)
        : base(parent)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(rateLimiter);
        ArgumentNullException.ThrowIfNull(logger);

        _logger = logger.ForContext<RelocationViewModel>();

        // Compose the nested LocationSearch. Construct BEFORE subscribing its
        // PropertyChanged so the handler wiring stays local to the constructor.
        // The Relocation panel has no historical pre-populated default, so we
        // omit the initialSearchText argument (defaults to empty string).
        LocationSearch = new LocationSearchViewModel(
            parent, httpClient, rateLimiter, logger, "relocation location");
        LocationSearch.PropertyChanged += OnLocationSearchPropertyChanged;
    }

    #endregion

    #region Nested Children

    /// <summary>
    /// Nested location-search child for the Relocation panel. Migrated from
    /// parent-owned sibling <c>MainViewModel.RelocationLocationSearch</c>
    /// under Session H-1.11 (third application of the H-1.6 nested-child
    /// play, after H-1.9 Mundane). Bindings previously on
    /// <c>RelocationLocationSearch.X</c> are now on
    /// <c>Relocation.LocationSearch.X</c>. Follow-on logic when the user
    /// picks a result lives in <see cref="OnLocationSearchPropertyChanged"/>.
    /// </summary>
    public LocationSearchViewModel LocationSearch { get; }

    #endregion

    #region Properties \u2014 Inputs

    /// <summary>
    /// Gets or sets the birth chart being relocated, the Birth Chart section's
    /// picker (a ComboBox bound to <see cref="MainViewModel.AllSavedCharts"/>,
    /// whose refresh keeps this selection by Id). When set, the relocation
    /// engine uses this chart instead of <see cref="MainViewModel.LastResult"/>.
    /// Each Load and each Calculate &amp; Save on the Chart panel sets it to the
    /// chart loaded or saved, so the picker follows the Chart panel; a pick by
    /// hand holds until the next of either (1.4.0, branch Relocate). Gates the
    /// Calculate command's <c>CanExecute</c> state together with the
    /// parent-owned <c>HasResult</c> flag.
    /// </summary>
    /// <remarks>
    /// A pick of another chart, or a pick cleared, clears <see cref="CurrentResult"/>
    /// (<see cref="InvalidateResult"/>; 1.6.1, branch StaleResult). The chart's
    /// identity is its Id: the saved-charts refresh re-points the picker at a new
    /// instance of the same chart on every save, import and delete, since
    /// <see cref="ChartEntity"/> compares by reference, and that keeps the result.
    /// </remarks>
    public ChartEntity? SelectedNatalChart
    {
        get => _selectedNatalChart;
        set
        {
            bool chartChanged = _selectedNatalChart?.Id != value?.Id;
            if (SetProperty(ref _selectedNatalChart, value))
            {
                if (chartChanged)
                    InvalidateResult();
                OnPropertyChanged(nameof(ChartInUseText));
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets the Birth Chart section's line naming the chart Calculate Relocation will use: the picked chart,
    /// "{Name} — {type}, {reading}, {place}"; else the Chart panel's chart (<see cref="MainViewModel.LastResult"/>)
    /// the same way with " (the Chart panel's chart, not saved)"; else that there is none (1.4.0, item 3,
    /// branch Relocate).
    /// </summary>
    /// <remarks>
    /// The type and the reading come from <see cref="DisplayText"/>, as the saved-chart row's do: the type by
    /// name, the reading as typed, never through <c>CalendarDateFormatter</c>. Each Load and each Calculate
    /// &amp; Save picks the Chart panel's chart in the picker, so the Chart panel's chart is unsaved here only
    /// when nothing is picked: the Electional search's chart cast from the Chart panel's fields, or a picked
    /// chart since deleted. Raised by <see cref="SelectedNatalChart"/>'s setter and, through
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

            return "No chart yet: load or calculate one on the Chart panel, or pick a saved chart.";
        }
    }

    /// <summary>
    /// Clears <see cref="CurrentResult"/> and raises <see cref="ChartInUseText"/> when the Chart panel's chart
    /// changes; called by the parent's <c>LastResult</c> setter, which this view model cannot observe.
    /// </summary>
    /// <remarks>
    /// Every change of the Chart panel's chart makes the result stale (1.6.1, branch StaleResult): with no chart
    /// picked it is the chart relocated; Load and Calculate &amp; Save re-point the picker as well; the Points
    /// recast changes what a picked chart's cast carries; Clear Form nulls it. The refresh request lets Export
    /// Relocation MD re-read <see cref="HasResult"/>.
    /// </remarks>
    internal void OnChartPanelChartChanged()
    {
        InvalidateResult();
        OnPropertyChanged(nameof(ChartInUseText));
        RequestCommandStateRefresh();
    }

    /// <summary>
    /// Gets or sets the target location's display name. Populated by the
    /// nested <see cref="LocationSearch"/> follow-on handler or by direct user
    /// entry. Gates the Calculate command's <c>CanExecute</c> state, and a
    /// change clears <see cref="CurrentResult"/> (1.6.1, branch StaleResult).
    /// </summary>
    public string LocationName
    {
        get => _locationName;
        set
        {
            if (SetProperty(ref _locationName, value))
            {
                InvalidateResult();
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets or sets the target location's latitude as free-form text. Parsed
    /// on every change by <see cref="ChartEntryViewModel.TryParseLatitude"/>
    /// (decimal degrees with a point, within ±90; 1.4.0, port plan §9 item 9),
    /// which sets <see cref="LatitudeError"/> and requests a command refresh,
    /// since Calculate Relocation reads <see cref="HasValidCoordinates"/>.
    /// Written by the nested <see cref="LocationSearch"/> follow-on handler
    /// under <see cref="CultureInfo.InvariantCulture"/> (F-CS-Relocation
    /// closure). A change clears <see cref="CurrentResult"/> (1.6.1, branch
    /// StaleResult).
    /// </summary>
    public string LatitudeText
    {
        get => _latitudeText;
        set
        {
            if (SetProperty(ref _latitudeText, value))
            {
                LatitudeError = string.IsNullOrWhiteSpace(value)
                    || ChartEntryViewModel.TryParseLatitude(value, out _, out string? error)
                        ? null
                        : error;
                OnPropertyChanged(nameof(HasValidCoordinates));
                InvalidateResult();
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets or sets the target location's longitude as free-form text. Parsed
    /// on every change by <see cref="ChartEntryViewModel.TryParseLongitude"/>
    /// (within ±180), as <see cref="LatitudeText"/> is, and a change clears
    /// <see cref="CurrentResult"/> as there.
    /// </summary>
    public string LongitudeText
    {
        get => _longitudeText;
        set
        {
            if (SetProperty(ref _longitudeText, value))
            {
                LongitudeError = string.IsNullOrWhiteSpace(value)
                    || ChartEntryViewModel.TryParseLongitude(value, out _, out string? error)
                        ? null
                        : error;
                OnPropertyChanged(nameof(HasValidCoordinates));
                InvalidateResult();
                RequestCommandStateRefresh();
            }
        }
    }

    #endregion

    #region Properties \u2014 Coordinate Validation

    /// <summary>The latitude box's tooltip while its text is blank or valid.</summary>
    public const string LatitudeHint = "Relocation latitude (north positive, e.g., 51.5074)";

    /// <summary>The longitude box's tooltip while its text is blank or valid.</summary>
    public const string LongitudeHint = "Relocation longitude (east positive, e.g., -0.1278)";

    /// <summary>
    /// Gets the message for the latitude box's text, or <see langword="null"/>
    /// when the text is valid or blank. A blank box is not marked, as a blank
    /// time-zone box is not, but leaves <see cref="HasValidCoordinates"/> false.
    /// </summary>
    public string? LatitudeError
    {
        get => _latitudeError;
        private set
        {
            if (SetProperty(ref _latitudeError, value))
            {
                OnPropertyChanged(nameof(IsLatitudeValid));
                OnPropertyChanged(nameof(LatitudeToolTip));
            }
        }
    }

    /// <summary>
    /// Gets the message for the longitude box's text, or <see langword="null"/>
    /// when the text is valid or blank.
    /// </summary>
    public string? LongitudeError
    {
        get => _longitudeError;
        private set
        {
            if (SetProperty(ref _longitudeError, value))
            {
                OnPropertyChanged(nameof(IsLongitudeValid));
                OnPropertyChanged(nameof(LongitudeToolTip));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether the latitude box is unmarked: its text
    /// is valid or blank. The heads draw the red border while it is false, as
    /// they do for <see cref="ChartEntryViewModel.IsTimezoneValid"/>.
    /// </summary>
    public bool IsLatitudeValid => _latitudeError is null;

    /// <summary>Gets a value indicating whether the longitude box is unmarked.</summary>
    public bool IsLongitudeValid => _longitudeError is null;

    /// <summary>
    /// Gets the latitude box's tooltip: the message while the text is invalid,
    /// otherwise <see cref="LatitudeHint"/>.
    /// </summary>
    public string LatitudeToolTip => _latitudeError ?? LatitudeHint;

    /// <summary>
    /// Gets the longitude box's tooltip: the message while the text is invalid,
    /// otherwise <see cref="LongitudeHint"/>.
    /// </summary>
    public string LongitudeToolTip => _longitudeError ?? LongitudeHint;

    /// <summary>
    /// Gets a value indicating whether both coordinate boxes hold a valid
    /// value. Calculate Relocation's <c>CanExecute</c> requires it, so the
    /// button is disabled while either box is invalid or blank.
    /// </summary>
    public bool HasValidCoordinates =>
        !string.IsNullOrWhiteSpace(_latitudeText) && _latitudeError is null
        && !string.IsNullOrWhiteSpace(_longitudeText) && _longitudeError is null;

    #endregion

    #region Properties \u2014 Result

    /// <summary>
    /// Gets or sets the most recent <see cref="RelocationResult"/> produced
    /// by <c>MainViewModel.CalculateRelocationAsync</c>. Derived
    /// <see cref="HasResult"/> pattern: the setter raises
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for both itself
    /// and <see cref="HasResult"/>, and requests a single command refresh.
    /// <c>IRelocationService.Calculate</c> always returns a non-null
    /// <see cref="RelocationResult"/> on success (throws on failure), so
    /// <c>_result is not null</c> is a complete success signal.
    /// </summary>
    /// <remarks>
    /// The result stands only while the inputs it was calculated from stand
    /// (1.6.1, branch StaleResult): a change of the picked chart's Id, of the
    /// place or of the Chart panel's chart clears it through
    /// <see cref="InvalidateResult"/>. A failed or cancelled Calculate leaves
    /// it, so what it leaves is a result for the inputs as they stand. A change
    /// made while a Calculate runs clears the old result, and the run then lands
    /// its result for the inputs it read at its start, which the result line
    /// names. Until 1.6.1 the result stayed through every change and Export
    /// Relocation MD wrote it.
    /// </remarks>
    public RelocationResult? CurrentResult
    {
        get => _currentResult;
        set
        {
            if (SetProperty(ref _currentResult, value))
            {
                OnPropertyChanged(nameof(HasResult));
                OnPropertyChanged(nameof(ResultSummary));
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a relocation result is available for
    /// export. Derived from <see cref="CurrentResult"/> per the H-1.7
    /// <c>HasSolarReturnResult</c> precedent.
    /// </summary>
    public bool HasResult => _currentResult is not null;

    /// <summary>
    /// Gets the result line under the panel's buttons: "{chart} at {place}: Ascendant {X} → {Y}, {n} house
    /// changes", or "Ascendant unchanged ({X})", "1 house change" in the singular; empty while there is no
    /// result (1.4.0, item 3, branch Relocate).
    /// </summary>
    /// <remarks>
    /// Naming the chart and the place says which result Export Relocation MD will write. Since 1.6.1 (branch
    /// StaleResult) the result clears, and with it this line and Export, when the picked chart's Id, the place
    /// or the Chart panel's chart changes, until the next Calculate; until then it stayed through every change.
    /// Until 1.4.0 the Ascendant change and the count showed only in the status bar. Raised with
    /// <see cref="CurrentResult"/>.
    /// </remarks>
    public string ResultSummary
    {
        get
        {
            if (_currentResult is not { } result)
            {
                return string.Empty;
            }

            string ascendant = result.AscendantSignChanged
                ? $"Ascendant {result.NatalAscendantSign} → {result.RelocatedAscendantSign}"
                : $"Ascendant unchanged ({result.NatalAscendantSign})";
            int count = result.HouseChanges.Count;
            string houses = count == 1 ? "1 house change" : $"{count} house changes";

            return $"{result.NatalChart.Input.Name} at {result.RelocationTarget.LocationName}: {ascendant}, {houses}";
        }
    }

    /// <summary>
    /// Clears <see cref="CurrentResult"/> when an input it was calculated from changes: the picked chart's Id,
    /// the place (<see cref="LocationName"/>, <see cref="LatitudeText"/>, <see cref="LongitudeText"/>) or the
    /// Chart panel's chart (1.6.1, branch StaleResult). Nothing happens without a result.
    /// </summary>
    /// <remarks>
    /// By the field-bypass pattern <see cref="Clear"/> uses: the field is nulled and <see cref="CurrentResult"/>,
    /// <see cref="HasResult"/> and <see cref="ResultSummary"/> are raised, with no refresh request of its own,
    /// since every caller requests exactly one.
    /// </remarks>
    private void InvalidateResult()
    {
        if (_currentResult is null)
            return;

        _currentResult = null;
        OnPropertyChanged(nameof(CurrentResult));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(ResultSummary));
    }

    #endregion

    #region Follow-on Handlers (migrated from parent per Rule #4 refinement)

    /// <summary>
    /// When the user picks a
    /// <see cref="LocationSearchViewModel.SelectedResult"/> from the nested
    /// relocation location search, copy the coordinates and display name
    /// into this VM's own form fields. Previously
    /// <c>MainViewModel.OnRelocationLocationSearchPropertyChanged</c>; moved
    /// under H-1.11 because every write targets <c>this.*</c> fields now
    /// that the relocation state is unified under this VM \u2014 the same
    /// Rule #4 refinement H-1.6 Electional and H-1.9 Mundane applied.
    /// Unlike those two handlers, no IANA timezone resolution runs \u2014
    /// the relocated chart reuses the natal UTC moment at the target
    /// coordinates, so the target timezone is irrelevant. Coordinate
    /// formatting uses <see cref="CultureInfo.InvariantCulture"/> so the
    /// decimal separator stays <c>.</c> regardless of
    /// <see cref="CultureInfo.CurrentCulture"/> \u2014 F-CS-Relocation
    /// closure.
    /// </summary>
    private void OnLocationSearchPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(LocationSearchViewModel.SelectedResult)) return;
        LocationSearchResult? value = LocationSearch.SelectedResult;
        if (value is null) return;

        LocationName = value.DisplayName;
        LatitudeText = value.Latitude.ToString("F4", CultureInfo.InvariantCulture);
        LongitudeText = value.Longitude.ToString("F4", CultureInfo.InvariantCulture);
    }

    #endregion

    #region Reset

    /// <summary>
    /// Resets every Relocation panel field to its default value, including
    /// the nested <see cref="LocationSearch"/>. Uses the field-bypass
    /// pattern from H-1.2's <c>ChartEntryViewModel.PopulateFrom</c> so a
    /// single <see cref="ChildViewModelBase.RequestCommandStateRefresh"/>
    /// fires at the end of the reset rather than cascading once per setter.
    /// </summary>
    /// <remarks>
    /// Not currently invoked from <c>MainViewModel.ClearForm</c> because the
    /// pre-extraction flow did not reset these relocation-panel fields on
    /// natal-chart clear; provided for future use and for symmetry with the
    /// other H-1 children that each expose a <c>Clear()</c> method.
    /// </remarks>
    public void Clear()
    {
        _selectedNatalChart = null;
        OnPropertyChanged(nameof(SelectedNatalChart));
        OnPropertyChanged(nameof(ChartInUseText));

        _locationName = string.Empty;
        OnPropertyChanged(nameof(LocationName));

        _latitudeText = string.Empty;
        OnPropertyChanged(nameof(LatitudeText));

        _longitudeText = string.Empty;
        OnPropertyChanged(nameof(LongitudeText));

        _latitudeError = null;
        OnPropertyChanged(nameof(LatitudeError));
        OnPropertyChanged(nameof(IsLatitudeValid));
        OnPropertyChanged(nameof(LatitudeToolTip));

        _longitudeError = null;
        OnPropertyChanged(nameof(LongitudeError));
        OnPropertyChanged(nameof(IsLongitudeValid));
        OnPropertyChanged(nameof(LongitudeToolTip));
        OnPropertyChanged(nameof(HasValidCoordinates));

        _currentResult = null;
        OnPropertyChanged(nameof(CurrentResult));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(ResultSummary));

        LocationSearch.Clear();

        RequestCommandStateRefresh();
    }

    #endregion
}
