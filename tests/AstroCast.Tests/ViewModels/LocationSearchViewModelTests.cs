using System.ComponentModel;
using System.Net.Http;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="LocationSearchViewModel"/> — the reusable
/// child view model introduced in Session H-1.3 that consolidates the
/// four previously-duplicated Nominatim location-search blocks from
/// <see cref="MainViewModel"/>. Covers setter plumbing (PropertyChanged
/// raised for <see cref="LocationSearchViewModel.SearchText"/> and
/// <see cref="LocationSearchViewModel.SelectedResult"/>), the empty-query
/// short-circuit path inside
/// <see cref="LocationSearchViewModel.SearchAsync"/>, the
/// <see cref="LocationSearchViewModel.Clear"/> and
/// <see cref="LocationSearchViewModel.ClearResults"/> semantics.
/// Background-thread mutation safety of the results collection is covered by
/// <see cref="CollectionSynchronizationTests"/>, not here — this suite's
/// earlier claim to that coverage was inaccurate and was corrected during the
/// Phase 3 slice-3d work that added the real tests.
/// </summary>
[Trait("Category", "Unit")]
public sealed class LocationSearchViewModelTests
{
    #region Test Infrastructure

    /// <summary>
    /// Constructs a <see cref="MainViewModel"/> using the same stub set as
    /// <see cref="ChartEntryViewModelTests"/> and returns its Chart Entry
    /// panel child (<see cref="MainViewModel.ChartEntryLocationSearch"/>).
    /// We construct the parent rather than the child in isolation because
    /// <see cref="LocationSearchViewModel"/> derives from
    /// <see cref="ChildViewModelBase"/>, whose constructor requires a real
    /// <see cref="MainViewModel"/> parent, and several methods write into
    /// <see cref="MainViewModel.StatusText"/>.
    /// </summary>
    private static (MainViewModel Parent, LocationSearchViewModel Child) CreateChild()
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        MainViewModel parent = new(
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
        return (parent, parent.ChartEntryLocationSearch);
    }

    #endregion

    #region Setter Plumbing

    /// <summary>
    /// Setting <see cref="LocationSearchViewModel.SearchText"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for
    /// "SearchText" so the bound TextBox two-way updates.
    /// </summary>
    [Fact]
    public void SearchText_SetToNewValue_RaisesPropertyChanged()
    {
        // Arrange
        (_, LocationSearchViewModel child) = CreateChild();
        List<string> changed = [];
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);

        // Act
        child.SearchText = "Richmond, VA";

        // Assert
        changed.Should().Contain("SearchText",
            "the TextBox two-way binding depends on this notification");
        child.SearchText.Should().Be("Richmond, VA");
    }

    /// <summary>
    /// Setting <see cref="LocationSearchViewModel.SelectedResult"/> must
    /// raise <see cref="INotifyPropertyChanged.PropertyChanged"/> for
    /// "SelectedResult" — this is the signal <see cref="MainViewModel"/>
    /// subscribes to so it can run the panel-specific follow-on logic
    /// (writing coordinates into the form, resolving the IANA timezone).
    /// </summary>
    [Fact]
    public void SelectedResult_SetToNewValue_RaisesPropertyChanged()
    {
        // Arrange
        (_, LocationSearchViewModel child) = CreateChild();
        List<string> changed = [];
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        LocationSearchResult result = new()
        {
            DisplayName = "Richmond, VA",
            Latitude = 37.5407,
            Longitude = -77.4360,
        };

        // Act
        child.SelectedResult = result;

        // Assert
        changed.Should().Contain("SelectedResult",
            "the parent subscribes to this notification to run follow-on logic");
        child.SelectedResult.Should().Be(result);
    }

    #endregion

    #region SearchAsync Short-Circuit

    /// <summary>
    /// <see cref="LocationSearchViewModel.SearchAsync"/> must short-circuit
    /// when the search text is empty or whitespace: no HTTP call is
    /// issued, the results collection stays empty, and the parent's
    /// status bar shows the prompt message. This guard existed in every
    /// one of the four pre-H-1.3 per-panel methods and must be preserved
    /// verbatim.
    /// </summary>
    [Fact]
    public async Task SearchAsync_WithEmptyQuery_ShortCircuitsAndSetsStatus()
    {
        // Arrange
        (MainViewModel parent, LocationSearchViewModel child) = CreateChild();
        child.SearchText = "   ";

        // Act
        await child.SearchAsync();

        // Assert
        child.Results.Should().BeEmpty(
            "no HTTP call should have been issued for a whitespace query");
        child.IsDropDownOpen.Should().BeFalse(
            "the dropdown only opens after a successful search with results");
        parent.StatusText.Should().Be("Enter a location name to search.",
            "the status bar must prompt the user for valid input");
    }

    #endregion

    #region Clear / ClearResults Semantics

    /// <summary>
    /// <see cref="LocationSearchViewModel.Clear"/> must reset
    /// <see cref="LocationSearchViewModel.Results"/>,
    /// <see cref="LocationSearchViewModel.SelectedResult"/>, and
    /// <see cref="LocationSearchViewModel.SearchText"/> — used by the
    /// parent's <c>ClearForm</c> and <c>LoadChart</c> flows to reset
    /// panel state after loading a saved chart.
    /// </summary>
    [Fact]
    public void Clear_ResetsAllSearchState()
    {
        // Arrange — seed every mutable field with a non-default value
        (_, LocationSearchViewModel child) = CreateChild();
        child.SearchText = "Tokyo";
        child.Results.Add(new LocationSearchResult
        {
            DisplayName = "Tokyo, Japan",
            Latitude = 35.6762,
            Longitude = 139.6503,
        });
        child.SelectedResult = child.Results[0];

        // Act
        child.Clear();

        // Assert
        child.SearchText.Should().BeEmpty(
            "Clear must wipe the text input so the panel returns to its initial state");
        child.Results.Should().BeEmpty(
            "Clear must empty the results collection");
        child.SelectedResult.Should().BeNull(
            "Clear must drop the selected result so the ComboBox clears");
    }

    /// <summary>
    /// <see cref="LocationSearchViewModel.ClearResults"/> must reset
    /// <see cref="LocationSearchViewModel.Results"/> and
    /// <see cref="LocationSearchViewModel.SelectedResult"/> but leave
    /// <see cref="LocationSearchViewModel.SearchText"/> intact — mirrors
    /// the narrower Electional <c>UseNatalLocation</c> cleanup that
    /// preserved the search text pre-H-1.3.
    /// </summary>
    [Fact]
    public void ClearResults_ClearsResultsAndSelectionButPreservesSearchText()
    {
        // Arrange
        (_, LocationSearchViewModel child) = CreateChild();
        child.SearchText = "Preserved";
        child.Results.Add(new LocationSearchResult
        {
            DisplayName = "Result A",
            Latitude = 0,
            Longitude = 0,
        });
        child.SelectedResult = child.Results[0];

        // Act
        child.ClearResults();

        // Assert
        child.SearchText.Should().Be("Preserved",
            "ClearResults is narrower than Clear — it must not wipe the text input");
        child.Results.Should().BeEmpty();
        child.SelectedResult.Should().BeNull();
    }

    #endregion

    #region Infrastructure Invariants

    /// <summary>
    /// The four location-search instances composed by
    /// <see cref="MainViewModel"/> must be distinct references so per-panel
    /// state never leaks across panels. If the constructor ever got
    /// deduplicated into a single shared instance, every panel would share
    /// results — this test catches that regression. Note that under
    /// Session H-1.6 the Electional instance moved from a parent-owned
    /// sibling (<c>ElectionalLocationSearch</c>) to a nested child
    /// (<c>Electional.LocationSearch</c>), and under Session H-1.9 the
    /// Mundane instance made the same migration (<c>MundaneLocationSearch</c>
    /// to <c>Mundane.LocationSearch</c>); the distinctness invariant must
    /// still hold across both nested compositions.
    /// </summary>
    [Fact]
    public void Parent_ComposesFourDistinctLocationSearchChildren()
    {
        // Arrange
        (MainViewModel parent, _) = CreateChild();

        // Assert — each child must be its own instance
        parent.ChartEntryLocationSearch.Should().NotBeSameAs(parent.Electional.LocationSearch);
        parent.ChartEntryLocationSearch.Should().NotBeSameAs(parent.Mundane.LocationSearch);
        parent.ChartEntryLocationSearch.Should().NotBeSameAs(parent.Relocation.LocationSearch);
        parent.Electional.LocationSearch.Should().NotBeSameAs(parent.Mundane.LocationSearch);
        parent.Electional.LocationSearch.Should().NotBeSameAs(parent.Relocation.LocationSearch);
        parent.Mundane.LocationSearch.Should().NotBeSameAs(parent.Relocation.LocationSearch);

        // And the Mundane panel must have its historical pre-populated default
        parent.Mundane.LocationSearch.SearchText.Should().Be("Washington, DC",
            "the Mundane panel pre-populated its search text pre-H-1.3 and the " +
            "extraction must preserve that default verbatim");
    }

    #endregion
}
