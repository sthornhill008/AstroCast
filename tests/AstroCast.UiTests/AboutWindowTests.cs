using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Threading;

using Xunit;

using AstroCast.Avalonia.Views;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// The Avalonia head's About window, the AGPL's "Appropriate Legal Notices" (section 0, required by section 5(d)): its
/// version, copyright, notice and source lines, the licence and the third-party notices on request, a Close button and
/// Escape; and the main window's About button that opens it.
/// </summary>
/// <remarks>
/// The view model reads the two texts from the test's own output folder, where <c>AstroCast.Core</c> copies
/// <c>LICENSE</c> and <c>THIRD-PARTY-NOTICES.md</c> as it does into the head's, so the window shows the real files.
/// </remarks>
public sealed class AboutWindowTests
{
    /// <summary>The licence's text, from the test's output folder.</summary>
    private static string LicenceText => File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "LICENSE"));

    /// <summary>The third-party notices' text, from the test's output folder.</summary>
    private static string NoticesText =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "THIRD-PARTY-NOTICES.md"));

    /// <summary>Shows the window over a view model of the test's output folder and lays it out.</summary>
    private static (AboutWindow Window, AboutViewModel ViewModel) Open(Action onClosed)
    {
        AboutViewModel vm = new("1.3.5", AppContext.BaseDirectory);
        AboutWindow window = new(vm);
        window.Closed += (_, _) => onClosed();
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(600, 560));
        window.Arrange(new global::Avalonia.Rect(window.DesiredSize));
        Dispatcher.UIThread.RunJobs();
        return (window, vm);
    }

    /// <summary>The window's button with the given caption.</summary>
    private static Button ButtonWith(Window window, string content) =>
        Assert.Single(window.GetLogicalDescendants().OfType<Button>(), b => (b.Content as string) == content);

    /// <summary>The window's one text box, which shows the licence or the notices.</summary>
    private static TextBox TextArea(AboutWindow window) =>
        Assert.Single(window.GetLogicalDescendants().OfType<TextBox>());

    /// <summary>Clicks a button with the pointer at its centre.</summary>
    private static void Click(Window window, Button button)
    {
        global::Avalonia.Point centre = button.TranslatePoint(
                new global::Avalonia.Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)
            ?? throw new InvalidOperationException($"The {button.Content} button is not in the window.");
        window.MouseDown(centre, MouseButton.Left, RawInputModifiers.None);
        window.MouseUp(centre, MouseButton.Left, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();
    }

    /// <summary>
    /// The window is titled About AstroCast and shows the four lines — the version larger and bold, the source line
    /// selectable — and the licence, in a read-only, wrapping text box.
    /// </summary>
    [AvaloniaFact]
    public void AboutWindow_ShowsTheFourLinesAndTheLicence()
    {
        (AboutWindow window, AboutViewModel vm) = Open(() => { });

        Assert.Equal("About AstroCast", window.Title);
        List<TextBlock> blocks = [.. window.GetLogicalDescendants().OfType<TextBlock>()];
        TextBlock version = Assert.Single(blocks, t => t.Text == "AstroCast v1.3.5");
        Assert.Equal(FontWeight.Bold, version.FontWeight);
        Assert.Contains(blocks, t => t.Text == "Copyright © 2026 Thornhill Computer Software");
        TextBlock notice = Assert.Single(blocks, t => t.Text == vm.Notice);
        Assert.Equal(TextWrapping.Wrap, notice.TextWrapping);
        Assert.True(version.FontSize > notice.FontSize, "The version line is larger than the notice.");
        Assert.Contains(
            window.GetLogicalDescendants().OfType<SelectableTextBlock>(),
            t => t.Text == "Source code: https://github.com/sthornhill008/AstroCast");

        TextBox text = TextArea(window);
        Assert.Equal(LicenceText, text.Text);
        Assert.True(text.IsReadOnly);
        Assert.Equal(TextWrapping.Wrap, text.TextWrapping);
    }

    /// <summary>
    /// A click on Third-party notices shows the notices, and a click on Licence the licence again.
    /// </summary>
    [AvaloniaFact]
    public void NoticesAndLicenceButtons_SwitchTheText()
    {
        (AboutWindow window, _) = Open(() => { });
        TextBox text = TextArea(window);

        Click(window, ButtonWith(window, "Third-party notices"));
        Assert.Equal(NoticesText, text.Text);

        Click(window, ButtonWith(window, "Licence"));
        Assert.Equal(LicenceText, text.Text);
    }

    /// <summary>A click on Close closes the window.</summary>
    [AvaloniaFact]
    public void CloseButton_ClosesTheWindow()
    {
        bool closed = false;
        (AboutWindow window, _) = Open(() => closed = true);

        Click(window, ButtonWith(window, "Close"));

        Assert.True(closed);
    }

    /// <summary>Escape closes the window.</summary>
    [AvaloniaFact]
    public void Escape_ClosesTheWindow()
    {
        bool closed = false;
        (AboutWindow window, _) = Open(() => closed = true);

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);
        Dispatcher.UIThread.RunJobs();

        Assert.True(closed);
    }

    /// <summary>
    /// The main window's status bar holds an About button, named About AstroCast for automation, beside the version
    /// and bound to <see cref="MainViewModel.OpenAboutWindowCommand"/>; a click on it opens the About window over the
    /// main window.
    /// </summary>
    [AvaloniaFact]
    public void MainWindow_StatusBarHoldsTheAboutButton()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        MainWindow window = new() { DataContext = vm, Width = 950, Height = 600 };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.Measure(new global::Avalonia.Size(950, 600));
        window.Arrange(new global::Avalonia.Rect(0, 0, 950, 600));
        Dispatcher.UIThread.RunJobs();

        Button about = Assert.Single(
            window.GetLogicalDescendants().OfType<Button>(),
            b => AutomationProperties.GetName(b) == "About AstroCast");
        Assert.Equal("About", about.Content);
        Assert.Equal("Version, licence and third-party notices", ToolTip.GetTip(about));
        Assert.Same(vm.OpenAboutWindowCommand, about.Command);
        Border statusBar = Assert.Single(
            about.GetLogicalAncestors().OfType<Border>(), b => DockPanel.GetDock(b) == Dock.Bottom);
        Assert.Contains(
            statusBar.GetLogicalDescendants().OfType<TextBlock>(), t => t.Text == $"v{vm.AppVersion}");

        Click(window, about);

        AboutWindow opened = Assert.Single(window.OwnedWindows.OfType<AboutWindow>());
        Assert.True(opened.IsVisible);
        opened.Close();
    }
}
