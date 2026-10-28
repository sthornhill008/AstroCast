using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Pins the panel-host inset: one content width for every translated panel, and
/// content that never runs underneath Fluent's overlay scroll bar.
/// </summary>
/// <remarks>
/// <para>
/// The head used to have <b>three</b> content widths at once. A ScrollViewer's
/// <c>Padding</c> becomes the content presenter's <i>Margin</i> in WPF, deflating
/// measure and arrange together, but stays the presenter's own <i>Padding</i> in
/// Avalonia: <see cref="ScrollContentPresenter"/> then measured its child at the
/// full viewport while arranging it at viewport minus padding, and
/// <c>StackPanel.ArrangeOverride</c> grants each child
/// <c>max(finalSize, desired)</c>. Every panel therefore settled at
/// <c>clamp(widest child's desired width, viewport - padding, viewport)</c>, which
/// is panel-dependent by construction and platform-dependent because text
/// measurement differs. Moving the inset onto the host <see cref="StackPanel"/>'s
/// <c>Margin</c> deflates measure as well, and the clamp disappears.
/// </para>
/// <para>
/// <b>Every assertion here is deliberately relative.</b> None names 334, or any
/// absolute width. Headless resolves Inter on Windows while the app renders Segoe
/// UI, and macOS renders Helvetica, so an absolute figure would pin the wrong
/// font on two platforms out of three. What is asserted instead is
/// font-independent: that the panels agree with <i>each other</i>, that the
/// content's right edge clears the <i>measured</i> scroll bar, and that the Chart
/// panel's width does not depend on its data.
/// </para>
/// <para>
/// <b>The two width assertions fail for different reasons, which is the point.</b>
/// Restoring the ScrollViewer's <c>Padding</c> brings the clamp back and
/// <see cref="TranslatedPanels_AllShareOneContentWidth"/> fails while the overlap
/// test still passes. Shrinking the inset from 16 to 8 keeps one width, so that
/// test passes, and
/// <see cref="PanelContent_DoesNotRunUnderTheOverlayScrollBar"/> fails instead.
/// Both were confirmed by mutation, each against the specific edit it guards.
/// </para>
/// </remarks>
public sealed class PanelContentWidthTests
{
    /// <summary>The app's shipped default size; short enough that panels scroll.</summary>
    private const double WindowWidth = 950d;

    /// <summary>See <see cref="WindowWidth"/>.</summary>
    private const double WindowHeight = 600d;

    /// <summary>Every translated panel root, with the nav panel that reveals it.</summary>
    /// <remarks>
    /// A panel must be <b>visible</b> to be measured: Avalonia does not lay out a
    /// collapsed subtree, so a sweep that forgets to switch panels reads zero for
    /// nine of the ten and passes vacuously.
    /// </remarks>
    private static readonly (string Root, NavigationPanel Panel)[] TranslatedPanels =
    [
        ("ChartPanelRoot", NavigationPanel.Chart),
        ("SynastryPanelRoot", NavigationPanel.Synastry),
        ("TransitsPanelRoot", NavigationPanel.Transits),
        ("RelocationPanelRoot", NavigationPanel.Relocation),
        ("DirectionsPanelRoot", NavigationPanel.Directions),
        ("ElectionalPanelRoot", NavigationPanel.Electional),
        ("MedicalPanelRoot", NavigationPanel.Medical),
        ("MapPanelRoot", NavigationPanel.Map),
        ("ExportPanelRoot", NavigationPanel.Export),
        ("ReturnsPanelRoot", NavigationPanel.Returns),
    ];

    /// <summary>
    /// All ten translated panels arrange to the same content width.
    /// </summary>
    [AvaloniaFact]
    public void TranslatedPanels_AllShareOneContentWidth()
    {
        Harness h = Harness.Create();
        List<(string Root, double Width)> widths = [];

        foreach ((string root, NavigationPanel panel) in TranslatedPanels)
        {
            h.Show(panel);
            StackPanel r = Assert.IsType<StackPanel>(h.Window.FindControl<StackPanel>(root));
            Assert.True(r.Bounds.Width > 0,
                $"{root} arranged to zero width, so this sweep measured nothing. " +
                "The panel must be visible before it is measured.");
            widths.Add((root, r.Bounds.Width));
        }

        double first = widths[0].Width;
        IEnumerable<string> offenders = widths
            .Where(w => Math.Abs(w.Width - first) > 0.01)
            .Select(w => $"{w.Root}={w.Width:0.##}");

        Assert.True(
            !offenders.Any(),
            $"Panels disagree on content width. {widths[0].Root}={first:0.##} but " +
            string.Join(", ", offenders) + ". A ScrollViewer Padding in this head " +
            "deflates arrange without deflating measure, so each panel settles at " +
            "clamp(widest desired, viewport - padding, viewport). Keep the inset on " +
            "the host StackPanel's Margin.");
    }

    /// <summary>
    /// No panel's content extends under the vertical overlay scroll bar.
    /// </summary>
    /// <remarks>
    /// Fluent's scroll bar overlays the content where WPF's reserves layout space,
    /// and it is the same width expanded as collapsed, because expanding thickens
    /// the thumb inside a constant-width control. The bar's left edge is read from
    /// the arranged bar rather than assumed, so this test states the requirement
    /// (content clears the bar) without hard-coding the bar's thickness.
    /// </remarks>
    [AvaloniaFact]
    public void PanelContent_DoesNotRunUnderTheOverlayScrollBar()
    {
        Harness h = Harness.Create();
        List<string> overlapped = [];
        int barsSeen = 0;

        foreach ((string root, NavigationPanel panel) in TranslatedPanels)
        {
            h.Show(panel);
            StackPanel r = Assert.IsType<StackPanel>(h.Window.FindControl<StackPanel>(root));

            ScrollBar? bar = h.ScrollViewer.GetVisualDescendants().OfType<ScrollBar>()
                .FirstOrDefault(b => b.Orientation == global::Avalonia.Layout.Orientation.Vertical
                                     && b.IsVisible && b.Bounds.Width > 0);
            if (bar is null) continue;   // panel fits; no bar to clear
            barsSeen++;

            double overlap = r.Bounds.Width - bar.Bounds.X;
            if (overlap > 0.01)
            {
                overlapped.Add($"{root} content {r.Bounds.Width:0.##} runs " +
                    $"{overlap:0.##} px past the bar's left edge at {bar.Bounds.X:0.##}");
            }
        }

        Assert.True(barsSeen > 0,
            "No scrolling panel was found, so this test proved nothing. It needs a " +
            "viewport short enough that at least one panel overflows vertically.");

        Assert.True(
            overlapped.Count == 0,
            "Content runs underneath the overlay scroll bar: " +
            string.Join("; ", overlapped) +
            ". The host StackPanel's right Margin must be at least the bar's width, " +
            "because Avalonia overlays the bar instead of reserving space for it.");
    }

    /// <summary>
    /// The Chart panel's width does not depend on how much text its first field
    /// holds.
    /// </summary>
    /// <remarks>
    /// Under the clamp this width tracked the name's length, jumping to the
    /// viewport once the name grew past roughly twenty characters and dragging
    /// every field column with it: a live layout jump while typing, not a
    /// cosmetic difference between panels. Deflating measure removes the
    /// dependence, so the width is asserted constant rather than asserted equal
    /// to any particular number.
    /// </remarks>
    [AvaloniaFact]
    public void ChartPanelWidth_DoesNotDependOnTheChartName()
    {
        Harness h = Harness.Create();
        h.Show(NavigationPanel.Chart);
        StackPanel chart = Assert.IsType<StackPanel>(
            h.Window.FindControl<StackPanel>("ChartPanelRoot"));

        List<(int Length, double Width)> observed = [];
        foreach (int length in new[] { 0, 10, 20, 30, 60 })
        {
            h.ViewModel.ChartEntry.Name = new string('W', length);
            h.Relayout();
            observed.Add((length, chart.Bounds.Width));
        }

        double first = observed[0].Width;
        IEnumerable<string> moved = observed
            .Where(o => Math.Abs(o.Width - first) > 0.01)
            .Select(o => $"{o.Length} chars -> {o.Width:0.##}");

        Assert.True(
            !moved.Any(),
            $"The Chart panel's width moved with its name: empty -> {first:0.##}, " +
            string.Join(", ", moved) +
            ". That is the measure/arrange clamp returning; the field columns move " +
            "with it while the user types.");
    }

    /// <summary>
    /// Builds the window once and exposes the panel-host ScrollViewer.
    /// </summary>
    private sealed class Harness
    {
        public required MainWindow Window { get; init; }

        public required MainViewModel ViewModel { get; init; }

        public required ScrollViewer ScrollViewer { get; init; }

        public static Harness Create()
        {
            MainViewModel vm = MainViewModelFactory.Create();
            MainWindow window = new()
            {
                DataContext = vm,
                Width = WindowWidth,
                Height = WindowHeight,
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            // The panel host is the nearest ScrollViewer ancestor of a panel root,
            // found by tree position rather than by index, so inserting another
            // ScrollViewer elsewhere in the shell cannot silently retarget this.
            StackPanel chart = window.FindControl<StackPanel>("ChartPanelRoot")!;
            ScrollViewer sv = chart.GetVisualAncestors().OfType<ScrollViewer>().First();

            Harness h = new() { Window = window, ViewModel = vm, ScrollViewer = sv };
            h.Relayout();
            return h;
        }

        public void Show(NavigationPanel panel)
        {
            ViewModel.ActivePanel = panel;
            Relayout();
        }

        public void Relayout()
        {
            Dispatcher.UIThread.RunJobs();
            Window.Measure(new global::Avalonia.Size(WindowWidth, WindowHeight));
            Window.Arrange(new global::Avalonia.Rect(0, 0, WindowWidth, WindowHeight));
            Dispatcher.UIThread.RunJobs();
        }
    }
}
