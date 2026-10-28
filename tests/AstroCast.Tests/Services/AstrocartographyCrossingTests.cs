using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Models.Astrocartography;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the crossing report (ruling S-D8, open detail Q9, branch J): the distance from a place
/// to a line is the great-circle distance to the line's nearest point, in kilometres, on a
/// sphere of radius 6,371.0088 km, and a line is listed when it is within 334 km.
/// </summary>
/// <remarks>
/// Expected values come from outside the code under test: person A's distance at Boulder
/// from the meridian formula on direct <c>swe_calc_ut</c>/<c>swe_sidtime</c> values
/// (fixtures/truth/acg_hartwell.py);
/// the places 333 km and 335 km from a line are built here by spherical trigonometry (Tier A)
/// on Tier B positions, through <see cref="TierB"/>.
/// </remarks>
[Collection("Ephemeris")]
public sealed class AstrocartographyCrossingTests
{
    /// <summary>The sphere's radius the report measures on (kilometres).</summary>
    private const double R = 6371.0088;

    /// <summary>Boulder, Colorado.</summary>
    private const double BoulderLatitude = 40.0150;

    /// <summary>Boulder, Colorado.</summary>
    private const double BoulderLongitude = -105.2705;

    private readonly EphemerisFixture _fixture;
    private readonly AstrocartographyService _service;

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public AstrocartographyCrossingTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
        _service = new AstrocartographyService(fixture.EphemerisService, new LoggerConfiguration().CreateLogger());
    }

    /// <summary>
    /// Person A clicked at Boulder, with the default orb, lists exactly Mars MC at 44.57 km:
    /// the next-nearest line, Mercury ASC, is about 790 km away. Mars's IC line, on the far
    /// side of the meridian circle, is not listed.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Hartwell_AtBoulder_ListsMarsMc_AtItsGreatCircleDistance()
    {
        AcgResult acg = _service.Calculate(Hartwell());

        AcgCrossingReport report = _service.GetCrossings(acg, BoulderLatitude, BoulderLongitude, "Boulder, Colorado");

        report.OrbKilometres.Should().Be(334.0);
        report.Crossings.Select(c => (c.Body, c.LineType)).Should().Equal(
            (CelestialBodyId.Mars, AcgLineType.MC));
        report.Crossings[0].DistanceKilometres.Should().BeApproximately(44.566818, 1e-4);
    }

    /// <summary>
    /// The orb is 334 km on the ground, for a meridian line and a horizon line alike: a place
    /// 333 km from the line lists it, at that distance, and a place 335 km away does not. The
    /// places lie on the great circle through the line's nearest point perpendicular to the line:
    /// on the equator, east of person A's Pluto MC line; and from its Mars ASC line at 40° N,
    /// towards the point beneath Mars, where Mars's altitude is the distance.
    /// </summary>
    /// <param name="body">The body.</param>
    /// <param name="type">The line.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(CelestialBodyId.Pluto, AcgLineType.MC)]
    [InlineData(CelestialBodyId.Mars, AcgLineType.ASC)]
    public void PlaceAt333Km_ListsTheLine_AndAt335Km_DoesNot(CelestialBodyId body, AcgLineType type)
    {
        ChartResult chart = Hartwell();
        AcgResult acg = _service.Calculate(chart);
        (double ra, double dec) = TierB.Equatorial(chart.JulianDay, body);
        double gast = TierB.GastDegrees(chart.JulianDay);

        foreach ((double km, bool listed) in new[] { (333.0, true), (335.0, false) })
        {
            (double lat, double lon) = type == AcgLineType.MC
                ? (0.0, Wrap180(ra - gast + km / R * 180.0 / Math.PI))
                : Towards(40.0, RisingLongitude(ra, dec, gast, 40.0), dec, Wrap180(ra - gast), km);

            AcgCrossingReport report = _service.GetCrossings(acg, lat, lon, "Test place");

            AcgCrossing? crossing = report.Crossings.SingleOrDefault(c => c.Body == body && c.LineType == type);
            if (listed)
            {
                crossing.Should().NotBeNull($"{body} {type} is {km} km from the place");
                (crossing?.DistanceKilometres).Should().BeApproximately(km, 1e-3);
            }
            else
            {
                crossing.Should().BeNull($"{body} {type} is {km} km from the place, beyond the 334 km orb");
            }
        }
    }

    /// <summary>
    /// A meridian line's distance from a place is R · asin(|cos φ · sin Δλ|), where φ is the
    /// place's latitude and Δλ its longitude less the line's (Tier A on Tier B), whatever the
    /// latitude: at Boulder, Quito, Reykjavik and Sydney, for every MC and IC line of person A
    /// within 60° of longitude of the place.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void MeridianLineDistance_IsRTimesAsinOfCosLatitudeTimesSinDeltaLongitude()
    {
        ChartResult chart = Hartwell();
        AcgResult acg = _service.Calculate(chart);
        double gast = TierB.GastDegrees(chart.JulianDay);
        (double Lat, double Lon)[] places = [(BoulderLatitude, BoulderLongitude), (-0.1807, -78.4678), (64.1466, -21.9426), (-33.8688, 151.2093)];

        foreach ((double lat, double lon) in places)
        {
            AcgCrossingReport report = _service.GetCrossings(acg, lat, lon, "Test place", orbKilometres: 20016.0);

            foreach (AcgCrossing crossing in report.Crossings.Where(c => c.LineType is AcgLineType.MC or AcgLineType.IC))
            {
                double lineLon = Wrap180(TierB.Equatorial(chart.JulianDay, crossing.Body).RightAscension - gast
                    + (crossing.LineType == AcgLineType.IC ? 180.0 : 0.0));
                double dLon = Wrap180(lon - lineLon);
                if (Math.Abs(dLon) > 60.0)
                {
                    continue;
                }

                double expected = R * Math.Asin(Math.Abs(Math.Cos(lat * Math.PI / 180.0) * Math.Sin(dLon * Math.PI / 180.0)));
                crossing.DistanceKilometres.Should().BeApproximately(expected, 1e-6,
                    $"{crossing.BodyName} {crossing.LineType} from ({lat}, {lon})");
            }
        }
    }

    #region Helpers

    /// <summary>Person A (invented): 1947-10-05 09:30 at UTC−5 (explicit), Harrisburg, Pennsylvania.</summary>
    private ChartResult Hartwell() => GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
    {
        Name = "Eleanor Hartwell",
        LocalDateTime = new DateTime(1947, 10, 5, 9, 30, 0),
        TimeZone = "America/New_York",
        UseExplicitOffset = true,
        UtcOffsetHours = -5.0,
        Latitude = 40.2737,
        Longitude = -76.8844,
        LocationName = "Harrisburg, Pennsylvania, United States",
        EventType = "Birth",
    });

    /// <summary>Where a body rises at a latitude (closed form, Tier A on Tier B).</summary>
    private static double RisingLongitude(double ra, double dec, double gast, double latitude)
    {
        double cosH = -Math.Tan(latitude * Math.PI / 180.0) * Math.Tan(dec * Math.PI / 180.0);
        return Wrap180(ra - Math.Acos(cosH) * 180.0 / Math.PI - gast);
    }

    /// <summary>
    /// The point <paramref name="km"/> from (<paramref name="lat"/>, <paramref name="lon"/>) along
    /// the great circle towards (<paramref name="toLat"/>, <paramref name="toLon"/>) (Tier A).
    /// </summary>
    private static (double Lat, double Lon) Towards(double lat, double lon, double toLat, double toLon, double km)
    {
        double p1 = lat * Math.PI / 180.0, l1 = lon * Math.PI / 180.0;
        double p2 = toLat * Math.PI / 180.0, l2 = toLon * Math.PI / 180.0;
        double bearing = Math.Atan2(
            Math.Sin(l2 - l1) * Math.Cos(p2),
            Math.Cos(p1) * Math.Sin(p2) - Math.Sin(p1) * Math.Cos(p2) * Math.Cos(l2 - l1));
        double d = km / R;
        double p3 = Math.Asin(Math.Sin(p1) * Math.Cos(d) + Math.Cos(p1) * Math.Sin(d) * Math.Cos(bearing));
        double l3 = l1 + Math.Atan2(Math.Sin(bearing) * Math.Sin(d) * Math.Cos(p1), Math.Cos(d) - Math.Sin(p1) * Math.Sin(p3));
        return (p3 * 180.0 / Math.PI, Wrap180(l3 * 180.0 / Math.PI));
    }

    private static double Wrap180(double degrees)
    {
        double x = ((degrees + 180.0) % 360.0 + 360.0) % 360.0;
        return x - 180.0;
    }

    #endregion
}
