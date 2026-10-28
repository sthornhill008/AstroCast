using System.Reflection;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Core.Models;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Pins the Directions panel's wiring (branch PrimaryPanel, 1.5.0) — the parts a build cannot check: the
/// words, the automation names, which command each button runs, the cancellation key, the key box's source
/// and names, the four settings' bindings driven from the keyboard, the gate on Calculate Directions, and the
/// result line and list shown once there is a result.
/// </summary>
public sealed class DirectionsPanelTests
{
    /// <summary>Calculate Directions' tooltip, shown while it is disabled, which says what it needs.</summary>
    private const string CalculateToolTip =
        "List the primary directions of the chart on the Chart panel. " +
        "Needs a chart there, an age limit of 1 to 120 years and an uncertainty of 0 to 120.";

    /// <summary>The automation names of the panel's interactive controls.</summary>
    private static readonly string[] InteractiveNames =
    [
        "Primary directions key", "Add Mercury to Pluto as significators", "Primary directions age limit",
        "Birth-time uncertainty", "Calculate Directions", "Cancel Directions", "Export Directions MD",
        "Primary directions list",
    ];

    /// <summary>Builds the window with a real view model, switched to the Directions panel and laid out.</summary>
    /// <returns>The view model, the window and the panel's arranged root.</returns>
    private static (MainViewModel Vm, MainWindow Window, StackPanel Panel) ShowDirectionsPanel()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        vm.NavigateToPanelCommand.Execute(NavigationPanel.Directions);

        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        Lay(window);

        StackPanel panel = Assert.IsType<StackPanel>(window.FindControl<StackPanel>("DirectionsPanelRoot"));
        return (vm, window, panel);
    }

    /// <summary>Runs the dispatcher and lays the window out at 1400 × 1400.</summary>
    /// <param name="window">The window.</param>
    private static void Lay(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(1400, 1400));
        window.Arrange(new global::Avalonia.Rect(0, 0, 1400, 1400));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>A control's automation name.</summary>
    /// <param name="control">The control.</param>
    /// <returns>Its name, or null.</returns>
    private static string? NameOf(Control control) => global::Avalonia.Automation.AutomationProperties.GetName(control);

    /// <summary>The panel's one control of a type with the automation name given.</summary>
    /// <typeparam name="T">The control's type.</typeparam>
    /// <param name="panel">The panel root.</param>
    /// <param name="name">The automation name.</param>
    /// <returns>The control.</returns>
    private static T Named<T>(StackPanel panel, string name)
        where T : Control =>
        Assert.Single(panel.GetLogicalDescendants().OfType<T>(), c => NameOf(c) == name);

    /// <summary>The panel's push buttons, toggles excluded (a CheckBox derives from Button in Avalonia).</summary>
    /// <param name="panel">The panel root.</param>
    /// <returns>The buttons.</returns>
    private static List<Button> PushButtons(StackPanel panel) =>
        panel.GetLogicalDescendants().OfType<Button>().Where(b => b is not ToggleButton).ToList();

    /// <summary>Sets <see cref="MainViewModel.HasResult"/>, which only the view model's own commands write.</summary>
    /// <param name="vm">The view model.</param>
    private static void SetHasResult(MainViewModel vm)
    {
        PropertyInfo property = typeof(MainViewModel).GetProperty(nameof(MainViewModel.HasResult))
            ?? throw new InvalidOperationException("MainViewModel has no HasResult property.");
        property.SetValue(vm, true);
    }

    /// <summary>A result of two synthetic hits over a stub chart named as the J2000 fixture is.</summary>
    /// <returns>The result.</returns>
    private static PrimaryDirectionResult Result()
    {
        PrimaryDirectionHit hit = new()
        {
            Significator = "Midheaven",
            Promissor = "Moon",
            PromissorKind = PrimaryDirectionPromissorKind.Body,
            Aspect = PrimaryDirectionAspect.Conjunction,
            AspectAngle = 0.0,
            Motion = PrimaryDirectionMotion.Direct,
            Arc = 16.001096,
            Years = 16.234098,
            JulianDayUt = 2457474.377752,
            InstantUtc = new DateTime(2016, 3, 26, 21, 3, 58, DateTimeKind.Utc),
            MonthsPerMinute = -2.9539,
        };
        return new PrimaryDirectionResult
        {
            NatalChart = new StubChartEngine().Calculate(new ChartInput
            {
                Name = "J2000 NYC Fixture",
                LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
                TimeZone = "America/New_York",
                Latitude = 40.7128,
                Longitude = -74.006,
                LocationName = "New York City",
                EventType = "Birth",
            }),
            Settings = PrimaryDirectionSettings.Default,
            Hits = [hit, hit],
            LeftOutCount = 60,
        };
    }

    /// <summary>
    /// The panel reads its title, its line, one Settings section, and the four rows Key, Planets, Age limit and
    /// Uncertainty from top to bottom as rendered.
    /// </summary>
    [AvaloniaFact]
    public void Panel_ReadsTitleLineSectionAndFourRows()
    {
        (_, _, StackPanel panel) = ShowDirectionsPanel();

        List<TextBlock> blocks = panel.GetLogicalDescendants().OfType<TextBlock>().ToList();
        Assert.Contains(blocks, t => t.Classes.Contains("panelTitle") && t.Text == "Primary Directions");
        Assert.Contains(
            blocks,
            t => t.Text == "A dated list of Placidus semi-arc directions, zodiacal, direct and converse, for the chart on the Chart panel.");
        Assert.Equal(["Settings"], blocks.Where(t => t.Classes.Contains("sectionHeader")).Select(t => t.Text ?? string.Empty));

        string[] labels = blocks
            .Where(t => t.Classes.Contains("formLabel"))
            .Select(t =>
            {
                global::Avalonia.Point? top = t.TranslatePoint(new global::Avalonia.Point(0, 0), panel);
                Assert.True(top.HasValue && t.Bounds.Height > 0, $"Label '{t.Text}' was not laid out.");
                return (Text: t.Text ?? string.Empty, Top: top.Value.Y);
            })
            .OrderBy(l => l.Top)
            .Select(l => l.Text)
            .ToArray();

        Assert.Equal(["Key", "Planets", "Age limit", "Uncertainty"], labels);
    }

    /// <summary>Every interactive control carries its automation name, and the result line its own.</summary>
    [AvaloniaFact]
    public void EveryControl_HasItsAutomationName()
    {
        (_, _, StackPanel panel) = ShowDirectionsPanel();

        List<string?> names = panel.GetLogicalDescendants()
            .OfType<Control>()
            .Where(c => c is Button or TextBox or ComboBox or CheckBox)
            .Select(NameOf)
            .ToList();

        Assert.Equal(InteractiveNames.OrderBy(n => n, StringComparer.Ordinal), names.OrderBy(n => n, StringComparer.Ordinal));
        Assert.Single(panel.GetLogicalDescendants().OfType<TextBlock>(), t => NameOf(t) == "Primary directions result");
    }

    /// <summary>The three buttons run their own commands, by reference.</summary>
    [AvaloniaFact]
    public void Buttons_RunTheirOwnCommands()
    {
        (MainViewModel vm, _, StackPanel panel) = ShowDirectionsPanel();

        Assert.Equal(3, PushButtons(panel).Count);
        Assert.Same(vm.CalculateDirectionsCommand, Named<Button>(panel, "Calculate Directions").Command);
        Assert.Same(vm.CancelCommand, Named<Button>(panel, "Cancel Directions").Command);
        Assert.Same(vm.ExportDirectionsMarkdownCommand, Named<Button>(panel, "Export Directions MD").Command);
    }

    /// <summary>Cancel passes the directions operation key, so it tracks this calculation and no other.</summary>
    [AvaloniaFact]
    public void CancelButton_PassesTheDirectionsOperationKey()
    {
        (_, _, StackPanel panel) = ShowDirectionsPanel();

        Button cancel = Named<Button>(panel, "Cancel Directions");

        Assert.Equal("Cancel", cancel.Content as string);
        Assert.Equal(MainViewModel.OperationKeys.Directions, cancel.CommandParameter);
    }

    /// <summary>
    /// Each setting, driven from the keyboard, moves its own property and no other: the key box by the Down arrow,
    /// the checkbox by Space, the two numbers by typing over them.
    /// </summary>
    [AvaloniaFact]
    public void Settings_FollowTheirOwnProperties_WhenDrivenByKeyboard()
    {
        (MainViewModel vm, MainWindow window, StackPanel panel) = ShowDirectionsPanel();
        DirectionsViewModel d = vm.Directions;
        (PrimaryDirectionKey, bool, int, int) Snapshot() =>
            (d.Key, d.PlanetsAsSignificators, d.AgeLimitYears, d.BirthTimeUncertaintyMinutes);

        Assert.Equal((PrimaryDirectionKey.Naibod, false, 100, 0), Snapshot());

        ComboBox key = Named<ComboBox>(panel, "Primary directions key");
        key.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(key.SelectedIndex == 1, "The Down arrow did not move the key box; nothing about its binding was tested.");
        Assert.Equal((PrimaryDirectionKey.Ptolemy, false, 100, 0), Snapshot());

        CheckBox planets = Named<CheckBox>(panel, "Add Mercury to Pluto as significators");
        planets.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        Assert.True(planets.IsChecked == true, "The checkbox never toggled; nothing about its binding was tested.");
        Assert.Equal((PrimaryDirectionKey.Ptolemy, true, 100, 0), Snapshot());

        TextBox age = Named<TextBox>(panel, "Primary directions age limit");
        age.Focus();
        age.SelectAll();
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput("90");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("90", age.Text);
        Assert.Equal((PrimaryDirectionKey.Ptolemy, true, 90, 0), Snapshot());

        TextBox uncertainty = Named<TextBox>(panel, "Birth-time uncertainty");
        uncertainty.Focus();
        uncertainty.SelectAll();
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput("4");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("4", uncertainty.Text);
        Assert.Equal((PrimaryDirectionKey.Ptolemy, true, 90, 4), Snapshot());
    }

    /// <summary>
    /// The key box lists <see cref="DirectionsViewModel.AvailableKeys"/> itself and shows the selected key by its
    /// display name, never the enum's.
    /// </summary>
    [AvaloniaFact]
    public void KeyBox_ListsTheAvailableKeysByTheirNames()
    {
        (MainViewModel vm, MainWindow window, StackPanel panel) = ShowDirectionsPanel();
        ComboBox key = Named<ComboBox>(panel, "Primary directions key");

        Assert.Same(DirectionsViewModel.AvailableKeys, key.ItemsSource);

        vm.Directions.Key = PrimaryDirectionKey.SolarArcRightAscension;
        Lay(window);

        Assert.Equal(PrimaryDirectionKey.SolarArcRightAscension, key.SelectedItem);
        List<string> texts = key.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text ?? string.Empty).ToList();
        Assert.Contains("True solar arc in right ascension", texts);
        Assert.DoesNotContain("SolarArcRightAscension", texts);
    }

    /// <summary>With a result the list shows, read-only, unwrapped, in the crossing report's font, holding the list text.</summary>
    [AvaloniaFact]
    public void List_WithAResult_IsReadOnlyAndShowsTheListText()
    {
        (MainViewModel vm, MainWindow window, StackPanel panel) = ShowDirectionsPanel();
        TextBox list = Named<TextBox>(panel, "Primary directions list");

        Assert.False(list.IsVisible, "The list shows before there is a result.");

        vm.Directions.CurrentResult = Result();
        Lay(window);

        Assert.True(list.IsVisible, "The list does not show with a result.");
        Assert.True(list.IsReadOnly);
        Assert.Equal(vm.Directions.ListText, list.Text);
        Assert.Equal(global::Avalonia.Media.TextWrapping.NoWrap, list.TextWrapping);
        Assert.Equal("Consolas", list.FontFamily.Name);
    }

    /// <summary>
    /// Calculate Directions greys out without a chart, and with an age limit of 0, and its tooltip, shown while it is
    /// disabled, says what it needs.
    /// </summary>
    [AvaloniaFact]
    public void Calculate_GreysOutWithoutAChartOrValidSettings()
    {
        (MainViewModel vm, _, StackPanel panel) = ShowDirectionsPanel();
        Button calculate = Named<Button>(panel, "Calculate Directions");

        Assert.False(calculate.IsEffectivelyEnabled, "Calculate Directions is enabled with no chart.");

        SetHasResult(vm);
        Dispatcher.UIThread.RunJobs();
        Assert.True(calculate.IsEffectivelyEnabled, "Calculate Directions is disabled with a chart and the default settings.");

        vm.Directions.AgeLimitYears = 0;
        Dispatcher.UIThread.RunJobs();
        Assert.False(calculate.IsEffectivelyEnabled, "Calculate Directions is enabled with an age limit of 0.");

        Assert.Equal(CalculateToolTip, ToolTip.GetTip(calculate) as string);
        Assert.True(ToolTip.GetShowOnDisabled(calculate), "Calculate Directions' tooltip is not shown while it is disabled.");
    }

    /// <summary>The result line shows only with a result, and reads the result summary.</summary>
    [AvaloniaFact]
    public void ResultLine_ShowsWithAResult()
    {
        (MainViewModel vm, MainWindow window, StackPanel panel) = ShowDirectionsPanel();
        TextBlock line = Named<TextBlock>(panel, "Primary directions result");

        Assert.False(line.IsVisible, "The result line shows before there is a result.");

        vm.Directions.CurrentResult = Result();
        Lay(window);

        Assert.True(line.IsVisible, "The result line does not show with a result.");
        Assert.Equal(vm.Directions.ResultSummary, line.Text);
        Assert.Equal("J2000 NYC Fixture: 2 directions to age 100, Naibod (0°59′08″ a year); 60 left out", line.Text);
    }
}
