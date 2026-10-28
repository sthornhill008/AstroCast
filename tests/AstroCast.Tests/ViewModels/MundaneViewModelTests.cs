using System.ComponentModel;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="MundaneViewModel"/> \u2014 the ninth H-1
/// decomposition child (Session H-1.9) that owns the Mundane panel: shared
/// mundane location plus the four independent sub-features (Ingress Charts,
/// Lunation Charts, Mundane Aspect Finder, Eclipse Calculator). Verifies the
/// pre-populated "Washington, DC" default survives the extraction, that the
/// nested <see cref="LocationSearchViewModel"/> (second application of the
/// H-1.6 migration) seeds the same default, that every input setter raises
/// <see cref="INotifyPropertyChanged.PropertyChanged"/> without signalling
/// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>, that the
/// five result setters preserve the pre-extraction "empty-scan zeroes the
/// flag" semantics (all five are value-based per H-1.8 \u2014 the
/// <c>HasXxx</c> flag stays false until the parent command writes
/// <c>series.Count &gt; 0</c> explicitly), that the migrated
/// <c>OnLocationSearchPropertyChanged</c> handler copies coordinates and
/// timezone from a selected result, that <see cref="MundaneViewModel.Clear"/>
/// uses the field-bypass pattern to emit exactly one refresh pulse, and that
/// <see cref="MainViewModel.Mundane"/> is a stable eagerly-composed child.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MundaneViewModelTests
{
    #region Test Infrastructure

    /// <summary>
    /// Constructs a <see cref="MainViewModel"/> using the same stub set as
    /// <see cref="MainViewModelTests"/> and returns its
    /// <see cref="MainViewModel.Mundane"/> child. We construct the parent
    /// rather than the child in isolation because the child derives from
    /// <see cref="ChildViewModelBase"/>, whose constructor requires a real
    /// <see cref="MainViewModel"/> parent, and because the migrated
    /// <c>OnLocationSearchPropertyChanged</c> handler writes into
    /// <see cref="MainViewModel.StatusText"/> (parent-owned rule #2).
    /// </summary>
    private static (MainViewModel Parent, MundaneViewModel Child) CreateChild()
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
        return (parent, parent.Mundane);
    }

    /// <summary>
    /// Helper that fabricates a minimally-populated
    /// <see cref="IngressSeries"/> with the specified count. A series with
    /// zero ingresses models the pre-extraction "empty scan" case.
    /// </summary>
    private static IngressSeries MakeIngressSeries(int count) => new()
    {
        Planet = CelestialBodyId.Sun,
        PlanetName = "Sun",
        Ingresses = [],
        ScanStart = new DateTime(2025, 1, 1),
        ScanEnd = new DateTime(2025, 12, 31),
        Count = count,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// Helper that fabricates an empty cardinal-ingress chart batch. The VM
    /// only cares that the reference round-trips \u2014 content is irrelevant.
    /// </summary>
    private static IReadOnlyList<IngressResult> MakeCardinalIngresses() => [];

    /// <summary>
    /// Helper that fabricates a minimally-populated
    /// <see cref="LunationSeries"/> with the specified count.
    /// </summary>
    private static LunationSeries MakeLunationSeries(int count) => new()
    {
        Lunations = [],
        ScanStart = new DateTime(2025, 1, 1),
        ScanEnd = new DateTime(2025, 12, 31),
        Count = count,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// Helper that fabricates a minimally-populated
    /// <see cref="MundaneAspectSeries"/> with the specified count.
    /// </summary>
    private static MundaneAspectSeries MakeMASeries(int count) => new()
    {
        Body1 = CelestialBodyId.Saturn,
        Body1Name = "Saturn",
        Body2 = CelestialBodyId.Mars,
        Body2Name = "Mars",
        HarmonicMode = "90\u00b0 Harmonic",
        Aspects = [],
        ScanStart = new DateTime(2025, 1, 1),
        ScanEnd = new DateTime(2035, 12, 31),
        Count = count,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <summary>
    /// Helper that fabricates a minimally-populated
    /// <see cref="EclipseSeries"/> with the specified count.
    /// </summary>
    private static EclipseSeries MakeEclipseSeries(int count) => new()
    {
        Eclipses = [],
        ScanStart = new DateTime(2025, 1, 1),
        ScanEnd = new DateTime(2027, 12, 31),
        Count = count,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    #endregion

    #region Defaults \u2014 Mundane Location

    /// <summary>
    /// The mundane location fields must preserve the pre-H-1.3 defaults
    /// verbatim: "Washington, DC" at its historical coordinates with the
    /// America/New_York timezone. The nested <see cref="LocationSearchViewModel"/>
    /// must also be seeded with "Washington, DC" because the Mundane panel has
    /// always pre-populated that search box.
    /// </summary>
    [Fact]
    public void MundaneLocation_Should_DefaultToWashingtonDC()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        child.LocationName.Should().Be("Washington, DC");
        child.LatitudeText.Should().Be("38.8977");
        child.LongitudeText.Should().Be("-77.0365");
        child.TimeZone.Should().Be("America/New_York");
        child.LocationSearch.SearchText.Should().Be("Washington, DC",
            "the nested LocationSearch must carry the historical default " +
            "through the H-1.9 extraction");
    }

    #endregion

    #region Defaults \u2014 Ingress Charts

    /// <summary>
    /// <see cref="MundaneViewModel.IngressPlanet"/> defaults to the Sun and
    /// <see cref="MundaneViewModel.CardinalOnly"/> defaults to
    /// <see langword="true"/> \u2014 the pre-extraction initialiser optimised
    /// for the common case of scanning Sun cardinal ingresses.
    /// </summary>
    [Fact]
    public void IngressInputs_Should_DefaultToSunAndCardinalOnly()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        child.IngressPlanet.Should().Be(CelestialBodyId.Sun);
        child.CardinalOnly.Should().BeTrue();
    }

    /// <summary>
    /// <see cref="MundaneViewModel.IngressYear"/> defaults to the current
    /// calendar year (F-GR-2 watch item \u2014
    /// <see cref="DateTime.Today"/> coupling preserved verbatim from the
    /// pre-extraction <see cref="MainViewModel"/> initialiser).
    /// </summary>
    [Fact]
    public void IngressYear_Should_DefaultToCurrentYear()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        child.IngressYear.Should().Be(DateTime.Today.Year,
            "IngressYear carries forward the DateTime.Today coupling tracked " +
            "by Session Golden Refresh watch-item F-GR-2");
    }

    /// <summary>
    /// Both ingress result surfaces start empty: <c>CurrentIngressSeries</c>
    /// and <c>CurrentCardinalIngresses</c> are null, and both
    /// <c>HasXxx</c> flags are false.
    /// </summary>
    [Fact]
    public void IngressResults_Should_DefaultToNullAndFalse()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        child.CurrentIngressSeries.Should().BeNull();
        child.HasIngressSeries.Should().BeFalse();
        child.CurrentCardinalIngresses.Should().BeNull();
        child.HasCardinalIngresses.Should().BeFalse();
    }

    /// <summary>
    /// <see cref="MundaneViewModel.AvailableIngressPlanets"/> lists the nine
    /// traditional ingress bodies (Sun plus Mercury through Pluto). The Moon,
    /// True Node, and Chiron are omitted because sign-ingress analysis is
    /// defined for slow-enough-to-be-mundane bodies only \u2014 this mirrors
    /// the pre-extraction <see cref="MainViewModel"/> initialiser order.
    /// </summary>
    [Fact]
    public void AvailableIngressPlanets_Should_ListNineBodiesInOrder()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        MundaneViewModel.AvailableIngressPlanets.Should().HaveCount(9);
        MundaneViewModel.AvailableIngressPlanets.Should().ContainInOrder(
            CelestialBodyId.Sun,
            CelestialBodyId.Mercury,
            CelestialBodyId.Venus,
            CelestialBodyId.Mars,
            CelestialBodyId.Jupiter,
            CelestialBodyId.Saturn,
            CelestialBodyId.Uranus,
            CelestialBodyId.Neptune,
            CelestialBodyId.Pluto);
    }

    #endregion

    #region Defaults \u2014 Lunation Charts

    /// <summary>
    /// <see cref="MundaneViewModel.LunationYear"/> defaults to the current
    /// calendar year (F-GR-2 watch item).
    /// </summary>
    [Fact]
    public void LunationYear_Should_DefaultToCurrentYear()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        child.LunationYear.Should().Be(DateTime.Today.Year);
    }

    /// <summary>
    /// Lunation result surfaces start empty.
    /// </summary>
    [Fact]
    public void LunationResults_Should_DefaultToNullAndFalse()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        child.CurrentLunationSeries.Should().BeNull();
        child.HasLunationSeries.Should().BeFalse();
    }

    #endregion

    #region Defaults \u2014 Mundane Aspect Finder

    /// <summary>
    /// <see cref="MundaneViewModel.MABody1"/> defaults to Saturn and
    /// <see cref="MundaneViewModel.MABody2"/> defaults to Mars. The
    /// harmonic mode defaults to the 90\u00b0 harmonic (hard aspects only).
    /// </summary>
    [Fact]
    public void MAInputs_Should_DefaultToSaturnMarsAnd90Harmonic()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        child.MABody1.Should().Be(CelestialBodyId.Saturn);
        child.MABody2.Should().Be(CelestialBodyId.Mars);
        child.MAHarmonicMode.Should().Be("90\u00b0 Harmonic");
    }

    /// <summary>
    /// <see cref="MundaneViewModel.MAStartYear"/> and
    /// <see cref="MundaneViewModel.MAEndYear"/> default to <c>2025</c> and
    /// <c>2035</c>, matching the pre-extraction <see cref="MainViewModel"/>
    /// initialisers verbatim. These are intentionally fixed years (not
    /// <see cref="DateTime.Today"/>-derived) \u2014 no watch item applies.
    /// </summary>
    [Fact]
    public void MAYears_Should_DefaultTo2025And2035()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        child.MAStartYear.Should().Be(2025);
        child.MAEndYear.Should().Be(2035);
    }

    /// <summary>
    /// Mundane aspect result surface starts empty.
    /// </summary>
    [Fact]
    public void MAResults_Should_DefaultToNullAndFalse()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        child.CurrentMASeries.Should().BeNull();
        child.HasMASeries.Should().BeFalse();
    }

    /// <summary>
    /// <see cref="MundaneViewModel.AvailableMundanePlanets"/> lists the eleven
    /// bodies eligible for a mundane aspect search: Sun through Pluto, plus
    /// the Moon and Chiron. The True Node is omitted by design \u2014 the
    /// pre-extraction list excluded it.
    /// </summary>
    [Fact]
    public void AvailableMundanePlanets_Should_ListElevenBodiesInOrder()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        child.AvailableMundanePlanets.Should().HaveCount(11);
        child.AvailableMundanePlanets.Should().ContainInOrder(
            CelestialBodyId.Sun, CelestialBodyId.Moon,
            CelestialBodyId.Mercury, CelestialBodyId.Venus, CelestialBodyId.Mars,
            CelestialBodyId.Jupiter, CelestialBodyId.Saturn,
            CelestialBodyId.Uranus, CelestialBodyId.Neptune, CelestialBodyId.Pluto,
            CelestialBodyId.Chiron);
        child.AvailableMundanePlanets.Should().NotContain(CelestialBodyId.TrueNode,
            "the True Node was intentionally excluded from the pre-extraction list");
    }

    /// <summary>
    /// <see cref="MundaneViewModel.AvailableHarmonicModes"/> exposes exactly
    /// two choices: the 45\u00b0 and 90\u00b0 harmonics used by the mundane
    /// aspect finder.
    /// </summary>
    [Fact]
    public void AvailableHarmonicModes_Should_ListFortyFiveAndNinetyDegree()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        child.AvailableHarmonicModes.Should().HaveCount(2);
        child.AvailableHarmonicModes.Should().ContainInOrder(
            "45\u00b0 Harmonic",
            "90\u00b0 Harmonic");
    }

    #endregion

    #region Defaults \u2014 Eclipse Calculator

    /// <summary>
    /// <see cref="MundaneViewModel.EclipseStartYear"/> and
    /// <see cref="MundaneViewModel.EclipseEndYear"/> default to <c>2025</c>
    /// and <c>2027</c> \u2014 a narrow three-year window matching the
    /// pre-extraction initialiser.
    /// </summary>
    [Fact]
    public void EclipseYears_Should_DefaultTo2025And2027()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        child.EclipseStartYear.Should().Be(2025);
        child.EclipseEndYear.Should().Be(2027);
    }

    /// <summary>
    /// Eclipse result surface starts empty.
    /// </summary>
    [Fact]
    public void EclipseResults_Should_DefaultToNullAndFalse()
    {
        // Arrange / Act
        (_, MundaneViewModel child) = CreateChild();

        // Assert
        child.CurrentEclipseSeries.Should().BeNull();
        child.HasEclipseSeries.Should().BeFalse();
    }

    #endregion

    #region Setter Plumbing \u2014 Mundane Location

    /// <summary>
    /// Setting <see cref="MundaneViewModel.LocationName"/> notifies
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> but does NOT fire
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> \u2014
    /// no command's <c>CanExecute</c> depends on the display name.
    /// </summary>
    [Fact]
    public void LocationName_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.LocationName = "Tokyo, Japan";

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.LocationName));
        refreshCount.Should().Be(0);
        child.LocationName.Should().Be("Tokyo, Japan");
    }

    /// <summary>
    /// <see cref="MundaneViewModel.LatitudeText"/> setter plumbing.
    /// </summary>
    [Fact]
    public void LatitudeText_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.LatitudeText = "35.6762";

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.LatitudeText));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// <see cref="MundaneViewModel.LongitudeText"/> setter plumbing.
    /// </summary>
    [Fact]
    public void LongitudeText_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.LongitudeText = "139.6503";

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.LongitudeText));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// <see cref="MundaneViewModel.TimeZone"/> setter plumbing.
    /// </summary>
    [Fact]
    public void TimeZone_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.TimeZone = "Asia/Tokyo";

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.TimeZone));
        refreshCount.Should().Be(0);
    }

    #endregion

    #region Setter Plumbing \u2014 Ingress / Lunation / MA / Eclipse Inputs

    /// <summary>
    /// <see cref="MundaneViewModel.IngressPlanet"/> setter plumbing.
    /// </summary>
    [Fact]
    public void IngressPlanet_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.IngressPlanet = CelestialBodyId.Jupiter;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.IngressPlanet));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// <see cref="MundaneViewModel.IngressYear"/> setter plumbing.
    /// </summary>
    [Fact]
    public void IngressYear_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.IngressYear = 2050;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.IngressYear));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// <see cref="MundaneViewModel.CardinalOnly"/> setter plumbing.
    /// </summary>
    [Fact]
    public void CardinalOnly_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.CardinalOnly = false;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.CardinalOnly));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// <see cref="MundaneViewModel.LunationYear"/> setter plumbing.
    /// </summary>
    [Fact]
    public void LunationYear_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.LunationYear = 2050;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.LunationYear));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// <see cref="MundaneViewModel.MABody1"/> setter plumbing.
    /// </summary>
    [Fact]
    public void MABody1_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.MABody1 = CelestialBodyId.Jupiter;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.MABody1));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// <see cref="MundaneViewModel.MABody2"/> setter plumbing.
    /// </summary>
    [Fact]
    public void MABody2_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.MABody2 = CelestialBodyId.Uranus;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.MABody2));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// <see cref="MundaneViewModel.MAHarmonicMode"/> setter plumbing.
    /// </summary>
    [Fact]
    public void MAHarmonicMode_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.MAHarmonicMode = "45\u00b0 Harmonic";

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.MAHarmonicMode));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// <see cref="MundaneViewModel.MAStartYear"/> setter plumbing.
    /// </summary>
    [Fact]
    public void MAStartYear_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.MAStartYear = 2000;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.MAStartYear));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// <see cref="MundaneViewModel.MAEndYear"/> setter plumbing.
    /// </summary>
    [Fact]
    public void MAEndYear_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.MAEndYear = 2100;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.MAEndYear));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// <see cref="MundaneViewModel.EclipseStartYear"/> setter plumbing.
    /// </summary>
    [Fact]
    public void EclipseStartYear_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.EclipseStartYear = 2000;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.EclipseStartYear));
        refreshCount.Should().Be(0);
    }

    /// <summary>
    /// <see cref="MundaneViewModel.EclipseEndYear"/> setter plumbing.
    /// </summary>
    [Fact]
    public void EclipseEndYear_SetToNewValue_NotifiesButDoesNotRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.EclipseEndYear = 2100;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.EclipseEndYear));
        refreshCount.Should().Be(0);
    }

    #endregion

    #region Result-setter Plumbing \u2014 value-based HasXxx (H-1.8 pattern)

    /// <summary>
    /// Setting <see cref="MundaneViewModel.CurrentIngressSeries"/> notifies
    /// PropertyChanged for the result but does NOT flip
    /// <see cref="MundaneViewModel.HasIngressSeries"/> and does NOT fire a
    /// refresh request. Preserves the pre-extraction semantics in which the
    /// parent command writes the flag explicitly as
    /// <c>series.Count &gt; 0</c> so an empty scan (non-null series with zero
    /// ingresses) leaves the export gated off.
    /// </summary>
    [Fact]
    public void CurrentIngressSeries_SetToNewValue_NotifiesButDoesNotFlipFlagOrRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.CurrentIngressSeries = MakeIngressSeries(count: 0);

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.CurrentIngressSeries));
        changed.Should().NotContain(nameof(MundaneViewModel.HasIngressSeries),
            "the result setter must not raise HasIngressSeries \u2014 parent " +
            "writes it explicitly so empty scans stay gated off");
        refreshCount.Should().Be(0);
        child.HasIngressSeries.Should().BeFalse();
    }

    /// <summary>
    /// Setting <see cref="MundaneViewModel.HasIngressSeries"/> explicitly
    /// raises PropertyChanged and fires exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> so the
    /// parent re-evaluates the ingress export command state.
    /// </summary>
    [Fact]
    public void HasIngressSeries_SetToTrue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.HasIngressSeries = true;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.HasIngressSeries));
        refreshCount.Should().Be(1,
            "HasIngressSeries gates the export command, so its setter must " +
            "trigger exactly one refresh");
        child.HasIngressSeries.Should().BeTrue();
    }

    /// <summary>
    /// Setting <see cref="MundaneViewModel.CurrentCardinalIngresses"/>
    /// notifies PropertyChanged for the result but does NOT flip
    /// <see cref="MundaneViewModel.HasCardinalIngresses"/> on its own.
    /// </summary>
    [Fact]
    public void CurrentCardinalIngresses_SetToNewValue_NotifiesButDoesNotFlipFlagOrRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.CurrentCardinalIngresses = MakeCardinalIngresses();

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.CurrentCardinalIngresses));
        changed.Should().NotContain(nameof(MundaneViewModel.HasCardinalIngresses));
        refreshCount.Should().Be(0);
        child.HasCardinalIngresses.Should().BeFalse();
    }

    /// <summary>
    /// <see cref="MundaneViewModel.HasCardinalIngresses"/> setter fires
    /// exactly one refresh.
    /// </summary>
    [Fact]
    public void HasCardinalIngresses_SetToTrue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.HasCardinalIngresses = true;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.HasCardinalIngresses));
        refreshCount.Should().Be(1);
        child.HasCardinalIngresses.Should().BeTrue();
    }

    /// <summary>
    /// Setting <see cref="MundaneViewModel.CurrentLunationSeries"/> notifies
    /// PropertyChanged for the result but does NOT flip
    /// <see cref="MundaneViewModel.HasLunationSeries"/> on its own.
    /// </summary>
    [Fact]
    public void CurrentLunationSeries_SetToNewValue_NotifiesButDoesNotFlipFlagOrRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.CurrentLunationSeries = MakeLunationSeries(count: 0);

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.CurrentLunationSeries));
        changed.Should().NotContain(nameof(MundaneViewModel.HasLunationSeries));
        refreshCount.Should().Be(0);
        child.HasLunationSeries.Should().BeFalse();
    }

    /// <summary>
    /// <see cref="MundaneViewModel.HasLunationSeries"/> setter fires exactly
    /// one refresh.
    /// </summary>
    [Fact]
    public void HasLunationSeries_SetToTrue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.HasLunationSeries = true;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.HasLunationSeries));
        refreshCount.Should().Be(1);
        child.HasLunationSeries.Should().BeTrue();
    }

    /// <summary>
    /// Setting <see cref="MundaneViewModel.CurrentMASeries"/> notifies
    /// PropertyChanged for the result but does NOT flip
    /// <see cref="MundaneViewModel.HasMASeries"/> on its own.
    /// </summary>
    [Fact]
    public void CurrentMASeries_SetToNewValue_NotifiesButDoesNotFlipFlagOrRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.CurrentMASeries = MakeMASeries(count: 0);

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.CurrentMASeries));
        changed.Should().NotContain(nameof(MundaneViewModel.HasMASeries));
        refreshCount.Should().Be(0);
        child.HasMASeries.Should().BeFalse();
    }

    /// <summary>
    /// <see cref="MundaneViewModel.HasMASeries"/> setter fires exactly one
    /// refresh.
    /// </summary>
    [Fact]
    public void HasMASeries_SetToTrue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.HasMASeries = true;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.HasMASeries));
        refreshCount.Should().Be(1);
        child.HasMASeries.Should().BeTrue();
    }

    /// <summary>
    /// Setting <see cref="MundaneViewModel.CurrentEclipseSeries"/> notifies
    /// PropertyChanged for the result but does NOT flip
    /// <see cref="MundaneViewModel.HasEclipseSeries"/> on its own.
    /// </summary>
    [Fact]
    public void CurrentEclipseSeries_SetToNewValue_NotifiesButDoesNotFlipFlagOrRefreshCommands()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.CurrentEclipseSeries = MakeEclipseSeries(count: 0);

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.CurrentEclipseSeries));
        changed.Should().NotContain(nameof(MundaneViewModel.HasEclipseSeries));
        refreshCount.Should().Be(0);
        child.HasEclipseSeries.Should().BeFalse();
    }

    /// <summary>
    /// <see cref="MundaneViewModel.HasEclipseSeries"/> setter fires exactly
    /// one refresh.
    /// </summary>
    [Fact]
    public void HasEclipseSeries_SetToTrue_RaisesPropertyChangedAndRefreshRequest()
    {
        // Arrange
        (_, MundaneViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.HasEclipseSeries = true;

        // Assert
        changed.Should().Contain(nameof(MundaneViewModel.HasEclipseSeries));
        refreshCount.Should().Be(1);
        child.HasEclipseSeries.Should().BeTrue();
    }

    #endregion

    #region Nested LocationSearch Follow-on Handler (migrated from parent per Rule #4 refinement)

    /// <summary>
    /// When the user picks a <see cref="LocationSearchViewModel.SelectedResult"/>
    /// on the nested mundane location search, the migrated
    /// <c>OnLocationSearchPropertyChanged</c> handler must copy the
    /// coordinates into <see cref="MundaneViewModel.LatitudeText"/> and
    /// <see cref="MundaneViewModel.LongitudeText"/>, write the display name
    /// into <see cref="MundaneViewModel.LocationName"/>, resolve the IANA
    /// timezone for the picked lat/lon into
    /// <see cref="MundaneViewModel.TimeZone"/>, and update
    /// <see cref="MainViewModel.StatusText"/>. This is the identical handler
    /// body that used to live on the parent as
    /// <c>OnMundaneLocationSearchPropertyChanged</c>; H-1.9 moved it into
    /// this child because every write now targets <c>this.*</c> under the
    /// same umbrella (Rule #4 refinement).
    /// </summary>
    [Fact]
    public void LocationSearch_SelectedResultChanged_CopiesIntoMundaneFieldsAndStatus()
    {
        // Arrange \u2014 Tokyo coordinates so the GeoTimeZone lookup produces
        // a distinctly different timezone from the Washington, DC default.
        (MainViewModel parent, MundaneViewModel child) = CreateChild();
        LocationSearchResult tokyo = new()
        {
            DisplayName = "Tokyo, Japan",
            Latitude = 35.6762,
            Longitude = 139.6503,
        };

        // Act
        child.LocationSearch.SelectedResult = tokyo;

        // Assert
        child.LatitudeText.Should().Be("35.6762",
            "the migrated handler must copy latitude into the Mundane " +
            "LatitudeText field (intra-VM write)");
        child.LongitudeText.Should().Be("139.6503");
        child.LocationName.Should().Be("Tokyo, Japan");
        child.TimeZone.Should().Be("Asia/Tokyo",
            "GeoTimeZone must resolve Tokyo coordinates to the Asia/Tokyo " +
            "IANA zone and the handler must write that into TimeZone");
        parent.StatusText.Should().Be("Mundane location set: Tokyo, Japan",
            "status text is a parent-owned concern (Rule #2 last resort) so " +
            "the handler writes through to the parent");
    }

    #endregion

    #region Clear Semantics

    /// <summary>
    /// <see cref="MundaneViewModel.Clear"/> must reset every mutable field
    /// (mundane location, four sub-feature inputs and results, and the nested
    /// location search) and emit exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> via the
    /// field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c>. If Clear used setters each of
    /// the five <c>HasXxx</c> flags would cascade its own refresh \u2014 five
    /// refresh pulses per reset rather than one.
    /// </summary>
    [Fact]
    public void Clear_ResetsAllFieldsAndFiresRefreshOnce()
    {
        // Arrange \u2014 seed every mutable field with a non-default value
        (_, MundaneViewModel child) = CreateChild();

        // Mundane Location
        child.LocationName = "Tokyo, Japan";
        child.LatitudeText = "35.6762";
        child.LongitudeText = "139.6503";
        child.TimeZone = "Asia/Tokyo";

        // Ingress Charts
        child.IngressPlanet = CelestialBodyId.Jupiter;
        child.IngressYear = 2050;
        child.CardinalOnly = false;
        child.CurrentIngressSeries = MakeIngressSeries(count: 3);
        child.CurrentCardinalIngresses = MakeCardinalIngresses();
        child.HasIngressSeries = true;
        child.HasCardinalIngresses = true;

        // Lunation Charts
        child.LunationYear = 2050;
        child.CurrentLunationSeries = MakeLunationSeries(count: 12);
        child.HasLunationSeries = true;

        // Mundane Aspect Finder
        child.MABody1 = CelestialBodyId.Jupiter;
        child.MABody2 = CelestialBodyId.Uranus;
        child.MAHarmonicMode = "45\u00b0 Harmonic";
        child.MAStartYear = 2000;
        child.MAEndYear = 2100;
        child.CurrentMASeries = MakeMASeries(count: 7);
        child.HasMASeries = true;

        // Eclipse Calculator
        child.EclipseStartYear = 2000;
        child.EclipseEndYear = 2100;
        child.CurrentEclipseSeries = MakeEclipseSeries(count: 4);
        child.HasEclipseSeries = true;

        // Nested LocationSearch
        child.LocationSearch.SearchText = "Tokyo";

        int refreshCount = 0;
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        // Act
        child.Clear();

        // Assert \u2014 Mundane Location restored
        child.LocationName.Should().Be("Washington, DC");
        child.LatitudeText.Should().Be("38.8977");
        child.LongitudeText.Should().Be("-77.0365");
        child.TimeZone.Should().Be("America/New_York");

        // Assert \u2014 Ingress Charts restored
        child.IngressPlanet.Should().Be(CelestialBodyId.Sun);
        child.IngressYear.Should().Be(DateTime.Today.Year,
            "Clear must re-read DateTime.Today so a user who holds the app " +
            "open across midnight sees the year roll forward (F-GR-2)");
        child.CardinalOnly.Should().BeTrue();
        child.CurrentIngressSeries.Should().BeNull();
        child.CurrentCardinalIngresses.Should().BeNull();
        child.HasIngressSeries.Should().BeFalse();
        child.HasCardinalIngresses.Should().BeFalse();

        // Assert \u2014 Lunation Charts restored
        child.LunationYear.Should().Be(DateTime.Today.Year);
        child.CurrentLunationSeries.Should().BeNull();
        child.HasLunationSeries.Should().BeFalse();

        // Assert \u2014 Mundane Aspect Finder restored
        child.MABody1.Should().Be(CelestialBodyId.Saturn);
        child.MABody2.Should().Be(CelestialBodyId.Mars);
        child.MAHarmonicMode.Should().Be("90\u00b0 Harmonic");
        child.MAStartYear.Should().Be(2025);
        child.MAEndYear.Should().Be(2035);
        child.CurrentMASeries.Should().BeNull();
        child.HasMASeries.Should().BeFalse();

        // Assert \u2014 Eclipse Calculator restored
        child.EclipseStartYear.Should().Be(2025);
        child.EclipseEndYear.Should().Be(2027);
        child.CurrentEclipseSeries.Should().BeNull();
        child.HasEclipseSeries.Should().BeFalse();

        // Assert \u2014 nested LocationSearch cleared (matches H-1.6 precedent)
        child.LocationSearch.SearchText.Should().BeEmpty(
            "Clear on the parent must propagate to Clear on the nested " +
            "LocationSearch, matching the H-1.6 Electional precedent");

        // Assert \u2014 single refresh pulse (field-bypass pattern)
        refreshCount.Should().Be(1,
            "Clear uses the field-bypass pattern so exactly one refresh " +
            "fires at the end rather than cascading through each setter");
    }

    #endregion

    #region Composition Invariants

    /// <summary>
    /// <see cref="MainViewModel.Mundane"/> must be a concrete instance of
    /// <see cref="MundaneViewModel"/> and must be the same reference each
    /// time it is accessed \u2014 composition is eager and the property
    /// getter must not construct a fresh child per call.
    /// </summary>
    [Fact]
    public void Parent_ExposesStableMundaneChild()
    {
        // Arrange
        (MainViewModel parent, MundaneViewModel child) = CreateChild();

        // Assert
        child.Should().NotBeNull();
        parent.Mundane.Should().BeSameAs(child,
            "Mundane is an eagerly-composed child; the getter must not " +
            "produce a new instance on every access");
    }

    #endregion
}
