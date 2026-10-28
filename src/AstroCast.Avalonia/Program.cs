using Avalonia;

namespace AstroCast.Avalonia;

/// <summary>
/// Process entry point for the Avalonia head.
/// </summary>
/// <remarks>
/// Avalonia applications bootstrap from a plain <c>Main</c> rather than from the
/// application class itself, which is why this type exists and has no WPF
/// counterpart — WPF generates its entry point from <c>App.xaml</c>.
/// </remarks>
internal static class Program
{
    /// <summary>
    /// Application entry point.
    /// </summary>
    /// <param name="args">Command-line arguments, forwarded to Avalonia.</param>
    /// <remarks>
    /// Must not use any Avalonia type before <see cref="BuildAvaloniaApp"/> has
    /// run: initialisation order matters and touching the framework early binds
    /// platform services before they are configured.
    /// </remarks>
    [STAThread]
    public static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    /// <summary>
    /// Builds the Avalonia application configuration.
    /// </summary>
    /// <returns>The configured <see cref="AppBuilder"/>.</returns>
    /// <remarks>
    /// <para>
    /// Public and separate from <see cref="Main"/> by convention: the Avalonia
    /// designer and the headless test host both locate an application by looking
    /// for a <c>BuildAvaloniaApp</c> method with this exact shape.
    /// </para>
    /// <para>
    /// No <c>WithInterFont()</c>: the head deliberately renders in whatever font
    /// the platform supplies as its default — Segoe UI on Windows, SF on macOS —
    /// rather than in a bundled one. Bundling Inter made the two heads differ on
    /// the same machine, since the WPF head has always rendered in Segoe UI, and
    /// Inter's wider metrics forced compensations into the markup (a widened
    /// label column, a nudged button width) that existed only to accommodate the
    /// font. Nothing binds the family by name, so removing this leaves no
    /// dangling reference to fall back from.
    /// </para>
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
}
