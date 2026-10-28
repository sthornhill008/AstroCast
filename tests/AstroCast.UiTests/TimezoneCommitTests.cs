using Avalonia;
using Avalonia.Controls;
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
/// Pins the one thing that decides whether a timezone picker is usable: that
/// choosing a suggestion from the drop-down reaches the <b>bound view-model
/// property</b>, not merely the control's displayed text. Covers both pickers —
/// the Electional panel's and the Chart panel's.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this file exists.</b> The Electional panel shipped with a picker whose
/// choice never reached <c>Electional.TimeZone</c>: the field showed the chosen
/// zone while the view model kept the typed fragment, so a search run straight
/// afterwards failed with
/// <c>TimeZoneNotFoundException: The time zone ID 'York' was not found</c>.
/// Six Electional panel tests and five picker tests were green throughout.
/// </para>
/// <para>
/// <b>The drop-down must be driven by keyboard, and that is the whole point of
/// this file.</b> Assigning <c>AutoCompleteBox.SelectedItem</c> directly — which
/// is what
/// <c>TimezonePickerTests.ChoosingFromTheDropDown_ReachesTheViewModel_AndSurvivesTheReset</c>
/// does — commits cleanly on <b>both</b> panels, broken and working alike, so a
/// test written that way passes for the wrong reason and proves nothing. A
/// direct assignment bypasses the drop-down's selection adapter entirely, and
/// the adapter is where the defect lives. Focusing the box, typing, then
/// <c>ArrowDown</c> + <c>Enter</c> goes through the adapter the way a user does,
/// and separates the two panels immediately.
/// </para>
/// <para>
/// <b>Typing must narrow the list first.</b> With the filter already empty the
/// <c>Reset</c> that <see cref="ObservableRangeCollection{T}.ReplaceAll"/>
/// raises swaps the full list for the full list and nothing is disturbed. Typing
/// a fragment narrows the collection to a single item, so committing the choice
/// re-widens it to every zone — a Reset that genuinely changes what the adapter
/// is looking at. That is the state in which the defect appears, and it is the
/// state a real user is always in.
/// </para>
/// <para>
/// <b>Both panels, deliberately.</b> The Chart panel's picker was never broken
/// and is the behaviour the Electional picker is made to match. Asserting it
/// here documents the target and guards it, so a future change that made both
/// equally wrong would fail this file rather than pass it.
/// </para>
/// <para>
/// <b>Machine-conditional.</b> Zones come from
/// <c>TimeZoneInfo.GetSystemTimeZones()</c>, so every test asserts the zone it
/// names exists before relying on it.
/// </para>
/// </remarks>
public sealed class TimezoneCommitTests
{
    /// <summary>A zone whose name contains, but does not start with, the fragment typed for it.</summary>
    private const string NewYork = "America/New_York";

    /// <summary>The fragment typed to reach <see cref="NewYork"/>; a substring, not a prefix.</summary>
    private const string NewYorkFragment = "York";

    /// <summary>A second zone, so the guard does not rest on one string's filtering.</summary>
    private const string Tokyo = "Asia/Tokyo";

    /// <summary>The fragment typed to reach <see cref="Tokyo"/>.</summary>
    private const string TokyoFragment = "Tokyo";

    /// <summary>The window size used for layout; large enough that no panel row is clipped.</summary>
    private const double WindowSide = 1400;

    /// <summary>
    /// Builds the shell on the given panel and returns that panel's timezone
    /// picker.
    /// </summary>
    /// <param name="panel">The panel to navigate to before laying out.</param>
    /// <param name="automationName">
    /// The picker's <c>AutomationProperties.Name</c>. Scoping by name rather
    /// than by a bare <c>Single()</c> over <see cref="AutoCompleteBox"/> is
    /// required: two panels now carry one each, and a window-wide search throws.
    /// </param>
    /// <returns>The view model, the located picker, and the window hosting them.</returns>
    /// <remarks>
    /// The panel is made visible before layout. All nine panels share the visual
    /// tree and a collapsed one measures to zero, so assertions about its
    /// contents would otherwise pass against nothing.
    /// </remarks>
    private static (MainViewModel Vm, AutoCompleteBox Picker, MainWindow Window) Open(
        NavigationPanel panel,
        string automationName)
    {
        MainViewModel vm = MainViewModelFactory.Create();
        vm.NavigateToPanelCommand.Execute(panel);

        MainWindow window = new() { DataContext = vm, Width = WindowSide, Height = WindowSide };
        window.Show();
        Layout(window);

        AutoCompleteBox picker = window.GetLogicalDescendants()
            .OfType<AutoCompleteBox>()
            // Fully qualified: inside this namespace "Avalonia" would otherwise
            // bind to AstroCast.Avalonia.
            .Single(c => global::Avalonia.Automation.AutomationProperties.GetName(c) == automationName);

        return (vm, picker, window);
    }

    /// <summary>Runs a full measure/arrange pass and drains the dispatcher.</summary>
    /// <param name="window">The window to lay out.</param>
    /// <remarks>
    /// Called again after typing, not just at startup. The drop-down's list is
    /// populated when the text changes, and its item containers only exist once
    /// a layout pass has run — without one the adapter has nothing to move onto
    /// and <c>ArrowDown</c> is a no-op on <i>every</i> panel, which would make
    /// this file fail everywhere for a reason that is not the defect.
    /// </remarks>
    private static void Layout(MainWindow window)
    {
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(WindowSide, WindowSide));
        window.Arrange(new Rect(0, 0, WindowSide, WindowSide));
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Types a fragment into the picker and commits the first drop-down
    /// suggestion, the way a user does.
    /// </summary>
    /// <param name="window">The window hosting the picker.</param>
    /// <param name="picker">The picker to drive.</param>
    /// <param name="fragment">The text to type.</param>
    /// <remarks>
    /// <c>ArrowDown</c> then <c>Enter</c> goes through the drop-down's selection
    /// adapter. Assigning <see cref="AutoCompleteBox.SelectedItem"/> instead
    /// bypasses it and cannot see this class of defect — see the remarks on the
    /// type.
    /// </remarks>
    private static void TypeThenCommitFirstSuggestion(
        MainWindow window,
        AutoCompleteBox picker,
        string fragment)
    {
        picker.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput(fragment);
        Layout(window);

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// Electional: typing a fragment and committing the suggestion it narrowed
    /// to must leave the chosen zone in the bound property.
    /// </summary>
    /// <remarks>
    /// The regression guard for the shipped defect.
    /// </remarks>
    [AvaloniaFact]
    public void ElectionalPicker_ChoosingFromTheDropDown_ReachesTheViewModel()
    {
        (MainViewModel vm, AutoCompleteBox picker, MainWindow window) = Open(
            NavigationPanel.Electional, "Electional timezone");

        Assert.Contains(NewYork, vm.AvailableTimezones);

        picker.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput(NewYorkFragment);
        Layout(window);

        // The list is genuinely narrowed: this is the state the defect needs.
        Assert.Contains(NewYork, vm.Electional.FilteredTimezones);
        Assert.True(vm.Electional.FilteredTimezones.Count < vm.AvailableTimezones.Count);
        Assert.Equal(NewYorkFragment, vm.Electional.TimeZone);

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(NewYork, vm.Electional.TimeZone);
        Assert.Equal(NewYork, picker.Text);
    }

    /// <summary>
    /// Electional: the same with a second zone, so the guard does not rest on
    /// one string.
    /// </summary>
    [AvaloniaFact]
    public void ElectionalPicker_ChoosingASecondZone_AlsoReachesTheViewModel()
    {
        (MainViewModel vm, AutoCompleteBox picker, MainWindow window) = Open(
            NavigationPanel.Electional, "Electional timezone");

        Assert.Contains(Tokyo, vm.AvailableTimezones);

        TypeThenCommitFirstSuggestion(window, picker, TokyoFragment);

        Assert.Equal(Tokyo, vm.Electional.TimeZone);
    }

    /// <summary>
    /// Chart: the behaviour the Electional picker is made to match, asserted
    /// under identical conditions.
    /// </summary>
    /// <remarks>
    /// This one passed before the Electional fix and must keep passing after it.
    /// It is the control group: if it ever fails, the fix was applied in the
    /// wrong direction.
    /// </remarks>
    [AvaloniaFact]
    public void ChartPicker_ChoosingFromTheDropDown_ReachesTheViewModel()
    {
        (MainViewModel vm, AutoCompleteBox picker, MainWindow window) = Open(
            NavigationPanel.Chart, "Timezone");

        Assert.Contains(NewYork, vm.AvailableTimezones);

        picker.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput(NewYorkFragment);
        Layout(window);

        Assert.Contains(NewYork, vm.FilteredTimezones);
        Assert.True(vm.FilteredTimezones.Count < vm.AvailableTimezones.Count);
        Assert.Equal(NewYorkFragment, vm.ChartEntry.TimeZone);

        window.KeyPressQwerty(PhysicalKey.ArrowDown, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(NewYork, vm.ChartEntry.TimeZone);
        Assert.Equal(NewYork, picker.Text);
    }

    /// <summary>
    /// Electional: the drop-down must still narrow as the user types.
    /// </summary>
    /// <remarks>
    /// Paired with the commit tests on purpose. The commit defect is fixed by
    /// moving <i>when</i> the filter refresh runs, and a refresh moved too far
    /// would trade a picker that loses the choice for one that never narrows.
    /// Both halves have to hold at once.
    /// </remarks>
    [AvaloniaFact]
    public void ElectionalPicker_StillNarrowsAsTheUserTypes()
    {
        (MainViewModel vm, AutoCompleteBox picker, MainWindow window) = Open(
            NavigationPanel.Electional, "Electional timezone");

        Assert.Contains(NewYork, vm.AvailableTimezones);
        int all = vm.Electional.FilteredTimezones.Count;

        picker.Focus();
        Dispatcher.UIThread.RunJobs();
        window.KeyTextInput(NewYorkFragment);
        Dispatcher.UIThread.RunJobs();

        // "It got smaller" and "everything left matches" are both satisfied by an
        // EMPTY list, so neither can stand alone: swapping the view model's
        // Contains filter for StartsWith empties the list and would leave this
        // green. The membership assertion is what makes the narrowing real.
        Assert.Contains(NewYork, vm.Electional.FilteredTimezones);
        Assert.True(vm.Electional.FilteredTimezones.Count < all);
        Assert.All(vm.Electional.FilteredTimezones, tz =>
            Assert.Contains(NewYorkFragment, tz, StringComparison.OrdinalIgnoreCase));

        // Backspaced away rather than assigned empty, so the clear travels the same
        // path the typing did.
        for (int i = 0; i < NewYorkFragment.Length; i++)
        {
            window.KeyPressQwerty(PhysicalKey.Backspace, RawInputModifiers.None);
            Dispatcher.UIThread.RunJobs();
        }

        Assert.True(string.IsNullOrEmpty(picker.Text));
        Assert.Equal(all, vm.Electional.FilteredTimezones.Count);
    }
}
