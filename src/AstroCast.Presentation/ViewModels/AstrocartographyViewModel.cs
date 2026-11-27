using System.ComponentModel;

using AstroCast.Core.Enums;
using AstroCast.Core.Models.Astrocartography;

namespace AstroCast.ViewModels;

/// <summary>
/// Child view model that owns the Astrocartography panel state: the 12
/// celestial-body visibility toggles, the 4 line-type toggles, the most
/// recent <see cref="AcgResult"/> produced by
/// <see cref="Core.Interfaces.IAstrocartographyService.Calculate"/>, the
/// computed <see cref="VisibleLines"/> projection that applies the current
/// toggle filters, and the formatted map-click crossing report text.
/// Extracted from <see cref="MainViewModel"/> by Session H-1.12 as the
/// twelfth and final H-1 decomposition child. After this extraction,
/// <see cref="MainViewModel"/> is a pure coordinator over twelve feature
/// children with no per-panel data surface of its own.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> This VM owns the ACG panel's data surface: 16 toggle flags
/// (<see cref="ShowSun"/> through <see cref="ShowNorthNode"/> and
/// <see cref="ShowMcLines"/> through <see cref="ShowDscLines"/>), the
/// <see cref="CurrentResult"/> / <see cref="HasResult"/> pair, the
/// <see cref="CrossingReportText"/> status, and the derived
/// <see cref="VisibleLines"/> projection. XAML bindings for every one of
/// these fields live under <c>Astrocartography.X</c> in
/// <c>MainWindow.xaml</c>. Property names are the pre-extraction names with
/// the <c>Acg</c> prefix stripped inside the child, matching the H-1.2 /
/// H-1.5 / H-1.6 / H-1.9 / H-1.10 / H-1.11 precedent (e.g.
/// <c>AcgShowSun</c> \u2192 <c>ShowSun</c>).
/// </para>
/// <para>
/// <b>Audit Question 1 \u2014 single-result.</b>
/// <see cref="Core.Interfaces.IAstrocartographyService"/> produces one
/// <see cref="AcgResult"/> per invocation containing the full line set for
/// a single natal chart; there is no multi-result scan analogue.
/// <see cref="CurrentResult"/> is therefore a single nullable reference
/// paired with a single <see cref="HasResult"/> flag \u2014 the H-1.5
/// Transits / H-1.10 R/I Timeline / H-1.11 Relocation shape, not the H-1.4
/// / H-1.7 / H-1.8 / H-1.9 multi-result shape.
/// </para>
/// <para>
/// <b>Audit Question 2 \u2014 explicit value-based <c>HasResult</c>.</b>
/// The pre-H-1.12 code used an explicit backing field <c>_hasAcgResult</c>
/// with a <c>private set</c> that called <c>RefreshAcgCommandStates()</c>.
/// Although <c>IAstrocartographyService.Calculate</c> returns a non-null
/// <see cref="AcgResult"/> on success (which would support the H-1.7 /
/// H-1.11 derived shape), the H-1.8 / H-1.9 / H-1.10 value-based pattern
/// preserves the pre-extraction write shape exactly: the parent sets
/// <c>Astrocartography.CurrentResult = acgResult</c> and then
/// <c>Astrocartography.HasResult = true</c> as two independent writes, and
/// the <see cref="HasResult"/> setter is public so the parent can write
/// through it directly. This choice also keeps the door open for future
/// "cached result but muted UI" states (e.g. a settings panel that hides
/// the map without discarding the result) without forcing a shape change.
/// </para>
/// <para>
/// <b>Audit Question 3 \u2014 <c>AcgMapWindow</c> lifecycle stays
/// on parent / View layer (Outcome A).</b> The pre-H-1.12 design already
/// had clean separation: <c>MainWindow.xaml.cs.ShowAcgMapWindow</c> (View
/// layer) creates the floating map window as a singleton and sets its
/// <c>System.Windows.Window.Owner</c>, while the VM exposes an
/// <c>OpenAcgMapWindowAction</c> delegate the command invokes. H-1.12
/// preserves this topology verbatim \u2014 the window lifecycle does
/// <b>not</b> move onto this child. The <c>AcgMapWindow</c> code-behind
/// simply rebinds its property-change subscription from the parent's
/// <c>AcgVisibleLines</c> / <c>AcgCrossingReportText</c> to this child's
/// <see cref="VisibleLines"/> / <see cref="CrossingReportText"/>. This
/// avoids Outcome B (moving the <c>Window</c> reference into the VM, which
/// would pull a WPF UI type into a VM that currently has no UI references)
/// and Outcome C (splitting creation from data, which gains no clarity
/// over the existing pattern).
/// </para>
/// <para>
/// <b>Audit Question 4 \u2014 preserve non-uniform toggle defaults
/// verbatim.</b> The pre-extraction defaults are <b>not</b> uniform: the
/// five traditional planets (Sun, Moon, Mercury, Venus, Mars, Jupiter,
/// Saturn) plus all four line types default to <see langword="true"/>,
/// while the five moderns + Chiron + North Node (Uranus, Neptune, Pluto,
/// Chiron, TrueNode) default to <see langword="false"/>. H-1.12 preserves
/// every one of these defaults exactly; changing any of them would alter
/// the user-visible initial map state.
/// </para>
/// <para>
/// <b>Audit Question 5 \u2014 antipode watch item is service-layer, out of
/// scope.</b> The open Section 11 watch item covering ACG antipode
/// treatment lives entirely in
/// <see cref="Core.Services.AstrocartographyService"/> and the
/// <see cref="AcgLine.Coordinates"/> model. This VM has zero touchpoints:
/// it filters by <see cref="AcgLine.Body"/> and
/// <see cref="AcgLine.LineType"/> only, never by coordinate geometry. The
/// watch item remains open and unchanged after H-1.12.
/// </para>
/// <para>
/// <b>Commands stay on parent (rule #4).</b>
/// <c>CalculateAcgCommand</c>, <c>ExportAcgMarkdownCommand</c>, and
/// <c>OpenAcgMapWindowCommand</c> all remain on
/// <see cref="MainViewModel"/>. Each reads parent-owned services
/// (<c>IAstrocartographyService</c>, <c>IFileDialogService</c>), threads the
/// parent-owned <c>OperationCancellationRegistry</c> token, and writes
/// status text on the parent. The command methods read <c>LastResult</c>
/// from the parent (chart context) and write their output back through
/// <see cref="CurrentResult"/> / <see cref="HasResult"/>.
/// </para>
/// <para>
/// <b><c>UpdateCrossingReport</c> stays on parent (service reader).</b>
/// <see cref="MainViewModel.UpdateCrossingReport"/> calls the parent-owned
/// <c>IAstrocartographyService.GetCrossings</c> \u2014 per cross-VM rule
/// #4, service readers stay on the parent. The method writes its output
/// through <see cref="CrossingReportText"/>, which makes the parent \u2192
/// child write path explicit and one-directional, matching the
/// <c>CalculateAcgAsync</c> pattern for <see cref="CurrentResult"/>.
/// </para>
/// </remarks>
public sealed class AstrocartographyViewModel : ChildViewModelBase
{
    #region Fields

    private AcgResult? _currentResult;
    private bool _hasResult;
    private string _crossingReportText = string.Empty;

    // Planet visibility toggles \u2014 pre-extraction defaults preserved
    // verbatim: traditional planets (Sun\u2013Saturn) on, moderns + Chiron +
    // Node off. Per Audit Q4: changing any of these defaults would alter
    // the user-visible initial map state.
    private bool _showSun = true;
    private bool _showMoon = true;
    private bool _showMercury = true;
    private bool _showVenus = true;
    private bool _showMars = true;
    private bool _showJupiter = true;
    private bool _showSaturn = true;
    private bool _showUranus;
    private bool _showNeptune;
    private bool _showPluto;
    private bool _showChiron;
    private bool _showNorthNode;

    // Line-type toggles \u2014 all four default to true (pre-extraction).
    private bool _showMcLines = true;
    private bool _showIcLines = true;
    private bool _showAscLines = true;
    private bool _showDscLines = true;

    #endregion

    #region Constructor

    /// <summary>
    /// Initialises the Astrocartography view model. Unlike H-1.11 Relocation
    /// or H-1.6 Electional, this child composes no nested location search
    /// (the panel has no location input of its own \u2014 the map is
    /// centred on the natal chart's birth location supplied by
    /// <see cref="MainViewModel.LastResult"/>) and depends on no services
    /// (all service calls live on the parent per cross-VM rule #4).
    /// </summary>
    /// <param name="parent">The composing <see cref="MainViewModel"/>.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if <paramref name="parent"/> is <see langword="null"/>.
    /// </exception>
    public AstrocartographyViewModel(MainViewModel parent)
        : base(parent)
    {
    }

    #endregion

    #region Properties \u2014 Result

    /// <summary>
    /// Gets or sets the most recent <see cref="AcgResult"/> produced by
    /// <c>MainViewModel.CalculateAcgAsync</c>. The setter raises
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for both itself
    /// and <see cref="VisibleLines"/> so the map rebinds whenever the
    /// underlying line set changes. <see cref="HasResult"/> is a separate
    /// value-based flag (H-1.8 / H-1.9 / H-1.10 shape) written by the
    /// parent immediately after this assignment.
    /// </summary>
    public AcgResult? CurrentResult
    {
        get => _currentResult;
        set
        {
            if (SetProperty(ref _currentResult, value))
            {
                OnPropertyChanged(nameof(VisibleLines));
            }
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether an ACG result is available
    /// for display and export. Explicit value-based pattern preserved from
    /// the pre-extraction <c>MainViewModel._hasAcgResult</c> field so the
    /// parent's <c>CalculateAcgAsync</c> retains its two-step write shape
    /// (<see cref="CurrentResult"/> first, then <see cref="HasResult"/>).
    /// Setter requests a parent command-state refresh so
    /// <c>ExportAcgMarkdownCommand</c> and <c>OpenAcgMapWindowCommand</c>
    /// re-evaluate their <c>CanExecute</c> predicates.
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
    /// Gets or sets the formatted crossing report for the most recent map
    /// click. Populated by <see cref="MainViewModel.UpdateCrossingReport"/>
    /// (which stays on the parent because it reads the parent-owned
    /// <c>IAstrocartographyService</c>). Bound one-way to the read-only
    /// <c>System.Windows.Controls.TextBox</c> below the map and also
    /// summarised onto the floating-window status bar via
    /// <c>AcgMapWindow.OnViewModelPropertyChanged</c>.
    /// </summary>
    public string CrossingReportText
    {
        get => _crossingReportText;
        set => SetProperty(ref _crossingReportText, value);
    }

    /// <summary>
    /// Gets the subset of ACG lines that should be visible on the map
    /// based on the current planet and line-type toggle settings. Returns
    /// an empty list when no ACG result has been computed. Pre-extraction
    /// this was <c>MainViewModel.AcgVisibleLines</c>; the computation is
    /// unchanged \u2014 the filter delegates are now private helpers on
    /// this child (<see cref="IsBodyVisible"/>,
    /// <see cref="IsLineTypeVisible"/>).
    /// </summary>
    public IReadOnlyList<AcgLine> VisibleLines
    {
        get
        {
            if (_currentResult is null) return [];

            return _currentResult.Lines
                .Where(line => IsBodyVisible(line.Body) && IsLineTypeVisible(line.LineType))
                .ToList()
                .AsReadOnly();
        }
    }

    /// <summary>
    /// Clears the map's result when the Chart panel's chart changes: <see cref="CurrentResult"/>, and with it
    /// <see cref="VisibleLines"/>, the <see cref="CrossingReportText"/> read from it, and <see cref="HasResult"/>.
    /// Called by the parent's <c>LastResult</c> setter, which this view model cannot observe, and by its Clear
    /// Form (branch StaleResults, 1.7.2, finding N2).
    /// </summary>
    /// <remarks>
    /// The lines are the Chart panel's chart's, so they do not outlive it: the export and the map window's
    /// command go with them, and a map window left open draws no lines. The sixteen line boxes are left as they
    /// are. Nothing happens without a result. Until 1.7.2 the result stayed through every change of chart, Clear
    /// Form included, and its export wrote the earlier chart's.
    /// </remarks>
    internal void OnChartPanelChartChanged()
    {
        CurrentResult = null;
        CrossingReportText = string.Empty;
        HasResult = false;
    }

    #endregion

    #region Properties \u2014 Planet Toggles

    /// <summary>Whether to include the Sun's ACG lines on the map.</summary>
    public bool ShowSun
    {
        get => _showSun;
        set { if (SetProperty(ref _showSun, value)) OnFilterChanged(); }
    }

    /// <summary>Whether to include the Moon's ACG lines on the map.</summary>
    public bool ShowMoon
    {
        get => _showMoon;
        set { if (SetProperty(ref _showMoon, value)) OnFilterChanged(); }
    }

    /// <summary>Whether to include Mercury's ACG lines on the map.</summary>
    public bool ShowMercury
    {
        get => _showMercury;
        set { if (SetProperty(ref _showMercury, value)) OnFilterChanged(); }
    }

    /// <summary>Whether to include Venus' ACG lines on the map.</summary>
    public bool ShowVenus
    {
        get => _showVenus;
        set { if (SetProperty(ref _showVenus, value)) OnFilterChanged(); }
    }

    /// <summary>Whether to include Mars' ACG lines on the map.</summary>
    public bool ShowMars
    {
        get => _showMars;
        set { if (SetProperty(ref _showMars, value)) OnFilterChanged(); }
    }

    /// <summary>Whether to include Jupiter's ACG lines on the map.</summary>
    public bool ShowJupiter
    {
        get => _showJupiter;
        set { if (SetProperty(ref _showJupiter, value)) OnFilterChanged(); }
    }

    /// <summary>Whether to include Saturn's ACG lines on the map.</summary>
    public bool ShowSaturn
    {
        get => _showSaturn;
        set { if (SetProperty(ref _showSaturn, value)) OnFilterChanged(); }
    }

    /// <summary>Whether to include Uranus' ACG lines on the map.</summary>
    public bool ShowUranus
    {
        get => _showUranus;
        set { if (SetProperty(ref _showUranus, value)) OnFilterChanged(); }
    }

    /// <summary>Whether to include Neptune's ACG lines on the map.</summary>
    public bool ShowNeptune
    {
        get => _showNeptune;
        set { if (SetProperty(ref _showNeptune, value)) OnFilterChanged(); }
    }

    /// <summary>Whether to include Pluto's ACG lines on the map.</summary>
    public bool ShowPluto
    {
        get => _showPluto;
        set { if (SetProperty(ref _showPluto, value)) OnFilterChanged(); }
    }

    /// <summary>Whether to include Chiron's ACG lines on the map.</summary>
    public bool ShowChiron
    {
        get => _showChiron;
        set { if (SetProperty(ref _showChiron, value)) OnFilterChanged(); }
    }

    /// <summary>Whether to include the North Node's ACG lines on the map.</summary>
    public bool ShowNorthNode
    {
        get => _showNorthNode;
        set { if (SetProperty(ref _showNorthNode, value)) OnFilterChanged(); }
    }

    #endregion

    #region Properties \u2014 Line-Type Toggles

    /// <summary>Whether MC (Midheaven) lines are visible on the map.</summary>
    public bool ShowMcLines
    {
        get => _showMcLines;
        set { if (SetProperty(ref _showMcLines, value)) OnFilterChanged(); }
    }

    /// <summary>Whether IC (Imum Coeli) lines are visible on the map.</summary>
    public bool ShowIcLines
    {
        get => _showIcLines;
        set { if (SetProperty(ref _showIcLines, value)) OnFilterChanged(); }
    }

    /// <summary>Whether Ascendant lines are visible on the map.</summary>
    public bool ShowAscLines
    {
        get => _showAscLines;
        set { if (SetProperty(ref _showAscLines, value)) OnFilterChanged(); }
    }

    /// <summary>Whether Descendant lines are visible on the map.</summary>
    public bool ShowDscLines
    {
        get => _showDscLines;
        set { if (SetProperty(ref _showDscLines, value)) OnFilterChanged(); }
    }

    #endregion

    #region Filter Helpers

    /// <summary>
    /// Raises <see cref="INotifyPropertyChanged.PropertyChanged"/> for
    /// <see cref="VisibleLines"/>. Invoked by every toggle setter so the
    /// floating <c>AcgMapWindow</c> refresh logic rebinds.
    /// </summary>
    private void OnFilterChanged() => OnPropertyChanged(nameof(VisibleLines));

    /// <summary>
    /// Returns <see langword="true"/> when the given celestial body's lines
    /// should be visible based on the current planet toggles. The
    /// <see cref="CelestialBodyId.TrueNode"/> branch maps to
    /// <see cref="ShowNorthNode"/> (the UI label uses "North Node" while
    /// the engine emits <c>TrueNode</c> per CLAUDE.md's convention).
    /// </summary>
    private bool IsBodyVisible(CelestialBodyId body) => body switch
    {
        CelestialBodyId.Sun => ShowSun,
        CelestialBodyId.Moon => ShowMoon,
        CelestialBodyId.Mercury => ShowMercury,
        CelestialBodyId.Venus => ShowVenus,
        CelestialBodyId.Mars => ShowMars,
        CelestialBodyId.Jupiter => ShowJupiter,
        CelestialBodyId.Saturn => ShowSaturn,
        CelestialBodyId.Uranus => ShowUranus,
        CelestialBodyId.Neptune => ShowNeptune,
        CelestialBodyId.Pluto => ShowPluto,
        CelestialBodyId.TrueNode => ShowNorthNode,
        CelestialBodyId.Chiron => ShowChiron,
        _ => false,
    };

    /// <summary>
    /// Returns <see langword="true"/> when the given ACG line type should
    /// be visible based on the current line-type toggles.
    /// </summary>
    private bool IsLineTypeVisible(AcgLineType lineType) => lineType switch
    {
        AcgLineType.MC => ShowMcLines,
        AcgLineType.IC => ShowIcLines,
        AcgLineType.ASC => ShowAscLines,
        AcgLineType.DSC => ShowDscLines,
        _ => false,
    };

    #endregion

    #region Reset

    /// <summary>
    /// Resets every Astrocartography panel field to its pre-extraction
    /// default value, including the 16 toggles (non-uniform defaults
    /// preserved per Audit Q4) and the result + crossing-report state.
    /// Uses the field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c> so a single
    /// <see cref="ChildViewModelBase.RequestCommandStateRefresh"/> fires at
    /// the end of the reset rather than cascading once per setter.
    /// </summary>
    /// <remarks>
    /// Not currently invoked from <c>MainViewModel.ClearForm</c> because
    /// the pre-extraction flow did not reset the ACG panel on natal-chart
    /// clear; provided for future use and for symmetry with the other H-1
    /// children that each expose a <c>Clear()</c> method.
    /// </remarks>
    public void Clear()
    {
        _currentResult = null;
        OnPropertyChanged(nameof(CurrentResult));
        OnPropertyChanged(nameof(VisibleLines));

        _hasResult = false;
        OnPropertyChanged(nameof(HasResult));

        _crossingReportText = string.Empty;
        OnPropertyChanged(nameof(CrossingReportText));

        _showSun = true;
        OnPropertyChanged(nameof(ShowSun));
        _showMoon = true;
        OnPropertyChanged(nameof(ShowMoon));
        _showMercury = true;
        OnPropertyChanged(nameof(ShowMercury));
        _showVenus = true;
        OnPropertyChanged(nameof(ShowVenus));
        _showMars = true;
        OnPropertyChanged(nameof(ShowMars));
        _showJupiter = true;
        OnPropertyChanged(nameof(ShowJupiter));
        _showSaturn = true;
        OnPropertyChanged(nameof(ShowSaturn));
        _showUranus = false;
        OnPropertyChanged(nameof(ShowUranus));
        _showNeptune = false;
        OnPropertyChanged(nameof(ShowNeptune));
        _showPluto = false;
        OnPropertyChanged(nameof(ShowPluto));
        _showChiron = false;
        OnPropertyChanged(nameof(ShowChiron));
        _showNorthNode = false;
        OnPropertyChanged(nameof(ShowNorthNode));

        _showMcLines = true;
        OnPropertyChanged(nameof(ShowMcLines));
        _showIcLines = true;
        OnPropertyChanged(nameof(ShowIcLines));
        _showAscLines = true;
        OnPropertyChanged(nameof(ShowAscLines));
        _showDscLines = true;
        OnPropertyChanged(nameof(ShowDscLines));

        RequestCommandStateRefresh();
    }

    #endregion
}
