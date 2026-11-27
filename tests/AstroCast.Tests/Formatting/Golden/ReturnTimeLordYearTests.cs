using System.Text;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Formatting.Golden;

/// <summary>
/// The year a return export reads its time lords for (branch ReturnAges, 1.7.2; finding A1 of the 1.7.2 code
/// audit, Steve's ruling): a solar return export reads all three sections, the annual profection, the Firdaria
/// and the Zodiacal Releasing, for the year the return opens, at the birthday that year begins on; a lunar or
/// planetary return export keeps the year running at the return.
/// </summary>
/// <remarks>
/// <para>
/// Until 1.7.2 a solar return that fell on the local day before the birthday printed the age of the year it
/// opens in the profection and the year before it in the Firdaria: person A's 2026 return, 2026-10-04 17:18:50
/// UT for a birthday of 5 October, said 79 in one section and 78 in the next.
/// </para>
/// <para>
/// The four goldens here were written from <c>master</c> before the rule changed: a solar return on its
/// birthday, one after it, and a lunar and a planetary return shortly before a birthday. They hold those
/// exports to what <c>master</c> printed. The golden comparison masks the profection's "(as of ...)" date, so
/// each test also names that line outright.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ReturnTimeLordYearTests : GoldenFormatterTestBase
{
    private const string ProfectionHeading = "## Annual Profection (at Return Ingress)";
    private const string FirdariaHeading = "## Firdaria (at Return Ingress)";
    private const string ReleasingHeading = "## Zodiacal Releasing (Spirit) (at Return Ingress)";

    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public ReturnTimeLordYearTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _logger = new LoggerConfiguration().CreateLogger();
    }

    #region A solar return before the birthday: the year it opens

    /// <summary>
    /// Person A (born 1947-10-05), solar return 2026: the return is 2026-10-04 17:18:50 UT, the day before the
    /// birthday on the chart's clock, and it opens the year of age 79. All three sections are read on the
    /// birthday that year begins on, 2026-10-05: the profection says 79 as of that day, the Firdaria summary
    /// says 79, and the releasing is the one that stands on that day. Until 1.7.2 the Firdaria said 78.
    /// </summary>
    [Fact]
    public void SolarReturn_TheDayBeforeTheBirthday_ReadsAllThreeSectionsAtAge79()
    {
        ChartResult natal = Hartwell();
        SolarReturnResult result = SolarReturn(natal, 2026);
        result.ReturnChart.UtcDateTime.Should().BeCloseTo(
            new DateTime(2026, 10, 4, 17, 18, 50, DateTimeKind.Utc), TimeSpan.FromSeconds(1));

        string sections = TimeLordSections(SolarReturnMarkdownFormatter.ToMarkdown(result));

        sections.Should().Contain("**Age:** 79 (as of 2026-10-05)")
            .And.Contain("At age 79, Eleanor Hartwell")
            .And.Contain("Age 79: Sun major period (2022\u20132032), Mercury sub-period (2025-08-13\u20132027-01-17)")
            .And.NotContain("Age 78")
            .And.NotContain("age 78");
        sections.Should().Be(SectionsReadOn(natal, new DateTime(2026, 10, 5)),
            "all three sections are read on the birthday the year begins on, where the age is 79");
    }

    /// <summary>
    /// A Firdaria period that begins on the birthday the year opens on is the one printed, though the return
    /// falls before that birthday. Person A's 2022 return is 2022-10-04 18:09:22 UT, the day before the 75th
    /// birthday, on which the Sun's ten years begin; her 2032 return is 2032-10-04 04:20:08 UT, two days
    /// before the 85th birthday on her clock, on which Venus's eight years begin. Until 1.7.2 each printed the
    /// period that ended on that birthday.
    /// </summary>
    /// <param name="year">The solar return's year.</param>
    /// <param name="newPeriod">The Current Major Period line of the period that begins on the birthday.</param>
    /// <param name="newSummary">How the Firdaria summary begins.</param>
    /// <param name="oldSummary">How the Firdaria summary began until 1.7.2.</param>
    [Theory]
    [InlineData(2022, "**Current Major Period:** Sun (2022\u20132032, 10y)",
        "Age 75: Sun major period (2022\u20132032), Sun sub-period (2022-10-05\u20132024-03-09)", "Age 74: South Node major period")]
    [InlineData(2032, "**Current Major Period:** Venus (2032\u20132040, 8y)",
        "Age 85: Venus major period (2032\u20132040), Venus sub-period (2032-10-05\u2013", "Age 84: Sun major period")]
    public void SolarReturn_BeforeABirthdayThatBeginsAFirdariaPeriod_PrintsTheNewPeriod(
        int year, string newPeriod, string newSummary, string oldSummary)
    {
        ChartResult natal = Hartwell();
        SolarReturnResult result = SolarReturn(natal, year);
        DateTime birthday = new(year, 10, 5);
        ReadingOnTheChartsClock(natal, result.ReturnChart.UtcDateTime).Date.Should().BeBefore(birthday,
            "the case is a return that falls before the birthday");

        string sections = TimeLordSections(SolarReturnMarkdownFormatter.ToMarkdown(result));

        sections.Should().Contain(newPeriod)
            .And.Contain(newSummary)
            .And.NotContain(oldSummary);
        sections.Should().Be(SectionsReadOn(natal, birthday),
            "all three sections are read on the birthday the year begins on");
    }

    #endregion

    #region A solar return on or after the birthday: as master printed it

    /// <summary>
    /// Person A's 1995 return, 1995-10-05 05:15:15 UT, is on her birthday on her clock (00:15 at UTC-5): the
    /// export is what <c>master</c> printed, read on the return's own day at age 48.
    /// </summary>
    [Fact]
    public void SolarReturn_OnTheBirthday_IsWhatMasterPrinted()
    {
        ChartResult natal = Hartwell();
        SolarReturnResult result = SolarReturn(natal, 1995);
        ReadingOnTheChartsClock(natal, result.ReturnChart.UtcDateTime).Date.Should().Be(new DateTime(1995, 10, 5));

        string markdown = SolarReturnMarkdownFormatter.ToMarkdown(result);

        AssertGolden(markdown, "SolarReturnMarkdownFormatter_Hartwell_1995.golden.md");
        string sections = TimeLordSections(markdown);
        sections.Should().Contain("**Age:** 48 (as of 1995-10-05)")
            .And.Contain("Age 48: Saturn major period (1987\u20131998), Mercury sub-period (1995-08-14\u20131997-03-10)");
        sections.Should().Be(SectionsReadOn(natal, new DateTime(1995, 10, 5)));
    }

    /// <summary>
    /// Person B's 1966 return, 1966-12-06 07:14:09 UT, is the day after her birthday of 5 December on her
    /// clock: the export is what <c>master</c> printed, read on the return's own day at age 2.
    /// </summary>
    [Fact]
    public void SolarReturn_AfterTheBirthday_IsWhatMasterPrinted()
    {
        ChartResult natal = Whitfield();
        SolarReturnResult result = SolarReturn(natal, 1966);
        ReadingOnTheChartsClock(natal, result.ReturnChart.UtcDateTime).Date.Should().Be(new DateTime(1966, 12, 6));

        string markdown = SolarReturnMarkdownFormatter.ToMarkdown(result);

        AssertGolden(markdown, "SolarReturnMarkdownFormatter_Whitfield_1966.golden.md");
        string sections = TimeLordSections(markdown);
        sections.Should().Contain("**Age:** 2 (as of 1966-12-06)")
            .And.Contain("Age 2: Sun major period (1964\u20131974), Venus sub-period (1966-05-10\u20131967-10-14)");
        sections.Should().Be(SectionsReadOn(natal, new DateTime(1966, 12, 6)));
    }

    #endregion

    #region A lunar or planetary return: the year running at the return, as master printed it

    /// <summary>
    /// Person A's lunar return of 2026-10-01 23:05:25 UT, four days before her 79th birthday: the export is
    /// what <c>master</c> printed, every section at age 78, the year running at the return.
    /// </summary>
    [Fact]
    public void LunarReturn_BeforeTheBirthday_IsWhatMasterPrinted()
    {
        string markdown = LunarReturnMarkdown();

        AssertGolden(markdown, "LunarReturnMarkdownFormatter_Hartwell_20261001.golden.md");
        TimeLordSections(markdown).Should().Contain("**Age:** 78 (as of 2026-10-01)")
            .And.Contain("At age 78, Eleanor Hartwell")
            .And.Contain("Age 78: Sun major period (2022\u20132032), Mercury sub-period (2025-08-13\u20132027-01-17)");
    }

    /// <summary>
    /// Person A's second Saturn return, 2006-09-14 16:17:30 UT, three weeks before her 59th birthday: the
    /// export is what <c>master</c> printed, every section at age 58, the year running at the return.
    /// </summary>
    [Fact]
    public void PlanetaryReturn_BeforeTheBirthday_IsWhatMasterPrinted()
    {
        string markdown = PlanetaryReturnMarkdown();

        AssertGolden(markdown, "PlanetaryReturnMarkdownFormatter_Hartwell_Saturn2.golden.md");
        TimeLordSections(markdown).Should().Contain("**Age:** 58 (as of 2006-09-14)")
            .And.Contain("At age 58, Eleanor Hartwell")
            .And.Contain("Age 58: Jupiter major period (1998\u20132010), Mercury sub-period (2005-08-13\u20132007-05-01)");
    }

    #endregion

    #region The four goldens under de-DE, sv-SE and th-TH

    /// <summary>
    /// <see cref="SolarReturn_OnTheBirthday_IsWhatMasterPrinted"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void SolarReturn_OnTheBirthday_IsWhatMasterPrinted_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        SolarReturn_OnTheBirthday_IsWhatMasterPrinted();
    }

    /// <summary>
    /// <see cref="SolarReturn_AfterTheBirthday_IsWhatMasterPrinted"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void SolarReturn_AfterTheBirthday_IsWhatMasterPrinted_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        SolarReturn_AfterTheBirthday_IsWhatMasterPrinted();
    }

    /// <summary>
    /// <see cref="LunarReturn_BeforeTheBirthday_IsWhatMasterPrinted"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void LunarReturn_BeforeTheBirthday_IsWhatMasterPrinted_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        LunarReturn_BeforeTheBirthday_IsWhatMasterPrinted();
    }

    /// <summary>
    /// <see cref="PlanetaryReturn_BeforeTheBirthday_IsWhatMasterPrinted"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same golden file.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void PlanetaryReturn_BeforeTheBirthday_IsWhatMasterPrinted_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        PlanetaryReturn_BeforeTheBirthday_IsWhatMasterPrinted();
    }

    /// <summary>
    /// <see cref="SolarReturn_TheDayBeforeTheBirthday_ReadsAllThreeSectionsAtAge79"/> under each of
    /// <see cref="CultureScope.OtherCultures"/>: the three sections read the same whatever the machine's
    /// regional settings.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void SolarReturn_TheDayBeforeTheBirthday_ReadsAllThreeSectionsAtAge79_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        SolarReturn_TheDayBeforeTheBirthday_ReadsAllThreeSectionsAtAge79();
    }

    #endregion

    #region Helpers

    /// <summary>Person A (invented), the suite's fixture: 1947-10-05 09:30 at an explicit UTC-5, Harrisburg.</summary>
    /// <returns>The chart.</returns>
    private ChartResult Hartwell() => GoldenFixtures.CreateChartEngine(_fixture).Calculate(GoldenFixtures.HartwellInput());

    /// <summary>
    /// Person B (invented), as <c>SolarReturnYearTests</c> casts her: 1964-12-05 14:30 America/New_York, Albany,
    /// New York.
    /// </summary>
    /// <returns>The chart.</returns>
    private ChartResult Whitfield() => GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
    {
        Name = "Margaret Whitfield",
        LocalDateTime = new DateTime(1964, 12, 5, 14, 30, 0),
        TimeZone = "America/New_York",
        Latitude = 42.6526,
        Longitude = -73.7562,
        LocationName = "Albany, New York, United States",
        EventType = "Birth",
    });

    /// <summary>The solar return of a year, cast at the natal place.</summary>
    /// <param name="natal">The natal chart.</param>
    /// <param name="year">The year.</param>
    /// <returns>The return.</returns>
    private SolarReturnResult SolarReturn(ChartResult natal, int year) =>
        new SolarReturnService(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _logger)
            .Calculate(natal, year, natal.Input.Latitude, natal.Input.Longitude, natal.Input.LocationName, natal.Input.TimeZone);

    /// <summary>Person A's first lunar return from 1 October 2026, at the natal place.</summary>
    /// <returns>The export.</returns>
    private string LunarReturnMarkdown()
    {
        ChartResult natal = Hartwell();
        IReadOnlyList<LunarReturnResult> results =
            new LunarReturnService(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _logger)
                .Calculate(natal, new DateTime(2026, 10, 1), new DateTime(2026, 10, 31),
                    natal.Input.Latitude, natal.Input.Longitude, natal.Input.LocationName, natal.Input.TimeZone);
        results.Should().NotBeEmpty("October 2026 holds a lunar return");
        results[0].ReturnChart.UtcDateTime.Should().BeCloseTo(
            new DateTime(2026, 10, 1, 23, 5, 25, DateTimeKind.Utc), TimeSpan.FromSeconds(1));

        return LunarReturnMarkdownFormatter.ToMarkdown(results[0]);
    }

    /// <summary>Person A's second Saturn return, at the natal place.</summary>
    /// <returns>The export.</returns>
    private string PlanetaryReturnMarkdown()
    {
        ChartResult natal = Hartwell();
        PlanetaryReturnResult result =
            new PlanetaryReturnService(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _logger)
                .Calculate(natal, CelestialBodyId.Saturn, 2,
                    natal.Input.Latitude, natal.Input.Longitude, natal.Input.LocationName, natal.Input.TimeZone);
        result.ReturnChart.UtcDateTime.Should().BeCloseTo(
            new DateTime(2006, 9, 14, 16, 17, 30, DateTimeKind.Utc), TimeSpan.FromSeconds(1));

        return PlanetaryReturnMarkdownFormatter.ToMarkdown(result);
    }

    /// <summary>
    /// The three time-lord sections of a return export, from the profection's heading to the rule before the
    /// return chart, with line endings flattened.
    /// </summary>
    /// <param name="markdown">The export.</param>
    /// <returns>The three sections.</returns>
    private static string TimeLordSections(string markdown)
    {
        string text = Flatten(markdown);
        int from = text.IndexOf(ProfectionHeading, StringComparison.Ordinal);
        int to = text.IndexOf("---\n\n## Return Chart", StringComparison.Ordinal);
        from.Should().BeGreaterThan(-1, "the export carries the profection section");
        to.Should().BeGreaterThan(from, "the return chart follows the time-lord sections");

        return text[from..to];
    }

    /// <summary>
    /// The three sections as the chart's own export writes them for a plain date, under the return export's
    /// headings: each read on that day by completed birthdays, the path this branch does not change.
    /// </summary>
    /// <param name="natal">The natal chart.</param>
    /// <param name="day">The day the sections are read on.</param>
    /// <returns>The three sections.</returns>
    private static string SectionsReadOn(ChartResult natal, DateTime day)
    {
        StringBuilder sb = new();
        ChartMarkdownFormatter.AppendProfectionYear(sb, natal, day, ProfectionHeading);
        ChartMarkdownFormatter.AppendFirdaria(sb, natal, day, FirdariaHeading);
        ChartMarkdownFormatter.AppendZodiacalReleasing(sb, natal, day, ReleasingHeading);

        return Flatten(sb.ToString());
    }

    /// <summary>An instant read on a natal chart's clock: its explicit offset, or its zone.</summary>
    /// <param name="natal">The natal chart.</param>
    /// <param name="utc">The instant.</param>
    /// <returns>The reading.</returns>
    private static DateTime ReadingOnTheChartsClock(ChartResult natal, DateTime utc) =>
        natal.Input.UseExplicitOffset && natal.Input.UtcOffsetHours.HasValue
            ? utc.AddHours(natal.ResolvedUtcOffsetHours)
            : natal.TimeZoneRules!.ToLocal(utc, natal.Input.TimeZone).LocalDateTime;

    /// <summary>Line endings flattened to LF.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text with LF line endings.</returns>
    private static string Flatten(string text) => text.Replace("\r\n", "\n").Replace("\r", "\n");

    #endregion
}
