using System.Globalization;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;
using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;
using FluentAssertions;
using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Every event chart is cast at its instant (branch R1): the solar, lunar and planetary returns, the
/// single ingress chart and the cardinal set, the lunation charts (for a year, and near a date) and the
/// great-conjunction chart. Each read its event's label back through
/// <see cref="ChartEngine.Calculate"/>, which before 1582-10-15 read the proleptic Gregorian label as a
/// Julian date: the chart landed 7 to 10 days after its event, or was refused when the label fell on
/// 1582-10-05 to 1582-10-14. The labels the charts print are unchanged.
/// </summary>
/// <remarks>
/// Every expected Julian Day is Tier B: direct <c>swe_calc_ut</c>, <c>swe_julday</c> and
/// <c>swe_revjul</c> on the shipped files (<c>r1-instants/truth/expected.py</c>, <c>expected2.py</c>),
/// each event found from its definition, never from the app. A chart's Julian Day is its event's to
/// the second (the chart is cast at the event taken to the whole second).
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class EventChartInstantTests
{
    /// <summary>One second, in days.</summary>
    private const double OneSecond = 1.0 / 86400.0;

    private const double Lat = 51.4769;
    private const double Lon = 0.0;
    private const string Place = "Greenwich";
    private const string Zone = "Etc/UTC";

    /// <summary>N1's solar return of 1301: Julian 1301-03-15 17:42:37 UT (Gregorian 1301-03-23 17:42:37).</summary>
    private const double N1Return1301 = 2196322.2379317195;

    /// <summary>N3's solar return of 1300, on the Julian leap day: Julian 1300-02-29 11:12:20 UT.</summary>
    private const double N3Return1300 = 2195941.966898709;

    /// <summary>N4's solar return of 1582: Julian 1582-10-01 08:47:18 UT, labelled 1582-10-11 08:47:18.</summary>
    private const double N4Return1582 = 2299156.866181115;

    /// <summary>N1's solar return of 2026: 2026-03-23 10:11:38 UT.</summary>
    private const double N1Return2026 = 2461122.9247509935;

    /// <summary>N1's lunar return picked between 1582-09-24 and 1582-10-04: Julian 1582-09-25 05:17:11 UT, labelled 1582-10-05 05:17:11.</summary>
    private const double N1LunarReturn1582 = 2299150.720267436;

    /// <summary>N1's lunar return of March 2026.</summary>
    private const double N1LunarReturn2026 = 2461112.785122139;

    /// <summary>N1's first Mars return: Julian 1302-02-23 11:46:30 UT.</summary>
    private const double N1MarsReturn1 = 2196666.990630424;

    /// <summary>N1's Mars return of 2026: the first passage with a pass in the year.</summary>
    private const double N1MarsReturn2026 = 2461119.780204564;

    /// <summary>The Full Moon nearest Julian 1300-03-01: Julian 1300-03-06.</summary>
    private const double FullMoonNear1300 = 2195947.918239336;

    /// <summary>The Full Moon nearest 2026-03-01.</summary>
    private const double FullMoonNear2026 = 2461102.9846532173;

    /// <summary>The third pass of the great conjunction of 1306: Julian 1306-07-19 11:34:48 UT.</summary>
    private const double GreatConjunction1306July = 2198273.9824976246;

    /// <summary>The great conjunction of 2020: 2020-12-21 18:20:36 UT.</summary>
    private const double GreatConjunction2020 = 2459205.2643089416;

    /// <summary>The Sun's ingresses into Aries, Cancer, Libra and Capricorn in 1300 (UT year on the calendar in force).</summary>
    private static readonly double[] Cardinal1300 =
    [
        2195954.169448379, 2196047.420651195, 2196140.736028435, 2196230.1021843543,
    ];

    /// <summary>The Sun's ingresses into Aries, Cancer, Libra and Capricorn in 2026 (UT year on the calendar in force).</summary>
    private static readonly double[] Cardinal2026 =
    [
        2461120.1152517586, 2461212.85035527, 2461306.5036287517, 2461396.368223971,
    ];

    /// <summary>Every New and Full Moon of 1300, in order (25).</summary>
    private static readonly double[] Lunations1300 =
    [
        2195888.709071468, 2195904.2972339685, 2195918.285352124, 2195933.84916242,
        2195947.918239336, 2195963.2910973425, 2195977.579751506, 2195992.6392521393,
        2196007.237291683, 2196021.9298676318, 2196036.867437864, 2196051.2118161037,
        2196066.4584432696, 2196080.5365959574, 2196096.005275846, 2196109.9482038314,
        2196125.506636266, 2196139.4742305395, 2196154.967516075, 2196169.1203464265,
        2196184.402486037, 2196198.8687677137, 2196213.8329667565, 2196228.6776569644,
        2196243.2784111616,
    ];

    /// <summary>Every New and Full Moon of 1582, in order (24).</summary>
    private static readonly double[] Lunations1582 =
    [
        2298891.4325814717, 2298906.872125744, 2298921.1723364964, 2298936.306636266,
        2298950.918405901, 2298965.6722476007, 2298980.6219211915, 2298995.0030126744,
        2299010.253151083, 2299024.3412216255, 2299039.799668851, 2299053.726603574,
        2299069.2629536814, 2299083.188995548, 2299098.658572026, 2299112.745420568,
        2299128.016155252, 2299142.399061601, 2299157.3744312427, 2299172.1352565386,
        2299186.770881875, 2299201.9143815045, 2299216.2298859833, 2299231.673307315,
    ];

    /// <summary>Every New and Full Moon of 2026, in order (25).</summary>
    private static readonly double[] Lunations2026 =
    [
        2461043.918689906, 2461059.327768644, 2461073.4230902023, 2461089.0008028816,
        2461102.9846532173, 2461118.557974995, 2461132.5916471384, 2461147.9943120484,
        2461162.224433184, 2461177.334063999, 2461191.8647315814, 2461206.6209543063,
        2461221.49770298, 2461235.9052925627, 2461251.1081458027, 2461265.233858097,
        2461280.679541765, 2461294.643753307, 2461310.2007266385, 2461324.15978514,
        2461339.674874401, 2461353.793139962, 2461369.120533517, 2461383.5360126137,
        2461398.5612788885,
    ];

    private readonly EphemerisFixture _fixture;
    private readonly ChartEngine _engine;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    /// <summary>Receives the shared ephemeris fixture and builds a fully wired engine.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public EventChartInstantTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _engine = GoldenFixtures.CreateChartEngine(fixture);
    }

    #region Solar returns

    /// <summary>
    /// N1's solar return of 1301 is cast at its event, Julian 1301-03-15 17:42:37 UT, and the chart
    /// prints that Julian date (R4); its input holds the instant's proleptic Gregorian ticks,
    /// "1301-03-23 17:42:37", as its reading at offset 0. It was cast 8 days late.
    /// </summary>
    [Fact]
    public void SolarReturn1301_Should_BeCastAtTheReturn_UnderItsLabel()
    {
        SolarReturnResult result = SolarReturns().Calculate(Natal("N1"), 1301, Lat, Lon, Place, Zone);

        result.ReturnChart.JulianDay.Should().BeApproximately(N1Return1301, OneSecond);
        result.ReturnChart.Input.LocalDateTime.Should().Be(new DateTime(1301, 3, 23, 17, 42, 37));
    }

    /// <summary>
    /// N3's solar return of 1300 falls on the Julian leap day, 1300-02-29 11:12:20 UT, and casts there.
    /// It was cast 8 days late.
    /// </summary>
    [Fact]
    public void SolarReturnOnTheJulianLeapDay_Should_BeCastAtTheReturn()
    {
        SolarReturnResult result = SolarReturns().Calculate(Natal("N3"), 1300, Lat, Lon, Place, Zone);

        result.ReturnChart.JulianDay.Should().BeApproximately(N3Return1300, OneSecond);
    }

    /// <summary>
    /// N4's solar return of 1582, Julian 1582-10-01 08:47:18 UT, labelled 1582-10-11 08:47:18 on the
    /// proleptic Gregorian calendar, a date that never occurred, now casts at its event. It was refused.
    /// </summary>
    [Fact]
    public void SolarReturnLabelledInTheTenDays_Should_BeCastAtTheReturn()
    {
        SolarReturnResult result = SolarReturns().Calculate(Natal("N4"), 1582, Lat, Lon, Place, Zone);

        result.ReturnChart.JulianDay.Should().BeApproximately(N4Return1582, OneSecond);
        result.ReturnChart.Input.LocalDateTime.Should().Be(new DateTime(1582, 10, 11, 8, 47, 18));
    }

    /// <summary>Guard: N1's solar return of 2026 is cast at its event, as before.</summary>
    [Fact]
    public void SolarReturnAfterTheSwitch_Should_BeCastAtTheReturn()
    {
        SolarReturns().Calculate(Natal("N1"), 2026, Lat, Lon, Place, Zone)
            .ReturnChart.JulianDay.Should().BeApproximately(N1Return2026, OneSecond);
    }

    #endregion

    #region Lunar and planetary returns

    /// <summary>
    /// N1's lunar return picked between 1582-09-24 and 1582-10-04 (Julian), at Julian 1582-09-25
    /// 05:17:11 UT and labelled 1582-10-05, now casts at its event. The range was refused.
    /// </summary>
    [Fact]
    public void LunarReturnLabelledInTheTenDays_Should_BeCastAtTheReturn()
    {
        IReadOnlyList<LunarReturnResult> results = LunarReturns().Calculate(
            Natal("N1"), new DateTime(1582, 9, 24), new DateTime(1582, 10, 4), Lat, Lon, Place, Zone);

        results.Should().ContainSingle();
        results[0].ReturnChart.JulianDay.Should().BeApproximately(N1LunarReturn1582, OneSecond);
    }

    /// <summary>Guard: N1's lunar return of March 2026 is cast at its event, as before.</summary>
    [Fact]
    public void LunarReturnAfterTheSwitch_Should_BeCastAtTheReturn()
    {
        IReadOnlyList<LunarReturnResult> results = LunarReturns().Calculate(
            Natal("N1"), new DateTime(2026, 3, 1), new DateTime(2026, 4, 1), Lat, Lon, Place, Zone);

        results.Should().ContainSingle();
        results[0].ReturnChart.JulianDay.Should().BeApproximately(N1LunarReturn2026, OneSecond);
    }

    /// <summary>N1's first Mars return, Julian 1302-02-23 11:46:30 UT, is cast there. It was 8 days late.</summary>
    [Fact]
    public void MarsReturnBeforeTheSwitch_Should_BeCastAtTheReturn()
    {
        PlanetaryReturns().Calculate(Natal("N1"), CelestialBodyId.Mars, 1, Lat, Lon, Place, Zone)
            .ReturnChart.JulianDay.Should().BeApproximately(N1MarsReturn1, OneSecond);
    }

    /// <summary>Guard: N1's Mars return of 2026, the first passage with a pass in the year, is cast at its event, as before.</summary>
    [Fact]
    public void MarsReturnAfterTheSwitch_Should_BeCastAtTheReturn()
    {
        PlanetaryReturns().CalculateForYear(Natal("N1"), CelestialBodyId.Mars, 2026, Lat, Lon, Place, Zone)
            .ReturnChart.JulianDay.Should().BeApproximately(N1MarsReturn2026, OneSecond);
    }

    #endregion

    #region Ingress charts

    /// <summary>
    /// The Sun's ingress into Aries of 1300, Julian 1300-03-12 16:04:00 UT, is cast there, under its
    /// label "1300-03-20 16:04:00". It was 8 days late.
    /// </summary>
    /// <param name="year">The year.</param>
    [Theory]
    [InlineData(1300)]
    [InlineData(2026)]
    public void SunIntoAriesChart_Should_BeCastAtTheIngress(int year)
    {
        IngressResult result = Ingresses().CalculateIngress(CelestialBodyId.Sun, ZodiacSign.Aries, year, Lat, Lon, Place, Zone);

        double expected = year == 1300 ? Cardinal1300[0] : Cardinal2026[0];
        ChartOf(result).JulianDay.Should().BeApproximately(expected, OneSecond);
    }

    /// <summary>
    /// The cardinal set of 1300 is cast at its four ingresses (each was 8 days late); that of 2026, as
    /// before (guard).
    /// </summary>
    /// <param name="year">The year.</param>
    [Theory]
    [InlineData(1300)]
    [InlineData(2026)]
    public void CardinalSet_Should_BeCastAtItsIngresses(int year)
    {
        IReadOnlyList<IngressResult> results = Ingresses().CalculateCardinalIngresses(year, Lat, Lon, Place, Zone);

        double[] expected = year == 1300 ? Cardinal1300 : Cardinal2026;
        results.Select(r => ChartOf(r).JulianDay).Should().HaveCount(4).And.SatisfyRespectively(
            jd => jd.Should().BeApproximately(expected[0], OneSecond),
            jd => jd.Should().BeApproximately(expected[1], OneSecond),
            jd => jd.Should().BeApproximately(expected[2], OneSecond),
            jd => jd.Should().BeApproximately(expected[3], OneSecond));
    }

    #endregion

    #region Lunation and great-conjunction charts

    /// <summary>
    /// The lunation charts of a year are cast at their lunations: 1300 (each was 7 or 8 days late),
    /// 1582 (the whole year was refused, one label falling on 1582-10-11) and 2026 (guard).
    /// </summary>
    /// <param name="year">The year.</param>
    [Theory]
    [InlineData(1300)]
    [InlineData(1582)]
    [InlineData(2026)]
    public void LunationChartsOfAYear_Should_BeCastAtTheirLunations(int year)
    {
        LunationSeries series = Lunations().CalculateLunationsForYear(year, Lat, Lon, Place, Zone);

        double[] expected = year switch { 1300 => Lunations1300, 1582 => Lunations1582, _ => Lunations2026 };
        List<double> cast = series.Lunations.Select(l => ChartOf(l).JulianDay).ToList();
        cast.Should().HaveCount(expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            cast[i].Should().BeApproximately(expected[i], OneSecond,
                "lunation {0} of {1} is cast at its event", (i + 1).ToString(CultureInfo.InvariantCulture), year.ToString(CultureInfo.InvariantCulture));
        }
    }

    /// <summary>
    /// The Full Moon chart nearest a date is cast at the Full Moon: nearest Julian 1300-03-01 (it was 8
    /// days late) and nearest 2026-03-01 (guard).
    /// </summary>
    /// <param name="year">The year of the date.</param>
    [Theory]
    [InlineData(1300)]
    [InlineData(2026)]
    public void LunationChartNearADate_Should_BeCastAtTheLunation(int year)
    {
        LunationResult result = Lunations().CalculateLunation(new DateTime(year, 3, 1), false, Lat, Lon, Place, Zone);

        ChartOf(result).JulianDay.Should().BeApproximately(year == 1300 ? FullMoonNear1300 : FullMoonNear2026, OneSecond);
    }

    /// <summary>
    /// The great-conjunction chart is cast at the conjunction: the one nearest 1306, the third pass at
    /// Julian 1306-07-19 11:34:48 UT (it was 8 days late), and the one of 2020 (guard).
    /// </summary>
    /// <param name="year">The year asked for.</param>
    [Theory]
    [InlineData(1306)]
    [InlineData(2020)]
    public void GreatConjunctionChart_Should_BeCastAtTheConjunction(int year)
    {
        GreatConjunctionService service = new(_fixture.EphemerisService, _engine, _logger);
        GreatConjunctionResult nearest = service.FindNearest(year);

        ChartResult chart = service.CastConjunctionChart(nearest, Lat, Lon, Place, Zone);

        chart.JulianDay.Should().BeApproximately(year == 1306 ? GreatConjunction1306July : GreatConjunction2020, OneSecond);
    }

    #endregion

    #region Helpers

    private SolarReturnService SolarReturns() => new(_fixture.EphemerisService, _engine, _logger);

    private LunarReturnService LunarReturns() => new(_fixture.EphemerisService, _engine, _logger);

    private PlanetaryReturnService PlanetaryReturns() => new(_fixture.EphemerisService, _engine, _logger);

    private IngressService Ingresses() => new(_fixture.EphemerisService, _engine, _logger);

    private LunationService Lunations() => new(_fixture.EphemerisService, _engine, _logger);

    /// <summary>The chart a result carries, which these calls always cast.</summary>
    private static ChartResult ChartOf(IngressResult result) =>
        result.Chart ?? throw new InvalidOperationException("The ingress carries no chart.");

    /// <summary>The chart a result carries, which these calls always cast.</summary>
    private static ChartResult ChartOf(LunationResult result) =>
        result.Chart ?? throw new InvalidOperationException("The lunation carries no chart.");

    /// <summary>
    /// A synthetic natal chart, read under D21 at UT: N1 Julian 1300-03-15 12:00, N3 Julian 1296-02-29
    /// 12:00, N4 Julian 1500-10-01 12:00, each at Greenwich.
    /// </summary>
    /// <param name="key">N1, N3 or N4.</param>
    /// <returns>The natal chart.</returns>
    private ChartResult Natal(string key)
    {
        DateTime reading = key switch
        {
            "N1" => new DateTime(1300, 3, 15, 12, 0, 0),
            "N3" => new DateTime(1296, 2, 29, 12, 0, 0),
            "N4" => new DateTime(1500, 10, 1, 12, 0, 0),
            _ => throw new ArgumentOutOfRangeException(nameof(key), key, "N1, N3 or N4."),
        };
        return _engine.Calculate(new ChartInput
        {
            Name = key,
            LocalDateTime = reading,
            TimeZone = Zone,
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = Lat,
            Longitude = Lon,
            LocationName = Place,
            EventType = "Birth",
            HouseSystem = HouseSystem.Placidus,
        });
    }

    #endregion
}
