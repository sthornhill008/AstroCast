using AstroCast.Core.Models;
using AstroCast.Core.Models.Progressions;
using AstroCast.Core.Models.SolarArcs;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;
using AstroCast.Tests.Services.MinorPoints;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="ProgressedHitFinder"/> against Tier B: an independent pyswisseph script
/// (arcs_truth.py, branch Arcs) over the shipped ephemeris files, with its own daily scan and 1 s
/// bisection, whose natal casts agree with the engine's to the bit. A hit matches when it names the same
/// progressed point, aspect angle and natal point, falls on the same UT date and lies within 60 s.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ProgressedHitFinderTests
{
    /// <summary>Tolerance on every Tier B instant, in days.</summary>
    private const double SixtySecondsInDays = 60.0 / 86400.0;

    private readonly EphemerisFixture _fixture;
    private readonly ProgressedHitFinder _finder;
    private readonly ChartResult _j2000;

    /// <summary>Casts J2000 NYC and builds the finder over the shared ephemeris.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ProgressedHitFinderTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _finder = new ProgressedHitFinder(fixture.EphemerisService);
        _j2000 = GoldenFixtures.J2000Nyc(fixture);
    }

    #region Tier B expectations

    /// <summary>
    /// Tier B, J2000 NYC, 2026-01-01 to 2027-12-31 (J2000.hits.txt): 14 progressed hits in time order,
    /// the progressed Moon's conjunction with its own natal place among them.
    /// (point, aspect angle, natal point, JD UT, UT date).
    /// </summary>
    private static readonly (string Point, int Aspect, string Target, double Jd, string Date)[] J2000Expected =
    [
        ("Moon", 120, "Mars", 2461058.312054, "2026-01-17"),
        ("Mercury", 0, "Uranus", 2461066.488598, "2026-01-25"),
        ("Moon", 0, "MC", 2461073.079350, "2026-02-01"),
        ("Moon", 60, "Mercury", 2461173.359623, "2026-05-12"),
        ("Moon", 90, "Neptune", 2461211.765827, "2026-06-20"),
        ("Moon", 90, "True Node", 2461234.229359, "2026-07-12"),
        ("Moon", 60, "ASC", 2461242.737934, "2026-07-21"),
        ("ASC", 0, "Neptune", 2461243.377071, "2026-07-21"),
        ("Venus", 0, "ASC", 2461327.817562, "2026-10-14"),
        ("Moon", 60, "Sun", 2461424.851986, "2027-01-19"),
        ("Moon", 180, "Saturn", 2461425.651279, "2027-01-20"),
        ("ASC", 180, "True Node", 2461454.547947, "2027-02-18"),
        ("Moon", 0, "Moon", 2461513.375599, "2027-04-17"),
        ("Moon", 90, "Uranus", 2461558.036892, "2027-06-01"),
    ];

    /// <summary>
    /// Tier B, N1 (Julian 1300-03-15 12:00 UT, Greenwich), proleptic 1400-01-01 to 1401-12-31
    /// (N1-1400.hits.txt): 13 progressed hits, every instant before the calendar switch.
    /// </summary>
    private static readonly (string Point, int Aspect, string Target, double Jd, string Date)[] N1In1400Expected =
    [
        ("Mercury", 90, "Jupiter", 2232497.161739, "1400-04-08"),
        ("Moon", 60, "ASC", 2232578.085133, "1400-06-28"),
        ("Moon", 180, "MC", 2232685.086742, "1400-10-13"),
        ("MC", 90, "Uranus", 2232738.626431, "1400-12-06"),
        ("Moon", 180, "Sun", 2232739.902653, "1400-12-07"),
        ("Moon", 180, "Venus", 2232781.909924, "1401-01-18"),
        ("Venus", 60, "Uranus", 2232845.445850, "1401-03-22"),
        ("Moon", 0, "Uranus", 2232891.810963, "1401-05-08"),
        ("Mercury", 90, "Neptune", 2232906.926235, "1401-05-23"),
        ("Mercury", 0, "ASC", 2232923.639378, "1401-06-09"),
        ("Moon", 60, "Saturn", 2232942.275822, "1401-06-27"),
        ("Sun", 120, "True Node", 2232954.396694, "1401-07-09"),
        ("Moon", 90, "Moon", 2233055.051716, "1401-10-18"),
    ];

    /// <summary>
    /// Tier B, M35 (1960-02-05 12:00 UT, Greenwich; M35.hits.txt): progressed Mercury's three passes over
    /// the square to the natal Ascendant, direct in 1974, retrograde in 1997 after its 1985 station, and
    /// direct again in 2019 after its 2007 station.
    /// </summary>
    private static readonly (double Jd, string Date)[] M35MercurySquareAscendant =
    [
        (2442291.838688, "1974-09-01"),
        (2450504.804607, "1997-02-25"),
        (2458732.200932, "2019-09-05"),
    ];

    #endregion

    #region Tier B

    /// <summary>Every Tier B hit of J2000 NYC in 2026 and 2027 is listed, none extra, each on its UT date and within 60 s.</summary>
    [Fact]
    public void FindHits_J2000Nyc_MatchesTierB()
    {
        IReadOnlyList<ProgressedHit> hits = _finder.FindHits(_j2000, new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31));

        AssertMatchesTierB(hits, J2000Expected);
    }

    /// <summary>
    /// Before the calendar switch the finder works on instants: N1's hits in proleptic 1400 and 1401
    /// match Tier B (the Julian labels are the export's business, branch Modes).
    /// </summary>
    [Fact]
    public void FindHits_BeforeTheSwitch_MatchesTierB()
    {
        IReadOnlyList<ProgressedHit> hits = _finder.FindHits(N1(), new DateOnly(1400, 1, 1), new DateOnly(1401, 12, 31));

        AssertMatchesTierB(hits, N1In1400Expected);
    }

    /// <summary>
    /// A progressed body that stations crosses a point more than once, and every pass is listed in order:
    /// M35's progressed Mercury squares the natal Ascendant three times in 45 years.
    /// </summary>
    [Fact]
    public void FindHits_StationingMercury_ListsEveryPass()
    {
        IReadOnlyList<ProgressedHit> hits = _finder.FindHits(M35(), new DateOnly(1974, 1, 1), new DateOnly(2020, 12, 31));

        List<ProgressedHit> passes = hits
            .Where(h => h.ProgressedPoint == "Mercury" && h.AspectAngle == 90.0 && h.NatalPoint == "ASC")
            .ToList();

        passes.Should().HaveCount(3);
        for (int i = 0; i < passes.Count; i++)
        {
            passes[i].JulianDayUt.Should().BeApproximately(M35MercurySquareAscendant[i].Jd, SixtySecondsInDays, $"pass {i + 1}");
            DateOnly.FromDateTime(passes[i].InstantUtc).Should().Be(
                DateOnly.ParseExact(M35MercurySquareAscendant[i].Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture));
        }
        hits.Should().BeInAscendingOrder(h => h.JulianDayUt);
    }

    #endregion

    #region Rules

    /// <summary>
    /// The progressed Midheaven is the solar arc Midheaven: every P MC hit has an SA MC hit to the same
    /// natal point by the same aspect, at the same instant to the second.
    /// </summary>
    [Fact]
    public void FindHits_ProgressedMidheaven_IsTheSolarArcMidheaven()
    {
        ChartResult m35 = M35();
        DateOnly start = new(1974, 1, 1);
        DateOnly end = new(2020, 12, 31);
        IReadOnlyList<ProgressedHit> progressed = _finder.FindHits(m35, start, end);
        IReadOnlyList<SolarArcHit> directed = new SolarArcHitFinder(_fixture.EphemerisService).FindHits(m35, start, end);

        List<ProgressedHit> midheaven = progressed.Where(h => h.ProgressedPoint == "MC").ToList();
        midheaven.Should().NotBeEmpty();
        foreach (ProgressedHit hit in midheaven)
        {
            SolarArcHit twin = directed.Single(d =>
                d.DirectedPoint == "MC" && !d.IsMidpoint && d.Target == hit.NatalPoint && d.AspectAngle == hit.AspectAngle
                && Math.Abs(d.JulianDayUt - hit.JulianDayUt) < 1.0);
            twin.JulianDayUt.Should().BeApproximately(hit.JulianDayUt, 1.0 / 86400.0, $"P MC {hit.AspectName} {hit.NatalPoint}");
        }
    }

    /// <summary>Only the five major aspects are searched.</summary>
    [Fact]
    public void FindHits_MajorsOnly()
    {
        IReadOnlyList<ProgressedHit> hits = _finder.FindHits(_j2000, new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31));

        hits.Should().NotBeEmpty();
        hits.Should().OnlyContain(h => h.AspectAngle == 0.0 || h.AspectAngle == 60.0 || h.AspectAngle == 90.0
            || h.AspectAngle == 120.0 || h.AspectAngle == 180.0);
        hits.Select(h => (h.AspectAngle, h.AspectName, h.AspectAbbreviation)).Distinct().Should().BeSubsetOf(
        [
            (0.0, "Conjunction", "cnj"), (60.0, "Sextile", "sxt"), (90.0, "Square", "sqr"),
            (120.0, "Trine", "tri"), (180.0, "Opposition", "opp"),
        ]);
    }

    /// <summary>
    /// A progressed body may aspect its own natal place: J2000's progressed Moon returns to the natal
    /// Moon on 2027-04-17 at 21:00:52 UT.
    /// </summary>
    [Fact]
    public void FindHits_IncludesABodyToItsOwnNatalPlace()
    {
        IReadOnlyList<ProgressedHit> hits = _finder.FindHits(_j2000, new DateOnly(2027, 1, 1), new DateOnly(2027, 12, 31));

        ProgressedHit lunarReturn = hits.Single(h => h.ProgressedPoint == "Moon" && h.NatalPoint == "Moon");
        (lunarReturn.AspectAngle, lunarReturn.AspectName, lunarReturn.AspectAbbreviation).Should().Be((0.0, "Conjunction", "cnj"));
        lunarReturn.InstantUtc.Should().BeCloseTo(new DateTime(2027, 4, 17, 21, 0, 52, DateTimeKind.Utc), TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// The progressed angles skip their own natal place: over 47 years M35's solar arc passes 60°, so the
    /// progressed Midheaven would sextile the natal Midheaven, and does not; its progressed Sun's sextile
    /// to the natal Sun, the same arc, is listed.
    /// </summary>
    [Fact]
    public void FindHits_AnglesSkipSelf()
    {
        IReadOnlyList<ProgressedHit> hits = _finder.FindHits(M35(), new DateOnly(1974, 1, 1), new DateOnly(2020, 12, 31));

        hits.Should().NotContain(h => (h.ProgressedPoint == "MC" || h.ProgressedPoint == "ASC") && h.NatalPoint == h.ProgressedPoint);
        hits.Should().ContainSingle(h => h.ProgressedPoint == "Sun" && h.NatalPoint == "Sun" && h.AspectAngle == 60.0);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Asserts the hits are the Tier B list: the same count, and in order the same point, aspect and
    /// natal point, the same UT date, and an instant within 60 s.
    /// </summary>
    /// <param name="hits">The finder's hits.</param>
    /// <param name="expected">The Tier B hits, in time order.</param>
    private static void AssertMatchesTierB(
        IReadOnlyList<ProgressedHit> hits, (string Point, int Aspect, string Target, double Jd, string Date)[] expected)
    {
        hits.Select(h => (h.ProgressedPoint, (int)h.AspectAngle, h.NatalPoint))
            .Should().Equal(expected.Select(e => (e.Point, e.Aspect, e.Target)));
        for (int i = 0; i < expected.Length; i++)
        {
            hits[i].JulianDayUt.Should().BeApproximately(expected[i].Jd, SixtySecondsInDays, $"hit {i}: {expected[i]}");
            DateOnly.FromDateTime(hits[i].InstantUtc).Should().Be(
                DateOnly.ParseExact(expected[i].Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                $"hit {i}: {expected[i]}");
            JulianDayUtility.JulianDayToInstant(hits[i].JulianDayUt).Should().Be(hits[i].InstantUtc);
        }
        hits.Should().BeInAscendingOrder(h => h.JulianDayUt);
    }

    /// <summary>N1: Julian 1300-03-15 12:00 UT at Greenwich, explicit offset 0 (the pre-1583 fixture).</summary>
    private ChartResult N1() => Greenwich("N1", new DateTime(1300, 3, 15, 12, 0, 0));

    /// <summary>M35: 1960-02-05 12:00 UT at Greenwich, found by the Tier B script for its progressed Mercury loop.</summary>
    private ChartResult M35() => Greenwich("M35", new DateTime(1960, 2, 5, 12, 0, 0));

    /// <summary>A chart at Greenwich read at an explicit offset of 0.</summary>
    /// <param name="name">The chart's name.</param>
    /// <param name="localDateTime">The reading, UT.</param>
    /// <returns>The cast chart.</returns>
    private ChartResult Greenwich(string name, DateTime localDateTime) =>
        GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
        {
            Name = name,
            LocalDateTime = localDateTime,
            TimeZone = "Etc/UTC",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            Latitude = 51.4779,
            Longitude = -0.0015,
            LocationName = "Greenwich",
            EventType = "Birth",
        });

    #endregion

    #region Branch AsteroidsTiming — the natal five as targets (R7)

    /// <summary>
    /// Tier B (ast_truth_timing.py): the target overload over the natal five lists the 2026 progressed hits to them,
    /// P Moon sextile Juno and P Venus square Ceres, each on the list's UT date, in time order, and nothing else.
    /// </summary>
    [Fact]
    public void FindHits_TargetOverload_J2000Nyc2026_MatchesTierB()
    {
        List<MinorPointTierB.TimingProgressionRow> expected = MinorPointTierB.LoadTimingProgressions("solararc_2026", "P hit");
        expected.Should().HaveCount(2);
        ChartResult natal = GoldenFixtures.CreateChartEngine(_fixture).Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true));
        IReadOnlyList<(string Name, double Longitude)> targets = natal.MinorPoints.Select(p => (p.Name, p.Longitude)).ToList();

        IReadOnlyList<ProgressedHit> hits = _finder.FindHits(natal, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), targets);

        hits.Select(h => (h.ProgressedPoint, h.AspectAbbreviation, h.NatalPoint, DateOnly.FromDateTime(h.InstantUtc)))
            .Should().Equal(expected.Select(r => (r.Point, r.Aspect, r.Target, Day(r.InstantUtc))));
        hits.Should().OnlyContain(h => MinorPointTierB.Names.Contains(h.NatalPoint));
        hits.Should().BeInAscendingOrder(h => h.JulianDayUt);
    }

    /// <summary>A hit row's UT date.</summary>
    /// <param name="instant">The row's instant, never null for a hit row.</param>
    /// <returns>The date.</returns>
    private static DateOnly Day(DateTime? instant)
    {
        instant.Should().NotBeNull("a hit row carries its instant");
        return instant is DateTime value ? DateOnly.FromDateTime(value) : DateOnly.MinValue;
    }

    #endregion
}
