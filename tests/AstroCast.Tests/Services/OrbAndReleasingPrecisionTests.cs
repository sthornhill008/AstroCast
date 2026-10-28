using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Two precision defects of the chart's derived tables, on the J2000 New York chart (2000-01-01 12:00 UT, 40.7128 N
/// 74.006 W, Placidus) cast through the engine: the angular orb kept to two decimals (E1-2), and a Level 2 releasing
/// period of less than a day left by a fractional sum (E1-4).
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class OrbAndReleasingPrecisionTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public OrbAndReleasingPrecisionTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Jupiter on the IC keeps its full orb, 3.2159220° (Tier B: Jupiter 25.2530878°, IC 28.4690098°, pyswisseph over
    /// the ephemeris files). Until 1.3.3 it was stored as 3.22, so an export truncating to the minute printed 3°13'
    /// for 3°12′57″ (E1-2).
    /// </summary>
    [Fact]
    public void Angularity_Orb_IsUnrounded()
    {
        ChartResult chart = GoldenFixtures.J2000Nyc(_fixture);

        AngularPlanet jupiter = chart.ChartContext?.AngularPlanets.Single(a => a.PlanetName == "Jupiter" && a.Angle == "IC")
            ?? throw new InvalidOperationException("The chart has no chart context.");

        jupiter.Orb.Should().BeApproximately(3.2159220, 1e-6);
    }

    /// <summary>
    /// Releasing from Spirit, every Level 2 period of every Level 1 period: none starts and ends on the same date, and
    /// the last of each Level 1 period ends on that period's end (E1-4). Until 1.3.3 a residual of less than a day,
    /// left by summing fractional days, was printed as a period of its own ("| Sagittarius | Jupiter | 0.0m |
    /// 2026-12-31 | 2026-12-31 |").
    /// </summary>
    [Fact]
    public void ZodiacalReleasing_NoZeroLengthPeriod()
    {
        ChartResult chart = GoldenFixtures.J2000Nyc(_fixture);
        IReadOnlyList<ZRPeriod> level1 = ZodiacalReleasingAnalyzer.Analyze(chart, new DateTime(2026, 6, 10)).Level1Periods;

        level1.Should().NotBeEmpty();
        foreach (ZRPeriod l1 in level1)
        {
            DateTime inside = l1.StartDate.AddDays((l1.EndDate - l1.StartDate).TotalDays / 2);
            ZodiacalReleasingResult result = ZodiacalReleasingAnalyzer.Analyze(chart, inside);

            result.CurrentL1Start.Should().Be(l1.StartDate);
            result.CurrentL2Periods.Should().NotBeEmpty();
            result.CurrentL2Periods.Should().NotContain(
                p => p.StartDate.Date == p.EndDate.Date,
                $"no Level 2 period of {l1.Sign} ({l1.StartDate:yyyy-MM-dd} to {l1.EndDate:yyyy-MM-dd}) starts and ends on the same date");
            result.CurrentL2Periods[^1].EndDate.Should().Be(l1.EndDate);
        }
    }
}
