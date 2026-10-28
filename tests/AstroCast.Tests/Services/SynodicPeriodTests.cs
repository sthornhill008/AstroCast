using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the synodic period (finding T5-2, branch B): consecutive conjunctions belong to one cycle
/// until the pair has passed through an opposition, so the passes of a triple conjunction are one
/// cycle, which begins at its first pass. Each first pass's period is the time since the previous
/// cycle's first pass; a later pass has no period of its own and is marked as pass n of N; the
/// average, minimum and maximum are taken over first passes only. Until branch B every conjunction
/// counted as a cycle, so Saturn–Uranus 2000–2100 averaged 15.77 years. Sun–Mercury, Sun–Venus and
/// Mercury–Venus, which can never be in opposition, keep a cycle per conjunction (Steve's ruling).
/// </summary>
/// <remarks>
/// Expected instants are Tier B (direct <c>swe_calc_ut</c>): the conjunctions, and the oppositions
/// between them that end a cycle. Periods and averages are Tier A arithmetic on those instants, each
/// rounded to 0.01 day as the series prints them, and years are days / 365.25.
/// </remarks>
[Collection("Ephemeris")]
public sealed class SynodicPeriodTests
{
    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public SynodicPeriodTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Saturn–Uranus 2000–2100 has four conjunctions, 2032-06-28 and the triple of 2079-02-26,
    /// 2079-08-31 and 2079-10-21 (Tier B), with oppositions only before 2032 and after 2079: two
    /// cycles. The average synodic period is the one first-pass interval, 17,044.39 days (46.66
    /// years). Until branch B: 5,760.43 days (15.77 years), range 51.54–17,044.39.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void SaturnUranus2000To2100_AveragesTheFirstPassInterval()
    {
        SynodicCycleSeries series = Service().FindConjunctions(CelestialBodyId.Saturn, CelestialBodyId.Uranus, 2000, 2100);

        series.Conjunctions.Should().HaveCount(4);
        series.AverageSynodicPeriodDays.Should().Be(17044.39);
        series.AverageSynodicPeriodYears.Should().Be(46.66);
        series.MinSynodicPeriodDays.Should().Be(17044.39);
        series.MaxSynodicPeriodDays.Should().Be(17044.39);
    }

    /// <summary>
    /// The 2079 passes are pass 1, 2 and 3 of 3: the first carries the period since 2032-06-28,
    /// 17,044.39 days, the later two none. The 2032 conjunction is a cycle of one pass with no
    /// period, the first in the series. The export marks the later passes.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void SaturnUranus2079_PassesAreMarkedOneTwoAndThreeOfThree()
    {
        SynodicCycleSeries series = Service().FindConjunctions(CelestialBodyId.Saturn, CelestialBodyId.Uranus, 2000, 2100);

        series.Conjunctions.Select(c => c.DateTimeUtc.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture))
            .Should().Equal("2032-06-28", "2079-02-26", "2079-08-31", "2079-10-21");
        series.Conjunctions.Select(c => (c.PassNumber, c.PassCount)).Should().Equal((1, 1), (1, 3), (2, 3), (3, 3));
        series.Conjunctions[0].SynodicPeriodDays.Should().BeNull();
        series.Conjunctions[1].SynodicPeriodDays.Should().Be(17044.39);
        series.Conjunctions[1].SynodicPeriodYears.Should().Be(46.66);
        series.Conjunctions[2].SynodicPeriodDays.Should().BeNull();
        series.Conjunctions[3].SynodicPeriodDays.Should().BeNull();

        string markdown = SynodicCycleFormatter.ToMarkdown(series);
        markdown.Should().Contain("| 17044 days (46.66 yr), pass 1 of 3 |")
            .And.Contain("| pass 2 of 3 |")
            .And.Contain("| pass 3 of 3 |");
    }

    /// <summary>
    /// Guard: a pair with no multi-pass conjunction is unchanged. Jupiter–Saturn 2000–2050:
    /// 2000-05-28, 2020-12-21 and 2040-10-31 (Tier B), an opposition between each, so three cycles of
    /// one pass; periods 7,512.09 and 7,253.73 days, average 7,382.91 (20.21 years).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void JupiterSaturn2000To2050_SinglePasses_AreUnchanged()
    {
        SynodicCycleSeries series = Service().FindConjunctions(CelestialBodyId.Jupiter, CelestialBodyId.Saturn, 2000, 2050);

        series.Conjunctions.Should().HaveCount(3).And.OnlyContain(c => c.PassNumber == 1 && c.PassCount == 1);
        series.AverageSynodicPeriodDays.Should().Be(7382.91);
        series.AverageSynodicPeriodYears.Should().Be(20.21);
        series.MinSynodicPeriodDays.Should().Be(7253.73);
        series.MaxSynodicPeriodDays.Should().Be(7512.09);
    }

    /// <summary>
    /// Guard for Steve's ruling: Sun–Venus can never be in opposition, so it keeps a cycle per
    /// conjunction. 2020–2030: 14 conjunctions (Tier B), each a cycle of one pass, 13 periods from
    /// 286.85 to 296.18 days, averaging 291.59 days (0.80 years), as before.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void SunVenus2020To2030_NeverOpposed_KeepsACyclePerConjunction()
    {
        SynodicCycleSeries series = Service().FindConjunctions(CelestialBodyId.Sun, CelestialBodyId.Venus, 2020, 2030);

        series.Conjunctions.Should().HaveCount(14).And.OnlyContain(c => c.PassNumber == 1 && c.PassCount == 1);
        series.AverageSynodicPeriodDays.Should().Be(291.59);
        series.AverageSynodicPeriodYears.Should().Be(0.80);
        series.MinSynodicPeriodDays.Should().Be(286.85);
        series.MaxSynodicPeriodDays.Should().Be(296.18);
    }

    private SynodicCycleService Service() => new(_fixture.EphemerisService, _logger);
}
