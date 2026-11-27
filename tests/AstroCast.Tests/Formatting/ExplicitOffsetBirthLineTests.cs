using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// E2-5 (branch O-4): the six exports that print a natal chart's wall clock beside its zone
/// — relocation, astrocartography, electional, transit, daily horoscope and composite —
/// label an explicit-offset chart's wall clock with its own label, as the chart export's
/// Date/Time line does, never with the zone whose rules the offset overrode. A zone chart's
/// line is unchanged, and the daily horoscope's header says which clock its days are on.
/// </summary>
/// <remarks>
/// <para>
/// The chart is person A (invented): 1947-10-05 09:30:00 at Harrisburg, Pennsylvania, entered
/// with the explicit offset −5 over the zone America/New_York. Every expected label is the
/// input's own offset (Tier A): −5 entered by the user is "UTC−5 (explicit)", the form card
/// 6 gives it, with U+2212 as the sign.
/// </para>
/// <para>
/// Each test runs under en-US and again under de-DE, sv-SE and th-TH: the lines read the
/// same whatever the machine's regional settings.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ExplicitOffsetBirthLineTests
{
    /// <summary>The explicit offset's label: the input's own −5, as card 6 writes it.</summary>
    private const string ExplicitLabel = "UTC−5 (explicit)";

    /// <summary>The daily horoscope's statement of the clock its days are on.</summary>
    private const string DailyClockLine =
        "**Timezone:** America/New_York — the days and every time in them (void of course, exact, entering and leaving orb, the Moon's sign change) are on this zone's clock";

    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger;

    /// <summary>
    /// Receives the shared ephemeris fixture used by every test in this class.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public ExplicitOffsetBirthLineTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _logger = new LoggerConfiguration().CreateLogger();
    }

    /// <summary>en-US and the three other cultures every culture-sensitive line runs under.</summary>
    public static TheoryData<string> AllCultures => new() { "en-US", "de-DE", "sv-SE", "th-TH" };

    #region Relocation

    /// <summary>The relocation export's birth line carries the explicit offset's label.</summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Relocation_ExplicitOffsetChart_BirthLine_CarriesItsOwnLabel(string culture)
    {
        using CultureScope scope = new(culture);

        string md = Relocation(Chart(explicitOffset: true));

        Line(md, "**Birth Date/Time:**").Should().Be($"**Birth Date/Time:** 1947-10-05 09:30:00 {ExplicitLabel}  ");
    }

    /// <summary>A zone chart's relocation birth line is unchanged: the zone in parentheses.</summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Relocation_ZoneChart_BirthLine_IsUnchanged(string culture)
    {
        using CultureScope scope = new(culture);

        string md = Relocation(Chart(explicitOffset: false));

        Line(md, "**Birth Date/Time:**").Should().Be("**Birth Date/Time:** 1947-10-05 09:30:00 (America/New_York)  ");
    }

    #endregion

    #region Astrocartography

    /// <summary>The astrocartography export's Born line carries the explicit offset's label.</summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Astrocartography_ExplicitOffsetChart_BornLine_CarriesItsOwnLabel(string culture)
    {
        using CultureScope scope = new(culture);

        string md = Astrocartography(Chart(explicitOffset: true));

        Line(md, "**Born:**").Should().Be($"**Born:** 1947-10-05 09:30:00 {ExplicitLabel}, Harrisburg, Pennsylvania  ");
    }

    /// <summary>A zone chart's astrocartography Born line is unchanged: the bare zone.</summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Astrocartography_ZoneChart_BornLine_IsUnchanged(string culture)
    {
        using CultureScope scope = new(culture);

        string md = Astrocartography(Chart(explicitOffset: false));

        Line(md, "**Born:**").Should().Be("**Born:** 1947-10-05 09:30:00 America/New_York, Harrisburg, Pennsylvania  ");
    }

    #endregion

    #region Electional

    /// <summary>The electional export's Natal Date line carries the explicit offset's label.</summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Electional_ExplicitOffsetChart_NatalDateLine_CarriesItsOwnLabel(string culture)
    {
        using CultureScope scope = new(culture);

        string md = Electional(Chart(explicitOffset: true));

        Line(md, "**Natal Date:**").Should().Be($"**Natal Date:** 1947-10-05, 09:30:00 {ExplicitLabel}, Harrisburg, Pennsylvania  ");
    }

    /// <summary>A zone chart's electional Natal Date line is unchanged: the bare zone.</summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Electional_ZoneChart_NatalDateLine_IsUnchanged(string culture)
    {
        using CultureScope scope = new(culture);

        string md = Electional(Chart(explicitOffset: false));

        Line(md, "**Natal Date:**").Should().Be("**Natal Date:** 1947-10-05, 09:30:00 America/New_York, Harrisburg, Pennsylvania  ");
    }

    #endregion

    #region Transit

    /// <summary>The transit export's Natal Date line carries the explicit offset's label.</summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Transit_ExplicitOffsetChart_NatalDateLine_CarriesItsOwnLabel(string culture)
    {
        using CultureScope scope = new(culture);

        string md = TransitMarkdownFormatter.ToMarkdown(Transits(Chart(explicitOffset: true)));

        Line(md, "**Natal Date:**").Should().Be($"**Natal Date:** 1947-10-05, 09:30:00 {ExplicitLabel}, Harrisburg, Pennsylvania  ");
    }

    /// <summary>A zone chart's transit Natal Date line is unchanged: the bare zone.</summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Transit_ZoneChart_NatalDateLine_IsUnchanged(string culture)
    {
        using CultureScope scope = new(culture);

        string md = TransitMarkdownFormatter.ToMarkdown(Transits(Chart(explicitOffset: false)));

        Line(md, "**Natal Date:**").Should().Be("**Natal Date:** 1947-10-05, 09:30:00 America/New_York, Harrisburg, Pennsylvania  ");
    }

    #endregion

    #region Daily horoscope

    /// <summary>
    /// The daily horoscope's Natal Date line carries the explicit offset's label, and its
    /// header says the days are on the zone's clock: the explicit offset fixes only the birth
    /// instant, and the days are the reader's present days.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Daily_ExplicitOffsetChart_NatalDateLine_CarriesItsOwnLabel_AndTheDaysStayOnTheZone(string culture)
    {
        using CultureScope scope = new(culture);

        string md = Daily(Chart(explicitOffset: true));

        Line(md, "**Natal Date:**").Should().Be($"**Natal Date:** 1947-10-05, 09:30:00 {ExplicitLabel}, Harrisburg, Pennsylvania  ");
        Line(md, "**Timezone:**").Should().Be(DailyClockLine + "  ");
    }

    /// <summary>
    /// A zone chart's daily Natal Date line is unchanged, and its header says the days are on
    /// the same zone's clock.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Daily_ZoneChart_NatalDateLine_IsUnchanged_AndTheDaysAreOnTheZone(string culture)
    {
        using CultureScope scope = new(culture);

        string md = Daily(Chart(explicitOffset: false));

        Line(md, "**Natal Date:**").Should().Be("**Natal Date:** 1947-10-05, 09:30:00 America/New_York, Harrisburg, Pennsylvania  ");
        Line(md, "**Timezone:**").Should().Be(DailyClockLine + "  ");
    }

    #endregion

    #region Composite

    /// <summary>
    /// The composite export's source-chart summary labels an explicit-offset chart's Date/Time
    /// with its own label, and leaves the zone chart beside it as it was.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Composite_ExplicitOffsetChart_SourceDateTime_CarriesItsOwnLabel(string culture)
    {
        using CultureScope scope = new(culture);

        string md = Composite(Chart(explicitOffset: true), GoldenFixtures.SecondaryNatal(_fixture));

        Lines(SourceCharts(md), "**Date/Time:**").Should().Equal(
            $"**Date/Time:** 1947-10-05 09:30:00 {ExplicitLabel}  ",
            "**Date/Time:** 1970-06-15 12:00:00 (America/Los_Angeles)  ");
    }

    /// <summary>A zone chart's composite source Date/Time is unchanged: the zone in parentheses.</summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void Composite_ZoneChart_SourceDateTime_IsUnchanged(string culture)
    {
        using CultureScope scope = new(culture);

        string md = Composite(Chart(explicitOffset: false), GoldenFixtures.SecondaryNatal(_fixture));

        Lines(SourceCharts(md), "**Date/Time:**").Should().Equal(
            "**Date/Time:** 1947-10-05 09:30:00 (America/New_York)  ",
            "**Date/Time:** 1970-06-15 12:00:00 (America/Los_Angeles)  ");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Casts person A (invented): 1947-10-05 09:30:00 at Harrisburg, Pennsylvania, in the zone
    /// America/New_York, with or without the explicit offset −5.
    /// </summary>
    /// <param name="explicitOffset">Whether the chart is cast at the explicit offset −5.</param>
    /// <returns>The cast chart.</returns>
    private ChartResult Chart(bool explicitOffset) =>
        GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
        {
            Name = "Explicit Offset Fixture",
            LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
            TimeZone = "America/New_York",
            UseExplicitOffset = explicitOffset,
            UtcOffsetHours = explicitOffset ? -5.0 : null,
            Latitude = 40.2737,
            Longitude = -76.8844,
            LocationName = "Harrisburg, Pennsylvania",
            EventType = "Birth",
            HouseSystem = HouseSystem.Placidus,
        });

    /// <summary>The chart relocated to Richmond, Virginia, as the relocation export.</summary>
    private string Relocation(ChartResult natal)
    {
        RelocationResult result = new RelocationService(GoldenFixtures.CreateChartEngine(_fixture), _logger)
            .Calculate(natal, new RelocationInput { Latitude = 37.5407, Longitude = -77.436, LocationName = "Richmond, Virginia" });
        return RelocationMarkdownFormatter.ToMarkdown(result, GoldenFixtures.GoldenClock());
    }

    /// <summary>The chart's astrocartography export, without a crossing report.</summary>
    private string Astrocartography(ChartResult natal)
    {
        AcgResult acg = new AstrocartographyService(_fixture.EphemerisService, _logger).Calculate(natal);
        return AstrocartographyFormatter.ToMarkdown(acg);
    }

    /// <summary>A one-candidate electional search over two days at Richmond, Virginia.</summary>
    private string Electional(ChartResult natal)
    {
        ElectionalResult result = new ElectionalService(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture))
            .Calculate(new ElectionalInput
            {
                NatalChart = natal,
                ElectionType = ElectionType.Other,
                ElectionGoal = "Clock label sample",
                RangeStart = new DateOnly(2026, 10, 5),
                RangeEnd = new DateOnly(2026, 10, 6),
                Latitude = 37.5407,
                Longitude = -77.436,
                LocationName = "Richmond, Virginia",
                TimeZone = "America/New_York",
                CandidateCount = 1,
            });
        return ElectionalMarkdownFormatter.ToMarkdown(result);
    }

    /// <summary>Two days of transits to the chart, 2026-10-01 to 2026-10-02.</summary>
    private TransitResult Transits(ChartResult natal) =>
        new TransitService(_fixture.EphemerisService, _logger).Calculate(new TransitInput
        {
            NatalChart = natal,
            ReportStartDate = new DateOnly(2026, 10, 1),
            ReportEndDate = new DateOnly(2026, 10, 2),
            IncludeMinorAspects = false,
            IncludeSolarArcs = false,
            IncludeSecondaryProgressions = false,
        });

    /// <summary>
    /// The daily horoscope for the chart's transits, its days on the chart's zone, as the
    /// heads build it.
    /// </summary>
    private string Daily(ChartResult natal)
    {
        TransitResult transits = Transits(natal);
        string zone = natal.Input.TimeZone;
        IReadOnlyList<DailyHoroscopeDay> days = new DailyHoroscopeService(
            _fixture.EphemerisService, new VoidOfCourseScanner(_fixture.EphemerisService)).BuildDays(transits, zone);
        return DailyHoroscopeMarkdownFormatter.ToMarkdown(transits, days, null, null, zone);
    }

    /// <summary>The composite of two charts, its houses cast at the mean of the two birth latitudes.</summary>
    private string Composite(ChartResult a, ChartResult b)
    {
        DignityService dignity = new();
        CompositeService service = new(
            _fixture.EphemerisService,
            new AspectService(_fixture.EphemerisService, _logger),
            new MidpointService(_logger),
            dignity,
            new MoonConditionService(),
            new ChartContextService(dignity, new SectService()),
            new FixedStarService(_fixture.EphemerisService, _logger),
            _logger);
        return CompositeMarkdownFormatter.ToMarkdown(service.Calculate(a, b));
    }

    /// <summary>
    /// The composite export from its first source chart's heading on: the source summaries,
    /// without the composite chart's own Date/Time line, which is on UTC and not a birth clock.
    /// </summary>
    private static string SourceCharts(string md) => md[md.IndexOf("## Source Chart A:", StringComparison.Ordinal)..];

    /// <summary>The one line of <paramref name="md"/> that starts with <paramref name="prefix"/>.</summary>
    private static string Line(string md, string prefix) => Lines(md, prefix).Should().ContainSingle().Subject;

    /// <summary>Every line of <paramref name="md"/> that starts with <paramref name="prefix"/>, in order.</summary>
    private static List<string> Lines(string md, string prefix) =>
        md.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.StartsWith(prefix, StringComparison.Ordinal)).ToList();

    #endregion
}
