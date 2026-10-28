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
/// Pins the Medical Astrology panel's wiring — the parts a build cannot check.
/// </summary>
/// <remarks>
/// <para>
/// This is the smallest translated panel: one text box, one button, five static
/// text blocks. Compiled bindings already prove that <c>Command</c> and every
/// bound path resolve to a real member, so what is left to test is narrow but
/// not empty — which command, whether the box writes back, whether the button
/// tracks its predicate, and the automation names.
/// </para>
/// <para>
/// <b>Why the command is asserted by reference rather than for non-null.</b>
/// The realistic mistake in a panel this small is not a dropped attribute, it is
/// a copy-paste from a neighbouring export button — <c>ExportMarkdownCommand</c>
/// instead of <c>ExportMedicalMarkdownCommand</c>. That builds, renders, enables
/// and disables identically, and writes a natal report where a medical one was
/// asked for. Only an identity comparison sees it, and a cross-head export
/// diff would not: both heads would be wrong in the same way only if both were
/// edited, and the panel would look correct in isolation either way.
/// </para>
/// <para>
/// <b>What is deliberately not here.</b> The panel declares no
/// <c>CommandParameter</c> anywhere, so there is no parameter string to pin —
/// unlike the Electional and Export panels, whose cancellation keys are load
/// bearing. It also declares no form grid; that absence is guarded in
/// <see cref="LabelColumnAlignmentTests"/>, where it belongs, because the reason
/// for it is the shared label column.
/// </para>
/// </remarks>
public sealed class MedicalPanelTests
{
    /// <summary>
    /// The number of buttons in the Medical panel, counted from the WPF
    /// original: Export Medical Report (Markdown).
    /// </summary>
    /// <remarks>
    /// A change to this number is a change to the panel's contents and should be
    /// a deliberate edit to this constant rather than silent drift.
    /// </remarks>
    private const int ExpectedButtonCount = 1;

    /// <summary>
    /// Builds the window with a real view model, switched to the Medical panel
    /// and laid out.
    /// </summary>
    /// <returns>The view model and the Medical panel's arranged root.</returns>
    /// <remarks>
    /// The panel must be made visible before layout. All nine panels share the
    /// visual tree, but a collapsed one measures to zero and assertions about its
    /// contents would then pass against nothing.
    /// </remarks>
    private static (MainViewModel Vm, StackPanel Panel) ShowMedicalPanel()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        vm.NavigateToPanelCommand.Execute(NavigationPanel.Medical);

        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(1400, 1400));
        window.Arrange(new global::Avalonia.Rect(0, 0, 1400, 1400));
        Dispatcher.UIThread.RunJobs();

        StackPanel panel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("MedicalPanelRoot"));

        return (vm, panel);
    }

    /// <summary>
    /// Selects the panel's push buttons, excluding toggles.
    /// </summary>
    /// <param name="panel">The panel root to search.</param>
    /// <returns>The buttons a user can press to run a command.</returns>
    /// <remarks>
    /// The Medical panel contains no checkboxes today, so this filter is
    /// currently a no-op. It is kept because <see cref="CheckBox"/> derives from
    /// <see cref="ToggleButton"/> derives from <see cref="Button"/>, and the
    /// Electional panel's equivalent test had to learn that the hard way — a
    /// bare <c>OfType&lt;Button&gt;()</c> there returned thirteen controls where
    /// the markup declared five.
    /// </remarks>
    private static List<Button> PushButtons(StackPanel panel) =>
        panel.GetLogicalDescendants()
             .OfType<Button>()
             .Where(b => b is not ToggleButton)
             .ToList();

    /// <summary>
    /// The export button runs the medical export command specifically, not some
    /// other export command that happens to be bound.
    /// </summary>
    /// <remarks>
    /// Mutation-checked: rebinding the button to <c>ExportMarkdownCommand</c>
    /// leaves the panel building, rendering and enabling identically, and fails
    /// only this assertion.
    /// </remarks>
    [AvaloniaFact]
    public void ExportButton_IsBoundToTheMedicalExportCommand()
    {
        (MainViewModel vm, StackPanel panel) = ShowMedicalPanel();

        List<Button> buttons = PushButtons(panel);

        Assert.True(
            buttons.Count == ExpectedButtonCount,
            $"Expected {ExpectedButtonCount} button, found {buttons.Count}: " +
            string.Join(", ", buttons.Select(b => b.Content ?? "<null>")));

        Assert.Same(vm.ExportMedicalMarkdownCommand, buttons[0].Command);
    }

    /// <summary>
    /// Every interactive control in the Medical panel exposes an automation
    /// name.
    /// </summary>
    /// <remarks>
    /// Slice 1.5's convention. The health-context box carries no content of its
    /// own, so without a name an assistive technology has nothing to announce:
    /// the label above it is a sibling in a stack panel, not its accessible
    /// name, and nothing in the markup connects the two.
    /// </remarks>
    [AvaloniaFact]
    public void EveryInteractiveControl_ExposesAnAutomationName()
    {
        (_, StackPanel panel) = ShowMedicalPanel();

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
                $"{c.GetType().Name} in the Medical panel has no AutomationProperties.Name."));
    }

    /// <summary>
    /// Typing into the health-context box reaches
    /// <see cref="MainViewModel.MedicalHealthContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Driven from the user's side, deliberately.</b> Assigning
    /// <c>vm.MedicalHealthContext</c> and reading <c>box.Text</c> back would test
    /// the binding in the direction nobody uses. A binding changed to
    /// <c>Mode=OneWay</c> passes that version completely while every keystroke in
    /// the running panel is discarded. This focuses the box, types, and watches
    /// the view model.
    /// </para>
    /// <para>
    /// <b>The box's own Text is asserted first, and that is not redundant.</b>
    /// If focus never landed or the headless input never arrived, both the box
    /// and the view model stay empty — and an assertion that only compared the
    /// view model to <c>""</c> would pass against a control that was never
    /// touched. Checking the box received the text first turns that silent
    /// vacuum into a failure that names its own cause.
    /// </para>
    /// <para>
    /// <b>What this test cannot tell you.</b> It proves the text reaches the view
    /// model, not that the export prints it. That is
    /// <c>PanelExportTests.ExportMedical_WritesThePanelsHealthContext</c>'s job:
    /// since O-7 (1.3.3) the medical export passes <c>MedicalHealthContext</c> to
    /// <c>MedicalMarkdownFormatter</c>, which prints it in the report header as
    /// <c>**Health Context:**</c>, and that test reads the line back from the
    /// exported file.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void HealthContextBox_CommitsTypedTextToTheViewModel()
    {
        (MainViewModel vm, StackPanel panel) = ShowMedicalPanel();

        TopLevel window = Assert.IsType<MainWindow>(TopLevel.GetTopLevel(panel));

        TextBox box = Assert.Single(panel.GetLogicalDescendants().OfType<TextBox>());

        box.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("Chronic migraine");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Chronic migraine", box.Text);
        Assert.Equal("Chronic migraine", vm.MedicalHealthContext);
    }

    /// <summary>
    /// The export button follows the command's <c>CanExecute</c> predicate: off
    /// until a chart has been calculated, on afterwards.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Enablement is expressed nowhere in the markup — not in this head and not
    /// in the WPF original. The only gate is <c>_ =&gt; HasResult</c>, supplied
    /// to the command in <c>MainViewModel</c>'s constructor. This test is what
    /// says the markup was right to stay silent: a hard-coded
    /// <c>IsEnabled="True"</c>, or a dropped <c>Command</c> attribute (a button
    /// with no command is enabled), fails the first assertion.
    /// </para>
    /// <para>
    /// <b>Both directions are asserted, because the first one alone is weak.</b>
    /// A button that is disabled forever also passes a disabled-at-start check.
    /// Driving a real calculation and watching it turn on is what separates
    /// "correctly gated" from "inert".
    /// </para>
    /// <para>
    /// <b>Why the event type is set to Event.</b> <c>CalculateAndSaveAsync</c>
    /// branches on a Birth chart into profection, firdaria and zodiacal
    /// releasing analysis, none of which is under test here and all of which
    /// would be running against a stub result with no planetary positions. The
    /// non-Birth branch reaches the same <c>HasResult = true</c> without that
    /// detour. <c>LastException</c> is asserted null so that a calculation which
    /// failed silently — <c>AsyncRelayCommand.Execute</c> is <c>async void</c>
    /// and swallows into that property — cannot masquerade as a disabled button.
    /// </para>
    /// <para>
    /// <b>IsEffectivelyEnabled, not IsEnabled — this differs from WPF and the
    /// difference is silent.</b> In WPF, <c>ICommand.CanExecute</c> drives
    /// <c>UIElement.IsEnabled</c> directly, so a port written by analogy reaches
    /// for <c>IsEnabled</c> here. In Avalonia <c>IsEnabled</c> is only the value
    /// the markup or code set — it stays <see langword="true"/> throughout — and
    /// command gating arrives through <c>Button.IsEnabledCore</c>, surfacing on
    /// <see cref="global::Avalonia.Input.InputElement.IsEffectivelyEnabled"/>.
    /// Asserting <c>IsEnabled</c> here fails in the disabled direction and, more
    /// dangerously, passes vacuously in the enabled direction: a test that only
    /// checked "enabled after calculating" would be green against a button that
    /// was never gated at all. Measured while writing this test — before a
    /// chart, <c>IsEnabled</c> was <see langword="true"/> and
    /// <c>IsEffectivelyEnabled</c> <see langword="false"/>.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void ExportButton_TracksTheCommandsCanExecutePredicate()
    {
        (MainViewModel vm, StackPanel panel) = ShowMedicalPanel();

        Button export = Assert.Single(PushButtons(panel));

        Assert.False(vm.HasResult);
        Assert.False(
            export.IsEffectivelyEnabled,
            "The export button is enabled before any chart exists. Enablement " +
            "belongs to ExportMedicalMarkdownCommand's CanExecute (_ => HasResult) " +
            "and must not be restated or overridden in markup.");

        vm.ChartEntry.Name = "Slice Six Subject";
        vm.ChartEntry.TimeZone = "UTC";
        vm.ChartEntry.LocationName = "Nowhere";
        vm.ChartEntry.EventType = "Event";

        vm.CalculateAndSaveCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        Assert.Null(vm.CalculateAndSaveCommand.LastException);
        Assert.True(
            vm.HasResult,
            "The calculation did not complete, so the enabled half of this test " +
            "would be asserting against a view model that never changed state.");

        Dispatcher.UIThread.RunJobs();

        Assert.True(
            export.IsEffectivelyEnabled,
            "A chart exists but the export button is still disabled — the button " +
            "is not tracking CanExecute at all.");
    }
}
