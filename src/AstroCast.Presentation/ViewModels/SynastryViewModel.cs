using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;

namespace AstroCast.ViewModels;

/// <summary>
/// Child view model that owns the Synastry &amp; Composite panel state: the
/// selected Person B chart, the optional cached Person B <see cref="ChartResult"/>,
/// relationship metadata, the most recent synastry and composite results, and
/// the composite interpretation context. Extracted from
/// <see cref="MainViewModel"/> by Session H-1.4 as a single child because both
/// panels share the Person B selection plumbing; keeping them fused avoids
/// splitting that plumbing across two siblings.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> This VM owns the data surface for the Synastry &amp; Composite
/// panel: <see cref="PersonB"/>, <see cref="PersonBChart"/>, the relationship
/// type and notes, the <see cref="SynastryResult"/> and
/// <see cref="CompositeResult"/> outputs, and the
/// <see cref="CompositeInterpretationContext"/> free-text field. XAML bindings
/// for these fields live under <c>Synastry.X</c> in <c>MainWindow.xaml</c>.
/// </para>
/// <para>
/// <b>Commands stay on parent (rule #4).</b> <c>CalculateSynastryCommand</c>,
/// <c>ExportSynastryMarkdownCommand</c>, <c>CalculateCompositeCommand</c>, and
/// <c>ExportCompositeMarkdownCommand</c> all remain on
/// <see cref="MainViewModel"/> because every one of them reads the parent-owned
/// <c>LastResult</c> (Person A), calls parent-owned services
/// (<c>ISynastryService</c>, <c>ICompositeService</c>,
/// <c>IChartEngine</c>), and writes status text on the parent. Command
/// methods read <see cref="PersonB"/> and <see cref="CompositeInterpretationContext"/>
/// from this child and write back to <see cref="SynastryResult"/>,
/// <see cref="CompositeResult"/>, and <see cref="PersonBChart"/>.
/// </para>
/// <para>
/// <b>Command-state refresh.</b> Setters for <see cref="PersonB"/>,
/// <see cref="SynastryResult"/>, and <see cref="CompositeResult"/> raise
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the parent
/// re-evaluates <c>CanExecute</c> on all four Synastry and Composite commands.
/// A single handler on the parent covers both panels because they share the
/// Person B plumbing — <c>CalculateSynastry.CanExecute</c> and
/// <c>CalculateComposite.CanExecute</c> both depend on
/// <c>HasResult &amp;&amp; PersonB is not null</c>.
/// </para>
/// <para>
/// <b>PersonBChart cache.</b> <see cref="PersonBChart"/> is an output-only
/// cache populated by the parent's <c>CalculateSynastryAsync</c> and
/// <c>CalculateCompositeAsync</c> methods after they compute Person B's chart.
/// Pre-H-1.4 each command re-computed the chart independently; storing the
/// result here lets tests observe it and future calls reuse it. No command
/// currently binds to <see cref="PersonBChart"/>, so its setter does not raise
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>.
/// </para>
/// </remarks>
public sealed class SynastryViewModel : ChildViewModelBase
{
    #region Fields

    private ChartEntity? _personB;
    private ChartResult? _personBChart;
    private SynastryResult? _synastryResult;
    private CompositeResult? _compositeResult;
    private RelationshipType _selectedRelationshipType = RelationshipType.Unspecified;
    private string _relationshipNotes = string.Empty;
    private string _compositeInterpretationContext = string.Empty;
    private DavisonResult? _davisonResult;
    private string _davisonInterpretationContext = string.Empty;

    #endregion

    #region Constructor

    /// <summary>
    /// Initialises the synastry &amp; composite view model.
    /// </summary>
    /// <param name="parent">The composing <see cref="MainViewModel"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="parent"/> is <see langword="null"/>.
    /// </exception>
    public SynastryViewModel(MainViewModel parent) : base(parent)
    {
    }

    #endregion

    #region Person B

    /// <summary>
    /// Gets or sets the persisted Person B chart entity selected from the
    /// ComboBox over the parent's <c>AllSavedCharts</c> (the unfiltered list,
    /// whose refresh keeps this selection by Id). Raises
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent re-evaluates the Calculate Synastry and Calculate Composite
    /// command <c>CanExecute</c> predicates.
    /// </summary>
    public ChartEntity? PersonB
    {
        get => _personB;
        set
        {
            if (SetProperty(ref _personB, value))
                RequestCommandStateRefresh();
        }
    }

    /// <summary>
    /// Gets or sets the cached calculated natal chart for <see cref="PersonB"/>.
    /// Written by the parent's synastry and composite command methods after
    /// they compute Person B's chart; allows tests to observe the cache and
    /// future callers to reuse it. Setter does not refresh command state
    /// because no command's <c>CanExecute</c> depends on this field.
    /// </summary>
    public ChartResult? PersonBChart
    {
        get => _personBChart;
        set => SetProperty(ref _personBChart, value);
    }

    /// <summary>
    /// Gets the list of <see cref="RelationshipType"/> enum values bound to
    /// the Relationship ComboBox. Static list per instance — seeded once and
    /// never mutated.
    /// </summary>
    public IReadOnlyList<RelationshipType> RelationshipTypes { get; } =
        Enum.GetValues<RelationshipType>();

    /// <summary>
    /// Gets or sets the selected relationship type for synastry
    /// interpretation context.
    /// </summary>
    public RelationshipType SelectedRelationshipType
    {
        get => _selectedRelationshipType;
        set => SetProperty(ref _selectedRelationshipType, value);
    }

    /// <summary>
    /// Gets or sets the optional free-text notes describing the relationship
    /// (e.g. "married 15 years", "business partners since 2010"). Included in
    /// the synastry Markdown export when non-empty.
    /// </summary>
    public string RelationshipNotes
    {
        get => _relationshipNotes;
        set => SetProperty(ref _relationshipNotes, value);
    }

    #endregion

    #region Synastry Result

    /// <summary>
    /// Gets or sets the most recent <see cref="Core.Models.SynastryResult"/>
    /// produced by <c>MainViewModel.CalculateSynastryAsync</c>. Setting this
    /// raises <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so
    /// the parent re-evaluates the Export Synastry Markdown command.
    /// </summary>
    public SynastryResult? SynastryResult
    {
        get => _synastryResult;
        set
        {
            if (SetProperty(ref _synastryResult, value))
            {
                OnPropertyChanged(nameof(HasSynastryResult));
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a synastry result is available for
    /// export. Derived from <see cref="SynastryResult"/> — no separate
    /// backing field.
    /// </summary>
    public bool HasSynastryResult => _synastryResult is not null;

    #endregion

    #region Composite Result

    /// <summary>
    /// Gets or sets the most recent <see cref="Core.Models.CompositeResult"/>
    /// produced by <c>MainViewModel.CalculateCompositeAsync</c>. Setting this
    /// raises <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so
    /// the parent re-evaluates the Export Composite Markdown command.
    /// </summary>
    public CompositeResult? CompositeResult
    {
        get => _compositeResult;
        set
        {
            if (SetProperty(ref _compositeResult, value))
            {
                OnPropertyChanged(nameof(HasCompositeResult));
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a composite result is available for
    /// export. Derived from <see cref="CompositeResult"/> — no separate
    /// backing field.
    /// </summary>
    public bool HasCompositeResult => _compositeResult is not null;

    /// <summary>
    /// Gets or sets the optional free-text interpretation context for
    /// composite chart AI interpretation. Rendered in the composite Markdown
    /// header when non-empty.
    /// </summary>
    public string CompositeInterpretationContext
    {
        get => _compositeInterpretationContext;
        set => SetProperty(ref _compositeInterpretationContext, value);
    }

    #endregion

    #region Davison Result

    /// <summary>
    /// Gets or sets the most recent <see cref="Core.Models.DavisonResult"/> produced by
    /// <c>MainViewModel.CalculateDavisonAsync</c>: the Davison chart, a real chart cast for the midpoint moment
    /// of the two births at the midpoint place, the Composite's twin on the Synastry panel (branch DavisonPanel,
    /// 1.7.0). Setting this raises <see cref="HasDavisonResult"/> and
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the parent re-evaluates the Export
    /// Davison Markdown command.
    /// </summary>
    public DavisonResult? DavisonResult
    {
        get => _davisonResult;
        set
        {
            if (SetProperty(ref _davisonResult, value))
            {
                OnPropertyChanged(nameof(HasDavisonResult));
                RequestCommandStateRefresh();
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether a Davison result is available for export. Derived from
    /// <see cref="DavisonResult"/>, no separate backing field.
    /// </summary>
    public bool HasDavisonResult => _davisonResult is not null;

    /// <summary>
    /// Gets or sets the optional free-text interpretation context for the Davison chart's AI interpretation,
    /// rendered in the Davison Markdown header when non-empty (the Composite's twin).
    /// </summary>
    public string DavisonInterpretationContext
    {
        get => _davisonInterpretationContext;
        set => SetProperty(ref _davisonInterpretationContext, value);
    }

    #endregion

    #region Reset

    /// <summary>
    /// Resets every Synastry, Composite and Davison panel field to its initial value.
    /// Uses the field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c> so a single
    /// <see cref="ChildViewModelBase.RequestCommandStateRefresh"/> fires at
    /// the end of the reset rather than cascading once per property setter.
    /// Called from the parent's <c>ClearForm</c> flow.
    /// </summary>
    public void Clear()
    {
        _personB = null;
        OnPropertyChanged(nameof(PersonB));

        _personBChart = null;
        OnPropertyChanged(nameof(PersonBChart));

        _synastryResult = null;
        OnPropertyChanged(nameof(SynastryResult));
        OnPropertyChanged(nameof(HasSynastryResult));

        _compositeResult = null;
        OnPropertyChanged(nameof(CompositeResult));
        OnPropertyChanged(nameof(HasCompositeResult));

        _selectedRelationshipType = RelationshipType.Unspecified;
        OnPropertyChanged(nameof(SelectedRelationshipType));

        _relationshipNotes = string.Empty;
        OnPropertyChanged(nameof(RelationshipNotes));

        _compositeInterpretationContext = string.Empty;
        OnPropertyChanged(nameof(CompositeInterpretationContext));

        _davisonResult = null;
        OnPropertyChanged(nameof(DavisonResult));
        OnPropertyChanged(nameof(HasDavisonResult));

        _davisonInterpretationContext = string.Empty;
        OnPropertyChanged(nameof(DavisonInterpretationContext));

        RequestCommandStateRefresh();
    }

    #endregion
}
