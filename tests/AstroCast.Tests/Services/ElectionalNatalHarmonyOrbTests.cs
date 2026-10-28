using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the orbs of the electional Natal Harmony table (branch O1, 1.3.3): the orb lookup sees each point's own
/// name ("Sun", "Venus", "ASC"), so a luminary takes the luminary orbs and a natal angle is an angle, while the table
/// still prints "E Sun" and "N ASC". The Natal Harmony score is a different reading — the significator against four
/// natal planets at 5° — and does not change.
/// </summary>
/// <remarks>
/// <para>
/// Until 1.3.3 the lookup saw the prefixed names, which <c>SynastryOrbs.GetCategory</c> does not recognise, so every
/// pair took the Outer orbs (conjunction 5°): the O investigation found 29% of the major aspects never listed.
/// </para>
/// <para>
/// Expected separations and scores are Tier B (<c>o1-bounds/truth/harmony_cases.py</c>: direct <c>swe_calc_ut</c>,
/// the J2000 New York natal chart at 2000-01-01 12:00 UT; the score by the scorer's written rule on those positions).
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ElectionalNatalHarmonyOrbTests
{
    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ElectionalNatalHarmonyOrbTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>
    /// At 2027-01-07 00:00 UT the election Sun (286.437°) is 6.068° from the natal Sun (280.369°): a conjunction
    /// inside the luminary orb of 8°, outside the Outer orb of 5°.
    /// </summary>
    [Fact]
    public void CalculateNatalHarmonyAspects_AnElectionSunSixDegreesFromTheNatalSun_IsListed()
    {
        IReadOnlyList<InterChartAspect> aspects = ElectionalService.CalculateNatalHarmonyAspects(
            Election(new DateTime(2027, 1, 7, 0, 0, 0)), GoldenFixtures.J2000Nyc(_fixture));

        InterChartAspect sun = aspects.Should().ContainSingle(a => a.Body1Name == "E Sun" && a.Body2Name == "N Sun").Which;
        sun.Aspect.Type.Should().Be(AspectType.Conjunction);
        sun.Orb.Should().BeApproximately(6.068467, 1e-5, "the Tier B separation of the two Suns");
    }

    /// <summary>
    /// At 2026-12-20 00:00 UT the election Sun (268.098°) is 6.144° from the natal Ascendant (274.242°): the natal
    /// ASC is an angle, and against a luminary the wider, luminary orb of 8° applies.
    /// </summary>
    [Fact]
    public void CalculateNatalHarmonyAspects_ANatalAscendant_TakesItsCategory()
    {
        IReadOnlyList<InterChartAspect> aspects = ElectionalService.CalculateNatalHarmonyAspects(
            Election(new DateTime(2026, 12, 20, 0, 0, 0)), GoldenFixtures.J2000Nyc(_fixture));

        InterChartAspect asc = aspects.Should().ContainSingle(a => a.Body1Name == "E Sun" && a.Body2Name == "N ASC").Which;
        asc.Aspect.Type.Should().Be(AspectType.Conjunction);
        asc.Orb.Should().BeApproximately(6.143660, 1e-5, "the Tier B separation of the election Sun from the natal Ascendant");
    }

    /// <summary>
    /// The Natal Harmony score reads the significator against natal Jupiter, Venus, Mars and Saturn at 5°, not the
    /// table: for a wedding (Venus) it is 48 at the first instant and 40 at the second (Tier B, the scorer's rule).
    /// </summary>
    /// <param name="year">The election instant's year.</param>
    /// <param name="month">Its month.</param>
    /// <param name="day">Its day (00:00 UT).</param>
    /// <param name="expected">The score.</param>
    [Theory]
    [InlineData(2027, 1, 7, 48.0)]
    [InlineData(2026, 12, 20, 40.0)]
    public void ScoreNatalHarmony_ReadsTheSignificatorNotTheTable(int year, int month, int day, double expected)
    {
        ElectionalScorer.ScoreNatalHarmony(Election(new DateTime(year, month, day, 0, 0, 0)), GoldenFixtures.J2000Nyc(_fixture), ElectionType.Wedding)
            .Should().Be(expected);
    }

    /// <summary>An election chart at New York, cast at <paramref name="utc"/> read on UTC.</summary>
    /// <param name="utc">The instant, as a UTC reading.</param>
    /// <returns>The chart.</returns>
    private ChartResult Election(DateTime utc) =>
        GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
        {
            Name = "Election",
            LocalDateTime = DateTime.SpecifyKind(utc, DateTimeKind.Unspecified),
            TimeZone = "UTC",
            Latitude = 40.7128,
            Longitude = -74.006,
            LocationName = "New York City",
            EventType = "Event",
            HouseSystem = HouseSystem.Placidus,
        });
}
