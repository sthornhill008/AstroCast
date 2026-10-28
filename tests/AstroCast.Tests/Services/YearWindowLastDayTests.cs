using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the last day of a year-based series (finding T0-1, branch A): a series from year A to year B
/// covers 1 January of A 00:00 UT (inclusive) to 1 January of B+1 00:00 UT (exclusive), the whole of
/// 31 December included. Until branch A each ended at 31 December of B 23:59:24 UT, but its daily
/// scan took its last sample at 31 December 00:00, so an event later on 31 December fell outside
/// both that request and the next year's.
/// </summary>
/// <remarks>
/// Every expected instant is Tier B: direct <c>swe_calc_ut</c> root-finding (a 0.05-day grid refined
/// to 1e-9 day, the timing investigation's truth library) for the crossing or the separation named.
/// The app prints these events to the second, so each is held to within one second.
/// </remarks>
[Collection("Ephemeris")]
public sealed class YearWindowLastDayTests
{
    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public YearWindowLastDayTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The lunations of 2028 include the Full Moon of 2028-12-31 16:48:31.886 UT (Tier B), which
    /// until branch A neither the 2028 request nor the 2029 request listed.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Lunations2028_IncludeTheFullMoonOf31December()
    {
        LunationSeries series = Lunations().FindLunations(2028, 2028);

        series.Lunations.Should().ContainSingle(l =>
            !l.IsNewMoon && Near(l.ExactUtc, Utc(2028, 12, 31, 16, 48, 31.886)));
    }

    /// <summary>
    /// The Moon's ingresses of 2025 include Gemini on 2025-12-31 13:13:06.389 UT (Tier B), which until
    /// branch A neither the 2025 request nor the 2026 request listed.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void MoonIngresses2025_IncludeGeminiOn31December()
    {
        IngressSeries series = Ingresses().FindIngresses(CelestialBodyId.Moon, 2025, 2025);

        series.Ingresses.Should().ContainSingle(i =>
            i.Sign == "Gemini" && Near(i.ExactUtc, Utc(2025, 12, 31, 13, 13, 6.389)));
    }

    /// <summary>
    /// The great conjunctions of 1961 to 1980 are exactly two, 1961-02-19 00:01:48.161 and
    /// 1980-12-31 21:24:09.143 UT (Tier B); until branch A the second was missing.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void GreatConjunctions1961To1980_AreExactlyTwo()
    {
        GreatConjunctionSeries series = GreatConjunctions().FindConjunctions(1961, 1980);

        series.Conjunctions.Should().HaveCount(2);
        series.Conjunctions[0].ExactUtc.Should().BeCloseTo(Utc(1961, 2, 19, 0, 1, 48.161), TimeSpan.FromSeconds(1));
        series.Conjunctions[1].ExactUtc.Should().BeCloseTo(Utc(1980, 12, 31, 21, 24, 9.143), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Mars and Saturn in the 90° harmonic from 1961 to 1970 include their opposition of 1970-12-31
    /// 20:06:22.251 UT (Tier B), missing until branch A.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void MundaneMarsSaturn1961To1970_IncludeTheOppositionOf31December()
    {
        MundaneAspectSeries series = new MundaneAspectService(_fixture.EphemerisService, _logger)
            .FindAspects(CelestialBodyId.Mars, CelestialBodyId.Saturn, "90° Harmonic", 1961, 1970);

        series.Aspects.Should().ContainSingle(a =>
            a.AspectName == "Opposition" && Near(a.ExactUtc, Utc(1970, 12, 31, 20, 6, 22.251)));
    }

    /// <summary>
    /// The Sun–Saturn synodic cycle from 1950 to 1959 ends with the conjunction of 1959-12-31
    /// 20:50:49.371 UT (Tier B), missing until branch A.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void SynodicSunSaturn1950To1959_EndsWithTheConjunctionOf31December()
    {
        SynodicCycleSeries series = new SynodicCycleService(_fixture.EphemerisService, _logger)
            .FindConjunctions(CelestialBodyId.Sun, CelestialBodyId.Saturn, 1950, 1959);

        series.Conjunctions[^1].DateTimeUtc.Should().BeCloseTo(Utc(1959, 12, 31, 20, 50, 49.371), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// A timeline of 2028 keeps Mercury's ingress into Aquarius of 2028-12-31 14:49:08.000 UT (Tier B)
    /// as well as its ingress into Capricorn of 2028-12-10 09:11:54.592. None of the timeline's
    /// default ingress bodies (Jupiter to Chiron) enters a sign on a 31 December from 1900 to 2050, so
    /// Mercury is asked for; the window is the same for every body and for the returns.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Timeline2028_KeepsMercurysIngressOf31December()
    {
        ReturnIngressTimeline timeline = new ReturnIngressTimelineService(_fixture.EphemerisService, _logger)
            .Calculate(null, 2028, 2028, includeReturns: false, includeIngresses: true,
                ingressPlanets: [CelestialBodyId.Mercury]);

        timeline.Entries.Should().ContainSingle(e =>
            e.EventType == "Ingress" && e.Sign == "Capricorn" && Near(e.ExactUtc, Utc(2028, 12, 10, 9, 11, 54.592)));
        timeline.Entries.Should().ContainSingle(e =>
            e.EventType == "Ingress" && e.Sign == "Aquarius" && Near(e.ExactUtc, Utc(2028, 12, 31, 14, 49, 8.000)));
    }

    /// <summary>
    /// The lunations of one year and of the next, requested one year at a time, are those of the two
    /// years requested together: none is lost between them or found twice. 2027 is a guard (no
    /// lunation falls on 2027-12-31); 2028 failed until branch A, losing the Full Moon of
    /// 2028-12-31 16:48 UT from the one-year requests.
    /// </summary>
    /// <param name="firstYear">The first of the two years.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(2027)]
    [InlineData(2028)]
    public void OneYearLunations_EndToEnd_EqualTheTwoYearRequest(int firstYear)
    {
        LunationService service = Lunations();

        IEnumerable<(string Type, DateTime ExactUtc)> oneAtATime = service.FindLunations(firstYear, firstYear).Lunations
            .Concat(service.FindLunations(firstYear + 1, firstYear + 1).Lunations)
            .Select(l => (l.Type, l.ExactUtc));
        IEnumerable<(string Type, DateTime ExactUtc)> together = service.FindLunations(firstYear, firstYear + 1).Lunations
            .Select(l => (l.Type, l.ExactUtc));

        oneAtATime.Should().Equal(together);
    }

    /// <summary>
    /// The Moon's ingresses of one year and of the next, requested one year at a time, are those of
    /// the two years requested together. 2027 is a guard (the Moon enters no sign on 2027-12-31);
    /// 2025 failed until branch A, losing Gemini of 2025-12-31 13:13 UT from the one-year requests.
    /// </summary>
    /// <param name="firstYear">The first of the two years.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(2027)]
    [InlineData(2025)]
    public void OneYearMoonIngresses_EndToEnd_EqualTheTwoYearRequest(int firstYear)
    {
        IngressService service = Ingresses();

        IEnumerable<(string Sign, DateTime ExactUtc)> oneAtATime = service.FindIngresses(CelestialBodyId.Moon, firstYear, firstYear).Ingresses
            .Concat(service.FindIngresses(CelestialBodyId.Moon, firstYear + 1, firstYear + 1).Ingresses)
            .Select(i => (i.Sign, i.ExactUtc));
        IEnumerable<(string Sign, DateTime ExactUtc)> together = service.FindIngresses(CelestialBodyId.Moon, firstYear, firstYear + 1).Ingresses
            .Select(i => (i.Sign, i.ExactUtc));

        oneAtATime.Should().Equal(together);
    }

    #region Helpers

    private LunationService Lunations() =>
        new(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _logger);

    private IngressService Ingresses() =>
        new(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _logger);

    private GreatConjunctionService GreatConjunctions() =>
        new(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _logger);

    private static bool Near(DateTime actual, DateTime expected) =>
        Math.Abs((actual - expected).TotalSeconds) <= 1.0;

    private static DateTime Utc(int year, int month, int day, int hour, int minute, double second) =>
        new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc).AddSeconds(second);

    #endregion
}
