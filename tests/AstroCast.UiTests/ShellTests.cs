using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Headless UI tests for the Avalonia shell: the main window, its navigation
/// rail, and its keyboard shortcuts.
/// </summary>
/// <remarks>
/// <para>
/// <b>The attribute must be <see cref="AvaloniaFactAttribute"/>.</b> A plain
/// <c>[Fact]</c> runs off the headless session's UI thread and fails with
/// <c>InvalidOperationException: The calling thread cannot access this object
/// because a different thread owns it</c> — a message that names neither the
/// attribute nor the cause. (<c>[AvaloniaTest]</c> is the NUnit spelling and
/// does not exist here.)
/// </para>
/// <para>
/// <b>Scope.</b> These tests deliberately do not assign a
/// <see cref="MainViewModel"/> as the <c>DataContext</c>. That is now a choice
/// rather than a constraint: since Phase 4 slice 1.5 the test doubles live in
/// the framework-free <c>AstroCast.TestDoubles</c> library, which this assembly
/// does reference, and <c>MainViewModelConstructionTests</c> builds a real view
/// model here. These tests stay unbound because what they assert — that the
/// markup loads, and that the rail and shortcuts point at the commands and
/// parameters they should — is true of the shell itself and should not start
/// failing because a view model changed. Behavioural navigation assertions
/// (<c>ActivePanel</c> switching, the <c>Is*PanelVisible</c> properties) are
/// already covered in <c>AstroCast.Tests</c> by <c>MainViewModelNavigationTests</c>
/// against the same view model this shell binds to.
/// </para>
/// </remarks>
public sealed class ShellTests
{
    /// <summary>
    /// The main window loads its compiled XAML and can be shown headlessly.
    /// </summary>
    /// <remarks>
    /// The single broadest guard in the suite. It fails on any XAML that does not
    /// load — an unresolvable type, a malformed selector, a missing control
    /// template — none of which the C# compiler catches, and all of which
    /// otherwise surface only when a human launches the application.
    /// </remarks>
    [AvaloniaFact]
    public void MainWindow_LoadsAndShows()
    {
        MainWindow window = new();

        window.Show();

        Assert.NotNull(window.Content);
        Assert.True(window.IsVisible);
    }

    /// <summary>
    /// The navigation rail carries exactly ten buttons, every one of them
    /// classed <c>nav</c> and bound to a distinct <see cref="NavigationPanel"/>.
    /// The tenth, Directions, came with branch PrimaryPanel (1.5.0).
    /// </summary>
    /// <remarks>
    /// Guards the rail against the two mistakes that markup duplication invites:
    /// a missing or extra button, and a copy-pasted button left pointing at the
    /// panel it was copied from. The distinct-count assertion is what catches the
    /// second one.
    /// </remarks>
    [AvaloniaFact]
    public void NavigationRail_HasTenButtons_EachTargetingADistinctPanel()
    {
        MainWindow window = new();
        window.Show();

        List<Button> navButtons = window.GetLogicalDescendants()
            .OfType<Button>()
            .Where(b => b.Classes.Contains("nav"))
            .ToList();

        Assert.Equal(10, navButtons.Count);

        List<NavigationPanel> targets = navButtons
            .Select(b => b.CommandParameter)
            .OfType<NavigationPanel>()
            .ToList();

        Assert.Equal(10, targets.Count);
        Assert.Equal(10, targets.Distinct().Count());
        Assert.Equal(
            Enum.GetValues<NavigationPanel>().OrderBy(p => p),
            targets.OrderBy(p => p));
    }

    /// <summary>
    /// The ten Alt+N shortcuts map to the panels the WPF head maps them to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Pinned explicitly because the mapping is counter-intuitive and a
    /// well-meaning tidy-up would silently break muscle memory. The rail's visual
    /// order is Chart, Synastry, Transits, Returns, <b>Relocation</b>,
    /// <b>Directions</b>, Electional, Medical, Map, Export — but Relocation is
    /// <b>Alt+9</b>, not Alt+5, because it was promoted to a top-level panel after
    /// the first eight were numbered, and Directions, the tenth panel (branch
    /// PrimaryPanel, 1.5.0), is <b>Alt+0</b>, the last free digit. Map and Export
    /// are likewise visually 9th and 10th while bound to Alt+8 and Alt+7.
    /// </para>
    /// <para>
    /// Neither the enum's declaration order nor the rail's layout order encodes
    /// this, so nothing else in the codebase would catch it changing.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void KeyBindings_MapAltNumberToTheExpectedPanel()
    {
        Dictionary<Key, NavigationPanel> expected = new()
        {
            [Key.D1] = NavigationPanel.Chart,
            [Key.D2] = NavigationPanel.Synastry,
            [Key.D3] = NavigationPanel.Transits,
            [Key.D4] = NavigationPanel.Returns,
            [Key.D5] = NavigationPanel.Electional,
            [Key.D6] = NavigationPanel.Medical,
            [Key.D7] = NavigationPanel.Export,
            [Key.D8] = NavigationPanel.Map,
            [Key.D9] = NavigationPanel.Relocation,
            [Key.D0] = NavigationPanel.Directions,
        };

        MainWindow window = new();
        window.Show();

        Assert.Equal(10, window.KeyBindings.Count);

        Dictionary<Key, NavigationPanel> actual = window.KeyBindings
            .Where(kb => kb.Gesture is not null && kb.CommandParameter is NavigationPanel)
            .ToDictionary(
                kb => kb.Gesture!.Key,
                kb => (NavigationPanel)kb.CommandParameter!);

        Assert.Equal(expected, actual);

        Assert.All(
            window.KeyBindings,
            kb => Assert.Equal(KeyModifiers.Alt, kb.Gesture!.KeyModifiers));
    }

    /// <summary>
    /// At the shipped default size, 950 × 600, the rail's ten buttons fit: the lowest button's bottom,
    /// translated to the rail's border, lies inside the border's height less its bottom padding (branch
    /// PrimaryPanel, 1.5.0, scoping decision 2a: the buttons' padding 4,5 and the caption's top margin 1).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rail is a <see cref="StackPanel"/> in a <see cref="Border"/> with no scroll viewer, and a vertical
    /// stack measures its children at their own heights, so a button that does not fit is arranged below the
    /// border's bottom and clipped by the window rather than shrunk. Reading the arranged position catches
    /// that; a count of buttons cannot.
    /// </para>
    /// <para>
    /// The minimum size, 900 × 500, is measured and written to the test output for the report, not asserted:
    /// ten buttons were never expected to fit there.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void NavigationRail_TenButtons_FitTheDefaultWindowHeight()
    {
        (double bottom, double limit) = RailFit(950, 600);
        (double minimumBottom, double minimumLimit) = RailFit(900, 500);

        TestContext.Current.TestOutputHelper?.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"Rail at 950 x 600: lowest button bottom {bottom:F1} px, border inner bottom {limit:F1} px; " +
            $"at 900 x 500: {minimumBottom:F1} px against {minimumLimit:F1} px."));

        Assert.True(
            bottom <= limit,
            string.Create(
                CultureInfo.InvariantCulture,
                $"At 950 x 600 the lowest rail button ends at {bottom:F1} px, below the border's inner bottom at {limit:F1} px " +
                $"(at 900 x 500: {minimumBottom:F1} against {minimumLimit:F1})."));
    }

    /// <summary>
    /// Shows the unbound shell at a size and reads where the rail's lowest button ends and where the rail's
    /// border stops, both in the border's coordinates.
    /// </summary>
    /// <param name="width">The window's width.</param>
    /// <param name="height">The window's height.</param>
    /// <returns>The lowest button's bottom, and the border's height less its bottom padding.</returns>
    private static (double Bottom, double Limit) RailFit(double width, double height)
    {
        MainWindow window = new() { Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(width, height));
        window.Arrange(new global::Avalonia.Rect(0, 0, width, height));
        Dispatcher.UIThread.RunJobs();

        List<Button> navButtons = window.GetLogicalDescendants()
            .OfType<Button>()
            .Where(b => b.Classes.Contains("nav"))
            .ToList();
        Assert.Equal(10, navButtons.Count);

        StackPanel rail = Assert.IsType<StackPanel>(navButtons[0].Parent);
        Border border = Assert.IsType<Border>(rail.Parent);

        double bottom = navButtons
            .Select(b =>
            {
                global::Avalonia.Point? end = b.TranslatePoint(new global::Avalonia.Point(0, b.Bounds.Height), border);
                Assert.True(end.HasValue, "A rail button is not in the border's visual tree.");
                return end.Value.Y;
            })
            .Max();

        window.Close();
        return (bottom, border.Bounds.Height - border.Padding.Bottom);
    }
}
