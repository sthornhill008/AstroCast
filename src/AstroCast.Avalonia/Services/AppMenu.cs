using System.Windows.Input;

using Avalonia.Controls;

namespace AstroCast.Avalonia.Services;

/// <summary>
/// Builds the application menu, the menu macOS names after the application (AstroCast, App.axaml's Name): one item of
/// its own, About AstroCast, which opens AstroCast's About window (MacMenu). Avalonia appends its standard items —
/// Services, Hide, Hide Others, Show All and Quit — after a custom application menu's own items, so nothing else is
/// added here.
/// </summary>
/// <remarks>
/// The App sets the menu in its Initialize, where Avalonia's macOS menu exporter will read it (see there). Until 1.3.6
/// the App set no menu, so Avalonia showed its default: About Avalonia, which opens Avalonia's own About window, under
/// a menu named "Avalonia Application".
/// </remarks>
internal static class AppMenu
{
    /// <summary>
    /// Builds the application menu over the given command.
    /// </summary>
    /// <param name="aboutCommand">
    /// The command About AstroCast runs; the App's runs the main view model's OpenAboutWindowCommand.
    /// </param>
    /// <returns>A menu with the one item, About AstroCast.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="aboutCommand"/> is null.</exception>
    internal static NativeMenu Create(ICommand aboutCommand)
    {
        ArgumentNullException.ThrowIfNull(aboutCommand);

        NativeMenu menu = new();
        menu.Items.Add(new NativeMenuItem { Header = "About AstroCast", Command = aboutCommand });
        return menu;
    }
}
