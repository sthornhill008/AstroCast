using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.ViewModels;

/// <summary>
/// Child view model that owns the Cycles panel state across three independent
/// scans: the natal-chart Planetary Cycle Timeline (per-planet phase-point
/// series anchored on the selected planetary return), mundane Great
/// Conjunctions (Jupiter–Saturn scan in a year range), and mundane Synodic
/// Cycles (arbitrary planet-pair conjunction scan in a year range). Extracted
/// from <see cref="MainViewModel"/> by Session H-1.8 as the eighth
/// decomposition child — a multi-result child following the H-1.4 / H-1.7
/// precedent, in which three independently-calculated cycle scans share a
/// single panel surface.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> This VM owns the data surface for the Cycles-family controls
/// only: <see cref="IncludeCycleSnapshots"/>,
/// <see cref="CurrentCycleTimeline"/>, <see cref="HasCycleTimeline"/>;
/// <see cref="GCStartYear"/>, <see cref="GCEndYear"/>,
/// <see cref="CurrentGCSeries"/>, <see cref="HasGCSeries"/>;
/// <see cref="SynodicBodyOptions"/>, <see cref="SynodicBodyA"/>,
/// <see cref="SynodicBodyB"/>, <see cref="SynodicStartYear"/>,
/// <see cref="SynodicEndYear"/>, <see cref="CurrentSynodicCycleSeries"/>, and
/// <see cref="HasSynodicCycleSeries"/>. XAML bindings for every one of these
/// fields live under <c>Cycles.X</c> in <c>MainWindow.xaml</c>.
/// </para>
/// <para>
/// <b>Multi-result pattern (H-1.4 / H-1.7 precedent).</b> Three independent
/// result surfaces — cycle timeline (<see cref="CurrentCycleTimeline"/>), great
/// conjunctions (<see cref="CurrentGCSeries"/>), and synodic cycles
/// (<see cref="CurrentSynodicCycleSeries"/>) — each expose their own
/// <c>HasXxx</c> derived flag. There is deliberately no aggregate
/// <c>HasResult</c> property: the three calculations are independent and gate
/// independent export commands. Each result setter raises
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> for both itself and the
/// derived flag, then signals
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
/// parent's per-domain refresher fires.
/// </para>
/// <para>
/// <b>Commands stay on parent (rule #4).</b> <c>CalculateCycleTimelineCommand</c>,
/// <c>ExportCycleTimelineMarkdownCommand</c>,
/// <c>FindGreatConjunctionsCommand</c>, <c>ExportGCSeriesMarkdownCommand</c>,
/// <c>FindSynodicCyclesCommand</c>, and <c>ExportSynodicCycleMdCommand</c>
/// remain on <see cref="MainViewModel"/> because each one reads the
/// parent-owned <c>LastResult</c> (the natal chart, for the cycle timeline
/// command), calls a parent-owned service
/// (<c>IPlanetaryCycleTimelineService</c>, <c>IGreatConjunctionService</c>,
/// <c>ISynodicCycleService</c>), threads the parent-owned
/// <c>OperationCancellationRegistry</c> token, and writes status text on the
/// parent. The command methods read inputs from this child and write the
/// computed result back through the public setters.
/// </para>
/// <para>
/// <b>Cross-VM read of <c>Returns</c> (Rule #2 last-resort).</b> The Cycle
/// Timeline command on <see cref="MainViewModel"/> reads
/// <c>SelectedReturnPlanet</c>, <c>SelectedReturnFindMode</c>,
/// <c>ReturnYear</c>, and <c>ReturnNumber</c> from the sibling
/// <see cref="ReturnsViewModel"/>. Per the Session H-1.8 §1A audit (Outcome A),
/// these four properties are shared between Planetary Return calculation (in
/// <see cref="ReturnsViewModel"/>) and Cycle Timeline scanning (this VM): both
/// commands consume the same planet / mode / year / number inputs, the XAML
/// bindings sit under the "Planetary Return" subsection heading inside the
/// Returns panel, and the Cycle Timeline feature was deliberately designed to
/// piggy-back on the planetary-return UI so the user doesn't retype planet and
/// number twice. The read travels <c>parent → Returns → field</c> rather than
/// child-to-child: the command method on the parent resolves
/// <c>Returns.SelectedReturnPlanet</c> etc. directly, keeping every cross-VM
/// hop via <see cref="MainViewModel"/> as the mediator. This is Rule #2
/// last-resort shared-state reading justified by the audit; no state is
/// duplicated and the XAML bindings for those four fields remain
/// <c>{Binding Returns.X}</c> unchanged.
/// </para>
/// <para>
/// <b>HasGCSeries setter semantics (historical).</b> The pre-extraction
/// <c>MainViewModel.HasGCSeries</c> setter used a value-based update: the
/// <c>FindGreatConjunctionsAsync</c> command assigns
/// <c>series.Count &gt; 0</c>, so <see cref="HasGCSeries"/> intentionally does
/// <i>not</i> mirror <c>CurrentGCSeries is not null</c>. A scan that returns
/// zero conjunctions sets the result to a non-null empty series but leaves
/// <see cref="HasGCSeries"/> at <see langword="false"/>, gating the export
/// command off. To preserve this behaviour during extraction,
/// <see cref="HasGCSeries"/> is a plain <see cref="bool"/> property (not a
/// derived expression) written explicitly by the parent command; the setter
/// fires <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> for
/// XAML button re-evaluation. The same pattern applies to
/// <see cref="HasSynodicCycleSeries"/>. Only
/// <see cref="HasCycleTimeline"/> is a derived expression
/// (<c>CurrentCycleTimeline is not null</c>) because the cycle-timeline
/// command does not emit an "empty" result — any successful scan produces a
/// non-null timeline and any failure leaves the field null.
/// </para>
/// <para>
/// <b>F-GR-2 watch item.</b> Unlike <see cref="ReturnsViewModel"/>, this VM
/// has no field initialisers that reference <see cref="DateTime.Today"/>: year
/// defaults are hard-coded (<see cref="GCStartYear"/> = 1800,
/// <see cref="GCEndYear"/> = 2100, <see cref="SynodicStartYear"/> = 2000,
/// <see cref="SynodicEndYear"/> = 2100), matching the pre-extraction
/// <see cref="MainViewModel"/> initialisers verbatim. The Polish Phase F-GR-2
/// watch item's VM list therefore does not need extending for this extraction.
/// </para>
/// </remarks>
public sealed class CyclesViewModel : ChildViewModelBase
{
    #region Fields — Cycle Timeline

    private bool _includeCycleSnapshots;
    private PlanetaryCycleTimeline? _currentCycleTimeline;

    #endregion

    #region Fields — Great Conjunctions

    private int _gcStartYear = 1800;
    private int _gcEndYear = 2100;
    private GreatConjunctionSeries? _currentGCSeries;
    private bool _hasGCSeries;

    #endregion

    #region Fields — Synodic Cycles

    private CelestialBodyId _synodicBodyA = CelestialBodyId.Jupiter;
    private CelestialBodyId _synodicBodyB = CelestialBodyId.Saturn;
    private int _synodicStartYear = 2000;
    private int _synodicEndYear = 2100;
    private SynodicCycleSeries? _currentSynodicCycleSeries;
    private bool _hasSynodicCycleSeries;

    #endregion

    #region Constructor

    /// <summary>
    /// Initialises the cycles view model.
    /// </summary>
    /// <param name="parent">The composing <see cref="MainViewModel"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="parent"/> is <see langword="null"/>.
    /// </exception>
    public CyclesViewModel(MainViewModel parent) : base(parent)
    {
    }

    #endregion

    #region Cycle Timeline

    /// <summary>
    /// Gets or sets a value indicating whether the cycle timeline calculation
    /// should include full planetary-position snapshots at each phase point.
    /// Bound to the "Include snapshots" checkbox in the Cycle Timeline row of
    /// the Returns panel.
    /// </summary>
    public bool IncludeCycleSnapshots
    {
        get => _includeCycleSnapshots;
        set => SetProperty(ref _includeCycleSnapshots, value);
    }

    /// <summary>
    /// Gets or sets the most recent
    /// <see cref="Core.Models.PlanetaryCycleTimeline"/> produced by
    /// <c>MainViewModel.CalculateCycleTimelineAsync</c>. Setting this raises
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for both
    /// <c>CurrentCycleTimeline</c> and the derived
    /// <see cref="HasCycleTimeline"/>, then fires
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent re-evaluates the cycle timeline command states.
    /// </summary>
    public PlanetaryCycleTimeline? CurrentCycleTimeline
    {
        get => _currentCycleTimeline;
        set
        {
            if (SetProperty(ref _currentCycleTimeline, value))
            {
                OnPropertyChanged(nameof(HasCycleTimeline));
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a cycle timeline result is available
    /// for export. Derived from <see cref="CurrentCycleTimeline"/> — no
    /// separate backing field. Unlike the pre-extraction
    /// <see cref="MainViewModel"/> property this is a pure derived expression:
    /// the cycle-timeline command never emits a non-null "empty" result.
    /// </summary>
    public bool HasCycleTimeline => _currentCycleTimeline is not null;

    #endregion

    #region Great Conjunctions

    /// <summary>
    /// Gets or sets the start year for the Jupiter–Saturn great conjunction
    /// scan. Default is <c>1800</c>. Consumed by
    /// <c>MainViewModel.FindGreatConjunctionsAsync</c>.
    /// </summary>
    public int GCStartYear
    {
        get => _gcStartYear;
        set => SetProperty(ref _gcStartYear, value);
    }

    /// <summary>
    /// Gets or sets the end year for the Jupiter–Saturn great conjunction
    /// scan. Default is <c>2100</c>. Consumed by
    /// <c>MainViewModel.FindGreatConjunctionsAsync</c>.
    /// </summary>
    public int GCEndYear
    {
        get => _gcEndYear;
        set => SetProperty(ref _gcEndYear, value);
    }

    /// <summary>
    /// Gets or sets the most recent
    /// <see cref="Core.Models.GreatConjunctionSeries"/> produced by
    /// <c>MainViewModel.FindGreatConjunctionsAsync</c>. Changes to this
    /// property do not themselves flip <see cref="HasGCSeries"/> — the parent
    /// command writes both explicitly to preserve the pre-extraction
    /// "empty-series zeroes the flag" semantics.
    /// </summary>
    public GreatConjunctionSeries? CurrentGCSeries
    {
        get => _currentGCSeries;
        set => SetProperty(ref _currentGCSeries, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether great conjunction results are
    /// available for export. Written explicitly by the parent command as
    /// <c>series.Count &gt; 0</c> so a scan that yields zero conjunctions
    /// leaves the export command gated off even though
    /// <see cref="CurrentGCSeries"/> is non-null. Setter fires
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent re-evaluates the great conjunction command states.
    /// </summary>
    public bool HasGCSeries
    {
        get => _hasGCSeries;
        set
        {
            if (SetProperty(ref _hasGCSeries, value))
            {
                RequestCommandStateRefresh();
            }
        }
    }

    #endregion

    #region Synodic Cycles

    /// <summary>
    /// Gets the list of planets available for synodic-cycle ComboBox binding.
    /// Covers the ten traditional bodies (Sun through Pluto); the True Node
    /// and Chiron are omitted because synodic-cycle analysis is defined for
    /// planet pairs only.
    /// </summary>
    public IReadOnlyList<CelestialBodyId> SynodicBodyOptions { get; } =
    [
        CelestialBodyId.Sun,
        CelestialBodyId.Moon,
        CelestialBodyId.Mercury,
        CelestialBodyId.Venus,
        CelestialBodyId.Mars,
        CelestialBodyId.Jupiter,
        CelestialBodyId.Saturn,
        CelestialBodyId.Uranus,
        CelestialBodyId.Neptune,
        CelestialBodyId.Pluto,
    ];

    /// <summary>
    /// Gets or sets the first body of the synodic-cycle pair. Default is
    /// <see cref="CelestialBodyId.Jupiter"/>.
    /// </summary>
    public CelestialBodyId SynodicBodyA
    {
        get => _synodicBodyA;
        set => SetProperty(ref _synodicBodyA, value);
    }

    /// <summary>
    /// Gets or sets the second body of the synodic-cycle pair. Default is
    /// <see cref="CelestialBodyId.Saturn"/>.
    /// </summary>
    public CelestialBodyId SynodicBodyB
    {
        get => _synodicBodyB;
        set => SetProperty(ref _synodicBodyB, value);
    }

    /// <summary>
    /// Gets or sets the start year for the synodic-cycle scan. Default is
    /// <c>2000</c>.
    /// </summary>
    public int SynodicStartYear
    {
        get => _synodicStartYear;
        set => SetProperty(ref _synodicStartYear, value);
    }

    /// <summary>
    /// Gets or sets the end year for the synodic-cycle scan. Default is
    /// <c>2100</c>.
    /// </summary>
    public int SynodicEndYear
    {
        get => _synodicEndYear;
        set => SetProperty(ref _synodicEndYear, value);
    }

    /// <summary>
    /// Gets or sets the most recent
    /// <see cref="Core.Models.SynodicCycleSeries"/> produced by
    /// <c>MainViewModel.FindSynodicCyclesAsync</c>. Changes to this property
    /// do not themselves flip <see cref="HasSynodicCycleSeries"/> — the parent
    /// command writes both explicitly to preserve the pre-extraction
    /// "empty-series zeroes the flag" semantics.
    /// </summary>
    public SynodicCycleSeries? CurrentSynodicCycleSeries
    {
        get => _currentSynodicCycleSeries;
        set => SetProperty(ref _currentSynodicCycleSeries, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether synodic-cycle results are
    /// available for export. Written explicitly by the parent command as
    /// <c>series.Count &gt; 0</c> so a scan that yields zero conjunctions
    /// leaves the export command gated off. Setter fires
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent re-evaluates the synodic cycle command states.
    /// </summary>
    public bool HasSynodicCycleSeries
    {
        get => _hasSynodicCycleSeries;
        set
        {
            if (SetProperty(ref _hasSynodicCycleSeries, value))
            {
                RequestCommandStateRefresh();
            }
        }
    }

    #endregion

    #region Reset

    /// <summary>
    /// Resets every Cycles panel field to its initial value. Uses the
    /// field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c> so a single
    /// <see cref="ChildViewModelBase.RequestCommandStateRefresh"/> fires at the
    /// end of the reset rather than cascading once per property setter. Called
    /// from the parent's <c>ClearForm</c> flow.
    /// </summary>
    /// <remarks>
    /// Cycle-timeline / great-conjunction / synodic-cycle inputs reset to
    /// their field-initialiser defaults (<see cref="IncludeCycleSnapshots"/>
    /// to <see langword="false"/>, year ranges to the hard-coded scan
    /// defaults, synodic bodies to Jupiter / Saturn); result references are
    /// nulled out and their <c>HasXxx</c> flags cleared, with
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> raised for each
    /// touched property.
    /// </remarks>
    public void Clear()
    {
        // Cycle Timeline
        _includeCycleSnapshots = false;
        OnPropertyChanged(nameof(IncludeCycleSnapshots));

        _currentCycleTimeline = null;
        OnPropertyChanged(nameof(CurrentCycleTimeline));
        OnPropertyChanged(nameof(HasCycleTimeline));

        // Great Conjunctions
        _gcStartYear = 1800;
        OnPropertyChanged(nameof(GCStartYear));

        _gcEndYear = 2100;
        OnPropertyChanged(nameof(GCEndYear));

        _currentGCSeries = null;
        OnPropertyChanged(nameof(CurrentGCSeries));

        _hasGCSeries = false;
        OnPropertyChanged(nameof(HasGCSeries));

        // Synodic Cycles
        _synodicBodyA = CelestialBodyId.Jupiter;
        OnPropertyChanged(nameof(SynodicBodyA));

        _synodicBodyB = CelestialBodyId.Saturn;
        OnPropertyChanged(nameof(SynodicBodyB));

        _synodicStartYear = 2000;
        OnPropertyChanged(nameof(SynodicStartYear));

        _synodicEndYear = 2100;
        OnPropertyChanged(nameof(SynodicEndYear));

        _currentSynodicCycleSeries = null;
        OnPropertyChanged(nameof(CurrentSynodicCycleSeries));

        _hasSynodicCycleSeries = false;
        OnPropertyChanged(nameof(HasSynodicCycleSeries));

        RequestCommandStateRefresh();
    }

    #endregion
}
