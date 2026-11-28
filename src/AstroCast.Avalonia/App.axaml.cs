using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;

using Serilog;

using AstroCast.Avalonia.Services;
using AstroCast.Avalonia.Views;
using AstroCast.Core.Data;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Services;
using AstroCast.Core.Services.PrimaryDirections;
using AstroCast.Services;
using AstroCast.ViewModels;

namespace AstroCast.Avalonia;

/// <summary>
/// Application entry point and composition root for the Avalonia head. Configures
/// Serilog, creates every Core service, and wires the <see cref="MainViewModel"/>
/// into the <see cref="MainWindow"/>.
/// </summary>
/// <remarks>
/// <para>
/// This is a faithful port of the WPF head's <c>App.xaml.cs</c>. The service graph
/// — its membership, its construction order, and the arguments passed to each
/// constructor — is identical, because it is plain C# with no framework coupling.
/// Only the parts that touch the UI framework differ:
/// </para>
/// <list type="bullet">
///   <item><description>
///     <c>OnStartup</c>/<c>OnExit</c> become <see cref="Initialize"/>,
///     <see cref="OnFrameworkInitializationCompleted"/> and the lifetime's
///     <c>ShutdownRequested</c> event.
///   </description></item>
///   <item><description>
///     The three platform services resolve to the Avalonia implementations:
///     <see cref="AvaloniaUiDispatcher"/>, <see cref="AvaloniaDialogService"/> and
///     <see cref="AvaloniaFileDialogService"/>.
///   </description></item>
///   <item><description>
///     The writable paths come from <see cref="AppPaths"/> rather than from the
///     executable's directory. See that type for why they diverge.
///   </description></item>
/// </list>
/// <para>
/// <b>Service creation order matters</b> and is preserved verbatim from the WPF
/// head: logger, then <c>EphemerisService</c> (which owns the native handle),
/// then the calculation services that depend on it, then <c>ChartEngine</c>, then
/// the higher-level services, then the repository, then the view model.
/// </para>
/// <para>
/// <c>EphemerisService</c> is disposed on shutdown so <c>swe_close()</c> is called
/// exactly once.
/// </para>
/// </remarks>
public sealed partial class App : Application
{
    /// <summary>The ephemeris service, held so it can be disposed on shutdown.</summary>
    private IEphemerisService? _ephemerisService;

    /// <summary>The root logger, held so it can be flushed and disposed on shutdown.</summary>
    private ILogger? _logger;

    /// <summary>The cancellation registry, held so in-flight work is cancelled on shutdown.</summary>
    private OperationCancellationRegistry? _cancelRegistry;

    /// <summary>
    /// The unexpected-error message on screen, if one is, so at most one shows at a time.
    /// </summary>
    private MessageWindow? _unexpectedErrorWindow;

    /// <inheritdoc />
    /// <remarks>
    /// Also sets the application menu, the menu macOS names after the App (AstroCast, App.axaml), here and not with
    /// the view model in <see cref="OnFrameworkInitializationCompleted"/>: Avalonia's macOS menu exporter reads the
    /// application's menu once, between the two, and does not follow a later change, so a menu set later is never
    /// shown and the default, About Avalonia, stays (MacMenu). Its About AstroCast forwards to the
    /// <see cref="MainViewModel.OpenAboutWindowCommand"/> of the view model the App's DataContext holds by then.
    /// </remarks>
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        NativeMenu.SetMenu(this, AppMenu.Create(new RelayCommand(
            _ => (DataContext as MainViewModel)?.OpenAboutWindowCommand.Execute(null))));
    }

    /// <summary>
    /// Builds the service graph, constructs the main window, and attaches the
    /// shutdown hook.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Avalonia's counterpart to WPF's <c>OnStartup</c>. Unlike that method this
    /// one is synchronous — Avalonia has no async initialisation hook — so the
    /// asynchronous part of startup (<c>InitializeAsync</c>, which opens the
    /// database and loads saved charts) is posted to the dispatcher to run once
    /// the window is up, rather than awaited here.
    /// </para>
    /// <para>
    /// A failure anywhere in this method is fatal: the exception is logged and
    /// written to standard error, and a <see cref="MessageWindow"/> shows it with the
    /// WPF head's text and title (<see cref="StartupFailureMessage"/>, "AstroCast —
    /// Startup Error") as the lifetime's main window; when the user closes it the
    /// application shuts down with exit code 1, or at once if the window cannot be
    /// made (O-8, H-04). Until 1.3.4 this head had no dialog for it and shut down
    /// with nothing on screen.
    /// </para>
    /// </remarks>
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            base.OnFrameworkInitializationCompleted();
            return;
        }

        try
        {
            // 0. Writable locations must exist before the logger or the
            //    repository touches them.
            AppPaths.EnsureCreated();

            // 1. Configure Serilog — rolling file sink in the per-user data directory.
            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    AppPaths.LogFileTemplate,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _logger.Information("AstroCast Avalonia starting");

            // 1b. Wire global exception safety nets so no unobserved or unhandled
            //     exception can terminate the process silently.
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            Dispatcher.UIThread.UnhandledException += OnDispatcherUnhandledException;

            // 2. EphemerisService — ephe/ ships beside the executable.
            _ephemerisService = new EphemerisService(AppPaths.EphemerisDirectory, _logger);

            // 2a. The ephemeris files are read once, now (branch EphePath, 1.7.2; finding N4):
            //     one position that needs them, discarded. When they cannot be read the user
            //     is told at once, in the shared check's plain words, in the window this head
            //     shows its other start-up problems in, and the program stops when it closes.
            if (EphemerisFilesCheck.Run(_ephemerisService, AppPaths.EphemerisDirectory, _logger) is string unreadable)
            {
                Console.Error.WriteLine(unreadable);
                ShowStartupFailure(desktop, unreadable);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            // 3. AspectService
            IAspectService aspectService = new AspectService(_ephemerisService, _logger);

            // 4. MidpointService
            IMidpointService midpointService = new MidpointService(_logger);

            // 5. DignityService (stateless, no dependencies)
            IDignityService dignityService = new DignityService();

            // 6. MoonConditionService (stateless, no dependencies)
            IMoonConditionService moonConditionService = new MoonConditionService();

            // 7a. SectService (stateless, no dependencies)
            ISectService sectService = new SectService();

            // 7b. ChartContextService (depends on DignityService, SectService)
            IChartContextService chartContextService = new ChartContextService(dignityService, sectService);

            // 8. FixedStarService (depends on EphemerisService)
            IFixedStarService fixedStarService = new FixedStarService(_ephemerisService, _logger);

            // 8b. ParanService (depends on EphemerisService)
            IParanService paranService = new ParanService(_ephemerisService, _logger);

            // 9. DeclinationAspectService (stateless, no dependencies)
            DeclinationAspectService declinationAspectService = new();

            // 9b. PlanetConditionService (stateless, no dependencies)
            IPlanetConditionService planetConditionService = new PlanetConditionService();

            // 9c. MoonMansionService (stateless, no dependencies)
            IMoonMansionService moonMansionService = new MoonMansionService();

            // 9d. PlanetaryHourCalculator (depends on EphemerisService)
            PlanetaryHourCalculator planetaryHourCalculator = new(_ephemerisService);

            // 9e. ArabicPartService (depends on DignityService)
            IArabicPartService arabicPartService = new ArabicPartService(dignityService);

            // 9f. AntisciaService (stateless, no dependencies)
            IAntisciaService antisciaService = new AntisciaService();

            // 9g. StationService (stateless, no dependencies)
            IStationService stationService = new StationService();

            // 10. ChartEngine
            IChartEngine chartEngine = new ChartEngine(
                _ephemerisService, aspectService, midpointService, dignityService,
                moonConditionService, chartContextService, fixedStarService,
                declinationAspectService, planetConditionService,
                moonMansionService, planetaryHourCalculator, arabicPartService,
                antisciaService, stationService, paranService, _logger);

            // 11. SynastryService
            ISynastryService synastryService = new SynastryService(_logger);

            // 11e. RelocationService (depends on ChartEngine)
            IRelocationService relocationService = new RelocationService(chartEngine, _logger);

            // 11b. CompositeService
            ICompositeService compositeService = new CompositeService(
                _ephemerisService, aspectService, midpointService, dignityService,
                moonConditionService, chartContextService, fixedStarService, _logger);

            // 11c. SolarReturnService
            ISolarReturnService solarReturnService = new SolarReturnService(
                _ephemerisService, chartEngine, _logger);

            // 11d. LunarReturnService
            ILunarReturnService lunarReturnService = new LunarReturnService(
                _ephemerisService, chartEngine, _logger);

            // 11e. PlanetaryReturnService
            IPlanetaryReturnService planetaryReturnService = new PlanetaryReturnService(
                _ephemerisService, chartEngine, _logger);

            // 11f. GreatConjunctionService
            IGreatConjunctionService greatConjunctionService = new GreatConjunctionService(
                _ephemerisService, chartEngine, _logger);

            // 11g. IngressService
            IIngressService ingressService = new IngressService(
                _ephemerisService, chartEngine, _logger);

            // 11h. LunationService
            ILunationService lunationService = new LunationService(
                _ephemerisService, chartEngine, _logger);

            // 11i. PlanetaryCycleTimelineService
            IPlanetaryCycleTimelineService cycleTimelineService = new PlanetaryCycleTimelineService(
                _ephemerisService, planetaryReturnService, _logger);

            // 11j. MundaneAspectService
            IMundaneAspectService mundaneAspectService = new MundaneAspectService(
                _ephemerisService, _logger);

            // 11k. EclipseService
            IEclipseService eclipseService = new EclipseService(
                _ephemerisService, _logger);

            // 11l. SynodicCycleService
            ISynodicCycleService synodicCycleService = new SynodicCycleService(
                _ephemerisService, _logger);

            // 11m. ReturnIngressTimelineService
            IReturnIngressTimelineService returnIngressTimelineService =
                new ReturnIngressTimelineService(_ephemerisService, _logger);

            // 11n. TransitService
            ITransitService transitService = new TransitService(_ephemerisService, _logger);
            //      The primary-direction engine beside it, the Directions panel's (branch PrimaryPanel, 1.5.0)
            IPrimaryDirectionService primaryDirectionService = new PrimaryDirectionService(_ephemerisService);
            //      The Davison chart's service beside it, the Synastry panel's Davison section's (branch DavisonPanel, 1.7.0)
            IDavisonService davisonService = new DavisonService(chartEngine, _logger);

            // 11o. ElectionalService
            IElectionalService electionalService = new ElectionalService(_ephemerisService, chartEngine);

            // 11p. AstrocartographyService
            IAstrocartographyService astrocartographyService =
                new AstrocartographyService(_ephemerisService, _logger);

            // 12. SqliteChartRepository — database in the per-user data directory.
            //     The resolved path is logged: startup previously recorded only
            //     the ephemeris directory, so when the two heads' stores had to
            //     be told apart the database location had to be recovered from
            //     the filesystem rather than read from the log.
            _logger.Information("Database path: {DatabasePath}", AppPaths.DatabaseFile);
            string connectionString = $"Data Source={AppPaths.DatabaseFile}";
            IChartRepository repository = new SqliteChartRepository(connectionString, _logger);

            // 13. DialogService — the real Avalonia implementation. The Chart
            //     panel's overwrite prompt is the first reachable call site, so
            //     the null object it replaced is no longer sufficient here. That
            //     null object remains the default for headless hosts.
            IDialogService dialogService = new AvaloniaDialogService();

            // 13a. UiDispatcher — the Avalonia half of the UI-thread seam. See
            //      AvaloniaUiDispatcher for why it is not a straight pass-through
            //      to Dispatcher.UIThread.
            IUiDispatcher uiDispatcher = new AvaloniaUiDispatcher();

            // 13c. FileDialogService — likewise the real implementation, over
            //      Avalonia's IStorageProvider. Reachable from the Chart panel's
            //      Save Markdown command.
            IFileDialogService fileDialogService = new AvaloniaFileDialogService();

            // 13b. OperationCancellationRegistry — owns the lifecycle of every
            //      long-running view-model operation's CancellationTokenSource.
            _cancelRegistry = new OperationCancellationRegistry();

            // 14. MainViewModel
            MainViewModel viewModel = new(
                chartEngine, repository, _ephemerisService, synastryService,
                compositeService, solarReturnService, lunarReturnService,
                planetaryReturnService, cycleTimelineService,
                greatConjunctionService, ingressService, lunationService,
                relocationService, mundaneAspectService, eclipseService,
                returnIngressTimelineService, synodicCycleService,
                transitService, electionalService, astrocartographyService,
                dialogService, _cancelRegistry, _logger, uiDispatcher, fileDialogService,
                primaryDirectionService: primaryDirectionService,
                davisonService: davisonService);

            // 15. Main window.
            desktop.MainWindow = new MainWindow
            {
                DataContext = viewModel,
            };

            // 15b. The App's DataContext — the application menu set in Initialize
            //      (About AstroCast, on macOS) opens the About window through it.
            DataContext = viewModel;

            desktop.ShutdownRequested += OnShutdownRequested;

            // 16. Initialise database and load charts once the window is up.
            //     Posted rather than awaited: this method is synchronous, and
            //     blocking it would stall the framework before the window shows.
            Dispatcher.UIThread.Post(
                async void () =>
                {
                    try
                    {
                        await viewModel.InitializeAsync();
                        _logger.Information("AstroCast Avalonia started successfully");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, "Deferred startup initialisation failed");
                    }
                },
                DispatcherPriority.Normal);
        }
        catch (Exception ex)
        {
            string message = StartupFailureMessage(ex);

            _logger?.Error(ex, "Fatal startup error");
            Console.Error.WriteLine(message);

            ShowStartupFailure(desktop, message);
        }

        base.OnFrameworkInitializationCompleted();
    }

    /// <summary>
    /// Shutdown hook: cancels in-flight work, releases the native ephemeris
    /// handle, and flushes the logger.
    /// </summary>
    /// <param name="sender">The application lifetime raising the event.</param>
    /// <param name="e">Shutdown event args; never cancelled here.</param>
    /// <remarks>
    /// Avalonia's counterpart to WPF's <c>OnExit</c>. Handlers are detached
    /// symmetrically with the startup path so a second application instance — in
    /// a test host, for example — does not double-subscribe.
    /// </remarks>
    private void OnShutdownRequested(object? sender, ShutdownRequestedEventArgs e)
    {
        _logger?.Information("AstroCast Avalonia shutting down");

        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
        Dispatcher.UIThread.UnhandledException -= OnDispatcherUnhandledException;

        try
        {
            // Cancel and dispose any still-active operation token sources before
            // tearing down ephemeris/logger, so background work stops first.
            _cancelRegistry?.Dispose();
            _ephemerisService?.Dispose();
        }
        finally
        {
            if (_logger is IDisposable disposableLogger)
            {
                disposableLogger.Dispose();
            }
        }
    }

    /// <summary>
    /// Last-resort handler for <see cref="Task"/> exceptions that were never
    /// observed. Marks them observed so the finalizer thread does not escalate
    /// them into process termination.
    /// </summary>
    /// <param name="sender">The unobserved task scheduler.</param>
    /// <param name="e">Event args carrying the aggregate exception.</param>
    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            _logger?.Error(
                e.Exception,
                "Unobserved task exception (Inner count: {InnerCount})",
                e.Exception.InnerExceptions.Count);
        }
        catch
        {
            // Logger unavailable — nothing else we can safely do here.
        }

        e.SetObserved();
    }

    /// <summary>
    /// Last-resort handler for unhandled exceptions on any AppDomain thread,
    /// including the finalizer thread and background threads not owned by
    /// Avalonia.
    /// </summary>
    /// <param name="sender">The current AppDomain.</param>
    /// <param name="e">
    /// Event args carrying the exception object and the
    /// <see cref="UnhandledExceptionEventArgs.IsTerminating"/> flag.
    /// </param>
    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        try
        {
            Exception? ex = e.ExceptionObject as Exception;

            _logger?.Fatal(
                ex,
                "AppDomain unhandled exception (IsTerminating={IsTerminating})",
                e.IsTerminating);
        }
        catch
        {
            // Logger unavailable — nothing else we can safely do here.
        }
    }

    /// <summary>
    /// Last-resort handler for exceptions raised on the Avalonia UI thread that
    /// were not caught by the originating handler. Marks the exception handled so
    /// the dispatcher continues rather than terminating the application.
    /// </summary>
    /// <param name="sender">The dispatcher that raised the event.</param>
    /// <param name="e">
    /// Event args carrying the exception. Setting
    /// <see cref="DispatcherUnhandledExceptionEventArgs.Handled"/> to
    /// <see langword="true"/> keeps the application alive.
    /// </param>
    /// <remarks>
    /// The direct counterpart of WPF's <c>DispatcherUnhandledException</c>.
    /// Avalonia 12 exposes the same shape — including a settable
    /// <c>Handled</c> — so the WPF head's third safety net ports without change
    /// of behaviour. As the WPF head shows a <c>MessageBox</c> here, this head shows
    /// a <see cref="MessageWindow"/> with the same text and title
    /// (<see cref="UnexpectedErrorMessage"/>, "AstroCast — Unexpected Error"), over
    /// the main window when it is visible, at most one at a time (O-8, H-05). Until
    /// 1.3.4 it logged only.
    /// </remarks>
    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            _logger?.Error(e.Exception, "Dispatcher unhandled exception");
        }
        catch
        {
            // Logger unavailable — nothing else we can safely do here.
        }

        // Surface the failure to the user, then keep the app alive.
        ShowUnexpectedError(e.Exception);

        e.Handled = true;
    }

    /// <summary>
    /// The text a start-up failure shows, exactly the WPF head's: "Failed to start AstroCast:", two newlines
    /// and the exception's message, and for a <see cref="DirectoryNotFoundException"/> two newlines and the
    /// hint that the ephemeris folder belongs in the application directory.
    /// </summary>
    /// <param name="ex">The exception start-up failed with.</param>
    /// <returns>The message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ex"/> is null.</exception>
    internal static string StartupFailureMessage(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        string message = $"Failed to start AstroCast:\n\n{ex.Message}";

        if (ex is DirectoryNotFoundException)
        {
            message += "\n\nEnsure the 'ephe' folder with .se1 files is in the application directory.";
        }

        return message;
    }

    /// <summary>
    /// The text an unhandled UI error shows, exactly the WPF head's: "An unexpected error occurred:", two
    /// newlines, the exception's message, two newlines, and that the application continues running.
    /// </summary>
    /// <param name="ex">The unhandled exception.</param>
    /// <returns>The message.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="ex"/> is null.</exception>
    internal static string UnexpectedErrorMessage(Exception ex)
    {
        ArgumentNullException.ThrowIfNull(ex);

        return $"An unexpected error occurred:\n\n{ex.Message}\n\nThe application will continue running, but you may want to save your work and restart.";
    }

    /// <summary>
    /// Shows a start-up failure: the lifetime stops shutting down on its own, a <see cref="MessageWindow"/>
    /// becomes the main window, and closing it shuts the application down with exit code 1. When the window
    /// cannot be made, shuts down with 1 at once.
    /// </summary>
    /// <param name="desktop">The application's lifetime.</param>
    /// <param name="message">The failure's message.</param>
    private void ShowStartupFailure(IClassicDesktopStyleApplicationLifetime desktop, string message)
    {
        try
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            MessageWindow window = new(message, "AstroCast \u2014 Startup Error")
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
            };
            window.Closed += (_, _) => desktop.Shutdown(1);
            desktop.MainWindow = window;
        }
        catch (Exception windowException)
        {
            try
            {
                _logger?.Error(windowException, "Could not show the start-up failure");
            }
            catch
            {
                // Logger unavailable — nothing else we can safely do here.
            }

            desktop.Shutdown(1);
        }
    }

    /// <summary>
    /// Shows an unhandled UI error in a <see cref="MessageWindow"/>, over the main window when it is visible,
    /// else on its own; at most one at a time, and any failure in showing it swallowed, as the WPF head
    /// swallows a <c>MessageBox</c> that cannot show.
    /// </summary>
    /// <param name="exception">The unhandled exception.</param>
    private void ShowUnexpectedError(Exception exception)
    {
        if (_unexpectedErrorWindow is not null)
            return;

        try
        {
            MessageWindow window = new(UnexpectedErrorMessage(exception), "AstroCast \u2014 Unexpected Error");
            window.Closed += (_, _) => _unexpectedErrorWindow = null;
            _unexpectedErrorWindow = window;

            Window? owner = (ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            if (owner is { IsVisible: true })
            {
                _ = window.ShowDialog(owner);
            }
            else
            {
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                window.Show();
            }
        }
        catch
        {
            // The window failed to show (e.g., shutting down) — swallow.
            _unexpectedErrorWindow = null;
        }
    }
}
