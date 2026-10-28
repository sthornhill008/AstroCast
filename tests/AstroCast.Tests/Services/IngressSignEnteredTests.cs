using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the sign a crossing enters (findings T4-2 and T2-6, branch B): a sign boundary crossed while
/// the body moves forward enters the sign that begins there; crossed while it is retrograde
/// (negative speed in longitude), it enters the sign that ends there, so a backward crossing of
/// 0° Aries enters Pisces. Until branch B every crossing was labelled with the sign that begins at
/// the boundary, which on a retrograde crossing is the sign being left.
/// </summary>
/// <remarks>
/// Every expected instant, speed and sign is Tier B: the crossings found by direct
/// <c>swe_calc_ut</c> root-finding (a 0.05-day grid refined to 1e-9 day, the timing investigation's
/// truth library), the speed from <c>swe_calc_ut</c> with the speed flag at the crossing, and the
/// sign the body's longitude lies in 1e-4 day after it. Each is held to within one second.
/// </remarks>
[Collection("Ephemeris")]
public sealed class IngressSignEnteredTests
{
    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public IngressSignEnteredTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Pluto crosses 300° backwards on 2024-09-01 23:57:26.788 UT at −0.01684°/day (Tier B): it
    /// enters Capricorn. Until branch B the list said Aquarius.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Pluto2024_RetrogradeCrossingOf300_EntersCapricorn()
    {
        IngressSeries series = Ingresses().FindIngresses(CelestialBodyId.Pluto, 2024, 2024);

        IngressResult crossing = series.Ingresses.Should().ContainSingle(i =>
            Near(i.ExactUtc, Utc(2024, 9, 1, 23, 57, 26.788))).Subject;
        crossing.Sign.Should().Be("Capricorn");
        crossing.Element.Should().Be("Earth");
        crossing.Modality.Should().Be("Cardinal");
        crossing.TargetLongitude.Should().Be(300.0);
    }

    /// <summary>
    /// The True Node crosses 0° backwards on 2025-01-11 23:01:33.811 UT at −0.1835°/day (Tier B): it
    /// enters Pisces, the sign that ends at the boundary. Until branch B the list said Aries.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void TrueNode2025_RetrogradeCrossingOf0_EntersPisces()
    {
        IngressSeries series = Ingresses().FindIngresses(CelestialBodyId.TrueNode, 2025, 2025);

        IngressResult crossing = series.Ingresses.Should().ContainSingle(i =>
            Near(i.ExactUtc, Utc(2025, 1, 11, 23, 1, 33.811))).Subject;
        crossing.Sign.Should().Be("Pisces");
        crossing.TargetLongitude.Should().Be(0.0);
    }

    /// <summary>
    /// The return-and-ingress timeline's Chiron entries for 2026–2027 read Taurus (2026-06-19
    /// 21:20:23, +0.037°/day), Aries (2026-09-18 01:50:41, −0.036°/day) and Taurus (2027-04-14
    /// 14:58:07, +0.061°/day), all Tier B. Until branch B the middle one, Chiron's return to Aries,
    /// read Taurus.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Timeline2026To2027_ChironEntries_ReadTaurusAriesTaurus()
    {
        ReturnIngressTimeline timeline = new ReturnIngressTimelineService(_fixture.EphemerisService, _logger)
            .Calculate(null, 2026, 2027, includeReturns: false, includeIngresses: true,
                ingressPlanets: [CelestialBodyId.Chiron]);

        timeline.Entries.Select(e => e.Sign).Should().Equal("Taurus", "Aries", "Taurus");
        timeline.Entries[0].ExactUtc.Should().BeCloseTo(Utc(2026, 6, 19, 21, 20, 22.742), TimeSpan.FromSeconds(1));
        timeline.Entries[1].ExactUtc.Should().BeCloseTo(Utc(2026, 9, 18, 1, 50, 41.139), TimeSpan.FromSeconds(1));
        timeline.Entries[2].ExactUtc.Should().BeCloseTo(Utc(2027, 4, 14, 14, 58, 6.743), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Guard: a forward crossing still enters the sign that begins at the boundary. Pluto's
    /// crossings of 300° on 2024-01-21 00:56:10.117 (+0.0325°/day) and 2024-11-19 20:39:41.275
    /// (+0.0181°/day) UT (Tier B) enter Aquarius, before branch B and after.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Pluto2024_ForwardCrossingsOf300_StillEnterAquarius()
    {
        IngressSeries series = Ingresses().FindIngresses(CelestialBodyId.Pluto, 2024, 2024);

        series.Ingresses.Should().ContainSingle(i => Near(i.ExactUtc, Utc(2024, 1, 21, 0, 56, 10.117)))
            .Which.Sign.Should().Be("Aquarius");
        series.Ingresses.Should().ContainSingle(i => Near(i.ExactUtc, Utc(2024, 11, 19, 20, 39, 41.275)))
            .Which.Sign.Should().Be("Aquarius");
    }

    #region Helpers

    private IngressService Ingresses() =>
        new(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _logger);

    private static bool Near(DateTime actual, DateTime expected) =>
        Math.Abs((actual - expected).TotalSeconds) <= 1.0;

    private static DateTime Utc(int year, int month, int day, int hour, int minute, double second) =>
        new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc).AddSeconds(second);

    #endregion
}
