using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Pins the Relocation panel's wiring — the parts a build cannot check.
/// </summary>
/// <remarks>
/// <para>
/// The panel reads as one birth chart, then a new place (1.4.0, item 3, branch
/// Relocate): a description line, a Birth Chart section holding the saved-chart
/// picker, a New Location section of five rows, and Calculate, Cancel and
/// Export. Four of its five text inputs are coordinate or name fields with no
/// content of their own, and its one Cancel button is identical in caption to
/// others in the head. The load-bearing, compiler-invisible pieces are
/// therefore: which command each button runs, the cancellation key, the
/// automation names, the sections' order and the wording.
/// </para>
/// <para>
/// Until 1.4.0 a grey hint hung under the picker by a hard-coded copy of the
/// label column (<c>Margin="80,0,0,4"</c> in WPF, 100 here), and a test here
/// guarded that copy. Every line now sits in a grid's field column, so no
/// margin tracks the column, and <c>LabelColumnAlignmentTests</c> covers both
/// grids.
/// </para>
/// </remarks>
public sealed class RelocationPanelTests
{
    /// <summary>
    /// The number of push buttons in the Relocation panel: Find, Calculate
    /// Relocation, Cancel, Export Relocation MD. The WPF original's fifth, which
    /// copied the chart's birth place, left the panel in 1.4.0 (branch
    /// Relocate): relocating a chart to its own birthplace gives the birth chart
    /// back.
    /// </summary>
    private const int ExpectedButtonCount = 4;

    /// <summary>
    /// Builds the window with a real view model, switched to the Relocation
    /// panel and laid out.
    /// </summary>
    /// <returns>The view model and the Relocation panel's arranged root.</returns>
    private static (MainViewModel Vm, StackPanel Panel) ShowRelocationPanel()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        vm.NavigateToPanelCommand.Execute(NavigationPanel.Relocation);

        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(1400, 1400));
        window.Arrange(new global::Avalonia.Rect(0, 0, 1400, 1400));
        Dispatcher.UIThread.RunJobs();

        StackPanel panel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("RelocationPanelRoot"));

        return (vm, panel);
    }

    /// <summary>Selects the panel's push buttons, excluding toggles.</summary>
    /// <param name="panel">The panel root to search.</param>
    /// <returns>The buttons a user can press to run a command.</returns>
    private static List<Button> PushButtons(StackPanel panel) =>
        panel.GetLogicalDescendants()
             .OfType<Button>()
             .Where(b => b is not ToggleButton)
             .ToList();

    /// <summary>
    /// Every button in the Relocation panel is bound to a command, and there are
    /// exactly four.
    /// </summary>
    [AvaloniaFact]
    public void EveryRelocationButton_IsBoundToACommand()
    {
        (_, StackPanel panel) = ShowRelocationPanel();

        List<Button> buttons = PushButtons(panel);

        Assert.True(
            buttons.Count == ExpectedButtonCount,
            $"Expected {ExpectedButtonCount} buttons, found {buttons.Count}: " +
            string.Join(", ", buttons.Select(b => b.Content ?? "<null>")));
        Assert.All(buttons, b => Assert.NotNull(b.Command));
    }

    /// <summary>
    /// Each button runs its own command, not a neighbour's.
    /// </summary>
    /// <remarks>
    /// Asserted by reference against the view model's own instances. "Find" is
    /// the button most likely to be crossed by a copy-paste from the Electional
    /// panel, which has a location lookup of the same shape.
    /// </remarks>
    [AvaloniaFact]
    public void EachActionButton_IsBoundToItsOwnCommand()
    {
        (MainViewModel vm, StackPanel panel) = ShowRelocationPanel();

        List<Button> buttons = PushButtons(panel);

        Button ByName(string name) => Assert.Single(
            buttons,
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == name);

        Assert.Same(vm.LookupRelocationLocationCommand, ByName("Find relocation location").Command);
        Assert.Same(vm.CalculateRelocationCommand, ByName("Calculate Relocation").Command);
        Assert.Same(vm.ExportRelocationMarkdownCommand, ByName("Export Relocation MD").Command);
    }

    /// <summary>
    /// The Cancel button passes the relocation operation key as its command
    /// parameter.
    /// </summary>
    /// <remarks>
    /// Same load-bearing string as every other panel's Cancel. With it missing
    /// or misspelled the button stops tracking the relocation calculation and
    /// starts tracking any running operation. Compared against
    /// <c>MainViewModel.OperationKeys.Relocation</c> rather than a repeated
    /// literal.
    /// </remarks>
    [AvaloniaFact]
    public void CancelButton_PassesTheOperationKeyAsItsCommandParameter()
    {
        (_, StackPanel panel) = ShowRelocationPanel();

        Button cancel = Assert.Single(
            PushButtons(panel),
            b => b.Content as string == "Cancel");

        Assert.Equal(MainViewModel.OperationKeys.Relocation, cancel.CommandParameter);
    }

    /// <summary>
    /// Every interactive control in the Relocation panel exposes an automation
    /// name.
    /// </summary>
    /// <remarks>
    /// Slice 1.5's convention. This panel needs it more than most: latitude,
    /// longitude and location-name boxes are indistinguishable to an assistive
    /// technology without one, and the head already carries a "Latitude" and a
    /// "Longitude" in the Chart panel, so these names are deliberately prefixed.
    /// </remarks>
    [AvaloniaFact]
    public void EveryInteractiveControl_ExposesAnAutomationName()
    {
        (_, StackPanel panel) = ShowRelocationPanel();

        List<Control> interactive = panel.GetLogicalDescendants()
            .OfType<Control>()
            .Where(c => c is Button or TextBox or ComboBox or CheckBox
                          or CalendarDatePicker or AutoCompleteBox)
            .ToList();

        Assert.NotEmpty(interactive);
        Assert.All(
            interactive,
            c => Assert.False(
                string.IsNullOrWhiteSpace(
                    global::Avalonia.Automation.AutomationProperties.GetName(c)),
                $"{c.GetType().Name} in the Relocation panel has no AutomationProperties.Name."));
    }

    /// <summary>
    /// The panel reads as one birth chart, then a new place: its description
    /// line, then the Birth Chart and New Location sections, in that order.
    /// </summary>
    /// <remarks>
    /// Until 1.4.0 the panel had no sections at all, only a natal-chart picker,
    /// a hint and an ungrouped place form, which read as a comparison of two
    /// charts (item 3, branch Relocate).
    /// </remarks>
    [AvaloniaFact]
    public void SectionHeaders_ReadBirthChartThenNewLocation()
    {
        (_, StackPanel panel) = ShowRelocationPanel();

        Assert.Contains(
            panel.Children.OfType<TextBlock>(),
            t => t.Text == "Recast a birth chart for a new place: the same moment, with new angles and houses.");

        string[] headers = panel.GetLogicalDescendants().OfType<TextBlock>()
            .Where(t => t.Classes.Contains("sectionHeader"))
            .Select(t => t.Text ?? string.Empty)
            .ToArray();

        Assert.Equal(["Birth Chart", "New Location"], headers);
    }

    /// <summary>
    /// The form labels read from top to bottom as rendered: the Birth Chart
    /// section's Chart, then the New Location section's five rows, Results
    /// labelled.
    /// </summary>
    /// <remarks>
    /// Sorted by where each label lands in the panel's coordinates, not by markup
    /// order, so the test pins what the user sees; a label that measured nothing
    /// would sort at the top and fail the comparison. Until 1.4.0 the Results row
    /// had no label in either head.
    /// </remarks>
    [AvaloniaFact]
    public void FormLabels_ReadTopToBottom()
    {
        (_, StackPanel panel) = ShowRelocationPanel();

        List<(string Text, double Top)> labels = panel.GetLogicalDescendants()
            .OfType<TextBlock>()
            .Where(t => t.Classes.Contains("formLabel"))
            .Select(t =>
            {
                global::Avalonia.Point? top = t.TranslatePoint(new global::Avalonia.Point(0, 0), panel);
                Assert.True(top.HasValue, $"Label '{t.Text}' is not in the panel's visual tree.");
                Assert.True(t.Bounds.Height > 0, $"Label '{t.Text}' was not laid out.");
                return (t.Text ?? string.Empty, top.Value.Y);
            })
            .ToList();

        string[] actual = labels.OrderBy(l => l.Top).Select(l => l.Text).ToArray();

        Assert.Equal(["Chart", "Search", "Results", "Latitude", "Longitude", "Location"], actual);
    }

    /// <summary>
    /// No tooltip in the panel speaks of a comparison or of a natal chart beside
    /// the one being relocated, and the picker's and Export's say what they do.
    /// </summary>
    /// <remarks>
    /// Until 1.4.0 Export's tooltip called its file "the relocation comparison
    /// report" and the picker's offered "a saved natal chart" beside "the Chart
    /// panel's chart". The export itself is unchanged: its Natal and Relocated
    /// columns compare one chart at two places.
    /// </remarks>
    [AvaloniaFact]
    public void Tooltips_SpeakOfOneChart()
    {
        (_, StackPanel panel) = ShowRelocationPanel();

        List<(Control Control, string Tip)> tips = [];
        foreach (Control control in panel.GetLogicalDescendants().OfType<Control>())
        {
            if (ToolTip.GetTip(control) is string tip)
            {
                tips.Add((control, tip));
            }
        }

        Assert.NotEmpty(tips);
        Assert.All(tips, t =>
        {
            Assert.DoesNotContain("comparison", t.Tip, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("natal", t.Tip, StringComparison.OrdinalIgnoreCase);
        });

        string TipOf(string name) => Assert.Single(
            tips,
            t => global::Avalonia.Automation.AutomationProperties.GetName(t.Control) == name).Tip;

        Assert.Equal(
            "The birth chart to relocate. Loading or calculating a chart on the Chart panel picks it here.",
            TipOf("Relocation birth chart"));
        Assert.Equal("Recast the birth chart for the new place", TipOf("Calculate Relocation"));
        Assert.Equal(
            "Export the relocated chart, with its angle and house changes, as Markdown",
            TipOf("Export Relocation MD"));
    }

    /// <summary>
    /// Typing into the latitude box reaches the view model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Driven from the user's side, and the view model is watched. These
    /// coordinate fields are the panel's only path to a relocation target when
    /// the location search is not used, and they are parsed only when Calculate
    /// is pressed — so a one-way binding here would surface as "Invalid
    /// relocation coordinates." long after the typing, with nothing pointing at
    /// the binding.
    /// </para>
    /// <para>
    /// The box's own <c>Text</c> is asserted first so that input which never
    /// arrived fails as itself rather than passing vacuously.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void LatitudeBox_CommitsTypedTextToTheViewModel()
    {
        (MainViewModel vm, StackPanel panel) = ShowRelocationPanel();

        TopLevel window = Assert.IsType<MainWindow>(TopLevel.GetTopLevel(panel));

        TextBox latitude = Assert.Single(
            panel.GetLogicalDescendants().OfType<TextBox>(),
            t => global::Avalonia.Automation.AutomationProperties.GetName(t)
                 == "Relocation latitude");

        latitude.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("51.5074");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("51.5074", latitude.Text);
        Assert.Equal("51.5074", vm.Relocation.LatitudeText);
    }

    /// <summary>
    /// An out-of-range latitude draws the box's red border, puts the message in its tooltip and disables
    /// Calculate Relocation; a valid one restores all three; a blank one is not red but keeps Calculate
    /// disabled (port plan §9 item 9, scoping decision 3(i): the time-zone idiom).
    /// </summary>
    /// <remarks>
    /// The red border is read where it is drawn, on the TextBox template's <c>PART_BorderElement</c>, with
    /// the box focused, as it is while the user types: Fluent's own focus style sets that border, so a
    /// style on the TextBox alone would show red only once the box lost focus.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [AvaloniaFact]
    public async Task OutOfRangeLatitude_MarksTheBoxAndDisablesCalculate()
    {
        MainViewModel vm = MainViewModelFactory.Create(repository: new SavedChartsRepository("Alpha"));
        vm.NavigateToPanelCommand.Execute(NavigationPanel.Relocation);
        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        await vm.InitializeAsync();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(1400, 1400));
        window.Arrange(new global::Avalonia.Rect(0, 0, 1400, 1400));
        Dispatcher.UIThread.RunJobs();

        StackPanel panel = Assert.IsType<StackPanel>(window.FindControl<StackPanel>("RelocationPanelRoot"));
        TextBox latitude = Assert.Single(
            panel.GetLogicalDescendants().OfType<TextBox>(),
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == "Relocation latitude");
        Button calculate = Assert.Single(
            PushButtons(panel),
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == "Calculate Relocation");

        vm.Relocation.SelectedNatalChart = vm.AllSavedCharts[0];
        vm.Relocation.LocationName = "London";
        vm.Relocation.LatitudeText = "51.5074";
        vm.Relocation.LongitudeText = "-0.1278";
        latitude.Focus();
        Dispatcher.UIThread.RunJobs();

        Assert.True(calculate.IsEffectivelyEnabled, "Valid coordinates with a chart and a place should enable Calculate.");
        Assert.DoesNotContain("invalidCoordinate", latitude.Classes);
        Assert.Equal(RelocationViewModel.LatitudeHint, ToolTip.GetTip(latitude));

        vm.Relocation.LatitudeText = "95";
        Dispatcher.UIThread.RunJobs();

        Assert.Contains("invalidCoordinate", latitude.Classes);
        Assert.Equal("Latitude must be between -90 and 90 degrees.", ToolTip.GetTip(latitude));
        Assert.False(calculate.IsEffectivelyEnabled, "Calculate is enabled with a latitude of 95.");
        global::Avalonia.Controls.Border border = Assert.Single(
            latitude.GetVisualDescendants().OfType<global::Avalonia.Controls.Border>(),
            b => b.Name == "PART_BorderElement");
        Assert.Same(latitude, window.FocusManager?.GetFocusedElement());
        Assert.Equal(
            global::Avalonia.Media.Colors.Red,
            Assert.IsAssignableFrom<global::Avalonia.Media.ISolidColorBrush>(border.BorderBrush).Color);
        Assert.Equal(new global::Avalonia.Thickness(2), border.BorderThickness);

        vm.Relocation.LatitudeText = string.Empty;
        Dispatcher.UIThread.RunJobs();

        Assert.DoesNotContain("invalidCoordinate", latitude.Classes);
        Assert.False(calculate.IsEffectivelyEnabled, "Calculate is enabled with a blank latitude.");
    }

    /// <summary>
    /// The Birth Chart section's line names the chart in use: none before a chart is loaded, then the loaded
    /// chart, which the picker shows too (1.4.0, item 3, branch Relocate).
    /// </summary>
    /// <remarks>
    /// The line sits in the section grid's field column, under the picker. Load hands the command the
    /// saved-charts list's instance; the picker holds <c>AllSavedCharts</c>' instance with the same Id.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [AvaloniaFact]
    public async Task ChartInUseLine_FollowsALoad()
    {
        MainViewModel vm = MainViewModelFactory.Create(repository: new SavedChartsRepository("Alpha", "Beta"));
        vm.NavigateToPanelCommand.Execute(NavigationPanel.Relocation);
        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        await vm.InitializeAsync();
        Dispatcher.UIThread.RunJobs();

        StackPanel panel = Assert.IsType<StackPanel>(window.FindControl<StackPanel>("RelocationPanelRoot"));
        TextBlock line = Assert.Single(
            panel.GetLogicalDescendants().OfType<TextBlock>(),
            t => global::Avalonia.Automation.AutomationProperties.GetName(t) == "Relocation chart in use");
        ComboBox picker = Assert.Single(
            panel.GetLogicalDescendants().OfType<ComboBox>(),
            c => global::Avalonia.Automation.AutomationProperties.GetName(c) == "Relocation birth chart");

        Assert.Equal("No chart yet: load or calculate one on the Chart panel, or pick a saved chart.", line.Text);

        vm.LoadChartCommand.Execute(vm.SavedCharts.Single(c => c.Name == "Beta"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Beta — Birth, 1947-10-05 09:30:00, Harrisburg, Pennsylvania, United States", line.Text);
        Assert.Same(vm.Relocation.SelectedNatalChart, picker.SelectedItem);
        Assert.Equal("Beta", vm.Relocation.SelectedNatalChart?.Name);
    }

    /// <summary>
    /// The result line under the buttons is hidden until a relocation is calculated, then names the chart, the
    /// place, the Ascendant and the house changes (1.4.0, item 3, branch Relocate). Until then the result showed
    /// only in the status bar.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [AvaloniaFact]
    public async Task ResultLine_ShowsAfterCalculate()
    {
        MainViewModel vm = MainViewModelFactory.Create(repository: new SavedChartsRepository("Alpha"));
        vm.NavigateToPanelCommand.Execute(NavigationPanel.Relocation);
        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        await vm.InitializeAsync();
        Dispatcher.UIThread.RunJobs();

        StackPanel panel = Assert.IsType<StackPanel>(window.FindControl<StackPanel>("RelocationPanelRoot"));
        TextBlock result = Assert.Single(
            panel.GetLogicalDescendants().OfType<TextBlock>(),
            t => global::Avalonia.Automation.AutomationProperties.GetName(t) == "Relocation result");
        Assert.False(result.IsVisible, "The result line shows before any relocation.");

        vm.Relocation.SelectedNatalChart = vm.AllSavedCharts[0];
        vm.Relocation.LocationName = "London";
        vm.Relocation.LatitudeText = "51.5074";
        vm.Relocation.LongitudeText = "-0.1278";
        vm.CalculateRelocationCommand.Execute(null);

        int waited = 0;
        while (vm.CalculateRelocationCommand.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            waited += 10;
        }

        Dispatcher.UIThread.RunJobs();

        Assert.False(vm.CalculateRelocationCommand.IsExecuting, "The relocation did not finish within 5 s.");
        Assert.True(result.IsVisible, "The result line is hidden after a relocation.");
        Assert.Equal("Alpha at London: Ascendant unchanged (Aries), 0 house changes", result.Text);
    }

    /// <summary>
    /// The result line hides, and Export Relocation MD greys, when the place changes after a relocation; the
    /// next Calculate shows the line for the new place and enables Export again (branch StaleResult, 1.6.1).
    /// Until 1.6.1 the line stayed and Export wrote the old result.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [AvaloniaFact]
    public async Task ResultLine_HidesWhenThePlaceChanges()
    {
        MainViewModel vm = MainViewModelFactory.Create(repository: new SavedChartsRepository("Alpha"));
        vm.NavigateToPanelCommand.Execute(NavigationPanel.Relocation);
        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        await vm.InitializeAsync();
        Dispatcher.UIThread.RunJobs();

        StackPanel panel = Assert.IsType<StackPanel>(window.FindControl<StackPanel>("RelocationPanelRoot"));
        TextBlock result = Assert.Single(
            panel.GetLogicalDescendants().OfType<TextBlock>(),
            t => global::Avalonia.Automation.AutomationProperties.GetName(t) == "Relocation result");
        Button export = Assert.Single(
            PushButtons(panel),
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == "Export Relocation MD");

        vm.Relocation.SelectedNatalChart = vm.AllSavedCharts[0];
        vm.Relocation.LocationName = "London";
        vm.Relocation.LatitudeText = "51.5074";
        vm.Relocation.LongitudeText = "-0.1278";
        vm.CalculateRelocationCommand.Execute(null);
        await WaitForRelocationAsync(vm);

        Assert.True(result.IsVisible, "The result line is hidden after a relocation.");
        Assert.True(export.IsEffectivelyEnabled, "Export is disabled with a result to write.");

        vm.Relocation.LocationName = "Paris";
        Dispatcher.UIThread.RunJobs();

        Assert.False(result.IsVisible, "The result line stays after the place changed.");
        Assert.False(export.IsEffectivelyEnabled, "Export stays enabled after the place changed.");

        vm.CalculateRelocationCommand.Execute(null);
        await WaitForRelocationAsync(vm);

        Assert.True(result.IsVisible, "The result line is hidden after the second relocation.");
        Assert.Equal("Alpha at Paris: Ascendant unchanged (Aries), 0 house changes", result.Text);
        Assert.True(export.IsEffectivelyEnabled, "Export is disabled after the second relocation.");
    }

    /// <summary>Waits for Calculate Relocation to finish, running the dispatcher's jobs, for at most 5 s.</summary>
    /// <param name="vm">The view model.</param>
    /// <returns>A task that completes when the command has finished.</returns>
    private static async Task WaitForRelocationAsync(MainViewModel vm)
    {
        int waited = 0;
        while (vm.CalculateRelocationCommand.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            Dispatcher.UIThread.RunJobs();
            waited += 10;
        }

        Dispatcher.UIThread.RunJobs();
        Assert.False(vm.CalculateRelocationCommand.IsExecuting, "The relocation did not finish within 5 s.");
    }
}
