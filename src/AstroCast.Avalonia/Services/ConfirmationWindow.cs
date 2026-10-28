using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AstroCast.Avalonia.Services;

/// <summary>
/// The modal Yes/No window shown by <see cref="AvaloniaDialogService"/>. Built in
/// code rather than markup, because it carries no view model and no bindings —
/// its two strings arrive as constructor arguments.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists at all.</b> Avalonia ships no <c>MessageBox</c>. The
/// alternatives were a third-party package (MessageBox.Avalonia) or roughly the
/// eighty lines below. The package was rejected: it is a new dependency on the
/// application's critical path, it brings its own theming that would not match
/// the rest of the head, and the requirement here is a single Yes/No prompt with
/// no icons, no checkbox, and no third button. See
/// <see cref="AstroCast.Core.Interfaces.IDialogService"/> for the contract this
/// serves.
/// </para>
/// <para>
/// <b>Dismissal must yield "No".</b> The interface forbids a third state and
/// requires that dismissing the dialog without choosing returns
/// <see langword="false"/>. That falls out of the design rather than needing a
/// handler: <see cref="Window.ShowDialog{TResult}"/> yields
/// <c>default(TResult)</c> when the window closes without a result being set, and
/// <c>default(bool)</c> is <see langword="false"/>. Closing via the title-bar
/// button, the Escape key, or the No button therefore all agree.
/// </para>
/// <para>
/// <b>Keyboard: Enter activates No, not Yes.</b> This is the opposite of what
/// the markup reads like, so it is worth stating plainly. No is focused when the
/// window opens, and a focused <see cref="Button"/> handles Enter itself and
/// marks the event handled while it bubbles. The event therefore never reaches
/// the top level, where <see cref="Button.IsDefault"/> is serviced. Yes carries
/// <c>IsDefault</c>, but with No focused that flag is <b>inert</b>: there is no
/// keyboard path to Yes in this window that does not first move focus to it.
/// </para>
/// <para>
/// An earlier revision of these remarks claimed the reverse — that Yes is the
/// default and takes Enter. The stated mechanism was false while the resulting
/// safety property was <i>stronger</i> than documented, which is the dangerous
/// combination: a reader who trusted the comment and found Enter declining the
/// prompt would reasonably "fix" the focus call and, in doing so, turn Enter
/// into a silent confirmation of a destructive overwrite. Verified by driving
/// the real dialog on both platforms: Enter declines on macOS and on Windows
/// alike, so this is Avalonia's input routing rather than a window-manager
/// difference.
/// </para>
/// <para>
/// <c>IsDefault</c> is kept rather than deleted. It costs nothing, it states the
/// intent the WPF <c>MessageBox</c> expressed with
/// <c>MessageBoxButton.YesNo</c>, and it becomes live the moment this window
/// grows a third focusable control that is not a button. Its inertness today is
/// a consequence of the focus call below, not of the flag being wrong.
/// </para>
/// <para>
/// <b>Escape and dismissal.</b> No is <see cref="Button.IsCancel"/>, so Escape
/// declines, and closing from the title bar yields the same <c>false</c> for the
/// reason given above. Every route out of this window that is not an explicit
/// click on Yes therefore declines, which for every call site — the import, the
/// overwrite of a chart that exists, the delete, and Export Both's Markdown
/// replacing a file — is the safe reading.
/// </para>
/// </remarks>
internal sealed class ConfirmationWindow : Window
{
    /// <summary>
    /// Initialises a new confirmation window.
    /// </summary>
    /// <param name="message">
    /// The message body. Wrapped, and allowed to grow the window vertically.
    /// </param>
    /// <param name="title">The window title.</param>
    public ConfirmationWindow(string message, string title)
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        ShowInTaskbar = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        TextBlock messageBlock = new()
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new global::Avalonia.Thickness(0, 0, 0, 16),
        };

        Button yesButton = new()
        {
            Content = "Yes",
            IsDefault = true,
            MinWidth = 88,
            Margin = new global::Avalonia.Thickness(0, 0, 8, 0),
        };
        AutomationProperties.SetName(yesButton, "Yes");
        yesButton.Click += (_, _) => Close(true);

        Button noButton = new()
        {
            Content = "No",
            IsCancel = true,
            MinWidth = 88,
        };
        AutomationProperties.SetName(noButton, "No");
        noButton.Click += (_, _) => Close(false);

        StackPanel buttonRow = new()
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        buttonRow.Children.Add(yesButton);
        buttonRow.Children.Add(noButton);

        StackPanel root = new()
        {
            Margin = new global::Avalonia.Thickness(16),
        };
        root.Children.Add(messageBlock);
        root.Children.Add(buttonRow);

        Content = root;

        // Focus No rather than Yes: every call site asks before something that
        // cannot be taken back (the import, the overwrite of a chart that exists,
        // the delete, Export Both's Markdown replacing a file), and both heads
        // default a Yes/No prompt to No (Steve's ruling of 2026-10-14; O-8, H-03;
        // the WPF MessageBox passes MessageBoxResult.No as its default).
        //
        // NavigationMethod.Tab is load-bearing, not decoration. Focus() defaults
        // to NavigationMethod.Unspecified, which moves focus without setting the
        // :focus-visible pseudo-class, so the Fluent theme paints no focus
        // adorner. The result was a destructive prompt on which the keyboard was
        // already aimed at a button the user could not see -- the button that
        // Space activates, and the one that consumes Enter. Requesting the Tab
        // navigation method is what makes the theme draw the ring.
        Opened += (_, _) => noButton.Focus(NavigationMethod.Tab);
    }
}
