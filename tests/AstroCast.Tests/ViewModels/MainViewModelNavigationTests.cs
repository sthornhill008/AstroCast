using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="MainViewModel"/> navigation panel switching.
/// Verifies ActivePanel property, visibility properties, and NavigateToPanelCommand.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MainViewModelNavigationTests
{
    #region Test Infrastructure

    /// <summary>
    /// Creates a <see cref="MainViewModel"/> with test stubs for all dependencies.
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

    #endregion

    #region ActivePanel Default Tests

    /// <summary>
    /// ActivePanel defaults to Chart on construction.
    /// </summary>
    [Fact]
    public void ActivePanel_DefaultsToChart()
    {
        MainViewModel vm = CreateViewModel();

        vm.ActivePanel.Should().Be(NavigationPanel.Chart);
    }

    /// <summary>
    /// IsChartPanelVisible is true by default; all others are false.
    /// </summary>
    [Fact]
    public void DefaultState_OnlyChartPanelVisible()
    {
        MainViewModel vm = CreateViewModel();

        vm.IsChartPanelVisible.Should().BeTrue();
        vm.IsSynastryPanelVisible.Should().BeFalse();
        vm.IsTransitsPanelVisible.Should().BeFalse();
        vm.IsReturnsPanelVisible.Should().BeFalse();
        vm.IsElectionalPanelVisible.Should().BeFalse();
        vm.IsMedicalPanelVisible.Should().BeFalse();
        vm.IsMapPanelVisible.Should().BeFalse();
        vm.IsExportPanelVisible.Should().BeFalse();
        vm.IsRelocationPanelVisible.Should().BeFalse();
        vm.IsDirectionsPanelVisible.Should().BeFalse();
    }

    #endregion

    #region Panel Switching Tests

    /// <summary>
    /// Setting ActivePanel to Synastry makes only IsSynastryPanelVisible true.
    /// </summary>
    [Fact]
    public void SetActivePanel_Synastry_UpdatesVisibility()
    {
        MainViewModel vm = CreateViewModel();

        vm.ActivePanel = NavigationPanel.Synastry;

        vm.IsSynastryPanelVisible.Should().BeTrue();
        vm.IsChartPanelVisible.Should().BeFalse();
        vm.IsTransitsPanelVisible.Should().BeFalse();
        vm.IsReturnsPanelVisible.Should().BeFalse();
        vm.IsElectionalPanelVisible.Should().BeFalse();
        vm.IsMedicalPanelVisible.Should().BeFalse();
        vm.IsMapPanelVisible.Should().BeFalse();
        vm.IsExportPanelVisible.Should().BeFalse();
        vm.IsRelocationPanelVisible.Should().BeFalse();
        vm.IsDirectionsPanelVisible.Should().BeFalse();
    }

    /// <summary>
    /// Setting ActivePanel to Map makes only IsMapPanelVisible true. This is the
    /// key property check for the Astrocartography-own-panel refactor: the map
    /// must have its own top-level panel so the SkiaSharp surface is visible
    /// when the user navigates to it.
    /// </summary>
    [Fact]
    public void SetActivePanel_Map_UpdatesVisibility()
    {
        MainViewModel vm = CreateViewModel();

        vm.ActivePanel = NavigationPanel.Map;

        vm.IsMapPanelVisible.Should().BeTrue();
        vm.IsChartPanelVisible.Should().BeFalse();
        vm.IsSynastryPanelVisible.Should().BeFalse();
        vm.IsTransitsPanelVisible.Should().BeFalse();
        vm.IsReturnsPanelVisible.Should().BeFalse();
        vm.IsElectionalPanelVisible.Should().BeFalse();
        vm.IsMedicalPanelVisible.Should().BeFalse();
        vm.IsExportPanelVisible.Should().BeFalse();
        vm.IsRelocationPanelVisible.Should().BeFalse();
        vm.IsDirectionsPanelVisible.Should().BeFalse();
    }

    /// <summary>
    /// Setting ActivePanel to Relocation makes only IsRelocationPanelVisible true.
    /// Relocation was promoted from a subsection inside the Returns panel to its
    /// own top-level panel with an Alt+9 KeyBinding and a dedicated sidebar button.
    /// </summary>
    [Fact]
    public void SetActivePanel_Relocation_UpdatesVisibility()
    {
        MainViewModel vm = CreateViewModel();

        vm.ActivePanel = NavigationPanel.Relocation;

        vm.IsRelocationPanelVisible.Should().BeTrue();
        vm.IsChartPanelVisible.Should().BeFalse();
        vm.IsSynastryPanelVisible.Should().BeFalse();
        vm.IsTransitsPanelVisible.Should().BeFalse();
        vm.IsReturnsPanelVisible.Should().BeFalse();
        vm.IsElectionalPanelVisible.Should().BeFalse();
        vm.IsMedicalPanelVisible.Should().BeFalse();
        vm.IsMapPanelVisible.Should().BeFalse();
        vm.IsExportPanelVisible.Should().BeFalse();
        vm.IsDirectionsPanelVisible.Should().BeFalse();
    }

    /// <summary>
    /// Setting ActivePanel to Directions makes only IsDirectionsPanelVisible true. Directions, the
    /// tenth panel (branch PrimaryPanel, 1.5.0), has the Alt+0 KeyBinding and the sidebar's tenth button.
    /// </summary>
    [Fact]
    public void SetActivePanel_Directions_UpdatesVisibility()
    {
        MainViewModel vm = CreateViewModel();

        vm.ActivePanel = NavigationPanel.Directions;

        vm.IsDirectionsPanelVisible.Should().BeTrue();
        vm.IsChartPanelVisible.Should().BeFalse();
        vm.IsSynastryPanelVisible.Should().BeFalse();
        vm.IsTransitsPanelVisible.Should().BeFalse();
        vm.IsReturnsPanelVisible.Should().BeFalse();
        vm.IsElectionalPanelVisible.Should().BeFalse();
        vm.IsMedicalPanelVisible.Should().BeFalse();
        vm.IsMapPanelVisible.Should().BeFalse();
        vm.IsExportPanelVisible.Should().BeFalse();
        vm.IsRelocationPanelVisible.Should().BeFalse();
    }

    /// <summary>
    /// Each NavigationPanel value produces exactly one true visibility property.
    /// </summary>
    [Theory]
    [InlineData(NavigationPanel.Chart)]
    [InlineData(NavigationPanel.Synastry)]
    [InlineData(NavigationPanel.Transits)]
    [InlineData(NavigationPanel.Returns)]
    [InlineData(NavigationPanel.Electional)]
    [InlineData(NavigationPanel.Medical)]
    [InlineData(NavigationPanel.Map)]
    [InlineData(NavigationPanel.Export)]
    [InlineData(NavigationPanel.Relocation)]
    [InlineData(NavigationPanel.Directions)]
    public void EachPanel_ProducesExactlyOneTrueVisibility(NavigationPanel panel)
    {
        MainViewModel vm = CreateViewModel();

        vm.ActivePanel = panel;

        bool[] visibilities =
        [
            vm.IsChartPanelVisible,
            vm.IsSynastryPanelVisible,
            vm.IsTransitsPanelVisible,
            vm.IsReturnsPanelVisible,
            vm.IsElectionalPanelVisible,
            vm.IsMedicalPanelVisible,
            vm.IsMapPanelVisible,
            vm.IsExportPanelVisible,
            vm.IsRelocationPanelVisible,
            vm.IsDirectionsPanelVisible
        ];

        visibilities.Count(v => v).Should().Be(1,
            $"exactly one panel should be visible when ActivePanel is {panel}");
    }

    #endregion

    #region PropertyChanged Tests

    /// <summary>
    /// Setting ActivePanel raises PropertyChanged for all visibility properties.
    /// </summary>
    [Fact]
    public void SetActivePanel_RaisesPropertyChanged_ForAllVisibilities()
    {
        MainViewModel vm = CreateViewModel();
        List<string> changedProperties = [];

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                changedProperties.Add(e.PropertyName);
        };

        vm.ActivePanel = NavigationPanel.Transits;

        changedProperties.Should().Contain(nameof(MainViewModel.ActivePanel));
        changedProperties.Should().Contain(nameof(MainViewModel.IsChartPanelVisible));
        changedProperties.Should().Contain(nameof(MainViewModel.IsSynastryPanelVisible));
        changedProperties.Should().Contain(nameof(MainViewModel.IsTransitsPanelVisible));
        changedProperties.Should().Contain(nameof(MainViewModel.IsReturnsPanelVisible));
        changedProperties.Should().Contain(nameof(MainViewModel.IsElectionalPanelVisible));
        changedProperties.Should().Contain(nameof(MainViewModel.IsMedicalPanelVisible));
        changedProperties.Should().Contain(nameof(MainViewModel.IsMapPanelVisible));
        changedProperties.Should().Contain(nameof(MainViewModel.IsExportPanelVisible));
        changedProperties.Should().Contain(nameof(MainViewModel.IsRelocationPanelVisible));
        changedProperties.Should().Contain(nameof(MainViewModel.IsDirectionsPanelVisible));
    }

    /// <summary>
    /// Setting ActivePanel to the same value does not raise PropertyChanged.
    /// </summary>
    [Fact]
    public void SetActivePanel_SameValue_DoesNotRaisePropertyChanged()
    {
        MainViewModel vm = CreateViewModel();
        List<string> changedProperties = [];

        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not null)
                changedProperties.Add(e.PropertyName);
        };

        // Chart is already the default
        vm.ActivePanel = NavigationPanel.Chart;

        changedProperties.Should().NotContain(nameof(MainViewModel.ActivePanel));
    }

    #endregion

    #region NavigateToPanelCommand Tests

    /// <summary>
    /// NavigateToPanelCommand sets ActivePanel to the given enum value.
    /// </summary>
    [Fact]
    public void NavigateToPanelCommand_SetsActivePanel()
    {
        MainViewModel vm = CreateViewModel();

        vm.NavigateToPanelCommand.Execute(NavigationPanel.Export);

        vm.ActivePanel.Should().Be(NavigationPanel.Export);
        vm.IsExportPanelVisible.Should().BeTrue();
    }

    /// <summary>
    /// NavigateToPanelCommand can always execute.
    /// </summary>
    [Fact]
    public void NavigateToPanelCommand_CanAlwaysExecute()
    {
        MainViewModel vm = CreateViewModel();

        vm.NavigateToPanelCommand.CanExecute(NavigationPanel.Medical).Should().BeTrue();
    }

    #endregion
}
