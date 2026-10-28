using System.Runtime.CompilerServices;

using AstroCast.Core.Models.Transit;

namespace AstroCast.ViewModels;

/// <summary>
/// Child view model that owns the Transits panel state: the report date range,
/// the four technique toggles (transits, solar arcs, secondary progressions,
/// primary directions) and the minor-aspects toggle, the free-text interpretation context, and the
/// most recent <see cref="TransitResult"/>. Extracted from
/// <see cref="MainViewModel"/> by Session H-1.5 following the H-1 decomposition
/// pattern.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> This VM owns the data surface for the Transits panel only:
/// <see cref="StartDate"/>, <see cref="EndDate"/>,
/// <see cref="IncludeTransits"/>, <see cref="IncludeMinorAspects"/>,
/// <see cref="IncludeSolarArcs"/>, <see cref="IncludeSecondaryProgressions"/>,
/// <see cref="IncludePrimaryDirections"/>,
/// <see cref="InterpretationContext"/>, and the <see cref="TransitResult"/>
/// output. XAML bindings for these fields live under <c>Transits.X</c> in
/// <c>MainWindow.xaml</c>. Profection state (summary, HasProfection) stays on
/// <see cref="MainViewModel"/> for now — profections display inside the Returns
/// panel and will move with it in Session H-1.7.
/// </para>
/// <para>
/// <b>Commands stay on parent (rule #4).</b>
/// <c>CalculateTransitCommand</c>, <c>ExportTransitMarkdownCommand</c>,
/// <c>ExportDailyHoroscopeMarkdownCommand</c>, and
/// <c>ExportTransitTimelineMarkdownCommand</c> remain on
/// <see cref="MainViewModel"/> because they read the parent-owned
/// <c>LastResult</c> (the natal chart), call the parent-owned
/// <c>ITransitService</c>, and write status text on the parent. The command
/// methods read inputs (<see cref="StartDate"/>, <see cref="EndDate"/>, the
/// four techniques, <see cref="IncludeMinorAspects"/>,
/// <see cref="InterpretationContext"/>) from this child and write the result
/// back to <see cref="TransitResult"/>.
/// </para>
/// <para>
/// <b>Command-state refresh.</b> The <see cref="TransitResult"/> setter raises
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the parent
/// re-evaluates <c>CanExecute</c> on the three transit-export commands: the
/// report export needs <see cref="HasResult"/>, the timeline and daily
/// exports <see cref="ResultIncludesTransits"/>. The four technique setters
/// raise it too, since Calculate needs at least one technique checked
/// (<see cref="HasTechnique"/>; branch ModesPanel, 1.4.0 item 2; the fourth,
/// <see cref="IncludePrimaryDirections"/>, since branch PrimaryPanel, 1.5.0),
/// and while the fourth is checked the Directions panel's settings. The date,
/// minor-aspect and context setters do not, because no command's
/// <c>CanExecute</c> depends on them.
/// </para>
/// <para>
/// <b>DateTime.Today coupling.</b> The field initialisers for
/// <see cref="StartDate"/> and <see cref="EndDate"/> call
/// <see cref="DateTime.Today"/> directly, as did the original fields on
/// <see cref="MainViewModel"/>. Session Golden Refresh watch-item F-GR-2 tracks
/// migrating this to an injected <c>IClock</c> to make date-dependent formatter
/// tests deterministic without harness normalisation; Session H-1.5 preserves
/// the existing coupling to keep the extraction a pure refactor.
/// </para>
/// </remarks>
public sealed class TransitsViewModel : ChildViewModelBase
{
    #region Fields

    private DateTime _startDate = DateTime.Today;
    private DateTime _endDate = DateTime.Today.AddMonths(1);
    private bool _includeTransits = true;
    private bool _includeMinorAspects;
    private bool _includeSolarArcs = true;
    private bool _includeSecondaryProgressions = true;
    private bool _includePrimaryDirections = true;
    private string _interpretationContext = string.Empty;
    private TransitResult? _transitResult;

    #endregion

    #region Constructor

    /// <summary>
    /// Initialises the transits view model.
    /// </summary>
    /// <param name="parent">The composing <see cref="MainViewModel"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="parent"/> is <see langword="null"/>.
    /// </exception>
    public TransitsViewModel(MainViewModel parent) : base(parent)
    {
    }

    #endregion

    #region Inputs

    /// <summary>
    /// Gets or sets the transit report start date. Consumed by
    /// <c>MainViewModel.CalculateTransitAsync</c> to build the
    /// <c>TransitInput.ReportStartDate</c>.
    /// </summary>
    public DateTime StartDate
    {
        get => _startDate;
        set => SetProperty(ref _startDate, value);
    }

    /// <summary>
    /// Gets or sets the transit report end date. Consumed by
    /// <c>MainViewModel.CalculateTransitAsync</c> to build the
    /// <c>TransitInput.ReportEndDate</c>.
    /// </summary>
    public DateTime EndDate
    {
        get => _endDate;
        set => SetProperty(ref _endDate, value);
    }

    /// <summary>
    /// Gets or sets whether the report runs the transits
    /// (<c>TransitInput.IncludeTransits</c>): the transiting bodies' aspects to
    /// the natal points, their stations and ingresses, the house overlays and
    /// the midpoint activations. True by default. The minor-aspects box is
    /// enabled only while this is true.
    /// </summary>
    /// <remarks>
    /// A change raises <see cref="HasTechnique"/> and requests a command
    /// refresh, since Calculate needs at least one technique checked.
    /// </remarks>
    public bool IncludeTransits
    {
        get => _includeTransits;
        set => SetTechnique(ref _includeTransits, value);
    }

    /// <summary>
    /// Gets or sets whether minor aspects (semi-square, sesquiquadrate,
    /// semi-sextile, quincunx) are included in the transits. Read only while
    /// <see cref="IncludeTransits"/> is true: the box is disabled otherwise,
    /// and Calculate passes false.
    /// </summary>
    public bool IncludeMinorAspects
    {
        get => _includeMinorAspects;
        set => SetProperty(ref _includeMinorAspects, value);
    }

    /// <summary>
    /// Gets or sets whether the report includes the solar arc directions
    /// (<c>TransitInput.IncludeSolarArcs</c>): the directed positions and their
    /// aspects at the report's start, and the dated hits in the period. True by
    /// default.
    /// </summary>
    /// <remarks>
    /// A change raises <see cref="HasTechnique"/> and requests a command
    /// refresh, since Calculate needs at least one technique checked.
    /// </remarks>
    public bool IncludeSolarArcs
    {
        get => _includeSolarArcs;
        set => SetTechnique(ref _includeSolarArcs, value);
    }

    /// <summary>
    /// Gets or sets whether the report includes the secondary progressions
    /// (<c>TransitInput.IncludeSecondaryProgressions</c>): the progressed
    /// positions, Moon, sign changes, stations and aspects at the report's
    /// start, and the dated hits in the period. True by default.
    /// </summary>
    /// <remarks>
    /// A change raises <see cref="HasTechnique"/> and requests a command
    /// refresh, since Calculate needs at least one technique checked.
    /// </remarks>
    public bool IncludeSecondaryProgressions
    {
        get => _includeSecondaryProgressions;
        set => SetTechnique(ref _includeSecondaryProgressions, value);
    }

    /// <summary>
    /// Gets or sets whether the report includes the primary directions
    /// (<c>TransitInput.IncludePrimaryDirections</c>): those exact in the period,
    /// found with the Directions panel's settings, so the period's hits and the
    /// whole-life list agree (branch PrimaryPanel, 1.5.0). True by default, as
    /// the other three are; the model's own default stays false.
    /// </summary>
    /// <remarks>
    /// A change raises <see cref="HasTechnique"/> and requests a command
    /// refresh, since Calculate needs at least one technique checked and, while
    /// this one is, the Directions panel's settings valid.
    /// </remarks>
    public bool IncludePrimaryDirections
    {
        get => _includePrimaryDirections;
        set => SetTechnique(ref _includePrimaryDirections, value);
    }

    /// <summary>
    /// Gets a value indicating whether at least one technique is checked:
    /// <see cref="IncludeTransits"/>, <see cref="IncludeSolarArcs"/>,
    /// <see cref="IncludeSecondaryProgressions"/> or
    /// <see cref="IncludePrimaryDirections"/>. Calculate needs it, as the
    /// transit service refuses a report with none.
    /// </summary>
    public bool HasTechnique =>
        _includeTransits || _includeSolarArcs || _includeSecondaryProgressions || _includePrimaryDirections;

    /// <summary>
    /// Gets or sets the optional free-text interpretation context for AI
    /// interpretation. Rendered in the transit Markdown header when non-empty.
    /// </summary>
    public string InterpretationContext
    {
        get => _interpretationContext;
        set => SetProperty(ref _interpretationContext, value);
    }

    #endregion

    #region Result

    /// <summary>
    /// Gets or sets the most recent <see cref="Core.Models.Transit.TransitResult"/>
    /// produced by <c>MainViewModel.CalculateTransitAsync</c>. Setting this
    /// raises <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so
    /// the parent re-evaluates the three transit-export command
    /// <c>CanExecute</c> predicates.
    /// </summary>
    public TransitResult? TransitResult
    {
        get => _transitResult;
        set
        {
            if (SetProperty(ref _transitResult, value))
            {
                OnPropertyChanged(nameof(HasResult));
                OnPropertyChanged(nameof(ResultIncludesTransits));
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a transit result is available for
    /// export. Derived from <see cref="TransitResult"/> — no separate backing
    /// field.
    /// </summary>
    public bool HasResult => _transitResult is not null;

    /// <summary>
    /// Gets a value indicating whether the result holds transits
    /// (<c>TransitResult.IncludesTransits</c>). The timeline and daily
    /// horoscope exports need it, since both are made of transits; the report
    /// export needs only <see cref="HasResult"/>.
    /// </summary>
    public bool ResultIncludesTransits => _transitResult is { IncludesTransits: true };

    #endregion

    #region Reset

    /// <summary>
    /// Resets every Transits panel field to its initial value. Uses the
    /// field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c> so a single
    /// <see cref="ChildViewModelBase.RequestCommandStateRefresh"/> fires at the
    /// end of the reset rather than cascading once per property setter. Called
    /// from the parent's <c>ClearForm</c> flow.
    /// </summary>
    public void Clear()
    {
        _startDate = DateTime.Today;
        OnPropertyChanged(nameof(StartDate));

        _endDate = DateTime.Today.AddMonths(1);
        OnPropertyChanged(nameof(EndDate));

        _includeTransits = true;
        OnPropertyChanged(nameof(IncludeTransits));

        _includeMinorAspects = false;
        OnPropertyChanged(nameof(IncludeMinorAspects));

        _includeSolarArcs = true;
        OnPropertyChanged(nameof(IncludeSolarArcs));

        _includeSecondaryProgressions = true;
        OnPropertyChanged(nameof(IncludeSecondaryProgressions));

        _includePrimaryDirections = true;
        OnPropertyChanged(nameof(IncludePrimaryDirections));
        OnPropertyChanged(nameof(HasTechnique));

        _interpretationContext = string.Empty;
        OnPropertyChanged(nameof(InterpretationContext));

        _transitResult = null;
        OnPropertyChanged(nameof(TransitResult));
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(ResultIncludesTransits));

        RequestCommandStateRefresh();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Sets a technique's field, then raises <see cref="HasTechnique"/> and
    /// requests a command refresh when the value changed.
    /// </summary>
    /// <param name="field">The technique's backing field.</param>
    /// <param name="value">The new value.</param>
    /// <param name="propertyName">The technique's property name, supplied by the compiler.</param>
    private void SetTechnique(
        ref bool field, bool value, [CallerMemberName] string? propertyName = null)
    {
        if (SetProperty(ref field, value, propertyName))
        {
            OnPropertyChanged(nameof(HasTechnique));
            RequestCommandStateRefresh();
        }
    }

    #endregion
}
