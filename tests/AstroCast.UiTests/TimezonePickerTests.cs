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
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Pins the Chart panel's timezone picker: that the view model's substring
/// filter is the only filter in force, and that neither typing nor choosing
/// from the drop-down loses the user's zone.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this needs a UI test rather than a view-model test.</b>
/// <c>TimezoneFilterTests</c> in <c>AstroCast.Tests</c> already covers
/// <c>SelectMatchingTimezones</c> thoroughly, and not one of those tests would
/// have caught what this file caught, because the failure did not live in the
/// view model. It lived in how a control reacts to the collection Reset that
/// <c>ObservableRangeCollection.ReplaceAll</c> raises — see the markup comment
/// on the picker for the full account.
/// </para>
/// <para>
/// <b>The `Contains` case that matters.</b> "York" is a substring of
/// "America/New_York" but not a prefix of it. A control applying its own
/// <c>StartsWith</c> filter — which is <c>AutoCompleteBox</c>'s default, and the
/// reason <c>FilterMode="None"</c> is set — would show nothing. That assertion
/// is what proves the view model's filter, and only the view model's filter,
/// decides what the user sees.
/// </para>
/// <para>
/// <b>These tests are machine-conditional in one respect.</b> The available
/// zones come from <c>TimeZoneInfo.GetSystemTimeZones()</c>, so each test that
/// names a zone first asserts that zone exists rather than assuming it.
/// </para>
/// </remarks>
public sealed class TimezonePickerTests
{
    /// <summary>Builds the shell bound to a fully stubbed view model.</summary>
    private static (MainWindow Window, MainViewModel Vm, AutoCompleteBox Picker) Open()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        MainWindow window = new() { DataContext = vm };
        window.Show();

        AutoCompleteBox picker = window.GetLogicalDescendants()
            .OfType<AutoCompleteBox>()
            // Fully qualified: inside this namespace "Avalonia" would otherwise
            // bind to AstroCast.Avalonia.
            .Single(c => global::Avalonia.Automation.AutomationProperties.GetName(c) == "Timezone");

        return (window, vm, picker);
    }

    /// <summary>Focuses the picker and types <paramref name="text"/> as keystrokes.</summary>
    /// <param name="window">The window hosting the picker.</param>
    /// <param name="picker">The picker to type into.</param>
    /// <param name="text">The text to type.</param>
    /// <remarks>
    /// Real key events rather than <c>picker.Text = …</c>. For this control the two
    /// happen to reach the view model identically — slice 4 measured that — but the
    /// tests below are named for typing, and a test should drive what its name says
    /// so it keeps meaning the same thing if the control's text handling changes.
    /// </remarks>
    private static void Type(MainWindow window, AutoCompleteBox picker, string text)
    {
        picker.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput(text);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// The picker does no filtering of its own, and is bound to the view model's
    /// filtered list.
    /// </summary>
    /// <remarks>
    /// <c>FilterMode</c> is the load-bearing assertion. Left at its default,
    /// the control would apply <c>StartsWith</c> on top of the view model's
    /// <c>Contains</c>, and the intersection would silently drop most matches.
    /// </remarks>
    [AvaloniaFact]
    public void Picker_DoesNoFilteringOfItsOwn_AndIsBoundToTheViewModelsList()
    {
        (MainWindow _, MainViewModel vm, AutoCompleteBox picker) = Open();

        Assert.Equal(AutoCompleteFilterMode.None, picker.FilterMode);
        Assert.False(picker.IsTextCompletionEnabled);
        Assert.Same(vm.FilteredTimezones, picker.ItemsSource);
    }

    /// <summary>
    /// Typing a substring that is not a prefix still matches — the filter is
    /// <c>Contains</c>, not <c>StartsWith</c>.
    /// </summary>
    [AvaloniaFact]
    public void TypingASubstring_FiltersByContains_NotByPrefix()
    {
        (MainWindow window, MainViewModel vm, AutoCompleteBox picker) = Open();

        Assert.Contains("America/New_York", vm.AvailableTimezones);

        Type(window, picker, "York");

        Assert.Contains("America/New_York", vm.FilteredTimezones);
        Assert.All(vm.FilteredTimezones, tz =>
            Assert.Contains("York", tz, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Typing leaves the typed text alone rather than completing it.
    /// </summary>
    [AvaloniaFact]
    public void TypingAPartialZone_DoesNotRewriteWhatWasTyped()
    {
        (MainWindow window, MainViewModel vm, AutoCompleteBox picker) = Open();

        Type(window, picker, "New_");

        Assert.Equal("New_", picker.Text);
        Assert.Equal("New_", vm.ChartEntry.TimeZone);
    }

    /// <summary>
    /// Clearing the text restores the full list.
    /// </summary>
    [AvaloniaFact]
    public void ClearingTheText_RestoresEveryZone()
    {
        (MainWindow window, MainViewModel vm, AutoCompleteBox picker) = Open();

        Type(window, picker, "York");
        Assert.True(vm.FilteredTimezones.Count < vm.AvailableTimezones.Count);

        // Backspaced away rather than assigned empty, so the clear goes through the
        // same text pipeline the typing did.
        for (int i = 0; i < "York".Length; i++)
        {
            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(string.IsNullOrEmpty(picker.Text));
        Assert.Equal(vm.AvailableTimezones.Count, vm.FilteredTimezones.Count);
    }

    /// <summary>
    /// Assigning <see cref="AutoCompleteBox.SelectedItem"/> propagates to the
    /// view model and to the field, and the list re-widens to every zone.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>RENAMED, because the old name was not true and cost a shipped defect.</b>
    /// This was called
    /// <c>ChoosingFromTheDropDown_ReachesTheViewModel_AndSurvivesTheReset</c>, and
    /// the Electional panel's picker was then built on the belief that choosing
    /// from a drop-down was covered. It was not. Slice 4 shipped a picker whose
    /// choice never reached its bound property, and this test — the one named for
    /// exactly that behaviour — stayed green throughout.
    /// </para>
    /// <para>
    /// <b>What it cannot observe.</b> Assigning <c>SelectedItem</c> does not go
    /// through the drop-down's selection adapter, and the adapter is where that
    /// defect lived. Verified by construction: with the defect present, this
    /// driver commits cleanly on the broken panel and the working one alike.
    /// Setting the popup <see cref="ListBox"/>'s own <c>SelectedItem</c>
    /// propagates nothing at all, and a synthetic click on a realised
    /// <c>ListBoxItem</c> light-dismisses the popup instead of selecting — so
    /// neither is a substitute either.
    /// </para>
    /// <para>
    /// <b>Where the real guard lives.</b> <c>TimezoneCommitTests</c> drives the
    /// drop-down the way a user does — focus, type, layout pass, <c>ArrowDown</c>,
    /// <c>Enter</c> — for both this picker and the Electional one. That is the file
    /// to extend if drop-down behaviour needs more coverage; do not add to this one
    /// believing it watches the same thing.
    /// </para>
    /// <para>
    /// <b>Why it is kept.</b> It still pins something real and distinct: that the
    /// programmatic path propagates in both directions, and that reaching an exact
    /// zone clears the filter so <c>FilteredTimezones</c> re-widens to every zone in
    /// one Reset. That re-widening is the event the whole control choice was made
    /// around, and no other test asserts it directly.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void AssigningSelectedItem_PropagatesBothWays_AndRewidensTheList()
    {
        (MainWindow _, MainViewModel vm, AutoCompleteBox picker) = Open();

        Assert.Contains("Europe/London", vm.AvailableTimezones);

        picker.SelectedItem = "Europe/London";

        Assert.Equal("Europe/London", vm.ChartEntry.TimeZone);
        Assert.Equal("Europe/London", picker.Text);
        Assert.Equal(vm.AvailableTimezones.Count, vm.FilteredTimezones.Count);
    }

    /// <summary>
    /// Typing a partial zone and then correcting it to an exact one leaves that
    /// exact zone in place, and re-widens the list.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The exact-match branch is the one worth pinning: reaching a zone that
    /// matches exactly clears the filter text, which replaces the bound collection
    /// in one Reset. A control that reacted badly to that Reset would blank the
    /// field, and the two-way binding would write the blank back — the user types a
    /// valid zone and watches it vanish.
    /// </para>
    /// <para>
    /// The correction is made with backspaces rather than by assigning
    /// <c>Text</c>. Assigning it mid-edit while the box holds focus leaves the
    /// control in a state where subsequent key input does not land, which showed up
    /// here as an empty result — a good illustration of why these tests drive keys
    /// rather than properties.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void CompletingAPartialZone_KeepsTheTypedZone()
    {
        (MainWindow window, MainViewModel vm, AutoCompleteBox picker) = Open();

        Assert.Contains("America/New_York", vm.AvailableTimezones);

        Type(window, picker, "New_");
        Assert.Equal("New_", vm.ChartEntry.TimeZone);
        Assert.True(vm.FilteredTimezones.Count < vm.AvailableTimezones.Count);

        for (int i = 0; i < "New_".Length; i++)
        {
            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }

        window.KeyTextInput("America/New_York");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("America/New_York", picker.Text);
        Assert.Equal("America/New_York", vm.ChartEntry.TimeZone);
        Assert.Equal(vm.AvailableTimezones.Count, vm.FilteredTimezones.Count);
    }

    /// <summary>The window's side for the chevron tests, large enough to lay out either panel.</summary>
    private const double ChevronWindowSide = 1400;

    /// <summary>
    /// Builds the shell on <paramref name="panel"/>, lays it out, and returns the picker named
    /// <paramref name="pickerName"/> and its chevron, the toggle named <paramref name="chevronName"/>.
    /// </summary>
    private static (MainWindow Window, AutoCompleteBox Picker, ToggleButton Chevron) OpenWithChevron(
        MainViewModel vm, NavigationPanel panel, string pickerName, string chevronName)
    {
        vm.NavigateToPanelCommand.Execute(panel);
        MainWindow window = new() { DataContext = vm, Width = ChevronWindowSide, Height = ChevronWindowSide };
        window.Show();
        LayOut(window);

        AutoCompleteBox picker = window.GetLogicalDescendants()
            .OfType<AutoCompleteBox>()
            .Single(c => global::Avalonia.Automation.AutomationProperties.GetName(c) == pickerName);
        ToggleButton chevron = window.GetVisualDescendants()
            .OfType<ToggleButton>()
            .Single(t => global::Avalonia.Automation.AutomationProperties.GetName(t) == chevronName);

        return (window, picker, chevron);
    }

    /// <summary>Runs a full measure/arrange pass and drains the dispatcher.</summary>
    private static void LayOut(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(ChevronWindowSide, ChevronWindowSide));
        window.Arrange(new global::Avalonia.Rect(0, 0, ChevronWindowSide, ChevronWindowSide));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// A real pointer click through the headless window: the left button down and up at the centre of
    /// <paramref name="target"/>, never a property set.
    /// </summary>
    private static void Click(MainWindow window, global::Avalonia.Controls.Control target)
    {
        global::Avalonia.Point centre = target.TranslatePoint(
                new global::Avalonia.Point(target.Bounds.Width / 2, target.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("The control is not in the window.");
        window.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(centre, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>The drop-down's list: the selecting control inside the picker's popup.</summary>
    private static ListBox DropDownList(AutoCompleteBox picker)
    {
        global::Avalonia.Controls.Primitives.Popup popup = picker.GetVisualDescendants()
            .OfType<global::Avalonia.Controls.Primitives.Popup>()
            .Single();
        global::Avalonia.Controls.Control child = popup.Child
            ?? throw new InvalidOperationException("The picker's popup has no content.");
        return child as ListBox
            ?? child.GetLogicalDescendants().OfType<ListBox>().Single();
    }

    /// <summary>
    /// Asserts the chevron opens and closes the list: a click opens it, the list holds the view model's
    /// filtered zones, and a second click closes it.
    /// </summary>
    private static void AssertTheChevronOpensAndClosesTheList(
        MainWindow window, AutoCompleteBox picker, ToggleButton chevron, IReadOnlyList<string> zones)
    {
        Assert.False(picker.IsDropDownOpen);
        Assert.NotEmpty(zones);

        Click(window, chevron);

        Assert.True(picker.IsDropDownOpen);
        Assert.True(chevron.IsChecked);
        Assert.Equal(zones, DropDownList(picker).Items.Cast<string>());

        Click(window, chevron);

        Assert.False(picker.IsDropDownOpen);
        Assert.False(chevron.IsChecked);
    }

    /// <summary>
    /// A click on the Chart panel's chevron opens the zone list, as the four sibling ComboBoxes' chevrons
    /// open theirs; a second click closes it (O-8, H-01). Until 1.3.4 the chevron was decoration that
    /// ignored the pointer, so the list opened only on typing.
    /// </summary>
    [AvaloniaFact]
    public void ChartPicker_ClickingTheChevron_OpensTheList()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        (MainWindow window, AutoCompleteBox picker, ToggleButton chevron) =
            OpenWithChevron(vm, NavigationPanel.Chart, "Timezone", "Show time zones");

        AssertTheChevronOpensAndClosesTheList(window, picker, chevron, vm.FilteredTimezones);
    }

    /// <summary>The Electional panel's chevron opens and closes its list the same way (O-8, H-01).</summary>
    [AvaloniaFact]
    public void ElectionalPicker_ClickingTheChevron_OpensTheList()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        (MainWindow window, AutoCompleteBox picker, ToggleButton chevron) =
            OpenWithChevron(vm, NavigationPanel.Electional, "Electional timezone", "Show electional time zones");

        AssertTheChevronOpensAndClosesTheList(window, picker, chevron, vm.Electional.FilteredTimezones);
    }
}
