using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Models.PrimaryDirections;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;
using AstroCast.Core.Services.PrimaryDirections;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Formatting.Golden;

/// <summary>
/// Golden-file tests for the pair, transit, relocation, and electional
/// Markdown formatters. Every test drives the formatter from a deterministic
/// input (the J2000 NYC canonical chart, optionally paired with the
/// secondary natal fixture, over a hardcoded date window) so any physical
/// drift surfaces as a diff against the checked-in golden.
/// </summary>
/// <remarks>
/// <para>
/// All ephemeris work is done through the shared
/// <see cref="EphemerisFixture"/> so Swiss Ephemeris is initialised exactly
/// once for the test run. Tests are categorised as integration because they
/// depend on live ephemeris data.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class RelationshipAndTransitFormatterGoldenTests : GoldenFormatterTestBase
{
    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger;

    /// <summary>
    /// Receives the shared ephemeris fixture used by every test in this class.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public RelationshipAndTransitFormatterGoldenTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _logger = new LoggerConfiguration().CreateLogger();
    }

    /// <summary>
    /// The J2000 NYC natal cast with the switch as given (branch AsteroidsExport, 1.6.0): with it off, exactly
    /// <see cref="GoldenFixtures.J2000Nyc"/>; with it on, the same chart carrying the five.
    /// </summary>
    /// <param name="includeMinorPoints">Whether the chart carries Ceres, Pallas, Juno, Vesta and Lilith.</param>
    /// <returns>The chart.</returns>
    private ChartResult J2000Nyc(bool includeMinorPoints) =>
        Cast(GoldenFixtures.J2000NycInput(includeMinorPoints), includeMinorPoints);

    /// <summary>The secondary natal cast with the switch as given; with it off, exactly <see cref="GoldenFixtures.SecondaryNatal"/>.</summary>
    /// <param name="includeMinorPoints">Whether the chart carries the five.</param>
    /// <returns>The chart.</returns>
    private ChartResult SecondaryNatal(bool includeMinorPoints) =>
        Cast(GoldenFixtures.SecondaryNatalInput(includeMinorPoints), includeMinorPoints);

    /// <summary>Casts an input with a fresh engine and checks that it carries the five exactly when the switch is on.</summary>
    /// <param name="input">The input.</param>
    /// <param name="includeMinorPoints">The input's switch.</param>
    /// <returns>The chart.</returns>
    private ChartResult Cast(ChartInput input, bool includeMinorPoints)
    {
        ChartResult chart = GoldenFixtures.CreateChartEngine(_fixture).Calculate(input);
        chart.MinorPoints.Should().HaveCount(includeMinorPoints ? 5 : 0, "the chart carries the five exactly when the switch is on");
        return chart;
    }

    /// <summary>
    /// Verifies <see cref="SynastryMarkdownFormatter.ToMarkdown"/> renders the
    /// synastry between J2000 NYC and the secondary natal identically to the
    /// checked-in golden. Any drift in inter-chart aspect orbs, house
    /// overlays, or relationship-indicator logic surfaces as a readable diff.
    /// </summary>
    [Fact]
    public void SynastryMarkdownFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(SynastryMarkdown(includeMinorPoints: false), "SynastryMarkdownFormatter_J2000_Secondary.golden.md");
    }

    /// <summary>The synastry export of J2000 NYC and the secondary natal, both cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether both charts carry the five.</param>
    /// <returns>The export.</returns>
    private string SynastryMarkdown(bool includeMinorPoints)
    {
        ChartResult personA = J2000Nyc(includeMinorPoints);
        ChartResult personB = SecondaryNatal(includeMinorPoints);

        SynastryService service = new(_logger);

        SynastryResult result = service.Calculate(personA, personB);

        return SynastryMarkdownFormatter.ToMarkdown(result, GoldenFixtures.GoldenClock());
    }

    /// <summary>
    /// Verifies <see cref="CompositeMarkdownFormatter.ToMarkdown"/> renders the
    /// composite chart for the J2000 NYC × secondary pair identically to the
    /// checked-in golden.
    /// </summary>
    [Fact]
    public void CompositeMarkdownFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(CompositeMarkdown(includeMinorPoints: false), "CompositeMarkdownFormatter_J2000_Secondary.golden.md");
    }

    /// <summary>The composite export of J2000 NYC and the secondary natal, both cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether both charts carry the five.</param>
    /// <returns>The export.</returns>
    private string CompositeMarkdown(bool includeMinorPoints)
    {
        ChartResult chartA = J2000Nyc(includeMinorPoints);
        ChartResult chartB = SecondaryNatal(includeMinorPoints);

        DignityService dignity = new();
        SectService sect = new();
        AspectService aspects = new(_fixture.EphemerisService, _logger);
        CompositeService service = new(
            _fixture.EphemerisService,
            aspects,
            new MidpointService(_logger),
            dignity,
            new MoonConditionService(),
            new ChartContextService(dignity, sect),
            new FixedStarService(_fixture.EphemerisService, _logger),
            _logger);

        CompositeResult result = service.Calculate(chartA, chartB);

        return CompositeMarkdownFormatter.ToMarkdown(result);
    }

    /// <summary>
    /// Verifies <see cref="RelocationMarkdownFormatter.ToMarkdown"/> renders
    /// the J2000 NYC natal relocated to Los Angeles identically to the
    /// checked-in golden.
    /// </summary>
    [Fact]
    public void RelocationMarkdownFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(RelocationMarkdown(includeMinorPoints: false), "RelocationMarkdownFormatter_J2000NYC_to_LA.golden.md");
    }

    /// <summary>The relocation export of J2000 NYC to Los Angeles, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string RelocationMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = J2000Nyc(includeMinorPoints);

        RelocationService service = new(
            GoldenFixtures.CreateChartEngine(_fixture),
            _logger);

        RelocationInput target = new()
        {
            Latitude = 34.0522,
            Longitude = -118.2437,
            LocationName = "Los Angeles",
        };

        RelocationResult result = service.Calculate(natal, target);

        return RelocationMarkdownFormatter.ToMarkdown(result, GoldenFixtures.GoldenClock());
    }

    /// <summary>
    /// Verifies <see cref="TransitMarkdownFormatter.ToMarkdown"/> renders the
    /// J2000 NYC natal transits for a single-week window around 2026-04-15
    /// identically to the checked-in golden.
    /// </summary>
    /// <remarks>
    /// A one-week window was chosen to keep the golden compact while still
    /// exercising the major output sections (active transits, house
    /// overlays, midpoints, declination aspects, transit-to-transit).
    /// </remarks>
    [Fact]
    public void TransitMarkdownFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(TransitMarkdown(includeMinorPoints: false), "TransitMarkdownFormatter_J2000NYC_20260415.golden.md");
    }

    /// <summary>The J2000 NYC transit report of 2026-04-12 to 2026-04-18 with the solar arcs and progressions, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string TransitMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = J2000Nyc(includeMinorPoints);

        TransitService service = new(_fixture.EphemerisService, _logger);

        TransitInput input = new()
        {
            NatalChart = natal,
            ReportStartDate = new DateOnly(2026, 4, 12),
            ReportEndDate = new DateOnly(2026, 4, 18),
            IncludeMinorAspects = false,
            IncludeSolarArcs = true,
            IncludeSecondaryProgressions = true,
        };

        TransitResult result = service.Calculate(input);

        return TransitMarkdownFormatter.ToMarkdown(result, timeProvider: GoldenFixtures.GoldenClock());
    }

    /// <summary>
    /// Verifies <see cref="TransitMarkdownFormatter.ToMarkdown"/> renders the J2000 NYC solar arcs and
    /// secondary progressions for 2026, without the transit scan, identically to the checked-in golden
    /// (branch Modes, 1.4.0 item 2): the Solar Arc and Progressions Report, its positions and aspects at
    /// 2026-01-01 and its dated hits over the year (Tier B, arcs_truth.py: 37 solar arc hits, 9 to points
    /// and 28 to midpoints, and 9 progressed hits).
    /// </summary>
    [Fact]
    public void SolarArcMarkdownFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(SolarArcMarkdown(includeMinorPoints: false), "SolarArcMarkdownFormatter_J2000NYC_2026.golden.md");
    }

    /// <summary>The J2000 NYC solar arc and progressions report of 2026, without the transit scan, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string SolarArcMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = J2000Nyc(includeMinorPoints);

        TransitService service = new(_fixture.EphemerisService, _logger);

        TransitInput input = new()
        {
            NatalChart = natal,
            ReportStartDate = new DateOnly(2026, 1, 1),
            ReportEndDate = new DateOnly(2026, 12, 31),
            IncludeMinorAspects = false,
            IncludeTransits = false,
            IncludeSolarArcs = true,
            IncludeSecondaryProgressions = true,
        };

        TransitResult result = service.Calculate(input);

        return TransitMarkdownFormatter.ToMarkdown(result, timeProvider: GoldenFixtures.GoldenClock());
    }

    /// <summary>
    /// Verifies <see cref="PrimaryDirectionMarkdownFormatter.ToMarkdown"/> renders J2000 NYC's whole-life
    /// primary directions (Naibod, the angles, lights and Part of Fortune, to age 100, an uncertainty of
    /// 4 minutes) identically to the checked-in golden (branch PrimaryExport, 1.5.0): the 603 hits of Tier B's
    /// j2000nyc_naibod_u4 list, with their months per minute and ranges, and the 60 left out.
    /// </summary>
    [Fact]
    public void PrimaryDirectionMarkdownFormatter_J2000Nyc_MatchesGolden()
    {
        AssertGolden(PrimaryDirectionJ2000NycMarkdown(includeMinorPoints: false), "PrimaryDirectionMarkdownFormatter_J2000NYC.golden.md");
    }

    /// <summary>J2000 NYC's whole-life primary directions, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string PrimaryDirectionJ2000NycMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = J2000Nyc(includeMinorPoints);
        PrimaryDirectionSettings settings = new() { BirthTimeUncertaintyMinutes = 4 };

        PrimaryDirectionResult result = new PrimaryDirectionService(_fixture.EphemerisService).Calculate(natal, settings);

        return PrimaryDirectionMarkdownFormatter.ToMarkdown(result, GoldenFixtures.GoldenClock());
    }

    /// <summary>
    /// Verifies <see cref="PrimaryDirectionMarkdownFormatter.ToMarkdown"/> renders person A's whole-life
    /// primary directions (Naibod, no uncertainty, the birth-time source given) identically to the
    /// checked-in golden: the 538 hits of Tier B's hartwell_naibod list, no Range column, no birth-time
    /// caution, and the 60 left out.
    /// </summary>
    [Fact]
    public void PrimaryDirectionMarkdownFormatter_Hartwell_MatchesGolden()
    {
        AssertGolden(PrimaryDirectionHartwellMarkdown(includeMinorPoints: false), "PrimaryDirectionMarkdownFormatter_Hartwell.golden.md");
    }

    /// <summary>Person A's whole-life primary directions, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string PrimaryDirectionHartwellMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = Cast(GoldenFixtures.HartwellInput("Birth Certificate", includeMinorPoints), includeMinorPoints);

        PrimaryDirectionResult result = new PrimaryDirectionService(_fixture.EphemerisService).Calculate(natal, PrimaryDirectionSettings.Default);

        return PrimaryDirectionMarkdownFormatter.ToMarkdown(result, GoldenFixtures.GoldenClock());
    }

    /// <summary>
    /// Verifies <see cref="TransitMarkdownFormatter.ToMarkdown"/> renders J2000 NYC's primary directions for
    /// 2026 as the transit report's fourth technique, the other three off (branch PrimaryExport, 1.5.0):
    /// the Primary Directions Report with the section's four hits of the year (Tier B, j2000nyc_naibod_u4),
    /// Naibod, an uncertainty of 4 minutes.
    /// </summary>
    [Fact]
    public void TransitMarkdownFormatter_PrimaryDirections2026_MatchesGolden()
    {
        ChartResult natal = GoldenFixtures.J2000Nyc(_fixture);

        TransitService service = new(_fixture.EphemerisService, _logger);

        TransitInput input = new()
        {
            NatalChart = natal,
            ReportStartDate = new DateOnly(2026, 1, 1),
            ReportEndDate = new DateOnly(2026, 12, 31),
            IncludeMinorAspects = false,
            IncludeTransits = false,
            IncludeSolarArcs = false,
            IncludeSecondaryProgressions = false,
            IncludePrimaryDirections = true,
            PrimaryDirectionSettings = new PrimaryDirectionSettings { BirthTimeUncertaintyMinutes = 4 },
        };

        TransitResult result = service.Calculate(input);

        string md = TransitMarkdownFormatter.ToMarkdown(result, timeProvider: GoldenFixtures.GoldenClock());

        AssertGolden(md, "TransitMarkdownFormatter_J2000NYC_2026_PrimaryDirections.golden.md");
    }

    /// <summary>
    /// Verifies <see cref="TransitTimelineMarkdownFormatter.ToMarkdown"/>
    /// renders the J2000 NYC natal transits for a one-month window
    /// (2026-04-01 to 2026-04-30) identically to the checked-in golden.
    /// </summary>
    /// <remarks>
    /// The timeline formatter's output scales with event count. A one-month
    /// window keeps the golden readable (typically 2,000–4,000 lines) while
    /// still exercising the clustering, cross-section, and timeline rendering.
    /// </remarks>
    [Fact]
    public void TransitTimelineMarkdownFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(TransitTimelineMarkdown(includeMinorPoints: false), "TransitTimelineMarkdownFormatter_J2000NYC_2026April.golden.md");
    }

    /// <summary>The J2000 NYC transit timeline of April 2026, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string TransitTimelineMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = J2000Nyc(includeMinorPoints);

        TransitService service = new(_fixture.EphemerisService, _logger);

        TransitInput input = new()
        {
            NatalChart = natal,
            ReportStartDate = new DateOnly(2026, 4, 1),
            ReportEndDate = new DateOnly(2026, 4, 30),
            IncludeMinorAspects = false,
            IncludeSolarArcs = true,
            IncludeSecondaryProgressions = true,
        };

        TransitResult result = service.Calculate(input);

        return TransitTimelineMarkdownFormatter.ToMarkdown(result, timeProvider: GoldenFixtures.GoldenClock());
    }

    /// <summary>
    /// Verifies <see cref="DailyHoroscopeMarkdownFormatter.ToMarkdown"/> renders the J2000 NYC
    /// natal's daily horoscope for 2026-10-20 to 2026-11-05, its days built by
    /// <see cref="DailyHoroscopeService.BuildDays"/> on America/New_York as the heads build them,
    /// identically to the checked-in golden.
    /// </summary>
    /// <remarks>
    /// The window holds four sign changes of the Moon after noon (23, 25, 27 and 29 October), an
    /// exit filed on the local day before its UT date (Saturn trine the natal Pluto, 21 October
    /// 23:17 EDT), three exact passes, entries and exits with their times, and the 25-hour day of
    /// 1 November. Major aspects, progressions off.
    /// </remarks>
    [Fact]
    public void DailyHoroscopeMarkdownFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(DailyHoroscopeMarkdown(includeMinorPoints: false), "DailyHoroscopeMarkdownFormatter_J2000NYC_20261020_20261105.golden.md");
    }

    /// <summary>The daily horoscope of J2000 NYC for 2026-10-20 to 2026-11-05, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string DailyHoroscopeMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = J2000Nyc(includeMinorPoints);

        TransitService service = new(_fixture.EphemerisService, _logger);

        TransitInput input = new()
        {
            NatalChart = natal,
            ReportStartDate = new DateOnly(2026, 10, 20),
            ReportEndDate = new DateOnly(2026, 11, 5),
            IncludeMinorAspects = false,
            IncludeSolarArcs = false,
            IncludeSecondaryProgressions = false,
        };

        TransitResult result = service.Calculate(input);
        string zone = natal.Input.TimeZone;
        IReadOnlyList<DailyHoroscopeDay> days = new DailyHoroscopeService(_fixture.EphemerisService).BuildDays(result, zone);

        return DailyHoroscopeMarkdownFormatter.ToMarkdown(result, days, null, null, zone);
    }

    /// <summary>
    /// Verifies <see cref="ElectionalMarkdownFormatter.ToMarkdown"/> renders
    /// a 5-day electional search for the J2000 NYC natal identically to the
    /// checked-in golden.
    /// </summary>
    /// <remarks>
    /// A short window (2026-04-20 to 2026-04-25), fixed target location
    /// (NYC), and default candidate count (3) keep the output deterministic
    /// and the golden modest. The election type is
    /// <see cref="ElectionType.BusinessLaunch"/>, chosen because its scoring
    /// exercises a broad spread of chart factors.
    /// </remarks>
    [Fact]
    public void ElectionalMarkdownFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(ElectionalMarkdown(includeMinorPoints: false), "ElectionalMarkdownFormatter_J2000NYC_20260420_5day.golden.md");
    }

    /// <summary>The 5-day electional search of J2000 NYC, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string ElectionalMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = J2000Nyc(includeMinorPoints);

        ElectionalService service = new(
            _fixture.EphemerisService,
            GoldenFixtures.CreateChartEngine(_fixture));

        ElectionalInput input = new()
        {
            NatalChart = natal,
            ElectionType = ElectionType.BusinessLaunch,
            ElectionGoal = "Launch a new software product.",
            RangeStart = new DateOnly(2026, 4, 20),
            RangeEnd = new DateOnly(2026, 4, 25),
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            TimeZone = "America/New_York",
            CandidateCount = 3,
        };

        ElectionalResult result = service.Calculate(input);

        return ElectionalMarkdownFormatter.ToMarkdown(result);
    }

    /// <summary>
    /// T10 (2026-06-10 live-run review): full-export snapshot of the Doctor
    /// Visit electional fixture (Richmond VA, window containing the
    /// 2026-06-12 live-run candidate moments). Pins the corrected Malefic
    /// Avoidance scores, the recomputed Overall, the unified D°MM′ orb
    /// sites, and the untouched remainder of the report.
    /// </summary>
    /// <remarks>
    /// A 4-day window (2026-06-11 to 2026-06-14) keeps the golden compact
    /// while containing both live-run candidate moments. Election type
    /// <see cref="ElectionType.Other"/> matches the live run's working type
    /// (Election Type taxonomy emission is out of scope per the review).
    /// </remarks>
    [Fact]
    public void ElectionalMarkdownFormatter_DoctorVisitRichmond_MatchesGolden()
    {
        AssertGolden(ElectionalDoctorVisitRichmondMarkdown(includeMinorPoints: false), "ElectionalMarkdownFormatter_DoctorVisit_Richmond_20260611_4day.golden.md");
    }

    /// <summary>The Doctor Visit electional search at Richmond, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string ElectionalDoctorVisitRichmondMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = J2000Nyc(includeMinorPoints);

        ElectionalService service = new(
            _fixture.EphemerisService,
            GoldenFixtures.CreateChartEngine(_fixture));

        ElectionalInput input = new()
        {
            NatalChart = natal,
            ElectionType = ElectionType.Other,
            ElectionGoal = "Best time to visit Doctor",
            RangeStart = new DateOnly(2026, 6, 11),
            RangeEnd = new DateOnly(2026, 6, 14),
            Latitude = 37.5385,
            Longitude = -77.4343,
            LocationName = "Richmond, VA",
            TimeZone = "America/New_York",
            CandidateCount = 2,
        };

        ElectionalResult result = service.Calculate(input);

        return ElectionalMarkdownFormatter.ToMarkdown(result);
    }

    // Branch AsteroidsExport (1.6.0): the same fixtures with the switch on. The synastry, composite and
    // relocation exports gain the five's sections, in goldens of their own suffixed _MinorPoints (R6, scoping
    // decisions 4 and 8); the daily horoscope, the electional and primary-direction reports print nothing of
    // them and stay byte-identical to their existing goldens (R8).

    /// <summary>
    /// Verifies <see cref="SynastryMarkdownFormatter.ToMarkdown"/> renders the synastry of J2000 NYC and the
    /// secondary natal, both carrying the five, identically to its own golden: each embedded chart's section and
    /// <c>## Inter-Chart Aspects: Asteroids and Lilith</c> after the Inter-Chart Aspects (Tier B's ten rows).
    /// </summary>
    [Fact]
    public void SynastryMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden()
    {
        AssertGolden(SynastryMarkdown(includeMinorPoints: true), "SynastryMarkdownFormatter_J2000_Secondary_MinorPoints.golden.md");
    }

    /// <summary>
    /// Verifies <see cref="CompositeMarkdownFormatter.ToMarkdown"/> renders the composite of J2000 NYC and the
    /// secondary natal, both carrying the five, identically to its own golden: the composite five in the section's
    /// composite form (Tier B's composite lists, Pallas retrograde).
    /// </summary>
    [Fact]
    public void CompositeMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden()
    {
        AssertGolden(CompositeMarkdown(includeMinorPoints: true), "CompositeMarkdownFormatter_J2000_Secondary_MinorPoints.golden.md");
    }

    /// <summary>
    /// Verifies <see cref="RelocationMarkdownFormatter.ToMarkdown"/> renders J2000 NYC relocated to Los Angeles,
    /// the natal carrying the five, identically to its own golden: the relocated chart's section with the place's
    /// houses (Tier B's j2000la lists); the House Changes stay the planets' (scoping decision 8).
    /// </summary>
    [Fact]
    public void RelocationMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden()
    {
        AssertGolden(RelocationMarkdown(includeMinorPoints: true), "RelocationMarkdownFormatter_J2000NYC_to_LA_MinorPoints.golden.md");
    }

    /// <summary>The daily horoscope never prints the five (R8): with the natal carrying them, byte-identical to its existing golden.</summary>
    [Fact]
    public void DailyHoroscopeMarkdownFormatter_SwitchOn_MatchesTheExistingGolden()
    {
        AssertGolden(DailyHoroscopeMarkdown(includeMinorPoints: true), "DailyHoroscopeMarkdownFormatter_J2000NYC_20261020_20261105.golden.md");
    }

    /// <summary>The electional report never prints the five (R8): with the natal carrying them, byte-identical to its existing golden.</summary>
    [Fact]
    public void ElectionalMarkdownFormatter_SwitchOn_MatchesTheExistingGolden()
    {
        AssertGolden(ElectionalMarkdown(includeMinorPoints: true), "ElectionalMarkdownFormatter_J2000NYC_20260420_5day.golden.md");
    }

    /// <summary>The Doctor Visit electional report never prints the five (R8): with the natal carrying them, byte-identical to its existing golden.</summary>
    [Fact]
    public void ElectionalMarkdownFormatter_DoctorVisitRichmond_SwitchOn_MatchesTheExistingGolden()
    {
        AssertGolden(ElectionalDoctorVisitRichmondMarkdown(includeMinorPoints: true), "ElectionalMarkdownFormatter_DoctorVisit_Richmond_20260611_4day.golden.md");
    }

    /// <summary>
    /// The primary-direction report never prints the five, the natal chart it embeds included (R8): J2000 NYC's,
    /// with the natal carrying them, byte-identical to its existing golden.
    /// </summary>
    [Fact]
    public void PrimaryDirectionMarkdownFormatter_J2000Nyc_SwitchOn_MatchesTheExistingGolden()
    {
        AssertGolden(PrimaryDirectionJ2000NycMarkdown(includeMinorPoints: true), "PrimaryDirectionMarkdownFormatter_J2000NYC.golden.md");
    }

    /// <summary>Person A's primary-direction report with the natal carrying the five (R8): byte-identical to its existing golden.</summary>
    [Fact]
    public void PrimaryDirectionMarkdownFormatter_Hartwell_SwitchOn_MatchesTheExistingGolden()
    {
        AssertGolden(PrimaryDirectionHartwellMarkdown(includeMinorPoints: true), "PrimaryDirectionMarkdownFormatter_Hartwell.golden.md");
    }

    // Branch AsteroidsTiming (1.6.0): the natal five as timing targets (R7). The transit report, the solar arc and
    // progressions report and the timeline gain the five's own sections, in goldens of their own suffixed
    // _MinorPoints; with the switch off the three existing goldens are unchanged (R9).

    /// <summary>
    /// Verifies <see cref="TransitMarkdownFormatter.ToMarkdown"/> renders the J2000 NYC transit report of 2026-04-12 to
    /// 2026-04-18, the natal carrying the five, identically to its own golden: the natal section of the five,
    /// <c>## Transits to the Asteroids and Lilith</c> after the Active Transits (Tier B's five rows: Saturn square Juno
    /// and trine Vesta, Pluto trine Ceres and sextile Vesta, the node sextile Juno; Mars's rows dropped by its gate),
    /// and the four technique sub-sections (P Venus square Ceres; four directed aspects; no hit).
    /// </summary>
    [Fact]
    public void TransitMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden()
    {
        AssertGolden(TransitMarkdown(includeMinorPoints: true), "TransitMarkdownFormatter_J2000NYC_20260415_MinorPoints.golden.md");
    }

    /// <summary>
    /// Verifies <see cref="TransitMarkdownFormatter.ToMarkdown"/> renders the J2000 NYC solar arcs and progressions of
    /// 2026, the natal carrying the five, identically to its own golden: the four technique sub-sections of the five
    /// (Tier B: P Mercury opposite Pallas and P Sun sextile Vesta at the start, P Moon sextile Juno and P Venus
    /// square Ceres in the year; six directed aspects at the start, SA Pluto conjunct Juno and SA Venus
    /// sesquiquadrate Pallas in the year).
    /// </summary>
    [Fact]
    public void SolarArcMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden()
    {
        AssertGolden(SolarArcMarkdown(includeMinorPoints: true), "SolarArcMarkdownFormatter_J2000NYC_2026_MinorPoints.golden.md");
    }

    /// <summary>
    /// Verifies <see cref="TransitTimelineMarkdownFormatter.ToMarkdown"/> renders the J2000 NYC timeline of April 2026,
    /// the natal carrying the five, identically to its own golden: <c>## Transit Windows: Asteroids and Lilith</c> and
    /// <c>## Events: Asteroids and Lilith</c> after their twins (Tier B's seven rows), the Clusters the chart's own.
    /// </summary>
    [Fact]
    public void TransitTimelineMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden()
    {
        AssertGolden(TransitTimelineMarkdown(includeMinorPoints: true), "TransitTimelineMarkdownFormatter_J2000NYC_2026April_MinorPoints.golden.md");
    }

    // Each golden again under de-DE, sv-SE and th-TH, compared with the same golden
    // file: the export reads the same whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="SynastryMarkdownFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void SynastryMarkdownFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        SynastryMarkdownFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="CompositeMarkdownFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void CompositeMarkdownFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        CompositeMarkdownFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="RelocationMarkdownFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void RelocationMarkdownFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        RelocationMarkdownFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="TransitMarkdownFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void TransitMarkdownFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        TransitMarkdownFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="SolarArcMarkdownFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void SolarArcMarkdownFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        SolarArcMarkdownFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="TransitTimelineMarkdownFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void TransitTimelineMarkdownFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        TransitTimelineMarkdownFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="DailyHoroscopeMarkdownFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void DailyHoroscopeMarkdownFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        DailyHoroscopeMarkdownFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="ElectionalMarkdownFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ElectionalMarkdownFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ElectionalMarkdownFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="ElectionalMarkdownFormatter_DoctorVisitRichmond_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ElectionalMarkdownFormatter_DoctorVisitRichmond_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ElectionalMarkdownFormatter_DoctorVisitRichmond_MatchesGolden();
    }

    /// <summary>
    /// <see cref="PrimaryDirectionMarkdownFormatter_J2000Nyc_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void PrimaryDirectionMarkdownFormatter_J2000Nyc_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        PrimaryDirectionMarkdownFormatter_J2000Nyc_MatchesGolden();
    }

    /// <summary>
    /// <see cref="PrimaryDirectionMarkdownFormatter_Hartwell_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void PrimaryDirectionMarkdownFormatter_Hartwell_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        PrimaryDirectionMarkdownFormatter_Hartwell_MatchesGolden();
    }

    /// <summary>
    /// <see cref="TransitMarkdownFormatter_PrimaryDirections2026_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void TransitMarkdownFormatter_PrimaryDirections2026_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        TransitMarkdownFormatter_PrimaryDirections2026_MatchesGolden();
    }

    /// <summary>
    /// <see cref="SynastryMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void SynastryMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        SynastryMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden();
    }

    /// <summary>
    /// <see cref="CompositeMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void CompositeMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        CompositeMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden();
    }

    /// <summary>
    /// <see cref="RelocationMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void RelocationMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        RelocationMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden();
    }

    /// <summary>
    /// <see cref="TransitMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void TransitMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        TransitMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden();
    }

    /// <summary>
    /// <see cref="SolarArcMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void SolarArcMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        SolarArcMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden();
    }

    /// <summary>
    /// <see cref="TransitTimelineMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void TransitTimelineMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        TransitTimelineMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden();
    }

    /// <summary>
    /// Verifies <see cref="DavisonMarkdownFormatter.ToMarkdown"/> renders the Davison chart of the J2000 NYC × secondary
    /// pair identically to the checked-in golden: the midpoint moment 1985-03-24 15:30:00 UT at (37.3825, −96.1249),
    /// the chart complete (branch Davison, 1.7.0).
    /// </summary>
    [Fact]
    public void DavisonMarkdownFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(DavisonMarkdown(includeMinorPoints: false), "DavisonMarkdownFormatter_J2000_Secondary.golden.md");
    }

    /// <summary>
    /// Verifies <see cref="DavisonMarkdownFormatter.ToMarkdown"/> renders the Davison chart of the pair with the switch on
    /// identically to its own golden: the chart's own five in their section (branch Davison, 1.7.0).
    /// </summary>
    [Fact]
    public void DavisonMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden()
    {
        AssertGolden(DavisonMarkdown(includeMinorPoints: true), "DavisonMarkdownFormatter_J2000_Secondary_MinorPoints.golden.md");
    }

    /// <summary>The Davison export of J2000 NYC and the secondary natal, both cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">The switch, for the two sources and the Davison chart.</param>
    /// <returns>The export.</returns>
    private string DavisonMarkdown(bool includeMinorPoints)
    {
        ChartResult chartA = J2000Nyc(includeMinorPoints);
        ChartResult chartB = SecondaryNatal(includeMinorPoints);
        DavisonService service = new(GoldenFixtures.CreateChartEngine(_fixture), _logger);

        DavisonResult result = service.Calculate(chartA, chartB, includeMinorPoints);

        return DavisonMarkdownFormatter.ToMarkdown(result);
    }

    /// <summary>
    /// <see cref="DavisonMarkdownFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void DavisonMarkdownFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope _ = new(culture);
        DavisonMarkdownFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="DavisonMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void DavisonMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope _ = new(culture);
        DavisonMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden();
    }
}
