using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the two labels branch B left for Steve's ruling, the same mislabel it fixed elsewhere. A solar
/// eclipse's phase table names the start and end of the central phase over the whole Earth
/// "Central eclipse begins (whole Earth)" and "Central eclipse ends (whole Earth)", where it said
/// "Totality begins" and "Totality ends"; a lunar eclipse keeps its totality rows. The lunation
/// export's summary line and section heading over the lunations flagged near a node read
/// "Lunations near a node", where they said "Possible Eclipses".
/// </summary>
/// <remarks>
/// Expected instants are Tier B, from the library's own eclipse searches called directly
/// (<c>swe_sol_eclipse_when_glob</c>, <c>swe_lun_eclipse_when</c>), rounded to the second as the
/// export prints them; the count of lunations near a node is branch B's Tier B count for 2028. Each
/// test runs under en-US, de-DE, sv-SE and th-TH, which must print the same text.
/// </remarks>
[Collection("Ephemeris")]
public sealed class EclipsePhaseAndLunationSectionTests
{
    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public EclipsePhaseAndLunationSectionTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>The cultures each label is checked under.</summary>
    public static TheoryData<string> AllCultures => new() { "en-US", "de-DE", "sv-SE", "th-TH" };

    /// <summary>
    /// The total solar eclipse of 2026-08-12: the central phase over the whole Earth runs from
    /// 16:58:09.355 to 18:34:00.697 UT (Tier B), printed 16:58:09 and 18:34:01. The rows are named
    /// for it, and the eclipse prints no "Totality begins" or "Totality ends".
    /// </summary>
    /// <param name="culture">The culture the export is written under.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(AllCultures))]
    public void SolarEclipse20260812_PhaseRows_NameTheCentralEclipseOverTheWholeEarth(string culture)
    {
        using CultureScope scope = new(culture);

        string section = DetailSection(EclipseExport(), "Total Solar Eclipse, August 12, 2026");

        section.Should().Contain("| Central eclipse begins (whole Earth) | 16:58:09 |")
            .And.Contain("| Central eclipse ends (whole Earth) | 18:34:01 |")
            .And.NotContain("Totality begins")
            .And.NotContain("Totality ends");
    }

    /// <summary>
    /// Guard: the total lunar eclipse of 2026-03-03 keeps its totality rows, 11:04:32.304 and
    /// 12:02:51.495 UT (Tier B), printed 11:04:32 and 12:02:51.
    /// </summary>
    /// <param name="culture">The culture the export is written under.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(AllCultures))]
    public void LunarEclipse20260303_KeepsItsTotalityRows(string culture)
    {
        using CultureScope scope = new(culture);

        string section = DetailSection(EclipseExport(), "Total Lunar Eclipse, March 3, 2026");

        section.Should().Contain("| Totality begins | 11:04:32 |")
            .And.Contain("| Totality ends | 12:02:51 |")
            .And.NotContain("Central eclipse");
    }

    /// <summary>
    /// The lunations of 2028 near a node are five (branch B, Tier B): the export's summary line reads
    /// "Lunations near a node: 5" and its section heading "Lunations near a node"; it no longer says
    /// "Possible Eclipses".
    /// </summary>
    /// <param name="culture">The culture the export is written under.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(AllCultures))]
    public void Lunations2028_SectionAndSummary_NameLunationsNearANode(string culture)
    {
        using CultureScope scope = new(culture);
        LunationSeries series = new LunationService(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _logger)
            .FindLunations(2028, 2028);

        string markdown = LunationFormatter.SeriesToMarkdown(series);

        markdown.Should().Contain("**Lunations near a node:** 5")
            .And.Contain("## Lunations near a node")
            .And.NotContain("Possible Eclipses");
    }

    #region Helpers

    private string EclipseExport() =>
        EclipseFormatter.SeriesToMarkdown(new EclipseService(_fixture.EphemerisService, _logger).FindEclipses(2026, 2026));

    /// <summary>The detail section of the eclipse whose heading ends with <paramref name="title"/>.</summary>
    private static string DetailSection(string markdown, string title)
    {
        int start = markdown.IndexOf(title, StringComparison.Ordinal);
        start.Should().BeGreaterThan(0, "the export has a section for {0}", title);
        int end = markdown.IndexOf("### #", start, StringComparison.Ordinal);
        return end < 0 ? markdown[start..] : markdown[start..end];
    }

    #endregion
}
