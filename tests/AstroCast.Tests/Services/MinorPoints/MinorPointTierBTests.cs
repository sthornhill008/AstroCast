using System.Globalization;

using AstroCast.Core.Models;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Xunit.Abstractions;

namespace AstroCast.Tests.Services.MinorPoints;

/// <summary>
/// The engine's minor points against Tier B (branch Asteroids, 1.6.0): six fixtures cast with the switch on, every
/// row of the five and every aspect at 2° held to ast_truth.py's lists (<see cref="MinorPointTierB"/>). Among them
/// J2000 NYC's Pallas retrograde and out of bounds (declination −29.707370°), person A's Ceres and Pallas, N1's Ceres and Juno
/// and Tromsø's Juno retrograde, and Tromsø's Lilith out of bounds. The seventh fixture, J2000 NYC at Los Angeles,
/// is the relocation case in <c>ChartEngineTests</c>.
/// </summary>
/// <param name="ephemeris">The shared ephemeris fixture.</param>
/// <param name="output">Receives each list's largest deviations.</param>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class MinorPointTierBTests(EphemerisFixture ephemeris, ITestOutputHelper output)
{
    /// <summary>
    /// The five rows of a fixture, in R1's order: longitude, latitude, right ascension and declination within 1e-6°,
    /// the speed within 1e-6° a day, the house, the direction and out of bounds (against the chart's obliquity)
    /// equal; the positions still the twelve.
    /// </summary>
    /// <param name="fixture">The lists' stem: the fixture.</param>
    [Theory]
    [InlineData("j2000nyc")]
    [InlineData("secondary")]
    [InlineData("hartwell")]
    [InlineData("m35")]
    [InlineData("n1")]
    [InlineData("tromso")]
    public void Positions_MatchTierB(string fixture)
    {
        List<MinorPointTierB.PointRow> rows = MinorPointTierB.LoadPoints(fixture);
        rows.Should().HaveCount(5, "the list holds the five");

        ChartResult chart = MinorPointTierB.Cast(ephemeris, fixture);

        (double position, double speed) = MinorPointTierB.AssertPoints(chart, rows);
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{fixture}_points.tsv: 5 rows; largest deviations: position {position:E2} deg, speed {speed:E2} deg/day"));
    }

    /// <summary>
    /// The aspects of a fixture, row for row in the list's order (by orb): point, aspect, target and applying or
    /// separating equal, the orb within 1e-6°.
    /// </summary>
    /// <param name="fixture">The lists' stem: the fixture.</param>
    /// <param name="aspects">The number of aspects in the list, as ast_truth.py's summary reports it.</param>
    [Theory]
    [InlineData("j2000nyc", 5)]
    [InlineData("secondary", 3)]
    [InlineData("hartwell", 5)]
    [InlineData("m35", 7)]
    [InlineData("n1", 6)]
    [InlineData("tromso", 6)]
    public void Aspects_MatchTierB(string fixture, int aspects)
    {
        List<MinorPointTierB.AspectRow> rows = MinorPointTierB.LoadAspects(fixture);
        rows.Should().HaveCount(aspects, "the list holds the aspects the summary reports");

        ChartResult chart = MinorPointTierB.Cast(ephemeris, fixture);

        double orb = MinorPointTierB.AssertAspects(chart, rows);
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{fixture}_aspects.tsv: {aspects} rows; largest deviation: orb {orb:E2} deg"));
    }
}
