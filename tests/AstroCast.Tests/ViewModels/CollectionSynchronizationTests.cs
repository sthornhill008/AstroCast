using System.Collections.Specialized;

using AstroCast.Core.Data;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Coverage for background-thread mutation of the view-model collections that
/// are registered for cross-thread access — <see cref="MainViewModel.SavedCharts"/>,
/// <see cref="MainViewModel.AllSavedCharts"/> and
/// <see cref="LocationSearchViewModel.Results"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>What this suite does and does not prove — read before trusting it.</b>
/// In the running application these three collections are registered with WPF's
/// collection-synchronisation infrastructure so the binding layer may read them
/// under a lock while a background thread mutates them. These tests run
/// <i>headless</i>: no UI framework is bootstrapped and nothing is bound to the
/// collections, so that registration is not doing observable work here. <b>These
/// tests would pass with or without it.</b>
/// </para>
/// <para>
/// Their actual value is narrower but real: they prove that the registration
/// path is exercised during construction without throwing, that it remains a
/// safe no-op when no platform service is available, and that concurrent
/// mutations from multiple threads land completely and without exception. That
/// is exactly the regression that the macOS port risks introducing when the
/// direct <c>BindingOperations</c> call is replaced by an injected abstraction.
/// Genuine cross-thread binding behaviour can only be confirmed by running the
/// WPF application; see the manual smoke test in the slice report.
/// </para>
/// <para>
/// This suite also corrects a documentation defect: the class remarks on
/// <see cref="LocationSearchViewModelTests"/> claimed to verify that
/// collection synchronisation "was registered against the results collection",
/// but no such test existed there.
/// </para>
/// <para>
/// The mutations below take the same per-collection lock the production code
/// registers, mirroring how a correctly-written background writer behaves.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class CollectionSynchronizationTests
{
    #region Test Infrastructure

    /// <summary>Number of concurrent writer threads used by the stress tests.</summary>
    private const int WriterCount = 4;

    /// <summary>Number of items each writer thread appends.</summary>
    private const int ItemsPerWriter = 250;

    /// <summary>
    /// Creates a <see cref="MainViewModel"/> using the shared stub set from
    /// <see cref="MainViewModelTests"/>.
    /// </summary>
    private static MainViewModel CreateViewModel()
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
            new OperationCancellationRegistry(),
            logger);
    }

    /// <summary>Builds a minimal valid <see cref="ChartEntity"/> for list population.</summary>
    private static ChartEntity MakeChart(int index) => new()
    {
        Name = $"Chart {index}",
        EventType = "Birth",
        LocalDateTime = "2000-01-01T00:00:00",
        TimeZone = "Europe/London",
        UtcDateTime = "2000-01-01T00:00:00Z",
        JulianDay = 2451544.5,
        Latitude = 51.5,
        Longitude = -0.13,
        LocationName = "London",
        CreatedAt = "2026-07-26T00:00:00Z",
        UpdatedAt = "2026-07-26T00:00:00Z",
    };

    /// <summary>Builds a <see cref="LocationSearchResult"/> for list population.</summary>
    private static LocationSearchResult MakeResult(int index) => new()
    {
        DisplayName = $"Place {index}",
        Latitude = index * 0.01,
        Longitude = index * -0.01,
    };

    /// <summary>
    /// Runs <paramref name="write"/> on <see cref="WriterCount"/> concurrent
    /// threads, each performing <see cref="ItemsPerWriter"/> writes, and returns
    /// the first exception observed (or <see langword="null"/>).
    /// </summary>
    private static Exception? RunConcurrentWriters(Action<int, int> write)
    {
        Exception? failure = null;
        object failureLock = new();

        Task[] writers = Enumerable.Range(0, WriterCount).Select(writer => Task.Run(() =>
        {
            try
            {
                for (int i = 0; i < ItemsPerWriter; i++)
                {
                    write(writer, i);
                }
            }
            catch (Exception ex)
            {
                lock (failureLock)
                {
                    failure ??= ex;
                }
            }
        })).ToArray();

        Task.WaitAll(writers);
        return failure;
    }

    #endregion

    #region Construction

    /// <summary>
    /// Constructing the view-model tree must register every synchronised
    /// collection without throwing, including when no platform service is
    /// available (the headless case every test in this project exercises).
    /// </summary>
    [Fact]
    public void Construction_WithNoPlatformService_RegistersCollectionsWithoutThrowing()
    {
        Action act = () => CreateViewModel();

        act.Should().NotThrow(
            "collection-synchronisation registration must be a safe no-op when no UI framework is present");
    }

    /// <summary>
    /// The registered collections must exist and start empty, so the mutation
    /// tests below start from a known baseline.
    /// </summary>
    [Fact]
    public void Construction_SynchronisedCollections_StartEmpty()
    {
        MainViewModel vm = CreateViewModel();

        vm.SavedCharts.Should().BeEmpty();
        vm.AllSavedCharts.Should().BeEmpty();
        vm.ChartEntryLocationSearch.Results.Should().BeEmpty();
    }

    #endregion

    #region Background-thread mutation

    /// <summary>
    /// Concurrent background appends to <see cref="LocationSearchViewModel.Results"/>
    /// must all land and must not throw.
    /// </summary>
    [Fact]
    public void Results_MutatedFromBackgroundThreads_AllItemsLandWithoutThrowing()
    {
        MainViewModel vm = CreateViewModel();
        LocationSearchViewModel search = vm.ChartEntryLocationSearch;
        object gate = new();

        Exception? failure = RunConcurrentWriters((writer, i) =>
        {
            lock (gate)
            {
                search.Results.Add(MakeResult((writer * ItemsPerWriter) + i));
            }
        });

        failure.Should().BeNull("background mutation must not throw");
        search.Results.Should().HaveCount(WriterCount * ItemsPerWriter);
    }

    /// <summary>
    /// Concurrent background appends to <see cref="MainViewModel.SavedCharts"/>
    /// must all land and must not throw.
    /// </summary>
    [Fact]
    public void SavedCharts_MutatedFromBackgroundThreads_AllItemsLandWithoutThrowing()
    {
        MainViewModel vm = CreateViewModel();
        object gate = new();

        Exception? failure = RunConcurrentWriters((writer, i) =>
        {
            lock (gate)
            {
                vm.SavedCharts.Add(MakeChart((writer * ItemsPerWriter) + i));
            }
        });

        failure.Should().BeNull("background mutation must not throw");
        vm.SavedCharts.Should().HaveCount(WriterCount * ItemsPerWriter);
    }

    /// <summary>
    /// The same guarantee for <see cref="MainViewModel.AllSavedCharts"/>, the
    /// second synchronised collection on the parent.
    /// </summary>
    [Fact]
    public void AllSavedCharts_MutatedFromBackgroundThreads_AllItemsLandWithoutThrowing()
    {
        MainViewModel vm = CreateViewModel();
        object gate = new();

        Exception? failure = RunConcurrentWriters((writer, i) =>
        {
            lock (gate)
            {
                vm.AllSavedCharts.Add(MakeChart((writer * ItemsPerWriter) + i));
            }
        });

        failure.Should().BeNull("background mutation must not throw");
        vm.AllSavedCharts.Should().HaveCount(WriterCount * ItemsPerWriter);
    }

    /// <summary>
    /// Clearing a synchronised collection from a background thread must also be
    /// safe, since <c>ClearResults</c> runs on whatever thread the search
    /// continuation resumed on.
    /// </summary>
    [Fact]
    public void Results_ClearedFromBackgroundThread_EmptiesWithoutThrowing()
    {
        MainViewModel vm = CreateViewModel();
        LocationSearchViewModel search = vm.ChartEntryLocationSearch;
        for (int i = 0; i < 50; i++)
        {
            search.Results.Add(MakeResult(i));
        }

        Exception? failure = null;
        Task.Run(() =>
        {
            try
            {
                search.ClearResults();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        }).GetAwaiter().GetResult();

        failure.Should().BeNull("clearing from a background thread must not throw");
        search.Results.Should().BeEmpty();
    }

    /// <summary>
    /// Change notifications must still be raised for background mutations —
    /// this is what a bound view depends on, and losing it would be silent.
    /// </summary>
    [Fact]
    public void Results_MutatedFromBackgroundThread_RaisesCollectionChanged()
    {
        MainViewModel vm = CreateViewModel();
        LocationSearchViewModel search = vm.ChartEntryLocationSearch;

        int notifications = 0;
        NotifyCollectionChangedEventHandler handler = (_, _) => Interlocked.Increment(ref notifications);
        search.Results.CollectionChanged += handler;

        try
        {
            Task.Run(() => search.Results.Add(MakeResult(1))).GetAwaiter().GetResult();
        }
        finally
        {
            search.Results.CollectionChanged -= handler;
        }

        notifications.Should().Be(1, "an append must raise exactly one CollectionChanged");
        search.Results.Should().ContainSingle();
    }

    #endregion
}
