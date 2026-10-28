using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AstroCast.Avalonia.Services;

/// <summary>
/// The Avalonia head's one-button message window: a message, a title and an OK button that closes it. Shown by
/// <see cref="App"/> where the WPF head shows a <c>MessageBox</c>: when start-up fails, and when an exception
/// on the UI thread goes unhandled (O-8, H-04, H-05). Built in code, as <see cref="ConfirmationWindow"/> is,
/// because it carries no view model and no bindings.
/// </summary>
/// <remarks>
/// OK is <see cref="Button.IsDefault"/> and <see cref="Button.IsCancel"/>, so Enter and Escape both close the
/// window, and it is focused on open with <see cref="NavigationMethod.Tab"/>, as
/// <see cref="ConfirmationWindow"/> focuses No: that navigation method sets the <c>:focus-visible</c>
/// pseudo-class, so the Fluent theme draws the focus ring the keyboard is aimed at.
/// </remarks>
internal sealed class MessageWindow : Window
{
    /// <summary>
    /// Initialises a new message window.
    /// </summary>
    /// <param name="message">The message body. Wrapped, and allowed to grow the window vertically.</param>
    /// <param name="title">The window title.</param>
    public MessageWindow(string message, string title)
    {
        Title = title;
        Width = 420;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        TextBlock messageBlock = new()
        {
            Text = message,
            TextWrapping = TextWrapping.Wrap,
            Margin = new global::Avalonia.Thickness(0, 0, 0, 16),
        };

        Button okButton = new()
        {
            Content = "OK",
            IsDefault = true,
            IsCancel = true,
            MinWidth = 88,
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        AutomationProperties.SetName(okButton, "OK");
        okButton.Click += (_, _) => Close();

        StackPanel root = new()
        {
            Margin = new global::Avalonia.Thickness(16),
        };
        root.Children.Add(messageBlock);
        root.Children.Add(okButton);

        Content = root;

        // NavigationMethod.Tab, as in ConfirmationWindow: Focus() alone sets no :focus-visible, and the theme
        // would paint no ring on the button Enter and Space now activate.
        Opened += (_, _) => okButton.Focus(NavigationMethod.Tab);
    }
}
