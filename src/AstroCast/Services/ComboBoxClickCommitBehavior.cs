using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace AstroCast.Services;

/// <summary>
/// Application-wide behavior that makes clicking a dropdown item in an editable
/// <see cref="ComboBox"/> commit that item's text even when the item is already
/// the selected one.
/// </summary>
/// <remarks>
/// <para>
/// <b>The WPF gap this closes.</b> An editable <see cref="ComboBox"/> only
/// updates its <see cref="ComboBox.Text"/> from a dropdown click when the click
/// <i>changes</i> the selection. Clicking the item that is already selected
/// raises no <c>SelectionChanged</c>, so the text — and everything bound to it —
/// stays as typed. The timezone pickers hit this dead-end: type or select
/// <c>Europe/London</c>, backspace to <c>Europe/Lond</c> (the selection survives
/// the narrowing filter by design), then click the still-highlighted
/// <c>Europe/London</c> — stock WPF does nothing, leaving an invalid timezone
/// and a blank resolved offset with no mouse-only way out.
/// </para>
/// <para>
/// <b>This is a pre-existing stock-WPF limitation, not a port artifact.</b> A
/// side-by-side harness reproduced the identical no-op against the original
/// <c>ICollectionView</c>-based filter (slice-3d investigation, 2026-07-26); it
/// went unnoticed because the gesture is rare. It is fixed here, in the WPF
/// head, because it is view-layer behaviour: the view models never see the
/// click at all.
/// </para>
/// <para>
/// <b>How it works.</b> A class handler on
/// <see cref="ComboBoxItem"/>.<see cref="UIElement.MouseLeftButtonUpEvent"/>
/// (with <c>handledEventsToo</c>, since the built-in handler marks the event
/// handled) detects the click-on-already-selected-item case and writes the
/// item's text through <see cref="ComboBox.TextProperty"/>, letting the
/// existing two-way <c>Text</c> binding carry it into the view model exactly as
/// typing the full name would. The write is deferred with
/// <see cref="System.Windows.Threading.Dispatcher.BeginInvoke(Delegate, object[])"/>
/// so the ComboBox's own click processing (selection no-op + dropdown close)
/// completes against stable item containers first — committing synchronously
/// re-filters the list mid-click, which regenerates the containers and
/// prevents the dropdown from closing.
/// </para>
/// <para>
/// <b>Scope guards.</b> The handler acts only on editable ComboBoxes, only when
/// the clicked item equals the current <see cref="ComboBox.SelectedItem"/>
/// (clicks that change the selection keep WPF's native pipeline), only for
/// <see cref="string"/> items (avoiding <c>DisplayMemberPath</c>/<c>ToString</c>
/// mismatches on typed items), and only when the text actually differs. The
/// only editable ComboBoxes in the application are the two timezone pickers.
/// </para>
/// </remarks>
public static class ComboBoxClickCommitBehavior
{
    /// <summary>
    /// Guards against double registration; class handlers are process-global
    /// and cannot be unregistered.
    /// </summary>
    private static bool s_registered;

    /// <summary>
    /// Registers the class handler. Call once at application startup, on the UI
    /// thread, before any window is shown. Subsequent calls are no-ops.
    /// </summary>
    public static void Register()
    {
        if (s_registered)
        {
            return;
        }

        s_registered = true;
        EventManager.RegisterClassHandler(
            typeof(ComboBoxItem),
            UIElement.MouseLeftButtonUpEvent,
            new MouseButtonEventHandler(OnComboBoxItemMouseLeftButtonUp),
            handledEventsToo: true);
    }

    /// <summary>
    /// Detects a click on the already-selected item of an editable ComboBox and
    /// schedules the text commit.
    /// </summary>
    /// <param name="sender">The clicked <see cref="ComboBoxItem"/>.</param>
    /// <param name="e">Mouse event args (never marked handled here).</param>
    private static void OnComboBoxItemMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ComboBoxItem container)
        {
            return;
        }

        if (ItemsControl.ItemsControlFromItemContainer(container) is not ComboBox combo
            || !combo.IsEditable)
        {
            return;
        }

        object item = combo.ItemContainerGenerator.ItemFromContainer(container);
        if (item is not string itemText
            || !Equals(item, combo.SelectedItem)
            || string.Equals(combo.Text, itemText, StringComparison.Ordinal))
        {
            return;
        }

        combo.Dispatcher.BeginInvoke(() =>
        {
            // Re-check on arrival: the click processing between the event and
            // this callback may have moved the selection or fixed the text.
            if (Equals(combo.SelectedItem, item)
                && !string.Equals(combo.Text, itemText, StringComparison.Ordinal))
            {
                combo.SetCurrentValue(ComboBox.TextProperty, itemText);
            }
        });
    }
}
