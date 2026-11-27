using System.Collections.Specialized;
using System.Windows.Input;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Models.Transit;
using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// A panel's result does not outlive the chart it was made from (branch StaleResults, 1.7.2, finding N2): every
/// result made from the Chart panel's chart is cleared when that chart changes, as the Relocation panel's has
/// been since 1.6.1, and the Synastry panel's three are cleared too when Person B changes.
/// </summary>
/// <remarks>
/// <para>
/// Until 1.7.2 only the Relocation panel cleared its result. The other panels kept theirs, their Export buttons
/// stayed enabled, and an export wrote the chart held in the old result: a user who listed directions for one
/// chart, loaded another and pressed Export Directions MD got the first chart's report.
/// </para>
/// <para>
/// The charts are invented (Alpha, Beta, Gamma, each person A's data under another name), the engines are
/// doubles that answer at once, and every result is made by its panel's own Calculate command, so each test
/// starts from what a user would have on screen.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class StaleResultsTests
{
    /// <summary>The results made from the Chart panel's chart, by the names <see cref="Harness"/> knows.</summary>
    private static readonly string[] ChartMadeResults =
    [
        "Directions", "Transits", "Synastry", "Composite", "Davison", "SolarReturn", "LunarReturn",
        "PlanetaryReturn", "CycleTimeline", "ReturnIngressTimeline", "Astrocartography", "Electional",
    ];

    /// <summary>The four ways the Chart panel's chart changes.</summary>
    private static readonly string[] ChartChanges = ["Load", "CalculateAndSave", "PointsRecast", "ClearForm"];

    /// <summary>Gets every chart-made result paired with every change of the Chart panel's chart.</summary>
    public static TheoryData<string, string> ResultsAndChanges
    {
        get
        {
            TheoryData<string, string> data = [];
            foreach (string result in ChartMadeResults)
            {
                foreach (string change in ChartChanges)
                    data.Add(result, change);
            }

            return data;
        }
    }

    #region The Chart panel's chart changes

    /// <summary>
    /// Each result made from the Chart panel's chart is gone, and its exports cannot run, once that chart
    /// changes: by a Load of another chart, by Calculate &amp; Save, by the Points recast and by Clear Form.
    /// </summary>
    /// <param name="result">The result.</param>
    /// <param name="change">The change of the Chart panel's chart.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [MemberData(nameof(ResultsAndChanges))]
    public async Task AChartPanelChange_ClearsTheResultAndDisablesItsExports(string result, string change)
    {
        Harness harness = await Harness.WithAlphaLoadedAsync();
        await harness.CalculateAsync(result);

        await harness.ChangeTheChartAsync(change);

        harness.Holds(result).Should().BeFalse($"{change} changed the Chart panel's chart");
        foreach (ICommand export in harness.Exports(result))
            export.CanExecute(null).Should().BeFalse($"{result} has no result after {change}");
    }

    /// <summary>What a panel shows of its result is emptied with it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AChartPanelChange_EmptiesWhatThePanelsShow()
    {
        Harness harness = await Harness.WithAlphaLoadedAsync();
        MainViewModel vm = harness.Main;
        await harness.CalculateAsync("Directions");
        await harness.CalculateAsync("LunarReturn");
        await harness.CalculateAsync("Astrocartography");
        vm.Astrocartography.CrossingReportText = "Crossing Report: 51.51\u00b0N, 0.13\u00b0W";
        vm.Directions.ResultSummary.Should().NotBeEmpty();
        vm.Directions.ListText.Should().NotBeEmpty();
        vm.Returns.SelectedLunarReturn.Should().NotBeNull();

        await harness.ChangeTheChartAsync("Load");

        vm.Directions.ResultSummary.Should().BeEmpty();
        vm.Directions.ListText.Should().BeEmpty();
        vm.Returns.LunarReturnResults.Should().BeNull();
        vm.Returns.SelectedLunarReturn.Should().BeNull();
        vm.Astrocartography.VisibleLines.Should().BeEmpty();
        vm.Astrocartography.CrossingReportText.Should().BeEmpty();
        vm.Astrocartography.HasResult.Should().BeFalse();
        vm.ReturnIngressTimeline.HasResult.Should().BeFalse();
    }

    /// <summary>
    /// A result made for the chart now on the Chart panel is kept by what does not change that chart: a search,
    /// the sort toggle and a reload of the saved-charts lists.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task WhatDoesNotChangeTheChart_KeepsEveryResult()
    {
        Harness harness = await Harness.WithAlphaLoadedAsync();
        foreach (string result in ChartMadeResults)
            await harness.CalculateAsync(result);

        await Harness.RunAsync(harness.Main.SearchChartsCommand);
        await Harness.RunAsync(harness.Main.ToggleSortDirectionCommand);
        await harness.Main.InitializeAsync();

        foreach (string result in ChartMadeResults)
            harness.Holds(result).Should().BeTrue($"{result} was made for the chart still on the Chart panel");
    }

    /// <summary>
    /// The series that are made from no chart survive every change of the Chart panel's chart short of Clear
    /// Form: great conjunctions, synodic cycles, ingresses, lunations, mundane aspects and eclipses.
    /// </summary>
    /// <param name="change">The change of the Chart panel's chart.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("Load")]
    [InlineData("CalculateAndSave")]
    [InlineData("PointsRecast")]
    public async Task AChartPanelChange_LeavesTheMundaneSeries(string change)
    {
        Harness harness = await Harness.WithAlphaLoadedAsync();
        MainViewModel vm = harness.Main;
        await Harness.RunAsync(vm.FindGreatConjunctionsCommand);
        await Harness.RunAsync(vm.FindSynodicCyclesCommand);
        await Harness.RunAsync(vm.FindIngressesCommand);
        await Harness.RunAsync(vm.FindLunationsCommand);
        await Harness.RunAsync(vm.FindMundaneAspectsCommand);
        await Harness.RunAsync(vm.FindEclipsesCommand);

        // The stub engines find nothing, so each series is empty and its export is off; the flags are set as a
        // series that found something sets them
        vm.Cycles.HasGCSeries = true;
        vm.Cycles.HasSynodicCycleSeries = true;
        vm.Mundane.HasIngressSeries = true;
        vm.Mundane.HasLunationSeries = true;
        vm.Mundane.HasMASeries = true;
        vm.Mundane.HasEclipseSeries = true;
        object?[] before =
        [
            vm.Cycles.CurrentGCSeries, vm.Cycles.CurrentSynodicCycleSeries, vm.Mundane.CurrentIngressSeries,
            vm.Mundane.CurrentLunationSeries, vm.Mundane.CurrentMASeries, vm.Mundane.CurrentEclipseSeries,
        ];
        before.Should().NotContainNulls(vm.StatusText);

        await harness.ChangeTheChartAsync(change);

        object?[] after =
        [
            vm.Cycles.CurrentGCSeries, vm.Cycles.CurrentSynodicCycleSeries, vm.Mundane.CurrentIngressSeries,
            vm.Mundane.CurrentLunationSeries, vm.Mundane.CurrentMASeries, vm.Mundane.CurrentEclipseSeries,
        ];
        after.Should().Equal(before, "a mundane series is made from no chart");
        ICommand[] exports =
        [
            vm.ExportGCSeriesMarkdownCommand, vm.ExportSynodicCycleMdCommand, vm.ExportIngressSeriesMarkdownCommand,
            vm.ExportLunationSeriesCommand, vm.ExportMASeriesCommand, vm.ExportEclipseSeriesCommand,
        ];
        foreach (ICommand export in exports)
            export.CanExecute(null).Should().BeTrue();
    }

    #endregion

    #region Person B and the Electional picker

    /// <summary>A change of Person B to another chart clears the Synastry panel's three results.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APersonBChange_ClearsTheThreeSynastryResults()
    {
        Harness harness = await Harness.WithTheThreeSynastryResultsAsync();
        MainViewModel vm = harness.Main;

        vm.Synastry.PersonB = vm.AllSavedCharts.Single(c => c.Name == "Gamma");

        AssertTheThreeSynastryResults(vm, held: false);
    }

    /// <summary>
    /// A refresh of the saved-charts lists re-points the Person B picker at a new object for the same chart, and
    /// clears nothing: the chart's identity is its Id.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ARefreshThatKeepsPersonB_ClearsNone()
    {
        Harness harness = await Harness.WithTheThreeSynastryResultsAsync();
        MainViewModel vm = harness.Main;
        ChartEntity before = vm.Synastry.PersonB ?? throw new InvalidOperationException("Person B is not picked.");

        await vm.InitializeAsync();

        vm.Synastry.PersonB.Should().NotBeSameAs(before, "the refresh hands out new objects");
        vm.Synastry.PersonB?.Id.Should().Be(before.Id);
        AssertTheThreeSynastryResults(vm, held: true);
    }

    /// <summary>
    /// In the window a bound picker writes "nothing picked" while its list is empty, in the middle of the
    /// refresh, before the same chart is picked again. That is the refresh, not a change of Person B, and it
    /// clears nothing; neither does it clear the electional result through the Electional picker.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ARefreshThatPassesThroughNoPick_ClearsNone()
    {
        Harness harness = await Harness.WithTheThreeSynastryResultsAsync();
        MainViewModel vm = harness.Main;
        await harness.CalculateAsync("Electional");
        int emptied = 0;
        vm.AllSavedCharts.CollectionChanged += (_, e) =>
        {
            // What each head's ComboBox does when its items are cleared: its selection goes, and its two-way
            // binding writes null to the view model
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                emptied++;
                vm.Synastry.PersonB = null;
                vm.Electional.SelectedNatalChart = null;
            }
        };

        await vm.InitializeAsync();

        emptied.Should().Be(1, "the refresh clears the list once");
        vm.Synastry.PersonB?.Name.Should().Be("Beta");
        vm.Electional.SelectedNatalChart?.Name.Should().Be("Alpha");
        AssertTheThreeSynastryResults(vm, held: true);
        harness.Holds("Electional").Should().BeTrue();
    }

    /// <summary>Deleting Person B's chart leaves no Person B, and the three results go with it.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task DeletingPersonBsChart_ClearsTheThreeSynastryResults()
    {
        Harness harness = await Harness.WithTheThreeSynastryResultsAsync();
        MainViewModel vm = harness.Main;

        await Harness.RunAsync(vm.DeleteChartCommand, vm.SavedCharts.Single(c => c.Name == "Beta"));

        vm.Synastry.PersonB.Should().BeNull(vm.StatusText);
        AssertTheThreeSynastryResults(vm, held: false);
    }

    /// <summary>Deleting another chart refreshes the lists and clears nothing.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task DeletingAnotherChart_ClearsNone()
    {
        Harness harness = await Harness.WithTheThreeSynastryResultsAsync();
        MainViewModel vm = harness.Main;

        await Harness.RunAsync(vm.DeleteChartCommand, vm.SavedCharts.Single(c => c.Name == "Gamma"));

        vm.AllSavedCharts.Should().HaveCount(2, vm.StatusText);
        AssertTheThreeSynastryResults(vm, held: true);
    }

    /// <summary>
    /// The electional result is made from the chart in the Electional panel's own picker, which follows the Chart
    /// panel as the Relocation panel's does: a pick of another chart by hand clears it.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task APickOfAnotherElectionalChart_ClearsTheElectionalResult()
    {
        Harness harness = await Harness.WithAlphaLoadedAsync();
        MainViewModel vm = harness.Main;
        await harness.CalculateAsync("Electional");
        vm.Electional.SelectedNatalChart?.Name.Should().Be("Alpha", "Load picked the loaded chart");

        vm.Electional.SelectedNatalChart = vm.AllSavedCharts.Single(c => c.Name == "Gamma");

        harness.Holds("Electional").Should().BeFalse();
        vm.ExportElectionalMarkdownCommand.CanExecute(null).Should().BeFalse();
    }

    #endregion

    #region Clear Form, and a failed run

    /// <summary>
    /// Clear Form clears the return and ingress timeline and the map, the two results it left before 1.7.2, even
    /// with no chart on the Chart panel to change; the two panels' own settings stay as they are.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ClearForm_ClearsTheReturnAndIngressTimelineAndTheMap()
    {
        Harness harness = await Harness.WithAlphaLoadedAsync();
        MainViewModel vm = harness.Main;
        ReturnIngressTimeline timeline = Harness.Timeline("Alpha");
        AcgResult map = Harness.Map(vm.LastResult ?? throw new InvalidOperationException("No chart is loaded."));
        vm.ClearFormCommand.Execute(null);
        vm.LastResult.Should().BeNull();

        // As a run that was in flight lands them, after the chart has gone
        vm.ReturnIngressTimeline.CurrentResult = timeline;
        vm.ReturnIngressTimeline.HasResult = true;
        vm.ReturnIngressTimeline.StartYear = 1990;
        vm.Astrocartography.CurrentResult = map;
        vm.Astrocartography.HasResult = true;
        vm.Astrocartography.ShowUranus = true;

        vm.ClearFormCommand.Execute(null);

        vm.ReturnIngressTimeline.CurrentResult.Should().BeNull();
        vm.ReturnIngressTimeline.HasResult.Should().BeFalse();
        vm.ExportRITimelineCommand.CanExecute(null).Should().BeFalse();
        vm.Astrocartography.CurrentResult.Should().BeNull();
        vm.Astrocartography.HasResult.Should().BeFalse();
        vm.ExportAcgMarkdownCommand.CanExecute(null).Should().BeFalse();
        vm.OpenAcgMapWindowCommand.CanExecute(null).Should().BeFalse();
        vm.ReturnIngressTimeline.StartYear.Should().Be(1990, "Clear Form clears the result, not the panel's years");
        vm.Astrocartography.ShowUranus.Should().BeTrue("Clear Form clears the result, not the map's line boxes");
    }

    /// <summary>
    /// A run that fails leaves the earlier result, as the Relocation panel's does: it is still a result for the
    /// chart on the Chart panel, since a change of that chart would have cleared it.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AFailedRun_LeavesTheEarlierResultForTheSameChart()
    {
        Harness harness = await Harness.WithAlphaLoadedAsync();
        MainViewModel vm = harness.Main;
        await harness.CalculateAsync("Directions");
        PrimaryDirectionResult? earlier = vm.Directions.CurrentResult;
        harness.Directions.Fail = true;

        await Harness.RunAsync(vm.CalculateDirectionsCommand);

        vm.StatusText.Should().Be("Primary directions error: The engine failed.");
        vm.Directions.CurrentResult.Should().BeSameAs(earlier);
        earlier?.NatalChart.Should().BeSameAs(vm.LastResult, "the result is the present chart's");
    }

    #endregion

    #region Helpers

    /// <summary>Asserts that the Synastry panel's three results are all held, or all gone, with their exports.</summary>
    /// <param name="vm">The view model.</param>
    /// <param name="held">Whether the results are expected to be held.</param>
    private static void AssertTheThreeSynastryResults(MainViewModel vm, bool held)
    {
        vm.Synastry.HasSynastryResult.Should().Be(held);
        vm.Synastry.HasCompositeResult.Should().Be(held);
        vm.Synastry.HasDavisonResult.Should().Be(held);
        vm.ExportSynastryMarkdownCommand.CanExecute(null).Should().Be(held);
        vm.ExportCompositeMarkdownCommand.CanExecute(null).Should().Be(held);
        vm.ExportDavisonMarkdownCommand.CanExecute(null).Should().Be(held);
    }

    /// <summary>
    /// A view model over three saved charts, Alpha, Beta and Gamma, whose engines are doubles that answer at
    /// once, with what the tests ask of it by a result's name.
    /// </summary>
    /// <param name="Main">The view model.</param>
    /// <param name="Directions">The primary-direction engine's double, which can be told to fail.</param>
    private sealed record Harness(MainViewModel Main, DirectionsDouble Directions)
    {
        /// <summary>Builds the view model, loads its lists and loads Alpha onto the Chart panel.</summary>
        /// <returns>The harness.</returns>
        public static async Task<Harness> WithAlphaLoadedAsync()
        {
            DirectionsDouble directions = new();
            MainViewModel main = new(
                new StubChartEngine(),
                new SavedChartsRepository("Alpha", "Beta", "Gamma"),
                new StubEphemerisService(),
                new StubSynastryService(),
                new StubCompositeService(),
                new StubSolarReturnService(),
                new LunarReturnDouble(),
                new StubPlanetaryReturnService(),
                new StubCycleTimelineService(),
                new StubGreatConjunctionService(),
                new StubIngressService(),
                new StubLunationService(),
                new StubRelocationService(),
                new StubMundaneAspectService(),
                new StubEclipseService(),
                new TimelineDouble(),
                new StubSynodicCycleService(),
                new TransitDouble(),
                new ElectionalDouble(),
                new MapDouble(),
                new TestDialogService(),
                new OperationCancellationRegistry(),
                new LoggerConfiguration().CreateLogger(),
                primaryDirectionService: directions,
                davisonService: new StubDavisonService());
            await main.InitializeAsync();
            await RunAsync(main.LoadChartCommand, main.SavedCharts.Single(c => c.Name == "Alpha"));
            main.LastResult.Should().NotBeNull(main.StatusText);

            // Person B for the Synastry panel's three, and the Electional panel's own fields
            main.Synastry.PersonB = main.AllSavedCharts.Single(c => c.Name == "Beta");
            main.Electional.Goal = "Open a shop";
            main.Electional.LocationName = "Richmond, VA";
            main.Electional.LatitudeText = "37.5385";
            main.Electional.LongitudeText = "-77.4343";
            main.Electional.TimeZone = "America/New_York";

            return new Harness(main, directions);
        }

        /// <summary>The same, with the Synastry, Composite and Davison results calculated for Alpha and Beta.</summary>
        /// <returns>The harness.</returns>
        public static async Task<Harness> WithTheThreeSynastryResultsAsync()
        {
            Harness harness = await WithAlphaLoadedAsync();
            await harness.CalculateAsync("Synastry");
            await harness.CalculateAsync("Composite");
            await harness.CalculateAsync("Davison");
            return harness;
        }

        /// <summary>Runs a command and waits for its handler to finish.</summary>
        /// <param name="command">The command.</param>
        /// <param name="parameter">The command's parameter, if it takes one.</param>
        /// <returns>A task that completes when the command has finished, and fails after 5 s.</returns>
        public static async Task RunAsync(AsyncRelayCommand command, object? parameter = null)
        {
            command.Execute(parameter);

            int waited = 0;
            while (command.IsExecuting && waited < 5000)
            {
                await Task.Delay(10);
                waited += 10;
            }

            command.IsExecuting.Should().BeFalse("the command finishes within 5 s");
        }

        /// <summary>A return and ingress timeline with one event counted, for the named chart.</summary>
        /// <param name="nativeName">The chart's name.</param>
        /// <returns>The timeline.</returns>
        public static ReturnIngressTimeline Timeline(string nativeName) => new()
        {
            NativeName = nativeName,
            Entries = [],
            ScanStart = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            ScanEnd = new DateTime(2029, 12, 31, 23, 59, 59, DateTimeKind.Utc),
            Count = 1,
            ReturnPlanets = [],
            IngressPlanets = [],
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };

        /// <summary>A map result with no lines, for the given chart.</summary>
        /// <param name="natal">The chart.</param>
        /// <returns>The map result.</returns>
        public static AcgResult Map(ChartResult natal) => new()
        {
            NatalChart = natal,
            Lines = [],
            Obliquity = 23.4367,
            GastDegrees = 0.0,
            LatitudeMin = -85.0,
            LatitudeMax = 85.0,
            LatitudeStep = 1.0,
        };

        /// <summary>Makes a result by its panel's own Calculate command and checks it is there to export.</summary>
        /// <param name="result">The result's name.</param>
        /// <returns>A task that completes when the result is held.</returns>
        public async Task CalculateAsync(string result)
        {
            await RunAsync(Calculate(result));

            Holds(result).Should().BeTrue($"{result} was calculated; status: {Main.StatusText}");
            foreach (ICommand export in Exports(result))
                export.CanExecute(null).Should().BeTrue($"{result} is there to export");
        }

        /// <summary>Changes the Chart panel's chart in one of the four ways it changes.</summary>
        /// <param name="change">"Load", "CalculateAndSave", "PointsRecast" or "ClearForm".</param>
        /// <returns>A task that completes when the chart has changed.</returns>
        public async Task ChangeTheChartAsync(string change)
        {
            ChartResult? before = Main.LastResult;
            switch (change)
            {
                case "Load":
                    await RunAsync(Main.LoadChartCommand, Main.SavedCharts.Single(c => c.Name == "Gamma"));
                    break;
                case "CalculateAndSave":
                    Main.ChartEntry.Name = "Delta";
                    await RunAsync(Main.CalculateAndSaveCommand);
                    break;
                case "PointsRecast":
                    Main.ChartEntry.IncludeMinorPoints = !Main.ChartEntry.IncludeMinorPoints;
                    break;
                case "ClearForm":
                    Main.ClearFormCommand.Execute(null);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(change), change, "Unknown change.");
            }

            Main.LastResult.Should().NotBeSameAs(before, $"{change} changes the Chart panel's chart; status: {Main.StatusText}");
        }

        /// <summary>Whether the named result is held.</summary>
        /// <param name="result">The result's name.</param>
        /// <returns><see langword="true"/> when the panel holds it.</returns>
        public bool Holds(string result) => result switch
        {
            "Directions" => Main.Directions.CurrentResult is not null || Main.Directions.HasResult,
            "Transits" => Main.Transits.TransitResult is not null || Main.Transits.HasResult,
            "Synastry" => Main.Synastry.SynastryResult is not null || Main.Synastry.HasSynastryResult,
            "Composite" => Main.Synastry.CompositeResult is not null || Main.Synastry.HasCompositeResult,
            "Davison" => Main.Synastry.DavisonResult is not null || Main.Synastry.HasDavisonResult,
            "SolarReturn" => Main.Returns.CurrentSolarReturnResult is not null || Main.Returns.HasSolarReturnResult,
            "LunarReturn" => Main.Returns.LunarReturnResults is not null || Main.Returns.SelectedLunarReturn is not null
                || Main.Returns.HasLunarReturnResult,
            "PlanetaryReturn" => Main.Returns.CurrentPlanetaryReturnResult is not null || Main.Returns.HasPlanetaryReturnResult,
            "CycleTimeline" => Main.Cycles.CurrentCycleTimeline is not null || Main.Cycles.HasCycleTimeline,
            "ReturnIngressTimeline" => Main.ReturnIngressTimeline.CurrentResult is not null || Main.ReturnIngressTimeline.HasResult,
            "Astrocartography" => Main.Astrocartography.CurrentResult is not null || Main.Astrocartography.HasResult,
            "Electional" => Main.Electional.ElectionalResult is not null || Main.Electional.HasResult,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown result."),
        };

        /// <summary>The commands that export, or open, the named result, wherever their buttons sit.</summary>
        /// <param name="result">The result's name.</param>
        /// <returns>The commands.</returns>
        public ICommand[] Exports(string result) => result switch
        {
            "Directions" => [Main.ExportDirectionsMarkdownCommand],
            "Transits" =>
            [
                Main.ExportTransitMarkdownCommand, Main.ExportDailyHoroscopeMarkdownCommand,
                Main.ExportTransitTimelineMarkdownCommand,
            ],
            "Synastry" => [Main.ExportSynastryMarkdownCommand],
            "Composite" => [Main.ExportCompositeMarkdownCommand],
            "Davison" => [Main.ExportDavisonMarkdownCommand],
            "SolarReturn" => [Main.ExportSolarReturnMarkdownCommand],
            "LunarReturn" => [Main.ExportLunarReturnMarkdownCommand],
            "PlanetaryReturn" => [Main.ExportPlanetaryReturnMarkdownCommand],
            "CycleTimeline" => [Main.ExportCycleTimelineMarkdownCommand],
            "ReturnIngressTimeline" => [Main.ExportRITimelineCommand],
            "Astrocartography" => [Main.ExportAcgMarkdownCommand, Main.OpenAcgMapWindowCommand],
            "Electional" => [Main.ExportElectionalMarkdownCommand],
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown result."),
        };

        /// <summary>The panel's own Calculate command for the named result.</summary>
        /// <param name="result">The result's name.</param>
        /// <returns>The command.</returns>
        private AsyncRelayCommand Calculate(string result) => result switch
        {
            "Directions" => Main.CalculateDirectionsCommand,
            "Transits" => Main.CalculateTransitCommand,
            "Synastry" => Main.CalculateSynastryCommand,
            "Composite" => Main.CalculateCompositeCommand,
            "Davison" => Main.CalculateDavisonCommand,
            "SolarReturn" => Main.CalculateSolarReturnCommand,
            "LunarReturn" => Main.CalculateLunarReturnCommand,
            "PlanetaryReturn" => Main.CalculatePlanetaryReturnCommand,
            "CycleTimeline" => Main.CalculateCycleTimelineCommand,
            "ReturnIngressTimeline" => Main.CalculateRITimelineCommand,
            "Astrocartography" => Main.CalculateAcgCommand,
            "Electional" => Main.CalculateElectionalCommand,
            _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unknown result."),
        };
    }

    /// <summary>A transit engine that answers at once with a result holding transits for the chart it was given.</summary>
    private sealed class TransitDouble : ITransitService
    {
        /// <inheritdoc />
        public TransitResult Calculate(TransitInput input, CancellationToken cancellationToken = default) => new()
        {
            NatalChart = input.NatalChart,
            ReportStartDate = input.ReportStartDate,
            ReportEndDate = input.ReportEndDate,
            MidpointDate = input.ReportStartDate,
            IncludeMinorAspects = input.IncludeMinorAspects,
            TransitPositions = new Dictionary<TransitBody, TransitPlanetPosition>(),
            Stations = [],
            Ingresses = [],
            ActiveTransits = [],
            HouseOverlays = [],
            MidpointActivations = [],
            MarsIncluded = false,
        };
    }

    /// <summary>A primary-direction engine that answers at once with one direction, or fails when told to.</summary>
    private sealed class DirectionsDouble : IPrimaryDirectionService
    {
        /// <summary>Gets or sets a value indicating whether the next calculation fails.</summary>
        public bool Fail { get; set; }

        /// <inheritdoc />
        public PrimaryDirectionResult Calculate(
            ChartResult natalChart, PrimaryDirectionSettings settings, CancellationToken cancellationToken = default)
        {
            if (Fail)
                throw new InvalidOperationException("The engine failed.");

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
            return new PrimaryDirectionResult { NatalChart = natalChart, Settings = settings, Hits = [hit], LeftOutCount = 0 };
        }

        /// <inheritdoc />
        public IReadOnlyList<PrimaryDirectionHit> FindHits(
            ChartResult natalChart, PrimaryDirectionSettings settings, DateOnly start, DateOnly end,
            CancellationToken cancellationToken = default) => [];
    }

    /// <summary>An electional engine that answers at once with a search that found no candidate.</summary>
    private sealed class ElectionalDouble : IElectionalService
    {
        /// <inheritdoc />
        public ElectionalResult Calculate(ElectionalInput input, CancellationToken cancellationToken = default) => new()
        {
            Input = input,
            NatalChart = input.NatalChart,
            Candidates = [],
            RequestedCandidateCount = input.CandidateCount,
            SamplesWithoutPlanetaryHour = 0,
            DisqualifiedPeriods = [],
            VoidOfCoursePeriods = [],
            CalculatedAt = DateTime.UtcNow,
        };
    }

    /// <summary>A map engine that answers at once with a result of no lines for the chart it was given.</summary>
    private sealed class MapDouble : IAstrocartographyService
    {
        /// <inheritdoc />
        public AcgResult Calculate(ChartResult natalChart, CancellationToken cancellationToken = default) =>
            Harness.Map(natalChart);

        /// <inheritdoc />
        public AcgCrossingReport GetCrossings(
            AcgResult acgResult, double latitude, double longitude, string locationName,
            double orbKilometres = AstroCast.Core.Services.AstrocartographyService.DefaultCrossingOrbKilometres) =>
            throw new NotSupportedException("No test asks the map double for crossings.");
    }

    /// <summary>A lunar return engine that answers a range search at once with one return.</summary>
    private sealed class LunarReturnDouble : ILunarReturnService
    {
        private readonly StubLunarReturnService _inner = new();

        /// <inheritdoc />
        public IReadOnlyList<LunarReturnResult> Calculate(
            ChartResult natalChart, DateTime startDate, DateTime endDate, double latitude, double longitude,
            string locationName, string timeZone, CancellationToken cancellationToken = default) =>
            [_inner.CalculateNext(natalChart, startDate, latitude, longitude, locationName, timeZone, cancellationToken)];

        /// <inheritdoc />
        public LunarReturnResult CalculateNext(
            ChartResult natalChart, DateTime onOrAfter, double latitude, double longitude,
            string locationName, string timeZone, CancellationToken cancellationToken = default) =>
            _inner.CalculateNext(natalChart, onOrAfter, latitude, longitude, locationName, timeZone, cancellationToken);
    }

    /// <summary>A return and ingress timeline engine that answers at once with one event counted.</summary>
    private sealed class TimelineDouble : IReturnIngressTimelineService
    {
        /// <inheritdoc />
        public ReturnIngressTimeline Calculate(
            ChartResult? natalChart, int startYear, int endYear, bool includeReturns = true,
            bool includeIngresses = true, CelestialBodyId[]? returnPlanets = null,
            CelestialBodyId[]? ingressPlanets = null, CancellationToken cancellationToken = default) =>
            Harness.Timeline(natalChart?.Input.Name ?? "No chart");
    }

    #endregion
}
