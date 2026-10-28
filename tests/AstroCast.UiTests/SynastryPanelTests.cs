using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Pins the Synastry &amp; Composite panel's wiring — the parts a build cannot
/// check.
/// </summary>
/// <remarks>
/// <para>
/// Compiled bindings already guarantee that every <c>Command</c> and bound
/// property path resolves to a real member. What they do not see is anything
/// that is a plain string, a missing attribute, or an enum-valued property.
/// </para>
/// <para>
/// This panel carries <b>three</b> Cancel buttons whose only distinguishing wiring
/// is a <c>CommandParameter</c> string each — the same shape as the Transits
/// panel's pair. A swap between them (each cancelling the other feature's
/// operation) builds, renders and reads correctly in any review that does not
/// check the parameter, and both captions read "Cancel", so the automation name
/// is the only thing that tells them apart in a tree walk.
/// </para>
/// <para>
/// It also carries two <see cref="ComboBox"/>es that must fill the field column.
/// Avalonia auto-sizes a ComboBox unless told otherwise, so a dropped
/// <c>HorizontalAlignment="Stretch"</c> leaves them hugging the left of the
/// column — visible on the first screenshot, invisible to the compiler.
/// </para>
/// </remarks>
public sealed class SynastryPanelTests
{
    /// <summary>
    /// The number of push buttons in the Synastry panel, counted from the WPF
    /// original: Calculate Synastry, Cancel, Export Synastry MD, Calculate
    /// Composite, Cancel, Export Composite MD, and since 1.7.0 (branch
    /// DavisonPanel) Calculate Davison, Cancel, Export Davison MD.
    /// </summary>
    private const int ExpectedButtonCount = 9;

    /// <summary>The shared label column width, as pinned cross-panel.</summary>
    private const double SharedLabelColumnWidth = 100d;

    /// <summary>
    /// Builds the window with a real view model, switched to the Synastry panel
    /// and laid out.
    /// </summary>
    /// <returns>The view model and the Synastry panel's arranged root.</returns>
    /// <remarks>
    /// The panel must be made visible before layout. All nine panels share the
    /// visual tree, but a collapsed one measures to zero and assertions about
    /// its contents would then pass against nothing.
    /// </remarks>
    private static (MainViewModel Vm, StackPanel Panel) ShowSynastryPanel()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        vm.NavigateToPanelCommand.Execute(NavigationPanel.Synastry);

        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(1400, 1400));
        window.Arrange(new global::Avalonia.Rect(0, 0, 1400, 1400));
        Dispatcher.UIThread.RunJobs();

        StackPanel panel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("SynastryPanelRoot"));

        return (vm, panel);
    }

    /// <summary>
    /// Selects the panel's push buttons, excluding toggles.
    /// </summary>
    /// <param name="panel">The panel root to search.</param>
    /// <returns>The buttons a user can press to run a command.</returns>
    /// <remarks>
    /// <see cref="CheckBox"/> derives from <see cref="ToggleButton"/> derives
    /// from <see cref="Button"/>. This panel has no checkboxes today, but the
    /// filter is kept because the Electional panel's equivalent test learned
    /// that the hard way.
    /// </remarks>
    private static List<Button> PushButtons(StackPanel panel) =>
        panel.GetLogicalDescendants()
             .OfType<Button>()
             .Where(b => b is not ToggleButton)
             .ToList();

    /// <summary>
    /// Every button in the Synastry panel is bound to a command, and there are
    /// exactly nine.
    /// </summary>
    [AvaloniaFact]
    public void EverySynastryButton_IsBoundToACommand()
    {
        (_, StackPanel panel) = ShowSynastryPanel();

        List<Button> buttons = PushButtons(panel);

        Assert.True(
            buttons.Count == ExpectedButtonCount,
            $"Expected {ExpectedButtonCount} buttons, found {buttons.Count}: " +
            string.Join(", ", buttons.Select(b => b.Content ?? "<null>")));
        Assert.All(buttons, b => Assert.NotNull(b.Command));
    }

    /// <summary>
    /// The six non-Cancel buttons each run their own command, not a
    /// neighbour's.
    /// </summary>
    /// <remarks>
    /// Asserted by reference against the view model's own instances. The
    /// realistic mistake in a panel with two near-identical sections is a
    /// copy-paste that leaves the Composite row calling the Synastry commands —
    /// which builds, renders, and enables identically, and produces a synastry
    /// report where a composite one was asked for.
    /// </remarks>
    [AvaloniaFact]
    public void EachActionButton_IsBoundToItsOwnCommand()
    {
        (MainViewModel vm, StackPanel panel) = ShowSynastryPanel();

        List<Button> buttons = PushButtons(panel);

        Button ByName(string name) => Assert.Single(
            buttons,
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == name);

        Assert.Same(vm.CalculateSynastryCommand, ByName("Calculate Synastry").Command);
        Assert.Same(vm.ExportSynastryMarkdownCommand, ByName("Export Synastry MD").Command);
        Assert.Same(vm.CalculateCompositeCommand, ByName("Calculate Composite").Command);
        Assert.Same(vm.ExportCompositeMarkdownCommand, ByName("Export Composite MD").Command);
        Assert.Same(vm.CalculateDavisonCommand, ByName("Calculate Davison").Command);
        Assert.Same(vm.ExportDavisonMarkdownCommand, ByName("Export Davison MD").Command);
    }

    /// <summary>
    /// The three Cancel buttons pass their own operation keys as command
    /// parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>CancelCommand</c>'s <c>CanExecute</c> is
    /// <c>CanCancel(parameter as string)</c>, which returns
    /// <c>HasActiveOperation</c> when the key is null or blank. With a parameter
    /// missing, misspelled, or swapped with its sibling, the button stops
    /// tracking its own feature and starts tracking <i>any</i> running
    /// operation — offering to cancel something it cannot.
    /// </para>
    /// <para>
    /// Both captions read "Cancel", so the buttons are selected by automation
    /// name, which also pins those names to the right visible control. The
    /// values are compared against <c>MainViewModel.OperationKeys</c> rather
    /// than repeated literals, so markup and registry cannot drift apart.
    /// </para>
    /// <para>
    /// Mutation-checked: swapping the two <c>CommandParameter</c> values fails
    /// this test and nothing else in the suite.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void CancelButtons_PassTheirOwnOperationKeysAsCommandParameters()
    {
        (_, StackPanel panel) = ShowSynastryPanel();

        List<Button> cancels = PushButtons(panel)
            .Where(b => b.Content as string == "Cancel")
            .ToList();

        Assert.Equal(3, cancels.Count);

        Button cancelSynastry = Assert.Single(
            cancels,
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == "Cancel Synastry");
        Button cancelComposite = Assert.Single(
            cancels,
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == "Cancel Composite");
        Button cancelDavison = Assert.Single(
            cancels,
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == "Cancel Davison");

        Assert.Equal(MainViewModel.OperationKeys.Synastry, cancelSynastry.CommandParameter);
        Assert.Equal(MainViewModel.OperationKeys.Composite, cancelComposite.CommandParameter);
        Assert.Equal(MainViewModel.OperationKeys.Davison, cancelDavison.CommandParameter);
    }

    /// <summary>
    /// Every interactive control in the Synastry panel exposes an automation
    /// name.
    /// </summary>
    /// <remarks>
    /// Slice 1.5's convention. The two ComboBoxes and two TextBoxes carry no
    /// literal content of their own, so without a name an assistive technology
    /// has nothing to announce — the label beside a field is a sibling, not its
    /// accessible name.
    /// </remarks>
    [AvaloniaFact]
    public void EveryInteractiveControl_ExposesAnAutomationName()
    {
        (_, StackPanel panel) = ShowSynastryPanel();

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
                $"{c.GetType().Name} in the Synastry panel has no AutomationProperties.Name."));
    }

    /// <summary>
    /// Both ComboBoxes stretch to fill the field column rather than auto-sizing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WPF stretches a ComboBox in a <c>*</c> column by default; Avalonia does
    /// not, so every translated ComboBox in this head carries
    /// <c>HorizontalAlignment="Stretch"</c> explicitly. Dropping it is a purely
    /// visual defect — the box shrinks to its content and hugs the left of the
    /// column — which no binding test and no compiler can see.
    /// </para>
    /// <para>
    /// Asserted on arranged geometry rather than on the property, so a future
    /// style that sets alignment some other way still passes as long as the
    /// result is right. Both boxes share row 0/1 of the same grid, so they must
    /// agree with each other and with the grid's field column.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void BothComboBoxes_FillTheFieldColumn()
    {
        (_, StackPanel panel) = ShowSynastryPanel();

        List<ComboBox> boxes = panel.GetLogicalDescendants().OfType<ComboBox>().ToList();

        Assert.Equal(2, boxes.Count);

        Grid grid = Assert.Single(
            panel.GetLogicalDescendants().OfType<Grid>(),
            g => g.Children.OfType<ComboBox>().Any());

        double fieldColumn = grid.Bounds.Width - SharedLabelColumnWidth;

        Assert.True(fieldColumn > 0, "The grid did not arrange; there is nothing to compare against.");

        Assert.All(boxes, b => Assert.True(
            b.Bounds.Width >= fieldColumn - 1,
            $"A Synastry ComboBox arranged {b.Bounds.Width:0.##} px into a " +
            $"{fieldColumn:0.##} px field column — it is auto-sizing, not stretching."));
    }

    /// <summary>
    /// Typing into the relationship-notes box reaches the view model.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Driven from the user's side — focus, then text input — and the view model
    /// is watched. Assigning the property and reading the box back would test
    /// the direction nobody uses, and would pass against a one-way binding that
    /// discards every keystroke in the running panel.
    /// </para>
    /// <para>
    /// The box's own <c>Text</c> is asserted first so that input which never
    /// arrived fails as itself rather than passing vacuously against an
    /// untouched control.
    /// </para>
    /// <para>
    /// This property is not decorative: <c>SynastryMarkdownFormatter</c> writes
    /// it into the exported report, so a broken binding here silently drops user
    /// content from the deliverable.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void RelationshipNotesBox_CommitsTypedTextToTheViewModel()
    {
        (MainViewModel vm, StackPanel panel) = ShowSynastryPanel();

        TopLevel window = Assert.IsType<MainWindow>(TopLevel.GetTopLevel(panel));

        TextBox notes = Assert.Single(
            panel.GetLogicalDescendants().OfType<TextBox>(),
            t => global::Avalonia.Automation.AutomationProperties.GetName(t)
                 == "Relationship notes");

        notes.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("married 15 years");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("married 15 years", notes.Text);
        Assert.Equal("married 15 years", vm.Synastry.RelationshipNotes);
    }

    /// <summary>
    /// The Davison section (branch DavisonPanel, 1.7.0) has its three buttons and its Context box, each by
    /// automation name, the box bound to <see cref="SynastryViewModel.DavisonInterpretationContext"/>.
    /// </summary>
    [AvaloniaFact]
    public void DavisonSection_HasItsThreeButtonsAndContextBox()
    {
        (MainViewModel vm, StackPanel panel) = ShowSynastryPanel();

        List<Button> buttons = PushButtons(panel);
        Button ByName(string name) => Assert.Single(
            buttons,
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == name);

        Assert.Equal("Calculate Davison", ByName("Calculate Davison").Content);
        Assert.Equal("Cancel", ByName("Cancel Davison").Content);
        Assert.Equal("Export Davison MD", ByName("Export Davison MD").Content);

        TextBox context = Assert.Single(
            panel.GetLogicalDescendants().OfType<TextBox>(),
            t => global::Avalonia.Automation.AutomationProperties.GetName(t) == "Davison context");
        vm.Synastry.DavisonInterpretationContext = "a long friendship";
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("a long friendship", context.Text);
    }

    /// <summary>
    /// The "Davison Chart" section header is followed by the grey line that says what the section casts.
    /// </summary>
    [AvaloniaFact]
    public void DavisonExplanation_SitsUnderItsHeader()
    {
        (_, StackPanel panel) = ShowSynastryPanel();

        TextBlock header = Assert.Single(
            panel.Children.OfType<TextBlock>(),
            t => t.Text == "Davison Chart");
        int at = panel.Children.IndexOf(header);
        TextBlock explanation = Assert.IsType<TextBlock>(panel.Children[at + 1]);

        Assert.Equal(
            "A real chart cast for the midpoint of the two births, in time and place; the same two charts as the synastry above.",
            explanation.Text);
    }
}
