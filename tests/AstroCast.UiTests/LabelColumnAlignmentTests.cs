using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;
using Avalonia.Threading;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Pins the one label column shared by every translated panel's form grids.
/// </summary>
/// <remarks>
/// <para>
/// The grids are separate <see cref="Grid"/> instances in separate panels that
/// read as one form family. A column that differs <i>within</i> a panel shows up
/// as a step in that panel's field column;
/// <c>ChartPanelFormGrids_ShareOneLabelColumnWidth</c> has pinned that for the
/// Chart panel since slice 2b. A column that differs <i>between</i> panels is
/// subtler — each panel looks right alone, and the mismatch only shows when
/// switching panels — and it is the visible defect slice 2b existed to fix.
/// This test is the cross-panel guard: every form grid in every translated
/// panel, one expected width, one place to change it deliberately.
/// </para>
/// <para>
/// The width itself is a <b>measured</b> value, not a preference: 100 is set by
/// the widest label in the translated set ("Progressions", Transits) on the
/// wider platform (macOS). The derivation lives in the comment block above the
/// Chart panel's first form grid in <c>MainWindow.axaml</c>, and re-deriving it
/// belongs there, on rendered pixels — not here. This test only guarantees that
/// whatever the value is, it is the <i>same</i> value everywhere.
/// </para>
/// <para>
/// Grids are collected through the logical tree, which sees collapsed panels
/// too, so no navigation is needed and a panel left invisible cannot exempt its
/// grids. The per-panel counts are asserted so a new form grid added to a
/// translated panel must be registered here deliberately rather than drifting
/// in unguarded.
/// </para>
/// </remarks>
public sealed class LabelColumnAlignmentTests
{
    /// <summary>
    /// The label column width shared by every translated panel's form grids.
    /// Moved from 84 in slice 5, when the Transits panel brought the governing
    /// label ("Progressions") into this head.
    /// </summary>
    private const double SharedLabelColumnWidth = 100d;

    /// <summary>
    /// The translated panels and how many <c>formLabel</c>-bearing form grids
    /// each contains: Chart six, Electional one, Transits two, Medical none,
    /// Synastry two, Relocation two (Birth Chart and New Location, since 1.4.0;
    /// three before), Directions one (its Settings, since 1.5.0), Map none, Returns ten.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The saved-charts filter grid also uses <c>formLabel</c> — faithfully, per
    /// the WPF original — but sits in the saved-charts pane, not in any of these
    /// panel roots, and keeps its <c>Auto</c> first column by design. Scoping by
    /// panel root is what keeps it out of this assertion, exactly as
    /// <c>ChartPanelFormGrids_ShareOneLabelColumnWidth</c> records.
    /// </para>
    /// <para>
    /// <b>Medical is registered at zero, and that entry does real work.</b> Its
    /// single label, "Health Context (optional)", is stacked above its text box
    /// rather than placed in a form grid — as in the WPF original. That is not a
    /// stylistic accident: the label measures 161.4 px of ink at this head's 14
    /// DIP, so a cell would need roughly 170 and the shared 100 column would clip
    /// it by seventy. A zero here means the obvious-looking "tidy-up" that wraps
    /// that label in a form grid to match the other panels fails this test
    /// instead of silently truncating the label at run time. Every other entry
    /// guards a width; this one guards the decision not to have a column at all.
    /// </para>
    /// </remarks>
    private static readonly (string Root, int ExpectedGrids)[] TranslatedPanels =
    [
        ("ChartPanelRoot", 6),
        ("ElectionalPanelRoot", 1),
        ("TransitsPanelRoot", 2),
        ("MedicalPanelRoot", 0),
        ("SynastryPanelRoot", 3),
        ("RelocationPanelRoot", 2),
        ("DirectionsPanelRoot", 1),
        ("MapPanelRoot", 0),
        ("ReturnsPanelRoot", 10),
    ];

    /// <summary>
    /// Every form grid in every translated panel carries the one shared label
    /// column width.
    /// </summary>
    [AvaloniaFact]
    public void TranslatedPanels_ShareOneLabelColumnWidth()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(1400, 1400));
        window.Arrange(new global::Avalonia.Rect(0, 0, 1400, 1400));
        Dispatcher.UIThread.RunJobs();

        foreach ((string root, int expectedGrids) in TranslatedPanels)
        {
            StackPanel panel = Assert.IsType<StackPanel>(
                window.FindControl<StackPanel>(root));

            List<Grid> formGrids = panel.GetLogicalDescendants()
                .OfType<Grid>()
                .Where(g => g.Children.OfType<TextBlock>()
                    .Any(t => t.Classes.Contains("formLabel")))
                .ToList();

            Assert.True(
                formGrids.Count == expectedGrids,
                $"{root}: expected {expectedGrids} form grids, found {formGrids.Count}. " +
                "A new form grid must be registered in this test deliberately.");

            foreach (Grid grid in formGrids)
            {
                double width = grid.ColumnDefinitions[0].Width.Value;
                Assert.True(
                    width == SharedLabelColumnWidth,
                    $"{root}: a form grid's label column is {width}, not the shared " +
                    $"{SharedLabelColumnWidth}. All translated panels' field columns " +
                    "must start at the same x; change the shared value everywhere " +
                    "at once or not at all.");
            }
        }
    }
}
