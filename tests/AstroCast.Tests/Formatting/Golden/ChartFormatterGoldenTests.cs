using System.Globalization;

using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Formatting.Golden;

/// <summary>
/// Golden-file tests for formatters that consume a <see cref="ChartResult"/>
/// produced by <see cref="ChartEngine"/>. Uses the shared
/// <see cref="EphemerisFixture"/> so the Swiss Ephemeris is initialised once
/// for the whole test run, and a deterministic J2000 canonical chart as the
/// input so ephemeris drift would show up as a failed golden comparison.
/// </summary>
/// <remarks>
/// The canonical chart is the J2000 epoch (2000-01-01 07:00 America/New_York,
/// = 2000-01-01 12:00 UTC = JD 2451545.0) at New York City. This is the same
/// reference moment used by the rest of the test suite, so any physical-value
/// change surfaces consistently across tests.
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartFormatterGoldenTests : GoldenFormatterTestBase
{
    private readonly ChartEngine _engine;
    private readonly AstrocartographyService _acgService;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds the chart engine and
    /// ACG service used to produce canonical inputs for every golden.
    /// </summary>
    public ChartFormatterGoldenTests(EphemerisFixture fixture)
    {
        ILogger logger = new LoggerConfiguration().CreateLogger();
        DignityService dignity = new();
        SectService sect = new();
        AspectService aspects = new(fixture.EphemerisService, logger);

        _engine = new ChartEngine(
            fixture.EphemerisService,
            aspects,
            new MidpointService(logger),
            dignity,
            new MoonConditionService(),
            new ChartContextService(dignity, sect),
            new FixedStarService(fixture.EphemerisService, logger),
            new DeclinationAspectService(),
            new PlanetConditionService(),
            new MoonMansionService(),
            new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(dignity),
            new AntisciaService(),
            new StationService(),
            new ParanService(fixture.EphemerisService, logger),
            logger);

        _acgService = new AstrocartographyService(fixture.EphemerisService, logger);
    }

    /// <summary>
    /// Produce the canonical J2000 natal chart used as input for every
    /// chart-dependent golden in this class.
    /// </summary>
    /// <param name="includeMinorPoints">
    /// Whether the chart carries Ceres, Pallas, Juno, Vesta and Lilith (branch AsteroidsExport, 1.6.0); off
    /// by default, as every existing golden casts it.
    /// </param>
    private ChartResult CanonicalChart(bool includeMinorPoints = false)
    {
        ChartResult chart = _engine.Calculate(new ChartInput
        {
            Name = "Golden Test Chart",
            LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
            TimeZone = "America/New_York",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Birth",
            IncludeMinorPoints = includeMinorPoints,
        });
        chart.MinorPoints.Should().HaveCount(includeMinorPoints ? 5 : 0, "the chart carries the five exactly when the switch is on");
        return chart;
    }

    /// <summary>
    /// Verifies <see cref="ChartMarkdownFormatter.ToMarkdown"/> renders the
    /// canonical J2000 chart identically to the checked-in golden.
    /// </summary>
    [Fact]
    public void ChartMarkdownFormatter_ToMarkdown_MatchesGolden()
    {
        ChartResult chart = CanonicalChart();

        string md = ChartMarkdownFormatter.ToMarkdown(chart, timeProvider: GoldenFixtures.GoldenClock());

        AssertGolden(md, "ChartMarkdownFormatter_ToMarkdown_J2000NYC.golden.md");
    }

    /// <summary>
    /// Guards the F-GR-2 invariant that production output is unchanged: with no
    /// injected clock, <see cref="ChartMarkdownFormatter.ToMarkdown"/> resolves
    /// the profection "as of" date from <see cref="TimeProvider.System"/> — the
    /// real current day, never the pinned golden date. This is the counterpart
    /// to the date-pinned golden above; together they lock both directions of
    /// the clock injection. The assertion compares against <see cref="DateTime.Today"/>
    /// captured in the same run, so it is self-consistent and never expires.
    /// </summary>
    [Fact]
    public void ChartMarkdownFormatter_ToMarkdown_DefaultClock_UsesRealToday()
    {
        ChartResult chart = CanonicalChart();

        // No timeProvider argument -> TimeProvider.System -> the real current
        // date, exactly as the pre-F-GR-2 DateTime.Today code behaved.
        string md = ChartMarkdownFormatter.ToMarkdown(chart);

        Assert.Contains(string.Create(CultureInfo.InvariantCulture, $"(as of {DateTime.Today:yyyy-MM-dd})"), md);
    }

    /// <summary>
    /// Verifies <see cref="ChartJsonFormatter.ToJson"/> renders the canonical
    /// J2000 chart identically to the checked-in golden.
    /// </summary>
    [Fact]
    public void ChartJsonFormatter_ToJson_MatchesGolden()
    {
        ChartResult chart = CanonicalChart();

        string json = ChartJsonFormatter.ToJson(chart);

        AssertGolden(json, "ChartJsonFormatter_ToJson_J2000NYC.golden.json");
    }

    /// <summary>
    /// Verifies <see cref="AstrocartographyFormatter.ToMarkdown"/> renders
    /// the full ACG result for the canonical J2000 chart identically to
    /// the checked-in golden, including a crossing report at the birth
    /// location with the default orb, as a map click there would list it.
    /// </summary>
    [Fact]
    public void AstrocartographyFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(AstrocartographyMarkdown(includeMinorPoints: false), "AstrocartographyFormatter_ToMarkdown_J2000NYC.golden.md");
    }

    /// <summary>
    /// The ACG export of the canonical chart, cast with the switch as given, with the crossing report at the
    /// birth location and the default orb.
    /// </summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string AstrocartographyMarkdown(bool includeMinorPoints)
    {
        ChartResult chart = CanonicalChart(includeMinorPoints);
        AcgResult acg = _acgService.Calculate(chart);
        AcgCrossingReport report = _acgService.GetCrossings(
            acg,
            latitude: 40.7128,
            longitude: -74.006,
            locationName: "New York City");

        return AstrocartographyFormatter.ToMarkdown(acg, report);
    }

    // Branch AsteroidsExport (1.6.0): the same fixture with the switch on. The chart exports gain the five's
    // section, in goldens of their own suffixed _MinorPoints (R6, scoping decision 4); the map prints nothing
    // of them and stays byte-identical to its existing golden (R8).

    /// <summary>
    /// Verifies <see cref="ChartMarkdownFormatter.ToMarkdown"/> renders the canonical J2000 chart cast with the
    /// switch on identically to its own golden: the existing export with the Asteroids and Lilith section after
    /// the Out-of-Bounds Planets (Pallas retrograde and out of bounds; five aspects at 2°).
    /// </summary>
    [Fact]
    public void ChartMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden()
    {
        ChartResult chart = CanonicalChart(includeMinorPoints: true);

        string md = ChartMarkdownFormatter.ToMarkdown(chart, timeProvider: GoldenFixtures.GoldenClock());

        AssertGolden(md, "ChartMarkdownFormatter_ToMarkdown_J2000NYC_MinorPoints.golden.md");
    }

    /// <summary>
    /// Verifies <see cref="ChartJsonFormatter.ToJson"/> renders the canonical J2000 chart cast with the switch on
    /// identically to its own golden: the existing JSON with the <c>minorPoints</c> array after <c>positions</c>.
    /// </summary>
    [Fact]
    public void ChartJsonFormatter_ToJson_MinorPoints_MatchesGolden()
    {
        ChartResult chart = CanonicalChart(includeMinorPoints: true);

        string json = ChartJsonFormatter.ToJson(chart);

        AssertGolden(json, "ChartJsonFormatter_ToJson_J2000NYC_MinorPoints.golden.json");
    }

    /// <summary>
    /// The map never prints the five (R8): the ACG export of the canonical chart cast with the switch on is
    /// byte-identical to its existing golden.
    /// </summary>
    [Fact]
    public void AstrocartographyFormatter_ToMarkdown_SwitchOn_MatchesTheExistingGolden()
    {
        AssertGolden(AstrocartographyMarkdown(includeMinorPoints: true), "AstrocartographyFormatter_ToMarkdown_J2000NYC.golden.md");
    }

    // Each golden again under de-DE, sv-SE and th-TH, compared with the same golden
    // file: the export reads the same whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="ChartMarkdownFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ChartMarkdownFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ChartMarkdownFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="ChartMarkdownFormatter_ToMarkdown_DefaultClock_UsesRealToday"/> under each of
    /// <see cref="CultureScope.OtherCultures"/>. The golden masks the "(as of …)" date, so this
    /// is the test that sees its calendar: no Buddhist year under th-TH.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ChartMarkdownFormatter_ToMarkdown_DefaultClock_UsesRealToday_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ChartMarkdownFormatter_ToMarkdown_DefaultClock_UsesRealToday();
    }

    /// <summary>
    /// <see cref="ChartJsonFormatter_ToJson_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ChartJsonFormatter_ToJson_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ChartJsonFormatter_ToJson_MatchesGolden();
    }

    /// <summary>
    /// <see cref="AstrocartographyFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void AstrocartographyFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        AstrocartographyFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="ChartMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ChartMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ChartMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden();
    }

    /// <summary>
    /// <see cref="ChartJsonFormatter_ToJson_MinorPoints_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ChartJsonFormatter_ToJson_MinorPoints_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ChartJsonFormatter_ToJson_MinorPoints_MatchesGolden();
    }
}
