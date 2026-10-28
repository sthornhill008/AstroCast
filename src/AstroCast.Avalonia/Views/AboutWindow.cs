using System.ComponentModel;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

using AstroCast.ViewModels;

namespace AstroCast.Avalonia.Views;

/// <summary>
/// The Avalonia head's About window: the version, the copyright, the licence notice and the source, then the licence
/// or the third-party notices in a scrolling text box, as the WPF head's <c>AboutWindow</c> shows them. These are the
/// GNU Affero General Public License's "Appropriate Legal Notices" (section 0, required by section 5(d)).
/// </summary>
/// <remarks>
/// <para>
/// Built in code, as <c>MessageWindow</c> and <see cref="AcgMapWindow"/> are, because it takes its view model as a
/// constructor argument. The view model's values are set once; only <see cref="AboutViewModel.DisplayedText"/>
/// changes, and the window follows it through <see cref="INotifyPropertyChanged.PropertyChanged"/>.
/// </para>
/// <para>
/// Close is <see cref="Button.IsCancel"/>, so Escape closes the window, and it is focused on open with
/// <see cref="NavigationMethod.Tab"/>, as <c>MessageWindow</c> focuses OK. <see cref="MainWindow"/> shows the window
/// with <see cref="Window.ShowDialog(Window)"/> over itself.
/// </para>
/// </remarks>
internal sealed class AboutWindow : Window
{
    /// <summary>The view model whose texts the window shows.</summary>
    private readonly AboutViewModel _viewModel;

    /// <summary>The text box that shows the licence or the third-party notices.</summary>
    private readonly TextBox _textBox;

    /// <summary>
    /// Initialises the About window over its view model.
    /// </summary>
    /// <param name="viewModel">The window's content.</param>
    /// <exception cref="ArgumentNullException"><paramref name="viewModel"/> is null.</exception>
    public AboutWindow(AboutViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);
        _viewModel = viewModel;

        Title = "About AstroCast";
        Width = 600;
        Height = 560;
        MinWidth = 480;
        MinHeight = 400;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        TextBlock version = new()
        {
            Text = viewModel.VersionLine,
            FontSize = 18,
            FontWeight = FontWeight.Bold,
        };

        TextBlock copyright = new()
        {
            Text = viewModel.CopyrightLine,
            Margin = new global::Avalonia.Thickness(0, 4, 0, 0),
        };

        TextBlock notice = new()
        {
            Text = viewModel.Notice,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Margin = new global::Avalonia.Thickness(0, 12, 0, 0),
        };

        SelectableTextBlock source = new()
        {
            Text = viewModel.SourceLine,
            Margin = new global::Avalonia.Thickness(0, 12, 0, 0),
        };

        Button licence = new()
        {
            Content = "Licence",
            Command = viewModel.ShowLicenceCommand,
            Padding = new global::Avalonia.Thickness(12, 5),
            MinWidth = 100,
        };
        AutomationProperties.SetName(licence, "Licence");

        Button notices = new()
        {
            Content = "Third-party notices",
            Command = viewModel.ShowNoticesCommand,
            Padding = new global::Avalonia.Thickness(12, 5),
            MinWidth = 100,
            Margin = new global::Avalonia.Thickness(8, 0, 0, 0),
        };
        AutomationProperties.SetName(notices, "Third-party notices");

        StackPanel buttons = new()
        {
            Orientation = Orientation.Horizontal,
            Margin = new global::Avalonia.Thickness(0, 12, 0, 8),
        };
        buttons.Children.Add(licence);
        buttons.Children.Add(notices);

        StackPanel header = new();
        header.Children.Add(version);
        header.Children.Add(copyright);
        header.Children.Add(notice);
        header.Children.Add(source);
        header.Children.Add(buttons);
        DockPanel.SetDock(header, Dock.Top);

        Button close = new()
        {
            Content = "Close",
            IsCancel = true,
            MinWidth = 88,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new global::Avalonia.Thickness(0, 12, 0, 0),
        };
        AutomationProperties.SetName(close, "Close");
        close.Click += (_, _) => Close();
        DockPanel.SetDock(close, Dock.Bottom);

        _textBox = new TextBox
        {
            Text = viewModel.DisplayedText,
            IsReadOnly = true,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = new FontFamily("Menlo, Consolas"),
            FontSize = 12,
        };
        ScrollViewer.SetVerticalScrollBarVisibility(_textBox, ScrollBarVisibility.Auto);
        AutomationProperties.SetName(_textBox, "Licence or third-party notices");

        DockPanel root = new()
        {
            Margin = new global::Avalonia.Thickness(16),
        };
        root.Children.Add(header);
        root.Children.Add(close);
        root.Children.Add(_textBox);

        Content = root;

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Closed += (_, _) => viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        // NavigationMethod.Tab, as in MessageWindow: Focus() alone sets no :focus-visible.
        Opened += (_, _) => close.Focus(NavigationMethod.Tab);
    }

    /// <summary>
    /// Shows the view model's new text, from its start, when <see cref="AboutViewModel.DisplayedText"/> changes.
    /// </summary>
    /// <param name="sender">The view model.</param>
    /// <param name="e">The changed property's name.</param>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(AboutViewModel.DisplayedText))
        {
            _textBox.Text = _viewModel.DisplayedText;
            _textBox.CaretIndex = 0;
        }
    }
}
