using System.Windows;

using AstroCast.ViewModels;

namespace AstroCast;

/// <summary>
/// The WPF head's About window: the version, the copyright, the licence notice and the source, then the licence or
/// the third-party notices in a scrolling text box, as the Avalonia head's <c>AboutWindow</c> shows them. These are the
/// GNU Affero General Public License's "Appropriate Legal Notices" (section 0, required by section 5(d)).
/// </summary>
/// <remarks>
/// <see cref="MainWindow"/> shows it modal, owned by itself, with <see cref="Window.ShowDialog"/>; its Close button is
/// <c>IsCancel</c>, so a click on it or Escape closes the window.
/// </remarks>
public partial class AboutWindow : Window
{
    /// <summary>
    /// Initialises the About window over its view model.
    /// </summary>
    /// <param name="viewModel">The window's content, its <see cref="FrameworkElement.DataContext"/>.</param>
    /// <exception cref="ArgumentNullException"><paramref name="viewModel"/> is null.</exception>
    public AboutWindow(AboutViewModel viewModel)
    {
        ArgumentNullException.ThrowIfNull(viewModel);

        InitializeComponent();
        DataContext = viewModel;
    }
}
