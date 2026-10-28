using System.ComponentModel;
using System.Globalization;
using System.Threading;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;

using AstroCast.Services;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Unit tests for <see cref="RelocationViewModel"/> \u2014 the eleventh H-1
/// decomposition child (Session H-1.11) that owns the Relocation panel. This
/// extraction is also the third and final application of the H-1.6
/// nested-<see cref="LocationSearchViewModel"/> migration play (after H-1.6
/// Electional and H-1.9 Mundane) and closes the F-CS-Relocation watch item
/// by threading <see cref="CultureInfo.InvariantCulture"/> through both
/// coordinate-format call sites in the migrated follow-on handler. Tests
/// cover defaults, setter plumbing (raise <c>PropertyChanged</c>, gate
/// command-refresh correctly for each property), the derived
/// <see cref="RelocationViewModel.HasResult"/> pattern (H-1.7 shape),
/// the migrated <c>OnLocationSearchPropertyChanged</c> handler (including
/// a de-DE regression test for F-CS-Relocation), the field-bypass
/// <see cref="RelocationViewModel.Clear"/> pattern, and stable composition
/// under <see cref="MainViewModel.Relocation"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class RelocationViewModelTests
{
    #region Test Infrastructure

    /// <summary>
    /// Constructs a <see cref="MainViewModel"/> using the same stub set as
    /// <see cref="MainViewModelTests"/> and returns its
    /// <see cref="MainViewModel.Relocation"/> child. We construct the parent
    /// rather than the child in isolation because the child derives from
    /// <see cref="ChildViewModelBase"/>, whose constructor requires a real
    /// <see cref="MainViewModel"/> parent.
    /// </summary>
    private static (MainViewModel Parent, RelocationViewModel Child) CreateChild()
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
        return (parent, parent.Relocation);
    }

    /// <summary>
    /// Constructs a minimal <see cref="ChartEntity"/> fixture for testing
    /// <see cref="RelocationViewModel.SelectedNatalChart"/> assignment. The
    /// VM only reads the reference \u2014 contents are irrelevant.
    /// </summary>
    private static ChartEntity MakeChartEntity() => new()
    {
        Id = 42,
        Name = "Test Native",
        EventType = "Birth",
        LocalDateTime = "2000-01-01T12:00:00",
        TimeZone = "UTC",
        UtcDateTime = "2000-01-01T12:00:00Z",
        JulianDay = 2451545.0,
        Latitude = 40.7128,
        Longitude = -74.006,
        LocationName = "New York City",
        CreatedAt = DateTime.UtcNow.ToString("o"),
        UpdatedAt = DateTime.UtcNow.ToString("o"),
    };

    /// <summary>
    /// Fabricates a minimally-populated <see cref="RelocationResult"/>. The
    /// VM only cares that the reference round-trips through
    /// <see cref="RelocationViewModel.CurrentResult"/> \u2014 the inner
    /// chart bodies are intentionally empty because the setter does not
    /// traverse them.
    /// </summary>
    private static RelocationResult MakeResult()
    {
        ChartInput input = new()
        {
            Name = "Test Native",
            LocalDateTime = new DateTime(2000, 1, 1, 12, 0, 0),
            TimeZone = "UTC",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
        };
        ChartResult chart = new()
        {
            Input = input,
            JulianDay = 2451545.0,
            Obliquity = 23.4367,
            UtcDateTime = DateTime.UtcNow,
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
            HouseSystemName = "Placidus",
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };
        return new RelocationResult
        {
            NatalChart = chart,
            RelocatedChart = chart,
            RelocationTarget = new RelocationInput
            {
                Latitude = 51.5074,
                Longitude = -0.1278,
                LocationName = "London, UK",
            },
            HouseChanges = [],
            NatalAscendantSign = ZodiacSign.Aries,
            RelocatedAscendantSign = ZodiacSign.Aries,
            NatalMidheavenSign = ZodiacSign.Capricorn,
            RelocatedMidheavenSign = ZodiacSign.Capricorn,
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };
    }

    #endregion

    #region Defaults

    /// <summary>
    /// Every mutable field defaults to its pre-extraction value: inputs are
    /// empty, the selected natal chart is null, the result is null, and the
    /// derived <see cref="RelocationViewModel.HasResult"/> is false. The
    /// nested <see cref="RelocationViewModel.LocationSearch"/> must also
    /// default to an empty search text because the Relocation panel had no
    /// historical pre-populated default (unlike Mundane's "Washington, DC").
    /// </summary>
    [Fact]
    public void Defaults_MatchPreExtractionState()
    {
        (_, RelocationViewModel child) = CreateChild();

        child.SelectedNatalChart.Should().BeNull();
        child.LocationName.Should().BeEmpty();
        child.LatitudeText.Should().BeEmpty();
        child.LongitudeText.Should().BeEmpty();
        child.CurrentResult.Should().BeNull();
        child.HasResult.Should().BeFalse();
        child.LocationSearch.SearchText.Should().BeEmpty(
            "the Relocation panel had no historical pre-populated search " +
            "default \u2014 unlike Mundane's \"Washington, DC\"");
    }

    #endregion

    #region Setter Plumbing \u2014 Inputs

    /// <summary>
    /// Setting <see cref="RelocationViewModel.SelectedNatalChart"/> raises
    /// <see cref="INotifyPropertyChanged.PropertyChanged"/> and fires exactly
    /// one <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>
    /// because the Calculate and UseNatalLocation commands depend on it.
    /// </summary>
    [Fact]
    public void SelectedNatalChart_Set_NotifiesAndRefreshes()
    {
        (_, RelocationViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.SelectedNatalChart = MakeChartEntity();

        changed.Should().Contain(nameof(RelocationViewModel.SelectedNatalChart));
        refreshCount.Should().Be(1);
        child.SelectedNatalChart.Should().NotBeNull();
    }

    /// <summary>
    /// Setting <see cref="RelocationViewModel.LocationName"/> notifies and
    /// fires a refresh because
    /// <c>CalculateRelocationCommand.CanExecute</c> reads its
    /// non-whitespace status.
    /// </summary>
    [Fact]
    public void LocationName_Set_NotifiesAndRefreshes()
    {
        (_, RelocationViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.LocationName = "London, UK";

        changed.Should().Contain(nameof(RelocationViewModel.LocationName));
        refreshCount.Should().Be(1);
        child.LocationName.Should().Be("London, UK");
    }

    /// <summary>
    /// <see cref="RelocationViewModel.LatitudeText"/> notifies and fires one
    /// refresh: since 1.4.0 the text is parsed on every change, and Calculate
    /// Relocation's <c>CanExecute</c> reads
    /// <see cref="RelocationViewModel.HasValidCoordinates"/>.
    /// </summary>
    [Fact]
    public void LatitudeText_Set_NotifiesAndRefreshes()
    {
        (_, RelocationViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.LatitudeText = "51.5074";

        changed.Should().Contain(nameof(RelocationViewModel.LatitudeText));
        refreshCount.Should().Be(1);
    }

    /// <summary>
    /// <see cref="RelocationViewModel.LongitudeText"/> notifies and fires one
    /// refresh, matching <see cref="RelocationViewModel.LatitudeText"/>.
    /// </summary>
    [Fact]
    public void LongitudeText_Set_NotifiesAndRefreshes()
    {
        (_, RelocationViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.LongitudeText = "-0.1278";

        changed.Should().Contain(nameof(RelocationViewModel.LongitudeText));
        refreshCount.Should().Be(1);
    }

    /// <summary>
    /// A latitude out of range sets <see cref="RelocationViewModel.LatitudeError"/>,
    /// turns the box's tooltip into the message and the coordinates invalid, and
    /// requests one refresh; a valid latitude clears it, and a blank one is not an
    /// error but still leaves the coordinates incomplete (1.4.0, port plan §9 item 9).
    /// </summary>
    [Fact]
    public void LatitudeText_SetOutOfRange_SetsTheErrorAndRefreshes()
    {
        (_, RelocationViewModel child) = CreateChild();
        child.LongitudeText = "-0.1278";
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.LatitudeText = "95";

        child.LatitudeError.Should().Be("Latitude must be between -90 and 90 degrees.");
        child.IsLatitudeValid.Should().BeFalse();
        child.LatitudeToolTip.Should().Be(child.LatitudeError);
        child.HasValidCoordinates.Should().BeFalse();
        changed.Should().Contain(
        [
            nameof(RelocationViewModel.LatitudeError),
            nameof(RelocationViewModel.IsLatitudeValid),
            nameof(RelocationViewModel.LatitudeToolTip),
            nameof(RelocationViewModel.HasValidCoordinates),
        ]);
        refreshCount.Should().Be(1);

        child.LatitudeText = "51.5074";

        child.LatitudeError.Should().BeNull();
        child.IsLatitudeValid.Should().BeTrue();
        child.LatitudeToolTip.Should().Be(RelocationViewModel.LatitudeHint);
        child.HasValidCoordinates.Should().BeTrue();

        child.LatitudeText = "  ";

        child.LatitudeError.Should().BeNull("a blank box is not marked as an error");
        child.IsLatitudeValid.Should().BeTrue();
        child.HasValidCoordinates.Should().BeFalse("a blank box still leaves nothing to relocate to");
    }

    #endregion

    #region Setter Plumbing \u2014 Derived HasResult (H-1.7 pattern)

    /// <summary>
    /// Setting <see cref="RelocationViewModel.CurrentResult"/> to a non-null
    /// value raises <c>PropertyChanged</c> for both <c>CurrentResult</c>
    /// and the derived <see cref="RelocationViewModel.HasResult"/>, and
    /// fires exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/>. The
    /// <c>HasResult</c> flag flips true automatically because it is derived
    /// from <c>_currentResult is not null</c> \u2014 no paired
    /// <c>HasRelocationResult = true</c> write is needed from the command
    /// body (unlike the H-1.8/H-1.9/H-1.10 value-based pattern).
    /// </summary>
    [Fact]
    public void CurrentResult_SetToValue_NotifiesBothAndFlagsTrueAndRefreshes()
    {
        (_, RelocationViewModel child) = CreateChild();
        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.CurrentResult = MakeResult();

        changed.Should().Contain(nameof(RelocationViewModel.CurrentResult));
        changed.Should().Contain(nameof(RelocationViewModel.HasResult),
            "HasResult is derived; the setter must raise PropertyChanged for " +
            "it so XAML Visibility bindings re-evaluate");
        refreshCount.Should().Be(1,
            "the setter gates the Export command, so exactly one refresh " +
            "pulse must fire");
        child.HasResult.Should().BeTrue();
    }

    /// <summary>
    /// The result line names the relocated chart, its new place, the Ascendant's change and the count of house
    /// changes, "1 house change" in the singular, and is raised with <see cref="RelocationViewModel.CurrentResult"/>
    /// (1.4.0, item 3, branch Relocate). Until then the result showed only in the status bar.
    /// </summary>
    /// <param name="natal">The natal Ascendant's sign.</param>
    /// <param name="relocated">The relocated Ascendant's sign.</param>
    /// <param name="houseChanges">The number of house changes.</param>
    /// <param name="expected">The line.</param>
    [Theory]
    [InlineData(ZodiacSign.Capricorn, ZodiacSign.Aquarius, 7, "Test Native at London, UK: Ascendant Capricorn → Aquarius, 7 house changes")]
    [InlineData(ZodiacSign.Aries, ZodiacSign.Aries, 1, "Test Native at London, UK: Ascendant unchanged (Aries), 1 house change")]
    public void ResultSummary_NamesTheChartPlaceAndChanges(
        ZodiacSign natal, ZodiacSign relocated, int houseChanges, string expected)
    {
        (_, RelocationViewModel child) = CreateChild();
        List<string?> changed = [];
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
        RelocationResult template = MakeResult();

        child.CurrentResult = new RelocationResult
        {
            NatalChart = template.NatalChart,
            RelocatedChart = template.RelocatedChart,
            RelocationTarget = template.RelocationTarget,
            HouseChanges = Enumerable.Range(0, houseChanges)
                .Select(i => new HouseChange
                {
                    PlanetName = $"Planet {i}",
                    NatalHouse = 1,
                    RelocatedHouse = 2,
                    BecameAngular = false,
                    LeftAngle = false,
                })
                .ToList(),
            NatalAscendantSign = natal,
            RelocatedAscendantSign = relocated,
            NatalMidheavenSign = template.NatalMidheavenSign,
            RelocatedMidheavenSign = template.RelocatedMidheavenSign,
            EngineVersion = template.EngineVersion,
            CalculatedAt = template.CalculatedAt,
        };

        child.ResultSummary.Should().Be(expected);
        changed.Should().Contain(nameof(RelocationViewModel.ResultSummary));
    }

    /// <summary>
    /// Setting <see cref="RelocationViewModel.CurrentResult"/> back to
    /// <see langword="null"/> raises <c>PropertyChanged</c> for both
    /// <c>CurrentResult</c> and <c>HasResult</c> and fires exactly one
    /// refresh, and <see cref="RelocationViewModel.HasResult"/> flips back
    /// to false \u2014 the derived pattern handles both directions
    /// symmetrically.
    /// </summary>
    [Fact]
    public void CurrentResult_SetBackToNull_NotifiesBothAndFlagsFalseAndRefreshes()
    {
        (_, RelocationViewModel child) = CreateChild();
        child.CurrentResult = MakeResult();

        List<string> changed = [];
        int refreshCount = 0;
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName!);
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.CurrentResult = null;

        changed.Should().Contain(nameof(RelocationViewModel.CurrentResult));
        changed.Should().Contain(nameof(RelocationViewModel.HasResult));
        refreshCount.Should().Be(1);
        child.HasResult.Should().BeFalse();
    }

    #endregion

    #region Result Invalidation (branch StaleResult, 1.6.1)

    /// <summary>A second saved chart, with another Id than <see cref="MakeChartEntity"/>'s 42 unless given.</summary>
    /// <param name="id">The chart's Id.</param>
    /// <returns>The chart.</returns>
    private static ChartEntity MakeOtherChartEntity(long id = 7) => new()
    {
        Id = id,
        Name = "Other Native",
        EventType = "Birth",
        LocalDateTime = "1970-06-15T12:00:00",
        TimeZone = "America/Los_Angeles",
        UtcDateTime = "1970-06-15T19:00:00Z",
        JulianDay = 2440753.291667,
        Latitude = 34.0522,
        Longitude = -118.2437,
        LocationName = "Los Angeles",
        CreatedAt = DateTime.UtcNow.ToString("o"),
        UpdatedAt = DateTime.UtcNow.ToString("o"),
    };

    /// <summary>
    /// Picking another chart clears the result: <see cref="RelocationViewModel.CurrentResult"/> null,
    /// <see cref="RelocationViewModel.HasResult"/> false and the result line empty, each raised (branch
    /// StaleResult, 1.6.1). Until 1.6.1 the result stayed, and Export Relocation MD wrote it.
    /// </summary>
    [Fact]
    public void ChangingThePickedChart_ClearsTheResult()
    {
        (_, RelocationViewModel child) = CreateChild();
        child.SelectedNatalChart = MakeChartEntity();
        child.CurrentResult = MakeResult();
        List<string?> changed = [];
        child.PropertyChanged += (_, e) => changed.Add(e.PropertyName);

        child.SelectedNatalChart = MakeOtherChartEntity();

        child.CurrentResult.Should().BeNull();
        child.HasResult.Should().BeFalse();
        child.ResultSummary.Should().BeEmpty();
        changed.Should().Contain(nameof(RelocationViewModel.CurrentResult));
        changed.Should().Contain(nameof(RelocationViewModel.HasResult));
        changed.Should().Contain(nameof(RelocationViewModel.ResultSummary));
    }

    /// <summary>
    /// A saved-charts refresh re-points the picker at a new instance of the same chart, since
    /// <see cref="ChartEntity"/> compares by reference; the result is keyed on the chart's Id and stays.
    /// </summary>
    [Fact]
    public void ARefreshRepointingThePickerAtTheSameChart_KeepsTheResult()
    {
        (_, RelocationViewModel child) = CreateChild();
        child.SelectedNatalChart = MakeOtherChartEntity(3);
        RelocationResult result = MakeResult();
        child.CurrentResult = result;

        child.SelectedNatalChart = MakeOtherChartEntity(3);

        child.CurrentResult.Should().BeSameAs(result);
        child.HasResult.Should().BeTrue();
    }

    /// <summary>A change of the place, in any of its three boxes, clears the result.</summary>
    /// <param name="property">The box changed.</param>
    [Theory]
    [InlineData("LocationName")]
    [InlineData("LatitudeText")]
    [InlineData("LongitudeText")]
    public void ChangingThePlace_ClearsTheResult(string property)
    {
        (_, RelocationViewModel child) = CreateChild();
        child.LocationName = "London, UK";
        child.LatitudeText = "51.5074";
        child.LongitudeText = "-0.1278";
        child.CurrentResult = MakeResult();

        switch (property)
        {
            case "LocationName":
                child.LocationName = "Paris";
                break;
            case "LatitudeText":
                child.LatitudeText = "48.8566";
                break;
            case "LongitudeText":
                child.LongitudeText = "2.3522";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(property), property, null);
        }

        child.HasResult.Should().BeFalse();
        child.ResultSummary.Should().BeEmpty();
    }

    /// <summary>Clearing the result rides on the setter's one refresh: no second pulse.</summary>
    [Fact]
    public void ClearingTheResult_KeepsEachSetterAtOneRefresh()
    {
        (_, RelocationViewModel child) = CreateChild();
        child.CurrentResult = MakeResult();
        int refreshCount = 0;
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.LocationName = "Paris";

        child.HasResult.Should().BeFalse();
        refreshCount.Should().Be(1);
    }

    #endregion

    #region Nested LocationSearch Follow-on Handler (migrated from parent per Rule #4 refinement)

    /// <summary>
    /// When the user picks a
    /// <see cref="LocationSearchViewModel.SelectedResult"/> on the nested
    /// Relocation location search, the migrated
    /// <c>OnLocationSearchPropertyChanged</c> handler must copy the
    /// coordinates into <see cref="RelocationViewModel.LatitudeText"/> and
    /// <see cref="RelocationViewModel.LongitudeText"/> (both formatted with
    /// <see cref="CultureInfo.InvariantCulture"/> \u2014 F-CS-Relocation
    /// closure) and write the display name into
    /// <see cref="RelocationViewModel.LocationName"/>. Unlike the Electional
    /// and Mundane handlers, no IANA timezone resolution runs because the
    /// relocated chart reuses the natal UTC moment at the target coordinates.
    /// This is the identical handler body that used to live on the parent
    /// as <c>OnRelocationLocationSearchPropertyChanged</c>; H-1.11 moved it
    /// into this child because every write now targets <c>this.*</c> under
    /// the same umbrella (Rule #4 refinement).
    /// </summary>
    [Fact]
    public void LocationSearch_SelectedResultChanged_CopiesCoordinatesAndNameInvariant()
    {
        (_, RelocationViewModel child) = CreateChild();
        LocationSearchResult london = new()
        {
            DisplayName = "London, UK",
            Latitude = 51.5074,
            Longitude = -0.1278,
        };

        child.LocationSearch.SelectedResult = london;

        child.LocationName.Should().Be("London, UK");
        child.LatitudeText.Should().Be("51.5074",
            "the migrated handler must format latitude under InvariantCulture " +
            "so the decimal separator stays '.' regardless of the host's " +
            "current culture");
        child.LongitudeText.Should().Be("-0.1278",
            "the migrated handler must format longitude under InvariantCulture");
    }

    /// <summary>
    /// F-CS-Relocation regression test. Before H-1.11 the handler used
    /// <c>value.Latitude.ToString("F4")</c> without an
    /// <see cref="IFormatProvider"/>, which emitted <c>"34,0522"</c> on a
    /// <c>de-DE</c> host. The matching <c>double.TryParse</c> on the parent
    /// also lacked a culture argument so the round-trip fell apart for any
    /// non-English locale. H-1.11 threads
    /// <see cref="CultureInfo.InvariantCulture"/> through both sides; this
    /// test pins the <see cref="CultureInfo.CurrentCulture"/> and
    /// <see cref="CultureInfo.CurrentUICulture"/> to <c>de-DE</c> for the
    /// duration of the write and confirms the emitted text uses <c>.</c>
    /// rather than <c>,</c>.
    /// </summary>
    [Fact]
    public void LocationSearch_SelectedResultChanged_EmitsInvariantDecimalSeparatorOnDeDE()
    {
        (_, RelocationViewModel child) = CreateChild();
        LocationSearchResult berlin = new()
        {
            DisplayName = "Berlin, Germany",
            Latitude = 52.5200,
            Longitude = 13.4050,
        };

        CultureInfo originalCulture = Thread.CurrentThread.CurrentCulture;
        CultureInfo originalUiCulture = Thread.CurrentThread.CurrentUICulture;
        try
        {
            Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");
            Thread.CurrentThread.CurrentUICulture = new CultureInfo("de-DE");

            child.LocationSearch.SelectedResult = berlin;

            child.LatitudeText.Should().Be("52.5200",
                "F-CS-Relocation: InvariantCulture must win over the host's " +
                "de-DE culture so the stored decimal separator stays '.'");
            child.LongitudeText.Should().Be("13.4050",
                "F-CS-Relocation: longitude must likewise round-trip as '.' " +
                "under InvariantCulture");
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
        }
    }

    /// <summary>
    /// Setting <see cref="LocationSearchViewModel.SelectedResult"/> back to
    /// <see langword="null"/> (e.g. when the dropdown is cleared) must not
    /// overwrite the location fields \u2014 the handler guards on null to
    /// preserve whatever the user may have typed. Pre-extraction behaviour
    /// preserved verbatim.
    /// </summary>
    [Fact]
    public void LocationSearch_SelectedResultSetToNull_DoesNotOverwriteFields()
    {
        (_, RelocationViewModel child) = CreateChild();
        child.LocationName = "Manual Entry";
        child.LatitudeText = "12.3456";
        child.LongitudeText = "78.9012";

        child.LocationSearch.SelectedResult = null;

        child.LocationName.Should().Be("Manual Entry");
        child.LatitudeText.Should().Be("12.3456");
        child.LongitudeText.Should().Be("78.9012");
    }

    #endregion

    #region Clear Semantics

    /// <summary>
    /// <see cref="RelocationViewModel.Clear"/> must reset every mutable
    /// field (natal-chart selection, location name/lat/long text, current
    /// result) and the nested location search, and emit exactly one
    /// <see cref="ChildViewModelBase.CommandStateRefreshRequested"/> via the
    /// field-bypass pattern from H-1.2's
    /// <c>ChartEntryViewModel.PopulateFrom</c>. If Clear used setters the
    /// four refresh-firing properties (SelectedNatalChart, LocationName, the
    /// CurrentResult setter, and the nested LocationSearch.Clear) would
    /// each cascade their own refresh \u2014 multiple refresh pulses per
    /// reset rather than one.
    /// </summary>
    [Fact]
    public void Clear_ResetsAllFieldsAndFiresRefreshOnce()
    {
        (_, RelocationViewModel child) = CreateChild();

        child.SelectedNatalChart = MakeChartEntity();
        child.LocationName = "London, UK";
        child.LatitudeText = "51.5074";
        child.LongitudeText = "-0.1278";
        child.CurrentResult = MakeResult();
        child.LocationSearch.SearchText = "London";

        int refreshCount = 0;
        child.CommandStateRefreshRequested += (_, _) => refreshCount++;

        child.Clear();

        child.SelectedNatalChart.Should().BeNull();
        child.LocationName.Should().BeEmpty();
        child.LatitudeText.Should().BeEmpty();
        child.LongitudeText.Should().BeEmpty();
        child.CurrentResult.Should().BeNull();
        child.HasResult.Should().BeFalse();
        child.LocationSearch.SearchText.Should().BeEmpty(
            "Clear on the parent must propagate to Clear on the nested " +
            "LocationSearch, matching the H-1.6 / H-1.9 precedent");

        refreshCount.Should().Be(1,
            "Clear uses the field-bypass pattern so exactly one refresh " +
            "fires at the end rather than cascading through each setter");
    }

    #endregion

    #region Composition Invariants

    /// <summary>
    /// <see cref="MainViewModel.Relocation"/> must be a concrete instance of
    /// <see cref="RelocationViewModel"/> and must be the same reference each
    /// time it is accessed \u2014 composition is eager and the property
    /// getter must not construct a fresh child per call.
    /// </summary>
    [Fact]
    public void Parent_ExposesStableRelocationChild()
    {
        (MainViewModel parent, RelocationViewModel child) = CreateChild();

        child.Should().NotBeNull();
        parent.Relocation.Should().BeSameAs(child,
            "Relocation is an eagerly-composed child; the getter must not " +
            "produce a new instance on every access");
    }

    /// <summary>
    /// The nested <see cref="RelocationViewModel.LocationSearch"/> must be
    /// a concrete instance of <see cref="LocationSearchViewModel"/> and
    /// must be stable across accesses, matching the H-1.6 / H-1.9 shape.
    /// </summary>
    [Fact]
    public void Child_ExposesStableNestedLocationSearch()
    {
        (_, RelocationViewModel child) = CreateChild();

        child.LocationSearch.Should().NotBeNull();
        child.LocationSearch.Should().BeSameAs(child.LocationSearch,
            "the nested LocationSearch is constructed once and reused");
    }

    #endregion
}
