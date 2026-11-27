using AstroCast.Core.Models;

namespace AstroCast.ViewModels;

/// <summary>
/// Child view model that owns the Return &amp; Ingress Timeline panel state:
/// the scan window (start year / end year) and the two event-type toggles
/// (include planetary returns, include sign ingresses) that parameterise the
/// combined chronological timeline, plus the most recent
/// <see cref="ReturnIngressTimeline"/> output. Extracted from
/// <see cref="MainViewModel"/> by Session H-1.10 as the tenth decomposition
/// child.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> This VM owns the data surface for the Return &amp; Ingress
/// Timeline panel only: <see cref="StartYear"/>, <see cref="EndYear"/>,
/// <see cref="IncludeReturns"/>, <see cref="IncludeIngresses"/>,
/// <see cref="CurrentResult"/>, and <see cref="HasResult"/>. XAML bindings
/// for these fields live under <c>ReturnIngressTimeline.X</c> in
/// <c>MainWindow.xaml</c>. Property names are the pre-extraction names with
/// the <c>RIT</c> / <c>RI</c> prefix stripped inside the child, matching the
/// H-1.2 / H-1.5 / H-1.6 / H-1.9 precedent.
/// </para>
/// <para>
/// <b>Audit Question 2 outcome \u2014 fully independent (Outcome A).</b> The
/// pre-extraction <c>CalculateRITimelineAsync</c> body reads four child-owned
/// inputs and one parent-owned input: <see cref="StartYear"/>,
/// <see cref="EndYear"/>, <see cref="IncludeReturns"/>,
/// <see cref="IncludeIngresses"/>, and <c>MainViewModel.LastResult</c> (the
/// natal chart). It does <b>not</b> read any sibling VM state \u2014 no
/// <c>Returns.*</c>, no <c>Mundane.*</c>. The natal-chart dependency resolves
/// through the parent (the chart engine is parent-owned per Rule #4; the
/// <c>LastResult</c> field is the parent's cached output), not through
/// another child, so this is not a cross-VM sibling read. R/I Timeline is a
/// standalone feature that builds the combined calendar from scratch via
/// <c>IReturnIngressTimelineService.Calculate</c>.
/// </para>
/// <para>
/// <b>Commands stay on parent (rule #4).</b>
/// <c>CalculateRITimelineCommand</c> and <c>ExportRITimelineCommand</c>
/// remain on <see cref="MainViewModel"/> because they call the parent-owned
/// <c>IReturnIngressTimelineService</c>, read the parent-owned
/// <c>LastResult</c>, thread the parent-owned
/// <c>OperationCancellationRegistry</c> token, and write status text on the
/// parent. The command bodies read inputs from this child and write the
/// result back to <see cref="CurrentResult"/> and <see cref="HasResult"/>.
/// </para>
/// <para>
/// <b>Value-based <c>HasX</c> pattern (H-1.8 / H-1.9 precedent).</b>
/// <see cref="IReturnIngressTimelineService.Calculate"/> always returns a
/// non-null <see cref="ReturnIngressTimeline"/> \u2014 a scan that yields
/// zero events still produces an object with <c>Count == 0</c> and an empty
/// <c>Entries</c> list. The pre-extraction code wrote
/// <c>HasRITimeline = timeline.Count &gt; 0;</c> explicitly to gate the
/// export command on non-empty results; this semantic is preserved verbatim.
/// The <see cref="CurrentResult"/> setter intentionally does <b>not</b> flip
/// <see cref="HasResult"/> and does <b>not</b> raise
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> \u2014 the
/// parent command writes both properties in sequence. The
/// <see cref="HasResult"/> setter raises
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
/// parent re-evaluates the R/I Timeline command states.
/// </para>
/// <para>
/// <b>DateTime.Today coupling.</b> The field initialisers for
/// <see cref="StartYear"/> and <see cref="EndYear"/> call
/// <see cref="DateTime.Today"/> directly, as did the original fields on
/// <see cref="MainViewModel"/>. Session Golden Refresh watch-item F-GR-2
/// tracks migrating this (plus matching call sites in
/// <c>ChartMarkdownFormatter</c>, <c>TransitsViewModel</c>,
/// <c>ElectionalViewModel</c>, <c>ReturnsViewModel</c>, and
/// <c>MundaneViewModel</c>) to an injected <c>IClock</c> to make
/// date-dependent formatter tests deterministic without harness
/// normalisation; Session H-1.10 preserves the existing coupling to keep
/// the extraction a pure refactor, bringing the F-GR-2 VM target list to
/// six production targets.
/// </para>
/// </remarks>
public sealed class ReturnIngressTimelineViewModel : ChildViewModelBase
{
    #region Fields

    private int _startYear = DateTime.Today.Year;
    private int _endYear = DateTime.Today.Year + 3;
    private bool _includeReturns = true;
    private bool _includeIngresses = true;
    private ReturnIngressTimeline? _currentResult;
    private bool _hasResult;

    #endregion

    #region Constructor

    /// <summary>
    /// Initialises the Return &amp; Ingress Timeline view model.
    /// </summary>
    /// <param name="parent">The composing <see cref="MainViewModel"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="parent"/> is <see langword="null"/>.
    /// </exception>
    public ReturnIngressTimelineViewModel(MainViewModel parent) : base(parent)
    {
    }

    #endregion

    #region Inputs

    /// <summary>
    /// Gets or sets the start year of the combined return &amp; ingress scan
    /// window. Consumed by <c>MainViewModel.CalculateRITimelineAsync</c>.
    /// </summary>
    public int StartYear
    {
        get => _startYear;
        set => SetProperty(ref _startYear, value);
    }

    /// <summary>
    /// Gets or sets the end year of the combined return &amp; ingress scan
    /// window. Consumed by <c>MainViewModel.CalculateRITimelineAsync</c>.
    /// </summary>
    public int EndYear
    {
        get => _endYear;
        set => SetProperty(ref _endYear, value);
    }

    /// <summary>
    /// Gets or sets whether planetary returns (Mercury\u2013Saturn by
    /// default) are included in the combined timeline. Consumed by
    /// <c>MainViewModel.CalculateRITimelineAsync</c>.
    /// </summary>
    public bool IncludeReturns
    {
        get => _includeReturns;
        set => SetProperty(ref _includeReturns, value);
    }

    /// <summary>
    /// Gets or sets whether outer-planet sign ingresses (Jupiter\u2013Pluto
    /// plus Chiron by default) are included in the combined timeline.
    /// Consumed by <c>MainViewModel.CalculateRITimelineAsync</c>.
    /// </summary>
    public bool IncludeIngresses
    {
        get => _includeIngresses;
        set => SetProperty(ref _includeIngresses, value);
    }

    #endregion

    #region Result

    /// <summary>
    /// Gets or sets the most recent <see cref="ReturnIngressTimeline"/>
    /// produced by <c>MainViewModel.CalculateRITimelineAsync</c>. Changes to
    /// this property do not themselves flip <see cref="HasResult"/> \u2014 the
    /// parent command writes both properties in sequence to preserve the
    /// pre-extraction "empty timeline zeroes the flag" semantic (value-based
    /// <c>HasX</c> pattern per H-1.8 / H-1.9).
    /// </summary>
    public ReturnIngressTimeline? CurrentResult
    {
        get => _currentResult;
        set => SetProperty(ref _currentResult, value);
    }

    /// <summary>
    /// Gets or sets a value indicating whether a Return &amp; Ingress
    /// Timeline result is available for export. Written explicitly by the
    /// parent command as <c>timeline.Count &gt; 0</c> so a scan that yields
    /// zero events leaves the export command gated off even though
    /// <see cref="CurrentResult"/> is non-null. Setter fires
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent re-evaluates the R/I Timeline command states.
    /// </summary>
    public bool HasResult
    {
        get => _hasResult;
        set
        {
            if (SetProperty(ref _hasResult, value))
            {
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Clears <see cref="CurrentResult"/> and <see cref="HasResult"/> when the Chart panel's chart changes; called
    /// by the parent's <c>LastResult</c> setter, which this view model cannot observe, and by its Clear Form
    /// (branch StaleResults, 1.7.2, finding N2).
    /// </summary>
    /// <remarks>
    /// The timeline is made from the Chart panel's chart and names it, so it does not outlive it; the panel's
    /// years and boxes are left as they are. Nothing happens without a result. Until 1.7.2 the timeline stayed
    /// through every change of chart, Clear Form included, and its export wrote the earlier chart's.
    /// </remarks>
    internal void OnChartPanelChartChanged()
    {
        CurrentResult = null;
        HasResult = false;
    }

    #endregion

    #region Reset

    /// <summary>
    /// Resets every Return &amp; Ingress Timeline panel field to its initial
    /// value. Uses the field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c> so a single
    /// <see cref="ChildViewModelBase.RequestCommandStateRefresh"/> fires at
    /// the end of the reset rather than cascading through the
    /// <see cref="HasResult"/> setter. Not called from the parent's
    /// <c>ClearForm</c> flow because the pre-extraction <c>ClearForm</c> did
    /// not reset Return &amp; Ingress Timeline state; provided for future use
    /// and symmetry with the other H-1 children.
    /// </summary>
    public void Clear()
    {
        _startYear = DateTime.Today.Year;
        OnPropertyChanged(nameof(StartYear));

        _endYear = DateTime.Today.Year + 3;
        OnPropertyChanged(nameof(EndYear));

        _includeReturns = true;
        OnPropertyChanged(nameof(IncludeReturns));

        _includeIngresses = true;
        OnPropertyChanged(nameof(IncludeIngresses));

        _currentResult = null;
        OnPropertyChanged(nameof(CurrentResult));

        _hasResult = false;
        OnPropertyChanged(nameof(HasResult));

        RequestCommandStateRefresh();
    }

    #endregion
}
