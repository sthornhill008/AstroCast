using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// The lunar-return range is a pair of dates picked on a calendar, so its bounds are
/// midnights on the chart's own clock, not in Greenwich (T0-2).
/// </summary>
/// <remarks>
/// <para>
/// The bounds used to be read as UT, so in New York the range ran from 20:00 or 19:00 the
/// evening before each picked date: a return in those hours before the start was listed,
/// and one in those hours before the end was not.
/// </para>
/// <para>
/// J2000 New York's natal Moon is at 223.3237512°; its returns around the edge below are
/// 2026-05-01 21:18:05 UT, 2026-05-29 03:19:54 UT (23:19:54 EDT on the 28th) and
/// 2026-06-25 09:10:54 UT (Tier B: direct <c>swe_calc_ut</c> calls, bisected; read on
/// New York's clock under IANA 2026e).
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class LunarReturnRangeTests
{
    private const double Lat = 40.7128;
    private const double Lon = -74.006;
    private const string Zone = "America/New_York";

    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public LunarReturnRangeTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// A range starting on 2026-05-29 starts at New York's midnight, 04:00 UT, so the return
    /// at 23:19:54 EDT on the 28th is outside it; the first return listed is June's.
    /// </summary>
    [Fact]
    public void Calculate_Should_StartTheRange_AtMidnightOnTheChartsClock()
    {
        IReadOnlyList<LunarReturnResult> results = Calculate(new DateTime(2026, 5, 29), new DateTime(2026, 6, 28));

        results.Select(r => r.ReturnChart.UtcDateTime).Should().ContainSingle()
            .Which.Should().BeCloseTo(new DateTime(2026, 6, 25, 9, 10, 54, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// A range ending on 2026-05-29 ends at New York's midnight, 04:00 UT, so the return at
    /// 23:19:54 EDT on the 28th is inside it, after the one on May 1.
    /// </summary>
    [Fact]
    public void Calculate_Should_EndTheRange_AtMidnightOnTheChartsClock()
    {
        IReadOnlyList<LunarReturnResult> results = Calculate(new DateTime(2026, 5, 1), new DateTime(2026, 5, 29));

        results.Select(r => r.ReturnChart.UtcDateTime).Should().HaveCount(2);
        results[0].ReturnChart.UtcDateTime.Should().BeCloseTo(
            new DateTime(2026, 5, 1, 21, 18, 5, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
        results[1].ReturnChart.UtcDateTime.Should().BeCloseTo(
            new DateTime(2026, 5, 29, 3, 19, 54, DateTimeKind.Utc), TimeSpan.FromSeconds(1));
    }

    /// <summary>J2000 New York's lunar returns between two picked dates, cast in New York.</summary>
    /// <param name="start">The first date picked.</param>
    /// <param name="end">The last date picked.</param>
    /// <returns>The returns.</returns>
    private IReadOnlyList<LunarReturnResult> Calculate(DateTime start, DateTime end)
    {
        ChartEngine engine = GoldenFixtures.CreateChartEngine(_fixture);
        LunarReturnService service = new(_fixture.EphemerisService, engine, new LoggerConfiguration().CreateLogger());

        return service.Calculate(
            GoldenFixtures.J2000Nyc(_fixture), start, end, Lat, Lon, "New York City", Zone);
    }
}
