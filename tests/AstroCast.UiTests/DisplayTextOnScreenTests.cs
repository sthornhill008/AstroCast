using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Core.Enums;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// The screen shows names, not enum names or stored strings (1.4.0, E3-6), through the head's
/// <c>DisplayTextConverter</c>.
/// </summary>
/// <remarks>
/// Each test reads the text the control renders, its <see cref="TextBlock"/>s, not the bound value: the value
/// stays the enum or the entity, which every <c>SelectedItem</c> binding and command parameter still use.
/// </remarks>
public sealed class DisplayTextOnScreenTests
{
    /// <summary>Shows the window on a panel and lays it out.</summary>
    /// <param name="vm">The view model.</param>
    /// <param name="panel">The panel to show.</param>
    /// <returns>The window.</returns>
    private static MainWindow Show(MainViewModel vm, NavigationPanel panel)
    {
        vm.NavigateToPanelCommand.Execute(panel);
        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(1400, 1400));
        window.Arrange(new global::Avalonia.Rect(0, 0, 1400, 1400));
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>The texts a control renders.</summary>
    /// <param name="control">The control.</param>
    /// <returns>Its visual TextBlocks' texts.</returns>
    private static List<string> RenderedTexts(Control control) =>
        control.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();

    /// <summary>Finds a combo box by its automation name.</summary>
    /// <param name="window">The window.</param>
    /// <param name="name">The automation name.</param>
    /// <returns>The combo box.</returns>
    private static ComboBox Combo(MainWindow window, string name) =>
        window.GetLogicalDescendants().OfType<ComboBox>()
            .Single(c => global::Avalonia.Automation.AutomationProperties.GetName(c) == name);

    /// <summary>The Houses box, closed, shows "Whole Sign", the exports' name, not "WholeSign".</summary>
    [AvaloniaFact]
    public void HousesCombo_ClosedBox_ShowsTheName()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        vm.ChartEntry.SelectedHouseSystem = HouseSystem.WholeSign;
        MainWindow window = Show(vm, NavigationPanel.Chart);

        ComboBox houses = Combo(window, "Houses");

        Assert.Equal(HouseSystem.WholeSign, houses.SelectedItem);
        List<string> texts = RenderedTexts(houses);
        Assert.Contains("Whole Sign", texts);
        Assert.DoesNotContain("WholeSign", texts);
    }

    /// <summary>The planetary return's "Find by" box shows "By Year", not "ByYear".</summary>
    [AvaloniaFact]
    public void FindByCombo_ShowsByYear()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        vm.Returns.SelectedReturnFindMode = ReturnFindMode.ByYear;
        MainWindow window = Show(vm, NavigationPanel.Returns);

        ComboBox findBy = Combo(window, "Planetary return find mode");

        Assert.Equal(ReturnFindMode.ByYear, findBy.SelectedItem);
        List<string> texts = RenderedTexts(findBy);
        Assert.Contains("By Year", texts);
        Assert.DoesNotContain("ByYear", texts);
    }

    /// <summary>
    /// A saved-chart row prints the reading as typed, "(1947-10-05 09:30:00)", not the stored round-trip text.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [AvaloniaFact]
    public async Task SavedChartRow_PrintsTheReadingAsTyped()
    {
        MainViewModel vm = MainViewModelFactory.Create(repository: new SavedChartsRepository("Harrisburg"));
        MainWindow window = Show(vm, NavigationPanel.Chart);
        await vm.InitializeAsync();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(1400, 1400));
        window.Arrange(new global::Avalonia.Rect(0, 0, 1400, 1400));
        Dispatcher.UIThread.RunJobs();

        ListBox list = window.GetLogicalDescendants().OfType<ListBox>()
            .Single(l => global::Avalonia.Automation.AutomationProperties.GetName(l) == "Saved charts");

        Assert.Contains(
            "Birth — Harrisburg, Pennsylvania, United States (1947-10-05 09:30:00)",
            RenderedTexts(list));
    }
}
