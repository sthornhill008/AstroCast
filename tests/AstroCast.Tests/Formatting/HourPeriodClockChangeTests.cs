using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Electional;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Branch O-4, TZ's observation: an Hour Period whose start and end fall on different offsets
/// of the chart's zone prints each bound with the zone record's name for its offset, so the
/// period cannot read backwards or shorter than it is. An hour on one offset, and any hour of a
/// chart cast at an explicit offset, prints as before.
/// </summary>
/// <remarks>
/// <para>
/// Every expected value is Tier B, from outside the code: the sunrise and sunset from direct
/// <c>swe_rise_trans</c> calls on the shipped library (disc centre), and every clock reading,
/// offset and zone name from Python <c>zoneinfo</c> over <c>tzdata</c> 2026.5 (IANA 2026e)
/// (<c>o4-clocks/truth/straddle_truth.py</c>). A repeated wall clock is cast at the later
/// instant (D17).
/// </para>
/// <para>
/// New York, 2026-11-01 01:30, is cast at 06:30 UT on EST. Its hour is the eighth of the night,
/// ruled by Mercury, from sunset 21:51:55 UT on 31 October to sunrise 11:27:58 UT: 05:47:57 UT
/// (01:47:57 EDT) to 06:55:57 UT (01:55:57 EST), across the fall-back at 06:00 UT. Unlabelled,
/// its 68 minutes read as eight.
/// </para>
/// <para>
/// Moscow, 2014-10-26 01:20, is cast at 22:20 UT on MSK (UTC+3). Its hour, the seventh of the
/// night and Venus's, runs from 21:14:14 UT (01:14:14 on MSK, UTC+4) to 22:25:58 UT (01:25:58
/// on MSK, UTC+3), across the change at 22:00 UT that kept the name MSK. Both bounds share the
/// name, so each carries its full offset label.
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class HourPeriodClockChangeTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>
    /// Receives the shared ephemeris fixture used by every test in this class.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public HourPeriodClockChangeTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>en-US and the three other cultures every culture-sensitive line runs under.</summary>
    public static TheoryData<string> AllCultures => new() { "en-US", "de-DE", "sv-SE", "th-TH" };

    /// <summary>
    /// The New York hour across the 2026 fall-back prints each bound with its zone name.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void HourPeriod_StraddlingNewYorkFallBack_LabelsEachBound(string culture)
    {
        using CultureScope scope = new(culture);

        string md = ChartMarkdownFormatter.ToMarkdown(Cast("America/New_York", 40.7128, -74.006, new DateTime(2026, 11, 1, 1, 30, 0)));

        Line(md, "**Hour Ruler:**").Should().Be("**Hour Ruler:** Mercury (Night hour #8)  ");
        Line(md, "**Hour Period:**").Should().Be("**Hour Period:** 01:47:57 EDT – 01:55:57 EST (America/New_York)");
    }

    /// <summary>
    /// The Moscow hour across the 2014 change, whose two offsets share the name MSK, prints each
    /// bound with its full offset label.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void HourPeriod_StraddlingAChangeThatKeepsTheName_LabelsEachBoundWithItsOffset(string culture)
    {
        using CultureScope scope = new(culture);

        string md = ChartMarkdownFormatter.ToMarkdown(Cast("Europe/Moscow", 55.7558, 37.6173, new DateTime(2014, 10, 26, 1, 20, 0)));

        Line(md, "**Hour Ruler:**").Should().Be("**Hour Ruler:** Venus (Night hour #7)  ");
        Line(md, "**Hour Period:**").Should().Be(
            "**Hour Period:** 01:14:14 MSK (UTC+4) – 01:25:58 MSK (UTC+3) (Europe/Moscow)");
    }

    /// <summary>An hour on one offset prints as it always has: bare bounds and the zone.</summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(AllCultures))]
    public void HourPeriod_OnOneOffset_IsUnchanged(string culture)
    {
        using CultureScope scope = new(culture);

        string md = ChartMarkdownFormatter.ToMarkdown(Cast("America/New_York", 40.7128, -74.006, new DateTime(2026, 10, 15, 12, 0, 0)));

        Line(md, "**Hour Ruler:**").Should().Be("**Hour Ruler:** Moon (Day hour #6)  ");
        Line(md, "**Hour Period:**").Should().Be("**Hour Period:** 11:46:01 – 12:41:28 (America/New_York)");
    }

    /// <summary>
    /// A chart cast at an explicit offset reads its hour on that fixed clock, which never
    /// changes, so the same New York hour prints as before: bare bounds on UTC−5 (05:47:57 and
    /// 06:55:57 UT less five hours) and the offset's label.
    /// </summary>
    [Fact]
    public void HourPeriod_ExplicitOffsetChart_AtTheSameInstant_IsUnchanged()
    {
        string md = ChartMarkdownFormatter.ToMarkdown(Cast("America/New_York", 40.7128, -74.006, new DateTime(2026, 11, 1, 1, 30, 0), -5.0));

        Line(md, "**Hour Ruler:**").Should().Be("**Hour Ruler:** Mercury (Night hour #8)  ");
        Line(md, "**Hour Period:**").Should().Be("**Hour Period:** 00:47:57 – 01:55:57 UTC−5 (explicit)");
    }

    /// <summary>
    /// The calculator records each bound's instant in UTC beside its wall clock: the instants
    /// the label is read from.
    /// </summary>
    [Fact]
    public void PlanetaryHour_RecordsEachBoundsInstantInUtc()
    {
        ChartResult chart = Cast("America/New_York", 40.7128, -74.006, new DateTime(2026, 11, 1, 1, 30, 0));

        PlanetaryHour hour = chart.PlanetaryHour.Should().BeOfType<PlanetaryHourInfo>().Which.Hour;
        hour.HourStartUtc.Should().Be(new DateTime(2026, 11, 1, 5, 47, 57, DateTimeKind.Utc));
        (hour.HourStartUtc?.Kind).Should().Be(DateTimeKind.Utc);
        hour.HourEndUtc.Should().Be(new DateTime(2026, 11, 1, 6, 55, 57, DateTimeKind.Utc));
        (hour.HourEndUtc?.Kind).Should().Be(DateTimeKind.Utc);
        hour.HourStart.Should().Be(new DateTime(2026, 11, 1, 1, 47, 57));
        hour.HourEnd.Should().Be(new DateTime(2026, 11, 1, 1, 55, 57));
    }

    /// <summary>Casts an event chart at a wall clock in a zone, or at an explicit offset over it.</summary>
    private ChartResult Cast(string zone, double latitude, double longitude, DateTime local, double? explicitOffset = null) =>
        GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
        {
            Name = "Hour Period Fixture",
            LocalDateTime = local,
            TimeZone = zone,
            UseExplicitOffset = explicitOffset.HasValue,
            UtcOffsetHours = explicitOffset,
            Latitude = latitude,
            Longitude = longitude,
            LocationName = zone,
            EventType = "Event",
        });

    /// <summary>The one line of <paramref name="md"/> that starts with <paramref name="prefix"/>.</summary>
    private static string Line(string md, string prefix) =>
        md.Split('\n').Select(l => l.TrimEnd('\r')).Where(l => l.StartsWith(prefix, StringComparison.Ordinal))
            .Should().ContainSingle().Subject;
}
