using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.SolarArcs;
using AstroCast.Core.Services;
using AstroCast.Core.Utilities;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;
using AstroCast.Tests.Services.MinorPoints;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="SolarArcHitFinder"/> against Tier B: an independent pyswisseph script
/// (arcs_truth.py, branch Arcs) over the shipped ephemeris files, with its own daily scan and 1 s
/// bisection, whose natal casts agree with the engine's to the bit. A hit matches when it names the same
/// directed point, aspect angle and target, falls on the same UT date and lies within 60 s of the truth.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class SolarArcHitFinderTests
{
    /// <summary>Tolerance on every Tier B instant, in days.</summary>
    private const double SixtySecondsInDays = 60.0 / 86400.0;

    /// <summary>A hit's residual at its instant: the directed point's distance from its aspect point.</summary>
    private const double ExactnessDegrees = 1e-6;

    private readonly EphemerisFixture _fixture;
    private readonly SolarArcHitFinder _finder;
    private readonly ChartResult _j2000;

    /// <summary>Casts J2000 NYC and builds the finder over the shared ephemeris.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public SolarArcHitFinderTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _finder = new SolarArcHitFinder(fixture.EphemerisService);
        _j2000 = GoldenFixtures.J2000Nyc(fixture);
    }

    #region Tier B expectations

    /// <summary>
    /// Tier B, J2000 NYC, 2026-01-01 to 2027-12-31 (J2000.hits.txt): 15 hits to natal points and 45 to
    /// natal midpoints, in time order. (point, aspect angle or dial harmonic, target, JD UT, UT date).
    /// </summary>
    private static readonly (string Point, int Aspect, string Target, double Jd, string Date)[] J2000Expected =
    [
        ("Neptune", 0, "Jupiter/ASC", 2461068.847759, "2026-01-28"),
        ("Chiron", 0, "Venus/Uranus", 2461074.360973, "2026-02-02"),
        ("Neptune", 45, "Venus/Mars", 2461074.961033, "2026-02-03"),
        ("Mercury", 90, "MC", 2461077.907772, "2026-02-06"),
        ("ASC", 45, "Neptune/MC", 2461081.248196, "2026-02-09"),
        ("Uranus", 90, "Pluto", 2461101.577068, "2026-03-02"),
        ("Venus", 0, "Mars/MC", 2461103.294048, "2026-03-03"),
        ("Uranus", 90, "Pluto/Chiron", 2461130.840923, "2026-03-31"),
        ("Pluto", 0, "Venus/Uranus", 2461132.888363, "2026-04-02"),
        ("Venus", 45, "Moon", 2461141.964298, "2026-04-11"),
        ("Saturn", 135, "Sun/True Node", 2461144.783848, "2026-04-14"),
        ("Sun", 135, "Saturn/True Node", 2461159.202358, "2026-04-28"),
        ("Uranus", 90, "Chiron", 2461160.105137, "2026-04-29"),
        ("Venus", 45, "Venus/Jupiter", 2461172.761089, "2026-05-12"),
        ("ASC", 0, "Mars/ASC", 2461178.874638, "2026-05-18"),
        ("MC", 45, "Sun", 2461192.981472, "2026-06-01"),
        ("Venus", 60, "MC", 2461194.172031, "2026-06-02"),
        ("Saturn", 90, "Saturn/ASC", 2461201.338322, "2026-06-09"),
        ("Moon", 135, "Jupiter", 2461203.558285, "2026-06-12"),
        ("ASC", 135, "True Node/MC", 2461218.005726, "2026-06-26"),
        ("Neptune", 90, "Mercury/MC", 2461223.971516, "2026-07-02"),
        ("Uranus", 90, "Sun/Moon", 2461242.308041, "2026-07-20"),
        ("Uranus", 45, "Jupiter/MC", 2461247.596554, "2026-07-26"),
        ("Jupiter", 135, "Sun/ASC", 2461247.776707, "2026-07-26"),
        ("Moon", 90, "Sun/Saturn", 2461249.996716, "2026-07-28"),
        ("Jupiter", 135, "Neptune/Pluto", 2461254.405720, "2026-08-01"),
        ("Jupiter", 135, "Neptune/Chiron", 2461283.671490, "2026-08-31"),
        ("Jupiter", 180, "Moon/Venus", 2461297.851833, "2026-09-14"),
        ("Saturn", 180, "Moon/Mercury", 2461304.741688, "2026-09-21"),
        ("ASC", 60, "Venus", 2461345.341579, "2026-10-31"),
        ("Mercury", 45, "Jupiter/Neptune", 2461348.924465, "2026-11-04"),
        ("True Node", 90, "ASC/MC", 2461373.258739, "2026-11-28"),
        ("Mars", 45, "Saturn", 2461384.361324, "2026-12-09"),
        ("Jupiter", 135, "Pluto/True Node", 2461391.173489, "2026-12-16"),
        ("MC", 90, "Saturn/Pluto", 2461392.934010, "2026-12-18"),
        ("Sun", 45, "Pluto/ASC", 2461401.291584, "2026-12-26"),
        ("ASC", 45, "Mercury/Venus", 2461403.479717, "2026-12-28"),
        ("Venus", 0, "Moon/Uranus", 2461408.918705, "2027-01-03"),
        ("Jupiter", 135, "True Node/Chiron", 2461420.441013, "2027-01-14"),
        ("MC", 90, "Saturn/Chiron", 2461422.201557, "2027-01-16"),
        ("Sun", 45, "Chiron/ASC", 2461430.559246, "2027-01-25"),
        ("True Node", 90, "Venus", 2461448.848209, "2027-02-12"),
        ("Mercury", 135, "Jupiter/True Node", 2461485.697941, "2027-03-21"),
        ("Uranus", 45, "Sun/Uranus", 2461509.274204, "2027-04-13"),
        ("Sun", 45, "Moon/Neptune", 2461548.670933, "2027-05-23"),
        ("Jupiter", 135, "Venus/Uranus", 2461564.824589, "2027-06-08"),
        ("True Node", 120, "Mercury", 2461565.131382, "2027-06-08"),
        ("Mars", 90, "Sun/Pluto", 2461569.909611, "2027-06-13"),
        ("Saturn", 135, "Mercury/Uranus", 2461571.715267, "2027-06-15"),
        ("Mars", 90, "Sun/Chiron", 2461599.179508, "2027-07-12"),
        ("Mercury", 0, "Mercury/Mars", 2461601.711765, "2027-07-15"),
        ("Moon", 0, "Pluto", 2461635.508053, "2027-08-18"),
        ("Neptune", 90, "ASC/MC", 2461646.817150, "2027-08-29"),
        ("Moon", 0, "Pluto/Chiron", 2461664.778835, "2027-09-16"),
        ("Venus", 45, "Uranus", 2461675.904858, "2027-09-27"),
        ("Sun", 135, "Moon/True Node", 2461685.456814, "2027-10-06"),
        ("Moon", 0, "Chiron", 2461694.050022, "2027-10-15"),
        ("Neptune", 90, "Venus", 2461722.416065, "2027-11-12"),
        ("Saturn", 180, "Moon/ASC", 2461727.602726, "2027-11-18"),
        ("Venus", 45, "Venus/MC", 2461750.757267, "2027-12-11"),
    ];

    /// <summary>
    /// Tier B, N1 (Julian 1300-03-15 12:00 UT, Greenwich), proleptic 1700-01-01 to 1701-12-31
    /// (N1-1700.hits.txt): 8 hits to natal points and 62 to midpoints, the arc about 394°.
    /// </summary>
    private static readonly (string Point, int Aspect, string Target, double Jd, string Date)[] N1In1700Expected =
    [
        ("True Node", 45, "ASC/MC", 2341986.534626, "1700-01-15"),
        ("Neptune", 120, "MC", 2341996.259281, "1700-01-24"),
        ("Venus", 45, "Jupiter/Pluto", 2341997.127010, "1700-01-25"),
        ("Saturn", 120, "Moon", 2342007.205265, "1700-02-04"),
        ("Sun", 45, "Sun/True Node", 2342010.747318, "1700-02-08"),
        ("Jupiter", 45, "Sun/Jupiter", 2342023.579746, "1700-02-21"),
        ("Pluto", 135, "Saturn", 2342031.326832, "1700-02-28"),
        ("Chiron", 45, "Mercury/Chiron", 2342047.035572, "1700-03-16"),
        ("Jupiter", 135, "Neptune/MC", 2342053.254307, "1700-03-22"),
        ("Chiron", 135, "Moon/Pluto", 2342065.355434, "1700-04-03"),
        ("Jupiter", 0, "ASC/MC", 2342068.703648, "1700-04-07"),
        ("Uranus", 90, "Moon/True Node", 2342073.664158, "1700-04-12"),
        ("Moon", 45, "Mercury/Mars", 2342098.837612, "1700-05-07"),
        ("Mercury", 0, "Saturn/True Node", 2342115.146244, "1700-05-23"),
        ("True Node", 90, "Mercury/Uranus", 2342117.250309, "1700-05-25"),
        ("Venus", 45, "Sun/Mars", 2342144.227207, "1700-06-21"),
        ("Moon", 45, "Venus/MC", 2342151.548939, "1700-06-29"),
        ("Chiron", 0, "Jupiter/Saturn", 2342174.900372, "1700-07-22"),
        ("MC", 90, "Neptune/Chiron", 2342185.040226, "1700-08-01"),
        ("ASC", 45, "Jupiter/Uranus", 2342199.175961, "1700-08-15"),
        ("Jupiter", 45, "Mercury/Uranus", 2342199.435207, "1700-08-15"),
        ("MC", 45, "Chiron/ASC", 2342200.492565, "1700-08-16"),
        ("Saturn", 180, "Mars", 2342202.202534, "1700-08-18"),
        ("True Node", 0, "Venus/Jupiter", 2342211.124691, "1700-08-27"),
        ("Moon", 45, "Jupiter/True Node", 2342222.087833, "1700-09-07"),
        ("Neptune", 135, "Jupiter/Uranus", 2342230.081631, "1700-09-15"),
        ("Moon", 45, "Sun", 2342234.924244, "1700-09-20"),
        ("Sun", 45, "Venus/True Node", 2342280.483318, "1700-11-04"),
        ("Jupiter", 45, "Venus/Jupiter", 2342293.320820, "1700-11-17"),
        ("True Node", 90, "Sun/Neptune", 2342324.193523, "1700-12-18"),
        ("Sun", 45, "Mars/MC", 2342330.618763, "1700-12-25"),
        ("True Node", 45, "Sun/ASC", 2342339.648975, "1701-01-03"),
        ("MC", 45, "True Node/MC", 2342363.874165, "1701-01-27"),
        ("Saturn", 90, "Uranus/Pluto", 2342367.066059, "1701-01-30"),
        ("Mercury", 180, "Moon/Uranus", 2342369.677959, "1701-02-02"),
        ("Venus", 135, "Neptune/Pluto", 2342379.935505, "1701-02-12"),
        ("Venus", 0, "Pluto/ASC", 2342395.392178, "1701-02-27"),
        ("Jupiter", 135, "Sun/Neptune", 2342406.402981, "1701-03-10"),
        ("Venus", 45, "Venus/Mars", 2342414.015568, "1701-03-18"),
        ("Jupiter", 0, "Sun/ASC", 2342421.860233, "1701-03-26"),
        ("ASC", 180, "Moon/Mercury", 2342438.026524, "1701-04-11"),
        ("Venus", 135, "Uranus/True Node", 2342443.403248, "1701-04-16"),
        ("MC", 45, "Mercury/Pluto", 2342446.646755, "1701-04-20"),
        ("Mercury", 45, "Mercury/MC", 2342466.750813, "1701-05-10"),
        ("Neptune", 90, "Moon/Mercury", 2342468.942738, "1701-05-12"),
        ("Moon", 45, "Sun/Venus", 2342504.747623, "1701-06-17"),
        ("ASC", 180, "Mars/Pluto", 2342508.189327, "1701-06-20"),
        ("Pluto", 90, "Mars/Uranus", 2342521.997478, "1701-07-04"),
        ("Sun", 45, "Jupiter/Pluto", 2342536.693211, "1701-07-19"),
        ("Neptune", 90, "Mars/Pluto", 2342539.108570, "1701-07-21"),
        ("True Node", 45, "Pluto/True Node", 2342549.535351, "1701-08-01"),
        ("Chiron", 90, "Saturn/Neptune", 2342557.806919, "1701-08-09"),
        ("Uranus", 180, "Chiron", 2342557.849850, "1701-08-09"),
        ("Chiron", 45, "Saturn/ASC", 2342573.267437, "1701-08-24"),
        ("ASC", 45, "Uranus/Neptune", 2342582.095707, "1701-09-02"),
        ("True Node", 90, "Venus/Neptune", 2342594.050922, "1701-09-14"),
        ("ASC", 0, "Uranus/ASC", 2342597.556744, "1701-09-18"),
        ("Mars", 45, "Moon/Jupiter", 2342602.685581, "1701-09-23"),
        ("Moon", 45, "Neptune/True Node", 2342605.019993, "1701-09-25"),
        ("True Node", 45, "Venus/ASC", 2342609.512211, "1701-09-30"),
        ("Neptune", 45, "Uranus/Neptune", 2342613.018108, "1701-10-03"),
        ("Moon", 90, "True Node/ASC", 2342620.481518, "1701-10-10"),
        ("Venus", 90, "Saturn", 2342625.891972, "1701-10-16"),
        ("Neptune", 90, "Uranus/ASC", 2342628.479801, "1701-10-18"),
        ("Jupiter", 90, "Pluto/True Node", 2342631.770725, "1701-10-22"),
        ("Uranus", 60, "Moon", 2342649.303188, "1701-11-08"),
        ("Jupiter", 135, "Venus/Neptune", 2342676.291309, "1701-12-05"),
        ("Sun", 45, "Sun/Mars", 2342683.906742, "1701-12-13"),
        ("Jupiter", 0, "Venus/ASC", 2342691.754337, "1701-12-21"),
        ("Pluto", 90, "Saturn/Chiron", 2342699.878574, "1701-12-29"),
    ];

    #endregion

    #region Tier B

    /// <summary>
    /// Every Tier B hit of J2000 NYC in 2026 and 2027 is listed, none extra, each on the same UT date
    /// and within 60 s, in time order.
    /// </summary>
    [Fact]
    public void FindHits_J2000Nyc_2026To2027_MatchesTierB()
    {
        IReadOnlyList<SolarArcHit> hits = _finder.FindHits(_j2000, new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31));

        AssertMatchesTierB(hits, J2000Expected);
    }

    /// <summary>
    /// N1, four centuries old in 1700, has a solar arc past 360°: every Tier B hit is listed and each
    /// hit's arc is the unwrapped one, between 360° and 720°.
    /// </summary>
    [Fact]
    public void FindHits_ArcBeyond360_IsUnwrapped()
    {
        IReadOnlyList<SolarArcHit> hits = _finder.FindHits(N1(), new DateOnly(1700, 1, 1), new DateOnly(1701, 12, 31));

        AssertMatchesTierB(hits, N1In1700Expected);
        hits.Should().OnlyContain(h => h.Arc > 360.0 && h.Arc < 720.0);
        hits[0].Arc.Should().BeApproximately(393.686170, 0.1, "Tier B's arc at the window's start, two weeks before the first hit");
    }

    #endregion

    #region Exactness and the dial

    /// <summary>At its instant every hit's directed point is within a millionth of a degree of its aspect point.</summary>
    [Fact]
    public void FindHits_EachHit_IsExact()
    {
        IReadOnlyList<SolarArcHit> hits = _finder.FindHits(_j2000, new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31));

        hits.Should().NotBeEmpty();
        foreach (SolarArcHit hit in hits)
        {
            double directed = DirectedLongitudeAt(_j2000, hit);
            double residual = hit.IsMidpoint
                ? TransitMidpointScanner.Dial45Separation(
                    TransitMidpointScanner.ReduceToDial45(directed),
                    TransitMidpointScanner.ReduceToDial45(MidpointLongitude(_j2000, hit)))
                : Math.Abs(WrapSigned(directed - NatalLongitude(_j2000, hit.Target) - SignedAngle(_j2000, hit)));
            residual.Should().BeLessThan(ExactnessDegrees, $"{hit.DirectedPoint} {hit.AspectAngle} {hit.Target} at {hit.InstantUtc:O}");
            JulianDayUtility.JulianDayToInstant(hit.JulianDayUt).Should().Be(hit.InstantUtc);
        }
    }

    /// <summary>The 45° series is in: a semisquare and a sesquiquadrate to natal points, named and abbreviated.</summary>
    [Fact]
    public void FindHits_IncludesSemisquareAndSesquiquadrate()
    {
        IReadOnlyList<SolarArcHit> hits = _finder.FindHits(_j2000, new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31));

        SolarArcHit semisquare = hits.Single(h => h.DirectedPoint == "Venus" && h.Target == "Moon" && !h.IsMidpoint);
        (semisquare.AspectAngle, semisquare.AspectName, semisquare.AspectAbbreviation).Should().Be((45.0, "Semisquare", "ssq"));
        DateOnly.FromDateTime(semisquare.InstantUtc).Should().Be(new DateOnly(2026, 4, 11));

        SolarArcHit sesquiquadrate = hits.Single(h => h.DirectedPoint == "Moon" && h.Target == "Jupiter" && !h.IsMidpoint);
        (sesquiquadrate.AspectAngle, sesquiquadrate.AspectName, sesquiquadrate.AspectAbbreviation).Should().Be((135.0, "Sesquiquadrate", "ses"));
        DateOnly.FromDateTime(sesquiquadrate.InstantUtc).Should().Be(new DateOnly(2026, 6, 12));
    }

    /// <summary>
    /// A midpoint hit lands the directed point on the midpoint's 45° dial position, names both bodies,
    /// carries the harmonic it stands at, and the midpoints holding the directed point are among the
    /// targets, as the transit activations take them.
    /// </summary>
    [Fact]
    public void FindHits_Midpoint_IsOnThe45Dial()
    {
        IReadOnlyList<SolarArcHit> hits = _finder.FindHits(_j2000, new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31));
        List<SolarArcHit> midpointHits = hits.Where(h => h.IsMidpoint).ToList();

        midpointHits.Should().HaveCount(45);
        foreach (SolarArcHit hit in midpointHits)
        {
            hit.Target.Should().Be($"{hit.MidpointBody1}/{hit.MidpointBody2}");
            double directed = DirectedLongitudeAt(_j2000, hit);
            double midpoint = MidpointLongitude(_j2000, hit);
            TransitMidpointScanner.Dial45Separation(
                TransitMidpointScanner.ReduceToDial45(directed), TransitMidpointScanner.ReduceToDial45(midpoint))
                .Should().BeLessThan(ExactnessDegrees);
            double separation = Math.Abs(WrapSigned(directed - midpoint));
            hit.AspectAngle.Should().Be(Math.Round(separation / 45.0) * 45.0);
            hit.AspectAngle.Should().BeOneOf(0.0, 45.0, 90.0, 135.0, 180.0);
        }

        SolarArcHit sunMoon = midpointHits.Single(h => h.Target == "Sun/Moon");
        (sunMoon.DirectedPoint, sunMoon.AspectAngle, sunMoon.AspectName).Should().Be(("Uranus", 90.0, "Square"));
        midpointHits.Should().Contain(h => h.DirectedPoint == "Venus" && h.Target == "Venus/Jupiter" && h.AspectAngle == 45.0);
    }

    #endregion

    #region Rules

    /// <summary>A directed point never hits its own natal place: every point would, at every arc that is an aspect.</summary>
    [Fact]
    public void FindHits_SkipsSelf()
    {
        IReadOnlyList<SolarArcHit> hits = _finder.FindHits(_j2000, new DateOnly(2026, 1, 1), new DateOnly(2027, 12, 31));

        hits.Should().NotBeEmpty();
        hits.Should().OnlyContain(h => h.IsMidpoint || h.DirectedPoint != h.Target);
    }

    /// <summary>
    /// The window is by UT date: SA Sun semisquare Pluto/ASC is exact 2026-12-26 18:59:53 UT, so a period
    /// ending on the 26th lists it, one beginning on the 27th does not, and the 26th alone lists only it.
    /// </summary>
    [Fact]
    public void FindHits_Window_IsByUtDate()
    {
        IReadOnlyList<SolarArcHit> toTheDay = _finder.FindHits(_j2000, new DateOnly(2026, 12, 1), new DateOnly(2026, 12, 26));
        IReadOnlyList<SolarArcHit> fromTheNext = _finder.FindHits(_j2000, new DateOnly(2026, 12, 27), new DateOnly(2027, 1, 31));
        IReadOnlyList<SolarArcHit> theDay = _finder.FindHits(_j2000, new DateOnly(2026, 12, 26), new DateOnly(2026, 12, 26));

        toTheDay.Should().ContainSingle(h => h.DirectedPoint == "Sun" && h.Target == "Pluto/ASC");
        fromTheNext.Should().NotContain(h => h.DirectedPoint == "Sun" && h.Target == "Pluto/ASC");
        SolarArcHit only = theDay.Should().ContainSingle().Subject;
        (only.DirectedPoint, only.AspectAngle, only.Target).Should().Be(("Sun", 45.0, "Pluto/ASC"));
        only.InstantUtc.Should().BeCloseTo(new DateTime(2026, 12, 26, 18, 59, 53, DateTimeKind.Utc), TimeSpan.FromSeconds(60));
    }

    /// <summary>A day with no hit gives an empty list.</summary>
    [Fact]
    public void FindHits_OneDayWithoutTarget_IsEmpty()
    {
        _finder.FindHits(_j2000, new DateOnly(2026, 1, 2), new DateOnly(2026, 1, 2)).Should().BeEmpty();
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Asserts the hits are the Tier B list: the same count, and in order the same point, aspect and
    /// target, the same UT date, and an instant within 60 s.
    /// </summary>
    /// <param name="hits">The finder's hits.</param>
    /// <param name="expected">The Tier B hits, in time order.</param>
    private static void AssertMatchesTierB(
        IReadOnlyList<SolarArcHit> hits, (string Point, int Aspect, string Target, double Jd, string Date)[] expected)
    {
        hits.Select(h => (h.DirectedPoint, (int)h.AspectAngle, h.Target))
            .Should().Equal(expected.Select(e => (e.Point, e.Aspect, e.Target)));
        for (int i = 0; i < expected.Length; i++)
        {
            hits[i].JulianDayUt.Should().BeApproximately(expected[i].Jd, SixtySecondsInDays, $"hit {i}: {expected[i]}");
            DateOnly.FromDateTime(hits[i].InstantUtc).Should().Be(
                DateOnly.ParseExact(expected[i].Date, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                $"hit {i}: {expected[i]}");
        }
        hits.Should().BeInAscendingOrder(h => h.JulianDayUt);
    }

    /// <summary>N1: Julian 1300-03-15 12:00 UT at Greenwich, explicit offset 0 (the pre-1583 fixture).</summary>
    private ChartResult N1() => GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
    {
        Name = "N1",
        LocalDateTime = new DateTime(1300, 3, 15, 12, 0, 0),
        TimeZone = "Etc/UTC",
        UseExplicitOffset = true,
        UtcOffsetHours = 0.0,
        Latitude = 51.4779,
        Longitude = -0.0015,
        LocationName = "Greenwich",
        EventType = "Birth",
    });

    /// <summary>
    /// The directed point's longitude at the hit's instant, computed here from the progressed Sun by
    /// the day-for-a-year key, independently of the finder's arithmetic.
    /// </summary>
    private double DirectedLongitudeAt(ChartResult natal, SolarArcHit hit)
    {
        double progressedJd = natal.JulianDay + ProgressedDateCalculator.RealDaysToProgressedDays(hit.JulianDayUt - natal.JulianDay);
        double progressedSun = _fixture.EphemerisService.CalculatePositionLongitudeOnly(progressedJd, CelestialBodyId.Sun).Longitude;
        double arc = Normalize(progressedSun - NatalLongitude(natal, "Sun"));
        return Normalize(NatalLongitude(natal, hit.DirectedPoint) + arc);
    }

    /// <summary>A natal point's longitude by name: a body, the Ascendant or the Midheaven.</summary>
    private static double NatalLongitude(ChartResult natal, string name) => name switch
    {
        "ASC" => natal.Houses.Ascendant,
        "MC" => natal.Houses.Midheaven,
        _ => natal.Positions.Single(p => p.Name == name).Longitude,
    };

    /// <summary>The natal midpoint a midpoint hit targets.</summary>
    private static double MidpointLongitude(ChartResult natal, SolarArcHit hit) =>
        natal.Midpoints.Single(m => m.Body1Name == hit.MidpointBody1 && m.Body2Name == hit.MidpointBody2).Longitude;

    /// <summary>
    /// The signed aspect angle a point hit was found at: the aspect's angle with the sign that puts the
    /// directed point at the natal target plus it.
    /// </summary>
    private double SignedAngle(ChartResult natal, SolarArcHit hit)
    {
        double directed = DirectedLongitudeAt(natal, hit);
        double separation = WrapSigned(directed - NatalLongitude(natal, hit.Target));
        return separation < 0 ? -hit.AspectAngle : hit.AspectAngle;
    }

    /// <summary>A longitude brought into [0°, 360°).</summary>
    private static double Normalize(double longitude) => ((longitude % 360.0) + 360.0) % 360.0;

    /// <summary>An angle wrapped to [−180°, 180°).</summary>
    private static double WrapSigned(double angle) => Normalize(angle + 180.0) - 180.0;

    #endregion

    #region Branch AsteroidsTiming — the natal five as targets (R7)

    /// <summary>
    /// Tier B (ast_truth_timing.py): the target overload over the natal five lists the 2026 solar arc hits to them,
    /// SA Pluto conjunct Juno and SA Venus sesquiquadrate Pallas, each on the list's UT date, in time order, with no
    /// midpoint contact and nothing else.
    /// </summary>
    [Fact]
    public void FindHits_TargetOverload_J2000Nyc2026_MatchesTierBWithNoMidpointHit()
    {
        List<MinorPointTierB.TimingProgressionRow> expected = MinorPointTierB.LoadTimingProgressions("solararc_2026", "SA hit");
        expected.Should().HaveCount(2);
        ChartResult natal = GoldenFixtures.CreateChartEngine(_fixture).Calculate(GoldenFixtures.J2000NycInput(includeMinorPoints: true));
        IReadOnlyList<(string Name, double Longitude)> targets = natal.MinorPoints.Select(p => (p.Name, p.Longitude)).ToList();

        IReadOnlyList<SolarArcHit> hits = _finder.FindHits(natal, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), targets);

        hits.Select(h => (h.DirectedPoint, h.AspectAbbreviation, h.Target, DateOnly.FromDateTime(h.InstantUtc)))
            .Should().Equal(expected.Select(r => (r.Point, r.Aspect, r.Target, Day(r.InstantUtc))));
        hits.Should().NotContain(h => h.IsMidpoint);
        hits.Should().OnlyContain(h => MinorPointTierB.Names.Contains(h.Target));
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
