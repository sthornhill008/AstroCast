using System.Reflection;

using AstroCast.Core.Interfaces;
using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for the cancellation surface exposed by <see cref="MainViewModel"/>:
/// the parameter-aware <see cref="MainViewModel.CancelCommand"/>, its CanExecute
/// predicate, and the <see cref="MainViewModel.HasActiveOperation"/> /
/// <see cref="MainViewModel.ActiveOperationDisplayName"/> notifications.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MainViewModelCancellationTests
{
    #region Test Infrastructure

    /// <summary>
    /// Creates a <see cref="MainViewModel"/> bound to a caller-supplied
    /// <see cref="OperationCancellationRegistry"/> so the test can drive the
    /// registry directly to simulate active operations.
    /// </summary>
    private static MainViewModel CreateViewModel(OperationCancellationRegistry registry, IDavisonService? davisonService = null)
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        return new MainViewModel(
            new StubChartEngine(),
            new StubChartRepository(),
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
            new TestDialogService(),
            registry,
            logger,
            davisonService: davisonService);
    }

    #endregion

    #region CancelCommand — CanExecute

    /// <summary>
    /// When no operation is registered, the global Cancel button (no command
    /// parameter) must report <c>false</c> from CanExecute so the button is
    /// disabled in the UI.
    /// </summary>
    [Fact]
    public void CancelCommand_NoOperationActive_CanExecuteReturnsFalse()
    {
        using OperationCancellationRegistry registry = new();
        MainViewModel vm = CreateViewModel(registry);

        bool canExecute = vm.CancelCommand.CanExecute(null);

        canExecute.Should().BeFalse(
            "Cancel must stay disabled when no long-running operation is active");
        vm.HasActiveOperation.Should().BeFalse();
    }

    #endregion

    #region CancelCommand — Per-Key Cancellation

    /// <summary>
    /// Executing CancelCommand with a specific operation key must cancel only
    /// that operation's token; concurrently-registered operations must remain
    /// uncancelled so independent panels can run side by side.
    /// </summary>
    [Fact]
    public void CancelCommand_OperationKey_CancelsOnlyThatOperation()
    {
        using OperationCancellationRegistry registry = new();
        MainViewModel vm = CreateViewModel(registry);

        CancellationToken transitsToken = registry.StartOperation(
            MainViewModel.OperationKeys.Transits);
        CancellationToken acgToken = registry.StartOperation(
            MainViewModel.OperationKeys.Astrocartography);

        vm.CancelCommand.Execute(MainViewModel.OperationKeys.Transits);

        transitsToken.IsCancellationRequested.Should().BeTrue(
            "the per-key Cancel button must cancel its own operation");
        acgToken.IsCancellationRequested.Should().BeFalse(
            "other concurrent operations must not be affected by a per-key cancel");
    }

    #endregion

    #region CancelCommand — Global Cancellation

    /// <summary>
    /// Executing CancelCommand with no parameter (the global "Cancel All"
    /// button) must cancel every currently registered operation in a single
    /// pass.
    /// </summary>
    [Fact]
    public void CancelCommand_NullParameter_CancelsAllRegisteredOperations()
    {
        using OperationCancellationRegistry registry = new();
        MainViewModel vm = CreateViewModel(registry);

        CancellationToken transitsToken = registry.StartOperation(
            MainViewModel.OperationKeys.Transits);
        CancellationToken acgToken = registry.StartOperation(
            MainViewModel.OperationKeys.Astrocartography);
        CancellationToken electionalToken = registry.StartOperation(
            MainViewModel.OperationKeys.Electional);

        vm.CancelCommand.Execute(null);

        transitsToken.IsCancellationRequested.Should().BeTrue();
        acgToken.IsCancellationRequested.Should().BeTrue();
        electionalToken.IsCancellationRequested.Should().BeTrue();
    }

    #endregion

    #region OperationKeys — Invariants

    /// <summary>
    /// Reflects over every public <c>const string</c> on
    /// <see cref="MainViewModel.OperationKeys"/> and returns their values.
    /// The guard tests below use this helper so new keys are automatically
    /// included without requiring test-code updates.
    /// </summary>
    private static IReadOnlyList<string> GetAllOperationKeys()
    {
        return typeof(MainViewModel.OperationKeys)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
            .Where(f => f.IsLiteral && f.FieldType == typeof(string))
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();
    }

    /// <summary>
    /// Every <see cref="MainViewModel.OperationKeys"/> constant must appear in
    /// the <c>s_operationDisplayNames</c> dictionary so the status bar and
    /// Cancel-button tooltips never fall back to raw keys. This guards against
    /// new operations being added to the enum without a matching display label.
    /// </summary>
    [Fact]
    public void AllOperationKeys_HaveDisplayNameEntries()
    {
        IReadOnlyList<string> keys = GetAllOperationKeys();
        keys.Should().NotBeEmpty("OperationKeys must declare at least one constant");

        MethodInfo getDisplayName = typeof(MainViewModel).GetMethod(
            "GetOperationDisplayName",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        foreach (string key in keys)
        {
            string displayName = (string)getDisplayName.Invoke(null, [key])!;
            displayName.Should().NotBe(key,
                $"operation key '{key}' is missing a human-readable entry in s_operationDisplayNames");
        }
    }

    /// <summary>
    /// Operation keys double as stable string identifiers in XAML
    /// <c>CommandParameter</c> bindings and registry lookups. Duplicates would
    /// silently merge two panels' cancellation scopes, so this test asserts
    /// every constant has a unique value.
    /// </summary>
    [Fact]
    public void AllOperationKeys_AreUnique()
    {
        IReadOnlyList<string> keys = GetAllOperationKeys();

        keys.Should().OnlyHaveUniqueItems(
            "duplicate OperationKeys values would collapse separate panels onto " +
            "a single cancellation scope");
    }

    #endregion

    /// <summary>
    /// Cancel with the Davison key cancels a running Davison calculation through its own registry key: the
    /// service's token is cancelled and the status line reads "Davison calculation cancelled." (branch
    /// DavisonPanel, 1.7.0).
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CancelDavison_CancelsThroughItsOwnKey()
    {
        using OperationCancellationRegistry registry = new();
        StubDavisonService davison = new() { HoldUntilCancelled = true };
        MainViewModel vm = CreateViewModel(registry, davison);
        typeof(MainViewModel).GetProperty(nameof(MainViewModel.LastResult))
            ?.SetValue(vm, new StubChartEngine().Calculate(new AstroCast.Core.Models.ChartInput
            {
                Name = "Person A",
                LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0),
                TimeZone = "UTC",
                Latitude = 40.7128,
                Longitude = -74.006,
                LocationName = "New York City",
            }));
        typeof(MainViewModel).GetProperty(nameof(MainViewModel.HasResult))?.SetValue(vm, true);
        vm.Synastry.PersonB = new AstroCast.Core.Data.ChartEntity
        {
            Id = 2,
            Name = "Person B",
            EventType = "Birth",
            LocalDateTime = "2000-01-01T12:00:00",
            TimeZone = "UTC",
            UtcDateTime = "2000-01-01T12:00:00Z",
            JulianDay = 2451545.0,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            CreatedAt = "2026-10-21T00:00:00Z",
            UpdatedAt = "2026-10-21T00:00:00Z",
        };

        vm.CalculateDavisonCommand.CanExecute(null).Should().BeTrue("a chart is on screen and Person B is picked");
        vm.CalculateDavisonCommand.Execute(null);
        await davison.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        vm.CancelCommand.CanExecute(MainViewModel.OperationKeys.Davison)
            .Should().BeTrue("the Davison calculation runs under its own key");
        vm.CancelCommand.Execute(MainViewModel.OperationKeys.Davison);

        int waited = 0;
        while (vm.CalculateDavisonCommand.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        vm.CalculateDavisonCommand.IsExecuting.Should().BeFalse("the command finishes within 5 s");
        vm.StatusText.Should().Be("Davison calculation cancelled.");
        vm.Synastry.DavisonResult.Should().BeNull();
        registry.IsOperationActive(MainViewModel.OperationKeys.Davison).Should().BeFalse();
    }
}
