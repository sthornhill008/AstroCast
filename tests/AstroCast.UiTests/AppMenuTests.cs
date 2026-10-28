using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;

using Xunit;

using AstroCast.Avalonia;
using AstroCast.Avalonia.Services;
using AstroCast.Avalonia.Views;
using AstroCast.Tests.ViewModels;
using AstroCast.ViewModels;

namespace AstroCast.UiTests;

/// <summary>
/// The macOS application menu (MacMenu): it is named after the application, AstroCast, and its one item of its own,
/// About AstroCast, opens AstroCast's About window, of which one is open at a time.
/// </summary>
/// <remarks>
/// A test cannot see the native menu bar: these pin what the head hands Avalonia, the menu and the application's
/// name, and the About window's reuse. Until 1.3.6 the menu read "Avalonia Application", with About Avalonia, because
/// the App set no name and no menu, so Avalonia showed its default.
/// </remarks>
public sealed class AppMenuTests
{
    /// <summary>
    /// The factory's menu holds exactly one item, About AstroCast, whose command is the one given; Avalonia appends
    /// its standard items after it on the Mac.
    /// </summary>
    [AvaloniaFact]
    public void AppMenu_HoldsOnlyAboutAstroCast_WithTheGivenCommand()
    {
        MainViewModel vm = MainViewModelFactory.Create();

        NativeMenu menu = AppMenu.Create(vm.OpenAboutWindowCommand);

        NativeMenuItem item = Assert.IsType<NativeMenuItem>(Assert.Single(menu.Items));
        Assert.Equal("About AstroCast", item.Header);
        Assert.Same(vm.OpenAboutWindowCommand, item.Command);
    }

    /// <summary>
    /// The real App's XAML names the application AstroCast, the name the Mac's application menu takes.
    /// </summary>
    /// <remarks>
    /// The App is built and its XAML loaded with <see cref="App.Initialize"/>, which loads App.axaml and nothing
    /// else; the service graph is built only by <see cref="App.OnFrameworkInitializationCompleted"/>, which is not
    /// called.
    /// </remarks>
    [AvaloniaFact]
    public void App_IsNamedAstroCast()
    {
        App app = new();

        app.Initialize();

        Assert.Equal("AstroCast", app.Name);
    }

    /// <summary>
    /// The real App sets its application menu in <see cref="App.Initialize"/>, the one item About AstroCast, and the
    /// item opens the About window through the main view model the App's DataContext holds.
    /// </summary>
    /// <remarks>
    /// Avalonia's macOS menu exporter reads the application's menu once, after <see cref="App.Initialize"/> and
    /// before <see cref="App.OnFrameworkInitializationCompleted"/>, and does not follow a later change: a menu set
    /// once the view model exists is never shown. So the menu is set in Initialize, and About AstroCast forwards to
    /// the view model's <see cref="MainViewModel.OpenAboutWindowCommand"/> at the time of the click.
    /// </remarks>
    [AvaloniaFact]
    public void App_Initialize_SetsTheMenu_WhoseAboutOpensThroughTheMainViewModel()
    {
        App app = new();
        app.Initialize();
        MainViewModel vm = MainViewModelFactory.Create();
        int opened = 0;
        vm.OpenAboutWindowAction = () => opened++;
        app.DataContext = vm;

        NativeMenu menu = Assert.IsType<NativeMenu>(NativeMenu.GetMenu(app));
        NativeMenuItem item = Assert.IsType<NativeMenuItem>(Assert.Single(menu.Items));
        Assert.Equal("About AstroCast", item.Header);
        Assert.NotNull(item.Command);
        item.Command.Execute(null);

        Assert.Equal(1, opened);
    }

    /// <summary>
    /// Invoking <see cref="MainViewModel.OpenAboutWindowCommand"/> twice leaves one About window open: the
    /// application menu stays live while the About dialog is up, so a second About AstroCast activates the window
    /// already open instead of opening another.
    /// </summary>
    [AvaloniaFact]
    public void OpenAboutWindowCommand_Twice_LeavesOneAboutWindowOpen()
    {
        MainViewModel vm = MainViewModelFactory.Create();
        MainWindow window = new() { DataContext = vm };
        window.Show();

        vm.OpenAboutWindowCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();
        vm.OpenAboutWindowCommand.Execute(null);
        Dispatcher.UIThread.RunJobs();

        AboutWindow opened = Assert.Single(window.OwnedWindows.OfType<AboutWindow>());
        Assert.True(opened.IsVisible);
        opened.Close();
    }
}
