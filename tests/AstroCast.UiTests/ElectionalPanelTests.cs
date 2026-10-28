using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Pins the Electional panel's wiring — the parts a build cannot check.
/// </summary>
/// <remarks>
/// <para>
/// Compiled bindings already guarantee that every <c>Command</c> and every bound
/// property path resolves to a real member; a typo there is an <c>AVLN2000</c>
/// build error. What they do <b>not</b> see is anything that is a plain string, a
/// missing attribute, or an enum-valued property: the cancellation key, the
/// automation names, the AutoCompleteBox's filter mode, and the key gesture on
/// the lookup box.
/// </para>
/// <para>
/// The seven day checkboxes get a behavioural test rather than a structural one,
/// because the realistic failure there is a copy-paste that leaves two of them
/// bound to the same <c>Allow*</c> property — which is a perfectly valid binding
/// and so invisible to both the compiler and a glance at the running window.
/// </para>
/// </remarks>
public sealed class ElectionalPanelTests
{
    /// <summary>
    /// The number of buttons in the Electional panel, counted from the WPF
    /// original: Lookup, Use Natal, Calculate Electional, Cancel, Export
    /// Electional MD.
    /// </summary>
    /// <remarks>
    /// A change to this number is a change to the panel's contents and should be
    /// a deliberate edit to this constant rather than silent drift.
    /// </remarks>
    private const int ExpectedButtonCount = 5;

    /// <summary>The number of day-of-week checkboxes, one per day.</summary>
    private const int ExpectedDayCheckBoxCount = 7;

    /// <summary>
    /// The day checkbox captions, in the order the markup declares them.
    /// </summary>
    /// <remarks>
    /// The order is load-bearing for the two day tests: it is what pairs each
    /// checkbox with its own <c>Allow*</c> property. A logical-tree search returns
    /// declaration order, but selecting by this array rather than relying on that
    /// makes the pairing explicit instead of incidental.
    /// </remarks>
    private static readonly string[] DayNames =
        ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

    /// <summary>
    /// Builds the window with a real view model, switched to the Electional panel
    /// and laid out.
    /// </summary>
    /// <returns>The view model and the Electional panel's arranged root.</returns>
    /// <remarks>
    /// The panel must be made visible before layout. All nine panels share the
    /// visual tree, but a collapsed one measures to zero and assertions about its
    /// contents would then pass against nothing.
    /// </remarks>
    private static (MainViewModel Vm, StackPanel Panel) ShowElectionalPanel()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        vm.NavigateToPanelCommand.Execute(NavigationPanel.Electional);

        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(1400, 1400));
        window.Arrange(new global::Avalonia.Rect(0, 0, 1400, 1400));
        Dispatcher.UIThread.RunJobs();

        StackPanel panel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("ElectionalPanelRoot"));

        return (vm, panel);
    }

    /// <summary>
    /// Selects the panel's push buttons, excluding checkboxes.
    /// </summary>
    /// <param name="panel">The panel root to search.</param>
    /// <returns>The buttons a user can press to run a command.</returns>
    /// <remarks>
    /// <para>
    /// <see cref="CheckBox"/> derives from <see cref="ToggleButton"/>, which
    /// derives from <see cref="Button"/>, so a bare <c>OfType&lt;Button&gt;()</c>
    /// returns this panel's eight checkboxes as well as its five buttons —
    /// thirteen where the markup declares five.
    /// </para>
    /// <para>
    /// The Export panel's equivalent test gets away without this filter only
    /// because that panel contains no checkboxes. This one would otherwise assert
    /// <c>Command is not null</c> against the seven day toggles, which are bound
    /// to properties and carry no command at all.
    /// </para>
    /// </remarks>
    private static List<Button> PushButtons(StackPanel panel) =>
        panel.GetLogicalDescendants()
             .OfType<Button>()
             .Where(b => b is not ToggleButton)
             .ToList();

    /// <summary>
    /// Every button in the Electional panel is bound to a command.
    /// </summary>
    /// <remarks>
    /// A button whose <c>Command</c> attribute was dropped still builds, still
    /// renders and still looks correct — it is simply inert when clicked.
    /// </remarks>
    [AvaloniaFact]
    public void EveryElectionalButton_IsBoundToACommand()
    {
        (_, StackPanel panel) = ShowElectionalPanel();

        List<Button> buttons = PushButtons(panel);

        Assert.True(
            buttons.Count == ExpectedButtonCount,
            $"Expected {ExpectedButtonCount} buttons, found {buttons.Count}: " +
            string.Join(", ", buttons.Select(b => b.Content ?? "<null>")));
        Assert.All(buttons, b => Assert.NotNull(b.Command));
    }

    /// <summary>
    /// The Cancel button passes the cancellation registry's key as its command
    /// parameter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the load-bearing string in the panel, and it is the same shape as
    /// the Export panel's <c>pdfSave</c>. <c>CancelCommand</c>'s <c>CanExecute</c>
    /// is <c>CanCancel(parameter as string)</c>, which returns
    /// <c>HasActiveOperation</c> when the key is null or blank. With the parameter
    /// missing or misspelled the button therefore stops tracking the electional
    /// search and starts tracking <i>any</i> running operation, offering to cancel
    /// something it cannot.
    /// </para>
    /// <para>
    /// The value is asserted against <c>MainViewModel.OperationKeys.Electional</c>
    /// rather than a repeated literal, so the markup and the registry cannot drift
    /// apart silently.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void CancelButton_PassesTheOperationKeyAsItsCommandParameter()
    {
        (_, StackPanel panel) = ShowElectionalPanel();

        Button cancel = Assert.Single(
            panel.GetLogicalDescendants().OfType<Button>(),
            b => b.Content as string == "Cancel");

        Assert.Equal(MainViewModel.OperationKeys.Electional, cancel.CommandParameter);
    }

    /// <summary>
    /// Every interactive control in the Electional panel exposes an automation
    /// name.
    /// </summary>
    /// <remarks>
    /// Slice 1.5's convention. Unlike the Export panel, most controls here are
    /// text boxes and combo boxes carrying no literal content of their own, so
    /// without a name an assistive technology has nothing at all to announce —
    /// the label beside a field is a sibling, not its accessible name.
    /// </remarks>
    [AvaloniaFact]
    public void EveryInteractiveControl_ExposesAnAutomationName()
    {
        (_, StackPanel panel) = ShowElectionalPanel();

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
                $"{c.GetType().Name} in the Electional panel has no AutomationProperties.Name."));
    }

    /// <summary>
    /// The timezone box defers filtering to the view model instead of applying
    /// its own.
    /// </summary>
    /// <remarks>
    /// <c>FilterMode</c> defaults to <c>StartsWith</c>. Left at the default it
    /// intersects with the view model's <c>Contains</c> filter, so typing "York"
    /// would stop finding <c>America/New_York</c> — a silent narrowing that looks
    /// like missing data rather than a bug. <c>IsTextCompletionEnabled</c> stays
    /// false so the control never rewrites what was typed.
    /// </remarks>
    [AvaloniaFact]
    public void TimezoneBox_DefersFilteringToTheViewModel()
    {
        (_, StackPanel panel) = ShowElectionalPanel();

        AutoCompleteBox box = Assert.Single(
            panel.GetLogicalDescendants().OfType<AutoCompleteBox>());

        Assert.Equal(AutoCompleteFilterMode.None, box.FilterMode);
        Assert.False(box.IsTextCompletionEnabled);
        Assert.Equal(0, box.MinimumPrefixLength);
    }

    /// <summary>
    /// Each day checkbox is bound to its own <c>Allow*</c> property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Asserted behaviourally, by clearing one day at a time and counting how many
    /// boxes went clear. Two checkboxes bound to the same property is a valid
    /// binding that the compiler accepts and that looks right until a search
    /// silently drops a day, and a structural test that merely counts seven
    /// checkboxes would not catch it.
    /// </para>
    /// <para>
    /// Mutation-checked: pointing any two of the seven at the same property makes
    /// the expected-unchecked count 2 and fails this test.
    /// </para>
    /// <para>
    /// <b>What it cannot observe.</b> It drives the view model and watches the
    /// checkboxes, which is the opposite of the direction a user drives. A binding
    /// changed to <c>Mode=OneWay</c> would leave this test green while every click
    /// in the running panel did nothing at all. That direction is
    /// <see cref="DayCheckBoxes_TogglingWithTheKeyboard_ReachesTheMatchingProperty"/>,
    /// and the two are complementary — keep both.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void DayCheckBoxes_EachBindToADistinctAllowedDayProperty()
    {
        (MainViewModel vm, StackPanel panel) = ShowElectionalPanel();

        List<CheckBox> days = panel.GetLogicalDescendants()
            .OfType<CheckBox>()
            .Where(c => c.Content as string is "Mon" or "Tue" or "Wed"
                          or "Thu" or "Fri" or "Sat" or "Sun")
            .ToList();

        Assert.Equal(ExpectedDayCheckBoxCount, days.Count);

        Action<bool>[] setters =
        [
            v => vm.Electional.AllowMonday = v,
            v => vm.Electional.AllowTuesday = v,
            v => vm.Electional.AllowWednesday = v,
            v => vm.Electional.AllowThursday = v,
            v => vm.Electional.AllowFriday = v,
            v => vm.Electional.AllowSaturday = v,
            v => vm.Electional.AllowSunday = v,
        ];

        foreach (Action<bool> setter in setters)
        {
            // Start from all-checked so exactly one box may change.
            foreach (Action<bool> reset in setters)
            {
                reset(true);
            }

            Dispatcher.UIThread.RunJobs();

            setter(false);
            Dispatcher.UIThread.RunJobs();

            int cleared = days.Count(c => c.IsChecked == false);

            Assert.Equal(1, cleared);
        }
    }

    /// <summary>
    /// Toggling a day checkbox from the keyboard clears that day's own
    /// <c>Allow*</c> property, and only that one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The other direction, and the one the user actually drives.</b>
    /// <see cref="DayCheckBoxes_EachBindToADistinctAllowedDayProperty"/> sets the
    /// view model and watches the boxes; this focuses a box, presses Space, and
    /// watches the view model. The gap between them is not theoretical: a binding
    /// changed to <c>Mode=OneWay</c> satisfies the first test completely while
    /// every click in the running panel does nothing, because the checkbox toggles
    /// its own visual state and never writes back. Only this test sees that.
    /// </para>
    /// <para>
    /// <b>Why Space rather than a synthetic click.</b> Space is what
    /// <see cref="ToggleButton"/> handles for keyboard activation, and it needs
    /// nothing but focus. A pointer press has to land on the control's rendered
    /// bounds, which makes the test depend on layout arithmetic that has nothing
    /// to do with the binding under test — and, as the drop-down work found, a
    /// synthetic click in a headless host does not always land where the
    /// coordinates say.
    /// </para>
    /// <para>
    /// <b>The press and the release are both required, and the release is easy to
    /// miss.</b> <c>KeyPressQwerty</c> sends key-<i>down</i> only.
    /// <see cref="ToggleButton"/> activates Space on key-<i>up</i>, so a
    /// press-without-release toggles nothing at all and every assertion below
    /// would report the view model unchanged — looking exactly like a broken
    /// binding. Measured while writing this test: press alone left
    /// <c>IsChecked=True</c> and <c>AllowMonday=True</c>; press plus release gave
    /// <c>False</c> and <c>False</c>. Controls that act on key-down —
    /// <c>AutoCompleteBox</c> with Enter, as in <c>TimezoneCommitTests</c> — hide
    /// this, so the pattern does not transfer between control types.
    /// </para>
    /// <para>
    /// <b>Distinctness is asserted from this side too.</b> Each press is made from
    /// an all-checked state and the test counts how many properties went false, so
    /// two checkboxes bound to the same property fail here as well — from the
    /// opposite direction to the sibling test. That redundancy is deliberate: the
    /// shared-property mistake is the realistic one, and it is worth catching
    /// whichever way the panel is driven.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void DayCheckBoxes_TogglingWithTheKeyboard_ReachesTheMatchingProperty()
    {
        (MainViewModel vm, StackPanel panel) = ShowElectionalPanel();

        TopLevel window = Assert.IsType<MainWindow>(TopLevel.GetTopLevel(panel));

        // Selected by caption so each box is paired with its own property rather
        // than with whatever position a tree walk happened to return it in.
        List<CheckBox> days = DayNames
            .Select(name => Assert.Single(
                panel.GetLogicalDescendants().OfType<CheckBox>(),
                c => c.Content as string == name))
            .ToList();

        Assert.Equal(ExpectedDayCheckBoxCount, days.Count);

        Action<bool>[] setters =
        [
            v => vm.Electional.AllowMonday = v,
            v => vm.Electional.AllowTuesday = v,
            v => vm.Electional.AllowWednesday = v,
            v => vm.Electional.AllowThursday = v,
            v => vm.Electional.AllowFriday = v,
            v => vm.Electional.AllowSaturday = v,
            v => vm.Electional.AllowSunday = v,
        ];

        Func<bool>[] readers =
        [
            () => vm.Electional.AllowMonday,
            () => vm.Electional.AllowTuesday,
            () => vm.Electional.AllowWednesday,
            () => vm.Electional.AllowThursday,
            () => vm.Electional.AllowFriday,
            () => vm.Electional.AllowSaturday,
            () => vm.Electional.AllowSunday,
        ];

        for (int i = 0; i < days.Count; i++)
        {
            // Start from all-allowed so exactly one property may change.
            foreach (Action<bool> reset in setters)
            {
                reset(true);
            }

            Dispatcher.UIThread.RunJobs();

            days[i].Focus();
            Dispatcher.UIThread.RunJobs();

            // Down AND up: ToggleButton activates Space on key-up.
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.False(
                days[i].IsChecked,
                $"Space did not toggle the {DayNames[i]} checkbox itself, so nothing " +
                "about the binding was exercised — this is a broken test, not a " +
                "broken binding. Check the box took focus and that the key-up was " +
                "sent as well as the key-down.");

            Assert.False(
                readers[i](),
                $"Toggling {DayNames[i]} did not reach its Allow* property. The " +
                "checkbox changed on screen and the view model did not — the " +
                "binding is not writing back.");

            int cleared = readers.Count(read => !read());

            Assert.Equal(1, cleared);
        }
    }

    /// <summary>
    /// Enter in the location lookup box runs the lookup command.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The WPF original expresses this as <c>TextBox.InputBindings</c> with
    /// <c>KeyBinding Key="Return"</c> — the only per-control input binding in that
    /// markup, so there was no existing pattern in this head to copy. Losing it
    /// costs nothing visible: the field still works, the button still works, and
    /// only the keyboard path disappears.
    /// </para>
    /// <para>
    /// The gesture and the command are both asserted, and the command is compared
    /// by reference against the one the Lookup button uses, so a binding pointed
    /// at some other command would fail rather than merely being non-null.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void LookupBox_BindsEnterToTheLookupCommand()
    {
        (MainViewModel vm, StackPanel panel) = ShowElectionalPanel();

        TextBox lookup = Assert.Single(
            panel.GetLogicalDescendants().OfType<TextBox>(),
            t => global::Avalonia.Automation.AutomationProperties.GetName(t)
                 == "Electional location lookup");

        KeyBinding binding = Assert.Single(lookup.KeyBindings);

        Assert.Equal(new KeyGesture(Key.Enter), binding.Gesture);
        Assert.Same(vm.LookupElectionalLocationCommand, binding.Command);
    }

    /// <summary>
    /// The Natal Chart picker's tooltip says what the search scores against: the picked chart, which Load and
    /// Calculate &amp; Save pick, else the Chart panel's chart, else one cast from its fields (1.6.1, item C,
    /// branch ElectionalPicker).
    /// </summary>
    /// <remarks>
    /// Until 1.4.0 it said "If empty", an empty mode the box offers no way back to once a chart is picked: it
    /// has no empty entry, and Clear Form does not clear it. From 1.4.0 to 1.6.0 it described the fallback rule
    /// alone; since 1.6.1 the picker follows the Chart panel, and the text says so. The test lived in
    /// <c>RelocationPanelTests</c> until 1.6.1, where the Relocation panel's own hint of the rule had given way.
    /// </remarks>
    [AvaloniaFact]
    public void ElectionalNatalChartTooltip_DescribesTheSelection()
    {
        (_, StackPanel panel) = ShowElectionalPanel();

        ComboBox picker = Assert.Single(
            panel.GetLogicalDescendants().OfType<ComboBox>(),
            c => global::Avalonia.Automation.AutomationProperties.GetName(c) == "Natal chart");
        Assert.Equal(
            "The natal chart the search scores against. Loading or calculating a chart on the Chart panel picks " +
            "it here; with none picked, the Chart panel's chart is used, or else one cast from its fields.",
            ToolTip.GetTip(picker));
    }

    /// <summary>
    /// The line under the Natal Chart picker names the chart in use: none before a chart is loaded, then the
    /// loaded chart, which the picker shows too (1.6.1, item C, branch ElectionalPicker).
    /// </summary>
    /// <remarks>
    /// The line sits in the picker's field cell, under the picker, in the same StackPanel. Load hands the command
    /// the saved-charts list's instance; the picker holds <c>AllSavedCharts</c>' instance with the same Id.
    /// </remarks>
    /// <returns>A task that completes when the assertions have run.</returns>
    [AvaloniaFact]
    public async Task ChartInUseLine_FollowsALoad()
    {
        MainViewModel vm = MainViewModelFactory.Create(repository: new SavedChartsRepository("Alpha", "Beta"));
        vm.NavigateToPanelCommand.Execute(NavigationPanel.Electional);
        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        await vm.InitializeAsync();
        Dispatcher.UIThread.RunJobs();

        StackPanel panel = Assert.IsType<StackPanel>(window.FindControl<StackPanel>("ElectionalPanelRoot"));
        TextBlock line = Assert.Single(
            panel.GetLogicalDescendants().OfType<TextBlock>(),
            t => global::Avalonia.Automation.AutomationProperties.GetName(t) == "Electional chart in use");
        ComboBox picker = Assert.Single(
            panel.GetLogicalDescendants().OfType<ComboBox>(),
            c => global::Avalonia.Automation.AutomationProperties.GetName(c) == "Natal chart");

        Assert.Equal(
            "No chart yet: pick a saved chart, or load or calculate one on the Chart panel; otherwise Calculate " +
            "casts one from the Chart panel's fields.",
            line.Text);

        vm.LoadChartCommand.Execute(vm.SavedCharts.Single(c => c.Name == "Beta"));
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Beta — Birth, 1947-10-05 09:30:00, Harrisburg, Pennsylvania, United States", line.Text);
        Assert.Same(vm.Electional.SelectedNatalChart, picker.SelectedItem);
        Assert.Equal("Beta", vm.Electional.SelectedNatalChart?.Name);
    }
}
