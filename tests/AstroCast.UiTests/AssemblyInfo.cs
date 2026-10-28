using Avalonia;
using Avalonia.Headless;
using Avalonia.Themes.Fluent;

using AstroCast.UiTests;

// Tells the headless xUnit integration which application to bootstrap. Required
// once per assembly; without it every [AvaloniaFact] fails at startup.
[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace AstroCast.UiTests;

/// <summary>
/// The minimal Avalonia application used to host headless UI tests.
/// </summary>
/// <remarks>
/// Deliberately not the production <see cref="AstroCast.Avalonia.App"/>: that
/// type's <c>OnFrameworkInitializationCompleted</c> builds the entire Core
/// service graph, opens the SQLite database and starts the Swiss Ephemeris —
/// none of which a view test needs, and all of which would make tests slow and
/// dependent on the user's real data directory. Views under test are given a
/// view model directly instead.
/// </remarks>
public sealed class TestApp : Application
{
    /// <summary>
    /// Initialises a new instance of the <see cref="TestApp"/> class, applying
    /// the Fluent theme so controls have templates.
    /// </summary>
    /// <remarks>
    /// A theme is mandatory. Without one, controls resolve no template, measure
    /// to zero and every layout assertion silently passes against nothing.
    /// </remarks>
    public TestApp() => Styles.Add(new FluentTheme());
}

/// <summary>
/// Builds the Avalonia application for the headless test session.
/// </summary>
public static class TestAppBuilder
{
    /// <summary>
    /// Configures the headless application.
    /// </summary>
    /// <returns>The configured <see cref="AppBuilder"/>.</returns>
    /// <remarks>
    /// <c>UseSkia()</c> together with <c>UseHeadlessDrawing = false</c> enables
    /// real rendering, so a test can capture a rendered frame and assert on
    /// pixels. It costs nothing when unused and cannot be turned on per-test —
    /// the builder is assembly-scoped — so it is enabled from the start.
    /// </remarks>
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<TestApp>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
