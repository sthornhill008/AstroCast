using System.ComponentModel;
using System.Reflection;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Models.Progressions;
using AstroCast.Core.Models.SolarArcs;
using AstroCast.Core.Models.Transit;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="MainViewModel"/> verifying form validation,
/// property change notifications, command state management, and cancellation.
/// Uses lightweight test doubles instead of the real Swiss Ephemeris.
/// </summary>
/// <remarks>
/// The <c>Stub*</c> service doubles used here were nested inside this class
/// until Phase 4 slice 1.5. They now live in the <c>AstroCast.TestDoubles</c>
/// project so <c>AstroCast.UiTests</c> can reach them across the xunit v2/v3
/// boundary. They kept this namespace, so they still resolve unqualified.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class MainViewModelTests
{
    #region Test Infrastructure

    /// <summary>
    /// Creates a <see cref="MainViewModel"/> with test stubs for all dependencies.
    /// </summary>
    /// <param name="dialogService">The dialog service, else a fresh <see cref="TestDialogService"/>.</param>
    /// <param name="transitService">The transit service, else a <see cref="StubTransitService"/>, which throws.</param>
    /// <param name="primaryDirectionService">
    /// The primary-direction service, else a <see cref="StubPrimaryDirectionService"/>, which throws (branch PrimaryPanel).
    /// </param>
    /// <param name="chartEngine">
    /// The chart engine, else a <see cref="StubChartEngine"/>; a <see cref="RecordingChartEngine"/> records the
    /// inputs the commands build (branch AsteroidsPanel).
    /// </param>
    /// <param name="repository">
    /// The chart repository, else a <see cref="StubChartRepository"/>, which stores nothing and cannot tell a save;
    /// a <see cref="RecordingChartRepository"/> counts the saves and updates (branch AsteroidsPanel).
    /// </param>
    private static MainViewModel CreateViewModel(
        TestDialogService? dialogService = null,
        ITransitService? transitService = null,
        IPrimaryDirectionService? primaryDirectionService = null,
        IChartEngine? chartEngine = null,
        IChartRepository? repository = null,
        IDavisonService? davisonService = null)
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        return new MainViewModel(
            chartEngine ?? new StubChartEngine(),
            repository ?? new StubChartRepository(),
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
            transitService ?? new StubTransitService(),
            new StubElectionalService(),
            new StubAstrocartographyService(),
            dialogService ?? new TestDialogService(),
            new OperationCancellationRegistry(),
            logger,
            primaryDirectionService: primaryDirectionService ?? new StubPrimaryDirectionService(),
            davisonService: davisonService ?? new StubDavisonService());
    }

    #endregion

    #region CanCalculate Tests (1–3)

    /// <summary>
    /// Test 1: CanExecute should return false when Name is empty (default state).
    /// </summary>
    [Fact]
    public void CalculateCommand_Should_BeDisabled_When_NameIsEmpty()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();
        vm.ChartEntry.Name = string.Empty;
        vm.ChartEntry.TimeZone = "UTC";
        vm.ChartEntry.LocationName = "London";

        // Act
        bool canExecute = vm.CalculateAndSaveCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse("CanCalculate requires a non-empty Name");
    }

    /// <summary>
    /// Test 2: CanExecute should return false when TimeZone is empty.
    /// </summary>
    [Fact]
    public void CalculateCommand_Should_BeDisabled_When_TimeZoneIsEmpty()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();
        vm.ChartEntry.Name = "Test Chart";
        vm.ChartEntry.TimeZone = string.Empty;
        vm.ChartEntry.LocationName = "London";

        // Act
        bool canExecute = vm.CalculateAndSaveCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse("CanCalculate requires a non-empty TimeZone");
    }

    /// <summary>
    /// Test 3: CanExecute should return true when all required fields are set.
    /// </summary>
    [Fact]
    public void CalculateCommand_Should_BeEnabled_When_AllRequiredFieldsSet()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();
        vm.ChartEntry.Name = "Test Chart";
        vm.ChartEntry.TimeZone = "UTC";
        vm.ChartEntry.LocationName = "London";

        // Act
        bool canExecute = vm.CalculateAndSaveCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeTrue("all three required fields are populated");
    }

    #endregion

    #region PropertyChanged Tests (4)

    /// <summary>
    /// Test 4: PropertyChanged should fire on the <see cref="MainViewModel.ChartEntry"/>
    /// child when <c>Name</c> is set. The Name property lives on the child
    /// (H-1.2 decomposition); this test confirms the parent continues to
    /// surface the child INPC stream to bindings.
    /// </summary>
    [Fact]
    public void PropertyChanged_Should_Fire_When_NameIsSet()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();
        List<string> changedProperties = [];
        vm.ChartEntry.PropertyChanged += (_, e) => changedProperties.Add(e.PropertyName!);

        // Act
        vm.ChartEntry.Name = "New Chart Name";

        // Assert
        changedProperties.Should().Contain("Name",
            "setting ChartEntry.Name should raise PropertyChanged on the child VM");
    }

    #endregion

    #region CancelCommand Test (5)

    /// <summary>
    /// Test 5: CancelCommand.CanExecute should be false when not calculating.
    /// </summary>
    [Fact]
    public void CancelCommand_Should_BeDisabled_When_NotCalculating()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();

        // Act
        bool canCancel = vm.CancelCommand.CanExecute(null);

        // Assert
        canCancel.Should().BeFalse("CancelCommand should be disabled when IsCalculating is false");
    }

    #endregion

    #region Validation Tests (6–7)

    /// <summary>
    /// Test 6: Validation should reject latitude outside -90 to 90.
    /// StatusText should show an error message without throwing.
    /// </summary>
    [Fact]
    public void CalculateCommand_Should_SetStatusError_When_LatitudeOutOfRange()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();
        vm.ChartEntry.Name = "Test";
        vm.ChartEntry.TimeZone = "UTC";
        vm.ChartEntry.LocationName = "Nowhere";
        vm.ChartEntry.LatitudeText = "91.0";
        vm.ChartEntry.LongitudeText = "0.0";

        // Act — execute the command (validation fails synchronously)
        vm.CalculateAndSaveCommand.Execute(null);

        // Allow the async void wrapper to complete
        // (validation returns before any await, so it completes synchronously)
        Thread.Sleep(50);

        // Assert
        vm.StatusText.Should().Contain("Latitude",
            "validation should produce an error message about latitude");
    }

    /// <summary>
    /// Test 7: Validation should reject names longer than 200 characters.
    /// StatusText should show an error message without throwing.
    /// </summary>
    [Fact]
    public void CalculateCommand_Should_SetStatusError_When_NameTooLong()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();
        vm.ChartEntry.Name = new string('A', 201);
        vm.ChartEntry.TimeZone = "UTC";
        vm.ChartEntry.LocationName = "London";
        vm.ChartEntry.LatitudeText = "51.5";
        vm.ChartEntry.LongitudeText = "-0.12";

        // Act — execute the command (validation fails synchronously)
        vm.CalculateAndSaveCommand.Execute(null);

        // Allow the async void wrapper to complete
        Thread.Sleep(50);

        // Assert
        vm.StatusText.Should().Contain("too long",
            "validation should produce an error message about name length");
    }

    #endregion

    #region Composite Command Tests

    /// <summary>
    /// CompositeCommand should be disabled when there is no chart result.
    /// </summary>
    [Fact]
    public void CompositeCommand_Should_BeDisabled_When_NoResult()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();

        // Act
        bool canExecute = vm.CalculateCompositeCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse("composite requires a natal chart result");
    }

    /// <summary>
    /// HasCompositeResult should default to false.
    /// H-1.4: the property moved to the <see cref="SynastryViewModel"/> child.
    /// </summary>
    [Fact]
    public void HasCompositeResult_Should_DefaultToFalse()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();

        // Assert
        vm.Synastry.HasCompositeResult.Should().BeFalse("no composite has been calculated yet");
    }

    /// <summary>
    /// ExportChartJsonCommand should be disabled when there is no chart result.
    /// </summary>
    [Fact]
    public void ExportChartJsonCommand_Should_BeDisabled_When_NoResult()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();

        // Act
        bool canExecute = vm.ExportChartJsonCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse("the JSON export requires a chart result");
    }

    /// <summary>
    /// ExportCompositeMarkdownCommand should be disabled when there is no composite result.
    /// </summary>
    [Fact]
    public void ExportCompositeCommand_Should_BeDisabled_When_NoResult()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();

        // Act
        bool canExecute = vm.ExportCompositeMarkdownCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse("export requires a composite result");
    }

    /// <summary>
    /// CalculateCompositeCommand should be enabled when HasResult is true and
    /// PersonB is not null — the two conditions in its CanExecute predicate.
    /// Uses reflection to set the private HasResult property. H-1.4: Person B
    /// selection moved to the <see cref="SynastryViewModel"/> child, so the
    /// test writes through <see cref="MainViewModel.Synastry"/>.
    /// </summary>
    [Fact]
    public void CompositeCommand_Should_BeEnabled_When_HasResult_And_PersonBSelected()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();

        // Set HasResult = true via reflection (private setter)
        typeof(MainViewModel)
            .GetProperty(nameof(MainViewModel.HasResult))!
            .SetValue(vm, true);

        vm.Synastry.PersonB = new ChartEntity
        {
            Name = "Person B",
            EventType = "Birth",
            LocalDateTime = "2000-01-01T12:00:00",
            TimeZone = "UTC",
            UtcDateTime = "2000-01-01T12:00:00Z",
            JulianDay = 2451545.0,
            Latitude = 51.5,
            Longitude = -0.1,
            LocationName = "London",
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o"),
        };

        // Act
        bool canExecute = vm.CalculateCompositeCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeTrue("both HasResult and PersonB are satisfied");
    }

    #endregion

    #region Solar Return Command Tests

    /// <summary>
    /// SolarReturnCommand should be disabled when there is no chart result.
    /// The Returns child's <see cref="ReturnsViewModel.SolarReturnYear"/>
    /// default and the <see cref="ReturnsViewModel.HasSolarReturnResult"/>
    /// derived flag are now covered by
    /// <c>ReturnsViewModelTests</c>; only the parent-owned command's
    /// CanExecute predicate is exercised here.
    /// </summary>
    [Fact]
    public void SolarReturnCommand_Should_BeDisabled_When_NoResult()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();

        // Act
        bool canExecute = vm.CalculateSolarReturnCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse("solar return requires a natal chart result");
    }

    #endregion

    #region Lunar Return Command Tests

    /// <summary>
    /// LunarReturnCommand should be disabled when there is no chart result.
    /// Per-field defaults moved to <c>ReturnsViewModelTests</c> in H-1.7;
    /// only command-level routing tests remain on the parent.
    /// </summary>
    [Fact]
    public void LunarReturnCommand_Should_BeDisabled_When_NoResult()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();

        // Act
        bool canExecute = vm.CalculateLunarReturnCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse("lunar return requires a natal chart result");
    }

    /// <summary>
    /// ExportLunarReturnCommand should be disabled when there is no lunar
    /// return result. <see cref="ReturnsViewModel.HasLunarReturnResult"/> /
    /// <see cref="ReturnsViewModel.SelectedLunarReturn"/> default tests live
    /// in <c>ReturnsViewModelTests</c>; this asserts the parent's command
    /// gating against an empty results list.
    /// </summary>
    [Fact]
    public void ExportLunarReturnCommand_Should_BeDisabled_When_NoResult()
    {
        // Arrange
        MainViewModel vm = CreateViewModel();

        // Act
        bool canExecute = vm.ExportLunarReturnMarkdownCommand.CanExecute(null);

        // Assert
        canExecute.Should().BeFalse("export requires a lunar return result");
    }

    #endregion

    #region Daily Horoscope Command Tests

    /// <summary>
    /// ExportDailyHoroscopeMarkdownCommand should not be null after construction.
    /// </summary>
    [Fact]
    public void ExportDailyHoroscopeCommand_Exists()
    {
        MainViewModel vm = CreateViewModel();

        vm.ExportDailyHoroscopeMarkdownCommand.Should().NotBeNull(
            "the command should be initialised in the constructor");
    }

    /// <summary>
    /// ExportDailyHoroscopeMarkdownCommand should be disabled when there is no transit result.
    /// </summary>
    [Fact]
    public void ExportDailyHoroscopeCommand_CannotExecute_WithoutTransitResult()
    {
        MainViewModel vm = CreateViewModel();

        bool canExecute = vm.ExportDailyHoroscopeMarkdownCommand.CanExecute(null);

        canExecute.Should().BeFalse("export requires a transit result");
    }

    #endregion

    #region Transit Technique Tests

    /// <summary>
    /// Sets a property only the view model's own commands write, as a Load or a Calculate &amp; Save would.
    /// </summary>
    /// <param name="vm">The view model.</param>
    /// <param name="name">The property's name.</param>
    /// <param name="value">The value.</param>
    private static void SetPrivately(MainViewModel vm, string name, object? value)
    {
        PropertyInfo property = typeof(MainViewModel).GetProperty(name)
            ?? throw new InvalidOperationException($"MainViewModel has no {name} property.");
        property.SetValue(vm, value);
    }

    /// <summary>A natal chart from the stub engine.</summary>
    /// <returns>The chart.</returns>
    private static ChartResult NatalChart() => new StubChartEngine().Calculate(new ChartInput
    {
        Name = "Test Person",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
    });

    /// <summary>
    /// A transit result holding the techniques given: the transit lists empty, a solar arc snapshot
    /// with two hits and a progression snapshot with one hit when asked for.
    /// </summary>
    /// <param name="transits">Whether the transit scan ran.</param>
    /// <param name="solarArcs">Whether the solar arcs are in.</param>
    /// <param name="progressions">Whether the secondary progressions are in.</param>
    /// <param name="primaryDirections">
    /// The primary directions the input asked for, with one hit in the period, or null for none (branch PrimaryPanel).
    /// </param>
    /// <returns>The result.</returns>
    private static TransitResult TransitResultWith(
        bool transits, bool solarArcs, bool progressions, PrimaryDirectionSettings? primaryDirections = null)
    {
        DateTime instant = new(2026, 4, 14, 6, 0, 0, DateTimeKind.Utc);
        SolarArcHit arcHit = new()
        {
            DirectedPoint = "Saturn",
            AspectAngle = 90.0,
            AspectName = "Square",
            AspectAbbreviation = "sqr",
            Target = "Sun",
            Arc = 26.75,
            JulianDayUt = 2461144.75,
            InstantUtc = instant,
        };

        return new TransitResult
        {
            NatalChart = NatalChart(),
            ReportStartDate = new DateOnly(2026, 4, 12),
            ReportEndDate = new DateOnly(2026, 4, 18),
            MidpointDate = new DateOnly(2026, 4, 15),
            IncludeMinorAspects = false,
            IncludesTransits = transits,
            TransitPositions = new Dictionary<TransitBody, TransitPlanetPosition>(),
            Stations = [],
            Ingresses = [],
            ActiveTransits = [],
            HouseOverlays = [],
            MidpointActivations = [],
            MarsIncluded = false,
            SolarArcs = solarArcs
                ? new SolarArcResult { ArcValue = 26.75, ArcFormatted = "26\u00B045\u2032", Positions = [], Aspects = [] }
                : null,
            SolarArcHits = solarArcs ? [arcHit, arcHit] : [],
            Progressions = progressions
                ? new ProgressionResult
                {
                    ProgressedDate = new DateTime(2000, 1, 27, 12, 0, 0, DateTimeKind.Utc),
                    ProgressedJulianDay = 2451571.0,
                    SolarArcValue = 26.75,
                    Positions = [],
                    MoonInfo = new ProgressedMoonInfo
                    {
                        Longitude = 210.85,
                        Sign = "Scorpio",
                        Degrees = 0,
                        Minutes = 51,
                        NatalHouse = 10,
                        PhaseName = "First Quarter",
                        PhaseAngle = 100.0,
                    },
                    SignChanges = [],
                    Stations = [],
                    Aspects = [],
                }
                : null,
            ProgressedHits = progressions
                ?
                [
                    new ProgressedHit
                    {
                        ProgressedPoint = "Moon",
                        AspectAngle = 0.0,
                        AspectName = "Conjunction",
                        AspectAbbreviation = "cnj",
                        NatalPoint = "Moon",
                        JulianDayUt = 2461144.75,
                        InstantUtc = instant,
                    },
                ]
                : [],
            PrimaryDirections = primaryDirections is null
                ? null
                : new PrimaryDirectionResult
                {
                    NatalChart = NatalChart(),
                    Settings = primaryDirections,
                    Hits = [DirectionHit(instant)],
                    LeftOutCount = 0,
                },
            PrimaryDirectionHits = primaryDirections is null ? [] : [DirectionHit(instant)],
        };
    }

    /// <summary>A synthetic primary-direction hit at an instant: the direct Moon conjunct the Midheaven.</summary>
    /// <param name="instant">The hit's instant.</param>
    /// <returns>The hit.</returns>
    private static PrimaryDirectionHit DirectionHit(DateTime instant) => new()
    {
        Significator = "Midheaven",
        Promissor = "Moon",
        PromissorKind = PrimaryDirectionPromissorKind.Body,
        Aspect = PrimaryDirectionAspect.Conjunction,
        AspectAngle = 0.0,
        Motion = PrimaryDirectionMotion.Direct,
        Arc = 26.0,
        Years = 26.3,
        JulianDayUt = 2461144.75,
        InstantUtc = instant,
    };

    /// <summary>
    /// A transit service that records the input it was given and returns a result holding the
    /// techniques the input asked for.
    /// </summary>
    private sealed class RecordingTransitService : ITransitService
    {
        /// <summary>Gets the input of the last call, or null before the first.</summary>
        public TransitInput? Input { get; private set; }

        /// <inheritdoc />
        public TransitResult Calculate(TransitInput input, CancellationToken cancellationToken = default)
        {
            Input = input;
            return TransitResultWith(
                input.IncludeTransits, input.IncludeSolarArcs, input.IncludeSecondaryProgressions,
                input.IncludePrimaryDirections ? input.PrimaryDirectionSettings : null);
        }
    }

    /// <summary>
    /// Calculate cannot execute with a natal chart but no technique checked, and the unchecking
    /// re-evaluates it (1.4.0 item 2, branch ModesPanel; scoping C1b; the fourth technique since branch
    /// PrimaryPanel, 1.5.0).
    /// </summary>
    [Fact]
    public void CalculateTransit_NoTechnique_IsDisabled()
    {
        MainViewModel vm = CreateViewModel();
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        int raised = 0;
        vm.CalculateTransitCommand.CanExecuteChanged += (_, _) => raised++;

        vm.Transits.IncludeTransits = false;
        vm.Transits.IncludeSolarArcs = false;
        vm.Transits.IncludeSecondaryProgressions = false;
        vm.Transits.IncludePrimaryDirections = false;

        vm.CalculateTransitCommand.CanExecute(null).Should().BeFalse("no technique is checked");
        raised.Should().Be(4, "each unchecked technique re-evaluates Calculate");
    }

    /// <summary>
    /// Calculate can execute with a natal chart and one technique checked, the solar arcs alone.
    /// </summary>
    [Fact]
    public void CalculateTransit_OneTechnique_IsEnabled()
    {
        MainViewModel vm = CreateViewModel();
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);

        vm.Transits.IncludeTransits = false;
        vm.Transits.IncludeSecondaryProgressions = false;

        vm.CalculateTransitCommand.CanExecute(null).Should().BeTrue("the solar arcs are checked");
    }

    /// <summary>
    /// Export Daily Horoscope MD cannot execute for a result without transits, which the daily
    /// digest is made of, while the report export can (scoping C1e).
    /// </summary>
    [Fact]
    public void ExportDaily_SolarArcOnlyResult_IsDisabled()
    {
        MainViewModel vm = CreateViewModel();

        vm.Transits.TransitResult = TransitResultWith(transits: false, solarArcs: true, progressions: false);

        vm.ExportDailyHoroscopeMarkdownCommand.CanExecute(null).Should().BeFalse("the result holds no transits");
        vm.ExportTransitMarkdownCommand.CanExecute(null).Should().BeTrue("the report export takes any result");
    }

    /// <summary>
    /// Export Timeline MD cannot execute for a result without transits, which the timeline is made
    /// of, while the report export can (scoping C1e).
    /// </summary>
    [Fact]
    public void ExportTimeline_SolarArcOnlyResult_IsDisabled()
    {
        MainViewModel vm = CreateViewModel();

        vm.Transits.TransitResult = TransitResultWith(transits: false, solarArcs: true, progressions: false);

        vm.ExportTransitTimelineMarkdownCommand.CanExecute(null).Should().BeFalse("the result holds no transits");
        vm.ExportTransitMarkdownCommand.CanExecute(null).Should().BeTrue("the report export takes any result");
    }

    /// <summary>
    /// Calculate passes the three techniques as checked, the minor aspects only with the transits,
    /// and the status line names the result: its transits when the scan ran, else its hits.
    /// </summary>
    /// <param name="transits">Whether Transits is checked.</param>
    /// <param name="minor">Whether Minor is checked.</param>
    /// <param name="solarArcs">Whether Solar Arc is checked.</param>
    /// <param name="progressions">Whether Progressions is checked.</param>
    /// <param name="status">The status line expected.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData(true, true, false, false, "Transits calculated: 0 active transits, 0 midpoint activations.")]
    [InlineData(false, true, true, false, "Solar Arcs calculated: 2 solar arc hits.")]
    [InlineData(false, false, true, true, "Solar Arcs and Progressions calculated: 2 solar arc hits, 1 progressed hit.")]
    public async Task CalculateTransit_PassesTheCheckedTechniques(
        bool transits, bool minor, bool solarArcs, bool progressions, string status)
    {
        RecordingTransitService service = new();
        MainViewModel vm = CreateViewModel(transitService: service);
        SetPrivately(vm, nameof(MainViewModel.LastResult), NatalChart());
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        vm.Transits.IncludeTransits = transits;
        vm.Transits.IncludeMinorAspects = minor;
        vm.Transits.IncludeSolarArcs = solarArcs;
        vm.Transits.IncludeSecondaryProgressions = progressions;
        vm.Transits.IncludePrimaryDirections = false;

        vm.CalculateTransitCommand.Execute(null);
        int waited = 0;
        while (vm.CalculateTransitCommand.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        vm.CalculateTransitCommand.IsExecuting.Should().BeFalse("the command finishes within 5 s");
        TransitInput input = service.Input
            ?? throw new InvalidOperationException($"The transit service was not called; status: {vm.StatusText}");
        input.IncludeTransits.Should().Be(transits);
        input.IncludeMinorAspects.Should().Be(transits && minor, "the minor aspects are the transits' only");
        input.IncludeSolarArcs.Should().Be(solarArcs);
        input.IncludeSecondaryProgressions.Should().Be(progressions);
        input.IncludePrimaryDirections.Should().BeFalse("the Directions box is unchecked");
        vm.StatusText.Should().Be(status);
    }

    /// <summary>
    /// With the Directions box alone checked, Calculate passes the primary directions and the Directions panel's
    /// settings, so the period's hits and the whole-life list agree, and the status line counts the period's
    /// hits (branch PrimaryPanel, 1.5.0).
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateTransit_PassesThePrimaryDirectionsAndTheirSettings()
    {
        RecordingTransitService service = new();
        MainViewModel vm = CreateViewModel(transitService: service);
        SetPrivately(vm, nameof(MainViewModel.LastResult), NatalChart());
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        vm.Transits.IncludeTransits = false;
        vm.Transits.IncludeSolarArcs = false;
        vm.Transits.IncludeSecondaryProgressions = false;
        vm.Transits.IncludePrimaryDirections = true;
        vm.Directions.Key = PrimaryDirectionKey.Ptolemy;
        vm.Directions.PlanetsAsSignificators = true;
        vm.Directions.AgeLimitYears = 90;
        vm.Directions.BirthTimeUncertaintyMinutes = 4;

        vm.CalculateTransitCommand.Execute(null);
        int waited = 0;
        while (vm.CalculateTransitCommand.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        vm.CalculateTransitCommand.IsExecuting.Should().BeFalse("the command finishes within 5 s");
        TransitInput input = service.Input
            ?? throw new InvalidOperationException($"The transit service was not called; status: {vm.StatusText}");
        input.IncludePrimaryDirections.Should().BeTrue();
        PrimaryDirectionSettings settings = input.PrimaryDirectionSettings;
        (settings.Key, settings.PlanetsAsSignificators, settings.AgeLimitYears, settings.BirthTimeUncertaintyMinutes)
            .Should().Be((PrimaryDirectionKey.Ptolemy, true, 90, 4));
        vm.StatusText.Should().Be("Primary Directions calculated: 1 primary direction hit.");
    }

    /// <summary>
    /// Settings outside the engine's limits disable Calculate while the Directions box is checked, and not while
    /// it is unchecked; a change to either re-evaluates it.
    /// </summary>
    [Fact]
    public void CalculateTransit_InvalidDirectionSettings_GateCalculateOnlyWithTheBox()
    {
        MainViewModel vm = CreateViewModel();
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        int raised = 0;
        vm.CalculateTransitCommand.CanExecuteChanged += (_, _) => raised++;

        vm.Directions.AgeLimitYears = 0;

        raised.Should().BeGreaterThan(0, "the Directions panel's settings re-evaluate Calculate");
        vm.Transits.IncludePrimaryDirections.Should().BeTrue("the box is checked by default");
        vm.CalculateTransitCommand.CanExecute(null).Should().BeFalse("the Directions settings are invalid and the box is checked");

        vm.Transits.IncludePrimaryDirections = false;
        vm.CalculateTransitCommand.CanExecute(null).Should().BeTrue("the other three techniques do not read the Directions settings");

        vm.Transits.IncludePrimaryDirections = true;
        vm.Directions.AgeLimitYears = 100;
        vm.CalculateTransitCommand.CanExecute(null).Should().BeTrue("the settings are valid again");
    }

    #endregion

    #region Directions (branch PrimaryPanel, 1.5.0)

    /// <summary>
    /// A primary-direction service that records the chart and settings it was given, and the operation the
    /// view model reports while it runs, and returns a result of two hits with one left out.
    /// </summary>
    /// <param name="activeOperation">Reads the view model's active operation while the service runs.</param>
    private sealed class RecordingPrimaryDirectionService(Func<string?> activeOperation) : IPrimaryDirectionService
    {
        /// <summary>Gets the chart of the last call, or null before the first.</summary>
        public ChartResult? Chart { get; private set; }

        /// <summary>Gets the settings of the last call, or null before the first.</summary>
        public PrimaryDirectionSettings? Settings { get; private set; }

        /// <summary>Gets the active operation's display name during the last call.</summary>
        public string? ActiveOperation { get; private set; }

        /// <summary>Gets whether the last call's token could be cancelled.</summary>
        public bool TokenCanBeCancelled { get; private set; }

        /// <summary>Gets the result the last call returned.</summary>
        public PrimaryDirectionResult? Result { get; private set; }

        /// <inheritdoc />
        public PrimaryDirectionResult Calculate(
            ChartResult natalChart, PrimaryDirectionSettings settings, CancellationToken cancellationToken = default)
        {
            Chart = natalChart;
            Settings = settings;
            ActiveOperation = activeOperation();
            TokenCanBeCancelled = cancellationToken.CanBeCanceled;
            PrimaryDirectionHit hit = new()
            {
                Significator = "Midheaven",
                Promissor = "Moon",
                PromissorKind = PrimaryDirectionPromissorKind.Body,
                Aspect = PrimaryDirectionAspect.Conjunction,
                AspectAngle = 0.0,
                Motion = PrimaryDirectionMotion.Direct,
                Arc = 16.0,
                Years = 16.2,
                JulianDayUt = 2457474.5,
                InstantUtc = new DateTime(2016, 3, 26, 0, 0, 0, DateTimeKind.Utc),
            };
            Result = new PrimaryDirectionResult { NatalChart = natalChart, Settings = settings, Hits = [hit, hit], LeftOutCount = 1 };
            return Result;
        }

        /// <inheritdoc />
        public IReadOnlyList<PrimaryDirectionHit> FindHits(
            ChartResult natalChart, PrimaryDirectionSettings settings, DateOnly start, DateOnly end,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The view model calculates the whole life.");
    }

    /// <summary>
    /// Calculate Directions runs the engine on the Chart panel's chart with the panel's settings, under its own
    /// operation key, sets the result and names it in the status line (branch PrimaryPanel, 1.5.0).
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateDirections_RunsUnderItsKeyAndSetsTheResult()
    {
        MainViewModel? vm = null;
        RecordingPrimaryDirectionService service = new(() => vm?.ActiveOperationDisplayName);
        vm = CreateViewModel(primaryDirectionService: service);
        ChartResult chart = NatalChart();
        SetPrivately(vm, nameof(MainViewModel.LastResult), chart);
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        vm.Directions.Key = PrimaryDirectionKey.Ptolemy;
        vm.Directions.PlanetsAsSignificators = true;
        vm.Directions.AgeLimitYears = 90;
        vm.Directions.BirthTimeUncertaintyMinutes = 4;

        vm.CalculateDirectionsCommand.Execute(null);
        int waited = 0;
        while (vm.CalculateDirectionsCommand.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        vm.CalculateDirectionsCommand.IsExecuting.Should().BeFalse("the command finishes within 5 s");
        service.Chart.Should().BeSameAs(chart);
        PrimaryDirectionSettings settings = service.Settings
            ?? throw new InvalidOperationException($"The service was not called; status: {vm.StatusText}");
        (settings.Key, settings.PlanetsAsSignificators, settings.AgeLimitYears, settings.BirthTimeUncertaintyMinutes)
            .Should().Be((PrimaryDirectionKey.Ptolemy, true, 90, 4));
        service.ActiveOperation.Should().Be("Primary Directions", "the calculation runs under the directions key");
        service.TokenCanBeCancelled.Should().BeTrue("Cancel reaches the running calculation");
        vm.Directions.CurrentResult.Should().BeSameAs(service.Result);
        vm.StatusText.Should().Be("Primary directions calculated: 2 directions to age 90 (1 left out).");
        vm.HasActiveOperation.Should().BeFalse("the key is released when the calculation ends");
    }

    /// <summary>
    /// Calculate Directions needs a chart on the Chart panel and valid settings, and a change to either
    /// re-evaluates it.
    /// </summary>
    [Fact]
    public void CalculateDirections_NeedsAChartAndValidSettings()
    {
        MainViewModel vm = CreateViewModel();
        int raised = 0;
        vm.CalculateDirectionsCommand.CanExecuteChanged += (_, _) => raised++;

        vm.CalculateDirectionsCommand.CanExecute(null).Should().BeFalse("there is no chart yet");

        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        vm.CalculateDirectionsCommand.CanExecute(null).Should().BeTrue("a chart and the default settings");

        int before = raised;
        vm.Directions.AgeLimitYears = 0;
        vm.CalculateDirectionsCommand.CanExecute(null).Should().BeFalse("an age limit of 0 is outside 1 to 120");
        raised.Should().BeGreaterThan(before, "the setting re-evaluates Calculate Directions");

        vm.Directions.AgeLimitYears = 100;
        vm.Directions.BirthTimeUncertaintyMinutes = 121;
        vm.CalculateDirectionsCommand.CanExecute(null).Should().BeFalse("an uncertainty of 121 is outside 0 to 120");
    }

    /// <summary>Clear Form puts the Directions panel back to its defaults and drops its result.</summary>
    [Fact]
    public void ClearForm_ClearsTheDirections()
    {
        MainViewModel vm = CreateViewModel();
        vm.Directions.Key = PrimaryDirectionKey.SolarArcRightAscension;
        vm.Directions.PlanetsAsSignificators = true;
        vm.Directions.AgeLimitYears = 80;
        vm.Directions.BirthTimeUncertaintyMinutes = 10;
        vm.Directions.CurrentResult = new PrimaryDirectionResult
        {
            NatalChart = NatalChart(),
            Settings = PrimaryDirectionSettings.Default,
            Hits = [],
            LeftOutCount = 0,
        };

        vm.ClearFormCommand.Execute(null);

        vm.Directions.Key.Should().Be(PrimaryDirectionKey.Naibod);
        vm.Directions.PlanetsAsSignificators.Should().BeFalse();
        vm.Directions.AgeLimitYears.Should().Be(100);
        vm.Directions.BirthTimeUncertaintyMinutes.Should().Be(0);
        vm.Directions.HasResult.Should().BeFalse();
        vm.ExportDirectionsMarkdownCommand.CanExecute(null).Should().BeFalse("the result is gone");
    }

    #endregion

    #region Natal Chart Dropdown Tests

    private static ChartEntity CreateTestChartEntity() => new()
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

    [Fact]
    public void RelocationCommand_CanExecute_WithDropdownAndLocation()
    {
        MainViewModel vm = CreateViewModel();
        vm.Relocation.SelectedNatalChart = CreateTestChartEntity();
        vm.Relocation.LocationName = "London";
        vm.Relocation.LatitudeText = "51.5074";
        vm.Relocation.LongitudeText = "-0.1278";

        bool canExecute = vm.CalculateRelocationCommand.CanExecute(null);

        canExecute.Should().BeTrue(
            "dropdown selection should satisfy natal chart requirement");
    }

    /// <summary>
    /// Calculate Relocation cannot execute while either coordinate is out of range,
    /// unreadable or blank (1.4.0, port plan §9 item 9): the predicate reads
    /// <see cref="RelocationViewModel.HasValidCoordinates"/>.
    /// </summary>
    [Fact]
    public void RelocationCommand_CannotExecute_WithInvalidCoordinates()
    {
        MainViewModel vm = CreateViewModel();
        vm.Relocation.SelectedNatalChart = CreateTestChartEntity();
        vm.Relocation.LocationName = "London";
        vm.Relocation.LongitudeText = "-0.1278";

        vm.Relocation.LatitudeText = "95";
        vm.CalculateRelocationCommand.CanExecute(null).Should().BeFalse("95 is beyond the pole");

        vm.Relocation.LatitudeText = "51,5";
        vm.CalculateRelocationCommand.CanExecute(null).Should().BeFalse("a comma is refused");

        vm.Relocation.LatitudeText = string.Empty;
        vm.CalculateRelocationCommand.CanExecute(null).Should().BeFalse("a blank latitude leaves nothing to relocate to");

        vm.Relocation.LatitudeText = "51.5074";
        vm.CalculateRelocationCommand.CanExecute(null).Should().BeTrue();
    }

    /// <summary>
    /// Calculate &amp; Save refuses a comma in the longitude with the Natal panel's
    /// message rather than reading "-7,5" as −75 (scoping decision 3(ii)).
    /// </summary>
    [Fact]
    public void CalculateAndSave_Should_RefuseACommaInTheLongitude()
    {
        MainViewModel vm = CreateViewModel();
        vm.ChartEntry.Name = "Comma";
        vm.ChartEntry.TimeZone = "UTC";
        vm.ChartEntry.LocationName = "Nowhere";
        vm.ChartEntry.LatitudeText = "37.4";
        vm.ChartEntry.LongitudeText = "-7,5";

        // Validation returns before any await, so the command has finished on return.
        vm.CalculateAndSaveCommand.Execute(null);

        vm.StatusText.Should().Be("Invalid longitude. Use decimal degrees (e.g., -79.1877).");
        vm.HasResult.Should().BeFalse("no chart is cast at −75");
    }

    /// <summary>
    /// The electional search refuses an event latitude beyond ±90 before it runs
    /// (scoping decision 3(ii)); until 1.4.0 it had no range check.
    /// </summary>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Fact]
    public async Task CalculateElectional_Should_RefuseALatitudeBeyondNinety()
    {
        MainViewModel vm = CreateViewModel();
        vm.Electional.SelectedNatalChart = CreateTestChartEntity();
        vm.Electional.Goal = "Open a shop";
        vm.Electional.LocationName = "Richmond, VA";
        vm.Electional.TimeZone = "America/New_York";
        vm.Electional.LatitudeText = "95";
        vm.Electional.LongitudeText = "-77.4343";

        vm.CalculateElectionalCommand.CanExecute(null).Should().BeTrue("the form is filled in");
        vm.CalculateElectionalCommand.Execute(null);
        int waited = 0;
        while (vm.CalculateElectionalCommand.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        vm.StatusText.Should().Be("Electional location: Latitude must be between -90 and 90 degrees.");
        vm.Electional.ElectionalResult.Should().BeNull("nothing is searched");
    }

    [Fact]
    public void RelocationCommand_CannotExecute_WithoutDropdownOrHasResult()
    {
        MainViewModel vm = CreateViewModel();
        vm.Relocation.LocationName = "London";

        bool canExecute = vm.CalculateRelocationCommand.CanExecute(null);

        canExecute.Should().BeFalse(
            "requires either a dropdown selection or HasResult");
    }

    [Fact]
    public void ElectionalCommand_CanExecute_WithDropdownAndRequiredFields()
    {
        MainViewModel vm = CreateViewModel();
        vm.Electional.SelectedNatalChart = CreateTestChartEntity();
        vm.Electional.Goal = "Test election";
        vm.Electional.LocationName = "New York";
        vm.Electional.TimeZone = "America/New_York";

        bool canExecute = vm.CalculateElectionalCommand.CanExecute(null);

        canExecute.Should().BeTrue(
            "dropdown selection should satisfy natal chart requirement");
    }

    /// <summary>
    /// Electional Calculate cannot execute while the event's time-zone box is
    /// blank, Natal's rule (1.4.0, port plan §9 item 16; scoping decision 4).
    /// Until then the run started, cast a dropdown chart, and failed in the
    /// service with "No time zone was given."
    /// </summary>
    [Fact]
    public void ElectionalCommand_CannotExecute_WithoutTimeZone()
    {
        MainViewModel vm = CreateViewModel();
        vm.Electional.SelectedNatalChart = CreateTestChartEntity();
        vm.Electional.Goal = "Test election";
        vm.Electional.LocationName = "New York";
        vm.Electional.TimeZone = "  ";

        vm.CalculateElectionalCommand.CanExecute(null).Should().BeFalse("the zone is blank");

        vm.Electional.TimeZone = "America/New_York";

        vm.CalculateElectionalCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public void ElectionalCommand_CannotExecute_WithoutDropdownOrCanCalculate()
    {
        MainViewModel vm = CreateViewModel();
        vm.Electional.Goal = "Test election";
        vm.Electional.LocationName = "New York";

        bool canExecute = vm.CalculateElectionalCommand.CanExecute(null);

        canExecute.Should().BeFalse(
            "requires either a dropdown selection or CanCalculate()");
    }

    [Fact]
    public void UseNatalLocationCommand_CanExecute_WithElectionalDropdown()
    {
        MainViewModel vm = CreateViewModel();
        vm.Electional.SelectedNatalChart = CreateTestChartEntity();

        bool canExecute = vm.UseNatalLocationCommand.CanExecute(null);

        canExecute.Should().BeTrue(
            "electional dropdown selection should enable Use Natal button");
    }

    #endregion

    #region Points (branch AsteroidsPanel, 1.6.0)

    /// <summary>The Points box's on line for the test chart.</summary>
    private const string PointsOnLine =
        "Points on: Test Person now carries the asteroids and Lilith; recalculate other panels to include them.";

    /// <summary>The Points box's off line for the test chart.</summary>
    private const string PointsOffLine =
        "Points off: Test Person no longer carries the asteroids and Lilith; recalculate other panels to leave them out.";

    /// <summary>
    /// An <see cref="IChartRepository"/> that counts its saves and updates and otherwise stores nothing, as
    /// <see cref="StubChartRepository"/> does: for the test that a recast never reaches the repository.
    /// </summary>
    private sealed class RecordingChartRepository : IChartRepository
    {
        private readonly StubChartRepository _inner = new();

        /// <summary>Gets the number of <see cref="SaveAsync"/> calls.</summary>
        public int Saves { get; private set; }

        /// <summary>Gets the number of <see cref="UpdateAsync"/> calls.</summary>
        public int Updates { get; private set; }

        /// <inheritdoc />
        public Task<long> SaveAsync(ChartEntity chart, CancellationToken ct = default)
        {
            Saves++;
            return _inner.SaveAsync(chart, ct);
        }

        /// <inheritdoc />
        public Task UpdateAsync(ChartEntity chart, CancellationToken ct = default)
        {
            Updates++;
            return _inner.UpdateAsync(chart, ct);
        }

        /// <inheritdoc />
        public Task<bool> DeleteAsync(long id, CancellationToken ct = default) => _inner.DeleteAsync(id, ct);

        /// <inheritdoc />
        public Task<ChartEntity?> GetByIdAsync(long id, CancellationToken ct = default) => _inner.GetByIdAsync(id, ct);

        /// <inheritdoc />
        public Task<ChartEntity?> GetByNameAsync(string name, CancellationToken ct = default) => _inner.GetByNameAsync(name, ct);

        /// <inheritdoc />
        public Task<IReadOnlyList<ChartEntity>> SearchByNameAsync(string namePattern, CancellationToken ct = default) =>
            _inner.SearchByNameAsync(namePattern, ct);

        /// <inheritdoc />
        public Task<IReadOnlyList<ChartEntity>> ListAllAsync(CancellationToken ct = default) => _inner.ListAllAsync(ct);

        /// <inheritdoc />
        public Task<IReadOnlyList<ChartEntity>> ListByEventTypeAsync(string eventType, CancellationToken ct = default) =>
            _inner.ListByEventTypeAsync(eventType, ct);

        /// <inheritdoc />
        public Task InitializeAsync(CancellationToken ct = default) => _inner.InitializeAsync(ct);

        /// <inheritdoc />
        public Task<string> ExportAllAsJsonAsync(CancellationToken ct = default) => _inner.ExportAllAsJsonAsync(ct);

        /// <inheritdoc />
        public Task<int> ImportFromJsonAsync(string json, CancellationToken ct = default) => _inner.ImportFromJsonAsync(json, ct);

        /// <inheritdoc />
        public Task<IReadOnlyList<ChartEntity>> QueryAsync(ChartQueryOptions options, CancellationToken ct = default) =>
            _inner.QueryAsync(options, ct);
    }

    /// <summary>Waits for a command's fire-and-forget handler to finish, as the transit tests above poll.</summary>
    /// <param name="command">The command.</param>
    /// <returns>A task that completes when the command has finished, and fails after 5 s.</returns>
    private static async Task WaitAsync(AsyncRelayCommand command)
    {
        int waited = 0;
        while (command.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        command.IsExecuting.Should().BeFalse("the command finishes within 5 s");
    }

    /// <summary>A saved horary chart, as the list would hold it.</summary>
    /// <returns>The chart.</returns>
    private static ChartEntity HoraryChartEntity() => new()
    {
        Name = "Test Person",
        EventType = "Horary",
        LocalDateTime = "2026-10-20T09:30:00",
        TimeZone = "UTC",
        UtcDateTime = "2026-10-20T09:30:00Z",
        JulianDay = 2461333.8958333335,
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        HoraryQuestion = "Will it rain?",
        HoraryCategory = "Weather",
        CreatedAt = DateTime.UtcNow.ToString("o"),
        UpdatedAt = DateTime.UtcNow.ToString("o"),
    };

    /// <summary>Calculate &amp; Save builds its input with the Points box's value (rule 4, the first site).</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateAndSave_PassesTheSwitchIntoTheInput()
    {
        RecordingChartEngine engine = new();
        MainViewModel vm = CreateViewModel(chartEngine: engine);
        vm.ChartEntry.IncludeMinorPoints = true;
        vm.ChartEntry.Name = "Test Person";
        vm.ChartEntry.TimeZone = "UTC";
        vm.ChartEntry.LocationName = "New York City";
        vm.ChartEntry.LatitudeText = "40.7128";
        vm.ChartEntry.LongitudeText = "-74.006";

        vm.CalculateAndSaveCommand.Execute(null);
        await WaitAsync(vm.CalculateAndSaveCommand);

        ChartInput input = engine.LastInput
            ?? throw new InvalidOperationException($"The engine was not called; status: {vm.StatusText}");
        input.Name.Should().Be("Test Person");
        input.IncludeMinorPoints.Should().BeTrue("the box is ticked");
        ChartResult result = vm.LastResult
            ?? throw new InvalidOperationException($"No chart is on screen; status: {vm.StatusText}");
        result.Input.Should().BeSameAs(input);
    }

    /// <summary>Load maps the row with the Points box's value, which the row does not hold (rule 4, the second site).</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task LoadChart_PassesTheSwitchIntoTheInput()
    {
        RecordingChartEngine engine = new();
        MainViewModel vm = CreateViewModel(chartEngine: engine);
        vm.ChartEntry.IncludeMinorPoints = true;

        vm.LoadChartCommand.Execute(CreateTestChartEntity());
        await WaitAsync(vm.LoadChartCommand);

        ChartInput input = engine.LastInput
            ?? throw new InvalidOperationException($"The engine was not called; status: {vm.StatusText}");
        input.Name.Should().Be("Test Person");
        input.IncludeMinorPoints.Should().BeTrue("Load takes the session's value, not the row's");
        vm.ChartEntry.IncludeMinorPoints.Should().BeTrue("PopulateFrom leaves the box");
    }

    /// <summary>Synastry casts Person B with the Points box's value (rule 4, the third site).</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Synastry_PersonB_CarriesTheSwitch()
    {
        RecordingChartEngine engine = new();
        MainViewModel vm = CreateViewModel(chartEngine: engine);
        vm.ChartEntry.IncludeMinorPoints = true;
        SetPrivately(vm, nameof(MainViewModel.LastResult), NatalChart());
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        vm.Synastry.PersonB = CreateTestChartEntity();

        vm.CalculateSynastryCommand.CanExecute(null).Should().BeTrue("a chart is on screen and Person B is picked");
        vm.CalculateSynastryCommand.Execute(null);
        await WaitAsync(vm.CalculateSynastryCommand);

        engine.Inputs.Should().ContainSingle($"Person B is the only chart cast; status: {vm.StatusText}")
            .Which.IncludeMinorPoints.Should().BeTrue("the box is ticked");
        vm.Synastry.SynastryResult.Should().NotBeNull(vm.StatusText);
    }

    /// <summary>The composite casts Person B with the Points box's value (rule 4, the sixth site).</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Composite_PersonB_CarriesTheSwitch()
    {
        RecordingChartEngine engine = new();
        MainViewModel vm = CreateViewModel(chartEngine: engine);
        vm.ChartEntry.IncludeMinorPoints = true;
        SetPrivately(vm, nameof(MainViewModel.LastResult), NatalChart());
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        vm.Synastry.PersonB = CreateTestChartEntity();

        vm.CalculateCompositeCommand.CanExecute(null).Should().BeTrue("a chart is on screen and Person B is picked");
        vm.CalculateCompositeCommand.Execute(null);
        await WaitAsync(vm.CalculateCompositeCommand);

        engine.Inputs.Should().ContainSingle($"Person B is the only chart cast; status: {vm.StatusText}")
            .Which.IncludeMinorPoints.Should().BeTrue("the box is ticked");
        vm.Synastry.CompositeResult.Should().NotBeNull(vm.StatusText);
    }

    /// <summary>
    /// The electional search casts the natal chart picked from the list with the Points box's value (rule 4, the
    /// fourth site). The stub electional service throws once the natal is cast; the cast is what this test reads.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    #region Davison (branch DavisonPanel, 1.7.0)

    /// <summary>Calculate Davison with no Person B does nothing: no call to the service and the status line untouched.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateDavison_WithNoPersonB_DoesNothing()
    {
        StubDavisonService davison = new();
        MainViewModel vm = CreateViewModel(davisonService: davison);
        SetPrivately(vm, nameof(MainViewModel.LastResult), NatalChart());
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        string status = vm.StatusText;

        vm.CalculateDavisonCommand.CanExecute(null).Should().BeFalse("no Person B is picked");
        vm.CalculateDavisonCommand.Execute(null);
        await WaitAsync(vm.CalculateDavisonCommand);

        davison.Calls.Should().BeEmpty();
        vm.Synastry.DavisonResult.Should().BeNull();
        vm.StatusText.Should().Be(status);
    }

    /// <summary>Calculate Davison sets the result and names the moment in UT and the place in the status line.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateDavison_SetsTheResultAndNamesTheMomentAndPlace()
    {
        StubDavisonService davison = new();
        MainViewModel vm = CreateViewModel(davisonService: davison);
        SetPrivately(vm, nameof(MainViewModel.LastResult), NatalChart());
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        vm.Synastry.PersonB = CreateTestChartEntity();

        vm.CalculateDavisonCommand.CanExecute(null).Should().BeTrue("a chart is on screen and Person B is picked");
        vm.CalculateDavisonCommand.Execute(null);
        await WaitAsync(vm.CalculateDavisonCommand);

        DavisonResult result = vm.Synastry.DavisonResult
            ?? throw new InvalidOperationException($"No Davison result; status: {vm.StatusText}");
        davison.Calls.Should().ContainSingle();
        vm.Synastry.HasDavisonResult.Should().BeTrue();
        vm.StatusText.Should().Be(
            $"Davison chart calculated for {result.ChartA.Input.Name} & {result.ChartB.Input.Name}: " +
            $"{CalendarDateFormatter.DateTime(result.MidpointUtc)} UTC at {result.MidpointLocationDescription}.");
        result.MidpointLocationDescription.Should().Be("Midpoint of New York City & New York City");
    }

    /// <summary>Calculate Davison casts Person B with the Points box's value and hands the switch to the service.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateDavison_PersonB_CarriesTheSwitch()
    {
        RecordingChartEngine engine = new();
        StubDavisonService davison = new();
        MainViewModel vm = CreateViewModel(chartEngine: engine, davisonService: davison);
        vm.ChartEntry.IncludeMinorPoints = true;
        SetPrivately(vm, nameof(MainViewModel.LastResult), NatalChart());
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        vm.Synastry.PersonB = CreateTestChartEntity();

        vm.CalculateDavisonCommand.Execute(null);
        await WaitAsync(vm.CalculateDavisonCommand);

        engine.Inputs.Should().ContainSingle($"Person B is the only chart cast; status: {vm.StatusText}")
            .Which.IncludeMinorPoints.Should().BeTrue("the box is ticked");
        davison.Calls.Should().ContainSingle().Which.IncludeMinorPoints.Should().BeTrue("the service takes the switch");
        vm.Synastry.PersonBChart.Should().NotBeNull("Person B's cast is cached, as the Composite caches it");
    }

    /// <summary>When the service throws, the status line reports "Davison error: {message}" and there is no result.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateDavison_WhenTheServiceThrows_ReportsTheError()
    {
        StubDavisonService davison = new() { ThrowOnCalculate = true };
        MainViewModel vm = CreateViewModel(davisonService: davison);
        SetPrivately(vm, nameof(MainViewModel.LastResult), NatalChart());
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        vm.Synastry.PersonB = CreateTestChartEntity();

        vm.CalculateDavisonCommand.Execute(null);
        await WaitAsync(vm.CalculateDavisonCommand);

        vm.StatusText.Should().Be("Davison error: " + StubDavisonService.ThrowMessage);
        vm.Synastry.DavisonResult.Should().BeNull();
        vm.IsCalculating.Should().BeFalse();
    }

    /// <summary>Export Davison MD is disabled without a result and enabled once one is set.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ExportDavison_IsDisabledWithoutAResult_AndEnabledAfter()
    {
        MainViewModel vm = CreateViewModel(davisonService: new StubDavisonService());
        vm.ExportDavisonMarkdownCommand.CanExecute(null).Should().BeFalse("export requires a Davison result");

        SetPrivately(vm, nameof(MainViewModel.LastResult), NatalChart());
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        vm.Synastry.PersonB = CreateTestChartEntity();
        vm.CalculateDavisonCommand.Execute(null);
        await WaitAsync(vm.CalculateDavisonCommand);

        vm.ExportDavisonMarkdownCommand.CanExecute(null).Should().BeTrue(vm.StatusText);
    }

    /// <summary>Clear Form clears the Davison result and its context, through the Synastry child's Clear.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ClearForm_ClearsTheDavisonResultAndContext()
    {
        MainViewModel vm = CreateViewModel(davisonService: new StubDavisonService());
        SetPrivately(vm, nameof(MainViewModel.LastResult), NatalChart());
        SetPrivately(vm, nameof(MainViewModel.HasResult), true);
        vm.Synastry.PersonB = CreateTestChartEntity();
        vm.Synastry.DavisonInterpretationContext = "a long friendship";
        vm.CalculateDavisonCommand.Execute(null);
        await WaitAsync(vm.CalculateDavisonCommand);
        vm.Synastry.HasDavisonResult.Should().BeTrue(vm.StatusText);

        vm.ClearFormCommand.Execute(null);

        vm.Synastry.DavisonResult.Should().BeNull();
        vm.Synastry.DavisonInterpretationContext.Should().BeEmpty();
        vm.ExportDavisonMarkdownCommand.CanExecute(null).Should().BeFalse();
    }

    #endregion

    [Fact]
    public async Task Electional_NatalFromTheList_CarriesTheSwitch()
    {
        RecordingChartEngine engine = new();
        MainViewModel vm = CreateViewModel(chartEngine: engine);
        vm.ChartEntry.IncludeMinorPoints = true;
        vm.Electional.SelectedNatalChart = CreateTestChartEntity();
        vm.Electional.Goal = "Open a shop";
        vm.Electional.LocationName = "Richmond, VA";
        vm.Electional.TimeZone = "America/New_York";
        vm.Electional.LatitudeText = "37.5385";
        vm.Electional.LongitudeText = "-77.4343";

        vm.CalculateElectionalCommand.CanExecute(null).Should().BeTrue("the form is filled in");
        vm.CalculateElectionalCommand.Execute(null);
        await WaitAsync(vm.CalculateElectionalCommand);

        engine.Inputs.Should().ContainSingle($"the picked natal chart is the only chart cast; status: {vm.StatusText}")
            .Which.IncludeMinorPoints.Should().BeTrue("the box is ticked");
        vm.LastResult.Should().BeNull("a natal chart picked from the list is not the Chart panel's chart");
    }

    /// <summary>
    /// The electional search, falling back to the Chart panel's fields, casts the natal chart with the Points
    /// box's value, and that chart becomes the Chart panel's chart (rule 4, the fifth site).
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Electional_NatalFromTheForm_CarriesTheSwitch()
    {
        RecordingChartEngine engine = new();
        MainViewModel vm = CreateViewModel(chartEngine: engine);
        vm.ChartEntry.IncludeMinorPoints = true;
        vm.ChartEntry.Name = "Test Person";
        vm.ChartEntry.TimeZone = "UTC";
        vm.ChartEntry.LocationName = "New York City";
        vm.ChartEntry.LatitudeText = "40.7128";
        vm.ChartEntry.LongitudeText = "-74.006";
        vm.Electional.Goal = "Open a shop";
        vm.Electional.LocationName = "Richmond, VA";
        vm.Electional.TimeZone = "America/New_York";
        vm.Electional.LatitudeText = "37.5385";
        vm.Electional.LongitudeText = "-77.4343";

        vm.CalculateElectionalCommand.CanExecute(null).Should().BeTrue("the Chart panel's form can calculate");
        vm.CalculateElectionalCommand.Execute(null);
        await WaitAsync(vm.CalculateElectionalCommand);

        ChartInput input = engine.Inputs
            .Should().ContainSingle($"the natal chart cast from the form is the only chart cast; status: {vm.StatusText}")
            .Which;
        input.Name.Should().Be("Test Person");
        input.IncludeMinorPoints.Should().BeTrue("the box is ticked");
        ChartResult result = vm.LastResult
            ?? throw new InvalidOperationException($"The form's natal chart did not become the Chart panel's; status: {vm.StatusText}");
        result.Input.Should().BeSameAs(input);
    }

    /// <summary>
    /// The Points box ticked while the form fallback's cast runs: the landed chart carried the box's old value,
    /// so it is recast at once with the new one, the search scores the chart on screen, and the run's own status
    /// line has the last word (1.6.1, item D, decision 5, branch ElectionalPicker). Until 1.6.1 a toggle during
    /// the cast did nothing, since the recast runs only with a chart on screen, and the landed chart disagreed
    /// with the box until the next tick or Calculate.
    /// </summary>
    /// <remarks>
    /// The engine's probe ticks the box from inside the fallback's cast, on the cast's thread: a tick there
    /// reaches the recast while <c>LastResult</c> is still null, which is exactly the case the fallback handles
    /// when its cast lands. The stub electional service throws once the natal is cast, so the status line is the
    /// search's error, which starts with "Electional" as its completion line does.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Electional_NatalFromTheForm_TickedDuringTheRun_RecastsWhenItLands()
    {
        RecordingChartEngine engine = new();
        MainViewModel vm = CreateViewModel(chartEngine: engine);
        engine.Probe = () =>
        {
            vm.ChartEntry.IncludeMinorPoints = true;
            return null;
        };
        vm.ChartEntry.Name = "Test Person";
        vm.ChartEntry.TimeZone = "UTC";
        vm.ChartEntry.LocationName = "New York City";
        vm.ChartEntry.LatitudeText = "40.7128";
        vm.ChartEntry.LongitudeText = "-74.006";
        vm.Electional.Goal = "Open a shop";
        vm.Electional.LocationName = "Richmond, VA";
        vm.Electional.TimeZone = "America/New_York";
        vm.Electional.LatitudeText = "37.5385";
        vm.Electional.LongitudeText = "-77.4343";

        vm.CalculateElectionalCommand.CanExecute(null).Should().BeTrue("the Chart panel's form can calculate");
        vm.CalculateElectionalCommand.Execute(null);
        await WaitAsync(vm.CalculateElectionalCommand);

        engine.Inputs.Should().HaveCount(2, $"the landed chart is recast for the moved box; status: {vm.StatusText}");
        engine.Inputs[0].IncludeMinorPoints.Should().BeFalse("the box was clear when the fallback's cast began");
        engine.Inputs[1].IncludeMinorPoints.Should().BeTrue("the recast carries the box's new value");
        ChartResult result = vm.LastResult
            ?? throw new InvalidOperationException($"The form's natal chart did not become the Chart panel's; status: {vm.StatusText}");
        result.Input.IncludeMinorPoints.Should().BeTrue("the chart on screen agrees with the box");
        vm.StatusText.Should().StartWith("Electional", "the run's own status line overwrites the recast's");
    }

    /// <summary>Calculate Relocation casts the picked chart with the Points box's value (rule 4, the seventh site).</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task Relocation_PickedChart_CarriesTheSwitch()
    {
        RecordingChartEngine engine = new();
        MainViewModel vm = CreateViewModel(chartEngine: engine);
        vm.ChartEntry.IncludeMinorPoints = true;
        vm.Relocation.SelectedNatalChart = CreateTestChartEntity();
        vm.Relocation.LocationName = "London";
        vm.Relocation.LatitudeText = "51.5074";
        vm.Relocation.LongitudeText = "-0.1278";

        vm.CalculateRelocationCommand.CanExecute(null).Should().BeTrue("a chart is picked and the place is valid");
        vm.CalculateRelocationCommand.Execute(null);
        await WaitAsync(vm.CalculateRelocationCommand);

        engine.Inputs.Should().ContainSingle($"the picked chart is the only chart cast; status: {vm.StatusText}")
            .Which.IncludeMinorPoints.Should().BeTrue("the box is ticked");
        vm.Relocation.HasResult.Should().BeTrue(vm.StatusText);
    }

    /// <summary>Clear Form leaves the Points box: the switch belongs to the session, not the form (R2).</summary>
    [Fact]
    public void ClearForm_LeavesTheMinorPointsSwitch()
    {
        MainViewModel vm = CreateViewModel();
        vm.ChartEntry.Name = "Populated";
        vm.ChartEntry.IncludeMinorPoints = true;

        vm.ClearFormCommand.Execute(null);

        vm.ChartEntry.Name.Should().BeEmpty("the form is cleared");
        vm.ChartEntry.IncludeMinorPoints.Should().BeTrue("Clear Form keeps the session's switch");
    }

    /// <summary>
    /// Ticking the Points box recasts the chart on screen from its own input with the switch on, and unticking
    /// it recasts with the switch off; each names the chart in the status line, and neither saves anything
    /// (rule 5; Steve's choice of 2026-10-20).
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TogglingPoints_RecastsTheChartOnScreen_WithoutSaving()
    {
        RecordingChartEngine engine = new();
        RecordingChartRepository repository = new();
        MainViewModel vm = CreateViewModel(chartEngine: engine, repository: repository);
        vm.LoadChartCommand.Execute(CreateTestChartEntity());
        await WaitAsync(vm.LoadChartCommand);
        ChartResult before = vm.LastResult
            ?? throw new InvalidOperationException($"Load cast no chart; status: {vm.StatusText}");
        before.Input.IncludeMinorPoints.Should().BeFalse("the box is off");
        engine.Inputs.Should().HaveCount(1, "Load cast the chart once");

        vm.ChartEntry.IncludeMinorPoints = true;

        engine.Inputs.Should().HaveCount(2, "ticking the box recasts the chart once");
        ChartResult on = vm.LastResult
            ?? throw new InvalidOperationException("The chart on screen is gone.");
        on.Should().NotBeSameAs(before, "the chart on screen is the recast");
        on.Input.Should().BeSameAs(engine.LastInput, "the recast is the engine's last cast");
        on.Input.IncludeMinorPoints.Should().BeTrue("the switch is on");
        on.Input.Should().BeEquivalentTo(
            before.Input, o => o.Excluding(i => i.IncludeMinorPoints), "the other seventeen properties are the chart's own");
        vm.StatusText.Should().Be(PointsOnLine);

        vm.ChartEntry.IncludeMinorPoints = false;

        engine.Inputs.Should().HaveCount(3, "unticking the box recasts the chart once more");
        ChartResult off = vm.LastResult
            ?? throw new InvalidOperationException("The chart on screen is gone.");
        off.Should().NotBeSameAs(on);
        off.Input.IncludeMinorPoints.Should().BeFalse("the switch is off again");
        off.Input.Should().BeEquivalentTo(before.Input, "the input is back to the chart's first, in every property");
        vm.StatusText.Should().Be(PointsOffLine);

        vm.HasResult.Should().BeTrue("a recast is not a Clear");
        repository.Saves.Should().Be(0, "a recast is never saved");
        repository.Updates.Should().Be(0, "a recast is never saved");
    }

    /// <summary>With no chart on screen the Points box changes nothing: no engine call, the status line as it was.</summary>
    [Fact]
    public void TogglingPoints_WithNoChartOnScreen_ChangesNothing()
    {
        RecordingChartEngine engine = new();
        MainViewModel vm = CreateViewModel(chartEngine: engine);
        string status = vm.StatusText;

        vm.ChartEntry.IncludeMinorPoints = true;
        vm.ChartEntry.IncludeMinorPoints = false;

        engine.Inputs.Should().BeEmpty("there is no chart to recast");
        vm.StatusText.Should().Be(status, "nothing happened");
        vm.LastResult.Should().BeNull();
        vm.HasResult.Should().BeFalse();
    }

    /// <summary>
    /// A horary chart is recast all the same when the box changes, and its line says a horary chart never
    /// carries the five (the engine's gate on the event type).
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TogglingPoints_OnAHoraryChart_SaysAHoraryChartNeverCarriesThem()
    {
        RecordingChartEngine engine = new();
        MainViewModel vm = CreateViewModel(chartEngine: engine);
        vm.LoadChartCommand.Execute(HoraryChartEntity());
        await WaitAsync(vm.LoadChartCommand);
        ChartResult before = vm.LastResult
            ?? throw new InvalidOperationException($"Load cast no chart; status: {vm.StatusText}");

        vm.ChartEntry.IncludeMinorPoints = true;

        engine.Inputs.Should().HaveCount(2, "a horary chart is recast all the same");
        ChartResult on = vm.LastResult
            ?? throw new InvalidOperationException("The chart on screen is gone.");
        on.Should().NotBeSameAs(before);
        on.Input.EventType.Should().Be("Horary");
        on.Input.IncludeMinorPoints.Should().BeTrue("the input carries the switch; the engine leaves the five out");
        vm.StatusText.Should().Be("Points on: a horary chart never carries the asteroids or Lilith.");

        vm.ChartEntry.IncludeMinorPoints = false;

        engine.Inputs.Should().HaveCount(3);
        vm.StatusText.Should().Be("Points off: a horary chart never carries the asteroids or Lilith.");
    }

    /// <summary>
    /// When the engine throws on the recast, the chart on screen is left as it was, the box is put back to the
    /// chart's value, and the status line reports the message and says where the box went (1.6.1, item E,
    /// decision 6, branch ElectionalPicker). The reset re-enters the recast, which returns at once: the chart
    /// equals the box again, so nothing is cast. Until 1.6.1 the box kept its value, so the chart disagreed with
    /// it until a tick recast it or a Calculate.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task TogglingPoints_WhenTheEngineThrows_KeepsTheChartAndPutsTheBoxBack()
    {
        RecordingChartEngine engine = new();
        MainViewModel vm = CreateViewModel(chartEngine: engine);
        vm.LoadChartCommand.Execute(CreateTestChartEntity());
        await WaitAsync(vm.LoadChartCommand);
        ChartResult before = vm.LastResult
            ?? throw new InvalidOperationException($"Load cast no chart; status: {vm.StatusText}");
        engine.ThrowOnCalculate = true;

        vm.ChartEntry.IncludeMinorPoints = true;

        engine.Inputs.Should().HaveCount(2, "the engine was asked to recast");
        vm.LastResult.Should().BeSameAs(before, "the chart on screen is left as it was");
        vm.HasResult.Should().BeTrue();
        vm.StatusText.Should().Be("Points error: " + RecordingChartEngine.ThrowMessage + "; the box is back to off.");
        vm.ChartEntry.IncludeMinorPoints.Should().BeFalse("the box is back to the chart's value");
        engine.Inputs.Should().HaveCount(2, "the reset recasts nothing");
    }

    #endregion

}
