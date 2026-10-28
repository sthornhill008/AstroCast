using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.MedicalAstrology;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Formatting.Golden;

/// <summary>
/// Golden-file tests for the return-family Markdown formatters
/// (Solar / Lunar / Planetary / Planetary-cycle-timeline /
/// Return-and-Ingress-timeline) plus the Medical formatter. Every test
/// drives its formatter from the canonical J2000 NYC natal chart so
/// physical-value drift surfaces as a diff against the checked-in golden.
/// </summary>
/// <remarks>
/// <para>
/// All tests share the <see cref="EphemerisFixture"/> via the
/// <c>"Ephemeris"</c> collection so Swiss Ephemeris is initialised exactly
/// once for the test run. Tests are categorised as integration because
/// they depend on live ephemeris data.
/// </para>
/// <para>
/// Return dates in these fixtures are deterministic: a given natal + return
/// number (e.g., Saturn return #1) produces the same UTC moment on any
/// machine. The canonical J2000 NYC natal places Saturn's first return in
/// the late 2020s, Mars returns roughly every ~2 years, and Mercury and Venus
/// return about once a year as seen from the Earth.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ReturnAndMedicalFormatterGoldenTests : GoldenFormatterTestBase
{
    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger;

    /// <summary>
    /// Receives the shared ephemeris fixture used by every test in this class.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public ReturnAndMedicalFormatterGoldenTests(EphemerisFixture fixture)
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
    private ChartResult J2000Nyc(bool includeMinorPoints)
    {
        ChartResult chart = GoldenFixtures.CreateChartEngine(_fixture).Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints));
        chart.MinorPoints.Should().HaveCount(includeMinorPoints ? 5 : 0, "the chart carries the five exactly when the switch is on");
        return chart;
    }

    /// <summary>
    /// Verifies <see cref="SolarReturnMarkdownFormatter.ToMarkdown"/> renders
    /// the J2000 NYC solar return for 2026 identically to the checked-in
    /// golden.
    /// </summary>
    [Fact]
    public void SolarReturnMarkdownFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(SolarReturnMarkdown(includeMinorPoints: false), "SolarReturnMarkdownFormatter_J2000NYC_2026.golden.md");
    }

    /// <summary>The 2026 solar return of J2000 NYC, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string SolarReturnMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = J2000Nyc(includeMinorPoints);

        SolarReturnService service = new(
            _fixture.EphemerisService,
            GoldenFixtures.CreateChartEngine(_fixture),
            _logger);

        SolarReturnResult result = service.Calculate(
            natal,
            year: 2026,
            latitude: 40.7128,
            longitude: -74.006,
            locationName: "New York City",
            timeZone: "America/New_York");

        return SolarReturnMarkdownFormatter.ToMarkdown(result);
    }

    /// <summary>
    /// Verifies <see cref="LunarReturnMarkdownFormatter.ToMarkdown"/> renders
    /// a single lunar return in April 2026 for J2000 NYC identically to the
    /// checked-in golden.
    /// </summary>
    /// <remarks>
    /// A one-month search window yields exactly one lunar return, keeping
    /// the golden compact.
    /// </remarks>
    [Fact]
    public void LunarReturnMarkdownFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(LunarReturnMarkdown(includeMinorPoints: false), "LunarReturnMarkdownFormatter_J2000NYC_2026April.golden.md");
    }

    /// <summary>The April 2026 lunar return of J2000 NYC, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string LunarReturnMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = J2000Nyc(includeMinorPoints);

        LunarReturnService service = new(
            _fixture.EphemerisService,
            GoldenFixtures.CreateChartEngine(_fixture),
            _logger);

        IReadOnlyList<LunarReturnResult> results = service.Calculate(
            natal,
            startDate: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc),
            endDate: new DateTime(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc),
            latitude: 40.7128,
            longitude: -74.006,
            locationName: "New York City",
            timeZone: "America/New_York");

        results.Should().NotBeEmpty("expected at least one lunar return in the April 2026 window");

        return LunarReturnMarkdownFormatter.ToMarkdown(results[0]);
    }

    /// <summary>
    /// Verifies <see cref="PlanetaryReturnMarkdownFormatter.ToMarkdown"/>
    /// renders the first Saturn return for the J2000 NYC natal identically
    /// to the checked-in golden.
    /// </summary>
    /// <remarks>
    /// Saturn's ~29.5-year sidereal period places the first return in the
    /// late 2020s regardless of when the test runs; the return number (1)
    /// keeps the output deterministic.
    /// </remarks>
    [Fact]
    public void PlanetaryReturnMarkdownFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(PlanetaryReturnMarkdown(includeMinorPoints: false), "PlanetaryReturnMarkdownFormatter_J2000NYC_Saturn1.golden.md");
    }

    /// <summary>The first Saturn return of J2000 NYC, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string PlanetaryReturnMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = J2000Nyc(includeMinorPoints);

        PlanetaryReturnService service = new(
            _fixture.EphemerisService,
            GoldenFixtures.CreateChartEngine(_fixture),
            _logger);

        PlanetaryReturnResult result = service.Calculate(
            natal,
            planet: CelestialBodyId.Saturn,
            returnNumber: 1,
            latitude: 40.7128,
            longitude: -74.006,
            locationName: "New York City",
            timeZone: "America/New_York");

        return PlanetaryReturnMarkdownFormatter.ToMarkdown(result);
    }

    /// <summary>
    /// Verifies <see cref="PlanetaryCycleTimelineFormatter.ToMarkdown"/>
    /// renders the first Mars cycle (between Mars returns 1 and 2) for the
    /// J2000 NYC natal identically to the checked-in golden.
    /// </summary>
    /// <remarks>
    /// Mars was chosen for its ~2-year cycle: the resulting timeline
    /// exercises the full phase-point scaffold (0°, 90°, 180°, 270°, 360°)
    /// in a compact golden. <c>includeSnapshots</c>
    /// is left at its default (<c>false</c>) to keep the output focused on
    /// the timing rather than embedding full ephemeris snapshots.
    /// </remarks>
    [Fact]
    public void PlanetaryCycleTimelineFormatter_ToMarkdown_MatchesGolden()
    {
        ChartResult natal = GoldenFixtures.J2000Nyc(_fixture);
        ChartEngine engine = GoldenFixtures.CreateChartEngine(_fixture);

        PlanetaryReturnService returnService = new(
            _fixture.EphemerisService, engine, _logger);

        PlanetaryCycleTimelineService service = new(
            _fixture.EphemerisService, returnService, _logger);

        PlanetaryCycleTimeline timeline = service.Calculate(
            natal,
            planet: CelestialBodyId.Mars,
            returnNumber: 1);

        string md = PlanetaryCycleTimelineFormatter.ToMarkdown(timeline);

        AssertGolden(md, "PlanetaryCycleTimelineFormatter_J2000NYC_Mars1.golden.md");
    }

    /// <summary>
    /// Verifies <see cref="ReturnIngressTimelineFormatter.ToMarkdown"/>
    /// renders the combined return + outer-ingress calendar for 2026–2027
    /// against the J2000 NYC natal identically to the checked-in golden.
    /// </summary>
    /// <remarks>
    /// A 2-year window keeps the timeline modest (tens of events). Default
    /// return and ingress planet sets are used so the golden tracks the
    /// service's documented defaults.
    /// </remarks>
    [Fact]
    public void ReturnIngressTimelineFormatter_ToMarkdown_MatchesGolden()
    {
        AssertGolden(ReturnIngressTimelineMarkdown(includeMinorPoints: false), "ReturnIngressTimelineFormatter_J2000NYC_2026_2027.golden.md");
    }

    /// <summary>The 2026–2027 return and ingress timeline of J2000 NYC, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string ReturnIngressTimelineMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = J2000Nyc(includeMinorPoints);

        ReturnIngressTimelineService service = new(
            _fixture.EphemerisService, _logger);

        ReturnIngressTimeline timeline = service.Calculate(
            natal,
            startYear: 2026,
            endYear: 2027);

        return ReturnIngressTimelineFormatter.ToMarkdown(timeline);
    }

    /// <summary>
    /// Verifies <see cref="MedicalMarkdownFormatter.Format"/> renders the
    /// J2000 NYC medical profile identically to the checked-in golden. The
    /// profile is produced from the canonical chart via the static
    /// <see cref="HealthProfileGenerator"/>, matching production usage in
    /// <c>MainViewModel</c>.
    /// </summary>
    [Fact]
    public void MedicalMarkdownFormatter_Format_MatchesGolden()
    {
        AssertGolden(MedicalMarkdown(includeMinorPoints: false), "MedicalMarkdownFormatter_J2000NYC.golden.md");
    }

    /// <summary>The medical profile of J2000 NYC, the natal cast with the switch as given.</summary>
    /// <param name="includeMinorPoints">Whether the natal chart carries the five.</param>
    /// <returns>The export.</returns>
    private string MedicalMarkdown(bool includeMinorPoints)
    {
        ChartResult natal = J2000Nyc(includeMinorPoints);
        HealthProfile profile = HealthProfileGenerator.Generate(natal);

        return MedicalMarkdownFormatter.Format(natal, profile, GoldenFixtures.GoldenClock());
    }

    // Branch AsteroidsExport (1.6.0): the same fixtures with the switch on. Each return chart carries its own
    // five (the services copy the switch), so the three return exports gain the section, in goldens of their own
    // suffixed _MinorPoints (R6, scoping decision 4); the return and ingress timeline and the medical report print
    // nothing of them and stay byte-identical to their existing goldens (R8).

    /// <summary>
    /// Verifies <see cref="SolarReturnMarkdownFormatter.ToMarkdown"/> renders the 2026 solar return of J2000 NYC,
    /// the natal carrying the five, identically to its own golden: the return chart's own five and their aspects.
    /// </summary>
    [Fact]
    public void SolarReturnMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden()
    {
        AssertGolden(SolarReturnMarkdown(includeMinorPoints: true), "SolarReturnMarkdownFormatter_J2000NYC_2026_MinorPoints.golden.md");
    }

    /// <summary>
    /// Verifies <see cref="LunarReturnMarkdownFormatter.ToMarkdown"/> renders the April 2026 lunar return of
    /// J2000 NYC, the natal carrying the five, identically to its own golden.
    /// </summary>
    [Fact]
    public void LunarReturnMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden()
    {
        AssertGolden(LunarReturnMarkdown(includeMinorPoints: true), "LunarReturnMarkdownFormatter_J2000NYC_2026April_MinorPoints.golden.md");
    }

    /// <summary>
    /// Verifies <see cref="PlanetaryReturnMarkdownFormatter.ToMarkdown"/> renders the first Saturn return of
    /// J2000 NYC, the natal carrying the five, identically to its own golden.
    /// </summary>
    [Fact]
    public void PlanetaryReturnMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden()
    {
        AssertGolden(PlanetaryReturnMarkdown(includeMinorPoints: true), "PlanetaryReturnMarkdownFormatter_J2000NYC_Saturn1_MinorPoints.golden.md");
    }

    /// <summary>The return and ingress timeline never prints the five (R8): with the natal carrying them, byte-identical to its existing golden.</summary>
    [Fact]
    public void ReturnIngressTimelineFormatter_SwitchOn_MatchesTheExistingGolden()
    {
        AssertGolden(ReturnIngressTimelineMarkdown(includeMinorPoints: true), "ReturnIngressTimelineFormatter_J2000NYC_2026_2027.golden.md");
    }

    /// <summary>
    /// The medical report never prints the five, the natal chart it embeds included (R8): with the natal carrying
    /// them, byte-identical to its existing golden.
    /// </summary>
    [Fact]
    public void MedicalMarkdownFormatter_SwitchOn_MatchesTheExistingGolden()
    {
        AssertGolden(MedicalMarkdown(includeMinorPoints: true), "MedicalMarkdownFormatter_J2000NYC.golden.md");
    }

    // Each golden again under de-DE, sv-SE and th-TH, compared with the same golden
    // file: the export reads the same whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="SolarReturnMarkdownFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void SolarReturnMarkdownFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        SolarReturnMarkdownFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="LunarReturnMarkdownFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void LunarReturnMarkdownFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        LunarReturnMarkdownFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="PlanetaryReturnMarkdownFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void PlanetaryReturnMarkdownFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        PlanetaryReturnMarkdownFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="PlanetaryCycleTimelineFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void PlanetaryCycleTimelineFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        PlanetaryCycleTimelineFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="ReturnIngressTimelineFormatter_ToMarkdown_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ReturnIngressTimelineFormatter_ToMarkdown_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ReturnIngressTimelineFormatter_ToMarkdown_MatchesGolden();
    }

    /// <summary>
    /// <see cref="MedicalMarkdownFormatter_Format_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void MedicalMarkdownFormatter_Format_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        MedicalMarkdownFormatter_Format_MatchesGolden();
    }

    /// <summary>
    /// <see cref="SolarReturnMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void SolarReturnMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        SolarReturnMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden();
    }

    /// <summary>
    /// <see cref="LunarReturnMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void LunarReturnMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        LunarReturnMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden();
    }

    /// <summary>
    /// <see cref="PlanetaryReturnMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void PlanetaryReturnMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        PlanetaryReturnMarkdownFormatter_ToMarkdown_MinorPoints_MatchesGolden();
    }
}
