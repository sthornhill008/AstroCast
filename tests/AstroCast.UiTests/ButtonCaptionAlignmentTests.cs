using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Pins the action buttons' caption alignment to WPF's.
/// </summary>
/// <remarks>
/// <para>
/// WPF's Button theme style centres content. That is measured, not inferred: on
/// the running WPF head the "Cancel" caption sits 31 px inside a 100 px button,
/// and <c>(100 - 38) / 2 = 31</c>, where a left-aligned caption would sit at 13
/// (Padding 12 + border 1). The registered metadata default is a trap — it reads
/// <c>Left</c>, and the theme style overrides it; only the value source settles
/// it.
/// </para>
/// <para>
/// Avalonia's FluentTheme sets no <c>HorizontalContentAlignment</c> at all. The
/// property sits at its registered default of <c>Stretch</c>, which stretches
/// the caption TextBlock across the button; <c>TextBlock.TextAlignment</c> then
/// defaults to <c>Start</c> and the glyphs render against the left padding edge.
/// So the visible outcome is left-aligned while the mechanism is Stretch — which
/// matters, because the fix is to set Center (collapsing the TextBlock to its
/// natural width), not to change a TextAlignment that is not reachable from the
/// button.
/// </para>
/// <para>
/// This only became visible when slice 6 gave a button horizontal slack.
/// <c>MinWidth="100"</c> creates the same slack for any caption narrower than
/// the 74 px inner box, so it was always there on the Cancels; nothing had
/// looked.
/// </para>
/// <para>
/// The setter lives on the <c>Button.action</c> style rather than on individual
/// buttons because WPF's theme centres <i>every</i> button, so a style-level
/// setter is the faithful translation. It is strictly convergent: for the 25
/// action buttons whose captions exceed the inner box it is a measured no-op
/// (they auto-size and already render at 13 in both heads), and for the eleven
/// with slack it moves Avalonia toward WPF.
/// </para>
/// </remarks>
public sealed class ButtonCaptionAlignmentTests
{
    /// <summary>
    /// Builds the window on a panel that contains action buttons with slack.
    /// </summary>
    /// <returns>The Synastry panel's arranged root.</returns>
    /// <remarks>
    /// Synastry is used because its two Cancel buttons are floored to
    /// <c>MinWidth</c> by a short caption, which is exactly the case where
    /// centre and left differ. A panel whose buttons all auto-size would make
    /// this test vacuous.
    /// </remarks>
    private static StackPanel ShowPanelWithSlackButtons()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        vm.NavigateToPanelCommand.Execute(NavigationPanel.Synastry);

        MainWindow window = new() { DataContext = vm, Width = 1400, Height = 1400 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(1400, 1400));
        window.Arrange(new global::Avalonia.Rect(0, 0, 1400, 1400));
        Dispatcher.UIThread.RunJobs();

        return Assert.IsType<StackPanel>(window.FindControl<StackPanel>("SynastryPanelRoot"));
    }

    /// <summary>
    /// Action buttons centre their content, as WPF's theme does.
    /// </summary>
    /// <remarks>
    /// Asserted on the resolved property rather than on glyph geometry, because
    /// the headless host substitutes a stub font manager whose text widths are
    /// not real — the alignment logic is framework code and font-independent,
    /// but any x-offset measured here would not be.
    /// </remarks>
    [AvaloniaFact]
    public void ActionButtons_CentreTheirContent()
    {
        StackPanel panel = ShowPanelWithSlackButtons();

        List<Button> actions = panel.GetLogicalDescendants()
            .OfType<Button>()
            .Where(b => b is not ToggleButton && b.Classes.Contains("action"))
            .ToList();

        Assert.NotEmpty(actions);
        Assert.All(actions, b => Assert.Equal(
            HorizontalAlignment.Center,
            b.HorizontalContentAlignment));
    }

    /// <summary>
    /// A short-captioned action button's caption sits with equal space either
    /// side — centred in fact, not merely in the property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Why this exists alongside the property assertion.</b> Centre and left
    /// coincide on a button that auto-sizes to its caption, so a property test
    /// alone could pass while nothing visible changed. This drives the real
    /// case: "Cancel" is floored to <c>MinWidth</c> 100 with a caption far
    /// narrower, which is exactly where the two alignments differ. On the
    /// running WPF head that caption sits 31 px in, and
    /// <c>(100 - 38) / 2 = 31</c>.
    /// </para>
    /// <para>
    /// <b>Asserted as equal gaps rather than an absolute offset, which makes it
    /// font-independent.</b> The headless host substitutes a stub font manager,
    /// so the caption's measured width here (41 px) is not the real one (38 px)
    /// — but "the space to the left equals the space to the right" holds for a
    /// centred caption at any width, while a left-aligned one puts the whole
    /// slack on the right. An absolute-offset assertion would have been pinned
    /// to a fictional font.
    /// </para>
    /// <para>
    /// The slack itself is asserted too, so the equality cannot be satisfied
    /// trivially by a caption that fills the button — in which case both gaps
    /// would be zero and the test would prove nothing.
    /// </para>
    /// <para>
    /// Mutation-checked: removing the <c>HorizontalContentAlignment</c> setter
    /// from <c>Button.action</c> drops the left gap to 0 and fails here.
    /// </para>
    /// </remarks>
    [AvaloniaFact]
    public void ShortCaption_SitsCentredWithEqualSpaceEitherSide()
    {
        StackPanel panel = ShowPanelWithSlackButtons();

        Button cancel = panel.GetLogicalDescendants()
            .OfType<Button>()
            .First(b => b.Content as string == "Cancel");

        Assert.Equal(100d, cancel.MinWidth);

        // The presenter is a template part, so it lives in the VISUAL tree —
        // a logical-tree search finds nothing.
        ContentPresenter presenter = Assert.Single(
            cancel.GetVisualDescendants().OfType<ContentPresenter>());

        TextBlock caption = Assert.Single(
            presenter.GetVisualDescendants().OfType<TextBlock>());

        // Both gaps are measured from the presenter's CONTENT box, i.e. inside
        // its padding. caption.Bounds is already relative to the presenter, so
        // the padding has to come off the left edge and off the right edge
        // once each — subtracting both from the width double-counts it.
        double leftGap = caption.Bounds.Left - presenter.Padding.Left;
        double rightGap = presenter.Bounds.Width - presenter.Padding.Right
                          - caption.Bounds.Right;

        Assert.True(
            leftGap > 1d,
            $"The caption starts {leftGap:0.##} px into the presenter — there is no " +
            "slack, so this test cannot tell centred from left-aligned.");

        Assert.True(
            Math.Abs(leftGap - rightGap) <= 1d,
            $"Caption gaps differ: {leftGap:0.##} px left, {rightGap:0.##} px right. " +
            "A centred caption splits the slack evenly; a left-aligned one puts it " +
            "all on the right.");
    }
}
