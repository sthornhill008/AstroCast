using AstroCast.Core.Interfaces;
using AstroCast.Services;
using AstroCast.ViewModels;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Constructs a fully stubbed <see cref="MainViewModel"/> for tests that need a
/// real view model but no real engine, database, or ephemeris.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this exists.</b> <see cref="MainViewModel"/> takes 23 required
/// constructor arguments. Before Phase 4 slice 1.5 every test file that needed
/// one carried its own private copy of that argument list — nineteen
/// near-identical copies. A UI test in <c>AstroCast.UiTests</c> would have been
/// the twentieth.
/// </para>
/// <para>
/// <b>Scope of the de-duplication.</b> This factory is deliberately additive.
/// The nineteen existing helpers in <c>AstroCast.Tests</c> were left exactly as
/// they are, so no existing test body changed and the 2175-test name list stayed
/// byte-identical. Folding those nineteen into this factory is a separate piece
/// of work; two of them differ from the common shape (one passes a caller-owned
/// <see cref="OperationCancellationRegistry"/>, one an optional dialog service),
/// so it is a refactor with its own verification, not a search-and-replace.
/// </para>
/// </remarks>
public static class MainViewModelFactory
{
    /// <summary>
    /// Creates a <see cref="MainViewModel"/> backed entirely by test doubles.
    /// </summary>
    /// <param name="dialogService">
    /// Dialog service to inject. When <see langword="null"/>, a fresh
    /// <see cref="TestDialogService"/> is used. Pass an instance when the test
    /// needs to assert on recorded confirmation calls.
    /// </param>
    /// <param name="cancelRegistry">
    /// Cancellation registry to inject. When <see langword="null"/>, a fresh
    /// registry is created. Pass an instance when the test needs to observe or
    /// trigger cancellation from outside the view model.
    /// </param>
    /// <param name="logger">
    /// Logger to inject. When <see langword="null"/>, a silent logger is
    /// created — no sinks are configured, so nothing is written anywhere.
    /// </param>
    /// <param name="repository">
    /// Chart repository to inject. When <see langword="null"/>, a
    /// <see cref="StubChartRepository"/>, which stores nothing, is used. Pass a
    /// <see cref="SavedChartsRepository"/> when the test needs saved charts that
    /// come back as new instances on every read.
    /// </param>
    /// <returns>
    /// A new <see cref="MainViewModel"/> whose <see cref="MainViewModel.ActivePanel"/>
    /// is at its default and whose services all throw or return dummy values
    /// rather than performing real calculations.
    /// </returns>
    /// <remarks>
    /// Of the four optional constructor parameters, three — <c>IUiDispatcher</c>,
    /// <c>IFileDialogService</c> and <c>ITimeZoneRules</c> — are intentionally
    /// left unset. They are nullable by design and the view model falls back to
    /// synchronous, dialog-free behaviour and the data the app carries, which is
    /// what a headless test wants. The fourth, <c>IPrimaryDirectionService</c>
    /// (branch PrimaryPanel, 1.5.0), is passed by name as a
    /// <see cref="StubPrimaryDirectionService"/>, which throws, so no test reaches
    /// the real engine through this factory unawares; the fifth,
    /// <c>IDavisonService</c> (branch DavisonPanel, 1.7.0), as a
    /// <see cref="StubDavisonService"/>, which answers with chart A.
    /// </remarks>
    public static MainViewModel Create(
        IDialogService? dialogService = null,
        OperationCancellationRegistry? cancelRegistry = null,
        ILogger? logger = null,
        IChartRepository? repository = null) =>
        new(
            new StubChartEngine(),
            repository ?? new StubChartRepository(),
            new StubEphemerisService(),
            new StubSynastryService(),
            new StubCompositeService(),
            new StubSolarReturnService(),
            new StubLunarReturnService(),
            new StubPlanetaryReturnService(),
            new StubCycleTimelineService(),
            new StubGreatConjunctionService(),
            new StubIngressService(),
            new StubLunationService(),
            new StubRelocationService(),
            new StubMundaneAspectService(),
            new StubEclipseService(),
            new StubReturnIngressTimelineService(),
            new StubSynodicCycleService(),
            new StubTransitService(),
            new StubElectionalService(),
            new StubAstrocartographyService(),
            dialogService ?? new TestDialogService(),
            cancelRegistry ?? new OperationCancellationRegistry(),
            logger ?? new LoggerConfiguration().CreateLogger(),
            primaryDirectionService: new StubPrimaryDirectionService(),
            davisonService: new StubDavisonService());
}
