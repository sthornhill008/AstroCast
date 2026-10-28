using System.ComponentModel;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ReturnsViewModel"/> — the multi-result Returns
/// panel child introduced in Session H-1.7. Verifies that the simple input
/// setters (<c>SolarReturnYear</c>, <c>SolarReturnInterpretationContext</c>,
/// <c>LunarReturnStartDate</c>, <c>LunarReturnEndDate</c>,
/// <c>LunarReturnInterpretationContext</c>, <c>ReturnNumber</c>,
/// <c>ReturnYear</c>, <c>PlanetaryReturnInterpContext</c>) raise
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> without signalling
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>; that the
/// three result setters (<c>CurrentSolarReturnResult</c>,
/// <c>LunarReturnResults</c>, <c>CurrentPlanetaryReturnResult</c>) raise
/// PropertyChanged for the result, the derived <c>HasXxxResult</c> flag, and
/// signal a single refresh request; that <see cref="ReturnsViewModel.SelectedLunarReturn"/>
/// signals refresh on its own (export gates on it); that
/// <see cref="ReturnsViewModel.SelectedReturnPlanet"/> auto-flips
/// <see cref="ReturnsViewModel.SelectedReturnFindMode"/> per the
/// outer-vs-inner planet convention; that
/// <see cref="ReturnsViewModel.Clear"/> resets every field via the
/// field-bypass pattern with exactly one terminal refresh pulse; and that
/// <see cref="MainViewModel.Returns"/> is a stable, eagerly-composed child.
/// Also covers the four absorbed Category C profection setters whose
/// <c>internal set</c> accessors are reachable thanks to
/// <c>InternalsVisibleTo("AstroCast.Tests")</c>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ReturnsViewModelTests
{
    #region Test Infrastructure

    /// <summary>
    /// Constructs a <see cref="MainViewModel"/> using the same stub set as
    /// <see cref="MainViewModelTests"/> and returns its
    /// <see cref="MainViewModel.Returns"/> child. We construct the parent
    /// rather than the child in isolation because the child derives from
    /// <see cref="ChildViewModelBase"/>, whose constructor requires a real
    /// <see cref="MainViewModel"/> parent.
    /// </summary>
    private static (MainViewModel Parent, ReturnsViewModel Child) CreateChild()
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
        return (parent, parent.Returns);
    }

    /// <summary>
    /// Helper that fabricates a minimally-populated <see cref="ChartResult"/>
    /// suitable as the natal reference inside the three return-result types.
    /// Values are arbitrary but valid — tests only care that the reference
    /// round-trips through the result property setters.
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
    /// Helper that fabricates a minimally-populated
    /// <see cref="SolarReturnResult"/> for setter tests.
    /// </summary>
    private static SolarReturnResult MakeSolarReturnResult() => new()
    {
        NatalChart = MakeChartResult(),
        ReturnChart = MakeChartResult(),
        Year = 2026,
        NatalSunLongitude = 280.0,
        ReturnSunLongitude = 280.0,
        ReturnLocationName = "London",
        ReturnLatitude = 51.5,
        ReturnLongitude = -0.1,
        PrecisionArcSeconds = 0.5,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// Helper that fabricates a single-element list of
    /// <see cref="LunarReturnResult"/> for setter tests.
    /// </summary>
    private static IReadOnlyList<LunarReturnResult> MakeLunarReturnResults() =>
    [
        new LunarReturnResult
        {
            NatalChart = MakeChartResult(),
            ReturnChart = MakeChartResult(),
            SequenceNumber = 1,
            NatalMoonLongitude = 90.0,
            ReturnMoonLongitude = 90.0,
            ReturnLocationName = "London",
            ReturnLatitude = 51.5,
            ReturnLongitude = -0.1,
            PrecisionArcSeconds = 0.5,
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        },
    ];

    /// <summary>
    /// Helper that fabricates a minimally-populated
    /// <see cref="PlanetaryReturnResult"/> for setter tests.
    /// </summary>
    private static PlanetaryReturnResult MakePlanetaryReturnResult() => new()
    {
        NatalChart = MakeChartResult(),
        ReturnChart = MakeChartResult(),
        Planet = CelestialBodyId.Saturn,
        PlanetName = "Saturn",
        NatalLongitude = 120.0,
        ReturnLongitude = 120.0,
        ReturnNumber = 1,
        AgeAtReturn = 29,
        Passes = [new DateTime(2029, 1, 1, 0, 0, 0, DateTimeKind.Utc)],
        ReturnLocationName = "London",
        ReturnLatitude = 51.5,
        ReturnLongitude_Geo = -0.1,
        PrecisionArcSeconds = 0.5,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    #endregion

    #region Defaults

    /// <summary>
    /// <see cref="ReturnsViewModel.SolarReturnYear"/> defaults to
    /// <see cref="DateTime.Today"/>.<see cref="DateTime.Year"/> per the field
    /// initialiser. F-GR-2 watch item: this couples the test to the system
    /// clock; preserved verbatim from the pre-extraction
    /// <see cref="MainViewModel"/> behaviour.
    /// </summary>
    [Fact]
    public void SolarReturnYear_Should_DefaultToCurrentYear()
    {
        // Arrange / Act
        (_, ReturnsViewModel child) = CreateChild();

        // Assert
        child.SolarReturnYear.Should().Be(DateTime.Today.Year,
            "solar return year should default to the current year");
    }

    /// <summary>
    /// <see cref="ReturnsViewModel.HasSolarReturnResult"/> defaults to
    /// <see langword="false"/> because
    /// <see cref="ReturnsViewModel.CurrentSolarReturnResult"/> starts
    /// <see langword="null"/>. The flag is derived from the result reference
    /// — there is no separate backing field.
    /// </summary>
    [Fact]
    public void HasSolarReturnResult_Should_DefaultToFalse()
    {
        // Arrange / Act
        (_, ReturnsViewModel child) = CreateChild();

        // Assert
        child.HasSolarReturnResult.Should().BeFalse(
            "no solar return has been calculated yet");
    }

    /// <summary>
    /// <see cref="ReturnsViewModel.HasLunarReturnResult"/> defaults to
    /// <see langword="false"/> because
    /// <see cref="ReturnsViewModel.LunarReturnResults"/> starts
    /// <see langword="null"/>. Derived from <c>Count &gt; 0</c>.
    /// </summary>
    [Fact]
    public void HasLunarReturnResult_Should_DefaultToFalse()
    {
        // Arrange / Act
        (_, ReturnsViewModel child) = CreateChild();

        // Assert
        child.HasLunarReturnResult.Should().BeFalse(
            "no lunar returns have been calculated yet");
    }

    /// <summary>
    /// <see cref="ReturnsViewModel.LunarReturnResults"/> defaults to
    /// <see langword="null"/> — the user has not run a calculation.
    /// </summary>
    [Fact]
    public void LunarReturnResults_Should_DefaultToNull()
    {
        // Arrange / Act
        (_, ReturnsViewModel child) = CreateChild();

        // Assert
        child.LunarReturnResults.Should().BeNull(
            "no lunar returns have been calculated yet");
    }

    /// <summary>
    /// <see cref="ReturnsViewModel.SelectedLunarReturn"/> defaults to
    /// <see langword="null"/>.
    /// </summary>
    [Fact]
    public void SelectedLunarReturn_Should_DefaultToNull()
    {
        // Arrange / Act
        (_, ReturnsViewModel child) = CreateChild();

        // Assert
        child.SelectedLunarReturn.Should().BeNull(
            "no lunar return has been selected yet");
    }

    /// <summary>
    /// <see cref="ReturnsViewModel.LunarReturnStartDate"/> defaults to
    /// <see cref="DateTime.Today"/> per the field initialiser. F-GR-2 watch
    /// item.
    /// </summary>
    [Fact]
    public void LunarReturnStartDate_Should_DefaultToToday()
    {
        // Arrange / Act
        (_, ReturnsViewModel child) = CreateChild();

        // Assert
        child.LunarReturnStartDate.Date.Should().Be(DateTime.Today,
            "lunar return start date should default to today");
    }

    /// <summary>
    /// <see cref="ReturnsViewModel.LunarReturnEndDate"/> defaults to
    /// <see cref="DateTime.Today"/>.<see cref="DateTime.AddMonths"/>(3) per
    /// the field initialiser. F-GR-2 watch item.
    /// </summary>
    [Fact]
    public void LunarReturnEndDate_Should_DefaultToThreeMonthsFromToday()
    {
        // Arrange / Act
        (_, ReturnsViewModel child) = CreateChild();

        // Assert
        child.LunarReturnEndDate.Date.Should().Be(DateTime.Today.AddMonths(3),
            "lunar return end date should default to 3 months from today");
    }

    /// <summary>
    /// <see cref="ReturnsViewModel.SelectedReturnPlanet"/> defaults to
    /// <see cref="CelestialBodyId.Saturn"/> and the find mode therefore
    /// defaults to <see cref="ReturnFindMode.ByReturnNumber"/> per the
    /// outer-planet convention.
    /// </summary>
    [Fact]
    public void SelectedReturnPlanet_Should_DefaultToSaturn()
    {
        // Arrange / Act
        (_, ReturnsViewModel child) = CreateChild();

        // Assert
        child.SelectedReturnPlanet.Should().Be(CelestialBodyId.Saturn);
        child.SelectedReturnFindMode.Should().Be(ReturnFindMode.ByReturnNumber);
        child.IsReturnByNumber.Should().BeTrue();
        child.IsReturnByYear.Should().BeFalse();
    }

    /// <summary>
    /// <see cref="ReturnsViewModel.HasProfection"/> defaults to
    /// <see langword="false"/> and all three profection summary strings
    /// default to empty before the lifecycle populates them.
    /// </summary>
    [Fact]
    public void Profection_Should_DefaultToEmpty()
    {
        // Arrange / Act
        (_, ReturnsViewModel child) = CreateChild();

        // Assert
        child.HasProfection.Should().BeFalse();
        child.ProfectionSummary.Should().BeEmpty();
        child.FirdariaSummary.Should().BeEmpty();
        child.ZodiacalReleasingSummary.Should().BeEmpty();
    }

    #endregion

    #region Setter Plumbing — non-CanExecute fields

    /// <summary>
    /// Setting <see cref="ReturnsViewModel.SolarReturnYear"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for "SolarReturnYear"
    /// but must NOT fire
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>: no
    /// command's <c>CanExecute</c> gates on the year input — the calculate
    /// command reads it lazily on Execute.
    /// </summary>
    [Fact]
    public void SolarReturnYear_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, ReturnsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.SolarReturnYear = 2030;

        // Assert
        changed.Should().Contain(nameof(ReturnsViewModel.SolarReturnYear),
            "the setter must notify the year TextBox binding");
        refreshCount.Should().Be(0,
            "no command's CanExecute depends on the year input");
    }

    /// <summary>
    /// Setting <see cref="ReturnsViewModel.LunarReturnStartDate"/> notifies
    /// without requesting a refresh — same rationale as
    /// <see cref="SolarReturnYear_SetToNewValue_NotifiesButDoesNotRefreshCommands"/>.
    /// </summary>
    [Fact]
    public void LunarReturnStartDate_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, ReturnsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.LunarReturnStartDate = new DateTime(2030, 6, 15);

        // Assert
        changed.Should().Contain(nameof(ReturnsViewModel.LunarReturnStartDate));
        refreshCount.Should().Be(0,
            "no command's CanExecute depends on the lunar date range");
    }

    /// <summary>
    /// Setting <see cref="ReturnsViewModel.LunarReturnEndDate"/> notifies
    /// without requesting a refresh.
    /// </summary>
    [Fact]
    public void LunarReturnEndDate_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, ReturnsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.LunarReturnEndDate = new DateTime(2030, 12, 31);

        // Assert
        changed.Should().Contain(nameof(ReturnsViewModel.LunarReturnEndDate));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// Setting <see cref="ReturnsViewModel.ReturnNumber"/> notifies without
    /// requesting a refresh — the calculate command reads it lazily.
    /// </summary>
    [Fact]
    public void ReturnNumber_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, ReturnsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.ReturnNumber = 3;

        // Assert
        changed.Should().Contain(nameof(ReturnsViewModel.ReturnNumber));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// Setting <see cref="ReturnsViewModel.ReturnYear"/> notifies without
    /// requesting a refresh.
    /// </summary>
    [Fact]
    public void ReturnYear_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, ReturnsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.ReturnYear = 2050;

        // Assert
        changed.Should().Contain(nameof(ReturnsViewModel.ReturnYear));
        refreshCount.Should().Be(0);
    }

    #endregion

    #region Result-setter Plumbing — multi-result HasXxxResult derivation

    /// <summary>
    /// Setting <see cref="ReturnsViewModel.CurrentSolarReturnResult"/> must
    /// raise <see cref="INotifyPropertyChanged.PropertyChanged"/> for both
    /// <c>CurrentSolarReturnResult</c> and the derived
    /// <see cref="ReturnsViewModel.HasSolarReturnResult"/>, then fire exactly
    /// one <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so
    /// the parent re-evaluates calculate/export solar return command states.
    /// </summary>
    [Fact]
    public void CurrentSolarReturnResult_SetToNewValue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, ReturnsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.CurrentSolarReturnResult = MakeSolarReturnResult();

        // Assert
        changed.Should().Contain(nameof(ReturnsViewModel.CurrentSolarReturnResult));
        changed.Should().Contain(nameof(ReturnsViewModel.HasSolarReturnResult),
            "HasSolarReturnResult is derived from CurrentSolarReturnResult");
        refreshCount.Should().Be(1,
            "the parent's solar return refresher must fire exactly once");
        child.HasSolarReturnResult.Should().BeTrue();
    }

    /// <summary>
    /// Setting <see cref="ReturnsViewModel.LunarReturnResults"/> mirrors the
    /// solar setter: notifies the result, the derived
    /// <see cref="ReturnsViewModel.HasLunarReturnResult"/>, and signals one
    /// refresh request.
    /// </summary>
    [Fact]
    public void LunarReturnResults_SetToNewValue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, ReturnsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.LunarReturnResults = MakeLunarReturnResults();

        // Assert
        changed.Should().Contain(nameof(ReturnsViewModel.LunarReturnResults));
        changed.Should().Contain(nameof(ReturnsViewModel.HasLunarReturnResult),
            "HasLunarReturnResult is derived from LunarReturnResults.Count > 0");
        refreshCount.Should().Be(1);
        child.HasLunarReturnResult.Should().BeTrue();
    }

    /// <summary>
    /// Setting <see cref="ReturnsViewModel.SelectedLunarReturn"/> signals
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> because
    /// the export body bails when the selection is <see langword="null"/>.
    /// Mirrors the pre-extraction setter behaviour verbatim.
    /// </summary>
    [Fact]
    public void SelectedLunarReturn_SetToNewValue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, ReturnsViewModel child) = CreateChild();
        LunarReturnResult selection = MakeLunarReturnResults()[0];
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.SelectedLunarReturn = selection;

        // Assert
        changed.Should().Contain(nameof(ReturnsViewModel.SelectedLunarReturn));
        refreshCount.Should().Be(1,
            "the export command bails when the selection is null, so " +
            "selection changes must trigger a refresh");
    }

    /// <summary>
    /// Setting <see cref="ReturnsViewModel.CurrentPlanetaryReturnResult"/>
    /// raises both the result and the derived
    /// <see cref="ReturnsViewModel.HasPlanetaryReturnResult"/> and signals a
    /// single refresh request.
    /// </summary>
    [Fact]
    public void CurrentPlanetaryReturnResult_SetToNewValue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, ReturnsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.CurrentPlanetaryReturnResult = MakePlanetaryReturnResult();

        // Assert
        changed.Should().Contain(nameof(ReturnsViewModel.CurrentPlanetaryReturnResult));
        changed.Should().Contain(nameof(ReturnsViewModel.HasPlanetaryReturnResult));
        refreshCount.Should().Be(1);
        child.HasPlanetaryReturnResult.Should().BeTrue();
    }

    #endregion

    #region SelectedReturnPlanet cascade

    /// <summary>
    /// Setting <see cref="ReturnsViewModel.SelectedReturnPlanet"/> to an
    /// inner planet (Mars / Venus / Mercury) must auto-flip
    /// <see cref="ReturnsViewModel.SelectedReturnFindMode"/> to
    /// <see cref="ReturnFindMode.ByYear"/> per the outer-vs-inner convention.
    /// </summary>
    [Fact]
    public void SelectedReturnPlanet_SetToMars_FlipsFindModeToByYear()
    {
        // Arrange
        (_, ReturnsViewModel child) = CreateChild();

        // Act
        child.SelectedReturnPlanet = CelestialBodyId.Mars;

        // Assert
        child.SelectedReturnFindMode.Should().Be(ReturnFindMode.ByYear,
            "Mars / Venus / Mercury default to ByYear because their returns " +
            "recur within most calendar years");
        child.IsReturnByYear.Should().BeTrue();
        child.IsReturnByNumber.Should().BeFalse();
    }

    /// <summary>
    /// Setting <see cref="ReturnsViewModel.SelectedReturnPlanet"/> to Jupiter
    /// keeps <see cref="ReturnFindMode.ByReturnNumber"/> active per the
    /// outer-planet convention. Verified separately from the Saturn default
    /// because Jupiter follows the same branch but is a different value.
    /// </summary>
    [Fact]
    public void SelectedReturnPlanet_SetToJupiter_KeepsFindModeByReturnNumber()
    {
        // Arrange
        (_, ReturnsViewModel child) = CreateChild();
        child.SelectedReturnPlanet = CelestialBodyId.Mars; // flip away first

        // Act
        child.SelectedReturnPlanet = CelestialBodyId.Jupiter;

        // Assert
        child.SelectedReturnFindMode.Should().Be(ReturnFindMode.ByReturnNumber,
            "Saturn / Jupiter benefit from numbering across a long lifetime");
        child.IsReturnByNumber.Should().BeTrue();
        child.IsReturnByYear.Should().BeFalse();
    }

    /// <summary>
    /// Setting <see cref="ReturnsViewModel.SelectedReturnFindMode"/> directly
    /// must raise PropertyChanged for the derived
    /// <see cref="ReturnsViewModel.IsReturnByYear"/> and
    /// <see cref="ReturnsViewModel.IsReturnByNumber"/> so XAML show/hide
    /// converters update.
    /// </summary>
    [Fact]
    public void SelectedReturnFindMode_SetDirectly_RaisesDerivedFlags()
    {
        // Arrange
        (_, ReturnsViewModel child) = CreateChild();
        List<string> changed = [];
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        // Act
        child.SelectedReturnFindMode = ReturnFindMode.ByYear;

        // Assert
        changed.Should().Contain(nameof(ReturnsViewModel.SelectedReturnFindMode));
        changed.Should().Contain(nameof(ReturnsViewModel.IsReturnByYear));
        changed.Should().Contain(nameof(ReturnsViewModel.IsReturnByNumber));
    }

    #endregion

    #region Profection (absorbed Category C — internal setters)

    /// <summary>
    /// The four absorbed profection setters
    /// (<see cref="ReturnsViewModel.ProfectionSummary"/>,
    /// <see cref="ReturnsViewModel.HasProfection"/>,
    /// <see cref="ReturnsViewModel.FirdariaSummary"/>,
    /// <see cref="ReturnsViewModel.ZodiacalReleasingSummary"/>) have
    /// <c>internal set</c> accessors reachable from the test project via
    /// <c>InternalsVisibleTo("AstroCast.Tests")</c>. Each setter must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> but must NOT fire
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>: no
    /// command's <c>CanExecute</c> depends on profection state, mirroring the
    /// pre-extraction <see cref="MainViewModel"/> private setters.
    /// </summary>
    [Fact]
    public void ProfectionSetters_Should_NotifyButDoNotRefreshCommands()
    {
        // Arrange
        (_, ReturnsViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.ProfectionSummary = "Year 7 — Lord of Year: Venus";
        child.HasProfection = true;
        child.FirdariaSummary = "Major: Sun, Sub: Moon";
        child.ZodiacalReleasingSummary = "L1 Cancer / L2 Leo";

        // Assert
        changed.Should().Contain(nameof(ReturnsViewModel.ProfectionSummary));
        changed.Should().Contain(nameof(ReturnsViewModel.HasProfection));
        changed.Should().Contain(nameof(ReturnsViewModel.FirdariaSummary));
        changed.Should().Contain(nameof(ReturnsViewModel.ZodiacalReleasingSummary));
        refreshCount.Should().Be(0,
            "no command's CanExecute depends on profection display state");
        child.HasProfection.Should().BeTrue();
        child.ProfectionSummary.Should().Be("Year 7 — Lord of Year: Venus");
    }

    #endregion

    #region Clear Semantics

    /// <summary>
    /// <see cref="ReturnsViewModel.Clear"/> must reset every mutable field
    /// (solar / lunar / planetary inputs and results plus the four absorbed
    /// profection fields) and emit exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> via the
    /// field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c>. If Clear used setters each
    /// reset would cascade its own refresh — and with three result properties
    /// plus a SelectedLunarReturn that all signal individually, the parent
    /// would see four refresh pulses per reset.
    /// </summary>
    [Fact]
    public void Clear_ResetsAllFieldsAndFiresRefreshOnce()
    {
        // Arrange — seed every mutable field with a non-default value
        (_, ReturnsViewModel child) = CreateChild();

        child.SolarReturnYear = 2030;
        child.SolarReturnInterpretationContext = "solar context";
        child.CurrentSolarReturnResult = MakeSolarReturnResult();

        child.LunarReturnStartDate = new DateTime(2020, 1, 1);
        child.LunarReturnEndDate = new DateTime(2020, 12, 31);
        child.LunarReturnInterpretationContext = "lunar context";
        IReadOnlyList<LunarReturnResult> lunars = MakeLunarReturnResults();
        child.LunarReturnResults = lunars;
        child.SelectedLunarReturn = lunars[0];

        // Pick Mars to flip find mode to ByYear via the cascade
        child.SelectedReturnPlanet = CelestialBodyId.Mars;
        child.ReturnNumber = 5;
        child.ReturnYear = 2050;
        child.PlanetaryReturnInterpContext = "planetary context";
        child.CurrentPlanetaryReturnResult = MakePlanetaryReturnResult();

        // Profection (absorbed)
        child.ProfectionSummary = "seeded profection";
        child.HasProfection = true;
        child.FirdariaSummary = "seeded firdaria";
        child.ZodiacalReleasingSummary = "seeded ZR";

        int refreshCount = 0;
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.Clear();

        // Assert — solar
        child.SolarReturnYear.Should().Be(DateTime.Today.Year,
            "Clear must restore SolarReturnYear to DateTime.Today.Year");
        child.SolarReturnInterpretationContext.Should().BeEmpty();
        child.CurrentSolarReturnResult.Should().BeNull();
        child.HasSolarReturnResult.Should().BeFalse();

        // Assert — lunar
        child.LunarReturnStartDate.Should().Be(DateTime.Today,
            "Clear must restore LunarReturnStartDate to DateTime.Today");
        child.LunarReturnEndDate.Should().Be(DateTime.Today.AddMonths(3),
            "Clear must restore LunarReturnEndDate to today + 3 months");
        child.LunarReturnInterpretationContext.Should().BeEmpty();
        child.LunarReturnResults.Should().BeNull();
        child.SelectedLunarReturn.Should().BeNull();
        child.HasLunarReturnResult.Should().BeFalse();

        // Assert — planetary
        child.SelectedReturnPlanet.Should().Be(CelestialBodyId.Saturn,
            "Clear must restore SelectedReturnPlanet to Saturn");
        child.SelectedReturnFindMode.Should().Be(ReturnFindMode.ByReturnNumber,
            "Clear must restore SelectedReturnFindMode to ByReturnNumber");
        child.ReturnNumber.Should().Be(1);
        child.ReturnYear.Should().Be(DateTime.Today.Year);
        child.PlanetaryReturnInterpContext.Should().BeEmpty();
        child.CurrentPlanetaryReturnResult.Should().BeNull();
        child.HasPlanetaryReturnResult.Should().BeFalse();

        // Assert — profection
        child.HasProfection.Should().BeFalse();
        child.ProfectionSummary.Should().BeEmpty();
        child.FirdariaSummary.Should().BeEmpty();
        child.ZodiacalReleasingSummary.Should().BeEmpty();

        // Assert — single refresh pulse (field-bypass pattern)
        refreshCount.Should().Be(1,
            "Clear uses the field-bypass pattern so exactly one refresh " +
            "fires at the end rather than cascading through each setter");
    }

    #endregion

    #region Composition Invariants

    /// <summary>
    /// <see cref="MainViewModel.Returns"/> must be a concrete instance of
    /// <see cref="ReturnsViewModel"/> and must be the same reference each
    /// time it is accessed — composition is eager and the property getter
    /// must not construct a fresh child per call.
    /// </summary>
    [Fact]
    public void Parent_ExposesStableReturnsChild()
    {
        // Arrange
        (MainViewModel parent, ReturnsViewModel child) = CreateChild();

        // Assert
        child.Should().NotBeNull();
        parent.Returns.Should().BeSameAs(child,
            "Returns is an eagerly-composed child; the getter must not " +
            "produce a new instance on every access");
    }

    #endregion
}
