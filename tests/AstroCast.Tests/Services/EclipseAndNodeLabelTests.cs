using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins two labels (branch B). The solar eclipse figure (finding T4-4, ruling T-D13, Q7): the span
/// of the central phase over the whole Earth keeps its value and is named "Duration of the central
/// eclipse (whole Earth)", where it was printed as "Totality Duration"; a lunar eclipse's figure,
/// which is totality, keeps that name. The lunation flag (finding T4-3, ruling T-D12 kept, Q8): the
/// rule is unchanged, a New Moon within 18° of the true node and a Full Moon within 12°, and the
/// label says what it measures, "Near a node (within 18°)" and "Near a node (within 12°)", where it
/// said "Possible Solar Eclipse" and "Possible Lunar Eclipse".
/// </summary>
/// <remarks>
/// Every expected instant, duration and node distance is Tier B: the library's own eclipse search
/// called directly (<c>swe_sol_eclipse_when_glob</c>, <c>swe_lun_eclipse_when</c>), and the
/// lunations and true node from direct <c>swe_calc_ut</c>. Each label is checked under en-US and
/// under de-DE, sv-SE and th-TH, which must print the same text.
/// </remarks>
[Collection("Ephemeris")]
public sealed class EclipseAndNodeLabelTests
{
    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public EclipseAndNodeLabelTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>The cultures each label is checked under.</summary>
    public static TheoryData<string> AllCultures => new() { "en-US", "de-DE", "sv-SE", "th-TH" };

    /// <summary>
    /// The total solar eclipse of 2026-08-12: the central phase runs from 16:58:09.355 to
    /// 18:34:00.697 UT over the whole Earth (Tier B), 95.86 minutes. The value is unchanged and is
    /// printed as "Duration of the central eclipse (whole Earth): 1h 35m 51s"; the eclipse no
    /// longer prints a "Totality Duration". Until branch B: "Totality Duration: 1h 35m 51s".
    /// </summary>
    /// <param name="culture">The culture the export is written under.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(AllCultures))]
    public void SolarEclipse20260812_FigureIsNamedForTheCentralEclipseOverTheWholeEarth(string culture)
    {
        using CultureScope scope = new(culture);
        EclipseSeries series = Eclipses().FindEclipses(2026, 2026);

        EclipseResult solar = series.Eclipses.Should().ContainSingle(e =>
            e.IsSolar && Near(e.MaximumUtc, Utc(2026, 8, 12, 17, 45, 59.221))).Subject;
        solar.TotalityDurationMinutes.Should().BeApproximately(95.8557, 1.0 / 60.0);

        string section = DetailSection(EclipseFormatter.SeriesToMarkdown(series), "Total Solar Eclipse, August 12, 2026");
        section.Should().Contain("**Duration of the central eclipse (whole Earth):** 1h 35m 51s")
            .And.NotContain("Totality Duration");
    }

    /// <summary>
    /// The total lunar eclipse of 2026-03-03: totality runs from 11:04:32.304 to 12:02:51.495 UT
    /// (Tier B), 58m 19s, and keeps the name "Totality Duration".
    /// </summary>
    /// <param name="culture">The culture the export is written under.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(AllCultures))]
    public void LunarEclipse20260303_KeepsTotalityDuration(string culture)
    {
        using CultureScope scope = new(culture);
        EclipseSeries series = Eclipses().FindEclipses(2026, 2026);

        string section = DetailSection(EclipseFormatter.SeriesToMarkdown(series), "Total Lunar Eclipse, March 3, 2026");
        section.Should().Contain("**Totality Duration:** 0h 58m 19s")
            .And.NotContain("central eclipse");
    }

    /// <summary>
    /// The eclipse timeline's Duration column holds both figures, so its heading stays "Duration" and
    /// one note under the table says what each means.
    /// </summary>
    /// <param name="culture">The culture the export is written under.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(AllCultures))]
    public void EclipseTimeline_DurationColumn_CarriesOneNoteSayingWhatEachMeans(string culture)
    {
        using CultureScope scope = new(culture);
        string markdown = EclipseFormatter.SeriesToMarkdown(Eclipses().FindEclipses(2026, 2026));

        markdown.Should().Contain("| # | Date (UTC) | Category | Type | Sign | Position | Node Dist | Duration |");
        markdown.Split('\n').Count(l => l.StartsWith("*Duration:", StringComparison.Ordinal)).Should().Be(1);
        markdown.Should().Contain(
            "*Duration: for a solar eclipse, the duration of the central eclipse over the whole Earth, " +
            "from the first moment the total or annular phase is seen anywhere to the last; " +
            "for a lunar eclipse, the duration of totality.*");
    }

    /// <summary>
    /// The lunations of 2028 near a node are the same lunations as before, by the same rule, with the
    /// label saying what it measures. Tier B: New Moons within 18° of the true node on 2028-01-26
    /// (3.8°) and 2028-07-22 (6.6°); Full Moons within 12° on 2028-01-12 (10.9°), 2028-07-06 (8.0°)
    /// and 2028-12-31 16:48:32 UT (3.4°). The column heading becomes "Near a node".
    /// </summary>
    /// <param name="culture">The culture the export is written under.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(AllCultures))]
    public void Lunations2028_NearANode_AreLabelledForWhatTheFlagMeasures(string culture)
    {
        using CultureScope scope = new(culture);
        LunationSeries series = new LunationService(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _logger)
            .FindLunations(2028, 2028);

        List<LunationResult> flagged = series.Lunations.Where(l => l.IsNearNode).ToList();
        flagged.Select(l => l.ExactUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)).Should().Equal(
            "2028-01-12", "2028-01-26", "2028-07-06", "2028-07-22", "2028-12-31");
        flagged.Where(l => l.IsNewMoon).Should().OnlyContain(l => l.EclipseIndicator == "Near a node (within 18°)");
        flagged.Where(l => !l.IsNewMoon).Should().OnlyContain(l => l.EclipseIndicator == "Near a node (within 12°)");
        flagged.Should().ContainSingle(l => !l.IsNewMoon && Near(l.ExactUtc, Utc(2028, 12, 31, 16, 48, 31.886)));

        string markdown = LunationFormatter.SeriesToMarkdown(series);
        markdown.Should().Contain("| # | Date (UTC) | Type | Sign | Degree | Element | Modality | Near a node |")
            .And.NotContain("Possible Lunar Eclipse")
            .And.NotContain("Possible Solar Eclipse");
        markdown.Split('\n').Should().ContainSingle(l => l.Contains("| 2028-12-31 16:48 | Full Moon |", StringComparison.Ordinal))
            .Which.TrimEnd('\r').Should().EndWith("| Near a node (within 12°) |");
    }

    #region Helpers

    private EclipseService Eclipses() => new(_fixture.EphemerisService, _logger);

    /// <summary>The detail section of the eclipse whose heading ends with <paramref name="title"/>.</summary>
    private static string DetailSection(string markdown, string title)
    {
        int start = markdown.IndexOf(title, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "the export has a section for {0}", title);
        int end = markdown.IndexOf("### #", start, StringComparison.Ordinal);
        return end < 0 ? markdown[start..] : markdown[start..end];
    }

    private static bool Near(DateTime actual, DateTime expected) =>
        Math.Abs((actual - expected).TotalSeconds) <= 1.0;

    private static DateTime Utc(int year, int month, int day, int hour, int minute, double second) =>
        new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc).AddSeconds(second);

    #endregion
}
