using AstroCast.Core.Data;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Calculate Relocation keeps the chart engine off the UI thread (1.4.0, port plan §9 item 10).
/// </summary>
/// <remarks>
/// <para>
/// Until 1.4.0 <c>CalculateRelocationAsync</c> cast a chart picked from the dropdown synchronously, before it
/// started its operation, so the cast ran on the UI thread and the panel's Cancel could not reach it. Calculate
/// Relocation now casts inside its one <c>Task.Run</c>, under the relocation operation's token, as Synastry
/// casts Person B. The panel's other caster, the button that copied the birth place, left the panel in 1.4.0
/// (branch Relocate).
/// </para>
/// <para>
/// The engine double records each cast and what the view model reported while it ran.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class RelocationCommandTests
{
    /// <summary>A saved chart in New York, as the dropdown would hold it.</summary>
    private static ChartEntity SavedChart() => new()
    {
        Id = 7,
        Name = "Dropdown chart",
        EventType = "Birth",
        LocalDateTime = "2000-01-01T07:00:00",
        TimeZone = "America/New_York",
        UtcDateTime = "2000-01-01T12:00:00Z",
        JulianDay = 2451545.0,
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        CreatedAt = "2026-10-16T00:00:00Z",
        UpdatedAt = "2026-10-16T00:00:00Z",
    };

    /// <summary>A view model over the given engine, every other service a stub.</summary>
    /// <param name="engine">The chart engine.</param>
    /// <returns>The view model.</returns>
    private static MainViewModel Create(IChartEngine engine) => new(
        engine,
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
        new OperationCancellationRegistry(),
        new LoggerConfiguration().CreateLogger());

    /// <summary>Fills the Relocation panel: a chart from the dropdown and a valid target.</summary>
    /// <param name="vm">The view model.</param>
    private static void FillRelocation(MainViewModel vm)
    {
        vm.Relocation.SelectedNatalChart = SavedChart();
        vm.Relocation.LocationName = "London";
        vm.Relocation.LatitudeText = "51.5074";
        vm.Relocation.LongitudeText = "-0.1278";
    }

    /// <summary>Waits for the relocation command to finish, for at most 5 s.</summary>
    /// <param name="vm">The view model.</param>
    /// <returns>A task that completes when the command has finished.</returns>
    private static async Task WaitForRelocationAsync(MainViewModel vm)
    {
        int waited = 0;
        while (vm.CalculateRelocationCommand.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        vm.CalculateRelocationCommand.IsExecuting.Should().BeFalse("the command finishes within 5 s");
    }

    /// <summary>
    /// A chart picked from the dropdown is cast while the relocation operation is active, so it runs where
    /// Cancel can reach it; until 1.4.0 no operation was active during the cast.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateRelocation_FromTheDropdown_CastsTheNatalChartUnderTheRelocationOperation()
    {
        RecordingChartEngine engine = new();
        MainViewModel vm = Create(engine);
        engine.Probe = () => vm.ActiveOperationKey;
        FillRelocation(vm);

        vm.CalculateRelocationCommand.Execute(null);
        await WaitForRelocationAsync(vm);

        engine.Inputs.Count.Should().Be(1);
        engine.ProbedDuringCalculate.Should().Be(MainViewModel.OperationKeys.Relocation);
        vm.Relocation.HasResult.Should().BeTrue();
    }

    /// <summary>
    /// Cancel reaches a cast of the picked chart: the run ends with "Relocation calculation cancelled." and no
    /// result. Until 1.4.0 the cast ran before the operation started, on the caller's thread, and Cancel could
    /// not reach it.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateRelocation_CancelledDuringTheNatalCast_ReportsCancelled()
    {
        RecordingChartEngine engine = new() { HoldUntilCancelled = true };
        MainViewModel vm = Create(engine);
        FillRelocation(vm);

        vm.CalculateRelocationCommand.Execute(null);
        await engine.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        vm.CancelCommand.CanExecute(MainViewModel.OperationKeys.Relocation)
            .Should().BeTrue("the natal cast runs under the relocation operation");
        vm.CancelCommand.Execute(MainViewModel.OperationKeys.Relocation);
        await WaitForRelocationAsync(vm);

        vm.StatusText.Should().Be("Relocation calculation cancelled.");
        vm.Relocation.HasResult.Should().BeFalse();
    }

    /// <summary>
    /// A cancelled Calculate leaves the result it finds. Since 1.6.1 (branch StaleResult) a result survives only
    /// while the inputs are unchanged, so the one it leaves is for the inputs as they stand.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task ACancelledCalculate_LeavesTheResult()
    {
        RecordingChartEngine engine = new();
        MainViewModel vm = Create(engine);
        FillRelocation(vm);
        vm.CalculateRelocationCommand.Execute(null);
        await WaitForRelocationAsync(vm);
        vm.Relocation.HasResult.Should().BeTrue("the first run lands a result");

        engine.HoldUntilCancelled = true;
        vm.CalculateRelocationCommand.Execute(null);
        vm.CancelCommand.CanExecute(MainViewModel.OperationKeys.Relocation)
            .Should().BeTrue("the second run registers its operation before its cast");
        vm.CancelCommand.Execute(MainViewModel.OperationKeys.Relocation);
        await WaitForRelocationAsync(vm);

        vm.StatusText.Should().Be("Relocation calculation cancelled.");
        vm.Relocation.HasResult.Should().BeTrue("a cancelled run leaves the result it found");
    }
}
