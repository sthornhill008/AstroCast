using System.ComponentModel;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="CyclesViewModel"/> — the multi-result Cycles
/// panel child introduced in Session H-1.8. Verifies that the simple input
/// setters (<c>IncludeCycleSnapshots</c>, <c>GCStartYear</c>, <c>GCEndYear</c>,
/// <c>SynodicBodyA</c>, <c>SynodicBodyB</c>, <c>SynodicStartYear</c>,
/// <c>SynodicEndYear</c>) raise
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> without signalling
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>; that the
/// three result setters (<c>CurrentCycleTimeline</c>, <c>CurrentGCSeries</c>,
/// <c>CurrentSynodicCycleSeries</c>) behave consistently with the
/// pre-extraction semantics — cycle timeline uses a derived <c>HasX</c> flag
/// and signals refresh via the setter; GC and synodic series setters write
/// the result without flipping the <c>HasX</c> flag themselves and the
/// companion <c>HasGCSeries</c> / <c>HasSynodicCycleSeries</c> booleans
/// (written explicitly by the parent command) signal refresh independently;
/// that <see cref="CyclesViewModel.Clear"/> resets every field via the
/// field-bypass pattern with exactly one terminal refresh pulse; and that
/// <see cref="MainViewModel.Cycles"/> is a stable, eagerly-composed child.
/// </summary>
[Trait("Category", "Unit")]
public sealed class CyclesViewModelTests
{
    #region Test Infrastructure

    /// <summary>
    /// Constructs a <see cref="MainViewModel"/> using the same stub set as
    /// <see cref="MainViewModelTests"/> and returns its
    /// <see cref="MainViewModel.Cycles"/> child. We construct the parent
    /// rather than the child in isolation because the child derives from
    /// <see cref="ChildViewModelBase"/>, whose constructor requires a real
    /// <see cref="MainViewModel"/> parent.
    /// </summary>
    private static (MainViewModel Parent, CyclesViewModel Child) CreateChild()
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
        return (parent, parent.Cycles);
    }

    /// <summary>
    /// Helper that fabricates a minimally-populated
    /// <see cref="PlanetaryCycleTimeline"/> for setter tests. Values are
    /// arbitrary but valid — tests only care that the reference round-trips.
    /// </summary>
    private static PlanetaryCycleTimeline MakeCycleTimeline() => new()
    {
        Planet = CelestialBodyId.Saturn,
        PlanetName = "Saturn",
        NatalLongitude = 120.0,
        NatalPositionFormatted = "0°00' Leo",
        NativeName = "Stub",
        ReturnNumber = 1,
        PhasePoints = [],
        CycleDurationDays = 10592.0,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// Helper that fabricates a minimally-populated
    /// <see cref="GreatConjunctionSeries"/> with the specified count. A series
    /// with zero conjunctions models the pre-extraction "empty scan" case.
    /// </summary>
    private static GreatConjunctionSeries MakeGCSeries(int count) => new()
    {
        Conjunctions = [],
        ScanStart = new DateTime(1800, 1, 1),
        ScanEnd = new DateTime(2100, 12, 31),
        Count = count,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// Helper that fabricates a minimally-populated
    /// <see cref="SynodicCycleSeries"/> with the specified count.
    /// </summary>
    private static SynodicCycleSeries MakeSynodicSeries(int count) => new()
    {
        BodyAName = "Jupiter",
        BodyBName = "Saturn",
        Conjunctions = [],
        StartDate = new DateTime(2000, 1, 1),
        EndDate = new DateTime(2100, 12, 31),
        Count = count,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    #endregion

    #region Defaults

    /// <summary>
    /// <see cref="CyclesViewModel.IncludeCycleSnapshots"/> defaults to
    /// <see langword="false"/>.
    /// </summary>
    [Fact]
    public void IncludeCycleSnapshots_Should_DefaultToFalse()
    {
        // Arrange / Act
        (_, CyclesViewModel child) = CreateChild();

        // Assert
        child.IncludeCycleSnapshots.Should().BeFalse();
    }

    /// <summary>
    /// <see cref="CyclesViewModel.HasCycleTimeline"/> defaults to
    /// <see langword="false"/> because
    /// <see cref="CyclesViewModel.CurrentCycleTimeline"/> starts
    /// <see langword="null"/>. The flag is derived from the result reference —
    /// there is no separate backing field.
    /// </summary>
    [Fact]
    public void HasCycleTimeline_Should_DefaultToFalse()
    {
        // Arrange / Act
        (_, CyclesViewModel child) = CreateChild();

        // Assert
        child.HasCycleTimeline.Should().BeFalse(
            "no cycle timeline has been calculated yet");
        child.CurrentCycleTimeline.Should().BeNull();
    }

    /// <summary>
    /// <see cref="CyclesViewModel.GCStartYear"/> and
    /// <see cref="CyclesViewModel.GCEndYear"/> default to the hard-coded scan
    /// range <c>1800</c>–<c>2100</c>, matching the pre-extraction
    /// <see cref="MainViewModel"/> field initialisers verbatim.
    /// </summary>
    [Fact]
    public void GCYears_Should_DefaultTo1800And2100()
    {
        // Arrange / Act
        (_, CyclesViewModel child) = CreateChild();

        // Assert
        child.GCStartYear.Should().Be(1800);
        child.GCEndYear.Should().Be(2100);
    }

    /// <summary>
    /// <see cref="CyclesViewModel.HasGCSeries"/> defaults to
    /// <see langword="false"/> and
    /// <see cref="CyclesViewModel.CurrentGCSeries"/> starts
    /// <see langword="null"/>. The flag is a plain boolean (not derived)
    /// because the parent command writes <c>series.Count &gt; 0</c> explicitly
    /// — an empty scan leaves the export gated off even though the result is
    /// non-null.
    /// </summary>
    [Fact]
    public void HasGCSeries_Should_DefaultToFalse()
    {
        // Arrange / Act
        (_, CyclesViewModel child) = CreateChild();

        // Assert
        child.HasGCSeries.Should().BeFalse();
        child.CurrentGCSeries.Should().BeNull();
    }

    /// <summary>
    /// <see cref="CyclesViewModel.SynodicBodyA"/> and
    /// <see cref="CyclesViewModel.SynodicBodyB"/> default to Jupiter and
    /// Saturn respectively (the classic great-conjunction pair).
    /// </summary>
    [Fact]
    public void SynodicBodies_Should_DefaultToJupiterAndSaturn()
    {
        // Arrange / Act
        (_, CyclesViewModel child) = CreateChild();

        // Assert
        child.SynodicBodyA.Should().Be(CelestialBodyId.Jupiter);
        child.SynodicBodyB.Should().Be(CelestialBodyId.Saturn);
    }

    /// <summary>
    /// <see cref="CyclesViewModel.SynodicStartYear"/> and
    /// <see cref="CyclesViewModel.SynodicEndYear"/> default to <c>2000</c> and
    /// <c>2100</c>, matching the pre-extraction
    /// <see cref="MainViewModel"/> field initialisers verbatim.
    /// </summary>
    [Fact]
    public void SynodicYears_Should_DefaultTo2000And2100()
    {
        // Arrange / Act
        (_, CyclesViewModel child) = CreateChild();

        // Assert
        child.SynodicStartYear.Should().Be(2000);
        child.SynodicEndYear.Should().Be(2100);
    }

    /// <summary>
    /// <see cref="CyclesViewModel.HasSynodicCycleSeries"/> defaults to
    /// <see langword="false"/> and
    /// <see cref="CyclesViewModel.CurrentSynodicCycleSeries"/> starts
    /// <see langword="null"/>. Same rationale as
    /// <see cref="CyclesViewModel.HasGCSeries"/>.
    /// </summary>
    [Fact]
    public void HasSynodicCycleSeries_Should_DefaultToFalse()
    {
        // Arrange / Act
        (_, CyclesViewModel child) = CreateChild();

        // Assert
        child.HasSynodicCycleSeries.Should().BeFalse();
        child.CurrentSynodicCycleSeries.Should().BeNull();
    }

    /// <summary>
    /// <see cref="CyclesViewModel.SynodicBodyOptions"/> lists the ten
    /// traditional bodies (Sun through Pluto) in the same order as the
    /// pre-extraction <see cref="MainViewModel"/> initialiser. The True Node
    /// and Chiron are omitted because synodic-cycle analysis is defined for
    /// planet pairs only.
    /// </summary>
    [Fact]
    public void SynodicBodyOptions_Should_ListTenBodiesInOrder()
    {
        // Arrange / Act
        (_, CyclesViewModel child) = CreateChild();

        // Assert
        child.SynodicBodyOptions.Should().HaveCount(10);
        child.SynodicBodyOptions.Should().ContainInOrder(
            CelestialBodyId.Sun,
            CelestialBodyId.Moon,
            CelestialBodyId.Mercury,
            CelestialBodyId.Venus,
            CelestialBodyId.Mars,
            CelestialBodyId.Jupiter,
            CelestialBodyId.Saturn,
            CelestialBodyId.Uranus,
            CelestialBodyId.Neptune,
            CelestialBodyId.Pluto);
    }

    #endregion

    #region Setter Plumbing — non-CanExecute input fields

    /// <summary>
    /// Setting <see cref="CyclesViewModel.IncludeCycleSnapshots"/> notifies
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> but does NOT fire
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> — no
    /// command's <c>CanExecute</c> depends on the snapshot toggle.
    /// </summary>
    [Fact]
    public void IncludeCycleSnapshots_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, CyclesViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.IncludeCycleSnapshots = true;

        // Assert
        changed.Should().Contain(nameof(CyclesViewModel.IncludeCycleSnapshots));
        refreshCount.Should().Be(0,
            "no command's CanExecute depends on the snapshot toggle");
    }

    /// <summary>
    /// Setting <see cref="CyclesViewModel.GCStartYear"/> notifies without
    /// requesting a refresh — the calculate command reads it lazily.
    /// </summary>
    [Fact]
    public void GCStartYear_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, CyclesViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.GCStartYear = 1900;

        // Assert
        changed.Should().Contain(nameof(CyclesViewModel.GCStartYear));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// Setting <see cref="CyclesViewModel.GCEndYear"/> notifies without
    /// requesting a refresh.
    /// </summary>
    [Fact]
    public void GCEndYear_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, CyclesViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.GCEndYear = 2050;

        // Assert
        changed.Should().Contain(nameof(CyclesViewModel.GCEndYear));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// Setting <see cref="CyclesViewModel.SynodicBodyA"/> notifies without
    /// requesting a refresh.
    /// </summary>
    [Fact]
    public void SynodicBodyA_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, CyclesViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.SynodicBodyA = CelestialBodyId.Mars;

        // Assert
        changed.Should().Contain(nameof(CyclesViewModel.SynodicBodyA));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// Setting <see cref="CyclesViewModel.SynodicBodyB"/> notifies without
    /// requesting a refresh.
    /// </summary>
    [Fact]
    public void SynodicBodyB_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, CyclesViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.SynodicBodyB = CelestialBodyId.Uranus;

        // Assert
        changed.Should().Contain(nameof(CyclesViewModel.SynodicBodyB));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// Setting <see cref="CyclesViewModel.SynodicStartYear"/> notifies without
    /// requesting a refresh.
    /// </summary>
    [Fact]
    public void SynodicStartYear_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, CyclesViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.SynodicStartYear = 1950;

        // Assert
        changed.Should().Contain(nameof(CyclesViewModel.SynodicStartYear));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// Setting <see cref="CyclesViewModel.SynodicEndYear"/> notifies without
    /// requesting a refresh.
    /// </summary>
    [Fact]
    public void SynodicEndYear_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, CyclesViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.SynodicEndYear = 2150;

        // Assert
        changed.Should().Contain(nameof(CyclesViewModel.SynodicEndYear));
        refreshCount.Should().Be(0);
    }

    #endregion

    #region Result-setter Plumbing — multi-result HasXxx derivation & refresh

    /// <summary>
    /// Setting <see cref="CyclesViewModel.CurrentCycleTimeline"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for both
    /// <c>CurrentCycleTimeline</c> and the derived
    /// <see cref="CyclesViewModel.HasCycleTimeline"/>, then fire exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent re-evaluates cycle-timeline command states.
    /// </summary>
    [Fact]
    public void CurrentCycleTimeline_SetToNewValue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, CyclesViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.CurrentCycleTimeline = MakeCycleTimeline();

        // Assert
        changed.Should().Contain(nameof(CyclesViewModel.CurrentCycleTimeline));
        changed.Should().Contain(nameof(CyclesViewModel.HasCycleTimeline),
            "HasCycleTimeline is derived from CurrentCycleTimeline");
        refreshCount.Should().Be(1,
            "the parent's cycle timeline refresher must fire exactly once");
        child.HasCycleTimeline.Should().BeTrue();
    }

    /// <summary>
    /// Setting <see cref="CyclesViewModel.CurrentGCSeries"/> notifies
    /// PropertyChanged for the result but does NOT flip
    /// <see cref="CyclesViewModel.HasGCSeries"/> on its own and does NOT fire
    /// a refresh request. This preserves the pre-extraction semantics in which
    /// the parent command writes the flag explicitly after the result so an
    /// empty scan (zero conjunctions, non-null series) leaves the export gated
    /// off.
    /// </summary>
    [Fact]
    public void CurrentGCSeries_SetToNewValue_NotifiesButDoesNotFlipFlagOrRefreshCommands()
    {
        // Arrange
        (_, CyclesViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.CurrentGCSeries = MakeGCSeries(count: 0);

        // Assert
        changed.Should().Contain(nameof(CyclesViewModel.CurrentGCSeries));
        changed.Should().NotContain(nameof(CyclesViewModel.HasGCSeries),
            "the result setter must not raise HasGCSeries — parent writes it " +
            "explicitly so empty scans stay gated off");
        refreshCount.Should().Be(0);
        child.HasGCSeries.Should().BeFalse(
            "HasGCSeries stays false until the parent writes it explicitly");
    }

    /// <summary>
    /// Setting <see cref="CyclesViewModel.HasGCSeries"/> explicitly fires
    /// PropertyChanged for itself and fires exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent re-evaluates the GC export command state.
    /// </summary>
    [Fact]
    public void HasGCSeries_SetToTrue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, CyclesViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.HasGCSeries = true;

        // Assert
        changed.Should().Contain(nameof(CyclesViewModel.HasGCSeries));
        refreshCount.Should().Be(1,
            "HasGCSeries gates the export command, so its setter must " +
            "trigger exactly one refresh");
        child.HasGCSeries.Should().BeTrue();
    }

    /// <summary>
    /// Setting <see cref="CyclesViewModel.CurrentSynodicCycleSeries"/>
    /// notifies PropertyChanged for the result but does NOT flip
    /// <see cref="CyclesViewModel.HasSynodicCycleSeries"/> or fire a refresh —
    /// same semantics as <see cref="CyclesViewModel.CurrentGCSeries"/>.
    /// </summary>
    [Fact]
    public void CurrentSynodicCycleSeries_SetToNewValue_NotifiesButDoesNotFlipFlagOrRefreshCommands()
    {
        // Arrange
        (_, CyclesViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.CurrentSynodicCycleSeries = MakeSynodicSeries(count: 0);

        // Assert
        changed.Should().Contain(nameof(CyclesViewModel.CurrentSynodicCycleSeries));
        changed.Should().NotContain(nameof(CyclesViewModel.HasSynodicCycleSeries),
            "the result setter must not raise HasSynodicCycleSeries — parent " +
            "writes it explicitly so empty scans stay gated off");
        refreshCount.Should().Be(0);
        child.HasSynodicCycleSeries.Should().BeFalse();
    }

    /// <summary>
    /// Setting <see cref="CyclesViewModel.HasSynodicCycleSeries"/> explicitly
    /// raises PropertyChanged and signals exactly one refresh request.
    /// </summary>
    [Fact]
    public void HasSynodicCycleSeries_SetToTrue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, CyclesViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.HasSynodicCycleSeries = true;

        // Assert
        changed.Should().Contain(nameof(CyclesViewModel.HasSynodicCycleSeries));
        refreshCount.Should().Be(1);
        child.HasSynodicCycleSeries.Should().BeTrue();
    }

    #endregion

    #region Clear Semantics

    /// <summary>
    /// <see cref="CyclesViewModel.Clear"/> must reset every mutable field
    /// (cycle timeline / great conjunction / synodic cycle inputs and
    /// results) and emit exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> via the
    /// field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c>. If Clear used setters the
    /// cycle-timeline result plus the two <c>HasX</c> booleans would each
    /// cascade their own refresh — three refresh pulses per reset rather than
    /// one.
    /// </summary>
    [Fact]
    public void Clear_ResetsAllFieldsAndFiresRefreshOnce()
    {
        // Arrange — seed every mutable field with a non-default value
        (_, CyclesViewModel child) = CreateChild();

        child.IncludeCycleSnapshots = true;
        child.CurrentCycleTimeline = MakeCycleTimeline();

        child.GCStartYear = 1900;
        child.GCEndYear = 2050;
        child.CurrentGCSeries = MakeGCSeries(count: 5);
        child.HasGCSeries = true;

        child.SynodicBodyA = CelestialBodyId.Mars;
        child.SynodicBodyB = CelestialBodyId.Venus;
        child.SynodicStartYear = 1950;
        child.SynodicEndYear = 2150;
        child.CurrentSynodicCycleSeries = MakeSynodicSeries(count: 3);
        child.HasSynodicCycleSeries = true;

        int refreshCount = 0;
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.Clear();

        // Assert — cycle timeline
        child.IncludeCycleSnapshots.Should().BeFalse(
            "Clear must restore IncludeCycleSnapshots to false");
        child.CurrentCycleTimeline.Should().BeNull();
        child.HasCycleTimeline.Should().BeFalse();

        // Assert — great conjunctions
        child.GCStartYear.Should().Be(1800,
            "Clear must restore GCStartYear to 1800");
        child.GCEndYear.Should().Be(2100);
        child.CurrentGCSeries.Should().BeNull();
        child.HasGCSeries.Should().BeFalse();

        // Assert — synodic cycles
        child.SynodicBodyA.Should().Be(CelestialBodyId.Jupiter,
            "Clear must restore SynodicBodyA to Jupiter");
        child.SynodicBodyB.Should().Be(CelestialBodyId.Saturn);
        child.SynodicStartYear.Should().Be(2000);
        child.SynodicEndYear.Should().Be(2100);
        child.CurrentSynodicCycleSeries.Should().BeNull();
        child.HasSynodicCycleSeries.Should().BeFalse();

        // Assert — single refresh pulse (field-bypass pattern)
        refreshCount.Should().Be(1,
            "Clear uses the field-bypass pattern so exactly one refresh " +
            "fires at the end rather than cascading through each setter");
    }

    #endregion

    #region Composition Invariants

    /// <summary>
    /// <see cref="MainViewModel.Cycles"/> must be a concrete instance of
    /// <see cref="CyclesViewModel"/> and must be the same reference each time
    /// it is accessed — composition is eager and the property getter must
    /// not construct a fresh child per call.
    /// </summary>
    [Fact]
    public void Parent_ExposesStableCyclesChild()
    {
        // Arrange
        (MainViewModel parent, CyclesViewModel child) = CreateChild();

        // Assert
        child.Should().NotBeNull();
        parent.Cycles.Should().BeSameAs(child,
            "Cycles is an eagerly-composed child; the getter must not " +
            "produce a new instance on every access");
    }

    #endregion
}
