using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins that every family of timed events converts its Julian Day to a
/// <see cref="DateTime"/> by rounding to the nearest second
/// (<c>JulianDayUtility.ToDateTimeUtc</c>), not by truncating each component.
/// </summary>
/// <remarks>
/// <para>
/// The probe mirrors the 04:00 UT case recorded in <c>JulianDayUtility</c>: an
/// event that falls on an exact minute whose Julian Day carries floating-point
/// noise. The natal chart below is cast for 2026-04-28 21:00:00 EDT, which is
/// 2026-04-29 01:00:00 UT exactly, and <c>JulianDayToDate</c> returns that
/// instant's hour as <c>0.9999999962747097</c>. Each family then finds an event
/// at the chart's own instant — a transit body conjoining its own natal
/// position, a planet's return to its own natal longitude, the Sun's and the
/// Moon's returns — so the true event time is 01:00:00 to well within the
/// finders' bisection precision.
/// </para>
/// <para>
/// A truncating conversion prints all of them as <c>00:59:59</c>, a second, a
/// minute and an hour early, and casts a return chart at that early instant.
/// Rounding prints <c>01:00:00</c> and casts the chart there.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class EventTimeRoundingTests
{
    private const double Latitude = 37.5385087;
    private const double Longitude = -77.43428;
    private const string LocationName = "Richmond, Virginia";
    private const string TimeZone = "America/New_York";

    /// <summary>The natal chart's own instant, and every probed event's: 2026-04-29 01:00:00 UT.</summary>
    private static readonly DateTime s_exactMinuteUtc = new(2026, 4, 29, 1, 0, 0, DateTimeKind.Utc);

    private readonly EphemerisFixture _fixture;
    private readonly Serilog.ILogger _logger;
    private readonly ChartEngine _engine;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a <see cref="ChartEngine"/>
    /// with the full production service graph.
    /// </summary>
    /// <param name="fixture">The shared Swiss Ephemeris fixture.</param>
    public EventTimeRoundingTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _logger = new Serilog.LoggerConfiguration().CreateLogger();
        DignityService dignity = new();

        _engine = new ChartEngine(
            fixture.EphemerisService, new AspectService(fixture.EphemerisService, _logger), new MidpointService(_logger), dignity,
            new MoonConditionService(), new ChartContextService(dignity, new SectService()), new FixedStarService(fixture.EphemerisService, _logger),
            new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService),
            new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, _logger), _logger);
    }

    /// <summary>
    /// A transit exact pass at an exact minute prints that minute: TR Saturn
    /// conjunct natal Saturn is exact at the chart's own instant, 01:00:00 UT.
    /// </summary>
    [Fact]
    public void TransitExactPass_AtAnExactMinute_ShouldPrintThatMinute()
    {
        TransitResult result = new TransitService(_fixture.EphemerisService, _logger).Calculate(new TransitInput
        {
            NatalChart = CalculateExactMinuteNatal(),
            ReportStartDate = new DateOnly(2026, 4, 27),
            ReportEndDate = new DateOnly(2026, 5, 3),
            IncludeMinorAspects = false,
            IncludeSolarArcs = false,
            IncludeSecondaryProgressions = false,
        });

        ActiveTransit saturn = result.ActiveTransits.Single(t =>
            t.TransitPlanet == TransitBody.Saturn && t.NatalTarget == "Saturn" && t.AspectName == "Conjunction");
        TransitLifecycleEvent exact = saturn.Lifecycle.Single(e =>
            e.EventType == TransitLifecycleEventType.Exact && e.DatePart == DateOnly.FromDateTime(s_exactMinuteUtc));

        exact.Date.Should().Be(s_exactMinuteUtc, "the exact pass falls on 01:00:00 UT and must round, not truncate");
        TransitMarkdownFormatter.ToMarkdown(result).Should()
            .Contain("2026-04-29 01:00:00 UTC").And.NotContain("2026-04-29 00:59:59 UTC");
    }

    /// <summary>
    /// A Return &amp; Ingress timeline entry prints the return's instant to the second: Jupiter's
    /// return #1 of the probe chart, the same instant <see cref="PlanetaryReturnService"/> casts
    /// its chart at, both from the passage finder through <c>JulianDayUtility.ToDateTimeUtc</c>;
    /// and the probe chart's 2026 timeline lists no Jupiter return, because Jupiter's crossing of
    /// its natal longitude at the chart's own instant is the birth's own passage, not a return
    /// (ruling T-D6, branch F). Until branch F this test pinned that crossing as a return at
    /// 01:00:00 UT; a genuine return cannot be placed on an exact minute to within the
    /// floating-point noise the probe exercises, so this family's rounding is covered by the
    /// shared converter and the other families' pins.
    /// </summary>
    [Fact]
    public void ReturnIngressTimelineEntry_PrintsTheReturnServicesInstant_AndTheBirthInstantIsNotAReturn()
    {
        ChartResult natal = CalculateExactMinuteNatal();
        ReturnIngressTimelineService service = new(_fixture.EphemerisService, _logger);

        ReturnIngressTimeline birthYear = service.Calculate(
            natal, 2026, 2026, includeIngresses: false, returnPlanets: [CelestialBodyId.Jupiter]);
        birthYear.Returns.Should().BeEmpty("Jupiter's crossing at the birth instant is the birth's own passage, not a return");

        PlanetaryReturnResult first = new PlanetaryReturnService(_fixture.EphemerisService, _engine, _logger).Calculate(
            natal, CelestialBodyId.Jupiter, 1, Latitude, Longitude, LocationName, TimeZone);
        ReturnIngressTimeline returnYear = service.Calculate(
            natal, first.ReturnChart.UtcDateTime.Year, first.ReturnChart.UtcDateTime.Year,
            includeIngresses: false, returnPlanets: [CelestialBodyId.Jupiter]);

        ReturnIngressTimelineEntry jupiter = returnYear.Returns.Should().ContainSingle(e => e.Label == "Return #1").Subject;
        jupiter.ExactUtc.Should().Be(first.ReturnChart.UtcDateTime, "the timeline's entry and the return chart are the passage's first pass, to the second");
        ReturnIngressTimelineFormatter.ToMarkdown(returnYear).Should()
            .Contain(string.Create(CultureInfo.InvariantCulture, $"| {jupiter.ExactUtc:yyyy-MM-dd HH:mm:ss} |"), "the printed time is the entry's own instant");
    }

    /// <summary>
    /// A return instant at an exact minute prints that minute: the solar return
    /// for the chart's own year falls at the chart's own instant.
    /// </summary>
    [Fact]
    public void SolarReturnInstant_AtAnExactMinute_ShouldPrintThatMinute()
    {
        SolarReturnResult result = new SolarReturnService(_fixture.EphemerisService, _engine, _logger).Calculate(
            CalculateExactMinuteNatal(), 2026, Latitude, Longitude, LocationName, TimeZone);

        result.ReturnChart.UtcDateTime.Should().Be(s_exactMinuteUtc, "the return falls on 01:00:00 UT and must round, not truncate");
        SolarReturnMarkdownFormatter.ToMarkdown(result).Should()
            .Contain("**Return Date/Time:** 2026-04-29 01:00:00 UTC");
    }

    /// <summary>
    /// A return chart is cast at the rounded instant: the lunar return chart's
    /// own time and Julian Day are those of 01:00:00 UT, and the time the report
    /// prints is the chart's own time.
    /// </summary>
    [Fact]
    public void LunarReturnChart_ShouldBeCastAtTheRoundedInstant()
    {
        IReadOnlyList<LunarReturnResult> results = new LunarReturnService(_fixture.EphemerisService, _engine, _logger).Calculate(
            CalculateExactMinuteNatal(),
            new DateTime(2026, 4, 28, 12, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc),
            Latitude, Longitude, LocationName, TimeZone);

        ChartResult chart = results.Should().ContainSingle().Subject.ReturnChart;
        double roundedInstantJd = _fixture.EphemerisService.DateToJulianDay(2026, 4, 29, 1.0);

        chart.UtcDateTime.Should().Be(s_exactMinuteUtc, "the chart is cast at the rounded return instant");
        chart.JulianDay.Should().BeApproximately(roundedInstantJd, 1e-9, "a chart cast a second early differs by 1.16e-5 days");
        LunarReturnMarkdownFormatter.ToMarkdown(results[0]).Should()
            .Contain(string.Create(CultureInfo.InvariantCulture, $"**Return Date/Time:** {chart.UtcDateTime:yyyy-MM-dd HH:mm:ss} UTC"), "the printed time is the chart's own time");
    }

    /// <summary>
    /// The probe chart: 2026-04-28 21:00:00 EDT in Richmond, Virginia, which is
    /// 2026-04-29 01:00:00 UT exactly.
    /// </summary>
    /// <returns>The calculated natal chart.</returns>
    private ChartResult CalculateExactMinuteNatal() => _engine.Calculate(new ChartInput
    {
        Name = "Exact-minute natal",
        LocalDateTime = new DateTime(2026, 4, 28, 21, 0, 0),
        TimeZone = TimeZone,
        Latitude = Latitude,
        Longitude = Longitude,
        LocationName = LocationName,
        EventType = "Event",
    });

    // The formatter writes numbers and dates with the invariant culture, so these
    // en-US expectations hold whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="TransitExactPass_AtAnExactMinute_ShouldPrintThatMinute"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void TransitExactPass_AtAnExactMinute_ShouldPrintThatMinute_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        TransitExactPass_AtAnExactMinute_ShouldPrintThatMinute();
    }

    /// <summary>
    /// <see cref="ReturnIngressTimelineEntry_PrintsTheReturnServicesInstant_AndTheBirthInstantIsNotAReturn"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void ReturnIngressTimelineEntry_PrintsTheReturnServicesInstant_AndTheBirthInstantIsNotAReturn_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        ReturnIngressTimelineEntry_PrintsTheReturnServicesInstant_AndTheBirthInstantIsNotAReturn();
    }

    /// <summary>
    /// <see cref="SolarReturnInstant_AtAnExactMinute_ShouldPrintThatMinute"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void SolarReturnInstant_AtAnExactMinute_ShouldPrintThatMinute_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        SolarReturnInstant_AtAnExactMinute_ShouldPrintThatMinute();
    }

    /// <summary>
    /// <see cref="LunarReturnChart_ShouldBeCastAtTheRoundedInstant"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void LunarReturnChart_ShouldBeCastAtTheRoundedInstant_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        LunarReturnChart_ShouldBeCastAtTheRoundedInstant();
    }
}
