using System.ComponentModel;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="SynastryViewModel"/> — the Synastry &amp;
/// Composite panel child introduced in Session H-1.4. Verifies that the
/// PersonB, SynastryResult, and CompositeResult setters each raise the
/// correct <see cref="INotifyPropertyChanged.PropertyChanged"/> notifications
/// and signal
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
/// parent's Calculate / Export commands re-evaluate <c>CanExecute</c>.
/// Also verifies the <see cref="SynastryViewModel.Clear"/> field-bypass
/// reset and the derived <c>HasSynastryResult</c> /
/// <c>HasCompositeResult</c> predicates.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SynastryViewModelTests
{
    #region Test Infrastructure

    /// <summary>
    /// Constructs a <see cref="MainViewModel"/> using the same stub set as
    /// <see cref="MainViewModelTests"/> and returns its
    /// <see cref="MainViewModel.Synastry"/> child. We construct the parent
    /// rather than the child in isolation because the child derives from
    /// <see cref="ChildViewModelBase"/>, whose constructor requires a real
    /// <see cref="MainViewModel"/> parent.
    /// </summary>
    private static (MainViewModel Parent, SynastryViewModel Child) CreateChild()
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
        return (parent, parent.Synastry);
    }

    /// <summary>
    /// Helper that fabricates a <see cref="ChartEntity"/> suitable for the
    /// Person B combobox. Values are arbitrary but valid — tests only care
    /// that the reference round-trips through setters.
    /// </summary>
    private static ChartEntity MakePersonB(string name = "Person B") => new()
    {
        Name = name,
        EventType = "Birth",
        LocalDateTime = "2000-01-01T12:00:00",
        TimeZone = "UTC",
        UtcDateTime = "2000-01-01T12:00:00Z",
        JulianDay = 2451545.0,
        Latitude = 51.5,
        Longitude = -0.1,
        LocationName = "London",
        CreatedAt = DateTime.UtcNow.ToString("o"),
        UpdatedAt = DateTime.UtcNow.ToString("o"),
    };

    /// <summary>
    /// Helper that fabricates a minimally-populated <see cref="ChartResult"/>.
    /// Used to drive the <c>PersonBChart</c>, <c>SynastryResult</c>, and
    /// <c>CompositeResult</c> setters without spinning up the real engine.
    /// </summary>
    private static ChartResult MakeChartResult() => new()
    {
        Input = new ChartInput
        {
            Name = "Stub",
            LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Unspecified),
            TimeZone = "UTC",
            Latitude = 51.5,
            Longitude = -0.1,
            LocationName = "London",
        },
        JulianDay = 2451545.0,
        Obliquity = 23.4367,
        UtcDateTime = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        Positions = [],
        Houses = new HouseData
        {
            Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
            Ascendant = 0.0,
            Midheaven = 270.0,
            Armc = 0.0,
            Vertex = 0.0,
        },
        Aspects = [],
        Midpoints = [],
        MidpointActivations = [],
        Dignities = [],
        MoonCondition = null,
        ChartContext = null,
        FixedStarConjunctions = [],
        ResolvedUtcOffsetHours = 0.0,
        ResolvedOffsetLabel = "UTC",
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    #endregion

    #region Setter Plumbing

    /// <summary>
    /// Setting <see cref="SynastryViewModel.PersonB"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for "PersonB" and
    /// fire <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so
    /// the parent re-evaluates <c>CalculateSynastryCommand</c> and
    /// <c>CalculateCompositeCommand</c> — both gate on
    /// <c>HasResult &amp;&amp; PersonB is not null</c>.
    /// </summary>
    [Fact]
    public void PersonB_SetToNewValue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, SynastryViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.PersonB = MakePersonB();

        // Assert
        changed.Should().Contain("PersonB",
            "the setter must notify bindings that PersonB changed");
        refreshCount.Should().Be(1,
            "PersonB gates CalculateSynastry / CalculateComposite, so the " +
            "setter must signal the parent once");
    }

    /// <summary>
    /// Setting <see cref="SynastryViewModel.SynastryResult"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for both
    /// "SynastryResult" and the derived "HasSynastryResult", and fire
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent's <c>ExportSynastryMarkdownCommand</c> re-evaluates.
    /// </summary>
    [Fact]
    public void SynastryResult_SetToNewValue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, SynastryViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        SynastryResult result = new()
        {
            PersonA = MakeChartResult(),
            PersonB = MakeChartResult(),
            RelationshipType = RelationshipType.Unspecified,
            RelationshipNotes = null,
            InterChartAspects = [],
            PersonAInPersonBHouses = [],
            PersonBInPersonAHouses = [],
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };

        // Act
        child.SynastryResult = result;

        // Assert
        changed.Should().Contain("SynastryResult",
            "bindings on SynastryResult must update");
        changed.Should().Contain("HasSynastryResult",
            "HasSynastryResult is derived from SynastryResult and must notify");
        refreshCount.Should().Be(1,
            "ExportSynastryMarkdown depends on HasSynastryResult");
        child.HasSynastryResult.Should().BeTrue();
    }

    /// <summary>
    /// Setting <see cref="SynastryViewModel.CompositeResult"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for both
    /// "CompositeResult" and the derived "HasCompositeResult", and fire
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent's <c>ExportCompositeMarkdownCommand</c> re-evaluates.
    /// </summary>
    [Fact]
    public void CompositeResult_SetToNewValue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, SynastryViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        ChartResult chart = MakeChartResult();
        CompositeResult result = new()
        {
            ChartA = chart,
            ChartB = chart,
            CompositeChart = chart,
            ReferenceLatitude = 0.0,
            ReferenceLongitude = 0.0,
            ReferenceLocationDescription = "Test",
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };

        // Act
        child.CompositeResult = result;

        // Assert
        changed.Should().Contain("CompositeResult",
            "bindings on CompositeResult must update");
        changed.Should().Contain("HasCompositeResult",
            "HasCompositeResult is derived from CompositeResult and must notify");
        refreshCount.Should().Be(1,
            "ExportCompositeMarkdown depends on HasCompositeResult");
        child.HasCompositeResult.Should().BeTrue();
    }

    /// <summary>
    /// Setting <see cref="SynastryViewModel.PersonBChart"/> must raise
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> for "PersonBChart"
    /// but must NOT fire
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> — no
    /// command's <c>CanExecute</c> depends on this cache.
    /// </summary>
    [Fact]
    public void PersonBChart_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, SynastryViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.PersonBChart = MakeChartResult();

        // Assert
        changed.Should().Contain("PersonBChart");
        refreshCount.Should().Be(0,
            "PersonBChart is an output-only cache — no command gates on it");
    }

    #endregion

    #region Clear Semantics

    /// <summary>
    /// <see cref="SynastryViewModel.Clear"/> must reset every mutable field
    /// (PersonB, PersonBChart, SynastryResult, CompositeResult, relationship
    /// metadata, composite context) and emit exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> via the
    /// field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c>. If Clear used setters each
    /// reset would cascade its own refresh, which would double-pump the
    /// parent's command-state refreshers.
    /// </summary>
    [Fact]
    public void Clear_ResetsAllFieldsAndFiresRefreshOnce()
    {
        // Arrange — seed every mutable field with a non-default value
        (_, SynastryViewModel child) = CreateChild();
        child.PersonB = MakePersonB();
        child.PersonBChart = MakeChartResult();
        child.SynastryResult = new SynastryResult
        {
            PersonA = MakeChartResult(),
            PersonB = MakeChartResult(),
            RelationshipType = RelationshipType.Romantic,
            RelationshipNotes = "dating 3 years",
            InterChartAspects = [],
            PersonAInPersonBHouses = [],
            PersonBInPersonAHouses = [],
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };
        ChartResult chart = MakeChartResult();
        child.CompositeResult = new CompositeResult
        {
            ChartA = chart,
            ChartB = chart,
            CompositeChart = chart,
            ReferenceLatitude = 0.0,
            ReferenceLongitude = 0.0,
            ReferenceLocationDescription = "Test",
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };
        child.SelectedRelationshipType = RelationshipType.Romantic;
        child.RelationshipNotes = "keep me";
        child.CompositeInterpretationContext = "keep me too";
        child.DavisonResult = MakeDavisonResult(chart);
        child.DavisonInterpretationContext = "keep me three";

        int refreshCount = 0;
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.Clear();

        // Assert — state
        child.PersonB.Should().BeNull(
            "Clear must drop the selected Person B");
        child.PersonBChart.Should().BeNull(
            "Clear must drop the cached Person B chart");
        child.SynastryResult.Should().BeNull();
        child.HasSynastryResult.Should().BeFalse();
        child.CompositeResult.Should().BeNull();
        child.HasCompositeResult.Should().BeFalse();
        child.SelectedRelationshipType.Should().Be(RelationshipType.Unspecified,
            "Clear must restore the relationship type to its default");
        child.RelationshipNotes.Should().BeEmpty();
        child.CompositeInterpretationContext.Should().BeEmpty();
        child.DavisonResult.Should().BeNull(
            "Clear must drop the Davison result");
        child.HasDavisonResult.Should().BeFalse();
        child.DavisonInterpretationContext.Should().BeEmpty(
            "Clear must reset the Davison context");

        // Assert — single refresh pulse (field-bypass pattern)
        refreshCount.Should().Be(1,
            "Clear uses the field-bypass pattern so exactly one refresh fires " +
            "at the end rather than cascading through each setter");
    }

    #endregion

    #region Composition Invariants

    /// <summary>
    /// <see cref="MainViewModel.Synastry"/> must be a concrete instance of
    /// <see cref="SynastryViewModel"/> and must be the same reference each
    /// time it is accessed — composition is eager and the property getter
    /// must not construct a fresh child per call.
    /// </summary>
    [Fact]
    public void Parent_ExposesStableSynastryChild()
    {
        // Arrange
        (MainViewModel parent, SynastryViewModel child) = CreateChild();

        // Assert
        child.Should().NotBeNull();
        parent.Synastry.Should().BeSameAs(child,
            "Synastry is an eagerly-composed child; the getter must not " +
            "produce a new instance on every access");
        parent.Synastry.RelationshipTypes.Should()
            .Contain(RelationshipType.Unspecified,
                "RelationshipTypes is the enum list bound to the Relationship " +
                "ComboBox and must include Unspecified so users can clear the " +
                "selection");
    }

    #endregion

    /// <summary>
    /// Setting <see cref="SynastryViewModel.DavisonResult"/> must raise <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// for both "DavisonResult" and the derived "HasDavisonResult", and fire
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the parent's
    /// <c>ExportDavisonMarkdownCommand</c> re-evaluates (branch DavisonPanel, 1.7.0; the Composite's twin).
    /// </summary>
    [Fact]
    public void DavisonResult_SetToNewValue_RaisesPropertyChangedAndRefreshRequest()
    {
        (_, SynastryViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName ?? string.Empty);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.DavisonResult = MakeDavisonResult(MakeChartResult());

        changed.Should().Contain("DavisonResult");
        changed.Should().Contain("HasDavisonResult", "HasDavisonResult is derived from DavisonResult and must notify");
        refreshCount.Should().Be(1, "ExportDavisonMarkdown depends on HasDavisonResult");
        child.HasDavisonResult.Should().BeTrue();
    }

    /// <summary>A Davison result over one chart as both sources and the chart, for the setter and Clear tests.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The result.</returns>
    private static DavisonResult MakeDavisonResult(ChartResult chart) => new()
    {
        ChartA = chart,
        ChartB = chart,
        DavisonChart = chart,
        MidpointUtc = new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc),
        MidpointJulianDay = 2451545.0,
        MidpointLatitude = 40.7128,
        MidpointLongitude = -74.006,
        MidpointLocationDescription = "Midpoint of Test & Test",
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };
}
