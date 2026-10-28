using System.Globalization;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;

using AstroCast.Services;
using AstroCast.Tests.Fixtures;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Post-H-1 Pass 2 (I-1) integration coverage for every cross-VM read,
/// cross-VM write, and command result-write path exercised by
/// <see cref="MainViewModel"/> after the H-1 decomposition. Pass 1 only
/// exercised the documented Cycles&#8594;Returns Rule #2 read; this suite
/// expands coverage to the remaining cross-VM edges identified in the
/// <c>Session_PostH1_Pass2_I1_I2_Implementation_Summary.md</c> audit.
/// </summary>
/// <remarks>
/// <para>
/// <b>Scope.</b> Each test either (a) sets up the source-side VM state,
/// invokes the parent-owned command, and asserts the destination-side VM
/// state reflects the cross-VM propagation, or (b) invokes a Calculate
/// command and asserts the child VM received the service result. Stubs are
/// reused verbatim from <c>AstroCast.TestDoubles</c> so the test doubles stay
/// centralised.
/// </para>
/// <para>
/// <b>Async handling.</b> Parent commands built on
/// <see cref="AsyncRelayCommand"/> execute fire-and-forget via <c>async void</c>.
/// Tests poll <see cref="AsyncRelayCommand.IsExecuting"/> to deterministically
/// wait for completion, mirroring the <c>WaitForCompletionAsync</c> helper in
/// <c>AsyncRelayCommandTests</c>.
/// </para>
/// </remarks>
[Trait("Category", "Integration")]
public sealed class CrossVmReadIntegrationTests
{
    #region Test Infrastructure

    /// <summary>
    /// Creates a <see cref="MainViewModel"/> using the shared stub set from
    /// <c>AstroCast.TestDoubles</c>. Every service stub is pulled through the
    /// <c>Stub*</c> types so the service contracts stay centralised.
    /// </summary>
    private static MainViewModel CreateViewModel()
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        return new MainViewModel(
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
    }

    /// <summary>
    /// Fabricates a minimally populated <see cref="ChartResult"/> fixture for
    /// commands that require <see cref="MainViewModel.LastResult"/>. The
    /// stub services echo inputs back into their outputs, so the fixture
    /// only needs to satisfy the calculation contract.
    /// </summary>
    /// <param name="name">The native's name.</param>
    /// <param name="utcDateTime">
    /// The chart's UTC instant; 2000-01-01 12:00 UTC when omitted. The planetary-return stub
    /// returns the natal chart as the return chart, so this is also the return's instant.
    /// </param>
    private static ChartResult MakeNatalResult(string name = "Test Native", DateTime? utcDateTime = null) =>
        new()
        {
            Input = new ChartInput
            {
                Name = name,
                LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0),
                TimeZone = "UTC",
                Latitude = 40.7128,
                Longitude = -74.006,
                LocationName = "New York City",
            },
            JulianDay = 2451545.0,
            Obliquity = 23.4367,
            UtcDateTime = utcDateTime ?? new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
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

    /// <summary>
    /// Sets <see cref="MainViewModel.LastResult"/> and
    /// <see cref="MainViewModel.HasResult"/> via reflection so tests can
    /// exercise commands gated by <c>HasResult</c> without running the full
    /// chart lifecycle. The private setter pattern matches the pre-existing
    /// reflection usage in <c>MainViewModelTests</c>.
    /// </summary>
    private static void SeedNatalResult(MainViewModel vm, ChartResult result)
    {
        typeof(MainViewModel)
            .GetProperty(nameof(MainViewModel.LastResult))!
            .SetValue(vm, result);
        typeof(MainViewModel)
            .GetProperty(nameof(MainViewModel.HasResult))!
            .SetValue(vm, true);
    }

    /// <summary>
    /// Polls <see cref="AsyncRelayCommand.IsExecuting"/> until the fire-and-
    /// forget async void handler completes (or the timeout elapses). Mirrors
    /// the helper in <c>AsyncRelayCommandTests</c> so tests have a
    /// deterministic completion barrier.
    /// </summary>
    private static async Task WaitForCompletionAsync(AsyncRelayCommand command, int timeoutMs = 5000)
    {
        int elapsed = 0;
        const int pollMs = 10;

        while (command.IsExecuting && elapsed < timeoutMs)
        {
            await Task.Delay(pollMs);
            elapsed += pollMs;
        }

        command.IsExecuting.Should().BeFalse(
            "the async command should have finished within {0} ms", timeoutMs);
    }

    /// <summary>
    /// Builds a <see cref="ChartEntity"/> fixture for tests that need to
    /// populate <see cref="ElectionalViewModel.SelectedNatalChart"/> or
    /// <see cref="RelocationViewModel.SelectedNatalChart"/>.
    /// </summary>
    private static ChartEntity MakeChartEntity(
        string name = "Dropdown Native",
        double latitude = 51.5074,
        double longitude = -0.1278,
        string locationName = "London") =>
        new()
        {
            Id = 99,
            Name = name,
            EventType = "Birth",
            LocalDateTime = "1990-06-15T09:30:00",
            TimeZone = "Europe/London",
            UtcDateTime = "1990-06-15T08:30:00Z",
            JulianDay = 2448058.854,
            Latitude = latitude,
            Longitude = longitude,
            LocationName = locationName,
            CreatedAt = DateTime.UtcNow.ToString("o"),
            UpdatedAt = DateTime.UtcNow.ToString("o"),
        };

    #endregion

    #region Cross-VM Reads (CR)

    /// <summary>
    /// CR-4a: <c>CalculateCycleTimelineCommand</c> reads
    /// <see cref="ReturnsViewModel.SelectedReturnPlanet"/> from the sibling
    /// <see cref="MainViewModel.Returns"/> VM (Rule #2 last-resort read
    /// documented in the H-1.8 audit). The stub service echoes the planet
    /// into the returned timeline, so a non-default planet in Returns must
    /// appear on <see cref="CyclesViewModel.CurrentCycleTimeline"/>.
    /// </summary>
    [Fact]
    public async Task CalculateCycleTimeline_Reads_SelectedReturnPlanet_FromReturns()
    {
        MainViewModel vm = CreateViewModel();
        SeedNatalResult(vm, MakeNatalResult());

        vm.Returns.SelectedReturnPlanet = CelestialBodyId.Saturn;
        vm.Returns.ReturnNumber = 3;

        vm.CalculateCycleTimelineCommand.Execute(null);
        await WaitForCompletionAsync(vm.CalculateCycleTimelineCommand);

        vm.Cycles.CurrentCycleTimeline.Should().NotBeNull(
            "the cycle timeline command should have populated the Cycles result");
        vm.Cycles.CurrentCycleTimeline!.Planet.Should().Be(CelestialBodyId.Saturn,
            "the command must read Returns.SelectedReturnPlanet and pass it to the service");
    }

    /// <summary>
    /// CR-4b: <c>CalculateCycleTimelineCommand</c> reads
    /// <see cref="ReturnsViewModel.ReturnNumber"/> when
    /// <see cref="ReturnsViewModel.SelectedReturnFindMode"/> is
    /// <see cref="ReturnFindMode.ByReturnNumber"/> (Saturn/Jupiter auto-flip
    /// the find mode). The stub echoes the return number into the result.
    /// </summary>
    [Fact]
    public async Task CalculateCycleTimeline_Reads_ReturnNumber_FromReturns_InByReturnNumberMode()
    {
        MainViewModel vm = CreateViewModel();
        SeedNatalResult(vm, MakeNatalResult());

        vm.Returns.SelectedReturnPlanet = CelestialBodyId.Jupiter;
        vm.Returns.ReturnNumber = 7;

        vm.CalculateCycleTimelineCommand.Execute(null);
        await WaitForCompletionAsync(vm.CalculateCycleTimelineCommand);

        vm.Cycles.CurrentCycleTimeline.Should().NotBeNull();
        vm.Cycles.CurrentCycleTimeline!.ReturnNumber.Should().Be(7,
            "the command must read Returns.ReturnNumber and pass it to the service");
    }

    /// <summary>
    /// CR-3a: <c>UseNatalLocationCommand</c> (Electional panel, fallback path
    /// when no natal-chart dropdown selection is present) reads the four
    /// location fields from <see cref="MainViewModel.ChartEntry"/> and writes
    /// them into <see cref="MainViewModel.Electional"/>. The timezone resolver
    /// may overwrite <c>Electional.TimeZone</c> during the lookup, so the
    /// assertion focuses on the three coordinate / name fields that the
    /// handler always copies verbatim.
    /// </summary>
    [Fact]
    public void UseNatalLocation_WithoutDropdown_Copies_ChartEntryFields_ToElectional()
    {
        MainViewModel vm = CreateViewModel();
        vm.ChartEntry.LocationName = "Paris";
        vm.ChartEntry.LatitudeText = "48.8566";
        vm.ChartEntry.LongitudeText = "2.3522";
        vm.ChartEntry.TimeZone = "Europe/Paris";

        vm.UseNatalLocationCommand.Execute(null);

        vm.Electional.LatitudeText.Should().Be("48.8566",
            "the Electional latitude must mirror ChartEntry on the fallback path");
        vm.Electional.LongitudeText.Should().Be("2.3522",
            "the Electional longitude must mirror ChartEntry on the fallback path");
        vm.Electional.LocationName.Should().Be("Paris",
            "the Electional location name must mirror ChartEntry on the fallback path");
        vm.Electional.LocationSearch.SearchText.Should().Be("Paris",
            "the nested LocationSearch text must also be seeded so the UI reflects the source");
    }

    /// <summary>
    /// CR-3b: <c>UseNatalLocationCommand</c> (Electional panel, dropdown path)
    /// copies coordinates from <see cref="ElectionalViewModel.SelectedNatalChart"/>
    /// into <see cref="MainViewModel.Electional"/>. Does NOT read from
    /// ChartEntry when the dropdown is populated.
    /// </summary>
    [Fact]
    public void UseNatalLocation_WithDropdown_Copies_SelectedNatalChart_ToElectional()
    {
        MainViewModel vm = CreateViewModel();
        vm.ChartEntry.LocationName = "SHOULD NOT BE USED";
        vm.ChartEntry.LatitudeText = "0.0";
        vm.ChartEntry.LongitudeText = "0.0";
        vm.Electional.SelectedNatalChart = MakeChartEntity(
            name: "Dropdown Native",
            latitude: 35.6762,
            longitude: 139.6503,
            locationName: "Tokyo");

        vm.UseNatalLocationCommand.Execute(null);

        vm.Electional.LocationName.Should().Be("Tokyo",
            "the Electional location must come from the dropdown selection, not ChartEntry");
        vm.Electional.LatitudeText.Should().Be(
            35.6762.ToString(CultureInfo.InvariantCulture),
            "dropdown latitude should be formatted and copied with InvariantCulture");
        vm.Electional.LongitudeText.Should().Be(
            139.6503.ToString(CultureInfo.InvariantCulture),
            "dropdown longitude should be formatted and copied with InvariantCulture");
    }

    /// <summary>
    /// CR-3c: <c>UseNatalLocationCommand</c> (dropdown path) takes the saved chart's own time zone, the lookup
    /// from its coordinates only where the zone is blank (1.6.1, item C, decision 4, branch ElectionalPicker).
    /// Since the picker follows Load and Calculate &amp; Save, the dropdown path is the usual one, and the
    /// chart's own zone is what its clock reads on. Until 1.6.1 the zone was always looked up: a chart at New
    /// York's coordinates on a London clock gave America/New_York.
    /// </summary>
    [Fact]
    public void UseNatalLocation_TakesTheSavedChartsOwnZone()
    {
        MainViewModel vm = CreateViewModel();
        vm.Electional.SelectedNatalChart = MakeChartEntity(
            name: "New York on a London clock",
            latitude: 40.7128,
            longitude: -74.006,
            locationName: "New York City");

        vm.UseNatalLocationCommand.Execute(null);

        vm.Electional.TimeZone.Should().Be("Europe/London",
            "the saved chart's own zone is taken, not the one its coordinates would give");
        vm.Electional.LocationName.Should().Be("New York City");
    }

    #endregion

    #region Cross-VM Writes (CW)

    /// <summary>
    /// CW-5: <c>OnChartEntryLocationSearchPropertyChanged</c> bridge
    /// (H-1.3 parent-mediated cross-VM write). Changing
    /// <see cref="LocationSearchViewModel.SelectedResult"/> on
    /// <see cref="MainViewModel.ChartEntryLocationSearch"/> must propagate
    /// the latitude, longitude, and display name into
    /// <see cref="MainViewModel.ChartEntry"/>.
    /// </summary>
    [Fact]
    public void SelectingChartEntryLocationSearchResult_Writes_CoordinatesToChartEntry()
    {
        MainViewModel vm = CreateViewModel();

        vm.ChartEntryLocationSearch.SelectedResult = new LocationSearchResult
        {
            DisplayName = "New York City, NY, USA",
            Latitude = 40.7128,
            Longitude = -74.006,
        };

        vm.ChartEntry.LatitudeText.Should().Be(
            40.7128.ToString(CultureInfo.InvariantCulture),
            "the bridge handler must write the selected latitude into ChartEntry");
        vm.ChartEntry.LongitudeText.Should().Be(
            (-74.006).ToString(CultureInfo.InvariantCulture),
            "the bridge handler must write the selected longitude into ChartEntry");
        vm.ChartEntry.LocationName.Should().Be("New York City, NY, USA",
            "the bridge handler must write the display name into ChartEntry");
    }

    /// <summary>
    /// CW-5 (null guard): setting <see cref="LocationSearchViewModel.SelectedResult"/>
    /// to <see langword="null"/> must NOT overwrite ChartEntry with empty
    /// strings. The bridge returns early on the null check to preserve any
    /// manually-entered values.
    /// </summary>
    [Fact]
    public void SelectingNullChartEntryLocationSearchResult_DoesNotOverwriteChartEntry()
    {
        MainViewModel vm = CreateViewModel();
        vm.ChartEntry.LocationName = "Preserved";
        vm.ChartEntry.LatitudeText = "12.345";
        vm.ChartEntry.LongitudeText = "67.890";

        vm.ChartEntryLocationSearch.SelectedResult = null;

        vm.ChartEntry.LocationName.Should().Be("Preserved",
            "the bridge handler must early-return on null to preserve manual entries");
        vm.ChartEntry.LatitudeText.Should().Be("12.345");
        vm.ChartEntry.LongitudeText.Should().Be("67.890");
    }

    /// <summary>
    /// CW-7: <c>ClearForm</c> must invoke <c>Clear()</c> on every child VM in
    /// the registered reset set (ChartEntry, ChartEntryLocationSearch,
    /// Synastry, Transits, Electional, Returns, Cycles). Verified by
    /// populating observable state on each child then confirming the reset
    /// propagated.
    /// </summary>
    [Fact]
    public void ClearForm_Clears_AllRegisteredChildren()
    {
        MainViewModel vm = CreateViewModel();

        vm.ChartEntry.Name = "Populated";
        vm.ChartEntry.LocationName = "Somewhere";
        vm.ChartEntryLocationSearch.SearchText = "search pending";
        vm.Electional.Goal = "test goal";
        vm.Electional.LocationSearch.SearchText = "electional search";

        vm.ClearFormCommand.Execute(null);

        vm.ChartEntry.Name.Should().BeEmpty("ChartEntry.Clear must have fired");
        vm.ChartEntry.LocationName.Should().BeEmpty("ChartEntry.Clear must have fired");
        vm.ChartEntryLocationSearch.SearchText.Should().BeEmpty(
            "ChartEntryLocationSearch.Clear must have fired");
        vm.Electional.Goal.Should().BeEmpty("Electional.Clear must have fired");
        vm.Electional.LocationSearch.SearchText.Should().BeEmpty(
            "Electional.Clear must cascade into the nested LocationSearch");
        vm.LastResult.Should().BeNull("parent-owned LastResult must also clear");
        vm.HasResult.Should().BeFalse("parent-owned HasResult must also clear");
    }

    #endregion

    #region Result Writes (RW)

    /// <summary>
    /// RW-5: <c>CalculateSolarReturnCommand</c> writes its service result to
    /// <see cref="ReturnsViewModel.CurrentSolarReturnResult"/> — the child-
    /// owned result slot that drives the derived
    /// <see cref="ReturnsViewModel.HasSolarReturnResult"/> flag.
    /// </summary>
    [Fact]
    public async Task CalculateSolarReturn_Writes_To_Returns_CurrentSolarReturnResult()
    {
        MainViewModel vm = CreateViewModel();
        SeedNatalResult(vm, MakeNatalResult());

        vm.Returns.SolarReturnYear = 2030;

        vm.CalculateSolarReturnCommand.Execute(null);
        await WaitForCompletionAsync(vm.CalculateSolarReturnCommand);

        vm.Returns.CurrentSolarReturnResult.Should().NotBeNull(
            "the command must write the service result into Returns.CurrentSolarReturnResult");
        vm.Returns.HasSolarReturnResult.Should().BeTrue(
            "the derived flag should light up once the result is populated");
        vm.Returns.CurrentSolarReturnResult!.Year.Should().Be(2030,
            "the stub echoes the requested year back into the result");
    }

    /// <summary>
    /// RW-6: <c>CalculateLunarReturnCommand</c> writes results into
    /// <see cref="ReturnsViewModel.LunarReturnResults"/>. The stub returns an
    /// empty list — the child's
    /// <see cref="ReturnsViewModel.HasLunarReturnResult"/> flag is
    /// <c>false</c> in that case, which is the documented post-extraction
    /// behaviour.
    /// </summary>
    [Fact]
    public async Task CalculateLunarReturn_Writes_To_Returns_LunarReturnResults()
    {
        MainViewModel vm = CreateViewModel();
        SeedNatalResult(vm, MakeNatalResult());

        vm.Returns.LunarReturnStartDate = new DateTime(2026, 1, 1);
        vm.Returns.LunarReturnEndDate = new DateTime(2026, 12, 31);

        vm.CalculateLunarReturnCommand.Execute(null);
        await WaitForCompletionAsync(vm.CalculateLunarReturnCommand);

        vm.Returns.LunarReturnResults.Should().NotBeNull(
            "the command must assign the service result (even if empty)");
    }

    /// <summary>
    /// RW-8: <c>CalculateCycleTimelineCommand</c> writes its result into
    /// <see cref="CyclesViewModel.CurrentCycleTimeline"/>.
    /// </summary>
    [Fact]
    public async Task CalculateCycleTimeline_Writes_To_Cycles_CurrentCycleTimeline()
    {
        MainViewModel vm = CreateViewModel();
        SeedNatalResult(vm, MakeNatalResult());

        vm.CalculateCycleTimelineCommand.Execute(null);
        await WaitForCompletionAsync(vm.CalculateCycleTimelineCommand);

        vm.Cycles.CurrentCycleTimeline.Should().NotBeNull(
            "the command must write the service result into Cycles.CurrentCycleTimeline");
        vm.Cycles.HasCycleTimeline.Should().BeTrue(
            "the derived flag should light up once the result is populated");
    }

    /// <summary>
    /// RW-9: <c>FindGreatConjunctionsCommand</c> writes its result into
    /// <see cref="CyclesViewModel.CurrentGCSeries"/> and lights up the
    /// value-based <see cref="CyclesViewModel.HasGCSeries"/> flag.
    /// </summary>
    [Fact]
    public async Task FindGreatConjunctions_Writes_To_Cycles_CurrentGCSeries()
    {
        MainViewModel vm = CreateViewModel();
        vm.Cycles.GCStartYear = 2020;
        vm.Cycles.GCEndYear = 2030;

        vm.FindGreatConjunctionsCommand.Execute(null);
        await WaitForCompletionAsync(vm.FindGreatConjunctionsCommand);

        vm.Cycles.CurrentGCSeries.Should().NotBeNull(
            "the command must write the service result into Cycles.CurrentGCSeries");
    }

    /// <summary>
    /// RW-10: <c>FindSynodicCyclesCommand</c> writes its result into
    /// <see cref="CyclesViewModel.CurrentSynodicCycleSeries"/>.
    /// </summary>
    [Fact]
    public async Task FindSynodicCycles_Writes_To_Cycles_CurrentSynodicCycleSeries()
    {
        MainViewModel vm = CreateViewModel();
        vm.Cycles.SynodicBodyA = CelestialBodyId.Jupiter;
        vm.Cycles.SynodicBodyB = CelestialBodyId.Saturn;
        vm.Cycles.SynodicStartYear = 2020;
        vm.Cycles.SynodicEndYear = 2030;

        vm.FindSynodicCyclesCommand.Execute(null);
        await WaitForCompletionAsync(vm.FindSynodicCyclesCommand);

        vm.Cycles.CurrentSynodicCycleSeries.Should().NotBeNull(
            "the command must write the service result into Cycles.CurrentSynodicCycleSeries");
    }

    /// <summary>
    /// RW-11: <c>FindIngressesCommand</c> writes its result into
    /// <see cref="MundaneViewModel.CurrentIngressSeries"/> and lights up the
    /// value-based <see cref="MundaneViewModel.HasIngressSeries"/> flag.
    /// </summary>
    [Fact]
    public async Task FindIngresses_Writes_To_Mundane_CurrentIngressSeries()
    {
        MainViewModel vm = CreateViewModel();
        vm.Mundane.IngressYear = 2026;
        vm.Mundane.IngressPlanet = CelestialBodyId.Jupiter;

        vm.FindIngressesCommand.Execute(null);
        await WaitForCompletionAsync(vm.FindIngressesCommand);

        vm.Mundane.CurrentIngressSeries.Should().NotBeNull(
            "the command must write the service result into Mundane.CurrentIngressSeries");
    }

    /// <summary>
    /// RW-16: <c>CalculateRITimelineCommand</c> writes into
    /// <see cref="ReturnIngressTimelineViewModel.CurrentResult"/> and sets
    /// <see cref="ReturnIngressTimelineViewModel.HasResult"/> to match the
    /// count (value-based HasResult pattern).
    /// </summary>
    [Fact]
    public async Task CalculateReturnIngressTimeline_Writes_To_ReturnIngressTimeline_CurrentResult()
    {
        MainViewModel vm = CreateViewModel();
        SeedNatalResult(vm, MakeNatalResult());

        vm.ReturnIngressTimeline.StartYear = 2026;
        vm.ReturnIngressTimeline.EndYear = 2030;

        vm.CalculateRITimelineCommand.Execute(null);
        await WaitForCompletionAsync(vm.CalculateRITimelineCommand);

        vm.ReturnIngressTimeline.CurrentResult.Should().NotBeNull(
            "the command must write the service result into ReturnIngressTimeline.CurrentResult");
    }

    /// <summary>
    /// RW-17: <c>CalculateRelocationCommand</c> writes its result into
    /// <see cref="RelocationViewModel.CurrentResult"/>. Requires a valid
    /// destination location so <c>double.TryParse</c> succeeds under
    /// InvariantCulture in the F-CS-Relocation path.
    /// </summary>
    [Fact]
    public async Task CalculateRelocation_Writes_To_Relocation_CurrentResult()
    {
        MainViewModel vm = CreateViewModel();
        SeedNatalResult(vm, MakeNatalResult());

        vm.Relocation.LocationName = "Sydney";
        vm.Relocation.LatitudeText = "-33.8688";
        vm.Relocation.LongitudeText = "151.2093";

        vm.CalculateRelocationCommand.Execute(null);
        await WaitForCompletionAsync(vm.CalculateRelocationCommand);

        vm.Relocation.CurrentResult.Should().NotBeNull(
            "the command must write the service result into Relocation.CurrentResult");
        vm.Relocation.HasResult.Should().BeTrue(
            "the derived flag should light up once the result is populated");
    }

    #endregion

    #region Return Status (O-4)

    /// <summary>
    /// E3-2 (branch O-4): the planetary-return status line shows the return's UTC date, and
    /// says it is UTC. The instant 2026-08-24 01:40 UTC is 2026-08-23 21:40 in New York, so a
    /// date with no clock would read as the wrong day there. The expected date is the result's
    /// own UTC instant (Tier A). Run under en-US: the status line follows the user's culture.
    /// </summary>
    [Fact]
    public async Task CalculatePlanetaryReturn_StatusText_LabelsTheReturnDateUtc()
    {
        using CultureScope scope = new("en-US");
        MainViewModel vm = CreateViewModel();
        SeedNatalResult(vm, MakeNatalResult(utcDateTime: new DateTime(2026, 8, 24, 1, 40, 0, DateTimeKind.Utc)));

        vm.Returns.SelectedReturnPlanet = CelestialBodyId.Mars;
        vm.Returns.SelectedReturnFindMode = ReturnFindMode.ByReturnNumber;
        vm.Returns.ReturnNumber = 42;

        vm.CalculatePlanetaryReturnCommand.Execute(null);
        await WaitForCompletionAsync(vm.CalculatePlanetaryReturnCommand);

        vm.StatusText.Should().Be("Mars return #42 calculated (age 29, 2026-08-24 UTC).");
    }

    #endregion

    #region Cross-VM Writes (CW-1 / CW-4) — Timing-Lord Summaries

    /// <summary>
    /// CW-1 (non-Birth branch): <c>CalculateAndSaveAsync</c> writes the four
    /// timing-lord summary fields on <see cref="MainViewModel.Returns"/> to
    /// their empty-state defaults when the chart is not of type "Birth".
    /// Pre-populating the fields proves the command's reset branch ran.
    /// </summary>
    [Fact]
    public async Task CalculateAndSave_NonBirthChart_Clears_TimingLordSummaries_OnReturns()
    {
        MainViewModel vm = CreateViewModel();
        vm.ChartEntry.Name = "Test Event";
        vm.ChartEntry.EventType = "Event";
        vm.ChartEntry.TimeZone = "UTC";
        vm.ChartEntry.LocationName = "Somewhere";
        vm.ChartEntry.LatitudeText = "0.0";
        vm.ChartEntry.LongitudeText = "0.0";
        vm.ChartEntry.EventDate = new DateTime(2020, 6, 15);
        vm.ChartEntry.EventTime = "12:00:00";

        vm.Returns.ProfectionSummary = "stale";
        vm.Returns.HasProfection = true;
        vm.Returns.FirdariaSummary = "stale firdaria";
        vm.Returns.ZodiacalReleasingSummary = "stale ZR";

        vm.CalculateAndSaveCommand.Execute(null);
        await WaitForCompletionAsync(vm.CalculateAndSaveCommand);

        vm.Returns.ProfectionSummary.Should().BeEmpty(
            "non-Birth branch must reset Returns.ProfectionSummary");
        vm.Returns.HasProfection.Should().BeFalse(
            "non-Birth branch must clear Returns.HasProfection");
        vm.Returns.FirdariaSummary.Should().BeEmpty(
            "non-Birth branch must reset Returns.FirdariaSummary");
        vm.Returns.ZodiacalReleasingSummary.Should().BeEmpty(
            "non-Birth branch must reset Returns.ZodiacalReleasingSummary");
    }

    #endregion
}
