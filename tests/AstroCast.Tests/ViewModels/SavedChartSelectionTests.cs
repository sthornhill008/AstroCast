using AstroCast.Core.Data;
using AstroCast.ViewModels;

using FluentAssertions;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// A refresh of the saved charts keeps the Person B, Electional natal-chart and Relocation natal-chart
/// selections by Id (1.4.0, branch Panels).
/// </summary>
/// <remarks>
/// <para>
/// The three pickers bind <see cref="MainViewModel.AllSavedCharts"/>, which every refresh clears and refills
/// with new instances, as the database hands them out. <see cref="ChartEntity"/> compares by reference, so a
/// selection kept as it was would point at a chart no longer in the list, and the bound ComboBox writes null
/// back when the list is cleared. The refresh therefore records the three Ids before it clears the list and
/// points each selection at the refilled chart with the same Id, or at nothing when that chart is gone.
/// </para>
/// <para>
/// These run with no window, so nothing writes null during the refill; the headless counterpart, with the
/// bindings in place, is <c>SavedChartPickersTests</c> in AstroCast.UiTests.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class SavedChartSelectionTests
{
    /// <summary>
    /// After a refresh each selection is the refilled instance with its Id, not the instance it held.
    /// </summary>
    [Fact]
    public async Task RefreshSavedCharts_PointsEachSelectionAtTheRefilledChartWithItsId()
    {
        SavedChartsRepository repository = new("Alpha", "Beta", "Gamma");
        MainViewModel vm = MainViewModelFactory.Create(repository: repository);
        await vm.InitializeAsync();

        ChartEntity alpha = vm.AllSavedCharts.Single(c => c.Name == "Alpha");
        ChartEntity beta = vm.AllSavedCharts.Single(c => c.Name == "Beta");
        ChartEntity gamma = vm.AllSavedCharts.Single(c => c.Name == "Gamma");
        vm.Synastry.PersonB = alpha;
        vm.Electional.SelectedNatalChart = beta;
        vm.Relocation.SelectedNatalChart = gamma;

        await vm.InitializeAsync();

        vm.Synastry.PersonB.Should().BeSameAs(vm.AllSavedCharts.Single(c => c.Id == alpha.Id));
        vm.Electional.SelectedNatalChart.Should().BeSameAs(vm.AllSavedCharts.Single(c => c.Id == beta.Id));
        vm.Relocation.SelectedNatalChart.Should().BeSameAs(vm.AllSavedCharts.Single(c => c.Id == gamma.Id));
        vm.Synastry.PersonB.Should().NotBeSameAs(alpha, "the list was refilled with new instances");
    }

    /// <summary>
    /// Deleting a selected chart empties its picker and leaves the other two on their charts.
    /// </summary>
    [Fact]
    public async Task DeletingASelectedChart_EmptiesOnlyItsPicker()
    {
        SavedChartsRepository repository = new("Alpha", "Beta", "Gamma");
        MainViewModel vm = MainViewModelFactory.Create(repository: repository);
        await vm.InitializeAsync();

        vm.Synastry.PersonB = vm.AllSavedCharts.Single(c => c.Name == "Alpha");
        vm.Electional.SelectedNatalChart = vm.AllSavedCharts.Single(c => c.Name == "Beta");
        vm.Relocation.SelectedNatalChart = vm.AllSavedCharts.Single(c => c.Name == "Gamma");

        // The repository and the confirmation complete synchronously, so Execute has finished on return.
        vm.DeleteChartCommand.Execute(vm.SavedCharts.Single(c => c.Name == "Beta"));
        vm.DeleteChartCommand.LastException.Should().BeNull();

        repository.Count.Should().Be(2);
        vm.Electional.SelectedNatalChart.Should().BeNull("its chart was deleted");
        vm.Synastry.PersonB.Should().BeSameAs(vm.AllSavedCharts.Single(c => c.Name == "Alpha"));
        vm.Relocation.SelectedNatalChart.Should().BeSameAs(vm.AllSavedCharts.Single(c => c.Name == "Gamma"));
    }
}
