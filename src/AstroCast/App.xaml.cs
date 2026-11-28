using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

using Serilog;

using AstroCast.Core.Data;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Services;
using AstroCast.Core.Services.PrimaryDirections;
using AstroCast.Services;
using AstroCast.ViewModels;

namespace AstroCast;

/// <summary>
/// Application entry point. Configures Serilog logging, creates all Core
/// services (ephemeris, aspects, midpoints, chart engine, repository),
/// and wires the <see cref="MainViewModel"/> into the <see cref="MainWindow"/>.
/// </summary>
/// <remarks>
/// <para>
/// Service creation order matters:
/// 1. Serilog logger
/// 2. EphemerisService (needs ephe/ path, IDisposable)
/// 3. AspectService (needs EphemerisService)
/// 4. MidpointService (standalone)
/// 5. ChartEngine (needs all three services)
/// 6. SynastryService (standalone)
/// 7. SqliteChartRepository (needs connection string)
/// 8. WpfDialogService (standalone)
/// 9. MainViewModel (needs engine, repository, ephemeris, synastry, dialog)
/// </para>
/// <para>
/// EphemerisService is disposed in <see cref="OnExit"/> to ensure
/// <c>swe_close()</c> is called exactly once.
/// </para>
/// </remarks>
public partial class App : Application
{
    private IEphemerisService? _ephemerisService;
    private ILogger? _logger;
    private OperationCancellationRegistry? _cancelRegistry;

    /// <summary>
    /// Application startup: create services, initialise database, show main window.
    /// </summary>
    /// <param name="e">Startup event args.</param>
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Editable-ComboBox click-commit (see ComboBoxClickCommitBehavior):
        // registered before any window exists so the class handler covers
        // every dropdown from the first click.
        ComboBoxClickCommitBehavior.Register();

        try
        {
            // 1. Configure Serilog — file sink in the application directory
            string logPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "logs", "astrocast-.log");

            _logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.File(
                    logPath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
                .CreateLogger();

            _logger.Information("AstroCast WPF starting");

            // 1b. Wire global exception safety nets so no unobserved or unhandled
            //     exception can crash the WPF process silently.
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
            AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
            DispatcherUnhandledException += OnDispatcherUnhandledException;

            // 2. EphemerisService — resolve ephe/ path relative to executable
            string ephePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ephe");
            _ephemerisService = new EphemerisService(ephePath, _logger);

            // 2a. The ephemeris files are read once, now (branch EphePath, 1.7.2; finding N4):
            //     one position that needs them, discarded. When they cannot be read the user
            //     is told at once, in the shared check's plain words, in the box this head
            //     shows its other start-up problems in, and the program stops. Until 1.7.2
            //     it started as usual and every chart then failed with a file name in the
            //     status bar.
            if (EphemerisFilesCheck.Run(_ephemerisService, ephePath, _logger) is string unreadable)
            {
                MessageBox.Show(
                    unreadable,
                    "AstroCast — Startup Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                Shutdown(1);
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

            // 12. SqliteChartRepository — database file in the application directory
            string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "astrocast.db");
            string connectionString = $"Data Source={dbPath}";
            IChartRepository repository = new SqliteChartRepository(connectionString, _logger);

            // 13. DialogService
            IDialogService dialogService = new WpfDialogService();

            // 13a. UiDispatcher — the WPF half of the H-2 UI-thread seam. The
            //      view models marshal PropertyChanged through this instead of
            //      touching Application.Current.Dispatcher directly, so the same
            //      view models can back a non-WPF UI later. Shared by
            //      MainViewModel and every ChildViewModelBase.
            IUiDispatcher uiDispatcher = new WpfUiDispatcher();

            // 13c. FileDialogService — the WPF half of the file-picker seam. The
            //      export/import commands ask for a path through this instead of
            //      constructing Microsoft.Win32 dialogs inline, so the same
            //      commands can drive a native picker on another platform.
            IFileDialogService fileDialogService = new WpfFileDialogService();

            // 13b. OperationCancellationRegistry — owns the lifecycle of every
            //      long-running view-model operation's CancellationTokenSource so
            //      the UI can cancel work without holding source references directly.
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

            // 10. Create and show window
            MainWindow window = new()
            {
                DataContext = viewModel,
            };

            window.Show();

            // 9. Initialise database and load charts (after window is visible)
            await viewModel.InitializeAsync();

            _logger.Information("AstroCast WPF started successfully");
        }
        catch (Exception ex)
        {
            string message = $"Failed to start AstroCast:\n\n{ex.Message}";

            if (ex is DirectoryNotFoundException)
            {
                message += "\n\nEnsure the 'ephe' folder with .se1 files is in the application directory.";
            }

            _logger?.Error(ex, "Fatal startup error");

            MessageBox.Show(
                message,
                "AstroCast — Startup Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(1);
        }
    }

    /// <summary>
    /// Application exit: dispose the ephemeris service to call <c>swe_close()</c>.
    /// </summary>
    /// <param name="e">Exit event args.</param>
    protected override void OnExit(ExitEventArgs e)
    {
        _logger?.Information("AstroCast WPF shutting down");

        // Detach global handlers symmetrically with OnStartup so a second App
        // instance (e.g., in tests or restart scenarios) does not double-subscribe.
        TaskScheduler.UnobservedTaskException -= OnUnobservedTaskException;
        AppDomain.CurrentDomain.UnhandledException -= OnAppDomainUnhandledException;
        DispatcherUnhandledException -= OnDispatcherUnhandledException;

        try
        {
            // Cancel and dispose any still-active operation token sources before
            // tearing down ephemeris/logger so background work stops first.
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

        base.OnExit(e);
    }

    /// <summary>
    /// Last-resort handler for <see cref="Task"/> exceptions that were never observed
    /// (no <c>await</c>, no <c>.Wait()</c>, no <c>.Result</c>). Marks them observed so
    /// the finalizer thread does not trigger process termination on .NET 4.5+ behaviour.
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

        // Mark observed so the runtime does not escalate this into a process crash.
        e.SetObserved();
    }

    /// <summary>
    /// Last-resort handler for unhandled exceptions on any AppDomain thread,
    /// including the finalizer thread and background threads not owned by WPF.
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
    /// Last-resort handler for exceptions raised on the WPF UI thread that were not
    /// caught by the originating handler (e.g., an event handler that forgot a
    /// <c>try/catch</c>). Marks the exception handled so the dispatcher continues.
    /// </summary>
    /// <param name="sender">The dispatcher object that raised the event.</param>
    /// <param name="e">
    /// Event args carrying the exception. Setting <see cref="DispatcherUnhandledExceptionEventArgs.Handled"/>
    /// to true keeps the application alive instead of terminating.
    /// </param>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
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
        try
        {
            MessageBox.Show(
                $"An unexpected error occurred:\n\n{e.Exception.Message}\n\nThe application will continue running, but you may want to save your work and restart.",
                "AstroCast — Unexpected Error",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch
        {
            // MessageBox failed (e.g., shutting down) — swallow.
        }

        e.Handled = true;
    }
}
