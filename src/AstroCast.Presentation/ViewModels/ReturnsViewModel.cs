using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.ViewModels;

/// <summary>
/// Child view model that owns the Returns panel state: Solar Return inputs and
/// result, Lunar Return date range plus result list and selection, Planetary
/// Return planet/mode/year/number selection plus result, and the absorbed
/// profection display surface (profection / firdaria / zodiacal releasing
/// summaries plus the <see cref="HasProfection"/> visibility flag). Extracted
/// from <see cref="MainViewModel"/> by Session H-1.7 as a single multi-result
/// child following the H-1.4 Synastry &amp; Composite precedent — three
/// independently-calculated returns share the panel surface and the profection
/// summaries are co-located inside the panel so the H-1.5 §1A audit slated them
/// for absorption here as Category C display-only state.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> This VM owns the data surface for the Returns panel only:
/// <see cref="SolarReturnYear"/>, <see cref="SolarReturnInterpretationContext"/>,
/// <see cref="CurrentSolarReturnResult"/>; <see cref="LunarReturnStartDate"/>,
/// <see cref="LunarReturnEndDate"/>, <see cref="LunarReturnResults"/>,
/// <see cref="SelectedLunarReturn"/>, <see cref="LunarReturnInterpretationContext"/>;
/// <see cref="SelectedReturnPlanet"/>, <see cref="SelectedReturnFindMode"/>,
/// <see cref="ReturnNumber"/>, <see cref="ReturnYear"/>,
/// <see cref="CurrentPlanetaryReturnResult"/>,
/// <see cref="PlanetaryReturnInterpContext"/>; and the four absorbed profection
/// fields <see cref="ProfectionSummary"/>, <see cref="HasProfection"/>,
/// <see cref="FirdariaSummary"/>, <see cref="ZodiacalReleasingSummary"/>. XAML
/// bindings for every one of these fields live under <c>Returns.X</c> in
/// <c>MainWindow.xaml</c>.
/// </para>
/// <para>
/// <b>Multi-result pattern (H-1.4 precedent).</b> Three independent result
/// surfaces — solar (<see cref="CurrentSolarReturnResult"/>), lunar
/// (<see cref="LunarReturnResults"/> + <see cref="SelectedLunarReturn"/>), and
/// planetary (<see cref="CurrentPlanetaryReturnResult"/>) — each expose their
/// own <c>HasXxxResult</c> derived flag. There is deliberately no aggregate
/// <c>HasResult</c> property: the three calculations are independent and gate
/// independent export commands. The setter on each result property raises
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> for both itself and
/// the derived flag, then signals
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
/// parent's per-domain refresher fires.
/// </para>
/// <para>
/// <b>Commands stay on parent (rule #4).</b>
/// <c>CalculateSolarReturnCommand</c>, <c>ExportSolarReturnMarkdownCommand</c>,
/// <c>CalculateLunarReturnCommand</c>, <c>ExportLunarReturnMarkdownCommand</c>,
/// <c>CalculatePlanetaryReturnCommand</c>, and
/// <c>ExportPlanetaryReturnMarkdownCommand</c> remain on
/// <see cref="MainViewModel"/> because each one reads the parent-owned
/// <c>LastResult</c> (the natal chart), calls a parent-owned service
/// (<c>ISolarReturnService</c>, <c>ILunarReturnService</c>,
/// <c>IPlanetaryReturnService</c>), threads the parent-owned
/// <c>OperationCancellationRegistry</c> token, and writes status text on the
/// parent. The command methods read inputs from this child and write the
/// computed result back through the public setters.
/// </para>
/// <para>
/// <b>Profection absorption — parent-to-child writes (Category C).</b>
/// <see cref="ProfectionSummary"/>, <see cref="HasProfection"/>,
/// <see cref="FirdariaSummary"/>, and <see cref="ZodiacalReleasingSummary"/>
/// have <c>internal set</c> accessors. They are populated exclusively by the
/// parent's chart-lifecycle methods (<c>CalculateAndSaveAsync</c> and
/// <c>LoadChartAsync</c>) — the parent owns the chart lifecycle, not because
/// it owns these fields but because the lifecycle decides when to compute
/// them. <c>InternalsVisibleTo("AstroCast.Tests")</c> on the AstroCast WPF
/// assembly grants the test project access so unit tests can drive the
/// internal setters directly without spinning up the full lifecycle. None of
/// the four setters raise
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>: no command's
/// <c>CanExecute</c> depends on profection state, mirroring the
/// pre-extraction behaviour of the <c>MainViewModel</c> private setters.
/// </para>
/// <para>
/// <b>SelectedLunarReturn cascade.</b> The lunar export command's
/// <c>CanExecute</c> ultimately reads <see cref="SelectedLunarReturn"/> (the
/// export body bails when it is <see langword="null"/>), so the
/// <see cref="SelectedLunarReturn"/> setter signals
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> just like the
/// pre-extraction <c>MainViewModel.SelectedLunarReturn</c> setter did. The
/// <see cref="LunarReturnResults"/> setter signals as well — calculating fires
/// two refresh pulses (results then selection), matching the original
/// behaviour.
/// </para>
/// <para>
/// <b>SelectedReturnPlanet cascade.</b> Setting
/// <see cref="SelectedReturnPlanet"/> auto-flips
/// <see cref="SelectedReturnFindMode"/>: Saturn or Jupiter switches to
/// <see cref="ReturnFindMode.ByReturnNumber"/>; Mars / Venus / Mercury switch
/// to <see cref="ReturnFindMode.ByYear"/>. This mirrors the pre-extraction
/// <c>MainViewModel</c> setter verbatim — fast outer planets benefit from
/// numbering across a long lifetime; inner planets are easier to find by
/// target year because their returns recur within most calendar years.
/// </para>
/// <para>
/// <b>F-GR-2 watch item.</b> The field initialisers for
/// <see cref="SolarReturnYear"/>, <see cref="LunarReturnStartDate"/>,
/// <see cref="LunarReturnEndDate"/>, and <see cref="ReturnYear"/> call
/// <see cref="DateTime.Today"/> directly, as did the original fields on
/// <see cref="MainViewModel"/>. The Polish Phase F-GR-2 watch item tracks
/// migrating these to an injected <c>IClock</c> to make date-dependent
/// formatter tests deterministic without harness normalisation; Session H-1.7
/// preserves the existing coupling to keep the extraction a pure refactor
/// and the watch item's VM list now includes <see cref="ReturnsViewModel"/>.
/// </para>
/// </remarks>
public sealed class ReturnsViewModel : ChildViewModelBase
{
    #region Fields — Solar Return

    private int _solarReturnYear = DateTime.Today.Year;
    private SolarReturnResult? _currentSolarReturnResult;
    private string _solarReturnInterpretationContext = string.Empty;

    #endregion

    #region Fields — Lunar Return

    private DateTime _lunarReturnStartDate = DateTime.Today;
    private DateTime _lunarReturnEndDate = DateTime.Today.AddMonths(3);
    private IReadOnlyList<LunarReturnResult>? _lunarReturnResults;
    private LunarReturnResult? _selectedLunarReturn;
    private string _lunarReturnInterpretationContext = string.Empty;

    #endregion

    #region Fields — Planetary Return

    private CelestialBodyId _selectedReturnPlanet = CelestialBodyId.Saturn;
    private ReturnFindMode _returnFindMode = ReturnFindMode.ByReturnNumber;
    private int _returnNumber = 1;
    private int _returnYear = DateTime.Today.Year;
    private PlanetaryReturnResult? _currentPlanetaryReturnResult;
    private string _planetaryReturnInterpContext = string.Empty;

    #endregion

    #region Fields — Profection (Category C — parent-owned writes)

    private string _profectionSummary = string.Empty;
    private bool _hasProfection;
    private string _firdariaSummary = string.Empty;
    private string _zodiacalReleasingSummary = string.Empty;

    #endregion

    #region Constructor

    /// <summary>
    /// Initialises the returns view model.
    /// </summary>
    /// <param name="parent">The composing <see cref="MainViewModel"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="parent"/> is <see langword="null"/>.
    /// </exception>
    public ReturnsViewModel(MainViewModel parent) : base(parent)
    {
    }

    #endregion

    #region Solar Return

    /// <summary>
    /// Gets or sets the solar return target year. Default is the current year
    /// per <see cref="DateTime.Today"/>. Consumed by
    /// <c>MainViewModel.CalculateSolarReturnAsync</c> when the user clicks
    /// Calculate.
    /// </summary>
    public int SolarReturnYear
    {
        get => _solarReturnYear;
        set => SetProperty(ref _solarReturnYear, value);
    }

    /// <summary>
    /// Gets or sets the most recent <see cref="Core.Models.SolarReturnResult"/>
    /// produced by <c>MainViewModel.CalculateSolarReturnAsync</c>. Setting this
    /// raises <see cref="INotifyPropertyChanged.PropertyChanged"/> for both
    /// <c>CurrentSolarReturnResult</c> and the derived
    /// <see cref="HasSolarReturnResult"/>, then fires
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent re-evaluates the solar return command states.
    /// </summary>
    public SolarReturnResult? CurrentSolarReturnResult
    {
        get => _currentSolarReturnResult;
        set
        {
            if (SetProperty(ref _currentSolarReturnResult, value))
            {
                OnPropertyChanged(nameof(HasSolarReturnResult));
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a solar return result is available for
    /// export. Derived from <see cref="CurrentSolarReturnResult"/> — no
    /// separate backing field.
    /// </summary>
    public bool HasSolarReturnResult => _currentSolarReturnResult is not null;

    /// <summary>
    /// Gets or sets the free-text interpretation context for solar return AI
    /// interpretation. Rendered in the solar return Markdown header when
    /// non-empty.
    /// </summary>
    public string SolarReturnInterpretationContext
    {
        get => _solarReturnInterpretationContext;
        set => SetProperty(ref _solarReturnInterpretationContext, value);
    }

    #endregion

    #region Lunar Return

    /// <summary>
    /// Gets or sets the lunar return search start date. Default is today.
    /// Consumed by <c>MainViewModel.CalculateLunarReturnAsync</c>.
    /// </summary>
    public DateTime LunarReturnStartDate
    {
        get => _lunarReturnStartDate;
        set => SetProperty(ref _lunarReturnStartDate, value);
    }

    /// <summary>
    /// Gets or sets the lunar return search end date. Default is three months
    /// from today. Consumed by <c>MainViewModel.CalculateLunarReturnAsync</c>.
    /// </summary>
    public DateTime LunarReturnEndDate
    {
        get => _lunarReturnEndDate;
        set => SetProperty(ref _lunarReturnEndDate, value);
    }

    /// <summary>
    /// Gets or sets the list of lunar return results from the last
    /// calculation. Setter raises
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for both
    /// <c>LunarReturnResults</c> and the derived
    /// <see cref="HasLunarReturnResult"/>, then signals
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent re-evaluates the lunar return command states.
    /// </summary>
    public IReadOnlyList<LunarReturnResult>? LunarReturnResults
    {
        get => _lunarReturnResults;
        set
        {
            if (SetProperty(ref _lunarReturnResults, value))
            {
                OnPropertyChanged(nameof(HasLunarReturnResult));
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets or sets the selected lunar return from the results list. Drives
    /// the export command's <c>CanExecute</c> via the parent's refresh routine
    /// (the export body bails when this is <see langword="null"/>), so the
    /// setter signals
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>.
    /// </summary>
    public LunarReturnResult? SelectedLunarReturn
    {
        get => _selectedLunarReturn;
        set
        {
            if (SetProperty(ref _selectedLunarReturn, value))
            {
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether one or more lunar return results are
    /// available for export. Derived from
    /// <see cref="LunarReturnResults"/>.<c>Count &gt; 0</c> — no separate
    /// backing field.
    /// </summary>
    public bool HasLunarReturnResult => _lunarReturnResults is { Count: > 0 };

    /// <summary>
    /// Gets or sets the free-text interpretation context for lunar return AI
    /// interpretation. Rendered in the lunar return Markdown header when
    /// non-empty.
    /// </summary>
    public string LunarReturnInterpretationContext
    {
        get => _lunarReturnInterpretationContext;
        set => SetProperty(ref _lunarReturnInterpretationContext, value);
    }

    #endregion

    #region Planetary Return

    /// <summary>
    /// Gets the static list of planets available for planetary return
    /// calculation. Bound to the planet ComboBox. Saturn / Jupiter / Mars /
    /// Venus / Mercury — outer planets and Sun are excluded (Sun returns are
    /// solar returns; Uranus / Neptune / Pluto have return periods longer
    /// than a human lifetime).
    /// </summary>
    public static IReadOnlyList<CelestialBodyId> AvailableReturnPlanets { get; } =
    [
        CelestialBodyId.Saturn,
        CelestialBodyId.Jupiter,
        CelestialBodyId.Mars,
        CelestialBodyId.Venus,
        CelestialBodyId.Mercury,
    ];

    /// <summary>
    /// Gets or sets the selected planet for planetary return calculation.
    /// Changing the planet auto-flips the default
    /// <see cref="SelectedReturnFindMode"/>: Saturn / Jupiter use
    /// <see cref="ReturnFindMode.ByReturnNumber"/>; Mars / Venus / Mercury use
    /// <see cref="ReturnFindMode.ByYear"/>.
    /// </summary>
    public CelestialBodyId SelectedReturnPlanet
    {
        get => _selectedReturnPlanet;
        set
        {
            if (SetProperty(ref _selectedReturnPlanet, value))
            {
                SelectedReturnFindMode = value is CelestialBodyId.Saturn or CelestialBodyId.Jupiter
                    ? ReturnFindMode.ByReturnNumber
                    : ReturnFindMode.ByYear;
            }
        }
    }

    /// <summary>
    /// Gets the static list of available find modes for the planetary return
    /// ComboBox. Order matches the original <c>MainViewModel</c> declaration:
    /// <see cref="ReturnFindMode.ByYear"/> first,
    /// <see cref="ReturnFindMode.ByReturnNumber"/> second.
    /// </summary>
    public static IReadOnlyList<ReturnFindMode> AvailableReturnFindModes { get; } =
        [ReturnFindMode.ByYear, ReturnFindMode.ByReturnNumber];

    /// <summary>
    /// Gets or sets the current find mode for planetary return calculation.
    /// Setter raises
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for the derived
    /// <see cref="IsReturnByYear"/> and <see cref="IsReturnByNumber"/>
    /// visibility flags so the XAML show/hide bindings update.
    /// </summary>
    public ReturnFindMode SelectedReturnFindMode
    {
        get => _returnFindMode;
        set
        {
            if (SetProperty(ref _returnFindMode, value))
            {
                OnPropertyChanged(nameof(IsReturnByYear));
                OnPropertyChanged(nameof(IsReturnByNumber));
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether <see cref="ReturnFindMode.ByYear"/> is
    /// active. Used by XAML to show the year input and hide the return-number
    /// input.
    /// </summary>
    public bool IsReturnByYear => SelectedReturnFindMode == ReturnFindMode.ByYear;

    /// <summary>
    /// Gets a value indicating whether
    /// <see cref="ReturnFindMode.ByReturnNumber"/> is active. Used by XAML to
    /// show the return-number input and hide the year input.
    /// </summary>
    public bool IsReturnByNumber => SelectedReturnFindMode == ReturnFindMode.ByReturnNumber;

    /// <summary>
    /// Gets or sets the return ordinal (1-based) used when
    /// <see cref="SelectedReturnFindMode"/> is
    /// <see cref="ReturnFindMode.ByReturnNumber"/>.
    /// </summary>
    public int ReturnNumber
    {
        get => _returnNumber;
        set => SetProperty(ref _returnNumber, value);
    }

    /// <summary>
    /// Gets or sets the target year used when
    /// <see cref="SelectedReturnFindMode"/> is
    /// <see cref="ReturnFindMode.ByYear"/>. Default is the current year per
    /// <see cref="DateTime.Today"/>.
    /// </summary>
    public int ReturnYear
    {
        get => _returnYear;
        set => SetProperty(ref _returnYear, value);
    }

    /// <summary>
    /// Gets or sets the most recent
    /// <see cref="Core.Models.PlanetaryReturnResult"/> produced by
    /// <c>MainViewModel.CalculatePlanetaryReturnAsync</c>. Setting this raises
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for both
    /// <c>CurrentPlanetaryReturnResult</c> and the derived
    /// <see cref="HasPlanetaryReturnResult"/>, then fires
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent re-evaluates the planetary return command states (which include
    /// the cycle timeline commands per the parent's refresher).
    /// </summary>
    public PlanetaryReturnResult? CurrentPlanetaryReturnResult
    {
        get => _currentPlanetaryReturnResult;
        set
        {
            if (SetProperty(ref _currentPlanetaryReturnResult, value))
            {
                OnPropertyChanged(nameof(HasPlanetaryReturnResult));
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a planetary return result is available
    /// for export. Derived from <see cref="CurrentPlanetaryReturnResult"/> —
    /// no separate backing field.
    /// </summary>
    public bool HasPlanetaryReturnResult => _currentPlanetaryReturnResult is not null;

    /// <summary>
    /// Gets or sets the free-text interpretation context for planetary return
    /// AI interpretation. Rendered in the planetary return Markdown header
    /// when non-empty.
    /// </summary>
    public string PlanetaryReturnInterpContext
    {
        get => _planetaryReturnInterpContext;
        set => SetProperty(ref _planetaryReturnInterpContext, value);
    }

    #endregion

    #region Profection (absorbed Category C — parent writes)

    /// <summary>
    /// Gets the summary text for the current profection year, displayed in the
    /// Returns panel. Setter is <c>internal</c>: only
    /// <see cref="MainViewModel"/>'s chart-lifecycle methods
    /// (<c>CalculateAndSaveAsync</c>, <c>LoadChartAsync</c>) write this field,
    /// and <c>InternalsVisibleTo("AstroCast.Tests")</c> grants the test
    /// project access for direct unit testing.
    /// </summary>
    public string ProfectionSummary
    {
        get => _profectionSummary;
        internal set => SetProperty(ref _profectionSummary, value);
    }

    /// <summary>
    /// Gets a value indicating whether profection data is available — true
    /// when a Birth chart is loaded and the lifecycle method has populated the
    /// summary fields. Drives XAML <c>System.Windows.Visibility</c>
    /// converters for all three profection-family TextBlocks (profection,
    /// firdaria, zodiacal releasing). Setter is <c>internal</c> for the same
    /// parent-owned-write reason as <see cref="ProfectionSummary"/>.
    /// </summary>
    public bool HasProfection
    {
        get => _hasProfection;
        internal set => SetProperty(ref _hasProfection, value);
    }

    /// <summary>
    /// Gets the summary text for the current firdaria period, displayed in
    /// the Returns panel. Setter is <c>internal</c> for the same
    /// parent-owned-write reason as <see cref="ProfectionSummary"/>.
    /// </summary>
    public string FirdariaSummary
    {
        get => _firdariaSummary;
        internal set => SetProperty(ref _firdariaSummary, value);
    }

    /// <summary>
    /// Gets the summary text for the current zodiacal-releasing period from
    /// Spirit, displayed in the Returns panel. Setter is <c>internal</c> for
    /// the same parent-owned-write reason as <see cref="ProfectionSummary"/>.
    /// </summary>
    public string ZodiacalReleasingSummary
    {
        get => _zodiacalReleasingSummary;
        internal set => SetProperty(ref _zodiacalReleasingSummary, value);
    }

    #endregion

    #region Chart Panel Change

    /// <summary>
    /// Clears the panel's three results when the Chart panel's chart changes: the solar return, the lunar
    /// returns with the one selected, and the planetary return. Called by the parent's <c>LastResult</c>
    /// setter, which this view model cannot observe (branch StaleResults, 1.7.2, finding N2).
    /// </summary>
    /// <remarks>
    /// Each return is cast from the Chart panel's chart, so none outlives it: a Load, Calculate &amp; Save, the
    /// Points recast and Clear Form each clear them, and their three exports with them, as the Relocation
    /// panel's result has cleared since 1.6.1. By the field-bypass pattern <see cref="Clear"/> uses, with one
    /// refresh request, and none when there was nothing to clear. The panel's own inputs and the profection,
    /// firdaria and releasing lines, which the parent writes for each chart, are left. Until 1.7.2 the results
    /// stayed, and each export wrote the earlier chart's.
    /// </remarks>
    internal void OnChartPanelChartChanged()
    {
        bool cleared = false;

        if (_currentSolarReturnResult is not null)
        {
            _currentSolarReturnResult = null;
            OnPropertyChanged(nameof(CurrentSolarReturnResult));
            OnPropertyChanged(nameof(HasSolarReturnResult));
            cleared = true;
        }

        if (_lunarReturnResults is not null)
        {
            _lunarReturnResults = null;
            OnPropertyChanged(nameof(LunarReturnResults));
            OnPropertyChanged(nameof(HasLunarReturnResult));
            cleared = true;
        }

        if (_selectedLunarReturn is not null)
        {
            _selectedLunarReturn = null;
            OnPropertyChanged(nameof(SelectedLunarReturn));
            cleared = true;
        }

        if (_currentPlanetaryReturnResult is not null)
        {
            _currentPlanetaryReturnResult = null;
            OnPropertyChanged(nameof(CurrentPlanetaryReturnResult));
            OnPropertyChanged(nameof(HasPlanetaryReturnResult));
            cleared = true;
        }

        if (cleared)
            RequestCommandStateRefresh();
    }

    #endregion

    #region Reset

    /// <summary>
    /// Resets every Returns panel field to its initial value. Uses the
    /// field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c> so a single
    /// <see cref="ChildViewModelBase.RequestCommandStateRefresh"/> fires at
    /// the end of the reset rather than cascading once per property setter.
    /// Called from the parent's <c>ClearForm</c> flow.
    /// </summary>
    /// <remarks>
    /// Solar / lunar / planetary inputs reset to their field-initialiser
    /// defaults; result references are nulled out and their derived
    /// <c>HasXxxResult</c> flags raise <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// alongside the result property itself. The four absorbed profection
    /// fields reset to empty strings / false. <see cref="SelectedReturnPlanet"/>
    /// is reset to Saturn and <see cref="SelectedReturnFindMode"/> to
    /// <see cref="ReturnFindMode.ByReturnNumber"/> (matching the original
    /// field initialisers); the cascade in the
    /// <see cref="SelectedReturnPlanet"/> setter is bypassed because we touch
    /// the backing fields directly.
    /// </remarks>
    public void Clear()
    {
        // Solar Return
        _solarReturnYear = DateTime.Today.Year;
        OnPropertyChanged(nameof(SolarReturnYear));

        _currentSolarReturnResult = null;
        OnPropertyChanged(nameof(CurrentSolarReturnResult));
        OnPropertyChanged(nameof(HasSolarReturnResult));

        _solarReturnInterpretationContext = string.Empty;
        OnPropertyChanged(nameof(SolarReturnInterpretationContext));

        // Lunar Return
        _lunarReturnStartDate = DateTime.Today;
        OnPropertyChanged(nameof(LunarReturnStartDate));

        _lunarReturnEndDate = DateTime.Today.AddMonths(3);
        OnPropertyChanged(nameof(LunarReturnEndDate));

        _lunarReturnResults = null;
        OnPropertyChanged(nameof(LunarReturnResults));
        OnPropertyChanged(nameof(HasLunarReturnResult));

        _selectedLunarReturn = null;
        OnPropertyChanged(nameof(SelectedLunarReturn));

        _lunarReturnInterpretationContext = string.Empty;
        OnPropertyChanged(nameof(LunarReturnInterpretationContext));

        // Planetary Return
        _selectedReturnPlanet = CelestialBodyId.Saturn;
        OnPropertyChanged(nameof(SelectedReturnPlanet));

        _returnFindMode = ReturnFindMode.ByReturnNumber;
        OnPropertyChanged(nameof(SelectedReturnFindMode));
        OnPropertyChanged(nameof(IsReturnByYear));
        OnPropertyChanged(nameof(IsReturnByNumber));

        _returnNumber = 1;
        OnPropertyChanged(nameof(ReturnNumber));

        _returnYear = DateTime.Today.Year;
        OnPropertyChanged(nameof(ReturnYear));

        _currentPlanetaryReturnResult = null;
        OnPropertyChanged(nameof(CurrentPlanetaryReturnResult));
        OnPropertyChanged(nameof(HasPlanetaryReturnResult));

        _planetaryReturnInterpContext = string.Empty;
        OnPropertyChanged(nameof(PlanetaryReturnInterpContext));

        // Profection (absorbed)
        _profectionSummary = string.Empty;
        OnPropertyChanged(nameof(ProfectionSummary));

        _hasProfection = false;
        OnPropertyChanged(nameof(HasProfection));

        _firdariaSummary = string.Empty;
        OnPropertyChanged(nameof(FirdariaSummary));

        _zodiacalReleasingSummary = string.Empty;
        OnPropertyChanged(nameof(ZodiacalReleasingSummary));

        RequestCommandStateRefresh();
    }

    #endregion
}
