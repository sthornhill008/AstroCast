using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the span clamp (branch B, the edge branch A found): every year-based series clamps its
/// window to the span the shipped ephemeris files cover, starting at the later of 1 January 00:00
/// UT of its first year and the span's start, and ending at the earlier of 1 January 00:00 UT after
/// its last year and the span's end, so no series asks for the sky outside the span. Until branch B a
/// series starting in 1200 asked at 1200-01-01 00:00 UT, before the span (Chiron and the timeline
/// failed there), and one ending in 2399 asked at 2400-01-01 00:00 UT, after it.
/// </summary>
/// <remarks>
/// The span is the files' (F0-6), from their headers and direct <c>swe_calc_ut</c> calls from a
/// freshly opened library: JD 2159357.7046912 (1200-01-01 04:54:46 UT, Julian calendar) to
/// JD 2597641.4939863 (2399-12-31 23:51:20 UT). Each series runs over an ephemeris decorator
/// that records every Julian Day it asks for. The library is stateful, so these tests also run
/// alone in a fresh process.
/// </remarks>
[Collection("Ephemeris")]
public sealed class SeriesSpanClampTests
{
    /// <summary>The first instant of the files' span (F0-6), Julian Day UT.</summary>
    private const double SpanFirst = 2159357.7046912;

    /// <summary>The last instant of the files' span (F0-6), Julian Day UT.</summary>
    private const double SpanLast = 2597641.4939863;

    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public SeriesSpanClampTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>The series and the years each is asked for at the span's two edges.</summary>
    public static TheoryData<string, int, int> EdgeRequests => new()
    {
        { "lunations", 1200, 1200 },
        { "lunations", 2399, 2399 },
        { "Chiron ingresses", 1200, 1200 },
        { "Chiron ingresses", 2399, 2399 },
        { "return and ingress timeline", 1200, 1200 },
        { "return and ingress timeline", 2399, 2399 },
        { "synodic Jupiter-Chiron", 1200, 1210 },
        { "synodic Jupiter-Chiron", 2390, 2399 },
        { "mundane Saturn-Chiron", 1200, 1210 },
        { "mundane Saturn-Chiron", 2390, 2399 },
        { "great conjunctions", 1200, 1220 },
        { "great conjunctions", 2380, 2399 },
        { "eclipses", 1200, 1200 },
        { "eclipses", 2399, 2399 },
    };

    /// <summary>
    /// A series asked for a first year of 1200 or a last year of 2399 completes and asks for the
    /// sky only inside the span.
    /// </summary>
    /// <param name="series">The series asked for.</param>
    /// <param name="startYear">The first year.</param>
    /// <param name="endYear">The last year.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [MemberData(nameof(EdgeRequests))]
    public void SeriesAtTheSpanEdges_AsksForTheSkyOnlyInsideTheSpan(string series, int startYear, int endYear)
    {
        RecordingEphemeris recorder = new(_fixture.EphemerisService);

        Run(series, recorder, startYear, endYear);

        recorder.Count.Should().BeGreaterThan(0);
        recorder.Earliest.Should().BeGreaterThanOrEqualTo(SpanFirst, "no series asks for the sky before the span");
        recorder.Latest.Should().BeLessThanOrEqualTo(SpanLast, "no series asks for the sky after the span");
    }

    /// <summary>
    /// A series inside the span keeps its window: lunations of 2026 ask for the sky from
    /// 1 January 2026 00:00 UT, as before.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void SeriesInsideTheSpan_KeepsItsWindow()
    {
        RecordingEphemeris recorder = new(_fixture.EphemerisService);

        Run("lunations", recorder, 2026, 2026);

        recorder.Earliest.Should().Be(2461041.5, "1 January 2026 00:00 UT is JD 2461041.5");
        recorder.Latest.Should().BeLessThanOrEqualTo(2461406.5, "1 January 2027 00:00 UT is JD 2461406.5");
    }

    /// <summary>
    /// A series whose first year is 1200 starts at the span's first instant, 04:54:46 UT, so its
    /// daily scan runs at 04:54:46; it still scans to the end of its last year. The Moon's ingresses
    /// of 1200 number 161 (Tier B), the last into Scorpio at JD 2159723.025613 (31 December 1200
    /// 12:36:53 UT on the Julian calendar, which <c>JulianDayUtility.ToDateTimeUtc</c> labels
    /// 1201-01-07 on the proleptic Gregorian calendar). With the clamp alone the scan stopped at
    /// 31 December 04:54:46 and lost it (Steve's ruling: scan on and filter).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void MoonIngresses1200_ScanToTheEndOf31December()
    {
        IngressSeries series = new IngressService(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _logger)
            .FindIngresses(CelestialBodyId.Moon, 1200, 1200);

        series.Ingresses.Should().HaveCount(161);
        IngressResult last = series.Ingresses[^1];
        last.Sign.Should().Be("Scorpio");
        last.ExactUtc.Should().BeCloseTo(
            new DateTime(1201, 1, 7, 12, 36, 53, DateTimeKind.Utc).AddMilliseconds(2), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// The Moon's ingresses of 1200 and of 1201, asked for one year at a time, are those of 1200–1201
    /// asked for together: 161 and 160 (Tier B), none lost between them or found twice. The two-year
    /// request steps from 04:54:46 UT through 1201 where the one-year request steps from 00:00, so the
    /// same crossing is refined from a different bracket: the two refinements agree to the finder's
    /// bisection tolerance (under 0.1 s), and each is rounded to the second, so they can print a
    /// second apart. Each pair is held to within one second.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void MoonIngresses1200And1201_OneYearAtATime_EqualTheTwoYearRequest()
    {
        IngressService service = new(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _logger);

        List<(string Sign, DateTime ExactUtc)> oneAtATime = service.FindIngresses(CelestialBodyId.Moon, 1200, 1200).Ingresses
            .Concat(service.FindIngresses(CelestialBodyId.Moon, 1201, 1201).Ingresses)
            .Select(i => (i.Sign, i.ExactUtc)).ToList();
        List<(string Sign, DateTime ExactUtc)> together = service.FindIngresses(CelestialBodyId.Moon, 1200, 1201).Ingresses
            .Select(i => (i.Sign, i.ExactUtc)).ToList();

        oneAtATime.Should().HaveCount(321);
        together.Should().HaveCount(321);
        oneAtATime.Select(i => i.Sign).Should().Equal(together.Select(i => i.Sign));
        oneAtATime.Zip(together).Should().OnlyContain(p =>
            Math.Abs((p.First.ExactUtc - p.Second.ExactUtc).TotalSeconds) <= 1.0);
    }

    private void Run(string series, RecordingEphemeris ephemeris, int startYear, int endYear)
    {
        switch (series)
        {
            case "lunations":
                new LunationService(ephemeris, GoldenFixtures.CreateChartEngine(_fixture), _logger).FindLunations(startYear, endYear);
                break;
            case "Chiron ingresses":
                new IngressService(ephemeris, GoldenFixtures.CreateChartEngine(_fixture), _logger)
                    .FindIngresses(CelestialBodyId.Chiron, startYear, endYear);
                break;
            case "return and ingress timeline":
                ChartResult natal = GoldenFixtures.J2000Nyc(_fixture);
                new ReturnIngressTimelineService(ephemeris, _logger).Calculate(natal, startYear, endYear);
                break;
            case "synodic Jupiter-Chiron":
                new SynodicCycleService(ephemeris, _logger)
                    .FindConjunctions(CelestialBodyId.Jupiter, CelestialBodyId.Chiron, startYear, endYear);
                break;
            case "mundane Saturn-Chiron":
                new MundaneAspectService(ephemeris, _logger)
                    .FindAspects(CelestialBodyId.Saturn, CelestialBodyId.Chiron, "90° Harmonic", startYear, endYear);
                break;
            case "great conjunctions":
                new GreatConjunctionService(ephemeris, GoldenFixtures.CreateChartEngine(_fixture), _logger)
                    .FindConjunctions(startYear, endYear);
                break;
            case "eclipses":
                new EclipseService(ephemeris, _logger).FindEclipses(startYear, endYear);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(series), series, "Unknown series.");
        }
    }
}
