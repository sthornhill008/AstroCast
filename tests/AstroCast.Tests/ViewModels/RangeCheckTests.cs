using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Models.Transit;
using AstroCast.Services;
using AstroCast.Tests.Fixtures;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// The checks each panel makes on its own range before it starts (branch InputChecks, 1.7.2, finding N6): a
/// reversed date or year range and a half-filled electional time window stop with a plain sentence on the status
/// line, and the engine is not called.
/// </summary>
/// <remarks>
/// Until 1.7.2 no panel checked its range: the engine refused in the code's words ("Scan range start (2026-11-27)
/// must be strictly before end (2026-11-27). (Parameter 'startDate')"), and an electional time window with one box
/// empty or mistyped was dropped in silence, the whole day searched. Every engine here is a double that records
/// its call, so "not called" is read from the record.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class RangeCheckTests
{
    private const string EndDateBeforeStart = "The end date is before the start date.";
    private const string EndDateNotAfterStart = "The end date must be after the start date.";
    private const string EndYearBeforeStart = "The end year is before the start year.";

    #region Dates

    /// <summary>Transit dates the wrong way round: the plain sentence, and no scan.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateTransit_EndBeforeStart_SaysSoWithoutCallingTheEngine()
    {
        Harness harness = await Harness.WithAChartAsync();
        harness.Main.Transits.StartDate = new DateTime(2026, 12, 1);
        harness.Main.Transits.EndDate = new DateTime(2026, 11, 27);

        await RunAsync(harness.Main.CalculateTransitCommand);

        harness.Main.StatusText.Should().Be(EndDateBeforeStart);
        harness.Calls.Should().BeEmpty();
        harness.Main.Transits.TransitResult.Should().BeNull();
    }

    /// <summary>A transit report of one day, start and end the same date, goes to the engine with that day.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateTransit_OneDay_CallsTheEngineWithThatDay()
    {
        Harness harness = await Harness.WithAChartAsync();
        harness.Main.Transits.StartDate = new DateTime(2026, 11, 27);
        harness.Main.Transits.EndDate = new DateTime(2026, 11, 27);

        await RunAsync(harness.Main.CalculateTransitCommand);

        harness.Calls.Should().Equal("Transits");
        harness.Transit.Input?.ReportStartDate.Should().Be(new DateOnly(2026, 11, 27));
        harness.Transit.Input?.ReportEndDate.Should().Be(new DateOnly(2026, 11, 27));
    }

    /// <summary>Electional dates the wrong way round: the plain sentence, and no search.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateElectional_EndBeforeStart_SaysSoWithoutCallingTheEngine()
    {
        Harness harness = await Harness.WithAChartAsync();
        FillElectional(harness.Main);
        harness.Main.Electional.StartDate = new DateTime(2026, 6, 20);
        harness.Main.Electional.EndDate = new DateTime(2026, 6, 11);

        await RunAsync(harness.Main.CalculateElectionalCommand);

        harness.Main.StatusText.Should().Be(EndDateBeforeStart);
        harness.Calls.Should().BeEmpty();
    }

    /// <summary>An electional search of one day goes to the engine: its end date is a whole day.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateElectional_OneDay_CallsTheEngine()
    {
        Harness harness = await Harness.WithAChartAsync();
        FillElectional(harness.Main);
        harness.Main.Electional.StartDate = new DateTime(2026, 6, 11);
        harness.Main.Electional.EndDate = new DateTime(2026, 6, 11);

        await RunAsync(harness.Main.CalculateElectionalCommand);

        harness.Calls.Should().Equal("Electional");
    }

    /// <summary>Lunar return dates the wrong way round: the plain sentence, and no search.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateLunarReturn_EndBeforeStart_SaysSoWithoutCallingTheEngine()
    {
        Harness harness = await Harness.WithAChartAsync();
        harness.Main.Returns.LunarReturnStartDate = new DateTime(2026, 12, 1);
        harness.Main.Returns.LunarReturnEndDate = new DateTime(2026, 11, 27);

        await RunAsync(harness.Main.CalculateLunarReturnCommand);

        harness.Main.StatusText.Should().Be(EndDateBeforeStart);
        harness.Calls.Should().BeEmpty();
    }

    /// <summary>
    /// A lunar return range of one date holds no time at all, its bounds being two midnights, and stays refused,
    /// in the user's words.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateLunarReturn_SameDate_SaysTheEndMustBeAfterTheStartWithoutCallingTheEngine()
    {
        Harness harness = await Harness.WithAChartAsync();
        harness.Main.Returns.LunarReturnStartDate = new DateTime(2026, 11, 27);
        harness.Main.Returns.LunarReturnEndDate = new DateTime(2026, 11, 27);

        await RunAsync(harness.Main.CalculateLunarReturnCommand);

        harness.Main.StatusText.Should().Be(EndDateNotAfterStart);
        harness.Calls.Should().BeEmpty();
    }

    /// <summary>A lunar return range the right way round goes to the engine.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateLunarReturn_EndAfterStart_CallsTheEngine()
    {
        Harness harness = await Harness.WithAChartAsync();
        harness.Main.Returns.LunarReturnStartDate = new DateTime(2026, 11, 27);
        harness.Main.Returns.LunarReturnEndDate = new DateTime(2026, 11, 28);

        await RunAsync(harness.Main.CalculateLunarReturnCommand);

        harness.Calls.Should().Equal("LunarReturns");
    }

    #endregion

    #region Years

    /// <summary>
    /// Every series that takes a start and an end year: with the years the wrong way round, the plain sentence and
    /// no scan.
    /// </summary>
    /// <param name="panel">The series: the name its engine double records.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("GreatConjunctions")]
    [InlineData("SynodicCycles")]
    [InlineData("MundaneAspects")]
    [InlineData("Eclipses")]
    [InlineData("ReturnIngressTimeline")]
    public async Task FindSeries_EndYearBeforeStartYear_SaysSoWithoutCallingTheEngine(string panel)
    {
        Harness harness = await Harness.WithAChartAsync();

        await RunAsync(SetYears(harness.Main, panel, startYear: 2030, endYear: 2026));

        harness.Main.StatusText.Should().Be(EndYearBeforeStart);
        harness.Calls.Should().BeEmpty();
    }

    /// <summary>A series of one year, start and end the same, goes to its engine.</summary>
    /// <param name="panel">The series: the name its engine double records.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("GreatConjunctions")]
    [InlineData("SynodicCycles")]
    [InlineData("MundaneAspects")]
    [InlineData("Eclipses")]
    [InlineData("ReturnIngressTimeline")]
    public async Task FindSeries_OneYear_CallsTheEngine(string panel)
    {
        Harness harness = await Harness.WithAChartAsync();

        await RunAsync(SetYears(harness.Main, panel, startYear: 2026, endYear: 2026));

        harness.Calls.Should().Equal(panel);
        harness.Main.StatusText.Should().NotBe(EndYearBeforeStart);
    }

    #endregion

    #region The electional time window

    /// <summary>Both boxes empty: no window, and the search runs.</summary>
    /// <param name="earliest">The Earliest box's text.</param>
    /// <param name="latest">The Latest box's text.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("", "")]
    [InlineData("  ", " ")]
    public async Task CalculateElectional_BothTimesEmpty_SearchesWithNoWindow(string earliest, string latest)
    {
        Harness harness = await ElectionalWithWindowAsync(earliest, latest);

        await RunAsync(harness.Main.CalculateElectionalCommand);

        harness.Calls.Should().Equal("Electional");
        harness.Electional.Input?.TimeConstraint.Should().BeNull();
    }

    /// <summary>
    /// Both boxes filled with times of day: the window, read with a colon whatever the machine's culture, in the
    /// forms the Chart panel's Time box takes.
    /// </summary>
    /// <param name="culture">The culture the form runs under.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("en-US")]
    [InlineData("fi-FI")]
    [InlineData("de-DE")]
    public async Task CalculateElectional_BothTimesFilled_SearchesWithTheWindow(string culture)
    {
        using CultureScope scope = new(culture);
        Harness harness = await ElectionalWithWindowAsync("9:00", " 17:30:00 ");

        await RunAsync(harness.Main.CalculateElectionalCommand);

        harness.Calls.Should().Equal("Electional");
        TimeConstraint? window = harness.Electional.Input?.TimeConstraint;
        window.Should().NotBeNull(harness.Main.StatusText);
        window?.Earliest.Should().Be(new TimeOnly(9, 0));
        window?.Latest.Should().Be(new TimeOnly(17, 30));
    }

    /// <summary>
    /// One box filled and the other empty, or a box that does not hold a time of day: the search stops before it
    /// starts, and the message says which box is at fault.
    /// </summary>
    /// <param name="earliest">The Earliest box's text.</param>
    /// <param name="latest">The Latest box's text.</param>
    /// <param name="expected">The status line.</param>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Theory]
    [InlineData("09:00", "", "The Latest time is empty. Fill in both Earliest and Latest, or leave both empty.")]
    [InlineData("", "17:00", "The Earliest time is empty. Fill in both Earliest and Latest, or leave both empty.")]
    [InlineData("9", "17:00", "The Earliest time is not a time of day. Use HH:mm.")]
    [InlineData("9.00", "17:00", "The Earliest time is not a time of day. Use HH:mm.")]
    [InlineData("09:00", "5 PM", "The Latest time is not a time of day. Use HH:mm.")]
    [InlineData("09:00", "24:00", "The Latest time is not a time of day. Use HH:mm.")]
    [InlineData("nine", "five", "The Earliest time is not a time of day. Use HH:mm.")]
    public async Task CalculateElectional_HalfFilledOrMistypedWindow_StopsNamingTheBox(
        string earliest, string latest, string expected)
    {
        Harness harness = await ElectionalWithWindowAsync(earliest, latest);

        await RunAsync(harness.Main.CalculateElectionalCommand);

        harness.Main.StatusText.Should().Be(expected);
        harness.Calls.Should().BeEmpty();
    }

    /// <summary>A window that would wrap midnight keeps its own refusal.</summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateElectional_EarliestAfterLatest_KeepsItsRefusal()
    {
        Harness harness = await ElectionalWithWindowAsync("22:00", "06:00");

        await RunAsync(harness.Main.CalculateElectionalCommand);

        harness.Main.StatusText.Should().Be(
            "Invalid time window: earliest must not be later than latest (windows may not wrap midnight).");
        harness.Calls.Should().BeEmpty();
    }

    #endregion

    #region A refusal from the engine

    /// <summary>
    /// A refusal the engine itself makes reaches the status line through the one helper: the message, without the
    /// framework's parameter suffix and "Actual value was" line.
    /// </summary>
    /// <returns>A task that completes when the assertions have run.</returns>
    [Fact]
    public async Task CalculateTransit_EngineRefusal_StatusLineHasNoSuffixAndNoActualValueLine()
    {
        Harness harness = await Harness.WithAChartAsync(
            new ArgumentOutOfRangeException("start", new DateOnly(1582, 10, 9), "1582-10-09 never occurred."));

        await RunAsync(harness.Main.CalculateTransitCommand);

        harness.Calls.Should().Equal("Transits");
        harness.Main.StatusText.Should().Be("Transit error: 1582-10-09 never occurred.");
    }

    #endregion

    #region Helpers

    /// <summary>Runs a command and waits for its handler to finish.</summary>
    /// <param name="command">The command.</param>
    /// <returns>A task that completes when the command has finished, and fails after 5 s.</returns>
    private static async Task RunAsync(AsyncRelayCommand command)
    {
        command.Execute(null);

        int waited = 0;
        while (command.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        command.IsExecuting.Should().BeFalse("the command finishes within 5 s");
    }

    /// <summary>Fills the Electional panel's required fields.</summary>
    /// <param name="main">The view model.</param>
    private static void FillElectional(MainViewModel main)
    {
        main.Electional.Goal = "Open a shop";
        main.Electional.LocationName = "Richmond, VA";
        main.Electional.LatitudeText = "37.5385";
        main.Electional.LongitudeText = "-77.4343";
        main.Electional.TimeZone = "America/New_York";
        main.Electional.StartDate = new DateTime(2026, 6, 11);
        main.Electional.EndDate = new DateTime(2026, 6, 20);
    }

    /// <summary>A view model with a chart, the Electional panel filled and its two time boxes as given.</summary>
    /// <param name="earliest">The Earliest box's text.</param>
    /// <param name="latest">The Latest box's text.</param>
    /// <returns>The harness.</returns>
    private static async Task<Harness> ElectionalWithWindowAsync(string earliest, string latest)
    {
        Harness harness = await Harness.WithAChartAsync();
        FillElectional(harness.Main);
        harness.Main.Electional.EarliestTimeText = earliest;
        harness.Main.Electional.LatestTimeText = latest;
        return harness;
    }

    /// <summary>Sets a series' two year boxes and gives its command.</summary>
    /// <param name="main">The view model.</param>
    /// <param name="panel">The series: the name its engine double records.</param>
    /// <param name="startYear">The start year.</param>
    /// <param name="endYear">The end year.</param>
    /// <returns>The series' command.</returns>
    private static AsyncRelayCommand SetYears(MainViewModel main, string panel, int startYear, int endYear)
    {
        switch (panel)
        {
            case "GreatConjunctions":
                main.Cycles.GCStartYear = startYear;
                main.Cycles.GCEndYear = endYear;
                return main.FindGreatConjunctionsCommand;
            case "SynodicCycles":
                main.Cycles.SynodicStartYear = startYear;
                main.Cycles.SynodicEndYear = endYear;
                return main.FindSynodicCyclesCommand;
            case "MundaneAspects":
                main.Mundane.MAStartYear = startYear;
                main.Mundane.MAEndYear = endYear;
                return main.FindMundaneAspectsCommand;
            case "Eclipses":
                main.Mundane.EclipseStartYear = startYear;
                main.Mundane.EclipseEndYear = endYear;
                return main.FindEclipsesCommand;
            case "ReturnIngressTimeline":
                main.ReturnIngressTimeline.StartYear = startYear;
                main.ReturnIngressTimeline.EndYear = endYear;
                return main.CalculateRITimelineCommand;
            default:
                throw new ArgumentOutOfRangeException(nameof(panel), panel, "Unknown series.");
        }
    }

    /// <summary>
    /// A view model whose range-taking engines are doubles that record their calls, with the record and the two
    /// doubles that keep their input.
    /// </summary>
    /// <param name="Main">The view model.</param>
    /// <param name="Calls">The engines called, by name, in order.</param>
    /// <param name="Transit">The transit engine's double.</param>
    /// <param name="Electional">The electional engine's double.</param>
    private sealed record Harness(
        MainViewModel Main, List<string> Calls, TransitDouble Transit, ElectionalDouble Electional)
    {
        /// <summary>
        /// Builds the view model and puts the invented birth chart, person A, on the Chart panel through
        /// Calculate &amp; Save, the stub engine casting it.
        /// </summary>
        /// <param name="transitRefusal">The exception the transit engine throws, else a plain marker.</param>
        /// <returns>The harness.</returns>
        public static async Task<Harness> WithAChartAsync(Exception? transitRefusal = null)
        {
            List<string> calls = [];
            TransitDouble transit = new(calls, transitRefusal);
            ElectionalDouble electional = new(calls);
            MainViewModel main = new(
                new StubChartEngine(),
                new StubChartRepository(),
                new StubEphemerisService(),
                new StubSynastryService(),
                new StubCompositeService(),
                new StubSolarReturnService(),
                new LunarReturnDouble(calls),
                new StubPlanetaryReturnService(),
                new StubCycleTimelineService(),
                new GreatConjunctionDouble(calls),
                new StubIngressService(),
                new StubLunationService(),
                new StubRelocationService(),
                new MundaneAspectDouble(calls),
                new EclipseDouble(calls),
                new ReturnIngressTimelineDouble(calls),
                new SynodicCycleDouble(calls),
                transit,
                electional,
                new StubAstrocartographyService(),
                new TestDialogService(),
                new OperationCancellationRegistry(),
                new LoggerConfiguration().CreateLogger(),
                primaryDirectionService: new StubPrimaryDirectionService(),
                davisonService: new StubDavisonService());

            main.ChartEntry.Name = "Eleanor Hartwell";
            main.ChartEntry.EventType = "Birth";
            main.ChartEntry.EventDate = new DateTime(1947, 10, 5);
            main.ChartEntry.EventTime = "09:30:00";
            main.ChartEntry.TimeZone = "America/New_York";
            main.ChartEntry.LocationName = "Harrisburg, Pennsylvania, United States";
            main.ChartEntry.LatitudeText = "40.2737";
            main.ChartEntry.LongitudeText = "-76.8844";
            await RunAsync(main.CalculateAndSaveCommand);
            main.LastResult.Should().NotBeNull(main.StatusText);

            return new Harness(main, calls, transit, electional);
        }
    }

    /// <summary>A transit engine that records its call and its input, then throws.</summary>
    /// <param name="calls">The record of engines called.</param>
    /// <param name="refusal">The exception to throw, else a plain marker.</param>
    private sealed class TransitDouble(List<string> calls, Exception? refusal) : ITransitService
    {
        /// <summary>Gets the input of the last call, or null before the first.</summary>
        public TransitInput? Input { get; private set; }

        /// <inheritdoc />
        public TransitResult Calculate(TransitInput input, CancellationToken cancellationToken = default)
        {
            calls.Add("Transits");
            Input = input;
            throw refusal ?? new InvalidOperationException("The transit engine was called.");
        }
    }

    /// <summary>An electional engine that records its call and its input, then throws.</summary>
    /// <param name="calls">The record of engines called.</param>
    private sealed class ElectionalDouble(List<string> calls) : IElectionalService
    {
        /// <summary>Gets the input of the last call, or null before the first.</summary>
        public ElectionalInput? Input { get; private set; }

        /// <inheritdoc />
        public ElectionalResult Calculate(ElectionalInput input, CancellationToken cancellationToken = default)
        {
            calls.Add("Electional");
            Input = input;
            throw new InvalidOperationException("The electional engine was called.");
        }
    }

    /// <summary>A lunar return engine that records a range search, then answers as the stub does.</summary>
    /// <param name="calls">The record of engines called.</param>
    private sealed class LunarReturnDouble(List<string> calls) : ILunarReturnService
    {
        private readonly StubLunarReturnService _inner = new();

        /// <inheritdoc />
        public IReadOnlyList<LunarReturnResult> Calculate(
            ChartResult natalChart, DateTime startDate, DateTime endDate, double latitude, double longitude,
            string locationName, string timeZone, CancellationToken cancellationToken = default)
        {
            calls.Add("LunarReturns");
            return _inner.Calculate(
                natalChart, startDate, endDate, latitude, longitude, locationName, timeZone, cancellationToken);
        }

        /// <inheritdoc />
        public LunarReturnResult CalculateNext(
            ChartResult natalChart, DateTime onOrAfter, double latitude, double longitude,
            string locationName, string timeZone, CancellationToken cancellationToken = default) =>
            _inner.CalculateNext(natalChart, onOrAfter, latitude, longitude, locationName, timeZone, cancellationToken);
    }

    /// <summary>A great-conjunction engine that records a series search, then answers as the stub does.</summary>
    /// <param name="calls">The record of engines called.</param>
    private sealed class GreatConjunctionDouble(List<string> calls) : IGreatConjunctionService
    {
        private readonly StubGreatConjunctionService _inner = new();

        /// <inheritdoc />
        public GreatConjunctionSeries FindConjunctions(
            int startYear, int endYear, CancellationToken cancellationToken = default)
        {
            calls.Add("GreatConjunctions");
            return _inner.FindConjunctions(startYear, endYear, cancellationToken);
        }

        /// <inheritdoc />
        public GreatConjunctionResult FindNearest(int year, CancellationToken cancellationToken = default) =>
            _inner.FindNearest(year, cancellationToken);

        /// <inheritdoc />
        public ChartResult CastConjunctionChart(
            GreatConjunctionResult conjunction, double latitude, double longitude,
            string locationName, string timeZone, CancellationToken cancellationToken = default) =>
            _inner.CastConjunctionChart(conjunction, latitude, longitude, locationName, timeZone, cancellationToken);
    }

    /// <summary>A synodic-cycle engine that records its call, then answers as the stub does.</summary>
    /// <param name="calls">The record of engines called.</param>
    private sealed class SynodicCycleDouble(List<string> calls) : ISynodicCycleService
    {
        private readonly StubSynodicCycleService _inner = new();

        /// <inheritdoc />
        public SynodicCycleSeries FindConjunctions(
            CelestialBodyId bodyA, CelestialBodyId bodyB, int startYear, int endYear,
            CancellationToken cancellationToken = default)
        {
            calls.Add("SynodicCycles");
            return _inner.FindConjunctions(bodyA, bodyB, startYear, endYear, cancellationToken);
        }
    }

    /// <summary>A mundane-aspect engine that records its call, then answers as the stub does.</summary>
    /// <param name="calls">The record of engines called.</param>
    private sealed class MundaneAspectDouble(List<string> calls) : IMundaneAspectService
    {
        private readonly StubMundaneAspectService _inner = new();

        /// <inheritdoc />
        public MundaneAspectSeries FindAspects(
            CelestialBodyId body1, CelestialBodyId body2, string harmonicMode, int startYear, int endYear,
            CancellationToken cancellationToken = default)
        {
            calls.Add("MundaneAspects");
            return _inner.FindAspects(body1, body2, harmonicMode, startYear, endYear, cancellationToken);
        }
    }

    /// <summary>An eclipse engine that records its call, then answers as the stub does.</summary>
    /// <param name="calls">The record of engines called.</param>
    private sealed class EclipseDouble(List<string> calls) : IEclipseService
    {
        private readonly StubEclipseService _inner = new();

        /// <inheritdoc />
        public EclipseSeries FindEclipses(int startYear, int endYear, CancellationToken cancellationToken = default)
        {
            calls.Add("Eclipses");
            return _inner.FindEclipses(startYear, endYear, cancellationToken);
        }

        /// <inheritdoc />
        public EclipseSeries FindSolarEclipses(int startYear, int endYear, CancellationToken cancellationToken = default)
        {
            calls.Add("Eclipses");
            return _inner.FindSolarEclipses(startYear, endYear, cancellationToken);
        }

        /// <inheritdoc />
        public EclipseSeries FindLunarEclipses(int startYear, int endYear, CancellationToken cancellationToken = default)
        {
            calls.Add("Eclipses");
            return _inner.FindLunarEclipses(startYear, endYear, cancellationToken);
        }
    }

    /// <summary>A return and ingress timeline engine that records its call, then answers as the stub does.</summary>
    /// <param name="calls">The record of engines called.</param>
    private sealed class ReturnIngressTimelineDouble(List<string> calls) : IReturnIngressTimelineService
    {
        private readonly StubReturnIngressTimelineService _inner = new();

        /// <inheritdoc />
        public ReturnIngressTimeline Calculate(
            ChartResult? natalChart, int startYear, int endYear, bool includeReturns = true,
            bool includeIngresses = true, CelestialBodyId[]? returnPlanets = null,
            CelestialBodyId[]? ingressPlanets = null, CancellationToken cancellationToken = default)
        {
            calls.Add("ReturnIngressTimeline");
            return _inner.Calculate(
                natalChart, startYear, endYear, includeReturns, includeIngresses, returnPlanets, ingressPlanets,
                cancellationToken);
        }
    }

    #endregion
}
