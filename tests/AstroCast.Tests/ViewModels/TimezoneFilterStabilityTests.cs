using System.Collections.Specialized;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Pins the notification contract of
/// <see cref="MainViewModel.FilteredTimezones"/> and
/// <see cref="ElectionalViewModel.FilteredTimezones"/>: the property returns
/// ONE collection instance for the lifetime of the view-model, and each filter
/// change raises exactly one
/// <see cref="NotifyCollectionChangedAction.Reset"/> on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this contract is load-bearing.</b> The Chart Entry and Electional
/// timezone ComboBoxes are editable and bind <c>SelectedItem</c> two-way. WPF's
/// <c>Selector</c> preserves the selected item across a
/// <see cref="NotifyCollectionChangedAction.Reset"/> on a stable
/// <c>ItemsSource</c> instance (the notification shape the original
/// <c>ICollectionView.Refresh()</c> produced) — but treats a <i>replaced</i>
/// <c>ItemsSource</c> instance as a hard reset and drops the selection, pushing
/// <see langword="null"/> through the two-way binding into
/// <c>SelectedTimezone</c>. That regression shipped in the first Phase 3
/// collection rewrite and was caught by manual smoke test, not by the suite:
/// the nulling agent is the WPF <c>Selector</c>, which does not exist in a
/// headless run, so no property-level assertion can observe the defect here.
/// These tests instead pin the mechanism the <c>Selector</c> depends on, which
/// IS observable headless — and they fail against the instance-swapping
/// implementation they were written to guard.
/// </para>
/// <para>
/// <b>Type-agnostic like the sibling suite.</b> No test names the property's
/// declared type or any AstroCast collection type. Instance stability is
/// asserted by reference identity, and the notification shape through
/// <see cref="INotifyCollectionChanged"/> — a BCL interface
/// (<c>System.Collections.Specialized</c>) and precisely the runtime contract
/// WPF itself discovers on the bound instance. The single-event assertions also
/// guard the flicker budget: one <c>Reset</c> per keystroke, never a per-item
/// notification storm across ~600 entries.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class TimezoneFilterStabilityTests
{
    #region Test Infrastructure

    /// <summary>
    /// A filter fragment matching a proper non-empty subset of any realistic
    /// IANA zone list, mirroring the sibling <c>TimezoneFilterTests</c>.
    /// </summary>
    private const string PartitioningFilter = "Europe/";

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

    /// <summary>
    /// Subscribes to the collection currently exposed by
    /// <paramref name="getCollection"/> and records every
    /// <see cref="NotifyCollectionChangedAction"/> it raises. Asserts the
    /// instance implements <see cref="INotifyCollectionChanged"/> — the runtime
    /// contract a WPF <c>ItemsSource</c> needs to observe in-place changes.
    /// </summary>
    private static List<NotifyCollectionChangedAction> RecordActions(
        Func<object> getCollection,
        Action mutate)
    {
        object collection = getCollection();
        collection.Should().BeAssignableTo<INotifyCollectionChanged>(
            "the bound instance must notify in place — a snapshot list forces an " +
            "ItemsSource replacement, which is exactly the selection-dropping defect");

        List<NotifyCollectionChangedAction> actions = [];
        NotifyCollectionChangedEventHandler handler = (_, e) => actions.Add(e.Action);
        INotifyCollectionChanged notifying = (INotifyCollectionChanged)collection;
        notifying.CollectionChanged += handler;
        try
        {
            mutate();
        }
        finally
        {
            notifying.CollectionChanged -= handler;
        }

        return actions;
    }

    #endregion

    #region Instance stability

    /// <summary>
    /// The Chart Entry filtered collection must be the same instance before,
    /// during, and after filtering — a replaced instance is what made the WPF
    /// <c>Selector</c> drop the selected timezone.
    /// </summary>
    [Fact]
    public void ChartEntryFilter_AcrossNarrowAndClear_KeepsSameCollectionInstance()
    {
        MainViewModel vm = CreateViewModel();
        object before = vm.FilteredTimezones;

        vm.ChartEntry.TimeZone = PartitioningFilter;
        ReferenceEquals(vm.FilteredTimezones, before).Should().BeTrue(
            "narrowing must mutate the bound collection in place, not replace it");

        vm.ChartEntry.TimeZone = string.Empty;
        ReferenceEquals(vm.FilteredTimezones, before).Should().BeTrue(
            "clearing the filter must also reuse the same instance");
    }

    /// <summary>
    /// The Electional filtered collection must likewise be one stable instance,
    /// including across <see cref="ElectionalViewModel.Clear"/>.
    /// </summary>
    [Fact]
    public void ElectionalFilter_AcrossNarrowAndClear_KeepsSameCollectionInstance()
    {
        MainViewModel vm = CreateViewModel();
        object before = vm.Electional.FilteredTimezones;

        vm.Electional.TimeZone = PartitioningFilter;
        ReferenceEquals(vm.Electional.FilteredTimezones, before).Should().BeTrue(
            "narrowing must mutate the bound collection in place, not replace it");

        vm.Electional.Clear();
        ReferenceEquals(vm.Electional.FilteredTimezones, before).Should().BeTrue(
            "the panel reset must also reuse the same instance");
    }

    #endregion

    #region Notification shape

    /// <summary>
    /// Narrowing the Chart Entry filter must raise exactly one
    /// <see cref="NotifyCollectionChangedAction.Reset"/> — the shape
    /// <c>ICollectionView.Refresh()</c> produced, and the one WPF's
    /// <c>Selector</c> preserves selection across. Per-item events across ~600
    /// entries would be a flicker regression; more than one event per keystroke
    /// multiplies layout work.
    /// </summary>
    [Fact]
    public void ChartEntryFilter_Narrowing_RaisesExactlyOneReset()
    {
        MainViewModel vm = CreateViewModel();

        List<NotifyCollectionChangedAction> actions = RecordActions(
            () => vm.FilteredTimezones,
            () => vm.ChartEntry.TimeZone = PartitioningFilter);

        actions.Should().ContainSingle()
            .Which.Should().Be(NotifyCollectionChangedAction.Reset);
    }

    /// <summary>
    /// Clearing the Chart Entry filter must also be a single
    /// <see cref="NotifyCollectionChangedAction.Reset"/>.
    /// </summary>
    [Fact]
    public void ChartEntryFilter_Clearing_RaisesExactlyOneReset()
    {
        MainViewModel vm = CreateViewModel();
        vm.ChartEntry.TimeZone = PartitioningFilter;

        List<NotifyCollectionChangedAction> actions = RecordActions(
            () => vm.FilteredTimezones,
            () => vm.ChartEntry.TimeZone = string.Empty);

        actions.Should().ContainSingle()
            .Which.Should().Be(NotifyCollectionChangedAction.Reset);
    }

    /// <summary>
    /// Narrowing the Electional filter must raise exactly one
    /// <see cref="NotifyCollectionChangedAction.Reset"/>.
    /// </summary>
    [Fact]
    public void ElectionalFilter_Narrowing_RaisesExactlyOneReset()
    {
        MainViewModel vm = CreateViewModel();

        List<NotifyCollectionChangedAction> actions = RecordActions(
            () => vm.Electional.FilteredTimezones,
            () => vm.Electional.TimeZone = PartitioningFilter);

        actions.Should().ContainSingle()
            .Which.Should().Be(NotifyCollectionChangedAction.Reset);
    }

    /// <summary>
    /// <see cref="ElectionalViewModel.Clear"/> resets the filter and must raise
    /// exactly one <see cref="NotifyCollectionChangedAction.Reset"/> on the
    /// filtered collection.
    /// </summary>
    [Fact]
    public void ElectionalFilter_Clear_RaisesExactlyOneReset()
    {
        MainViewModel vm = CreateViewModel();
        vm.Electional.TimeZone = PartitioningFilter;

        List<NotifyCollectionChangedAction> actions = RecordActions(
            () => vm.Electional.FilteredTimezones,
            () => vm.Electional.Clear());

        actions.Should().ContainSingle()
            .Which.Should().Be(NotifyCollectionChangedAction.Reset);
    }

    #endregion
}
