using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Utilities;

namespace AstroCast.Tests.ViewModels;

// ---------------------------------------------------------------------------
// Service test doubles for MainViewModel's twenty engine/service dependencies.
//
// These lived as internal nested types inside MainViewModelTests until Phase 4
// slice 1.5. They moved here so AstroCast.UiTests can reach them: that project
// is on xunit v3 (a hard requirement of Avalonia.Headless.XUnit) while
// AstroCast.Tests is on xunit v2, and the two majors cannot coexist in one
// assembly. An xunit-free library is consumable by both.
//
// The namespace is deliberately unchanged from where these types used to live,
// so no consuming test file needs a new using directive and no test's fully
// qualified name moves. Namespaces may legally span assemblies; the Phase 3
// presentation-layer extraction relies on the same property.
//
// They are public rather than internal because a nested internal type is
// visible to its whole assembly, but a top-level internal type in a separate
// assembly would be visible to nothing.
// ---------------------------------------------------------------------------

/// <summary>
/// Minimal <see cref="IChartEngine"/> stub for ViewModel constructor.
/// Returns a dummy <see cref="ChartResult"/> without performing real calculations.
/// </summary>
public sealed class StubChartEngine : IChartEngine
{
    /// <inheritdoc />
    public ChartResult Calculate(ChartInput input, CancellationToken cancellationToken = default) =>
        CreateDummyResult(input);

    /// <inheritdoc />
    public ChartResult CalculateAtInstant(ChartInput input, double julianDayUt, CancellationToken cancellationToken = default) =>
        CreateDummyResult(input);

    /// <inheritdoc />
    public ChartResult CalculateRelocated(ChartResult natalChart, RelocationInput target, CancellationToken cancellationToken = default) =>
        CreateDummyResult(new ChartInput
        {
            Name = $"{natalChart.Input.Name} \u2014 Relocated to {target.LocationName}",
            LocalDateTime = natalChart.Input.LocalDateTime,
            TimeZone = natalChart.Input.TimeZone,
            UseExplicitOffset = natalChart.Input.UseExplicitOffset,
            UtcOffsetHours = natalChart.Input.UtcOffsetHours,
            Latitude = target.Latitude,
            Longitude = target.Longitude,
            LocationName = target.LocationName,
            EventType = natalChart.Input.EventType,
            HouseSystem = natalChart.Input.HouseSystem,
            HoraryQuestion = natalChart.Input.HoraryQuestion,
            HoraryCategory = natalChart.Input.HoraryCategory,
        });

    /// <inheritdoc />
    public ChartResult CalculateForElectional(ChartInput input, CancellationToken cancellationToken = default) =>
        CreateDummyResult(input);

    /// <inheritdoc />
    public ChartResult CalculateForElectional(ChartInput input, DateTime utcDateTime, CancellationToken cancellationToken = default) =>
        CreateDummyResult(input);

    /// <inheritdoc />
    public ChartResult CalculateElectionalScoringInputs(ChartInput input, DateTime utcDateTime, CancellationToken cancellationToken = default) =>
        CreateDummyResult(input);

    private static ChartResult CreateDummyResult(ChartInput input) => new()
    {
        Input = input,
        JulianDay = 2460000.0,
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
        ResolvedUtcOffsetHours = -5.0,
        ResolvedOffsetLabel = "EST (UTC-5)",
        HouseSystemName = "Placidus",
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };
}

/// <summary>
/// An <see cref="IChartEngine"/> that records every <see cref="ChartInput"/> given to <see cref="Calculate"/> and
/// returns <see cref="StubChartEngine"/>'s dummy result for it, or throws while <see cref="ThrowOnCalculate"/> is
/// set (branch AsteroidsPanel, 1.6.0): for the tests that read which input a command built, and what the view
/// model does when the cast fails.
/// </summary>
/// <remarks>
/// A call is recorded before it throws, so a test can see that the engine was reached. The other members
/// delegate to the stub unrecorded. The record is taken under a lock, since a command's <c>Task.Run</c> casts
/// off the test's thread; read it once the command has finished. A cast can also probe the view model while it
/// runs (<see cref="Probe"/>), signal that it has started (<see cref="Entered"/>) and hold until its token is
/// cancelled (<see cref="HoldUntilCancelled"/>): the one engine double for every view-model test since 1.6.1
/// (branch ElectionalPicker), where <c>RelocationCommandTests</c> had nested its own.
/// </remarks>
public sealed class RecordingChartEngine : IChartEngine
{
    private readonly StubChartEngine _inner = new();
    private readonly List<ChartInput> _inputs = [];
    private readonly object _gate = new();

    /// <summary>The message <see cref="Calculate"/> throws with while <see cref="ThrowOnCalculate"/> is set.</summary>
    public const string ThrowMessage = "The recording engine was told to throw.";

    /// <summary>Gets or sets a value indicating whether <see cref="Calculate"/> throws instead of casting.</summary>
    public bool ThrowOnCalculate { get; set; }

    /// <summary>
    /// Gets or sets what to read from the view model while a cast runs: called inside <see cref="Calculate"/>,
    /// on the cast's own thread, after the input is recorded and before <see cref="Entered"/> is signalled.
    /// </summary>
    public Func<string?>? Probe { get; set; }

    /// <summary>Gets the value <see cref="Probe"/> read during the last cast, or null when it did not run.</summary>
    public string? ProbedDuringCalculate { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether a cast waits for its token to be cancelled, for at most 2 s, and
    /// then throws <see cref="OperationCanceledException"/> if it was.
    /// </summary>
    public bool HoldUntilCancelled { get; set; }

    /// <summary>Gets a task that completes when a cast has started, after its probe and before any hold.</summary>
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets every input given to <see cref="Calculate"/>, in call order, the throwing calls included.</summary>
    public IReadOnlyList<ChartInput> Inputs
    {
        get
        {
            lock (_gate)
            {
                return _inputs.ToArray();
            }
        }
    }

    /// <summary>Gets the input of the last <see cref="Calculate"/> call, or null before the first.</summary>
    public ChartInput? LastInput
    {
        get
        {
            lock (_gate)
            {
                return _inputs.Count == 0 ? null : _inputs[^1];
            }
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">
    /// Thrown, with <see cref="ThrowMessage"/>, while <see cref="ThrowOnCalculate"/> is set.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Thrown while <see cref="HoldUntilCancelled"/> is set and the token is cancelled within the hold.
    /// </exception>
    public ChartResult Calculate(ChartInput input, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _inputs.Add(input);
        }

        ProbedDuringCalculate = Probe?.Invoke();
        Entered.TrySetResult();
        if (HoldUntilCancelled)
        {
            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(2));
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (ThrowOnCalculate)
        {
            throw new InvalidOperationException(ThrowMessage);
        }

        return _inner.Calculate(input, cancellationToken);
    }

    /// <inheritdoc />
    public ChartResult CalculateAtInstant(ChartInput input, double julianDayUt, CancellationToken cancellationToken = default) =>
        _inner.CalculateAtInstant(input, julianDayUt, cancellationToken);

    /// <inheritdoc />
    public ChartResult CalculateRelocated(ChartResult natalChart, RelocationInput target, CancellationToken cancellationToken = default) =>
        _inner.CalculateRelocated(natalChart, target, cancellationToken);

    /// <inheritdoc />
    public ChartResult CalculateForElectional(ChartInput input, CancellationToken cancellationToken = default) =>
        _inner.CalculateForElectional(input, cancellationToken);

    /// <inheritdoc />
    public ChartResult CalculateForElectional(ChartInput input, DateTime utcDateTime, CancellationToken cancellationToken = default) =>
        _inner.CalculateForElectional(input, utcDateTime, cancellationToken);

    /// <inheritdoc />
    public ChartResult CalculateElectionalScoringInputs(ChartInput input, DateTime utcDateTime, CancellationToken cancellationToken = default) =>
        _inner.CalculateElectionalScoringInputs(input, utcDateTime, cancellationToken);
}

/// <summary>
/// Minimal <see cref="IChartRepository"/> stub that stores nothing.
/// All methods return empty results or default values.
/// </summary>
public sealed class StubChartRepository : IChartRepository
{
    /// <inheritdoc />
    public Task<long> SaveAsync(ChartEntity chart, CancellationToken ct = default) =>
        Task.FromResult(1L);

    /// <inheritdoc />
    public Task UpdateAsync(ChartEntity chart, CancellationToken ct = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task<bool> DeleteAsync(long id, CancellationToken ct = default) =>
        Task.FromResult(true);

    /// <inheritdoc />
    public Task<ChartEntity?> GetByIdAsync(long id, CancellationToken ct = default) =>
        Task.FromResult<ChartEntity?>(null);

    /// <inheritdoc />
    public Task<ChartEntity?> GetByNameAsync(string name, CancellationToken ct = default) =>
        Task.FromResult<ChartEntity?>(null);

    /// <inheritdoc />
    public Task<IReadOnlyList<ChartEntity>> SearchByNameAsync(string namePattern, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ChartEntity>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<ChartEntity>> ListAllAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ChartEntity>>([]);

    /// <inheritdoc />
    public Task<IReadOnlyList<ChartEntity>> ListByEventTypeAsync(string eventType, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ChartEntity>>([]);

    /// <inheritdoc />
    public Task InitializeAsync(CancellationToken ct = default) =>
        Task.CompletedTask;

    /// <inheritdoc />
    public Task<string> ExportAllAsJsonAsync(CancellationToken ct = default) =>
        Task.FromResult("[]");

    /// <inheritdoc />
    public Task<int> ImportFromJsonAsync(string json, CancellationToken ct = default) =>
        Task.FromResult(0);

    /// <inheritdoc />
    public Task<IReadOnlyList<ChartEntity>> QueryAsync(
        AstroCast.Core.Models.ChartQueryOptions options, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ChartEntity>>([]);
}

/// <summary>
/// Minimal <see cref="IEphemerisService"/> stub for ViewModel constructor.
/// Returns dummy values without invoking the Swiss Ephemeris native library.
/// </summary>
public sealed class StubEphemerisService : IEphemerisService
{
    /// <inheritdoc />
    public string Version => "Test 2.10";

    /// <inheritdoc />
    public PlanetPosition CalculatePosition(double julianDayUt, CelestialBodyId body) => new()
    {
        Body = body,
        Name = body.ToString(),
        Longitude = 0.0,
        Latitude = 0.0,
        Distance = 1.0,
        SpeedInLongitude = 1.0,
        SpeedInLatitude = 0.0,
        SpeedInDistance = 0.0,
    };

    /// <inheritdoc />
    public PlanetPosition CalculatePositionLongitudeOnly(double julianDayUt, CelestialBodyId body) =>
        CalculatePosition(julianDayUt, body);

    /// <inheritdoc />
    public IReadOnlyList<PlanetPosition> CalculateAllPositions(double julianDayUt) => [];

    /// <inheritdoc />
    public double DateToJulianDay(int year, int month, int day, double hour,
        CalendarType calendar = CalendarType.Gregorian) => 2460000.0;

    /// <inheritdoc />
    public (int Year, int Month, int Day, double Hour) JulianDayToDate(
        double julianDay, CalendarType calendar = CalendarType.Gregorian) =>
        (2026, 1, 1, 12.0);

    /// <inheritdoc />
    public HouseData CalculateHouses(double julianDayUt, double latitude, double longitude,
        char houseSystem = 'P') => new()
    {
        Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
        Ascendant = 0.0,
        Midheaven = 270.0,
        Armc = 0.0,
        Vertex = 0.0,
    };

    /// <inheritdoc />
    public double GetObliquity(double julianDayUt) => 23.44;

    /// <inheritdoc />
    public (double SunriseJd, double SunsetJd) CalculateSunriseSunset(
        double julianDayUt, double latitude, double longitude) =>
        (julianDayUt + 0.25, julianDayUt + 0.75);

    /// <inheritdoc />
    public HouseData CalculateHousesFromArmc(double armc, double latitude, double obliquity,
        char houseSystem = 'P') => new()
    {
        Cusps = Enumerable.Range(0, 12).Select(i => i * 30.0).ToList(),
        Ascendant = 0.0,
        Midheaven = 270.0,
        Armc = armc,
        Vertex = 0.0,
    };

    /// <inheritdoc />
    public double? CalculateRiseTransit(double julianDayUt, CelestialBodyId body,
        int rsmiFlag, double latitude, double longitude, string? starName = null) =>
        julianDayUt + 0.25;

    /// <inheritdoc />
    public double CalculateFixedStarLongitude(string starName, double julianDayUt) => 0.0;

    /// <inheritdoc />
    public (double Longitude, double Latitude) CalculateFixedStarPosition(string starName, double julianDayUt) =>
        (0.0, 0.0);

    /// <inheritdoc />
    public (double RightAscension, double Declination) EclipticToEquatorial(
        double eclipticLongitude, double eclipticLatitude, double obliquity) =>
        (eclipticLongitude, 0.0);

    /// <inheritdoc />
    public double GetSiderealTimeDegrees(double julianDayUt) => 0.0;

    /// <inheritdoc />
    public (int Flags, double[] Tret) FindNextSolarEclipse(double jdStart) =>
        (-1, new double[10]);

    /// <inheritdoc />
    public (int Flags, double[] Tret) FindNextLunarEclipse(double jdStart) =>
        (-1, new double[10]);

    /// <inheritdoc />
    public void Dispose() { }
}

/// <summary>
/// Minimal <see cref="ISynastryService"/> stub for ViewModel constructor.
/// Returns a dummy <see cref="SynastryResult"/>.
/// </summary>
public sealed class StubSynastryService : ISynastryService
{
    /// <inheritdoc />
    public SynastryResult Calculate(
        ChartResult personA,
        ChartResult personB,
        RelationshipType relationshipType = RelationshipType.Unspecified,
        string? relationshipNotes = null,
        CancellationToken cancellationToken = default) => new()
    {
        PersonA = personA,
        PersonB = personB,
        RelationshipType = relationshipType,
        RelationshipNotes = relationshipNotes,
        InterChartAspects = [],
        PersonAInPersonBHouses = [],
        PersonBInPersonAHouses = [],
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };
}

/// <summary>
/// Minimal <see cref="ICompositeService"/> stub for ViewModel constructor.
/// Returns a dummy <see cref="CompositeResult"/>.
/// </summary>
public sealed class StubCompositeService : ICompositeService
{
    /// <inheritdoc />
    public CompositeResult Calculate(
        ChartResult chartA,
        ChartResult chartB,
        CancellationToken cancellationToken = default) => new()
    {
        ChartA = chartA,
        ChartB = chartB,
        CompositeChart = chartA,
        ReferenceLatitude = 0.0,
        ReferenceLongitude = 0.0,
        ReferenceLocationDescription = "Test Location",
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };
}

/// <summary>
/// An <see cref="IDavisonService"/> double for the view-model tests (branch DavisonPanel, 1.7.0): records every
/// call, returns chart A as the Davison chart with the midpoints computed from the two charts (so the status
/// line's moment and place are real), throws on demand, and can hold a call until its token is cancelled.
/// </summary>
public sealed class StubDavisonService : IDavisonService
{
    private readonly List<(ChartResult ChartA, ChartResult ChartB, bool IncludeMinorPoints)> _calls = [];
    private readonly object _gate = new();

    /// <summary>The message <see cref="Calculate"/> throws with while <see cref="ThrowOnCalculate"/> is set.</summary>
    public const string ThrowMessage = "The Davison double was told to throw.";

    /// <summary>Gets or sets a value indicating whether <see cref="Calculate"/> throws instead of answering.</summary>
    public bool ThrowOnCalculate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether a call waits for its token to be cancelled, for at most 2 s, and then
    /// throws <see cref="OperationCanceledException"/> if it was.
    /// </summary>
    public bool HoldUntilCancelled { get; set; }

    /// <summary>Gets a task that completes when a call has started, after its record and before any hold.</summary>
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>Gets every call, in order, the throwing and cancelled ones included.</summary>
    public IReadOnlyList<(ChartResult ChartA, ChartResult ChartB, bool IncludeMinorPoints)> Calls
    {
        get
        {
            lock (_gate)
            {
                return _calls.ToArray();
            }
        }
    }

    /// <inheritdoc />
    /// <exception cref="InvalidOperationException">Thrown, with <see cref="ThrowMessage"/>, while <see cref="ThrowOnCalculate"/> is set.</exception>
    /// <exception cref="OperationCanceledException">Thrown while <see cref="HoldUntilCancelled"/> is set and the token is cancelled within the hold.</exception>
    public DavisonResult Calculate(
        ChartResult chartA,
        ChartResult chartB,
        bool includeMinorPoints,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _calls.Add((chartA, chartB, includeMinorPoints));
        }

        Entered.TrySetResult();
        if (HoldUntilCancelled)
        {
            cancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(2));
            cancellationToken.ThrowIfCancellationRequested();
        }

        if (ThrowOnCalculate)
        {
            throw new InvalidOperationException(ThrowMessage);
        }

        double midpointJd = (chartA.JulianDay + chartB.JulianDay) / 2.0;
        double difference = ((chartB.Input.Longitude - chartA.Input.Longitude + 540.0) % 360.0) - 180.0;
        double longitude = ((chartA.Input.Longitude + difference / 2.0 + 540.0) % 360.0) - 180.0;
        return new DavisonResult
        {
            ChartA = chartA,
            ChartB = chartB,
            DavisonChart = chartA,
            MidpointUtc = JulianDayUtility.JulianDayToInstant(midpointJd),
            MidpointJulianDay = midpointJd,
            MidpointLatitude = (chartA.Input.Latitude + chartB.Input.Latitude) / 2.0,
            MidpointLongitude = longitude,
            MidpointLocationDescription = $"Midpoint of {chartA.Input.LocationName} & {chartB.Input.LocationName}",
            EngineVersion = "Test 1.0.0",
            CalculatedAt = DateTime.UtcNow,
        };
    }
}

/// <summary>
/// Minimal <see cref="ISolarReturnService"/> stub for ViewModel constructor.
/// Returns a dummy <see cref="SolarReturnResult"/>.
/// </summary>
public sealed class StubSolarReturnService : ISolarReturnService
{
    /// <inheritdoc />
    public SolarReturnResult Calculate(
        ChartResult natalChart,
        int year,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default) => new()
    {
        NatalChart = natalChart,
        ReturnChart = natalChart,
        Year = year,
        NatalSunLongitude = 0.0,
        ReturnSunLongitude = 0.0,
        ReturnLocationName = locationName,
        ReturnLatitude = latitude,
        ReturnLongitude = longitude,
        PrecisionArcSeconds = 0.0,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };
}

/// <summary>
/// Minimal <see cref="ILunarReturnService"/> stub for ViewModel constructor.
/// Returns a dummy <see cref="LunarReturnResult"/>.
/// </summary>
public sealed class StubLunarReturnService : ILunarReturnService
{
    /// <inheritdoc />
    public IReadOnlyList<LunarReturnResult> Calculate(
        ChartResult natalChart,
        DateTime startDate,
        DateTime endDate,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default) => [];

    /// <inheritdoc />
    public LunarReturnResult CalculateNext(
        ChartResult natalChart,
        DateTime onOrAfter,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default) => new()
    {
        NatalChart = natalChart,
        ReturnChart = natalChart,
        SequenceNumber = 1,
        NatalMoonLongitude = 0.0,
        ReturnMoonLongitude = 0.0,
        ReturnLocationName = locationName,
        ReturnLatitude = latitude,
        ReturnLongitude = longitude,
        PrecisionArcSeconds = 0.0,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };
}

public sealed class StubPlanetaryReturnService : IPlanetaryReturnService
{
    /// <inheritdoc />
    public PlanetaryReturnResult Calculate(
        ChartResult natalChart,
        CelestialBodyId planet,
        int returnNumber,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default) => new()
    {
        NatalChart = natalChart,
        ReturnChart = natalChart,
        Planet = planet,
        PlanetName = planet.ToString(),
        NatalLongitude = 0.0,
        ReturnLongitude = 0.0,
        ReturnNumber = returnNumber,
        AgeAtReturn = 29,
        Passes = [natalChart.UtcDateTime],
        ReturnLocationName = locationName,
        ReturnLatitude = latitude,
        ReturnLongitude_Geo = longitude,
        PrecisionArcSeconds = 0.0,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <inheritdoc />
    public PlanetaryReturnResult CalculateForYear(
        ChartResult natalChart,
        CelestialBodyId planet,
        int year,
        double latitude,
        double longitude,
        string locationName,
        string timeZone,
        CancellationToken cancellationToken = default) => Calculate(
            natalChart, planet, 1, latitude, longitude, locationName, timeZone, cancellationToken);
}

public sealed class StubCycleTimelineService : IPlanetaryCycleTimelineService
{
    /// <inheritdoc />
    public PlanetaryCycleTimeline Calculate(
        ChartResult natalChart,
        CelestialBodyId planet,
        int returnNumber,
        bool includeSnapshots = false,
        CancellationToken cancellationToken = default) => new()
    {
        Planet = planet,
        PlanetName = planet.ToString(),
        NatalLongitude = 0.0,
        NatalPositionFormatted = "00°00'00\" Aries",
        NativeName = natalChart.Input.Name,
        ReturnNumber = returnNumber,
        PhasePoints = [],
        CycleDurationDays = 365.0,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <inheritdoc />
    public PlanetaryCycleTimeline CalculateForDate(
        ChartResult natalChart,
        CelestialBodyId planet,
        DateTime date,
        bool includeSnapshots = false,
        CancellationToken cancellationToken = default) => Calculate(
            natalChart, planet, 1, includeSnapshots, cancellationToken);
}

public sealed class StubGreatConjunctionService : IGreatConjunctionService
{
    /// <inheritdoc />
    public GreatConjunctionSeries FindConjunctions(
        int startYear, int endYear,
        CancellationToken cancellationToken = default) => new()
    {
        Conjunctions = [],
        ScanStart = new DateTime(startYear, 1, 1),
        ScanEnd = new DateTime(endYear, 12, 31),
        Count = 0,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <inheritdoc />
    public GreatConjunctionResult FindNearest(
        int year,
        CancellationToken cancellationToken = default) => new()
    {
        ExactUtc = new DateTime(year, 6, 1),
        Longitude = 0.0,
        PositionFormatted = "00°00'00\" Aries",
        Sign = "Aries",
        Element = "Fire",
        Modality = "Cardinal",
        IsMutation = false,
        YearsSincePrevious = 20.0,
        JupiterLongitude = 0.0,
        SaturnLongitude = 0.0,
        PrecisionArcSeconds = 0.0,
    };

    /// <inheritdoc />
    public ChartResult CastConjunctionChart(
        GreatConjunctionResult conjunction,
        double latitude, double longitude,
        string locationName, string timeZone,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Stub");
}

public sealed class StubIngressService : IIngressService
{
    /// <inheritdoc />
    public IngressSeries FindIngresses(
        CelestialBodyId planet, int startYear, int endYear,
        bool cardinalOnly = false,
        CancellationToken cancellationToken = default) => new()
    {
        Planet = planet,
        PlanetName = planet.ToString(),
        Ingresses = [],
        ScanStart = new DateTime(startYear, 1, 1),
        ScanEnd = new DateTime(endYear, 12, 31),
        Count = 0,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <inheritdoc />
    public IngressResult CalculateIngress(
        CelestialBodyId planet, ZodiacSign sign, int year,
        double latitude, double longitude,
        string locationName, string timeZone,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Stub");

    /// <inheritdoc />
    public IReadOnlyList<IngressResult> CalculateCardinalIngresses(
        int year, double latitude, double longitude,
        string locationName, string timeZone,
        CancellationToken cancellationToken = default) => [];
}

public sealed class StubLunationService : ILunationService
{
    /// <inheritdoc />
    public LunationSeries FindLunations(
        int startYear, int endYear,
        CancellationToken cancellationToken = default) => new()
    {
        Lunations = [],
        ScanStart = new DateTime(startYear, 1, 1),
        ScanEnd = new DateTime(endYear, 12, 31),
        Count = 0,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <inheritdoc />
    public LunationResult CalculateLunation(
        DateTime nearDate, bool newMoon,
        double latitude, double longitude,
        string locationName, string timeZone,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("Stub");

    /// <inheritdoc />
    public LunationSeries CalculateLunationsForYear(
        int year, double latitude, double longitude,
        string locationName, string timeZone,
        CancellationToken cancellationToken = default) => new()
    {
        Lunations = [],
        ScanStart = new DateTime(year, 1, 1),
        ScanEnd = new DateTime(year, 12, 31),
        Count = 0,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };
}

public sealed class StubRelocationService : IRelocationService
{
    /// <inheritdoc />
    public RelocationResult Calculate(
        ChartResult natalChart,
        RelocationInput target,
        CancellationToken cancellationToken = default) => new()
    {
        NatalChart = natalChart,
        RelocatedChart = natalChart,
        RelocationTarget = target,
        HouseChanges = [],
        NatalAscendantSign = Core.Enums.ZodiacSign.Aries,
        RelocatedAscendantSign = Core.Enums.ZodiacSign.Aries,
        NatalMidheavenSign = Core.Enums.ZodiacSign.Capricorn,
        RelocatedMidheavenSign = Core.Enums.ZodiacSign.Capricorn,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };
}

public sealed class StubMundaneAspectService : IMundaneAspectService
{
    /// <inheritdoc />
    public MundaneAspectSeries FindAspects(
        Core.Enums.CelestialBodyId body1,
        Core.Enums.CelestialBodyId body2,
        string harmonicMode,
        int startYear,
        int endYear,
        CancellationToken cancellationToken = default) => new()
    {
        Body1 = body1,
        Body1Name = body1.ToString(),
        Body2 = body2,
        Body2Name = body2.ToString(),
        HarmonicMode = harmonicMode,
        Aspects = [],
        ScanStart = new DateTime(startYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ScanEnd = new DateTime(endYear, 12, 31, 23, 59, 59, DateTimeKind.Utc),
        Count = 0,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };
}

public sealed class StubEclipseService : IEclipseService
{
    /// <inheritdoc />
    public EclipseSeries FindEclipses(int startYear, int endYear,
        CancellationToken cancellationToken = default) => new()
    {
        Eclipses = [],
        ScanStart = new DateTime(startYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ScanEnd = new DateTime(endYear, 12, 31, 23, 59, 59, DateTimeKind.Utc),
        Count = 0,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };

    /// <inheritdoc />
    public EclipseSeries FindSolarEclipses(int startYear, int endYear,
        CancellationToken cancellationToken = default) =>
        FindEclipses(startYear, endYear, cancellationToken);

    /// <inheritdoc />
    public EclipseSeries FindLunarEclipses(int startYear, int endYear,
        CancellationToken cancellationToken = default) =>
        FindEclipses(startYear, endYear, cancellationToken);
}

public sealed class StubReturnIngressTimelineService : IReturnIngressTimelineService
{
    /// <inheritdoc />
    public ReturnIngressTimeline Calculate(
        ChartResult? natalChart, int startYear, int endYear,
        bool includeReturns = true, bool includeIngresses = true,
        CelestialBodyId[]? returnPlanets = null, CelestialBodyId[]? ingressPlanets = null,
        CancellationToken cancellationToken = default) => new()
    {
        NativeName = natalChart?.Input.Name ?? "Test",
        Entries = [],
        ScanStart = new DateTime(startYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        ScanEnd = new DateTime(endYear, 12, 31, 23, 59, 59, DateTimeKind.Utc),
        Count = 0,
        ReturnPlanets = [],
        IngressPlanets = [],
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };
}

public sealed class StubSynodicCycleService : ISynodicCycleService
{
    /// <inheritdoc />
    public SynodicCycleSeries FindConjunctions(
        CelestialBodyId bodyA, CelestialBodyId bodyB,
        int startYear, int endYear,
        CancellationToken cancellationToken = default) => new()
    {
        BodyAName = bodyA.ToString(),
        BodyBName = bodyB.ToString(),
        Conjunctions = [],
        StartDate = new DateTime(startYear, 1, 1, 0, 0, 0, DateTimeKind.Utc),
        EndDate = new DateTime(endYear, 12, 31, 23, 59, 59, DateTimeKind.Utc),
        Count = 0,
        EngineVersion = "Test 1.0.0",
        CalculatedAt = DateTime.UtcNow,
    };
}

/// <summary>
/// Minimal <see cref="ITransitService"/> stub for ViewModel constructor.
/// Calculate is never invoked by ViewModel unit tests (which exercise
/// command CanExecute / state only), so throwing is the simplest contract
/// that signals "not exercised here".
/// </summary>
public sealed class StubTransitService : ITransitService
{
    /// <inheritdoc />
    public AstroCast.Core.Models.Transit.TransitResult Calculate(
        AstroCast.Core.Models.Transit.TransitInput input,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("StubTransitService.Calculate is not used by ViewModel unit tests.");
}

/// <summary>
/// Minimal <see cref="IPrimaryDirectionService"/> stub for the ViewModel constructor (branch PrimaryPanel,
/// 1.5.0). Neither method is invoked by the ViewModel tests that take it, which exercise command
/// CanExecute and state only, so both throw, as <see cref="StubTransitService"/> does.
/// </summary>
public sealed class StubPrimaryDirectionService : IPrimaryDirectionService
{
    /// <inheritdoc />
    public AstroCast.Core.Models.PrimaryDirections.PrimaryDirectionResult Calculate(
        ChartResult natalChart,
        AstroCast.Core.Models.PrimaryDirections.PrimaryDirectionSettings settings,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("StubPrimaryDirectionService.Calculate is not used by ViewModel unit tests.");

    /// <inheritdoc />
    public IReadOnlyList<AstroCast.Core.Models.PrimaryDirections.PrimaryDirectionHit> FindHits(
        ChartResult natalChart,
        AstroCast.Core.Models.PrimaryDirections.PrimaryDirectionSettings settings,
        DateOnly start,
        DateOnly end,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("StubPrimaryDirectionService.FindHits is not used by ViewModel unit tests.");
}

/// <summary>
/// Minimal <see cref="IElectionalService"/> stub for ViewModel constructor.
/// Calculate is never invoked by ViewModel unit tests; throwing keeps
/// the stub honest if a future test accidentally reaches into it.
/// </summary>
public sealed class StubElectionalService : IElectionalService
{
    /// <inheritdoc />
    public AstroCast.Core.Models.Electional.ElectionalResult Calculate(
        AstroCast.Core.Models.Electional.ElectionalInput input,
        CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("StubElectionalService.Calculate is not used by ViewModel unit tests.");
}

/// <summary>
/// Minimal <see cref="IAstrocartographyService"/> stub for ViewModel constructor.
/// Neither Calculate nor GetCrossings is invoked by ViewModel unit tests.
/// </summary>
public sealed class StubAstrocartographyService : IAstrocartographyService
{
    /// <inheritdoc />
    public AstroCast.Core.Models.Astrocartography.AcgResult Calculate(
        ChartResult natalChart, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException("StubAstrocartographyService.Calculate is not used by ViewModel unit tests.");

    /// <inheritdoc />
    public AstroCast.Core.Models.Astrocartography.AcgCrossingReport GetCrossings(
        AstroCast.Core.Models.Astrocartography.AcgResult acgResult,
        double latitude, double longitude,
        string locationName,
        double orbKilometres = AstroCast.Core.Services.AstrocartographyService.DefaultCrossingOrbKilometres) =>
        throw new NotImplementedException("StubAstrocartographyService.GetCrossings is not used by ViewModel unit tests.");
}
