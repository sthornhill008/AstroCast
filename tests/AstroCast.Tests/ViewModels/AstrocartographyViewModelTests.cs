using System.ComponentModel;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="AstrocartographyViewModel"/> \u2014 the twelfth
/// and final H-1 decomposition child (Session H-1.12) that owns the ACG
/// panel. After this extraction <see cref="MainViewModel"/> is a pure
/// coordinator over twelve child VMs with no per-panel data surface of its
/// own. Tests cover pre-extraction toggle defaults (non-uniform: traditional
/// planets + all four line types default true, moderns + Chiron + Node
/// default false \u2014 preserved verbatim per Audit Q4), setter plumbing
/// (16 toggles each raise <see cref="AstrocartographyViewModel.VisibleLines"/>
/// so the map refreshes), the value-based
/// <see cref="AstrocartographyViewModel.HasResult"/> pattern (preserving
/// the pre-extraction explicit <c>_hasAcgResult</c> field shape per Audit
/// Q2), the derived <see cref="AstrocartographyViewModel.VisibleLines"/>
/// filter projection, parent \u2192 child write paths for
/// <see cref="AstrocartographyViewModel.CurrentResult"/> +
/// <see cref="AstrocartographyViewModel.HasResult"/> +
/// <see cref="AstrocartographyViewModel.CrossingReportText"/>, the
/// field-bypass <see cref="AstrocartographyViewModel.Clear"/> pattern, and
/// stable composition under <see cref="MainViewModel.Astrocartography"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AstrocartographyViewModelTests
{
    #region Test Infrastructure

    /// <summary>
    /// Constructs a <see cref="MainViewModel"/> using the same stub set as
    /// <see cref="MainViewModelTests"/> and returns its
    /// <see cref="MainViewModel.Astrocartography"/> child. We construct the
    /// parent rather than the child in isolation because the child derives
    /// from <see cref="ChildViewModelBase"/>, whose constructor requires a
    /// real <see cref="MainViewModel"/> parent.
    /// </summary>
    private static (MainViewModel Parent, AstrocartographyViewModel Child) CreateChild()
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        MainViewModel parent = new(
            new StubChartEngine(),
            new StubChartRepository(),
            new StubEphemerisService(),
            new StubSynastryService(),
            new StubCompositeService(),
            new StubSolarReturnService(),
            new StubLunarReturnService(),
            new StubPlanetaryReturnService(),
            new StubCycleTimelineService(),
            new StubGreatConjunctionService(),
            new StubIngressService(),
            new StubLunationService(),
            new StubRelocationService(),
            new StubMundaneAspectService(),
            new StubEclipseService(),
            new StubReturnIngressTimelineService(),
            new StubSynodicCycleService(),
            new StubTransitService(),
            new StubElectionalService(),
            new StubAstrocartographyService(),
            new TestDialogService(),
            new OperationCancellationRegistry(),
            logger);
        return (parent, parent.Astrocartography);
    }

    /// <summary>
    /// Fabricates a minimally-populated <see cref="AcgResult"/> carrying a
    /// single Sun MC line. Tests only exercise the reference round-trip and
    /// the <see cref="AstrocartographyViewModel.VisibleLines"/> filter; the
    /// natal-chart bodies are intentionally empty because neither the setter
    /// nor the filter traverses them.
    /// </summary>
    private static AcgResult MakeResult(
        CelestialBodyId body = CelestialBodyId.Sun,
        AcgLineType lineType = AcgLineType.MC)
    {
        ChartInput input = new()
        {
            Name = "Test Native",
            LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0),
            TimeZone = "UTC",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
        };
        ChartResult chart = new()
        {
            Input = input,
            JulianDay = 2451545.0,
            Obliquity = 23.4367,
            UtcDateTime = DateTime.UtcNow,
            Positions = [],
            Houses = new HouseData
            {
                Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
                Ascendant = 0.0,
                Midheaven = 270.0,
                Armc = 0.0,
                Vertex = 0.0,
            },
            Aspects = [],
            Midpoints = [],
            MidpointActivations = [],
            Dignities = [],
            MoonCondition = null,
            ChartContext = null,
            FixedStarConjunctions = [],
            ResolvedUtcOffsetHours = 0.0,
            ResolvedOffsetLabel = "UTC",
            HouseSystemName = "Placidus",
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };
        AcgLine line = new()
        {
            Body = body,
            BodyName = body.ToString(),
            LineType = lineType,
            Coordinates =
            [
                new AcgCoordinate(-66.0, 0.0),
                new AcgCoordinate(66.0, 0.0),
            ],
            PlanetLongitude = 0.0,
            PlanetLatitude = 0.0,
        };
        return new AcgResult
        {
            NatalChart = chart,
            Lines = [line],
            Obliquity = 23.4367,
            GastDegrees = 0.0,
            LatitudeMin = -66.0,
            LatitudeMax = 66.0,
            LatitudeStep = 1.0,
        };
    }

    #endregion

    #region Defaults \u2014 Audit Q4 (non-uniform toggle defaults preserved verbatim)

    /// <summary>
    /// The 7 traditional planet toggles (Sun through Saturn) default to
    /// <see langword="true"/>. Audit Q4 mandates verbatim preservation of
    /// the pre-H-1.12 defaults; changing any of them would alter the
    /// user-visible initial ACG map state.
    /// </summary>
    [Fact]
    public void ToggleDefaults_TraditionalPlanetsOn()
    {
        (_, AstrocartographyViewModel child) = CreateChild();

        child.ShowSun.Should().BeTrue("Sun line should default to visible");
        child.ShowMoon.Should().BeTrue("Moon line should default to visible");
        child.ShowMercury.Should().BeTrue("Mercury line should default to visible");
        child.ShowVenus.Should().BeTrue("Venus line should default to visible");
        child.ShowMars.Should().BeTrue("Mars line should default to visible");
        child.ShowJupiter.Should().BeTrue("Jupiter line should default to visible");
        child.ShowSaturn.Should().BeTrue("Saturn line should default to visible");
    }

    /// <summary>
    /// The 5 moderns + Chiron + North Node toggles default to
    /// <see langword="false"/>. Audit Q4 verbatim-preservation mandate.
    /// </summary>
    [Fact]
    public void ToggleDefaults_ModernsAndChironAndNodeOff()
    {
        (_, AstrocartographyViewModel child) = CreateChild();

        child.ShowUranus.Should().BeFalse("modern planets should default to hidden");
        child.ShowNeptune.Should().BeFalse("modern planets should default to hidden");
        child.ShowPluto.Should().BeFalse("modern planets should default to hidden");
        child.ShowChiron.Should().BeFalse("Chiron should default to hidden");
        child.ShowNorthNode.Should().BeFalse("North Node should default to hidden");
    }

    /// <summary>
    /// All four line-type toggles (MC, IC, ASC, DSC) default to
    /// <see langword="true"/>.
    /// </summary>
    [Fact]
    public void LineTypeToggles_DefaultAllTrue()
    {
        (_, AstrocartographyViewModel child) = CreateChild();

        child.ShowMcLines.Should().BeTrue("MC lines should default visible");
        child.ShowIcLines.Should().BeTrue("IC lines should default visible");
        child.ShowAscLines.Should().BeTrue("ASC lines should default visible");
        child.ShowDscLines.Should().BeTrue("DSC lines should default visible");
    }

    /// <summary>
    /// Result state defaults: no result, flag false, empty crossing report,
    /// empty visible-lines projection.
    /// </summary>
    [Fact]
    public void ResultState_DefaultsToEmpty()
    {
        (_, AstrocartographyViewModel child) = CreateChild();

        child.CurrentResult.Should().BeNull();
        child.HasResult.Should().BeFalse(
            "no ACG result exists before CalculateAcgCommand runs");
        child.CrossingReportText.Should().BeEmpty();
        child.VisibleLines.Should().BeEmpty(
            "no lines are visible until an ACG result is assigned");
    }

    #endregion

    #region Setter Plumbing \u2014 Planet Toggles

    /// <summary>
    /// Setting <see cref="AstrocartographyViewModel.ShowSun"/> raises
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for both the
    /// toggle itself and the derived
    /// <see cref="AstrocartographyViewModel.VisibleLines"/> projection \u2014
    /// WPF XAML bindings rely on both to refresh the map.
    /// </summary>
    [Fact]
    public void ShowSun_Setter_NotifiesAndFiresVisibleLinesChange()
    {
        (_, AstrocartographyViewModel child) = CreateChild();
        List<string> changed = [];
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        child.ShowSun = false;

        changed.Should().Contain(nameof(AstrocartographyViewModel.ShowSun));
        changed.Should().Contain(nameof(AstrocartographyViewModel.VisibleLines),
            "toggling a planet visibility must raise PropertyChanged for VisibleLines so the map refreshes");
        child.ShowSun.Should().BeFalse();
    }

    /// <summary>
    /// Representative modern-planet toggle test: flipping the Uranus toggle
    /// from its <see langword="false"/> default to <see langword="true"/>
    /// raises <c>PropertyChanged</c> for both itself and
    /// <see cref="AstrocartographyViewModel.VisibleLines"/>.
    /// </summary>
    [Fact]
    public void ShowUranus_Setter_NotifiesAndFiresVisibleLinesChange()
    {
        (_, AstrocartographyViewModel child) = CreateChild();
        List<string> changed = [];
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        child.ShowUranus = true;

        changed.Should().Contain(nameof(AstrocartographyViewModel.ShowUranus));
        changed.Should().Contain(nameof(AstrocartographyViewModel.VisibleLines));
        child.ShowUranus.Should().BeTrue();
    }

    /// <summary>
    /// Setting a toggle to its current value is a no-op under
    /// <see cref="EqualityComparer{T}.Default"/> \u2014 no
    /// <c>PropertyChanged</c> should fire. Pins the
    /// <c>ChildViewModelBase.SetProperty</c> contract for ACG toggles.
    /// </summary>
    [Fact]
    public void ShowSun_SetToSameValue_DoesNotNotify()
    {
        (_, AstrocartographyViewModel child) = CreateChild();
        List<string> changed = [];
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        child.ShowSun = true; // already true by default

        changed.Should().NotContain(nameof(AstrocartographyViewModel.ShowSun));
        changed.Should().NotContain(nameof(AstrocartographyViewModel.VisibleLines));
    }

    #endregion

    #region Setter Plumbing \u2014 Line-Type Toggles

    /// <summary>
    /// Setting <see cref="AstrocartographyViewModel.ShowMcLines"/> notifies
    /// and fires a <see cref="AstrocartographyViewModel.VisibleLines"/>
    /// change, matching the planet-toggle contract.
    /// </summary>
    [Fact]
    public void ShowMcLines_Setter_NotifiesAndFiresVisibleLinesChange()
    {
        (_, AstrocartographyViewModel child) = CreateChild();
        List<string> changed = [];
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        child.ShowMcLines = false;

        changed.Should().Contain(nameof(AstrocartographyViewModel.ShowMcLines));
        changed.Should().Contain(nameof(AstrocartographyViewModel.VisibleLines),
            "toggling a line-type visibility must raise PropertyChanged for VisibleLines");
        child.ShowMcLines.Should().BeFalse();
    }

    #endregion

    #region Result Writes \u2014 Value-based HasResult (H-1.8/H-1.9/H-1.10 pattern)

    /// <summary>
    /// Setting <see cref="AstrocartographyViewModel.CurrentResult"/> to a
    /// non-null value raises <c>PropertyChanged</c> for both
    /// <c>CurrentResult</c> and
    /// <see cref="AstrocartographyViewModel.VisibleLines"/>. Unlike the
    /// derived-<c>HasResult</c> pattern used in H-1.7 / H-1.11, this setter
    /// does <b>not</b> flip <c>HasResult</c> or fire a command refresh by
    /// itself \u2014 the parent writes both properties as two independent
    /// steps (Audit Q2: value-based shape preserved from the pre-extraction
    /// two-step write).
    /// </summary>
    [Fact]
    public void CurrentResult_Set_NotifiesSelfAndVisibleLinesOnly()
    {
        (_, AstrocartographyViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.CurrentResult = MakeResult();

        changed.Should().Contain(nameof(AstrocartographyViewModel.CurrentResult));
        changed.Should().Contain(nameof(AstrocartographyViewModel.VisibleLines),
            "assigning a new result must rebind the derived VisibleLines projection");
        changed.Should().NotContain(nameof(AstrocartographyViewModel.HasResult),
            "value-based HasResult is written separately by the parent \u2014 assigning CurrentResult alone must not fire HasResult");
        refreshCount.Should().Be(0,
            "CurrentResult setter does not gate commands under the value-based shape");
        child.CurrentResult.Should().NotBeNull();
        child.HasResult.Should().BeFalse(
            "value-based: HasResult does not flip when CurrentResult is assigned");
    }

    /// <summary>
    /// Setting <see cref="AstrocartographyViewModel.HasResult"/> raises
    /// <c>PropertyChanged</c> and fires exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> because
    /// <c>ExportAcgMarkdownCommand</c> and <c>OpenAcgMapWindowCommand</c>
    /// both gate on it.
    /// </summary>
    [Fact]
    public void HasResult_Set_NotifiesAndRefreshes()
    {
        (_, AstrocartographyViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.HasResult = true;

        changed.Should().Contain(nameof(AstrocartographyViewModel.HasResult));
        refreshCount.Should().Be(1,
            "ExportAcgMarkdownCommand and OpenAcgMapWindowCommand gate on HasResult \u2014 exactly one refresh must fire");
        child.HasResult.Should().BeTrue();
    }

    /// <summary>
    /// Setting <see cref="AstrocartographyViewModel.CrossingReportText"/>
    /// raises <c>PropertyChanged</c> but does not fire a command refresh
    /// (no command's <c>CanExecute</c> reads the crossing report).
    /// </summary>
    [Fact]
    public void CrossingReportText_Set_NotifiesButDoesNotRefresh()
    {
        (_, AstrocartographyViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.CrossingReportText = "Crossing Report: 51.50\u00b0N, 0.13\u00b0W";

        changed.Should().Contain(nameof(AstrocartographyViewModel.CrossingReportText));
        refreshCount.Should().Be(0);
    }

    #endregion

    #region VisibleLines Filter Projection

    /// <summary>
    /// When <see cref="AstrocartographyViewModel.CurrentResult"/> is null
    /// <see cref="AstrocartographyViewModel.VisibleLines"/> returns an empty
    /// list regardless of the toggle state.
    /// </summary>
    [Fact]
    public void VisibleLines_WithNoResult_ReturnsEmpty()
    {
        (_, AstrocartographyViewModel child) = CreateChild();

        child.VisibleLines.Should().BeEmpty();
    }

    /// <summary>
    /// A Sun MC line is visible when both <see cref="AstrocartographyViewModel.ShowSun"/>
    /// and <see cref="AstrocartographyViewModel.ShowMcLines"/> are on
    /// (the default).
    /// </summary>
    [Fact]
    public void VisibleLines_WithSunMcLineAndDefaults_IncludesLine()
    {
        (_, AstrocartographyViewModel child) = CreateChild();
        child.CurrentResult = MakeResult(CelestialBodyId.Sun, AcgLineType.MC);

        child.VisibleLines.Should().HaveCount(1,
            "Sun MC line passes both planet and line-type filters under defaults");
    }

    /// <summary>
    /// Turning off the Sun toggle filters a Sun MC line out of
    /// <see cref="AstrocartographyViewModel.VisibleLines"/>.
    /// </summary>
    [Fact]
    public void VisibleLines_PlanetToggleOff_FiltersLineOut()
    {
        (_, AstrocartographyViewModel child) = CreateChild();
        child.CurrentResult = MakeResult(CelestialBodyId.Sun, AcgLineType.MC);

        child.ShowSun = false;

        child.VisibleLines.Should().BeEmpty(
            "Sun MC line must be filtered out when the Sun toggle is off");
    }

    /// <summary>
    /// Turning off the MC line-type toggle filters a Sun MC line out of
    /// <see cref="AstrocartographyViewModel.VisibleLines"/>.
    /// </summary>
    [Fact]
    public void VisibleLines_LineTypeToggleOff_FiltersLineOut()
    {
        (_, AstrocartographyViewModel child) = CreateChild();
        child.CurrentResult = MakeResult(CelestialBodyId.Sun, AcgLineType.MC);

        child.ShowMcLines = false;

        child.VisibleLines.Should().BeEmpty(
            "Sun MC line must be filtered out when the MC line-type toggle is off");
    }

    /// <summary>
    /// The <see cref="CelestialBodyId.TrueNode"/> filter branch maps to
    /// <see cref="AstrocartographyViewModel.ShowNorthNode"/> \u2014 the UI
    /// labels it "North Node" while the engine emits <c>TrueNode</c>.
    /// </summary>
    [Fact]
    public void VisibleLines_TrueNodeMapsToShowNorthNodeToggle()
    {
        (_, AstrocartographyViewModel child) = CreateChild();
        child.CurrentResult = MakeResult(CelestialBodyId.TrueNode, AcgLineType.MC);

        child.VisibleLines.Should().BeEmpty(
            "TrueNode line hidden by default because ShowNorthNode defaults to false");

        child.ShowNorthNode = true;

        child.VisibleLines.Should().HaveCount(1,
            "flipping ShowNorthNode must make the TrueNode line visible");
    }

    #endregion

    #region Clear Semantics

    /// <summary>
    /// <see cref="AstrocartographyViewModel.Clear"/> must reset every
    /// mutable field (current result, has-result flag, crossing report,
    /// all 16 toggles back to their pre-extraction defaults) and emit
    /// exactly one <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>
    /// via the field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c>. If Clear used setters the
    /// <c>HasResult</c> setter alone would already fire one refresh; the
    /// other writes (toggles + CurrentResult + CrossingReportText) would
    /// cascade additional notifications. Field-bypass reduces the refresh
    /// count to exactly one deliberate terminal pulse.
    /// </summary>
    [Fact]
    public void Clear_ResetsAllFieldsAndFiresRefreshOnce()
    {
        (_, AstrocartographyViewModel child) = CreateChild();
        child.CurrentResult = MakeResult();
        child.HasResult = true;
        child.CrossingReportText = "Test report";
        child.ShowSun = false;
        child.ShowUranus = true;
        child.ShowMcLines = false;

        int refreshCount = 0;
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.Clear();

        child.CurrentResult.Should().BeNull();
        child.HasResult.Should().BeFalse();
        child.CrossingReportText.Should().BeEmpty();

        child.ShowSun.Should().BeTrue("traditional planets default on");
        child.ShowMoon.Should().BeTrue();
        child.ShowMercury.Should().BeTrue();
        child.ShowVenus.Should().BeTrue();
        child.ShowMars.Should().BeTrue();
        child.ShowJupiter.Should().BeTrue();
        child.ShowSaturn.Should().BeTrue();
        child.ShowUranus.Should().BeFalse("moderns + Chiron + Node default off");
        child.ShowNeptune.Should().BeFalse();
        child.ShowPluto.Should().BeFalse();
        child.ShowChiron.Should().BeFalse();
        child.ShowNorthNode.Should().BeFalse();
        child.ShowMcLines.Should().BeTrue("all four line types default on");
        child.ShowIcLines.Should().BeTrue();
        child.ShowAscLines.Should().BeTrue();
        child.ShowDscLines.Should().BeTrue();

        refreshCount.Should().Be(1,
            "Clear uses the field-bypass pattern so exactly one refresh " +
            "fires at the end rather than cascading through each setter");
    }

    #endregion

    #region Parent Command Gating

    /// <summary>
    /// <c>ExportAcgMarkdownCommand</c> on the parent must be disabled until
    /// <see cref="AstrocartographyViewModel.HasResult"/> flips true, and
    /// must enable automatically when the child raises its refresh signal.
    /// This pins the parent \u2194 child wiring via the
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> event
    /// subscription established in the parent's constructor.
    /// </summary>
    [Fact]
    public void ExportAcgMarkdownCommand_GatedOn_ChildHasResult()
    {
        (MainViewModel parent, AstrocartographyViewModel child) = CreateChild();

        parent.ExportAcgMarkdownCommand.CanExecute(null).Should().BeFalse(
            "export disabled until an ACG result is available on the child");

        child.HasResult = true;

        parent.ExportAcgMarkdownCommand.CanExecute(null).Should().BeTrue(
            "flipping HasResult on the child must propagate a command-state " +
            "refresh to the parent so the Export button lights up");
    }

    /// <summary>
    /// <c>OpenAcgMapWindowCommand</c> on the parent is gated on the child's
    /// <see cref="AstrocartographyViewModel.HasResult"/> in lockstep with
    /// <c>ExportAcgMarkdownCommand</c>.
    /// </summary>
    [Fact]
    public void OpenAcgMapWindowCommand_GatedOn_ChildHasResult()
    {
        (MainViewModel parent, AstrocartographyViewModel child) = CreateChild();

        parent.OpenAcgMapWindowCommand.CanExecute(null).Should().BeFalse(
            "Open Map Window disabled until ACG has produced a result to display");

        child.HasResult = true;

        parent.OpenAcgMapWindowCommand.CanExecute(null).Should().BeTrue();
    }

    #endregion

    #region UpdateCrossingReport (parent method writes through child)

    /// <summary>
    /// <see cref="MainViewModel.UpdateCrossingReport"/> is a no-op when the
    /// child has no current result (pre-H-1.12 behaviour preserved
    /// verbatim). The method stays on the parent because it reads the
    /// parent-owned <c>IAstrocartographyService</c> per Rule #4; it writes
    /// its output through
    /// <see cref="AstrocartographyViewModel.CrossingReportText"/>.
    /// </summary>
    [Fact]
    public void UpdateCrossingReport_WithNoResult_IsNoOp()
    {
        (MainViewModel parent, AstrocartographyViewModel child) = CreateChild();

        Action act = () => parent.UpdateCrossingReport(51.5, -0.1);

        act.Should().NotThrow(
            "calling UpdateCrossingReport before ACG is computed is a benign no-op");
        child.CrossingReportText.Should().BeEmpty(
            "without a CurrentResult the crossing report text stays empty");
    }

    #endregion

    #region Composition Invariants

    /// <summary>
    /// <see cref="MainViewModel.Astrocartography"/> must be a concrete
    /// instance of <see cref="AstrocartographyViewModel"/> and must be the
    /// same reference each time it is accessed \u2014 composition is eager
    /// and the property getter must not construct a fresh child per call.
    /// </summary>
    [Fact]
    public void Parent_ExposesStableAstrocartographyChild()
    {
        (MainViewModel parent, AstrocartographyViewModel child) = CreateChild();

        child.Should().NotBeNull();
        parent.Astrocartography.Should().BeSameAs(child,
            "Astrocartography is an eagerly-composed child; the getter must " +
            "not produce a new instance on every access");
    }

    /// <summary>
    /// After the H-1.12 extraction <see cref="MainViewModel"/> exposes
    /// twelve eager child VMs. This test inventories the set so accidental
    /// removal of any child surfaces in CI immediately.
    /// </summary>
    [Fact]
    public void Parent_ExposesAllTwelveH1Children()
    {
        (MainViewModel parent, _) = CreateChild();

        parent.ChartEntry.Should().NotBeNull();
        parent.ChartEntryLocationSearch.Should().NotBeNull();
        parent.Synastry.Should().NotBeNull();
        parent.Transits.Should().NotBeNull();
        parent.Electional.Should().NotBeNull();
        parent.Returns.Should().NotBeNull();
        parent.Cycles.Should().NotBeNull();
        parent.Mundane.Should().NotBeNull();
        parent.ReturnIngressTimeline.Should().NotBeNull();
        parent.Relocation.Should().NotBeNull();
        parent.Astrocartography.Should().NotBeNull(
            "Astrocartography is the twelfth and final H-1 decomposition child");
    }

    #endregion
}
