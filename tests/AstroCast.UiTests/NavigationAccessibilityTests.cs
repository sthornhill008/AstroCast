using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.LogicalTree;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// Verifies that interactive shell controls expose a usable accessible name to
/// assistive technology and to UI automation.
/// </summary>
/// <remarks>
/// <para>
/// <b>The convention this file enforces.</b> Every interactive control in the
/// Avalonia head carries an explicit <c>AutomationProperties.Name</c>. Slice 2
/// and everything after it should follow that as it adds controls — a control
/// without a name is a control no screen reader can announce and no UI test can
/// address except by position.
/// </para>
/// <para>
/// <b>Why the assertions go through an automation peer.</b> Reading the name
/// back out of the markup would only prove the markup says what it says. The
/// peer is what a screen reader and a UI automation client actually consume, so
/// that is what gets asserted.
/// </para>
/// <para>
/// <b>What "missing" actually looked like.</b> Before these names were added the
/// nine rail buttons did not report an empty name — they all reported the same
/// one, <c>"Avalonia.Controls.StackPanel"</c>, because
/// <c>ContentControlAutomationPeer</c> falls back to <c>Content.ToString()</c>
/// when no name is set and every rail button's content is a
/// <see cref="StackPanel"/>. Nine identical, meaningless names are worse than
/// none: they look populated. <see cref="NavigationRail_ButtonNames_AreNotTheFallbackContentType"/>
/// is the guard against silently regressing to that state.
/// </para>
/// </remarks>
public sealed class NavigationAccessibilityTests
{
    /// <summary>
    /// Builds the shell and returns its ten navigation-rail buttons paired with
    /// the panel each one targets.
    /// </summary>
    /// <returns>
    /// One entry per rail button: the button itself and the
    /// <see cref="NavigationPanel"/> carried by its <c>CommandParameter</c>.
    /// </returns>
    private static List<(Button Button, NavigationPanel Panel)> GetNavButtons()
    {
        MainWindow window = new();
        window.Show();

        return window.GetLogicalDescendants()
            .OfType<Button>()
            .Where(b => b.Classes.Contains("nav"))
            .Select(b => (Button: b, Panel: (NavigationPanel)b.CommandParameter!))
            .ToList();
    }

    /// <summary>
    /// Every navigation-rail button reports an accessible name matching the
    /// panel it opens.
    /// </summary>
    /// <remarks>
    /// The name is taken from the panel rather than the visible caption because
    /// two captions are abbreviated and would mislabel their target: Relocation's
    /// caption reads "Relocate" and Electional's reads "Election".
    /// </remarks>
    [AvaloniaFact]
    public void NavigationRail_EveryButton_ExposesItsPanelNameToAutomation()
    {
        List<(Button Button, NavigationPanel Panel)> navButtons = GetNavButtons();

        Assert.Equal(10, navButtons.Count);

        foreach ((Button button, NavigationPanel panel) in navButtons)
        {
            AutomationPeer peer = ControlAutomationPeer.CreatePeerForElement(button);

            Assert.Equal(panel.ToString(), peer.GetName());
        }
    }

    /// <summary>
    /// The ten accessible names are distinct.
    /// </summary>
    /// <remarks>
    /// The rail is ten near-identical copies of one markup block, so the
    /// mistake it invites is a copy-pasted button left carrying the name of the
    /// button it was copied from. A per-button equality check would not catch a
    /// duplicate; this does.
    /// </remarks>
    [AvaloniaFact]
    public void NavigationRail_ButtonNames_AreAllDistinct()
    {
        List<string> names = GetNavButtons()
            .Select(x => ControlAutomationPeer.CreatePeerForElement(x.Button).GetName())
            .ToList();

        Assert.Equal(10, names.Count);
        Assert.Equal(10, names.Distinct().Count());
    }

    /// <summary>
    /// No navigation-rail button falls back to reporting its content's type name.
    /// </summary>
    /// <remarks>
    /// This is the specific regression guard described on the class. If someone
    /// removes an <c>AutomationProperties.Name</c>, Avalonia does not leave the
    /// name empty — it substitutes <c>Content.ToString()</c>, which for these
    /// buttons is the string "Avalonia.Controls.StackPanel". An assertion that
    /// merely required a non-empty name would pass straight through that.
    /// </remarks>
    [AvaloniaFact]
    public void NavigationRail_ButtonNames_AreNotTheFallbackContentType()
    {
        foreach ((Button button, NavigationPanel _) in GetNavButtons())
        {
            string? name = ControlAutomationPeer.CreatePeerForElement(button).GetName();

            Assert.False(string.IsNullOrWhiteSpace(name));
            Assert.DoesNotContain("Avalonia.Controls", name);
        }
    }
}
