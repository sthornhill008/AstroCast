using System.Linq;

using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using AstroCast.Avalonia.Services;
using AstroCast.ViewModels;

namespace AstroCast.Avalonia.Views;

/// <summary>
/// The application's main window. Hosts the navigation rail and the switchable
/// panel region.
/// </summary>
/// <remarks>
/// <para>
/// The <see cref="MainViewModel"/> is assigned as the <c>DataContext</c> by the
/// composition root after construction, so this type does not create or resolve
/// it. Keeping the code-behind free of view-model construction is what allows the
/// window to be instantiated directly in headless tests.
/// </para>
/// <para>
/// It does, however, supply the one piece of window lifecycle the view model
/// cannot: opening the astrocartography map window. The view model exposes
/// <see cref="MainViewModel.OpenAcgMapWindowAction"/> as a seam precisely so that
/// creating, owning and re-activating a <see cref="Window"/> stays on the View
/// layer, and the WPF head fills that seam from its own code-behind in the same
/// way.
/// </para>
/// </remarks>
public sealed partial class MainWindow : Window
{
    /// <summary>
    /// Initialises a new instance of the <see cref="MainWindow"/> class.
    /// </summary>
    public MainWindow() => InitializeComponent();

    /// <summary>
    /// Loads the compiled XAML for this window.
    /// </summary>
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    /// <summary>
    /// Supplies the map-window and About-window openers to the view model
    /// whenever a new one is bound.
    /// </summary>
    /// <remarks>
    /// Assigning an action is not view-model construction, so this keeps the
    /// headless-test guarantee above: a test may bind a view model and never
    /// invoke the action, in which case no map window is ever created.
    /// </remarks>
    protected override void OnDataContextChanged(EventArgs e)
    {
        if (DataContext is MainViewModel vm)
        {
            vm.OpenAcgMapWindowAction = ShowAcgMapWindow;
            vm.OpenAboutWindowAction = ShowAboutWindow;
        }

        base.OnDataContextChanged(e);
    }

    /// <summary>
    /// Reuse the existing ACG map window if one is already open; otherwise
    /// create a new instance owned by this main window so it closes when the
    /// main window closes.
    /// </summary>
    /// <remarks>
    /// Avalonia has no <c>Application.Current.Windows</c>, so the open window is
    /// found through the desktop lifetime's window list, which is the closest
    /// equivalent. A minimised window is restored before activation, as in the
    /// WPF head.
    /// </remarks>
    private void ShowAcgMapWindow()
    {
        if (DataContext is not MainViewModel vm) return;

        if (global::Avalonia.Application.Current?.ApplicationLifetime
            is IClassicDesktopStyleApplicationLifetime desktop)
        {
            AcgMapWindow? existing = desktop.Windows.OfType<AcgMapWindow>().FirstOrDefault();
            if (existing is not null)
            {
                if (existing.WindowState == WindowState.Minimized)
                {
                    existing.WindowState = WindowState.Normal;
                }

                existing.Activate();
                return;
            }
        }

        AcgMapWindow mapWindow = new(vm, AppPaths.DataDirectory);
        mapWindow.Show(this);
    }

    /// <summary>
    /// Show the About window as a dialog over this main window, its licence and
    /// third-party notices read from the application folder, where AstroCast.Core
    /// copies them beside the executable; or activate the one already open, as
    /// <see cref="ShowAcgMapWindow"/> reuses the map window. The application
    /// menu's About AstroCast stays live while the dialog is up, so a second
    /// click would otherwise open a second window (MacMenu).
    /// </summary>
    private void ShowAboutWindow()
    {
        if (DataContext is not MainViewModel vm) return;

        AboutWindow? existing = OwnedWindows.OfType<AboutWindow>().FirstOrDefault();
        if (existing is not null)
        {
            existing.Activate();
            return;
        }

        AboutWindow aboutWindow = new(new AboutViewModel(vm.AppVersion, AppContext.BaseDirectory));
        _ = aboutWindow.ShowDialog(this);
    }
}
