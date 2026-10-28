using System.Globalization;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Pins the Chart panel's form geometry — the parts of it that were tuned to a
/// font or to a panel edge, and that would regress silently.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why these are worth a test at all.</b> Each assertion below corresponds to
/// something that was wrong in the shipped panel and was found by looking at
/// rendered pixels rather than at markup: a label column sized for a font that is
/// no longer bundled, two buttons whose right border was clipped by the panel's
/// content edge, and a drop-down field with no affordance saying it had a
/// drop-down. None of the three fails a build, and none is visible in a diff.
/// </para>
/// <para>
/// <b>What the measurement here is, and is not.</b> The headless host renders
/// through Skia with real text metrics, so <c>DesiredSize</c> is a real measured
/// width rather than a stub. It is still not authoritative for production geometry,
/// and <b>the reason differs by platform</b> — which is why a single sentence about
/// "the headless font" cannot be right for both.
/// </para>
/// <para>
/// <b>On Windows, headless renders in Inter</b>, a font the application does not use.
/// Measured in slice 2b, not inferred: the formLabel's resolved family reads
/// <c>compositefont:fonts:Inter#Inter, $Default#Inter, $Default</c> and the TextBlock
/// inherits <c>Inter</c>. So on this platform headless measures neither Segoe UI nor
/// anything the user ever sees.
/// </para>
/// <para>
/// <b>On macOS, headless and the application resolve the same family</b> — Helvetica,
/// Avalonia's default on the macOS Skia backend. There the problem is not the font at
/// all; it is that this suite does not run there. macOS is the platform that governs
/// the label column, and a Windows-hosted run says nothing about it.
/// </para>
/// <para>
/// <b>Do not conclude that removing Inter would fix this.</b> Nothing in this solution
/// calls <c>WithInterFont()</c> — yet Windows headless still resolves Inter, because
/// <c>Avalonia.Fonts.Inter</c> is a hard package dependency of
/// <c>Avalonia.Headless</c> and the headless platform brings it in on its own. "No
/// <c>WithInterFont()</c> call" therefore does not imply "Inter is not resolved", and
/// no change to this project can make the headless host measure the production font.
/// The limitation is structural.
/// </para>
/// <para>
/// <b>An earlier version of this remark called this a "conservative" guard, on the
/// reasoning that Inter is the widest family in play so anything fitting here fits in
/// the application. That was disproven by a shipped regression.</b> Headless measured
/// the widest label at 77 px against an 80 px column and passed, while that label
/// needed 81 px in the running macOS head and clipped. A headless figure is not an
/// upper bound on the production platforms, so "passes here" implies nothing about
/// "fits there" in either direction.
/// </para>
/// <para>
/// Only <see cref="FormLabels_FitInsideTheLabelColumn"/> depends on that
/// distinction. The other tests in this file pin structural facts — a shared
/// column width, symmetric margins, a chevron's presence, the order of the sections,
/// the labels and the Tab stops — which the headless host reports correctly because
/// they do not depend on glyph metrics at all.
/// </para>
/// </remarks>
public sealed class ChartPanelLayoutTests
{
    /// <summary>
    /// The label column width shared by every translated panel's form grids —
    /// the six here, Electional's one, and the Transits panel's two.
    /// </summary>
    /// <remarks>
    /// Set by the widest label in the translated set under the <b>widest
    /// platform</b>, which is macOS/Helvetica — Avalonia's default family there,
    /// not the system UI font. The governor is "Progressions" (Transits): ~90 px
    /// on Windows at the theme-pinned size of 14 (1 bearing + 80.8 ink + 8
    /// margin), and 96.5–97.5 px on macOS, measured there directly with the
    /// SkiaSharp harness. 100 clears both; this panel's own widest label,
    /// "UTC Offset" (77 Windows / 81 macOS), no longer governs. See the comment
    /// block above the first form grid in <c>MainWindow.axaml</c> for the full
    /// derivation and for why this number must be re-measured on macOS rather
    /// than on Windows. The cross-panel guard holding all eight grids to one
    /// width is <c>LabelColumnAlignmentTests</c>.
    /// </remarks>
    private const double LabelColumnWidth = 100d;

    /// <summary>
    /// Builds the main window with a real view model and runs a full layout pass
    /// at the production window size.
    /// </summary>
    /// <returns>The laid-out window.</returns>
    /// <remarks>
    /// The explicit <see cref="Layoutable.Measure"/>/<see cref="Layoutable.Arrange"/>
    /// pair matters: <see cref="Window.Show"/> alone leaves <c>Bounds</c> zero for
    /// controls inside the scrolling panel host, and every assertion would then
    /// pass against nothing.
    /// </remarks>
    private static MainWindow ShowLaidOutWindow(string eventType = "Birth")
    {
        MainViewModel vm = MainViewModelFactory.Create();
        vm.ChartEntry.EventType = eventType;
        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1200 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new Size(1400, 1200));
        window.Arrange(new Rect(0, 0, 1400, 1200));
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>
    /// The number of <c>formLabel</c> TextBlocks inside <c>ChartPanelRoot</c>.
    /// </summary>
    /// <remarks>
    /// Twenty-one: nineteen always visible, "Points" among them since 1.6.0
    /// (branch AsteroidsPanel), plus <c>Question</c> and <c>Category</c>
    /// in the Horary grid, which is collapsed unless the event type is Horary.
    /// A change to this number is a change to the panel's form and should be a
    /// deliberate edit here rather than silent drift.
    /// </remarks>
    private const int ExpectedChartLabelCount = 21;

    /// <summary>
    /// Every form label fits inside the label column when measured under the
    /// <i>headless</i> font — a gross-regression guard only.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This test cannot detect production clipping, and has already failed to.</b>
    /// Run on Windows it passed at an 80 px column while "UTC Offset" was clipping in
    /// the running macOS head. The 77 px it measured was a <i>Windows headless</i>
    /// figure — Inter ink, not Segoe UI and not Helvetica — and it never described
    /// macOS, where the label needs 81 px. Its passing says nothing about whether any
    /// label fits in the running application on either platform.
    /// </para>
    /// <para>
    /// Note that on macOS headless and the application resolve the same family
    /// (Helvetica), so a macOS headless run would track production for these labels.
    /// That does not rescue this test: the suite runs on Windows, where the font is
    /// Inter, so what it reports is a Windows Inter figure regardless.
    /// </para>
    /// <para>
    /// <b>Do not try to fix that by tightening the threshold.</b> Requiring a margin
    /// here would still be measuring Inter on Windows, only more strictly — it would
    /// trade a test that is honestly weak for one that looks strong and is not. A
    /// Windows headless figure is not an upper bound on the macOS head, so no
    /// threshold against it can stand in for the real measurement.
    /// </para>
    /// <para>
    /// <b>The real check is rendered-pixel measurement of the running application on
    /// both platforms, with macOS governing</b>, since Helvetica is the wider of the
    /// two for these labels. That measurement is recorded in the comment block above
    /// the first form grid in <c>MainWindow.axaml</c> and must be redone there
    /// whenever the column, the font, or the label text changes.
    /// </para>
    /// <para>
    /// What this test does still earn its place doing: catching a gross regression —
    /// a label added later that is far wider than the column, or a column narrowed by
    /// a large amount — where the error exceeds the gap between the headless font and
    /// the real ones. That is a genuinely narrower claim than the one this test was
    /// originally written to make.
    /// </para>
    /// <para>
    /// <b>THE WIDTH ASSERTION USED TO BE A TAUTOLOGY. It compared
    /// <c>DesiredSize.Width</c> against the column width — and
    /// <c>DesiredSize</c> is the result of measuring inside that column, so it is
    /// clamped to it and can never exceed it.</b> Measured, not reasoned about:
    /// renaming a label to "UTC Offset Long", which needs <b>119.21px</b> of text
    /// plus margin, left <c>DesiredSize.Width</c> reporting exactly <b>84.00</b> —
    /// the column width — and the old assertion green. The test passed for every
    /// label narrow enough not to matter and silently swallowed the only case it
    /// existed to catch.
    /// </para>
    /// <para>
    /// It now measures the text <b>unconstrained</b>, through
    /// <see cref="FormattedText"/> built from the label's own typeface and size,
    /// and adds the horizontal margin the <c>formLabel</c> style reserves. That
    /// number is what the label would need if the column let it have it, so it can
    /// exceed the column and the assertion can fail. Expect it to read roughly
    /// 0.1–0.7px below the old <c>DesiredSize</c> figures for labels that did fit,
    /// which is layout's rounding, not a change of subject.
    /// </para>
    /// <para>
    /// <b>Scoped to <c>ChartPanelRoot</c>, and every label must actually measure.</b>
    /// Window-wide this collected <b>41</b> labels across three panels while claiming
    /// to describe the Chart panel — and <b>19 of them measured exactly 0.00</b>,
    /// because a collapsed panel measures to zero and all nine panels share the
    /// visual tree. Every one of the Electional panel's seventeen labels was in that
    /// set, including <c>Mercury Rx</c>, which has no slack at all in the running
    /// macOS head. The sweep looked like growing coverage while being nothing of the
    /// kind, and each new panel would have added more zeros.
    /// </para>
    /// <para>
    /// The event type is forced to Horary so the Horary grid is realised too;
    /// otherwise <c>Question</c> and <c>Category</c> are two more labels asserted
    /// against zero inside this very panel. The explicit non-zero assertion is what
    /// makes any future collapse fail loudly instead of passing quietly.
    /// </para>
    /// <para>
    /// <b>It does not catch a narrowed column</b>, and an earlier version of this
    /// remark wrongly implied it did: <see cref="LabelColumnWidth"/> is a constant
    /// here, not read from the markup. Narrowing all six grids to 70 leaves this
    /// test green and fails
    /// <see cref="ChartPanelFormGrids_ShareOneLabelColumnWidth"/> instead, which is
    /// the test that reads the markup. The two are complementary and neither
    /// substitutes for the other.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void FormLabels_FitInsideTheLabelColumn()
    {
        MainWindow window = ShowLaidOutWindow(eventType: "Horary");

        StackPanel chartPanel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("ChartPanelRoot"));

        List<TextBlock> labels = chartPanel.GetLogicalDescendants()
            .OfType<TextBlock>()
            .Where(t => t.Classes.Contains("formLabel"))
            .ToList();

        Assert.Equal(ExpectedChartLabelCount, labels.Count);

        foreach (TextBlock label in labels)
        {
            // A collapsed or unrealised label measures zero, which would satisfy any
            // width assertion without the label ever having been looked at.
            Assert.True(
                label.DesiredSize.Width > 0,
                $"Label '{label.Text}' measured 0px, so nothing about it was actually " +
                "asserted. Its panel or grid is collapsed at layout time — make it " +
                "visible before measuring, or this test is passing against nothing.");

            double needed = UnconstrainedWidth(label);

            Assert.True(
                needed <= LabelColumnWidth,
                $"Label '{label.Text}' needs {needed:F2}px but the column is " +
                $"{LabelColumnWidth}px. Widen the column in all six Chart panel " +
                "grids together, or shorten the label.");
        }
    }

    /// <summary>
    /// The width a label would take if nothing constrained it: its text measured
    /// through its own typeface, plus the horizontal margin its style reserves.
    /// </summary>
    /// <param name="label">The label to measure.</param>
    /// <returns>The unconstrained width in device-independent pixels.</returns>
    /// <remarks>
    /// <see cref="Layoutable.DesiredSize"/> cannot be used for this. It is the
    /// result of measuring the label inside its grid column, so it is clamped to
    /// that column and comparing the two is circular — see the remarks on
    /// <see cref="FormLabels_FitInsideTheLabelColumn"/> for the measurement that
    /// established it.
    /// </remarks>
    private static double UnconstrainedWidth(TextBlock label)
    {
        FormattedText text = new(
            label.Text ?? string.Empty,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            new Typeface(label.FontFamily, label.FontStyle, label.FontWeight),
            label.FontSize,
            Brushes.Black);

        return text.Width + label.Margin.Left + label.Margin.Right;
    }

    /// <summary>
    /// All six Chart panel form grids share one label column width, so the fields
    /// line up down the whole panel.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The six grids are separate <see cref="Grid"/> instances that read as a
    /// single form. Changing one and not the others produces a step in the field
    /// column that is obvious on screen and invisible in a diff.
    /// </para>
    /// <para>
    /// <b>The search is scoped to the Chart panel, and must stay scoped.</b> This
    /// test originally searched the whole window for any grid containing a
    /// <c>formLabel</c>, which identified exactly the Chart panel's grids only while the Chart
    /// panel was the sole user of that class. Slice 3's saved-charts filter grid
    /// uses <c>formLabel</c> as well — faithfully, since the WPF original styles
    /// its Type/Sort/From/To labels with the <c>FormLabel</c> resource — and that
    /// grid has an <c>Auto</c> first column by design, because it is a four-column
    /// filter row rather than part of this form. Window-wide, the count became six
    /// and the width assertion failed on a grid this test was never about.
    /// Scoping to <c>ChartPanelRoot</c> restores the claim the summary makes
    /// instead of relying on a coincidence about who else uses the class.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void ChartPanelFormGrids_ShareOneLabelColumnWidth()
    {
        MainWindow window = ShowLaidOutWindow();

        StackPanel chartPanel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("ChartPanelRoot"));

        // Select the form grids by what they contain, not by their column width.
        // Filtering on "first column is absolute" also catches grids inside control
        // templates (the date picker has one), and filtering on the width itself
        // would make the test tautological.
        List<double> firstColumnWidths = chartPanel.GetLogicalDescendants()
            .OfType<Grid>()
            .Where(g => g.Children.OfType<TextBlock>().Any(t => t.Classes.Contains("formLabel")))
            .Select(g => g.ColumnDefinitions[0].Width.Value)
            .ToList();

        Assert.Equal(6, firstColumnWidths.Count);
        Assert.All(firstColumnWidths, w => Assert.Equal(LabelColumnWidth, w));
    }

    /// <summary>
    /// The two buttons that sit against the panel's right content edge carry a
    /// right margin, so their border is not clipped away.
    /// </summary>
    /// <remarks>
    /// Both shipped with a zero right margin. Sitting hard against the content
    /// edge cost them their right border, so the rounded corner rendered flat —
    /// found on macOS, and present on Windows once looked for. The assertion is
    /// deliberately "right matches left" rather than "right is non-zero", because
    /// a symmetric margin is what makes the button look deliberately inset rather
    /// than nudged.
    /// </remarks>
    /// <remarks>
    /// Scoped to <c>ChartPanelRoot</c>, and must stay scoped. "Lookup" was unique
    /// window-wide only while the Chart panel was the sole location-lookup form;
    /// slice 4's Electional panel has its own Lookup button, faithfully, and a
    /// window-wide <c>Single</c> then throws before asserting anything. The same
    /// lesson as <c>ChartPanelFormGrids_ShareOneLabelColumnWidth</c> above.
    /// </remarks>
    [AvaloniaFact]
    public void EdgeDockedButtons_HaveASymmetricHorizontalMargin()
    {
        MainWindow window = ShowLaidOutWindow();

        StackPanel chartPanel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("ChartPanelRoot"));

        string[] edgeButtons = ["Now", "Lookup"];

        foreach (string caption in edgeButtons)
        {
            Button button = chartPanel.GetLogicalDescendants()
                .OfType<Button>()
                .Single(b => (b.Content as string) == caption);

            Assert.True(
                button.Margin.Right > 0,
                $"The {caption} button has no right margin; it will be clipped by " +
                "the panel's content edge.");
            Assert.Equal(button.Margin.Left, button.Margin.Right);
        }
    }

    /// <summary>
    /// The timezone field carries a drop-down chevron, as its sibling combo boxes
    /// do, and a click on it opens the list, as theirs does.
    /// </summary>
    /// <remarks>
    /// The timezone picker is an <see cref="AutoCompleteBox"/> rather than a
    /// <see cref="ComboBox"/>, for the reasons recorded in the markup, and so it
    /// inherits no chevron from a control template. Without one it reads as a plain
    /// text box and nothing indicates it has a list at all. The chevron is a
    /// <see cref="ToggleButton"/> holding the ComboBox template's glyph, its
    /// <c>IsChecked</c> bound two-way to the box's own <c>IsDropDownOpen</c>, so it
    /// takes the click that opens and closes the list (O-8, H-01); it is no tab
    /// stop, so the keyboard stays in the text field. A ToggleButton, not a Button,
    /// because the panels' button counts leave toggles out. Until 1.3.4 the glyph
    /// was decoration that took no hit tests.
    /// </remarks>
    /// <remarks>
    /// Scoped to <c>ChartPanelRoot</c>, and must stay scoped. The bare
    /// <c>Single()</c> here identified this control only while the Chart panel
    /// held the window's only <see cref="AutoCompleteBox"/>; slice 4's Electional
    /// timezone field is a second one, for the same reasons, and the window-wide
    /// form then throws before asserting anything.
    /// </remarks>
    [AvaloniaFact]
    public void TimezonePicker_CarriesAClickableChevron()
    {
        MainWindow window = ShowLaidOutWindow();

        StackPanel chartPanel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("ChartPanelRoot"));

        AutoCompleteBox timezone = chartPanel.GetLogicalDescendants()
            .OfType<AutoCompleteBox>()
            .Single();

        ToggleButton toggle = Assert.IsType<ToggleButton>(timezone.InnerRightContent);
        Assert.Equal("Show time zones", global::Avalonia.Automation.AutomationProperties.GetName(toggle));
        PathIcon chevron = Assert.IsType<PathIcon>(toggle.Content);
        Assert.NotNull(chevron.Data);
        Assert.True(toggle.IsHitTestVisible);

        // No tab stop: a Tab pressed in the field's text box moves the focus on, past the toggle. The
        // markup sets Focusable="False" (the ToggleButton.timezoneChevron style); IsTabStop keeps its
        // default, true.
        TextBox field = timezone.GetVisualDescendants().OfType<TextBox>().Single();
        field.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Same(field, window.FocusManager?.GetFocusedElement());

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        object? afterTab = window.FocusManager?.GetFocusedElement();
        Assert.NotNull(afterTab);
        Assert.NotSame(field, afterTab);
        Assert.NotSame(toggle, afterTab);
        Assert.False(toggle.Focusable);
    }

    /// <summary>
    /// The panel's section headers follow the order a chart is entered in: who, where, when, then the
    /// house system and the notes (1.4.0, item 1).
    /// </summary>
    /// <remarks>
    /// The place comes before the date because a lookup fills the time zone, and the Offset line reads
    /// the zone and the date together. Houses is a section of its own, its row labelled "System". The
    /// Horary Question header is in the logical tree whatever the event type, as the Returns panel's
    /// collapsed sections are in <c>ReturnsPanelTests.SectionHeaders_MatchTheWpfOrder</c>.
    /// </remarks>
    [AvaloniaFact]
    public void SectionHeaders_FollowTheEntryOrder()
    {
        MainWindow window = ShowLaidOutWindow();

        StackPanel chartPanel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("ChartPanelRoot"));

        string[] expected =
        [
            "Chart Subject",
            "Location",
            "Date, Time & Timezone",
            "Houses",
            "Notes",
            "Horary Question",
            "Actions",
        ];

        string[] actual = chartPanel.GetLogicalDescendants().OfType<TextBlock>()
            .Where(t => t.Classes.Contains("sectionHeader"))
            .Select(t => t.Text ?? string.Empty)
            .ToArray();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// The form labels read from top to bottom in the entry order, as rendered.
    /// </summary>
    /// <remarks>
    /// The labels are sorted by where they land on screen (their top edge in <c>ChartPanelRoot</c>'s
    /// coordinates), not by their order in the markup, so the test pins what the user sees. The event
    /// type is Horary so the Category and Question rows are laid out too; a label that measured nothing
    /// would sort at the top and fail the comparison rather than pass unseen.
    /// </remarks>
    [AvaloniaFact]
    public void FormLabels_ReadTopToBottomInTheEntryOrder()
    {
        MainWindow window = ShowLaidOutWindow(eventType: "Horary");

        StackPanel chartPanel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("ChartPanelRoot"));

        string[] expected =
        [
            "Name", "Type", "Gender",
            "Lookup", "Results", "Latitude", "Longitude", "Location",
            "Date", "Time", "Timezone", "Offset", "Override", "UTC Offset",
            "System", "Points",
            "Source", "Notes", "Keywords",
            "Category", "Question",
        ];

        List<(string Text, double Top)> labels = chartPanel.GetLogicalDescendants()
            .OfType<TextBlock>()
            .Where(t => t.Classes.Contains("formLabel"))
            .Select(t =>
            {
                global::Avalonia.Point? top = t.TranslatePoint(new global::Avalonia.Point(0, 0), chartPanel);
                Assert.True(top.HasValue, $"Label '{t.Text}' is not in the panel's visual tree.");
                Assert.True(t.Bounds.Height > 0, $"Label '{t.Text}' was not laid out.");
                return (t.Text ?? string.Empty, top.Value.Y);
            })
            .ToList();

        string[] actual = labels.OrderBy(l => l.Top).Select(l => l.Text).ToArray();

        Assert.Equal(expected, actual);
    }

    /// <summary>
    /// One Tab from the Gender field lands on the location lookup, the first field of the Location
    /// section, which now follows Chart Subject.
    /// </summary>
    /// <remarks>
    /// Neither head sets a tab index, so Tab follows the document order and the order of the sections
    /// is the keyboard's order too. Before 1.4.0 the next stop was the date's Now button.
    /// </remarks>
    [AvaloniaFact]
    public void Tab_FromGender_ReachesTheLocationLookup()
    {
        MainWindow window = ShowLaidOutWindow();

        StackPanel chartPanel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("ChartPanelRoot"));

        ComboBox gender = chartPanel.GetLogicalDescendants()
            .OfType<ComboBox>()
            .Single(c => global::Avalonia.Automation.AutomationProperties.GetName(c) == "Gender");

        gender.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Same(gender, window.FocusManager?.GetFocusedElement());

        window.KeyPressQwerty(PhysicalKey.Tab, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Control? afterTab = window.FocusManager?.GetFocusedElement() as Control;
        Assert.NotNull(afterTab);
        Assert.Equal(
            "Location lookup",
            global::Avalonia.Automation.AutomationProperties.GetName(afterTab));
        Assert.IsType<TextBox>(afterTab);
    }

    /// <summary>The Points box's tooltip, word for word (1.6.0, branch AsteroidsPanel; topic 8).</summary>
    private const string PointsToolTip =
        "Ceres, Pallas, Juno, Vesta and Black Moon Lilith (the mean apogee) in every report that carries them " +
        "this session; not saved with the chart.";

    /// <summary>
    /// The Points row sits under System in the Houses grid: the label in row 1 of the label column, the CheckBox
    /// named "Include asteroids and Lilith" in row 1 of the field column, with its caption and tooltip, unchecked
    /// at launch (1.6.0, branch AsteroidsPanel; topic 8).
    /// </summary>
    /// <remarks>
    /// The grid is found by the "System" label it holds, so the test pins that the row joined the Houses grid
    /// rather than a grid of its own, which <see cref="ChartPanelFormGrids_ShareOneLabelColumnWidth"/> would
    /// count. The label's top edge lies below System's, so the row reads under it on screen and not only in the
    /// markup.
    /// </remarks>
    [AvaloniaFact]
    public void PointsRow_SitsUnderSystem_InTheHousesGrid()
    {
        MainWindow window = ShowLaidOutWindow();

        StackPanel chartPanel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("ChartPanelRoot"));

        Grid houses = chartPanel.GetLogicalDescendants()
            .OfType<Grid>()
            .Single(g => g.Children.OfType<TextBlock>()
                .Any(t => t.Classes.Contains("formLabel") && t.Text == "System"));
        TextBlock system = houses.Children.OfType<TextBlock>().Single(t => t.Text == "System");

        Assert.Equal(2, houses.RowDefinitions.Count);

        TextBlock points = Assert.Single(houses.Children.OfType<TextBlock>(), t => t.Text == "Points");
        Assert.Contains("formLabel", points.Classes);
        Assert.Equal(1, Grid.GetRow(points));
        Assert.Equal(0, Grid.GetColumn(points));

        CheckBox box = Assert.Single(houses.Children.OfType<CheckBox>());
        Assert.Equal("Include asteroids and Lilith", global::Avalonia.Automation.AutomationProperties.GetName(box));
        Assert.Equal("Include asteroids and Lilith", box.Content as string);
        Assert.Equal(PointsToolTip, ToolTip.GetTip(box) as string);
        Assert.Equal(1, Grid.GetRow(box));
        Assert.Equal(1, Grid.GetColumn(box));
        Assert.False(box.IsChecked == true, "the box is unchecked at launch");

        global::Avalonia.Point? systemTop = system.TranslatePoint(new global::Avalonia.Point(0, 0), chartPanel);
        global::Avalonia.Point? pointsTop = points.TranslatePoint(new global::Avalonia.Point(0, 0), chartPanel);
        Assert.True(systemTop.HasValue && pointsTop.HasValue, "both labels are in the panel's visual tree");
        Assert.True(points.Bounds.Height > 0, "the Points label was laid out");
        Assert.True(pointsTop.Value.Y > systemTop.Value.Y, "Points is laid out below System");
    }

    /// <summary>
    /// Space on the focused Points box ticks it and sets <see cref="ChartEntryViewModel.IncludeMinorPoints"/>,
    /// and Space again clears both: the binding runs both ways, in the shape of the Transits panel's
    /// <c>ToggleCheckBoxes_EachBindToTheirOwnProperty_WhenDrivenByKeyboard</c>.
    /// </summary>
    /// <remarks>
    /// The press and the release are both sent: <see cref="ToggleButton"/> activates Space on key-up. The box is
    /// asserted to have moved before the view model is read, so a harness that delivered nothing names itself
    /// rather than reading as a broken binding.
    /// </remarks>
    [AvaloniaFact]
    public void PointsCheckBox_TogglesTheSwitch_ByKeyboard()
    {
        MainWindow window = ShowLaidOutWindow();
        MainViewModel vm = Assert.IsType<MainViewModel>(window.DataContext);

        StackPanel chartPanel = Assert.IsType<StackPanel>(
            window.FindControl<StackPanel>("ChartPanelRoot"));

        CheckBox box = Assert.Single(
            chartPanel.GetLogicalDescendants().OfType<CheckBox>(),
            c => c.Content as string == "Include asteroids and Lilith");
        Assert.False(vm.ChartEntry.IncludeMinorPoints);
        Assert.False(box.IsChecked == true);

        box.Focus();
        Dispatcher.UIThread.RunJobs();
        Assert.Same(box, window.FocusManager?.GetFocusedElement());

        // Down AND up: ToggleButton activates Space on key-up.
        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(
            box.IsChecked == true,
            "the box itself never toggled: the harness did not deliver the keypress, so nothing about the binding was tested");
        Assert.True(vm.ChartEntry.IncludeMinorPoints, "Space on the box sets the session's switch");

        window.KeyPressQwerty(PhysicalKey.Space, RawInputModifiers.None);
        window.KeyReleaseQwerty(PhysicalKey.Space, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.False(box.IsChecked == true, "Space again clears the box");
        Assert.False(vm.ChartEntry.IncludeMinorPoints, "and the switch with it");
    }
}
