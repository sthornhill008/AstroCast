using System.ComponentModel;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="TransitsViewModel"/> — the Transits panel child
/// introduced in Session H-1.5. Verifies that the simple input setters
/// (<c>StartDate</c>, <c>EndDate</c>, <c>IncludeMinorAspects</c>,
/// <c>InterpretationContext</c>) raise
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> without signalling
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> (they gate
/// no command's <c>CanExecute</c>); that the four technique setters
/// (<c>IncludeTransits</c>, <c>IncludeSolarArcs</c>,
/// <c>IncludeSecondaryProgressions</c>, <c>IncludePrimaryDirections</c>) signal one refresh each, since
/// Calculate needs at least one checked; that the <c>TransitResult</c> setter
/// raises <c>PropertyChanged</c> for both <c>TransitResult</c> and the
/// derived <c>HasResult</c> and signals a single refresh request; that
/// <see cref="TransitsViewModel.Clear"/> resets every field via the
/// field-bypass pattern with exactly one terminal refresh pulse; and that
/// <see cref="MainViewModel.Transits"/> is a stable, eagerly-composed child.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransitsViewModelTests
{
    #region Test Infrastructure

    /// <summary>
    /// Constructs a <see cref="MainViewModel"/> using the same stub set as
    /// <see cref="MainViewModelTests"/> and returns its
    /// <see cref="MainViewModel.Transits"/> child. We construct the parent
    /// rather than the child in isolation because the child derives from
    /// <see cref="ChildViewModelBase"/>, whose constructor requires a real
    /// <see cref="MainViewModel"/> parent.
    /// </summary>
    private static (MainViewModel Parent, TransitsViewModel Child) CreateChild()
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
        return (parent, parent.Transits);
    }

    /// <summary>
    /// Helper that fabricates a minimally-populated <see cref="ChartResult"/>
    /// suitable for the <see cref="TransitResult.NatalChart"/> required init
    /// property. Values are arbitrary but valid — tests only care that the
    /// reference round-trips through the <c>TransitResult</c> setter.
    /// </summary>
    private static ChartResult MakeChartResult() => new()
    {
        Input = new ChartInput
        {
            Name = "Stub",
            LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Unspecified),
            TimeZone = "UTC",
            Latitude = 51.5,
            Longitude = -0.1,
            LocationName = "London",
        },
        JulianDay = 2451545.0,
        Obliquity = 23.4367,
        UtcDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
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
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// Helper that fabricates a minimally-populated <see cref="TransitResult"/>
    /// so the <c>TransitResult</c> setter can be driven without spinning up
    /// the real transit engine.
    /// </summary>
    private static TransitResult MakeTransitResult() => new()
    {
        NatalChart = MakeChartResult(),
        ReportStartDate = new DateOnly(2026, 1, 1),
        ReportEndDate = new DateOnly(2026, 2, 1),
        MidpointDate = new DateOnly(2026, 1, 15),
        IncludeMinorAspects = false,
        TransitPositions = new Dictionary<TransitBody, TransitPlanetPosition>(),
        Stations = [],
        Ingresses = [],
        ActiveTransits = [],
        HouseOverlays = [],
        MidpointActivations = [],
        MarsIncluded = false,
    };

    #endregion

    #region Setter Plumbing

    /// <summary>
    /// Setting <see cref="TransitsViewModel.StartDate"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for "StartDate"
    /// but must NOT fire
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>. No
    /// command's <c>CanExecute</c> gates on the date range; the date is read
    /// lazily when <c>CalculateTransitCommand</c> executes.
    /// </summary>
    [Fact]
    public void StartDate_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, TransitsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.StartDate = new DateTime(2030, 6, 15);

        // Assert
        changed.Should().Contain("StartDate",
            "the setter must notify DatePicker bindings");
        refreshCount.Should().Be(0,
            "no command's CanExecute depends on the date range — the " +
            "three export commands gate on HasResult only");
    }

    /// <summary>
    /// Setting <see cref="TransitsViewModel.EndDate"/> mirrors
    /// <see cref="StartDate_SetToNewValue_NotifiesButDoesNotRefreshCommands"/> —
    /// notifies bindings without requesting a refresh. Verified separately so
    /// a regression on either setter would be caught in isolation.
    /// </summary>
    [Fact]
    public void EndDate_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, TransitsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.EndDate = new DateTime(2030, 12, 31);

        // Assert
        changed.Should().Contain("EndDate");
        refreshCount.Should().Be(0,
            "no command's CanExecute depends on the date range");
    }

    /// <summary>
    /// Setting <see cref="TransitsViewModel.TransitResult"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for both
    /// "TransitResult" and the derived "HasResult", and fire exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent's three transit-export commands re-evaluate
    /// <c>CanExecute</c>.
    /// </summary>
    [Fact]
    public void TransitResult_SetToNewValue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, TransitsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.TransitResult = MakeTransitResult();

        // Assert
        changed.Should().Contain("TransitResult",
            "bindings on TransitResult must update");
        changed.Should().Contain("HasResult",
            "HasResult is derived from TransitResult and must notify");
        refreshCount.Should().Be(1,
            "ExportTransitMarkdown, ExportDailyHoroscopeMarkdown, and " +
            "ExportTransitTimelineMarkdown all depend on HasResult");
        child.HasResult.Should().BeTrue();
    }

    /// <summary>
    /// Setting a technique raises <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// for it and requests exactly one command refresh, since Calculate needs at
    /// least one of <see cref="TransitsViewModel.IncludeTransits"/>,
    /// <see cref="TransitsViewModel.IncludeSolarArcs"/>,
    /// <see cref="TransitsViewModel.IncludeSecondaryProgressions"/> and
    /// <see cref="TransitsViewModel.IncludePrimaryDirections"/> checked
    /// (1.4.0 item 2, branch ModesPanel; scoping C1b; the fourth since branch PrimaryPanel, 1.5.0).
    /// </summary>
    /// <param name="property">The technique's property name.</param>
    [Theory]
    [InlineData(nameof(TransitsViewModel.IncludeTransits))]
    [InlineData(nameof(TransitsViewModel.IncludeSolarArcs))]
    [InlineData(nameof(TransitsViewModel.IncludeSecondaryProgressions))]
    [InlineData(nameof(TransitsViewModel.IncludePrimaryDirections))]
    public void Technique_Set_NotifiesAndRequestsRefresh(string property)
    {
        // Arrange
        (_, TransitsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act: each technique is checked by default, so false is a change
        switch (property)
        {
            case nameof(TransitsViewModel.IncludeTransits):
                child.IncludeTransits = false;
                break;
            case nameof(TransitsViewModel.IncludeSolarArcs):
                child.IncludeSolarArcs = false;
                break;
            case nameof(TransitsViewModel.IncludeSecondaryProgressions):
                child.IncludeSecondaryProgressions = false;
                break;
            case nameof(TransitsViewModel.IncludePrimaryDirections):
                child.IncludePrimaryDirections = false;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, "Not a technique.");
        }

        // Assert
        changed.Should().Contain(property, "the setter must notify its checkbox binding");
        refreshCount.Should().Be(1,
            "Calculate's CanExecute reads the four techniques, so each change asks the parent to re-evaluate it");
    }

    #endregion

    #region Clear Semantics

    /// <summary>
    /// <see cref="TransitsViewModel.Clear"/> must reset every mutable field
    /// (StartDate, EndDate, IncludeMinorAspects, the four techniques
    /// IncludeTransits, IncludeSolarArcs, IncludeSecondaryProgressions and
    /// IncludePrimaryDirections, InterpretationContext, TransitResult) and emit exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> via the
    /// field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c>. If Clear used setters each
    /// reset would cascade its own refresh — and with nine fields plus the
    /// derived-HasResult notification the parent would see multiple
    /// command-refresh pulses per reset.
    /// </summary>
    [Fact]
    public void Clear_ResetsAllFieldsAndFiresRefreshOnce()
    {
        // Arrange — seed every mutable field with a non-default value
        (_, TransitsViewModel child) = CreateChild();
        child.StartDate = new DateTime(2020, 1, 1);
        child.EndDate = new DateTime(2020, 12, 31);
        child.IncludeMinorAspects = true;
        child.IncludeTransits = false;
        child.IncludeSolarArcs = false;
        child.IncludeSecondaryProgressions = false;
        child.IncludePrimaryDirections = false;
        child.InterpretationContext = "seeded";
        child.TransitResult = MakeTransitResult();

        int refreshCount = 0;
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.Clear();

        // Assert — state
        child.StartDate.Should().Be(DateTime.Today,
            "Clear must restore StartDate to DateTime.Today (the field " +
            "initialiser default)");
        child.EndDate.Should().Be(DateTime.Today.AddMonths(1),
            "Clear must restore EndDate to DateTime.Today + 1 month (the " +
            "field initialiser default)");
        child.IncludeMinorAspects.Should().BeFalse();
        child.IncludeTransits.Should().BeTrue("every technique is checked by default");
        child.IncludeSolarArcs.Should().BeTrue("every technique is checked by default");
        child.IncludeSecondaryProgressions.Should().BeTrue("every technique is checked by default");
        child.IncludePrimaryDirections.Should().BeTrue("every technique is checked by default");
        child.InterpretationContext.Should().BeEmpty();
        child.TransitResult.Should().BeNull();
        child.HasResult.Should().BeFalse();

        // Assert — single refresh pulse (field-bypass pattern)
        refreshCount.Should().Be(1,
            "Clear uses the field-bypass pattern so exactly one refresh " +
            "fires at the end rather than cascading through each setter");
    }

    #endregion

    #region Composition Invariants

    /// <summary>
    /// <see cref="MainViewModel.Transits"/> must be a concrete instance of
    /// <see cref="TransitsViewModel"/> and must be the same reference each
    /// time it is accessed — composition is eager and the property getter
    /// must not construct a fresh child per call.
    /// </summary>
    [Fact]
    public void Parent_ExposesStableTransitsChild()
    {
        // Arrange
        (MainViewModel parent, TransitsViewModel child) = CreateChild();

        // Assert
        child.Should().NotBeNull();
        parent.Transits.Should().BeSameAs(child,
            "Transits is an eagerly-composed child; the getter must not " +
            "produce a new instance on every access");
    }

    #endregion
}
