using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Threading;

using Xunit;

using AstroCast.Avalonia.Services;

namespace AstroCast.UiTests;

/// <summary>
/// The Avalonia head's one-button message window, which shows a start-up failure and an unhandled UI error as
/// the WPF head's <c>MessageBox</c> does (O-8, H-04, H-05): its title and message, and an OK button that closes
/// it, focused on open so Enter closes it too.
/// </summary>
public sealed class MessageWindowTests
{
    private const string Message = "Failed to start AstroCast:\n\nX";
    private const string Title = "AstroCast — Startup Error";

    /// <summary>Shows the window and lays it out.</summary>
    private static MessageWindow Open(Action onClosed)
    {
        MessageWindow window = new(Message, Title);
        window.Closed += (_, _) => onClosed();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(420, 400));
        window.Arrange(new global::Avalonia.Rect(window.DesiredSize));
        Dispatcher.UIThread.RunJobs();
        return window;
    }

    /// <summary>The window's OK button.</summary>
    private static Button Ok(MessageWindow window) =>
        Assert.Single(
            window.GetLogicalDescendants().OfType<Button>(),
            b => global::Avalonia.Automation.AutomationProperties.GetName(b) == "OK");

    /// <summary>
    /// The window carries its title and message, and a pointer click on OK closes it.
    /// </summary>
    [AvaloniaFact]
    public void MessageWindow_ShowsItsMessageAndTitle_AndOkCloses()
    {
        bool closed = false;
        MessageWindow window = Open(() => closed = true);

        Assert.Equal(Title, window.Title);
        Assert.Contains(window.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == Message);

        Button ok = Ok(window);
        global::Avalonia.Point centre = ok.TranslatePoint(
                new global::Avalonia.Point(ok.Bounds.Width / 2, ok.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException("The OK button is not in the window.");
        window.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(centre, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(closed);
    }

    /// <summary>
    /// OK has the keyboard focus when the window opens, so Enter closes it, as Enter dismisses a WPF
    /// <c>MessageBox</c>.
    /// </summary>
    [AvaloniaFact]
    public void MessageWindow_OpensWithOkFocused_SoEnterCloses()
    {
        bool closed = false;
        MessageWindow window = Open(() => closed = true);

        Assert.Same(Ok(window), window.FocusManager?.GetFocusedElement());

        window.KeyPressQwerty(PhysicalKey.Enter, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(closed);
    }
}
