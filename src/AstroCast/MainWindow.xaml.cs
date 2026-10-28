using System.Linq;
using System.Windows;

using AstroCast.ViewModels;

namespace AstroCast;

/// <summary>
/// Interaction logic for MainWindow.xaml. Hosts the chart input form and the
/// switchable navigation panels. The Astrocartography map itself lives in a
/// separate floating <see cref="AcgMapWindow"/> (opened via
/// <see cref="MainViewModel.OpenAcgMapWindowCommand"/>) so it can use the
/// full screen instead of being constrained by the sidebar layout.
/// </summary>
public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // DataContext is assigned by App.xaml.cs AFTER the constructor runs,
        // so we can't reach the ViewModel here. Wire up the Open Map Window
        // callback when the DataContext lands.
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.NewValue is MainViewModel vm)
        {
            vm.OpenAcgMapWindowAction = ShowAcgMapWindow;
            vm.OpenAboutWindowAction = ShowAboutWindow;
        }
    }

    /// <summary>
    /// Reuse the existing ACG map window if one is already open; otherwise
    /// create a new instance owned by this main window so it closes when
    /// the main window closes.
    /// </summary>
    private void ShowAcgMapWindow()
    {
        if (DataContext is not MainViewModel vm) return;

        AcgMapWindow? existing = Application.Current.Windows
            .OfType<AcgMapWindow>()
            .FirstOrDefault();

        if (existing is not null)
        {
            // Bring it forward and unminimize if needed.
            if (existing.WindowState == WindowState.Minimized)
            {
                existing.WindowState = WindowState.Normal;
            }
            existing.Activate();
            return;
        }

        AcgMapWindow mapWindow = new(vm, AppDomain.CurrentDomain.BaseDirectory)
        {
            Owner = this,
        };
        mapWindow.Show();
    }

    /// <summary>
    /// Show the About window, modal and owned by this main window, its licence
    /// and third-party notices read from the application folder, where
    /// AstroCast.Core copies them beside the executable.
    /// </summary>
    private void ShowAboutWindow()
    {
        if (DataContext is not MainViewModel vm) return;

        AboutWindow aboutWindow = new(new AboutViewModel(vm.AppVersion, AppContext.BaseDirectory))
        {
            Owner = this,
        };
        aboutWindow.ShowDialog();
    }
}
