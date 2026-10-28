using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Behavioural coverage for the timezone substring filter exposed by
/// <see cref="MainViewModel.FilteredTimezones"/> and
/// <see cref="ElectionalViewModel.FilteredTimezones"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this suite exists.</b> Two live editable ComboBoxes
/// (<c>MainWindow.xaml:447</c> for Chart Entry and <c>:1950</c> for Electional,
/// both <c>IsEditable=True StaysOpenOnEdit=True</c>) filter roughly six hundred
/// IANA zone identifiers as the user types. Until this suite was written that
/// behaviour had <i>no</i> automated coverage at all — the existing timezone
/// tests only exercise <c>IsTimezoneValid</c> and <c>SelectedTimezone</c>. The
/// macOS port replaces the underlying WPF <c>CollectionViewSource</c> /
/// <c>ICollectionView</c> machinery with framework-neutral filtering, and this
/// suite is the regression net for that swap.
/// </para>
/// <para>
/// <b>Deliberately type-agnostic.</b> Every assertion enumerates
/// <c>FilteredTimezones</c> through <see cref="System.Linq.Enumerable.Cast{TResult}"/>
/// and never names the property's declared type, never touches <c>.Filter</c>,
/// <c>.View</c> or <c>.Refresh()</c>, and never imports
/// <c>System.Windows.Data</c>. The suite therefore compiles and asserts the
/// same observable contract before and after the underlying type changes, and
/// must pass <b>unedited</b> across that change. Needing to edit these tests to
/// accommodate a new implementation would mean the observable contract moved,
/// which is precisely what they exist to prevent.
/// </para>
/// <para>
/// <b>Expectations are derived, not hard-coded.</b> The available zone list
/// comes from the host operating system, so the expected result of every filter
/// is computed from <see cref="MainViewModel.AvailableTimezones"/> using the
/// documented predicate rather than pinned to specific zone names. Each test
/// additionally asserts that the expected set is non-empty and a proper subset
/// of the full list, so a filter that silently matched everything (or nothing)
/// cannot make an assertion pass vacuously.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class TimezoneFilterTests
{
    #region Test Infrastructure

    /// <summary>
    /// A filter fragment guaranteed to partition any realistic IANA zone list
    /// into a non-empty matching set and a non-empty non-matching set, and which
    /// is never itself an exact zone identifier (so it always survives the
    /// "clear the filter on an exact match" rule).
    /// </summary>
    private const string PartitioningFilter = "Europe/";

    /// <summary>
    /// Creates a <see cref="MainViewModel"/> using the shared stub set from
    /// <see cref="MainViewModelTests"/>. Both filters under test hang off a
    /// single parent: Chart Entry's lives on the parent itself, Electional's on
    /// its child, and both read the parent-owned
    /// <see cref="MainViewModel.AvailableTimezones"/>.
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
    /// Enumerates the Chart Entry filtered view without naming its type.
    /// </summary>
    private static List<string> VisibleChartEntry(MainViewModel vm) =>
        vm.FilteredTimezones.Cast<string>().ToList();

    /// <summary>
    /// Enumerates the Electional filtered view without naming its type.
    /// </summary>
    private static List<string> VisibleElectional(MainViewModel vm) =>
        vm.Electional.FilteredTimezones.Cast<string>().ToList();

    /// <summary>
    /// Computes the expected filter result using the predicate documented on
    /// the production code: show everything when the filter text is empty,
    /// otherwise case-insensitive substring match.
    /// </summary>
    private static List<string> Expected(MainViewModel vm, string filterText) =>
        string.IsNullOrEmpty(filterText)
            ? vm.AvailableTimezones.ToList()
            : vm.AvailableTimezones
                .Where(tz => tz.Contains(filterText, StringComparison.OrdinalIgnoreCase))
                .ToList();

    /// <summary>
    /// Asserts that <paramref name="filterText"/> genuinely partitions the zone
    /// list, so a downstream equality assertion cannot pass vacuously against a
    /// filter that matches everything or nothing.
    /// </summary>
    private static void AssertIsMeaningfulPartition(MainViewModel vm, string filterText)
    {
        List<string> expected = Expected(vm, filterText);
        expected.Should().NotBeEmpty(
            "the chosen filter fragment must match at least one zone, or the test proves nothing");
        expected.Count.Should().BeLessThan(vm.AvailableTimezones.Count,
            "the chosen filter fragment must exclude at least one zone, or the test proves nothing");
    }

    #endregion

    #region Chart Entry filter

    /// <summary>
    /// With no filter text the Chart Entry view must expose every available zone.
    /// </summary>
    [Fact]
    public void ChartEntryFilter_NoFilterText_ShowsAllTimezones()
    {
        MainViewModel vm = CreateViewModel();

        VisibleChartEntry(vm).Should().BeEquivalentTo(vm.AvailableTimezones);
    }

    /// <summary>
    /// Typing a partial zone fragment must narrow the Chart Entry view to
    /// exactly the case-insensitive substring matches.
    /// </summary>
    [Fact]
    public void ChartEntryFilter_PartialText_NarrowsToSubstringMatches()
    {
        MainViewModel vm = CreateViewModel();
        AssertIsMeaningfulPartition(vm, PartitioningFilter);

        vm.ChartEntry.TimeZone = PartitioningFilter;

        List<string> visible = VisibleChartEntry(vm);
        visible.Should().BeEquivalentTo(Expected(vm, PartitioningFilter));
        visible.Should().OnlyContain(
            tz => tz.Contains(PartitioningFilter, StringComparison.OrdinalIgnoreCase));
        visible.Count.Should().BeLessThan(vm.AvailableTimezones.Count);
    }

    /// <summary>
    /// The Chart Entry filter must be case-insensitive: a lowercase fragment
    /// must produce the same set as its canonical-cased equivalent.
    /// </summary>
    [Fact]
    public void ChartEntryFilter_LowercaseText_MatchesCaseInsensitively()
    {
        MainViewModel vm = CreateViewModel();
        AssertIsMeaningfulPartition(vm, PartitioningFilter);

        vm.ChartEntry.TimeZone = PartitioningFilter.ToLowerInvariant();

        VisibleChartEntry(vm).Should().BeEquivalentTo(Expected(vm, PartitioningFilter));
    }

    /// <summary>
    /// An exact zone identifier clears the filter rather than narrowing to the
    /// single match — once the user has committed to a valid zone the dropdown
    /// shows the whole list again so they can pick a different one.
    /// </summary>
    [Fact]
    public void ChartEntryFilter_ExactMatch_ShowsAllTimezones()
    {
        MainViewModel vm = CreateViewModel();
        string exact = vm.AvailableTimezones[0];

        vm.ChartEntry.TimeZone = exact;

        VisibleChartEntry(vm).Should().BeEquivalentTo(vm.AvailableTimezones,
            "an exact match clears the filter text");
    }

    /// <summary>
    /// The reset path: clearing the text after filtering must restore the full
    /// list.
    /// </summary>
    [Fact]
    public void ChartEntryFilter_ClearedAfterFiltering_RestoresFullList()
    {
        MainViewModel vm = CreateViewModel();
        vm.ChartEntry.TimeZone = PartitioningFilter;
        VisibleChartEntry(vm).Count.Should().BeLessThan(vm.AvailableTimezones.Count);

        vm.ChartEntry.TimeZone = string.Empty;

        VisibleChartEntry(vm).Should().BeEquivalentTo(vm.AvailableTimezones);
    }

    /// <summary>
    /// Surrounding whitespace is trimmed before filtering.
    /// </summary>
    [Fact]
    public void ChartEntryFilter_TextWithWhitespace_IsTrimmedBeforeFiltering()
    {
        MainViewModel vm = CreateViewModel();
        AssertIsMeaningfulPartition(vm, PartitioningFilter);

        vm.ChartEntry.TimeZone = "  " + PartitioningFilter + "  ";

        VisibleChartEntry(vm).Should().BeEquivalentTo(Expected(vm, PartitioningFilter));
    }

    /// <summary>
    /// A fragment matching no zone at all must produce an empty view rather
    /// than falling back to the full list.
    /// </summary>
    [Fact]
    public void ChartEntryFilter_NoMatches_ProducesEmptyView()
    {
        MainViewModel vm = CreateViewModel();

        vm.ChartEntry.TimeZone = "zzz-no-such-zone-zzz";

        VisibleChartEntry(vm).Should().BeEmpty();
    }

    #endregion

    #region Electional filter

    /// <summary>
    /// With no filter text the Electional view must expose every available zone.
    /// </summary>
    [Fact]
    public void ElectionalFilter_NoFilterText_ShowsAllTimezones()
    {
        MainViewModel vm = CreateViewModel();

        VisibleElectional(vm).Should().BeEquivalentTo(vm.AvailableTimezones);
    }

    /// <summary>
    /// Typing a partial zone fragment must narrow the Electional view to exactly
    /// the case-insensitive substring matches.
    /// </summary>
    [Fact]
    public void ElectionalFilter_PartialText_NarrowsToSubstringMatches()
    {
        MainViewModel vm = CreateViewModel();
        AssertIsMeaningfulPartition(vm, PartitioningFilter);

        vm.Electional.TimeZone = PartitioningFilter;

        List<string> visible = VisibleElectional(vm);
        visible.Should().BeEquivalentTo(Expected(vm, PartitioningFilter));
        visible.Should().OnlyContain(
            tz => tz.Contains(PartitioningFilter, StringComparison.OrdinalIgnoreCase));
        visible.Count.Should().BeLessThan(vm.AvailableTimezones.Count);
    }

    /// <summary>
    /// The Electional filter must be case-insensitive.
    /// </summary>
    [Fact]
    public void ElectionalFilter_LowercaseText_MatchesCaseInsensitively()
    {
        MainViewModel vm = CreateViewModel();
        AssertIsMeaningfulPartition(vm, PartitioningFilter);

        vm.Electional.TimeZone = PartitioningFilter.ToLowerInvariant();

        VisibleElectional(vm).Should().BeEquivalentTo(Expected(vm, PartitioningFilter));
    }

    /// <summary>
    /// An exact zone identifier clears the Electional filter, matching Chart
    /// Entry's behaviour.
    /// </summary>
    [Fact]
    public void ElectionalFilter_ExactMatch_ShowsAllTimezones()
    {
        MainViewModel vm = CreateViewModel();
        string exact = vm.AvailableTimezones[0];

        vm.Electional.TimeZone = exact;

        VisibleElectional(vm).Should().BeEquivalentTo(vm.AvailableTimezones,
            "an exact match clears the filter text");
    }

    /// <summary>
    /// The reset path: <see cref="ElectionalViewModel.Clear"/> must restore the
    /// full list after filtering.
    /// </summary>
    [Fact]
    public void ElectionalFilter_Clear_RestoresFullList()
    {
        MainViewModel vm = CreateViewModel();
        vm.Electional.TimeZone = PartitioningFilter;
        VisibleElectional(vm).Count.Should().BeLessThan(vm.AvailableTimezones.Count);

        vm.Electional.Clear();

        VisibleElectional(vm).Should().BeEquivalentTo(vm.AvailableTimezones);
    }

    /// <summary>
    /// Clearing the text directly must also restore the full list.
    /// </summary>
    [Fact]
    public void ElectionalFilter_ClearedAfterFiltering_RestoresFullList()
    {
        MainViewModel vm = CreateViewModel();
        vm.Electional.TimeZone = PartitioningFilter;
        VisibleElectional(vm).Count.Should().BeLessThan(vm.AvailableTimezones.Count);

        vm.Electional.TimeZone = string.Empty;

        VisibleElectional(vm).Should().BeEquivalentTo(vm.AvailableTimezones);
    }

    /// <summary>
    /// A fragment matching no zone must produce an empty Electional view.
    /// </summary>
    [Fact]
    public void ElectionalFilter_NoMatches_ProducesEmptyView()
    {
        MainViewModel vm = CreateViewModel();

        vm.Electional.TimeZone = "zzz-no-such-zone-zzz";

        VisibleElectional(vm).Should().BeEmpty();
    }

    #endregion

    #region Independence

    /// <summary>
    /// The two filters share one source list but must hold independent filter
    /// state: narrowing Chart Entry must leave the Electional view untouched.
    /// </summary>
    [Fact]
    public void Filters_ChartEntryNarrowed_LeavesElectionalUnfiltered()
    {
        MainViewModel vm = CreateViewModel();
        AssertIsMeaningfulPartition(vm, PartitioningFilter);

        vm.ChartEntry.TimeZone = PartitioningFilter;

        VisibleChartEntry(vm).Should().BeEquivalentTo(Expected(vm, PartitioningFilter));
        VisibleElectional(vm).Should().BeEquivalentTo(vm.AvailableTimezones,
            "the Electional filter has its own state and was never touched");
    }

    /// <summary>
    /// The mirror case: narrowing Electional must leave Chart Entry untouched.
    /// </summary>
    [Fact]
    public void Filters_ElectionalNarrowed_LeavesChartEntryUnfiltered()
    {
        MainViewModel vm = CreateViewModel();
        AssertIsMeaningfulPartition(vm, PartitioningFilter);

        vm.Electional.TimeZone = PartitioningFilter;

        VisibleElectional(vm).Should().BeEquivalentTo(Expected(vm, PartitioningFilter));
        VisibleChartEntry(vm).Should().BeEquivalentTo(vm.AvailableTimezones,
            "the Chart Entry filter has its own state and was never touched");
    }

    /// <summary>
    /// Both filters may be active simultaneously with different text, each
    /// producing its own result over the shared source list.
    /// </summary>
    [Fact]
    public void Filters_BothNarrowedDifferently_ProduceIndependentResults()
    {
        MainViewModel vm = CreateViewModel();
        const string other = "America/";
        AssertIsMeaningfulPartition(vm, PartitioningFilter);
        AssertIsMeaningfulPartition(vm, other);

        vm.ChartEntry.TimeZone = PartitioningFilter;
        vm.Electional.TimeZone = other;

        VisibleChartEntry(vm).Should().BeEquivalentTo(Expected(vm, PartitioningFilter));
        VisibleElectional(vm).Should().BeEquivalentTo(Expected(vm, other));
    }

    /// <summary>
    /// Clearing one panel must not disturb the other panel-s filter.
    /// </summary>
    [Fact]
    public void Filters_ElectionalClear_LeavesChartEntryFilterIntact()
    {
        MainViewModel vm = CreateViewModel();
        AssertIsMeaningfulPartition(vm, PartitioningFilter);

        vm.ChartEntry.TimeZone = PartitioningFilter;
        vm.Electional.TimeZone = "America/";
        vm.Electional.Clear();

        VisibleElectional(vm).Should().BeEquivalentTo(vm.AvailableTimezones);
        VisibleChartEntry(vm).Should().BeEquivalentTo(Expected(vm, PartitioningFilter),
            "clearing the Electional panel must not clear the Chart Entry filter");
    }

    #endregion
}
