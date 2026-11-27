using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Core.Data;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// The Person B, Electional natal-chart and Relocation natal-chart pickers keep their charts across every
/// refresh of the saved charts; only deleting a chart empties its picker (1.4.0, branch Panels).
/// </summary>
/// <remarks>
/// <para>
/// Until 1.4.0 the three pickers bound <c>SavedCharts</c>, the saved-charts pane's filtered list, which every
/// save, search, sort, import and delete clears and refills with new instances. On the clear each ComboBox
/// dropped its item and its two-way <c>SelectedItem</c> binding wrote null to the view model: Calculate &amp;
/// Save emptied all three pickers, and the Relocation and Electional runs then fell back, silently, to the
/// chart just calculated. Found by the probe that opened piece C. The pickers now bind the unfiltered
/// <c>AllSavedCharts</c>, which only <c>RefreshSavedChartsAsync</c> refills, keeping each selection by Id, so a
/// search, a sort or the filter no longer reaches them at all.
/// </para>
/// <para>
/// Each chart is picked through its ComboBox, as a user picks it, so the test holds whichever list the box
/// binds. <see cref="SavedChartsRepository"/> hands out new instances on every read, as the database does.
/// </para>
/// </remarks>
public sealed class SavedChartPickersTests
{
    /// <summary>The three pickers' automation names, with the chart each test picks in it.</summary>
    private static readonly (string Box, string Chart)[] Picks =
    [
        ("Person B", "Alpha"),
        ("Natal chart", "Beta"),
        ("Relocation birth chart", "Gamma"),
    ];

    /// <summary>Reads a picker's selection from the view model.</summary>
    /// <param name="vm">The view model.</param>
    /// <param name="box">The picker's automation name.</param>
    /// <returns>The selected chart, if any.</returns>
    private static ChartEntity? Selection(MainViewModel vm, string box) => box switch
    {
        "Person B" => vm.Synastry.PersonB,
        "Natal chart" => vm.Electional.SelectedNatalChart,
        "Relocation birth chart" => vm.Relocation.SelectedNatalChart,
        _ => throw new ArgumentOutOfRangeException(nameof(box), box, null),
    };

    /// <summary>Finds a picker by its automation name.</summary>
    /// <param name="window">The window.</param>
    /// <param name="name">The automation name.</param>
    /// <returns>The ComboBox.</returns>
    private static ComboBox Box(MainWindow window, string name) =>
        window.GetLogicalDescendants().OfType<ComboBox>()
            .Single(c => global::Avalonia.Automation.AutomationProperties.GetName(c) == name);

    /// <summary>
    /// Each picker keeps its chart across Calculate &amp; Save, Search, the sort toggle,
    /// <c>InitializeAsync</c> and a filter that hides every chart; deleting a picked chart empties that
    /// picker alone. The Relocation picker (1.4.0, branch Relocate) and the Electional natal-chart picker (1.6.1,
    /// branch ElectionalPicker) follow Calculate &amp; Save to the chart it saved, the Chart panel's chart;
    /// Person B keeps its pick.
    /// </summary>
    /// <param name="trigger">What refreshes the saved charts.</param>
    /// <param name="deleted">For "delete", the chart deleted; otherwise null.</param>
    [AvaloniaTheory]
    [InlineData("calculate", null)]
    [InlineData("search", null)]
    [InlineData("sort", null)]
    [InlineData("initialize", null)]
    [InlineData("filter", null)]
    [InlineData("delete", "Alpha")]
    [InlineData("delete", "Beta")]
    [InlineData("delete", "Gamma")]
    public async Task Pickers_KeepTheirCharts_UntilOneIsDeleted(string trigger, string? deleted)
    {
        SavedChartsRepository repository = new("Alpha", "Beta", "Gamma");
        MainViewModel vm = MainViewModelFactory.Create(repository: repository);
        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        await vm.InitializeAsync();
        Dispatcher.UIThread.RunJobs();

        foreach ((string box, string chart) in Picks)
        {
            ComboBox combo = Box(window, box);
            combo.SelectedItem = combo.Items.OfType<ChartEntity>().Single(c => c.Name == chart);
            Dispatcher.UIThread.RunJobs();
            Assert.Equal(chart, Selection(vm, box)?.Name);
        }

        int queries = repository.QueryCount;
        switch (trigger)
        {
            case "calculate":
                vm.ChartEntry.Name = "Panels Subject";
                vm.ChartEntry.TimeZone = "UTC";
                vm.ChartEntry.LocationName = "Nowhere";
                vm.ChartEntry.EventType = "Event";
                vm.CalculateAndSaveCommand.Execute(null);
                break;
            case "search":
                vm.SearchChartsCommand.Execute(null);
                break;
            case "sort":
                vm.ToggleSortDirectionCommand.Execute(null);
                break;
            case "initialize":
                await vm.InitializeAsync();
                break;
            case "filter":
                vm.SearchText = "no chart has this name";
                vm.SearchChartsCommand.Execute(null);
                break;
            case "delete":
                vm.DeleteChartCommand.Execute(vm.SavedCharts.Single(c => c.Name == deleted));
                break;
        }

        Dispatcher.UIThread.RunJobs();

        Assert.True(repository.QueryCount > queries, $"'{trigger}' did not refresh the saved charts.");
        if (trigger == "filter")
        {
            Assert.Empty(vm.SavedCharts);
        }

        foreach ((string box, string picked) in Picks)
        {
            ChartEntity? selected = Selection(vm, box);
            ComboBox combo = Box(window, box);

            // Calculate & Save picks the chart it saved in the Relocation picker (1.4.0, branch Relocate) and
            // the Electional natal-chart picker (1.6.1, branch ElectionalPicker), which follow the Chart
            // panel's chart; Person B keeps its pick.
            string chart = trigger == "calculate" && box is "Relocation birth chart" or "Natal chart"
                ? "Panels Subject"
                : picked;

            if (chart == deleted)
            {
                Assert.Null(selected);
                Assert.Null(combo.SelectedItem);
                continue;
            }

            Assert.True(
                selected is not null,
                $"'{trigger}' emptied the {box} picker, which held {chart}.");
            Assert.Equal(chart, selected.Name);
            Assert.Same(selected, combo.SelectedItem);
            Assert.Contains(selected, combo.Items.OfType<ChartEntity>());
        }
    }

    /// <summary>
    /// A refresh of the saved charts that keeps Person B and the Electional panel's chart keeps the results made
    /// from them (branch StaleResults, 1.7.2, finding N2): the Synastry panel's three and the electional result.
    /// </summary>
    /// <remarks>
    /// Those results are cleared when their picker's chart changes, by its Id. In the window each ComboBox loses
    /// its item while the list is refilled, and its two-way binding writes null to the view model before the
    /// refresh picks the same chart again; the test sees that null arrive. It is the refresh, not a change of
    /// chart, so nothing is cleared. Deleting a third chart is the refresh here: it changes no picker's chart
    /// and not the Chart panel's.
    /// </remarks>
    [AvaloniaFact]
    public async Task ARefresh_KeepsTheResultsOfThePickersItRepoints()
    {
        SavedChartsRepository repository = new("Alpha", "Beta", "Gamma");
        MainViewModel vm = MainViewModelFactory.Create(repository: repository);
        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        await vm.InitializeAsync();
        Dispatcher.UIThread.RunJobs();

        vm.LoadChartCommand.Execute(vm.SavedCharts.Single(c => c.Name == "Alpha"));
        Dispatcher.UIThread.RunJobs();
        Assert.NotNull(vm.LastResult);
        Assert.Equal("Alpha", vm.Electional.SelectedNatalChart?.Name);

        ComboBox personB = Box(window, "Person B");
        personB.SelectedItem = personB.Items.OfType<ChartEntity>().Single(c => c.Name == "Beta");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Beta", vm.Synastry.PersonB?.Name);

        await RunAsync(vm.CalculateSynastryCommand);
        await RunAsync(vm.CalculateCompositeCommand);
        await RunAsync(vm.CalculateDavisonCommand);
        vm.Electional.ElectionalResult = NoCandidates(vm.LastResult);
        Assert.True(vm.Synastry.HasSynastryResult, vm.StatusText);
        Assert.True(vm.Synastry.HasCompositeResult, vm.StatusText);
        Assert.True(vm.Synastry.HasDavisonResult, vm.StatusText);

        List<string?> personBWrites = [];
        List<string?> electionalWrites = [];
        vm.Synastry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SynastryViewModel.PersonB))
                personBWrites.Add(vm.Synastry.PersonB?.Name);
        };
        vm.Electional.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ElectionalViewModel.SelectedNatalChart))
                electionalWrites.Add(vm.Electional.SelectedNatalChart?.Name);
        };

        vm.DeleteChartCommand.Execute(vm.SavedCharts.Single(c => c.Name == "Gamma"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(2, vm.AllSavedCharts.Count);
        Assert.Equal(new string?[] { null, "Beta" }, personBWrites);
        Assert.Equal(new string?[] { null, "Alpha" }, electionalWrites);
        Assert.True(vm.Synastry.HasSynastryResult, "The refresh cleared the synastry result.");
        Assert.True(vm.Synastry.HasCompositeResult, "The refresh cleared the composite result.");
        Assert.True(vm.Synastry.HasDavisonResult, "The refresh cleared the Davison result.");
        Assert.True(vm.Electional.HasResult, "The refresh cleared the electional result.");
        Assert.True(vm.ExportSynastryMarkdownCommand.CanExecute(null));
        Assert.True(vm.ExportElectionalMarkdownCommand.CanExecute(null));
    }

    /// <summary>Runs a command and waits for its handler to finish, for at most 5 s.</summary>
    /// <param name="command">The command.</param>
    /// <returns>A task that completes when the command has finished.</returns>
    private static async Task RunAsync(AsyncRelayCommand command)
    {
        command.Execute(null);

        int waited = 0;
        while (command.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            waited += 10;
        }

        Assert.False(command.IsExecuting, "The command did not finish within 5 s.");
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>An electional result with no candidate, as a search for the given chart would leave it.</summary>
    /// <param name="natal">The chart the search was scored against.</param>
    /// <returns>The result.</returns>
    private static AstroCast.Core.Models.Electional.ElectionalResult NoCandidates(AstroCast.Core.Models.ChartResult natal)
    {
        AstroCast.Core.Models.Electional.ElectionalInput input = new()
        {
            NatalChart = natal,
            ElectionType = AstroCast.Core.Enums.ElectionType.Other,
            ElectionGoal = "Open a shop",
            RangeStart = new DateOnly(2026, 6, 11),
            RangeEnd = new DateOnly(2026, 6, 20),
            Latitude = 37.5385,
            Longitude = -77.4343,
            LocationName = "Richmond, VA",
            TimeZone = "America/New_York",
        };
        return new AstroCast.Core.Models.Electional.ElectionalResult
        {
            Input = input,
            NatalChart = natal,
            Candidates = [],
            RequestedCandidateCount = input.CandidateCount,
            SamplesWithoutPlanetaryHour = 0,
            DisqualifiedPeriods = [],
            VoidOfCoursePeriods = [],
            CalculatedAt = DateTime.UtcNow,
        };
    }
}
