using System.ComponentModel;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="ElectionalViewModel"/> — the Electional panel
/// child introduced in Session H-1.6 and the first H-1 child to compose a
/// nested <see cref="LocationSearchViewModel"/> under its umbrella rather
/// than as a parent-owned sibling. Verifies that the command-gating setters
/// (<c>Goal</c>, <c>LocationName</c>, <c>SelectedNatalChart</c>,
/// <c>ElectionalResult</c>) raise
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> and fire exactly
/// one <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>; that
/// the non-gating setters (<c>StartDate</c>, <c>EndDate</c>) notify without
/// requesting a refresh; that <see cref="ElectionalViewModel.Clear"/>
/// resets every field plus the nested <see cref="LocationSearchViewModel"/>
/// via the field-bypass pattern with exactly one terminal refresh pulse;
/// that <see cref="MainViewModel.Electional"/> is a stable, eagerly-composed
/// child with a stable nested <c>LocationSearch</c>; and that the
/// <c>OnLocationSearchPropertyChanged</c> follow-on handler (moved under
/// H-1.6's Rule #4 refinement) copies the selected location's coordinates
/// and display name into the child's own form fields while still writing
/// the parent-owned <see cref="MainViewModel.StatusText"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ElectionalViewModelTests
{
    #region Test Infrastructure

    /// <summary>
    /// Constructs a <see cref="MainViewModel"/> using the same stub set as
    /// <see cref="MainViewModelTests"/> and returns its
    /// <see cref="MainViewModel.Electional"/> child. We construct the parent
    /// rather than the child in isolation because
    /// <see cref="ElectionalViewModel"/> derives from
    /// <see cref="ChildViewModelBase"/>, whose constructor requires a real
    /// <see cref="MainViewModel"/> parent, and the nested
    /// <see cref="LocationSearchViewModel"/> expects the parent's shared
    /// <c>HttpClient</c> and <c>NominatimRateLimiter</c> to be wired through.
    /// </summary>
    private static (MainViewModel Parent, ElectionalViewModel Child) CreateChild()
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
        return (parent, parent.Electional);
    }

    /// <summary>
    /// Helper that fabricates a minimally-populated <see cref="ChartResult"/>
    /// suitable for the <see cref="ElectionalResult.NatalChart"/> and
    /// <see cref="ElectionalInput.NatalChart"/> required init properties.
    /// Values are arbitrary but valid — tests only care that the reference
    /// round-trips through the <c>ElectionalResult</c> setter.
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
    /// <see cref="ElectionalResult"/> so the <c>ElectionalResult</c> setter
    /// can be driven without spinning up the real electional engine.
    /// </summary>
    private static ElectionalResult MakeElectionalResult()
    {
        ChartResult natal = MakeChartResult();
        ElectionalInput input = new()
        {
            NatalChart = natal,
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Stub goal",
            RangeStart = new DateOnly(2026, 4, 20),
            RangeEnd = new DateOnly(2026, 4, 25),
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York",
            TimeZone = "America/New_York",
        };
        return new ElectionalResult
        {
            Input = input,
            NatalChart = natal,
            Candidates = [],
            // An empty result models a search that found nothing, so the request
            // stands at whatever the input asked for.
            RequestedCandidateCount = input.CandidateCount,
            SamplesWithoutPlanetaryHour = 0,
            DisqualifiedPeriods = [],
            VoidOfCoursePeriods = [],
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>
    /// Helper that fabricates a minimally-populated <see cref="ChartEntity"/>
    /// so the <c>SelectedNatalChart</c> setter can be driven with a non-null
    /// value.
    /// </summary>
    private static ChartEntity MakeChartEntity() => new()
    {
        Name = "Test Person",
        EventType = "Birth",
        LocalDateTime = "2000-01-01T12:00:00",
        TimeZone = "UTC",
        UtcDateTime = "2000-01-01T12:00:00Z",
        JulianDay = 2451545.0,
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        CreatedAt = DateTime.UtcNow.ToString("o"),
        UpdatedAt = DateTime.UtcNow.ToString("o"),
    };

    #endregion

    #region Setter Plumbing — Command-Gating

    /// <summary>
    /// Setting <see cref="ElectionalViewModel.Goal"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for "Goal" and
    /// fire exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> because
    /// <c>CalculateElectionalCommand.CanExecute</c> gates on
    /// <c>!string.IsNullOrWhiteSpace(Goal)</c>.
    /// </summary>
    [Fact]
    public void Goal_SetToNewValue_NotifiesAndRequestsRefresh()
    {
        // Arrange
        (_, ElectionalViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.Goal = "Launch new product line";

        // Assert
        changed.Should().Contain("Goal",
            "the TextBox two-way binding depends on this notification");
        refreshCount.Should().Be(1,
            "CalculateElectionalCommand gates on Goal being non-empty");
    }

    /// <summary>
    /// Setting <see cref="ElectionalViewModel.TimeZone"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for "TimeZone" and
    /// fire exactly one refresh request, because since 1.4.0
    /// <c>CalculateElectionalCommand.CanExecute</c> gates on a non-blank zone
    /// (port plan §9 item 16). The refresh is the setter's, not its callers':
    /// Use Natal Location sets LocationName, which raises, before the zone.
    /// </summary>
    [Fact]
    public void TimeZone_SetToNewValue_NotifiesAndRequestsRefresh()
    {
        (_, ElectionalViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.TimeZone = "America/New_York";

        changed.Should().Contain("TimeZone");
        refreshCount.Should().Be(1,
            "CalculateElectionalCommand gates on TimeZone being non-blank");
    }

    /// <summary>
    /// Setting <see cref="ElectionalViewModel.LocationName"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for
    /// "LocationName" and fire exactly one refresh request because
    /// <c>CalculateElectionalCommand.CanExecute</c> gates on
    /// <c>!string.IsNullOrWhiteSpace(LocationName)</c> and
    /// <c>UseNatalLocationCommand.CanExecute</c> reads the parent's
    /// <c>ChartEntry.LocationName</c> OR <c>Electional.SelectedNatalChart</c>.
    /// </summary>
    [Fact]
    public void LocationName_SetToNewValue_NotifiesAndRequestsRefresh()
    {
        // Arrange
        (_, ElectionalViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.LocationName = "New York, NY";

        // Assert
        changed.Should().Contain("LocationName");
        refreshCount.Should().Be(1,
            "CalculateElectionalCommand gates on LocationName being non-empty");
    }

    /// <summary>
    /// Setting <see cref="ElectionalViewModel.SelectedNatalChart"/> must
    /// raise <see cref="INotifyPropertyChanged.PropertyChanged"/> and fire
    /// exactly one refresh request because both
    /// <c>CalculateElectionalCommand.CanExecute</c> and
    /// <c>UseNatalLocationCommand.CanExecute</c> read this value.
    /// </summary>
    [Fact]
    public void SelectedNatalChart_SetToNewValue_NotifiesAndRequestsRefresh()
    {
        // Arrange
        (_, ElectionalViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.SelectedNatalChart = MakeChartEntity();

        // Assert
        changed.Should().Contain("SelectedNatalChart",
            "the ComboBox SelectedItem binding depends on this notification");
        refreshCount.Should().Be(1,
            "CalculateElectional and UseNatalLocation both gate on this");
    }

    /// <summary>
    /// Setting <see cref="ElectionalViewModel.ElectionalResult"/> must
    /// raise <see cref="INotifyPropertyChanged.PropertyChanged"/> for both
    /// "ElectionalResult" and the derived "HasResult", and fire exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent's <c>ExportElectionalMarkdownCommand</c> re-evaluates
    /// <c>CanExecute</c> (which gates on <c>HasResult</c>).
    /// </summary>
    [Fact]
    public void ElectionalResult_SetToNewValue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, ElectionalViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.ElectionalResult = MakeElectionalResult();

        // Assert
        changed.Should().Contain("ElectionalResult",
            "bindings on ElectionalResult must update");
        changed.Should().Contain("HasResult",
            "HasResult is derived from ElectionalResult and must notify");
        refreshCount.Should().Be(1,
            "ExportElectionalMarkdownCommand depends on HasResult");
        child.HasResult.Should().BeTrue();
    }

    #endregion

    #region Setter Plumbing — Non-Gating

    /// <summary>
    /// Setting <see cref="ElectionalViewModel.StartDate"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for "StartDate"
    /// but must NOT fire
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>. No
    /// command's <c>CanExecute</c> gates on the date range; the dates are
    /// read lazily when <c>CalculateElectionalCommand</c> executes.
    /// </summary>
    [Fact]
    public void StartDate_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, ElectionalViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.StartDate = new DateTime(2030, 6, 15);

        // Assert
        changed.Should().Contain("StartDate",
            "the DatePicker binding depends on this notification");
        refreshCount.Should().Be(0,
            "no command's CanExecute depends on the electional date range");
    }

    /// <summary>
    /// Setting <see cref="ElectionalViewModel.EndDate"/> mirrors
    /// <see cref="StartDate_SetToNewValue_NotifiesButDoesNotRefreshCommands"/>
    /// — notifies bindings without requesting a refresh. Verified
    /// separately so a regression on either setter would be caught in
    /// isolation.
    /// </summary>
    [Fact]
    public void EndDate_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, ElectionalViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.EndDate = new DateTime(2030, 12, 31);

        // Assert
        changed.Should().Contain("EndDate");
        refreshCount.Should().Be(0,
            "no command's CanExecute depends on the electional date range");
    }

    #endregion

    #region Clear Semantics

    /// <summary>
    /// <see cref="ElectionalViewModel.Clear"/> must reset every mutable
    /// field (ElectionType, Goal, StartDate, EndDate, LocationName,
    /// LatitudeText, LongitudeText, TimeZone, IsTimezoneValid,
    /// CandidateCount, AvoidMercuryRetrograde, ConstraintNotes,
    /// EarliestTimeText, LatestTimeText, ElectionalResult) AND clear the
    /// nested <see cref="LocationSearchViewModel"/>, all while emitting
    /// exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> via
    /// the field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c>. If Clear used setters each
    /// reset would cascade its own refresh pulse.
    /// </summary>
    [Fact]
    public void Clear_ResetsAllFieldsAndFiresRefreshOnce()
    {
        // Arrange — seed every mutable field with a non-default value
        (_, ElectionalViewModel child) = CreateChild();
        child.ElectionType = ElectionType.Wedding;
        child.Goal = "seeded goal";
        child.StartDate = new DateTime(2020, 1, 1);
        child.EndDate = new DateTime(2020, 12, 31);
        child.LocationName = "seeded location";
        child.LatitudeText = "37.5407";
        child.LongitudeText = "-77.4360";
        child.TimeZone = "America/New_York";
        child.CandidateCount = 5;
        child.AvoidMercuryRetrograde = false;
        child.ConstraintNotes = "seeded notes";
        child.EarliestTimeText = "09:00";
        child.LatestTimeText = "17:00";
        child.ElectionalResult = MakeElectionalResult();
        child.LocationSearch.SearchText = "seeded search";
        child.LocationSearch.Results.Add(new LocationSearchResult
        {
            DisplayName = "Seeded result",
            Latitude = 0,
            Longitude = 0,
        });
        child.LocationSearch.SelectedResult = child.LocationSearch.Results[0];

        int refreshCount = 0;
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.Clear();

        // Assert — state
        child.ElectionType.Should().Be(ElectionType.BusinessLaunch,
            "Clear must restore ElectionType to the field initialiser default");
        child.Goal.Should().BeEmpty();
        child.StartDate.Should().Be(DateTime.Today.AddDays(1),
            "Clear must restore StartDate to DateTime.Today + 1 day");
        child.EndDate.Should().Be(DateTime.Today.AddMonths(1),
            "Clear must restore EndDate to DateTime.Today + 1 month");
        child.LocationName.Should().BeEmpty();
        child.LatitudeText.Should().Be("0");
        child.LongitudeText.Should().Be("0");
        child.TimeZone.Should().BeEmpty();
        child.IsTimezoneValid.Should().BeTrue(
            "Clear must restore the validity flag since TimeZone is empty");
        child.CandidateCount.Should().Be(3,
            "CandidateCount defaults to 3 per the field initialiser");
        child.AvoidMercuryRetrograde.Should().BeTrue(
            "AvoidMercuryRetrograde defaults to true per the field initialiser");
        child.ConstraintNotes.Should().BeEmpty();
        child.EarliestTimeText.Should().BeEmpty();
        child.LatestTimeText.Should().BeEmpty();
        child.ElectionalResult.Should().BeNull();
        child.HasResult.Should().BeFalse();

        // Assert — nested LocationSearch cleared too (H-1.6 umbrella semantics)
        child.LocationSearch.SearchText.Should().BeEmpty(
            "Clear must propagate into the nested LocationSearch child");
        child.LocationSearch.Results.Should().BeEmpty();
        child.LocationSearch.SelectedResult.Should().BeNull();

        // Assert — single refresh pulse (field-bypass pattern)
        refreshCount.Should().Be(1,
            "Clear uses the field-bypass pattern so exactly one refresh " +
            "fires at the end rather than cascading through each setter");
    }

    #endregion

    #region Composition Invariants

    /// <summary>
    /// <see cref="MainViewModel.Electional"/> must be a concrete instance
    /// of <see cref="ElectionalViewModel"/> and must be the same reference
    /// each time it is accessed — composition is eager and the property
    /// getter must not construct a fresh child per call.
    /// </summary>
    [Fact]
    public void Parent_ExposesStableElectionalChild()
    {
        // Arrange
        (MainViewModel parent, ElectionalViewModel child) = CreateChild();

        // Assert
        child.Should().NotBeNull();
        parent.Electional.Should().BeSameAs(child,
            "Electional is an eagerly-composed child; the getter must not " +
            "produce a new instance on every access");
    }

    /// <summary>
    /// The nested <see cref="ElectionalViewModel.LocationSearch"/> must be
    /// a stable reference across accesses (constructed once in the child's
    /// constructor) and must be distinct from the three other
    /// parent-owned sibling <see cref="LocationSearchViewModel"/>
    /// instances — otherwise state would leak between the Electional
    /// panel's search and the Chart Entry / Mundane / Relocation panels.
    /// </summary>
    [Fact]
    public void Electional_LocationSearchIsStableAndDistinctFromSiblings()
    {
        // Arrange
        (MainViewModel parent, ElectionalViewModel child) = CreateChild();

        // Assert — stable reference
        child.LocationSearch.Should().NotBeNull();
        child.LocationSearch.Should().BeSameAs(child.LocationSearch,
            "the nested LocationSearch is constructed once and reused");

        // Assert — distinct from every remaining parent-owned sibling and
        // from the other nested LocationSearch (H-1.9 Mundane).
        child.LocationSearch.Should().NotBeSameAs(parent.ChartEntryLocationSearch);
        child.LocationSearch.Should().NotBeSameAs(parent.Mundane.LocationSearch);
        child.LocationSearch.Should().NotBeSameAs(parent.Relocation.LocationSearch);
    }

    #endregion

    #region Follow-on Handler (moved per Rule #4 refinement)

    /// <summary>
    /// When the user picks a
    /// <see cref="LocationSearchViewModel.SelectedResult"/> on the nested
    /// electional location search, the follow-on handler migrated under
    /// H-1.6 must copy the coordinates and display name into the child's
    /// own <see cref="ElectionalViewModel.LatitudeText"/>,
    /// <see cref="ElectionalViewModel.LongitudeText"/>, and
    /// <see cref="ElectionalViewModel.LocationName"/> properties, and
    /// still write the parent-owned
    /// <see cref="MainViewModel.StatusText"/> (Rule #2 — status text is a
    /// parent concern).
    /// </summary>
    [Fact]
    public void NestedLocationSearch_SelectingResult_CopiesCoordinatesAndSetsStatus()
    {
        // Arrange
        (MainViewModel parent, ElectionalViewModel child) = CreateChild();
        LocationSearchResult picked = new()
        {
            DisplayName = "Richmond, VA, USA",
            Latitude = 37.5407,
            Longitude = -77.4360,
        };

        // Act — drive the nested child's SelectedResult, which raises the
        // PropertyChanged the child subscribes to
        child.LocationSearch.SelectedResult = picked;

        // Assert — coordinates copied into the electional form fields
        child.LatitudeText.Should().Be(
            picked.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "the handler must copy the latitude into the child's form field");
        child.LongitudeText.Should().Be(
            picked.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture),
            "the handler must copy the longitude into the child's form field");
        child.LocationName.Should().Be(picked.DisplayName,
            "the handler must copy the display name into the child's form field");

        // Assert — parent status text written (Rule #2 last resort)
        parent.StatusText.Should().Be(
            $"Electional location set: {picked.DisplayName}",
            "status text is a parent-owned concern and must still be " +
            "written even though the rest of the handler now targets " +
            "this.* fields under the nested umbrella");
    }

    #endregion
}
