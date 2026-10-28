using System.ComponentModel;
using System.Reflection;

using AstroCast.Core.Data;
using AstroCast.Core.Models;
using AstroCast.ViewModels;

using FluentAssertions;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// The Electional panel's Natal Chart picker follows the Chart panel and names the chart the search will score
/// against (1.6.1, item C, branch ElectionalPicker), as the Relocation picker has since 1.4.0.
/// </summary>
/// <remarks>
/// <para>
/// Each Load and each Calculate &amp; Save picks the Chart panel's chart in the Electional picker, as the
/// <see cref="MainViewModel.AllSavedCharts"/> instance with its Id; a pick by hand holds until the next Load or
/// Calculate &amp; Save, or until its chart is deleted. <see cref="ElectionalViewModel.ChartInUseText"/> names the
/// picked chart, else the Chart panel's unsaved chart, else says there is none and that Calculate casts one from
/// the Chart panel's fields.
/// </para>
/// <para>
/// Until 1.6.1 the picker showed only a hand pick, and the panel said nothing about which chart the search
/// would use. The helpers are <c>RelocationChartInUseTests</c>' own, copied rather than shared, so each file
/// reads on its own.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ElectionalChartInUseTests
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
    /// Loading a saved chart picks it in the Electional picker, as the <see cref="MainViewModel.AllSavedCharts"/>
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
        vm.Electional.SelectedNatalChart.Should().BeSameAs(vm.AllSavedCharts.Single(c => c.Id == listed.Id));
        vm.Electional.SelectedNatalChart.Should().NotBeSameAs(listed, "the picker's items are AllSavedCharts' own");
    }

    /// <summary>
    /// Calculate &amp; Save picks the chart it saved in the Electional picker, a new chart and an overwritten one
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
        vm.Electional.SelectedNatalChart = vm.AllSavedCharts.Single(c => c.Name == "Alpha");
        vm.ChartEntry.Name = name;
        vm.ChartEntry.TimeZone = "UTC";
        vm.ChartEntry.LocationName = "Nowhere";
        vm.ChartEntry.EventType = "Event";

        vm.CalculateAndSaveCommand.Execute(null);
        await WaitForSaveAsync(vm);

        ChartEntity? saved = await repository.GetByNameAsync(name);
        saved.Should().NotBeNull();
        vm.Electional.SelectedNatalChart.Should().BeSameAs(vm.AllSavedCharts.Single(c => c.Id == saved?.Id));
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
        vm.Electional.SelectedNatalChart = vm.AllSavedCharts.Single(c => c.Name == "Alpha");

        vm.SearchChartsCommand.Execute(null);
        vm.ToggleSortDirectionCommand.Execute(null);
        await vm.InitializeAsync();

        vm.Electional.SelectedNatalChart.Should().BeSameAs(vm.AllSavedCharts.Single(c => c.Name == "Alpha"));

        vm.LoadChartCommand.Execute(vm.SavedCharts.Single(c => c.Name == "Beta"));

        vm.Electional.SelectedNatalChart.Should().BeSameAs(
            vm.AllSavedCharts.Single(c => c.Name == "Beta"), "a Load picks the loaded chart");
    }

    /// <summary>
    /// The chart-in-use line names the picked chart; else the Chart panel's unsaved chart; else says there is
    /// none and that Calculate casts one from the Chart panel's fields.
    /// </summary>
    /// <param name="state">"picked", "unsaved" or "none".</param>
    /// <param name="expected">The line.</param>
    [Theory]
    [InlineData("picked", "Beta — Birth, 1947-10-05 09:30:00, Harrisburg, Pennsylvania, United States")]
    [InlineData("unsaved", "Form Subject — Birth, 2000-01-01 07:00:00, New York City (the Chart panel's chart, not saved)")]
    [InlineData("none", "No chart yet: pick a saved chart, or load or calculate one on the Chart panel; otherwise Calculate casts one from the Chart panel's fields.")]
    public void ChartInUseText(string state, string expected)
    {
        MainViewModel vm = MainViewModelFactory.Create();
        switch (state)
        {
            case "picked":
                vm.Electional.SelectedNatalChart = SavedChartsRepository.NewChart(2, "Beta");
                break;
            case "unsaved":
                SetChartPanelChart(vm, UnsavedChart());
                break;
        }

        vm.Electional.ChartInUseText.Should().Be(expected);
    }

    /// <summary>
    /// A change of the Chart panel's chart raises <see cref="ElectionalViewModel.ChartInUseText"/>, so the line
    /// follows it.
    /// </summary>
    [Fact]
    public void ChartInUseText_IsRaised_WhenTheChartPanelChartChanges()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        List<string?> changed = [];
        ((INotifyPropertyChanged)vm.Electional).PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        SetChartPanelChart(vm, UnsavedChart());

        changed.Should().Contain(nameof(ElectionalViewModel.ChartInUseText));
    }

    /// <summary>
    /// Clear Form empties both saved-chart pickers, the Electional and the Relocation one, since each follows the
    /// Chart panel's chart and Clear Form empties the Chart panel (Steve's decision of 2026-10-21, 1.6.1, branch
    /// ElectionalPicker): both lines read their no-chart text and the Relocation result is gone. Until 1.6.1 both
    /// kept their pick through Clear Form, the Electional one since 1.4.0 and the Relocation one since branch
    /// Relocate, so a cleared Chart panel still named a chart on each panel.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ClearForm_EmptiesBothPickers()
    {
        (MainViewModel vm, _) = await CreateAsync();
        ChartEntity listed = vm.SavedCharts.Single(c => c.Name == "Beta");
        vm.LoadChartCommand.Execute(listed);
        ChartEntity beta = vm.AllSavedCharts.Single(c => c.Id == listed.Id);
        vm.Electional.SelectedNatalChart.Should().BeSameAs(beta, "the Load picked Beta in the Electional picker");
        vm.Relocation.SelectedNatalChart.Should().BeSameAs(beta, "the Load picked Beta in the Relocation picker");

        vm.ClearFormCommand.Execute(null);

        vm.Electional.SelectedNatalChart.Should().BeNull("Clear Form empties the Electional picker");
        vm.Relocation.SelectedNatalChart.Should().BeNull("Clear Form empties the Relocation picker");
        vm.Electional.ChartInUseText.Should().Be(
            "No chart yet: pick a saved chart, or load or calculate one on the Chart panel; otherwise Calculate "
            + "casts one from the Chart panel's fields.");
        vm.Relocation.ChartInUseText.Should().Be(
            "No chart yet: load or calculate one on the Chart panel, or pick a saved chart.");
        vm.Relocation.HasResult.Should().BeFalse();
    }
}
