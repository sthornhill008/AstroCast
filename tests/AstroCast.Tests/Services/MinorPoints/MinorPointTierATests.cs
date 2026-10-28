using System.Globalization;

using AstroCast.Core.Models;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Xunit.Abstractions;

namespace AstroCast.Tests.Services.MinorPoints;

/// <summary>
/// Lilith against Tier A (branch Asteroids, 1.6.0): the mean lunar apogee by Meeus ch. 47's mean elements with the
/// reduction to the ecliptic (u = L′ − M′ + 180° − Ω; λ = Ω + atan2(cos i sin u, cos u); β = asin(sin i sin u);
/// i = 5.1453964°; plus the nutation in longitude), computed by lilith_meeus.py into <c>TierB/lilith_meeus.tsv</c>
/// at each fixture's instant. The engine's Lilith, the library's mean apogee (SE_MEAN_APOG), lies within 10″ in
/// longitude and 1″ in latitude of it: under 1″ at every fixture but N1 (1300), 4.66″.
/// </summary>
/// <param name="ephemeris">The shared ephemeris fixture.</param>
/// <param name="output">Receives each fixture's deviation.</param>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class MinorPointTierATests(EphemerisFixture ephemeris, ITestOutputHelper output)
{
    /// <summary>Tolerance on Lilith's longitude, in arcseconds.</summary>
    private const double LongitudeToleranceArcSeconds = 10.0;

    /// <summary>Tolerance on Lilith's latitude, in arcseconds.</summary>
    private const double LatitudeToleranceArcSeconds = 1.0;

    /// <summary>
    /// Lilith's longitude lies within 10″ and its latitude within 1″ of Meeus's mean apogee at the fixture's
    /// instant.
    /// </summary>
    /// <param name="fixture">The lists' stem: the fixture.</param>
    [Theory]
    [InlineData("j2000nyc")]
    [InlineData("secondary")]
    [InlineData("hartwell")]
    [InlineData("m35")]
    [InlineData("n1")]
    [InlineData("tromso")]
    public void Lilith_MatchesMeeus(string fixture)
    {
        MinorPointTierB.LilithRow row = MinorPointTierB.LoadLilith(fixture);

        ChartResult chart = MinorPointTierB.Cast(ephemeris, fixture);

        PlanetPosition lilith = chart.MinorPoints.Should().ContainSingle(p => p.Name == "Lilith").Which;
        double longitudeArcSeconds = MinorPointTierB.WrappedDifference(lilith.Longitude, row.Longitude) * 3600.0;
        double latitudeArcSeconds = Math.Abs(lilith.Latitude - row.Latitude) * 3600.0;
        longitudeArcSeconds.Should().BeLessThanOrEqualTo(LongitudeToleranceArcSeconds,
            "the library's mean apogee and Meeus's mean elements agree in longitude to within 10″");
        latitudeArcSeconds.Should().BeLessThanOrEqualTo(LatitudeToleranceArcSeconds,
            "the reduction to the ecliptic gives the latitude to within 1″");
        output.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{fixture}: Lilith vs Meeus: longitude {longitudeArcSeconds:F2}″, latitude {latitudeArcSeconds:F2}″"));
    }
}
