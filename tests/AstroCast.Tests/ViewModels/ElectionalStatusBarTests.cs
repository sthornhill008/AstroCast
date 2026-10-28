using System.Globalization;

using AstroCast.Core.Data;
using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models.Electional;
using AstroCast.Services;
using AstroCast.Tests.Fixtures;
using AstroCast.ViewModels;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.ViewModels;

/// <summary>
/// Pins the status line the electional command leaves (branch Q, 1.3.3): the best candidate's score as the export
/// prints it, in the screen's culture, and a refusal without the framework's parameter suffix.
/// </summary>
/// <remarks>
/// <para>
/// Until 1.3.3 the line read "(score 82/100)": a whole number, rounding a midpoint to even (88.5 as "88"), against a
/// scale the score does not have, where the export now prints 82.25. A refusal read "Electional error: … (Parameter
/// 'input')", the framework's suffix to an <see cref="ArgumentException"/>'s message, which says nothing to the user.
/// </para>
/// <para>
/// The command runs against a service double, so no ephemeris is read; the line's date is printed by the framework
/// in the culture under test, as the command prints it.
/// </para>
/// </remarks>
[Trait("Category", "Unit")]
public sealed class ElectionalStatusBarTests
{
    /// <summary>The best candidate's instant on the event's clock: Fri 12 Jun 2026 5:00 PM (Richmond's first).</summary>
    private static readonly DateTime BestLocal = new(2026, 6, 12, 17, 0, 0, DateTimeKind.Unspecified);

    /// <summary>
    /// The polar refusal's message as the service builds it (branch O3), without the framework's suffix.
    /// </summary>
    private const string PolarRefusal =
        "Electional candidates are cast in the natal chart's house system, Placidus, which has no houses at latitude "
        + "69.6492° N. Choose a natal chart in a house system that exists there, such as Equal, Whole Sign or "
        + "Regiomontanus.";

    /// <summary>
    /// A completed search: the status line gives the best candidate's score as the export prints it, to two decimals
    /// at most and with no "/100", in the screen's culture: "82.25" under en-US, "82,25" under de-DE.
    /// </summary>
    /// <param name="culture">The culture the form runs under.</param>
    /// <param name="score">The score as the line prints it.</param>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Theory]
    [InlineData("en-US", "82.25")]
    [InlineData("de-DE", "82,25")]
    public async Task CalculateElectional_Completed_StatusLineGivesTheBestScoreExactly(string culture, string score)
    {
        using CultureScope scope = new(culture);
        MainViewModel main = FilledForm(new ElectionalServiceDouble(Completed));

        await RunAsync(main);

        string best = BestLocal.ToString("ddd, dd MMM yyyy h:mm tt", CultureInfo.GetCultureInfo(culture));
        main.StatusText.Should().Be($"Electional complete — best: {best} (score {score})");
    }

    /// <summary>
    /// A refusal: an <see cref="ArgumentException"/> naming its parameter prints its message without the framework's
    /// " (Parameter 'input')"; one naming no parameter prints its message unchanged, even when the text itself ends so.
    /// </summary>
    /// <param name="paramName">The exception's parameter name, or <see langword="null"/> for none.</param>
    /// <param name="message">The message the exception is constructed with.</param>
    /// <param name="expected">The status line.</param>
    /// <returns>A task that completes when the assertion has run.</returns>
    [Theory]
    [InlineData("input", PolarRefusal, "Electional error: " + PolarRefusal)]
    [InlineData(null, PolarRefusal + " (Parameter 'input')", "Electional error: " + PolarRefusal + " (Parameter 'input')")]
    public async Task CalculateElectional_Refused_StatusLineGivesTheMessageWithoutTheParameterSuffix(
        string? paramName, string message, string expected)
    {
        MainViewModel main = FilledForm(new ElectionalServiceDouble(_ => throw new ArgumentException(message, paramName)));

        await RunAsync(main);

        main.StatusText.Should().Be(expected);
        main.Electional.ElectionalResult.Should().BeNull("a refused search leaves no result");
    }

    /// <summary>Runs the electional command and waits for it to finish.</summary>
    /// <param name="main">The view model.</param>
    /// <returns>A task that completes when the command has finished.</returns>
    private static async Task RunAsync(MainViewModel main)
    {
        main.CalculateElectionalCommand.CanExecute(null).Should().BeTrue("the form is filled in");
        main.CalculateElectionalCommand.Execute(null);

        int waited = 0;
        while (main.CalculateElectionalCommand.IsExecuting && waited < 5000)
        {
            await Task.Delay(10);
            waited += 10;
        }

        main.CalculateElectionalCommand.IsExecuting.Should().BeFalse("the command finishes within 5 s");
    }

    /// <summary>
    /// A view model whose electional search runs against the given service, with a natal chart chosen from the
    /// dropdown (the stub engine casts it) and the event's fields filled in.
    /// </summary>
    /// <param name="electionalService">The electional service.</param>
    /// <returns>The view model.</returns>
    private static MainViewModel FilledForm(IElectionalService electionalService)
    {
        MainViewModel main = new(
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
            electionalService,
            new StubAstrocartographyService(),
            new TestDialogService(),
            new OperationCancellationRegistry(),
            new LoggerConfiguration().CreateLogger());

        main.Electional.SelectedNatalChart = new ChartEntity
        {
            Name = "Status bar",
            EventType = "Birth",
            LocalDateTime = "2000-01-01T07:00:00",
            TimeZone = "America/New_York",
            UtcDateTime = "2000-01-01T12:00:00Z",
            JulianDay = 2451545.0,
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            CreatedAt = "2026-10-10T12:00:00Z",
            UpdatedAt = "2026-10-10T12:00:00Z",
        };
        main.Electional.Goal = "Best time to visit Doctor";
        main.Electional.LocationName = "Richmond, VA";
        main.Electional.LatitudeText = "37.5385";
        main.Electional.LongitudeText = "-77.4343";
        main.Electional.TimeZone = "America/New_York";

        return main;
    }

    /// <summary>
    /// A completed search with one candidate, Richmond's first: Fri 12 Jun 2026 5:00 PM, scoring 82.25 (Moon 90,
    /// Ascendant 50, Significator 85, Natal Harmony 50, Malefic Avoidance 100, Mercury 80, Planetary Hour 100, with
    /// Other's weights).
    /// </summary>
    /// <param name="input">The search's input.</param>
    /// <returns>The result.</returns>
    private static ElectionalResult Completed(ElectionalInput input)
    {
        ScoreBreakdown breakdown = new()
        {
            MoonScore = 90.0,
            AscendantScore = 50.0,
            SignificatorScore = 85.0,
            NatalHarmonyScore = 50.0,
            MaleficAvoidanceScore = 100.0,
            MercuryScore = 80.0,
            PlanetaryHourScore = 100.0,
            Weights = ElectionalWeights.GetWeights(ElectionType.Other),
            ElectionTypeName = nameof(ElectionType.Other),
        };

        return new ElectionalResult
        {
            Input = input,
            NatalChart = input.NatalChart,
            Candidates =
            [
                new CandidateWindow
                {
                    Rank = 1,
                    LocalDateTime = BestLocal,
                    Chart = input.NatalChart,
                    Moon = new MoonCondition
                    {
                        Longitude = 45.0,
                        Sign = "Taurus",
                        Phase = MoonPhaseType.WaningCrescent,
                        PhasePercent = 20.0,
                        PhasePosition = "early",
                        IsVoidOfCourse = false,
                        IsInViaCombusta = false,
                        DailyMotion = 13.0,
                    },
                    PlanetaryHour = new PlanetaryHour
                    {
                        RulingPlanet = "Moon",
                        HourNumber = 10,
                        IsDaytime = true,
                        HourStart = BestLocal,
                        HourEnd = BestLocal.AddMinutes(75),
                    },
                    Score = breakdown.OverallScore,
                    ScoreBreakdown = breakdown,
                    NatalHarmonyAspects = [],
                    Strengths = [],
                    Compromises = [],
                },
            ],
            RequestedCandidateCount = 1,
            SamplesWithoutPlanetaryHour = 0,
            DisqualifiedPeriods = [],
            VoidOfCoursePeriods = [],
            CalculatedAt = new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc),
        };
    }

    /// <summary>An electional service that answers every search with the given function.</summary>
    /// <param name="calculate">The search's answer: a result, or an exception thrown.</param>
    private sealed class ElectionalServiceDouble(Func<ElectionalInput, ElectionalResult> calculate) : IElectionalService
    {
        /// <inheritdoc />
        public ElectionalResult Calculate(ElectionalInput input, CancellationToken cancellationToken = default) =>
            calculate(input);
    }
}
