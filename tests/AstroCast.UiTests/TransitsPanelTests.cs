using System.Reflection;

using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.SolarArcs;
using AstroCast.Core.Models.Transit;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Pins the Transits panel's wiring — the parts a build cannot check.
/// </summary>
/// <remarks>
/// <para>
/// Compiled bindings already guarantee that every <c>Command</c> and every bound
/// property path resolves to a real member; a typo there is an <c>AVLN2000</c>
/// build error. What they do <b>not</b> see is anything that is a plain string or
/// a missing attribute: the two cancellation keys, and the automation names.
/// </para>
/// <para>
/// This panel hosts <b>two</b> child view models — <c>Transits</c> and
/// <c>ReturnIngressTimeline</c> — and <b>two</b> Cancel buttons whose only
/// distinguishing wiring is a <c>CommandParameter</c> string each. That doubles
/// the panel's load-bearing strings relative to Electional, and a swap between
/// them (each button cancelling the other feature's operation) would build,
/// render and read correctly in every review that does not check the parameter.
/// </para>
/// <para>
/// The seven toggle checkboxes get a behavioural test rather than a structural
/// one, for the same reason as Electional's day checkboxes: the realistic
/// failure is a copy-paste that leaves two of them bound to the same property —
/// a perfectly valid binding, invisible to the compiler and to a glance at the
/// running window.
/// </para>
/// </remarks>
public sealed class TransitsPanelTests
{
    /// <summary>
    /// The number of push buttons in the Transits panel, counted from the WPF
    /// original: Calculate, Cancel, Export Report MD, Export Timeline MD; Export
    /// Daily Horoscope MD; Calculate Timeline, Cancel, Export MD. Calculate and
    /// Export Report MD read "Calculate Transits" and "Export Transit MD" until
    /// 1.4.0 (branch ModesPanel; scoping C1e), when the report came to hold
    /// any of three techniques.
    /// </summary>
    /// <remarks>
    /// A change to this number is a change to the panel's contents and should be
    /// a deliberate edit to this constant rather than silent drift.
    /// </remarks>
    private const int ExpectedButtonCount = 8;

    /// <summary>The number of toggle checkboxes across both features.</summary>
    /// <remarks>
    /// Five on <c>Transits</c>: Transits, Minor, Solar Arc and Progressions since
    /// 1.4.0 (branch ModesPanel), where one Progressions box drove both
    /// directions until then, and Directions since 1.5.0 (branch PrimaryPanel);
    /// two on <c>ReturnIngressTimeline</c>.
    /// </remarks>
    private const int ExpectedCheckBoxCount = 7;

    /// <summary>
    /// The checkbox captions, in the order the markup declares them.
    /// </summary>
    /// <remarks>
    /// The order is load-bearing for the toggle test: it pairs each checkbox
    /// with its own view-model property. Selecting by this array rather than
    /// relying on logical-tree order makes the pairing explicit.
    /// </remarks>
    private static readonly string[] CheckBoxCaptions =
    [
        "Include transits", "Include minor aspects", "Include solar arc directions",
        "Include secondary progressions", "Include primary directions", "Returns", "Ingresses",
    ];

    /// <summary>The Minor checkbox's tooltip, which says why it greys out (scoping C1c).</summary>
    private const string MinorToolTip =
        "Transits only: include the semi-square, sesquiquadrate, semi-sextile and quincunx";

    /// <summary>
    /// Calculate's tooltip, which says why it greys out (scoping C1b; the fourth technique and the Directions
    /// panel's settings since branch PrimaryPanel, 1.5.0).
    /// </summary>
    private const string CalculateToolTip =
        "Calculate the report for the checked techniques over the dates above. Needs a chart on the " +
        "Chart panel and at least one of Transits, Solar Arc, Progressions and Directions checked; " +
        "Directions takes the Directions panel's settings.";

    /// <summary>
    /// Builds the window with a real view model, switched to the Transits panel
    /// and laid out.
    /// </summary>
    /// <returns>
    /// The view model, the window (needed to send keyboard input), and the
    /// Transits panel's arranged root.
    /// </returns>
    /// <remarks>
    /// The panel must be made visible before layout. All ten panels share the
    /// visual tree, but a collapsed one measures to zero and assertions about
    /// its contents would then pass against nothing.
    /// </remarks>
    private static (MainViewModel Vm, MainWindow Window, StackPanel Panel) ShowTransitsPanel()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        vm.NavigateToPanelCommand.Execute(NavigationPanel.Transits);

        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(1400, 1400));
        window.Arrange(new global::Avalonia.Rect(0, 0, 1400, 1400));
        Dispatcher.UIThread.RunJobs();

        StackPanel panel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("TransitsPanelRoot"));

        return (vm, window, panel);
    }

    /// <summary>
    /// Selects the panel's push buttons, excluding checkboxes.
    /// </summary>
    /// <param name="panel">The panel root to search.</param>
    /// <returns>The buttons a user can press to run a command.</returns>
    /// <remarks>
    /// <see cref="CheckBox"/> derives from <see cref="ToggleButton"/>, which
    /// derives from <see cref="Button"/>, so a bare <c>OfType&lt;Button&gt;()</c>
    /// returns this panel's four checkboxes as well as its eight buttons —
    /// twelve where the markup declares eight. The same trap Electional's tests
    /// document.
    /// </remarks>
    private static List<Button> PushButtons(StackPanel panel) =>
        panel.GetLogicalDescendants()
             .OfType<Button>()
             .Where(b => b is not ToggleButton)
             .ToList();

    /// <summary>
    /// Every push button in the Transits panel is bound to a command.
    /// </summary>
    /// <remarks>
    /// A button whose <c>Command</c> attribute was dropped still builds, still
    /// renders and still looks correct — it is simply inert when clicked.
    /// </remarks>
    [AvaloniaFact]
    public void EveryTransitsButton_IsBoundToACommand()
    {
        (_, _, StackPanel panel) = ShowTransitsPanel();

        List<Button> buttons = PushButtons(panel);

        Assert.True(
            buttons.Count == ExpectedButtonCount,
            $"Expected {ExpectedButtonCount} buttons, found {buttons.Count}: " +
            string.Join(", ", buttons.Select(b => b.Content ?? "<null>")));
        Assert.All(buttons, b => Assert.NotNull(b.Command));
    }

    /// <summary>
    /// Each of the two Cancel buttons passes its own feature's cancellation key
    /// as its command parameter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These are the load-bearing strings in the panel, and this panel is the
    /// first with <b>two</b> of them. <c>CancelCommand</c>'s <c>CanExecute</c> is
    /// <c>CanCancel(parameter as string)</c>, which returns
    /// <c>HasActiveOperation</c> when the key is null or blank — so a missing or
    /// misspelled key makes that button track <i>any</i> running operation, and a
    /// <b>swap</b> between the two keys makes each button cancel the other
    /// feature's search. Every one of those states builds and renders
    /// identically to the correct one.
    /// </para>
    /// <para>
    /// The buttons are selected by their automation names, which is what also
    /// pins those names to the right visible control (both captions read
    /// "Cancel"). The values are asserted against the
    /// <c>MainViewModel.OperationKeys</c> constants rather than repeated
    /// literals, so the markup and the registry cannot drift apart silently.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void CancelButtons_PassTheirOwnOperationKeysAsCommandParameters()
    {
        (_, _, StackPanel panel) = ShowTransitsPanel();

        List<Button> cancels = PushButtons(panel)
            .Where(b => b.Content as string == "Cancel")
            .ToList();

        Assert.Equal(2, cancels.Count);

        Button cancelTransits = Assert.Single(
            cancels,
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == "Cancel Transits");
        Button cancelTimeline = Assert.Single(
            cancels,
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == "Cancel Timeline");

        Assert.Equal(MainViewModel.OperationKeys.Transits, cancelTransits.CommandParameter);
        Assert.Equal(
            MainViewModel.OperationKeys.ReturnIngressTimeline,
            cancelTimeline.CommandParameter);
    }

    /// <summary>
    /// Every interactive control in the Transits panel exposes an automation
    /// name.
    /// </summary>
    /// <remarks>
    /// Slice 1.5's convention. The date pickers and year boxes carry no literal
    /// content of their own, so without a name an assistive technology has
    /// nothing to announce — the label beside a field is a sibling, not its
    /// accessible name.
    /// </remarks>
    [AvaloniaFact]
    public void EveryInteractiveControl_ExposesAnAutomationName()
    {
        (_, _, StackPanel panel) = ShowTransitsPanel();

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
                $"{c.GetType().Name} in the Transits panel has no AutomationProperties.Name."));
    }

    /// <summary>
    /// Each toggle checkbox is bound to its own view-model property, across both
    /// child view models, driven the way a user drives it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Seven checkboxes, five on <c>Transits</c> and two on
    /// <c>ReturnIngressTimeline</c>. Each is focused and toggled with the
    /// keyboard, and the assertion is that exactly its own property changed —
    /// which catches both a duplicated binding (two boxes on one property) and a
    /// binding that silently became <c>OneWay</c>, in a single direction of
    /// drive. Driving the view model instead and watching the checkboxes would
    /// miss the <c>OneWay</c> case entirely, as Electional's pair of day tests
    /// documents.
    /// </para>
    /// <para>
    /// <b>The press and the release are both required.</b>
    /// <c>KeyPressQwerty</c> sends key-<i>down</i> only, and
    /// <see cref="ToggleButton"/> activates Space on key-<i>up</i> — a
    /// press-without-release toggles nothing and every assertion reports the
    /// view model unchanged, indistinguishable from a broken binding. The test
    /// therefore asserts the checkbox itself moved before asserting anything
    /// about the view model, so a harness failure names itself.
    /// </para>
    /// <para>
    /// Mutation-checked: pointing two checkboxes at one property, or flipping a
    /// binding to <c>OneWay</c>, fails this test and nothing else.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void ToggleCheckBoxes_EachBindToTheirOwnProperty_WhenDrivenByKeyboard()
    {
        (MainViewModel vm, MainWindow window, StackPanel panel) = ShowTransitsPanel();

        List<CheckBox> boxes = CheckBoxCaptions
            .Select(caption => Assert.Single(
                panel.GetLogicalDescendants().OfType<CheckBox>(),
                c => c.Content as string == caption))
            .ToList();

        Assert.Equal(ExpectedCheckBoxCount, boxes.Count);

        Func<bool>[] getters =
        [
            () => vm.Transits.IncludeTransits,
            () => vm.Transits.IncludeMinorAspects,
            () => vm.Transits.IncludeSolarArcs,
            () => vm.Transits.IncludeSecondaryProgressions,
            () => vm.Transits.IncludePrimaryDirections,
            () => vm.ReturnIngressTimeline.IncludeReturns,
            () => vm.ReturnIngressTimeline.IncludeIngresses,
        ];

        // Normalise to all-true so every toggle below is a true -> false flip.
        // Transits comes before Minor and is restored before Minor's turn, so
        // Minor is enabled when it is focused.
        vm.Transits.IncludeTransits = true;
        vm.Transits.IncludeMinorAspects = true;
        vm.Transits.IncludeSolarArcs = true;
        vm.Transits.IncludeSecondaryProgressions = true;
        vm.Transits.IncludePrimaryDirections = true;
        vm.ReturnIngressTimeline.IncludeReturns = true;
        vm.ReturnIngressTimeline.IncludeIngresses = true;
        Dispatcher.UIThread.RunJobs();

        for (int i = 0; i < boxes.Count; i++)
        {
            Assert.True(
                boxes[i].IsChecked == true,
                $"'{CheckBoxCaptions[i]}' did not follow its property to true; " +
                "the binding is not reaching the control.");

            boxes[i].Focus();
            Dispatcher.UIThread.RunJobs();

            // Down AND up: ToggleButton activates Space on key-up.
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();

            Assert.False(
                boxes[i].IsChecked == true,
                $"'{CheckBoxCaptions[i]}' itself never toggled — the harness did " +
                "not deliver the keypress, so nothing about the binding was tested.");

            for (int j = 0; j < getters.Length; j++)
            {
                bool expected = j != i;
                Assert.True(
                    getters[j]() == expected,
                    $"After toggling '{CheckBoxCaptions[i]}', property {j} " +
                    $"('{CheckBoxCaptions[j]}') reads {getters[j]()} — expected {expected}. " +
                    "Two checkboxes are sharing a property, or the toggle wrote to " +
                    "the wrong one.");
            }

            // Restore for the next iteration.
            boxes[i].Focus();
            Dispatcher.UIThread.RunJobs();
            window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
            window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
            Assert.True(
                getters[i]() && boxes[i].IsChecked == true,
                $"'{CheckBoxCaptions[i]}' failed to restore to true between iterations.");
        }
    }

    /// <summary>The panel's one checkbox with the caption given.</summary>
    /// <param name="panel">The panel root to search.</param>
    /// <param name="caption">The caption.</param>
    /// <returns>The checkbox.</returns>
    private static CheckBox CheckBoxCaptioned(StackPanel panel, string caption) =>
        Assert.Single(panel.GetLogicalDescendants().OfType<CheckBox>(), c => c.Content as string == caption);

    /// <summary>
    /// The panel's one push button with the automation name given, whose caption
    /// reads the same.
    /// </summary>
    /// <remarks>
    /// A button's command gates it through <see cref="InputElement.IsEffectivelyEnabled"/>,
    /// not <see cref="InputElement.IsEnabled"/>, which stays true, so the tests below read
    /// the former: the state the user sees.
    /// </remarks>
    /// <param name="panel">The panel root to search.</param>
    /// <param name="name">The automation name and caption.</param>
    /// <returns>The button.</returns>
    private static Button ButtonNamed(StackPanel panel, string name)
    {
        Button button = Assert.Single(
            PushButtons(panel),
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == name);
        Assert.Equal(name, button.Content as string);
        return button;
    }

    /// <summary>Sets <see cref="MainViewModel.HasResult"/>, which only the view model's own commands write.</summary>
    /// <param name="vm">The view model.</param>
    private static void SetHasResult(MainViewModel vm)
    {
        PropertyInfo property = typeof(MainViewModel).GetProperty(nameof(MainViewModel.HasResult))
            ?? throw new InvalidOperationException("MainViewModel has no HasResult property.");
        property.SetValue(vm, true);
    }

    /// <summary>A transit result with or without the transits, holding a solar arc snapshot.</summary>
    /// <param name="transits">Whether the transit scan ran.</param>
    /// <returns>The result.</returns>
    private static TransitResult TransitResultWith(bool transits) => new()
    {
        NatalChart = new StubChartEngine().Calculate(new ChartInput
        {
            Name = "Test Person",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
        }),
        ReportStartDate = new DateOnly(2026, 4, 12),
        ReportEndDate = new DateOnly(2026, 4, 18),
        MidpointDate = new DateOnly(2026, 4, 15),
        IncludeMinorAspects = false,
        IncludesTransits = transits,
        TransitPositions = new Dictionary<TransitBody, TransitPlanetPosition>(),
        Stations = [],
        Ingresses = [],
        ActiveTransits = [],
        HouseOverlays = [],
        MidpointActivations = [],
        MarsIncluded = false,
        SolarArcs = new SolarArcResult { ArcValue = 26.75, ArcFormatted = "26\u00B045\u2032", Positions = [], Aspects = [] },
    };

    /// <summary>
    /// The Minor checkbox greys out while Transits is unchecked, since the minor
    /// aspects are the transits' only, and its tooltip, shown while it is
    /// disabled, says so (1.4.0 item 2, branch ModesPanel; scoping C1c).
    /// </summary>
    [AvaloniaFact]
    public void MinorAspects_IsDisabledUnlessTransits()
    {
        (MainViewModel vm, _, StackPanel panel) = ShowTransitsPanel();
        CheckBox minor = CheckBoxCaptioned(panel, "Include minor aspects");

        Assert.True(minor.IsEffectivelyEnabled, "Minor is enabled while Transits is checked.");

        vm.Transits.IncludeTransits = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(minor.IsEffectivelyEnabled, "Minor stayed enabled with Transits unchecked.");

        vm.Transits.IncludeTransits = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(minor.IsEffectivelyEnabled, "Minor did not come back with Transits checked.");

        Assert.Equal(MinorToolTip, ToolTip.GetTip(minor) as string);
        Assert.True(ToolTip.GetShowOnDisabled(minor), "Minor's tooltip is not shown while it is disabled.");
    }

    /// <summary>
    /// With a chart on the Chart panel, Calculate greys out while no technique is
    /// checked and comes back when one is, and its tooltip, shown while it is
    /// disabled, says why (scoping C1b).
    /// </summary>
    [AvaloniaFact]
    public void CalculateButton_IsDisabledWithNoTechnique()
    {
        (MainViewModel vm, _, StackPanel panel) = ShowTransitsPanel();
        SetHasResult(vm);
        Dispatcher.UIThread.RunJobs();
        Button calculate = ButtonNamed(panel, "Calculate");

        Assert.True(calculate.IsEffectivelyEnabled, "Calculate is enabled with a chart and every technique checked.");

        vm.Transits.IncludeTransits = false;
        vm.Transits.IncludeSolarArcs = false;
        vm.Transits.IncludeSecondaryProgressions = false;
        vm.Transits.IncludePrimaryDirections = false;
        Dispatcher.UIThread.RunJobs();
        Assert.False(calculate.IsEffectivelyEnabled, "Calculate stayed enabled with no technique checked.");

        vm.Transits.IncludeSecondaryProgressions = true;
        Dispatcher.UIThread.RunJobs();
        Assert.True(calculate.IsEffectivelyEnabled, "Calculate did not come back with Progressions checked.");

        Assert.Equal(CalculateToolTip, ToolTip.GetTip(calculate) as string);
        Assert.True(ToolTip.GetShowOnDisabled(calculate), "Calculate's tooltip is not shown while it is disabled.");
    }

    /// <summary>
    /// For a result without transits Export Timeline MD and Export Daily
    /// Horoscope MD grey out, since both are made of transits, while the report
    /// export stays enabled; a result with transits enables all three (scoping
    /// C1e).
    /// </summary>
    [AvaloniaFact]
    public void DailyAndTimeline_AreDisabledForASolarArcOnlyResult()
    {
        (MainViewModel vm, _, StackPanel panel) = ShowTransitsPanel();
        Button report = ButtonNamed(panel, "Export Report MD");
        Button timeline = ButtonNamed(panel, "Export Timeline MD");
        Button daily = ButtonNamed(panel, "Export Daily Horoscope MD");

        vm.Transits.TransitResult = TransitResultWith(transits: false);
        Dispatcher.UIThread.RunJobs();
        Assert.True(report.IsEffectivelyEnabled, "The report export is disabled for a solar arc result.");
        Assert.False(timeline.IsEffectivelyEnabled, "Export Timeline MD is enabled for a result without transits.");
        Assert.False(daily.IsEffectivelyEnabled, "Export Daily Horoscope MD is enabled for a result without transits.");
        Assert.True(ToolTip.GetShowOnDisabled(timeline), "Export Timeline MD's tooltip is not shown while it is disabled.");
        Assert.True(ToolTip.GetShowOnDisabled(daily), "Export Daily Horoscope MD's tooltip is not shown while it is disabled.");

        vm.Transits.TransitResult = TransitResultWith(transits: true);
        Dispatcher.UIThread.RunJobs();
        Assert.True(report.IsEffectivelyEnabled, "The report export is disabled for a transit result.");
        Assert.True(timeline.IsEffectivelyEnabled, "Export Timeline MD is disabled for a result with transits.");
        Assert.True(daily.IsEffectivelyEnabled, "Export Daily Horoscope MD is disabled for a result with transits.");
    }
}
