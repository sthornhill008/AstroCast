using System.ComponentModel;
using System.Reflection;

using AstroCast.Core.Data;
using AstroCast.Core.Models;
using AstroCast.ViewModels;

using FluentAssertions;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// The Relocation panel's Birth Chart section follows the Chart panel and names the chart Calculate Relocation
/// will use (1.4.0, item 3, branch Relocate).
/// </summary>
/// <remarks>
/// <para>
/// Each Load and each Calculate &amp; Save picks the Chart panel's chart in the Relocation picker, as the
/// <see cref="MainViewModel.AllSavedCharts"/> instance with its Id; a pick by hand holds until the next Load or
/// Calculate &amp; Save, or until its chart is deleted. <see cref="RelocationViewModel.ChartInUseText"/> names the
/// picked chart, else the Chart panel's unsaved chart, else says there is none.
/// </para>
/// <para>
/// Until 1.4.0 the picker kept whatever had been picked, or nothing, and a hint said that the Chart panel's
/// chart was used until a saved one was chosen: two charts in play, which read as a comparison.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class RelocationChartInUseTests
{
    /// <summary>A view model over three saved Birth charts, Alpha, Beta and Gamma, loaded into its lists.</summary>
    /// <returns>The view model and its repository.</returns>
    private static async Task<(MainViewModel Vm, SavedChartsRepository Repository)> CreateAsync()
    {
        SavedChartsRepository repository = new("Alpha", "Beta", "Gamma");
        MainViewModel vm = MainViewModelFactory.Create(repository: repository);
        await vm.InitializeAsync();
        return (vm, repository);
    }

    /// <summary>Waits for Calculate &amp; Save to finish, for at most 5 s.</summary>
    /// <param name="vm">The view model.</param>
    /// <returns>A task that completes when the command has finished.</returns>
    private static async Task WaitForSaveAsync(MainViewModel vm)
    {
        int waited = 0;
        while (vm.CalculateAndSaveCommand.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        vm.CalculateAndSaveCommand.IsExecuting.Should().BeFalse("the command finishes within 5 s");
    }

    /// <summary>Sets the Chart panel's chart, which only the view model's own commands write.</summary>
    /// <param name="vm">The view model.</param>
    /// <param name="result">The chart.</param>
    private static void SetChartPanelChart(MainViewModel vm, ChartResult? result)
    {
        PropertyInfo property = typeof(MainViewModel).GetProperty(nameof(MainViewModel.LastResult))
            ?? throw new InvalidOperationException("MainViewModel has no LastResult property.");
        property.SetValue(vm, result);
    }

    /// <summary>An unsaved chart, as the Electional search casts one from the Chart panel's fields.</summary>
    /// <returns>The chart.</returns>
    private static ChartResult UnsavedChart() => new StubChartEngine().Calculate(new ChartInput
    {
        Name = "Form Subject",
        LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
        TimeZone = "America/New_York",
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        EventType = "Birth",
    });

    /// <summary>
    /// Loading a saved chart picks it in the Relocation picker, as the <see cref="MainViewModel.AllSavedCharts"/>
    /// instance with its Id, never the instance the saved-charts list handed to Load.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task LoadChart_PreselectsTheLoadedChart()
    {
        (MainViewModel vm, _) = await CreateAsync();
        ChartEntity listed = vm.SavedCharts.Single(c => c.Name == "Beta");

        vm.LoadChartCommand.Execute(listed);

        vm.StatusText.Should().StartWith("Loaded 'Beta'", "the load completed rather than failing after the pick");
        vm.Relocation.SelectedNatalChart.Should().BeSameAs(vm.AllSavedCharts.Single(c => c.Id == listed.Id));
        vm.Relocation.SelectedNatalChart.Should().NotBeSameAs(listed, "the picker's items are AllSavedCharts' own");
    }

    /// <summary>
    /// Calculate &amp; Save picks the chart it saved in the Relocation picker, a new chart and an overwritten one
    /// alike.
    /// </summary>
    /// <param name="name">The Chart panel's chart name: new, or one already saved.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("Delta")]
    [InlineData("Beta")]
    public async Task CalculateAndSave_PreselectsTheSavedChart(string name)
    {
        (MainViewModel vm, SavedChartsRepository repository) = await CreateAsync();
        vm.Relocation.SelectedNatalChart = vm.AllSavedCharts.Single(c => c.Name == "Alpha");
        vm.ChartEntry.Name = name;
        vm.ChartEntry.TimeZone = "UTC";
        vm.ChartEntry.LocationName = "Nowhere";
        vm.ChartEntry.EventType = "Event";

        vm.CalculateAndSaveCommand.Execute(null);
        await WaitForSaveAsync(vm);

        ChartEntity? saved = await repository.GetByNameAsync(name);
        saved.Should().NotBeNull();
        vm.Relocation.SelectedNatalChart.Should().BeSameAs(vm.AllSavedCharts.Single(c => c.Id == saved?.Id));
    }

    /// <summary>
    /// A chart picked by hand holds through Search, the sort toggle and a reload of the lists, and gives way to
    /// the next Load.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AHandPick_HoldsUntilTheNextLoad()
    {
        (MainViewModel vm, _) = await CreateAsync();
        vm.Relocation.SelectedNatalChart = vm.AllSavedCharts.Single(c => c.Name == "Gamma");

        vm.SearchChartsCommand.Execute(null);
        vm.ToggleSortDirectionCommand.Execute(null);
        await vm.InitializeAsync();

        vm.Relocation.SelectedNatalChart.Should().BeSameAs(vm.AllSavedCharts.Single(c => c.Name == "Gamma"));

        vm.LoadChartCommand.Execute(vm.SavedCharts.Single(c => c.Name == "Alpha"));

        vm.Relocation.SelectedNatalChart.Should().BeSameAs(
            vm.AllSavedCharts.Single(c => c.Name == "Alpha"), "a Load picks the loaded chart");
    }

    /// <summary>
    /// The chart-in-use line names the picked chart; else the Chart panel's unsaved chart; else says there is
    /// none.
    /// </summary>
    /// <param name="state">"picked", "unsaved" or "none".</param>
    /// <param name="expected">The line.</param>
    [Theory]
    [InlineData("picked", "Gamma — Birth, 1947-10-05 09:30:00, Harrisburg, Pennsylvania, United States")]
    [InlineData("unsaved", "Form Subject — Birth, 2000-01-01 07:00:00, New York City (the Chart panel's chart, not saved)")]
    [InlineData("none", "No chart yet: load or calculate one on the Chart panel, or pick a saved chart.")]
    public void ChartInUseText(string state, string expected)
    {
        MainViewModel vm = MainViewModelFactory.Create();
        switch (state)
        {
            case "picked":
                vm.Relocation.SelectedNatalChart = SavedChartsRepository.NewChart(3, "Gamma");
                break;
            case "unsaved":
                SetChartPanelChart(vm, UnsavedChart());
                break;
        }

        vm.Relocation.ChartInUseText.Should().Be(expected);
    }

    /// <summary>
    /// A change of the Chart panel's chart raises <see cref="RelocationViewModel.ChartInUseText"/>, so the line
    /// follows it.
    /// </summary>
    [Fact]
    public void ChartInUseText_IsRaised_WhenTheChartPanelChartChanges()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        List<string?> changed = [];
        ((INotifyPropertyChanged)vm.Relocation).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        SetChartPanelChart(vm, UnsavedChart());

        changed.Should().Contain(nameof(RelocationViewModel.ChartInUseText));
    }

    /// <summary>
    /// A change of the Chart panel's chart clears the relocation result and disables Export Relocation MD
    /// (branch StaleResult, 1.6.1): a picked chart's cast carries the session's switch, and with none picked
    /// the Chart panel's chart is the one relocated, so any new chart there makes the result stale. Until
    /// 1.6.1 the result stayed and Export wrote it.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task AChartPanelChange_ClearsTheResultAndDisablesExport()
    {
        (MainViewModel vm, _) = await CreateAsync();
        vm.Relocation.SelectedNatalChart = vm.AllSavedCharts.Single(c => c.Name == "Alpha");
        vm.Relocation.LocationName = "London";
        vm.Relocation.LatitudeText = "51.5074";
        vm.Relocation.LongitudeText = "-0.1278";
        vm.CalculateRelocationCommand.Execute(null);
        await WaitForRelocationAsync(vm);
        vm.Relocation.HasResult.Should().BeTrue($"the stub relocation service returns a result; status: {vm.StatusText}");
        vm.ExportRelocationMarkdownCommand.CanExecute(null).Should().BeTrue();

        SetChartPanelChart(vm, UnsavedChart());

        vm.Relocation.HasResult.Should().BeFalse();
        vm.ExportRelocationMarkdownCommand.CanExecute(null).Should().BeFalse();
    }

    /// <summary>Waits for Calculate Relocation to finish, for at most 5 s.</summary>
    /// <param name="vm">The view model.</param>
    /// <returns>A task that completes when the command has finished.</returns>
    private static async Task WaitForRelocationAsync(MainViewModel vm)
    {
        int waited = 0;
        while (vm.CalculateRelocationCommand.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        vm.CalculateRelocationCommand.IsExecuting.Should().BeFalse("the command finishes within 5 s");
    }
}
