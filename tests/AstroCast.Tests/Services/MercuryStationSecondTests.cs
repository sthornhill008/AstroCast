using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models.Electional;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins Mercury's stations to the second (O1 fix-up, 1.3.3): <see cref="MercuryRetroDetector"/> bisects every station to
/// 1e-6 day on a lattice fixed in time and takes its instant to the second, so a station prints on its own minute and
/// any range that reaches it gives it the same second.
/// </summary>
/// <remarks>
/// <para>
/// Until then each station was bisected to 0.0007 day from the half-day bracket the scan handed over, about ±30 s, by
/// where the scan began: K100's station retrograde printed 3:13 AM (Tier B 3:12 AM EDT), R01's station direct 10:54 AM
/// (10:53 AM EST) and R16's 6:58 PM (6:57 PM EDT). A plain bisection to 1e-6 day still depends on the bracket: from
/// the starts of the second independence case below it gives 02:33:31 or 02:33:30, a station 0.004 s past the half
/// second (Tier B simulation, <c>o1-bounds/fixup/out/independence-starts.txt</c>).
/// </para>
/// <para>
/// Expected values are Tier B (<c>o1-bounds/fixup/truth/stations.py</c>: direct <c>swe_calc_ut</c>, the sign of
/// Mercury's speed in longitude bisected to 1e-9 day), read in New York with Python <c>zoneinfo</c>
/// (<c>truth/expected.py</c>, <c>out/expected.json</c>).
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class MercuryStationSecondTests
{
    private const string NewYork = "America/New_York";

    /// <summary>The detector's precision: a 1e-6-day cell's half width, and the rounding to the second.</summary>
    private static readonly TimeSpan Precision = TimeSpan.FromSeconds(1);

    private readonly EphemerisFixture _fixture;
    private readonly TzdbTimeZoneRules _rules = new();

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public MercuryStationSecondTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>
    /// The six stations of 2026, each from a range of two days either side of it: within a second of Tier B, and
    /// printed in New York on Tier B's minute. A station retrograde opens its period inside the range and a station
    /// direct closes it there; the other end of each period is searched outside.
    /// </summary>
    /// <param name="retrograde">True for a station retrograde (the period's start), false for a station direct (its end).</param>
    /// <param name="tierBUtc">The station by Tier B, to the second (UT).</param>
    /// <param name="printedNewYork">Its reading in New York, as the export prints it.</param>
    [Theory]
    [InlineData(true, "2026-02-26 06:48:10", "Thu, 26 Feb 2026 1:48 AM")]
    [InlineData(false, "2026-03-20 19:32:50", "Fri, 20 Mar 2026 3:32 PM")]
    [InlineData(true, "2026-06-29 17:35:55", "Mon, 29 Jun 2026 1:35 PM")]
    [InlineData(false, "2026-07-23 22:57:52", "Thu, 23 Jul 2026 6:57 PM")]
    [InlineData(true, "2026-10-24 07:12:43", "Sat, 24 Oct 2026 3:12 AM")]
    [InlineData(false, "2026-11-13 15:53:53", "Fri, 13 Nov 2026 10:53 AM")]
    public void FindRetrogradePeriods_TheStationsOf2026_AreTierBsToTheSecond_AndPrintOnItsMinute(bool retrograde, string tierBUtc, string printedNewYork)
    {
        DateTime truth = Utc(tierBUtc);

        DisqualifiedPeriod period = new MercuryRetroDetector(_fixture.EphemerisService, _rules)
            .FindRetrogradePeriods(JulianDay(truth.AddDays(-2.0)), JulianDay(truth.AddDays(2.0)), NewYork)
            .Should().ContainSingle().Which;

        DateTime stationUtc = retrograde ? period.StartUtc : period.EndUtc;
        DateTime stationLocal = retrograde ? period.Start : period.End;
        stationUtc.Should().BeCloseTo(truth, Precision, "the station is Tier B's to the second");
        stationLocal.ToString("ddd, dd MMM yyyy h:mm tt", CultureInfo.InvariantCulture).Should().Be(printedNewYork,
            "the station prints on Tier B's minute");
    }

    /// <summary>
    /// The same station retrograde from six range starts: five a few hours apart with the station inside the range,
    /// and one two days after it, where Mercury is retrograde at the start and the station is searched back. Each
    /// gives the same second, within a second of Tier B.
    /// </summary>
    /// <param name="tierBUtc">The station by Tier B (UT), with its fraction of a second.</param>
    /// <param name="year">The year of the first range start.</param>
    /// <param name="month">Its month.</param>
    /// <param name="day">Its day: two days before the station, 00:00 UT.</param>
    [Theory]
    [InlineData("2026-10-24 07:12:42.921", 2026, 10, 22)]
    [InlineData("2032-07-10 02:33:30.504", 2032, 7, 8)]
    public void FindRetrogradePeriods_FromSixRangeStarts_GivesTheStationTheSameSecond(string tierBUtc, int year, int month, int day)
    {
        DateTime truth = DateTime.SpecifyKind(DateTime.ParseExact(tierBUtc, "yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture), DateTimeKind.Utc);
        DateTime first = new(year, month, day, 0, 0, 0, DateTimeKind.Utc);
        DateTime[] starts =
        [
            first, first.AddHours(2.0), first.AddHours(4.0), first.AddHours(7.0), first.AddHours(10.0),
            first.AddDays(4.0).AddHours(1.0),
        ];
        MercuryRetroDetector detector = new(_fixture.EphemerisService, _rules);

        List<DateTime> stations = starts
            .Select(start => detector.FindRetrogradePeriods(JulianDay(start), JulianDay(start.AddDays(4.0)), NewYork)
                .Should().ContainSingle().Which.StartUtc)
            .ToList();

        stations.Distinct().Should().ContainSingle("the same station gives the same second from any range that reaches it");
        stations[0].Should().BeCloseTo(truth, Precision, "the station is Tier B's to the second");
    }

    /// <summary>
    /// K100 (Richmond, a contract from 28 October to 3 November 2026, Mercury retrograde avoided) prints its
    /// retrograde from "Sat, 24 Oct 2026 3:12 AM" to "Fri, 13 Nov 2026 10:53 AM": the stations' own minutes in New York.
    /// </summary>
    [Fact]
    public void ToMarkdown_K100_PrintsTheRetrogradeOnItsStationsMinutes()
    {
        ElectionalResult result = new ElectionalService(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture)).Calculate(new ElectionalInput
        {
            NatalChart = GoldenFixtures.J2000Nyc(_fixture),
            ElectionType = ElectionType.Contract,
            ElectionGoal = "K100",
            RangeStart = new DateOnly(2026, 10, 28),
            RangeEnd = new DateOnly(2026, 11, 3),
            Latitude = 37.5407,
            Longitude = -77.436,
            LocationName = "Richmond, Virginia",
            TimeZone = NewYork,
            CandidateCount = 3,
            AvoidMercuryRetrograde = true,
        });

        ElectionalMarkdownFormatter.ToMarkdown(result).Should().Contain(
            "| Sat, 24 Oct 2026 3:12 AM | Fri, 13 Nov 2026 10:53 AM | Mercury retrograde |");
    }

    /// <summary>The Julian Day (UT) of an instant.</summary>
    /// <param name="utc">The instant.</param>
    /// <returns>Its Julian Day.</returns>
    private double JulianDay(DateTime utc) =>
        _fixture.EphemerisService.DateToJulianDay(utc.Year, utc.Month, utc.Day, utc.TimeOfDay.TotalHours);

    /// <summary>A UT instant from "yyyy-MM-dd HH:mm:ss".</summary>
    /// <param name="text">The instant.</param>
    /// <returns>It, as <see cref="DateTimeKind.Utc"/>.</returns>
    private static DateTime Utc(string text) =>
        DateTime.SpecifyKind(DateTime.ParseExact(text, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture), DateTimeKind.Utc);
}
