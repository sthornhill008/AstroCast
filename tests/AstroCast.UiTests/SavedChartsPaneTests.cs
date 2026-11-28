using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Core.Data;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Covers the saved-charts pane: the two WPF constructs that had to be
/// re-expressed in a different Avalonia idiom, plus the search box's
/// commit behaviour.
/// </summary>
/// <remarks>
/// <para>
/// Two of the three tests here pin a <i>translation</i>. A <c>DataTrigger</c> on
/// a view-model property became a negated class binding plus two selectors, and a
/// <c>RelativeSource AncestorType</c> command lookup became <c>$parent[ListBox]</c>
/// with an explicit cast. Both compile whether or not they behave correctly and
/// both look right in a rendered window, so both were confirmed to discriminate:
/// breaking the negation, and repointing one row command at the other, each fails
/// the corresponding test.
/// </para>
/// <para>
/// The third pins <i>behaviour only</i>, and deliberately claims less. It was
/// written to guard the carried-over <c>UpdateSourceTrigger=PropertyChanged</c>
/// attribute and does not, because that attribute turns out to be redundant on
/// Avalonia — the default already commits per keystroke. Rather than delete a
/// test that still describes something worth holding true, it was rewritten to
/// state what it actually covers. See its own remarks.
/// </para>
/// <para>
/// This pane is chrome rather than one of the nine panels — it is always visible —
/// so it needs no navigation step before layout.
/// </para>
/// </remarks>
public sealed class SavedChartsPaneTests
{
    /// <summary>
    /// A minimal saved chart, carrying only what the item template binds.
    /// </summary>
    /// <param name="name">The chart name.</param>
    /// <returns>A populated <see cref="ChartEntity"/>.</returns>
    private static ChartEntity Chart(string name) => new()
    {
        Id = 1,
        Name = name,
        EventType = "Birth",
        LocalDateTime = "2026-01-01T12:00:00",
        TimeZone = "America/New_York",
        UtcDateTime = "2026-01-01T17:00:00",
        JulianDay = 2461042.2083333335,
        Latitude = 40.7128,
        Longitude = -74.0060,
        LocationName = "New York, NY",
        CreatedAt = "2026-01-01T12:00:00Z",
        UpdatedAt = "2026-01-01T12:00:00Z",
    };

    /// <summary>
    /// Builds and lays out the window, optionally seeding the saved-charts list.
    /// </summary>
    /// <param name="vm">Receives the view model backing the window.</param>
    /// <param name="charts">Charts to place in <c>SavedCharts</c> before layout.</param>
    /// <returns>The laid-out window.</returns>
    /// <remarks>
    /// Charts are seeded <b>before</b> the layout pass so the list's item
    /// containers are realised by the time the assertions run. Adding them
    /// afterwards leaves the template unrealised and any search for its controls
    /// finds nothing — which would pass a badly written test.
    /// </remarks>
    private static MainWindow ShowPane(out MainViewModel vm, params ChartEntity[] charts)
    {
        vm = MainViewModelFactory.Create();

        foreach (ChartEntity chart in charts)
        {
            vm.SavedCharts.Add(chart);
        }

        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(1400, 1200));
        window.Arrange(new global::Avalonia.Rect(0, 0, 1400, 1200));
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>
    /// The sort-direction button's caption follows
    /// <see cref="MainViewModel.SortAscending"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// WPF expresses this as a <c>Button.Style</c> whose <c>Content</c> setter is
    /// overridden by a <c>DataTrigger</c> on <c>SortAscending</c>. Avalonia
    /// selectors cannot test a view-model property, so the button opts into a
    /// <c>descending</c> class through a negated binding and two selectors supply
    /// the two captions.
    /// </para>
    /// <para>
    /// The failure this catches is a stuck caption: if the negation, the class
    /// binding, or the selector order is wrong, the button still renders and still
    /// toggles the sort — it just never changes what it says, so the control lies
    /// about the current direction. Both directions are asserted, because a
    /// selector that always matches would satisfy a one-sided test.
    /// </para>
    /// <para>
    /// <b>Scoped to <c>SavedChartsPane</c>, and must stay scoped.</b> The bare
    /// window-wide <c>Single</c> identified this button only while the pane held
    /// the window's only <c>sortDirection</c>-classed control. Five panels are
    /// still to be translated and a second sort toggle in any of them would make
    /// the window-wide form throw before asserting anything — the same failure
    /// that already hit <see cref="ChartPanelLayoutTests"/> three times. Scoping
    /// pre-empts it rather than waiting for it.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void SortDirectionButton_CaptionFollowsSortAscending()
    {
        MainWindow window = ShowPane(out MainViewModel vm);

        Button sort = Assert.Single(
            window.GetLogicalDescendants().OfType<Button>(),
            b => b.Classes.Contains("sortDirection")
                 && b.GetVisualAncestors().OfType<DockPanel>()
                     .Any(d => d.Name == "SavedChartsPane"));

        vm.SortAscending = true;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("A-Z", sort.Content);

        vm.SortAscending = false;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Z-A", sort.Content);
    }

    /// <summary>
    /// A chart row's Load and Delete buttons reach the commands on
    /// <see cref="MainViewModel"/>, not on the chart item.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The commands live on the parent view model while the item template's data
    /// context is a <see cref="ChartEntity"/>. WPF bridges that with
    /// <c>RelativeSource AncestorType=ListView</c>; Avalonia uses
    /// <c>$parent[ListBox]</c> plus an explicit cast, because compiled bindings
    /// cannot infer the ancestor's data context type.
    /// </para>
    /// <para>
    /// Compiled bindings validate the member names after the cast, but not that
    /// the ancestor lookup finds anything at runtime. If it resolves to nothing —
    /// a changed container type, a retargeted <c>$parent</c> — the binding yields
    /// null, the buttons render normally and do nothing at all. Asserting
    /// reference equality against the view model's own command instances is what
    /// distinguishes "wired to the right command" from merely "not null".
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void ChartRowButtons_ResolveToTheParentViewModelCommands()
    {
        MainWindow window = ShowPane(out MainViewModel vm, Chart("Ada Lovelace"));

        ListBox list = Assert.Single(
            window.GetLogicalDescendants().OfType<ListBox>(),
            l => l.GetVisualAncestors().OfType<DockPanel>()
                  .Any(d => d.Name == "SavedChartsPane"));

        List<Button> rowButtons = list.GetLogicalDescendants().OfType<Button>().ToList();

        Button load = Assert.Single(rowButtons, b => b.Content as string == "Load");
        Button delete = Assert.Single(rowButtons, b => b.Content as string == "Delete");

        Assert.Same(vm.LoadChartCommand, load.Command);
        Assert.Same(vm.DeleteChartCommand, delete.Command);

        // The parameter is the row's own item, which is what makes one shared
        // command able to act on the row that was clicked.
        Assert.Same(vm.SavedCharts[0], load.CommandParameter);
        Assert.Same(vm.SavedCharts[0], delete.CommandParameter);
    }

    /// <summary>
    /// A saved chart with a long name, a long place line and long keywords keeps its Load and Delete buttons
    /// inside the list, at the size the window opens with and at its minimum size, and each of the row's three
    /// lines is trimmed with an ellipsis and carries its full text as its tooltip (branch WindowsFit, 1.7.2;
    /// finding A9).
    /// </summary>
    /// <remarks>
    /// <para>
    /// The fault was the WPF head's: its list scrolled sideways, so one long row pushed every row's buttons
    /// past the pane's edge. This head's <c>ListBox</c> never scrolled sideways, so its buttons were already
    /// in view and a long line was simply cut; the ellipsis and the tooltip are what this branch adds here,
    /// so both heads read alike. A row with buttons never scrolls sideways in either head.
    /// </para>
    /// <para>
    /// The buttons' places are read from the laid-out tree, as rectangles in the list's own coordinates,
    /// never from a picture.
    /// </para>
    /// </remarks>
    /// <param name="width">The window's width.</param>
    /// <param name="height">The window's height.</param>
    [AvaloniaTheory]
    [InlineData(950, 600)]
    [InlineData(900, 500)]
    public void ChartRow_WithLongLines_KeepsItsButtonsInsideTheList_AndTrimsItsText(int width, int height)
    {
        const string longName = "Eleanor Margaret Hartwell-Whitfield of Harrisburg, the long-named example chart";
        const string longKeywords = "example, invented, long, keywords, that, run, well, past, the, width, of, the, saved, charts, pane";
        MainViewModel vm = MainViewModelFactory.Create();
        ChartEntity chart = Chart(longName);
        chart.LocationName = "Harrisburg, Dauphin County, Pennsylvania, United States of America";
        chart.Keywords = longKeywords;
        vm.SavedCharts.Add(chart);
        vm.SavedCharts.Add(Chart("Ada Lovelace"));

        MainWindow window = new() { DataContext = vm, Width = width, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(width, height));
        window.Arrange(new global::Avalonia.Rect(0, 0, width, height));
        Dispatcher.UIThread.RunJobs();

        ListBox list = Assert.Single(
            window.GetLogicalDescendants().OfType<ListBox>(),
            l => l.GetVisualAncestors().OfType<DockPanel>()
                  .Any(d => d.Name == "SavedChartsPane"));

        List<Button> rowButtons = list.GetVisualDescendants().OfType<Button>()
            .Where(b => b.Content as string is "Load" or "Delete").ToList();
        Assert.Equal(4, rowButtons.Count);
        foreach (Button button in rowButtons)
        {
            global::Avalonia.Point? origin = global::Avalonia.VisualExtensions.TranslatePoint(
                button, new global::Avalonia.Point(0, 0), list);
            Assert.NotNull(origin);
            Assert.True(button.Bounds.Width > 0, $"The {button.Content} button has no width.");
            Assert.True(origin.Value.X >= 0, $"The {button.Content} button starts left of the list.");
            Assert.True(
                origin.Value.X + button.Bounds.Width <= list.Bounds.Width,
                $"The {button.Content} button ends at {origin.Value.X + button.Bounds.Width:F1}, past the list's width of {list.Bounds.Width:F1}.");
        }

        ListBoxItem row = list.GetVisualDescendants().OfType<ListBoxItem>().First(i => ReferenceEquals(i.DataContext, chart));
        List<TextBlock> lines = row.GetVisualDescendants().OfType<TextBlock>()
            .Where(t => t.GetVisualAncestors().OfType<Button>().FirstOrDefault() is null).ToList();
        Assert.Equal(3, lines.Count);
        Assert.Equal(longName, lines[0].Text);
        Assert.Equal(longKeywords, lines[2].Text);
        foreach (TextBlock line in lines)
        {
            Assert.Equal(global::Avalonia.Media.TextTrimming.CharacterEllipsis, line.TextTrimming);
            Assert.Equal(line.Text, ToolTip.GetTip(line) as string);
            Assert.True(line.Bounds.Width <= list.Bounds.Width, "A line of the row is wider than the list.");
        }
    }

    /// <summary>
    /// The search box commits each keystroke to
    /// <see cref="MainViewModel.SearchText"/> without waiting for focus to leave.
    /// </summary>
    /// <remarks>
    /// <para>
    /// What this pins is the <i>behaviour</i> — that a search runs against the
    /// term currently on screen — not any particular mechanism for delivering it.
    /// The distinction matters, because the mechanism turned out not to be what
    /// the markup's comment originally claimed.
    /// </para>
    /// <para>
    /// <b>This test does not guard the <c>UpdateSourceTrigger</c> attribute, and
    /// must not be described as though it does.</b> Deleting that attribute from
    /// the binding leaves this test passing, which was verified by doing exactly
    /// that. Avalonia commits <c>TextBox.Text</c> to its source on every keystroke
    /// by default, so the attribute is redundant here; only WPF, whose default is
    /// LostFocus, ever needed it. See the comment above the search box in
    /// <c>MainWindow.axaml</c> for the two measurements.
    /// </para>
    /// <para>
    /// It is still worth keeping. A binding changed to <c>OneWay</c>, a source
    /// property that stops raising change notification, or a future Avalonia
    /// default that moves to LostFocus would each break the guarantee the search
    /// button depends on, and each would fail here. What it cannot do is tell you
    /// the attribute is present — so do not delete the attribute on the strength
    /// of this test still being green.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void SearchBox_CommitsTypedTextToTheViewModelWithoutAFocusChange()
    {
        MainWindow window = ShowPane(out MainViewModel vm);

        TextBox search = Assert.Single(
            window.GetLogicalDescendants().OfType<TextBox>(),
            t => t.GetVisualAncestors().OfType<DockPanel>()
                  .Any(d => d.Name == "SavedChartsPane"));

        search.Focus();
        Dispatcher.UIThread.RunJobs();

        window.KeyTextInput("Ada");
        Dispatcher.UIThread.RunJobs();

        Assert.Equal("Ada", search.Text);
        Assert.Equal("Ada", vm.SearchText);
    }
}
