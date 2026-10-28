using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins <see cref="HousePlacement"/>, the one rule that finds the house a longitude lies in (rulings D2 and S-D1,
/// branch F6, 1.3.3): house <c>i</c> is the arc from cusp <c>i</c> to cusp <c>i + 1</c>, taken in the direction the
/// cusps run, cusp <c>i</c> included and cusp <c>i + 1</c> excluded.
/// </summary>
/// <remarks>
/// Every expected value is Tier A: the arc that holds the longitude, by arithmetic on the cusps. Where the cusps are
/// a real chart's they are Tier B, from pyswisseph with the ephemeris files. The engine tests cast at an explicit UTC
/// instant and read each body's <see cref="PlanetPosition.House"/> and <see cref="PlanetPosition.HouseExact"/>.
/// </remarks>
[Collection("Ephemeris")]
public sealed class HousePlacementTests
{
    /// <summary>How far an engine's <see cref="PlanetPosition.HouseExact"/> may lie from Tier A's.</summary>
    private const double HouseExactTolerance = 1e-12;

    /// <summary>Equal cusps from 15°: 15, 45, 75 … 345.</summary>
    private static readonly double[] EqualFrom15 = EqualCusps(15.0);

    /// <summary>
    /// Placidus cusps at 66° N 150° W, JD 2369730.5 (1776-01-01 00:00 UT; the F6 investigation's houses-grid record
    /// 216), from pyswisseph: Aries holds no cusp, so it is intercepted in the 11th house (328.5634° to 31.2037°).
    /// </summary>
    private static readonly double[] PlacidusInterceptingAries =
    [
        106.71708667189536, 111.13719862803117, 117.47805918281323, 127.79512143075158,
        148.5633904617285, 211.20369498213714, 286.71708667189534, 291.1371986280312,
        297.47805918281324, 307.7951214307516, 328.5633904617285, 31.203694982137126,
    ];

    /// <summary>
    /// Regiomontanus cusps at 69.6492° N 18.9553° E, 2026-05-15 23:45 UT, from pyswisseph. The ecliptic's
    /// upper-meridian point is below the horizon there, so the library runs the cusps backwards through the zodiac.
    /// </summary>
    private static readonly double[] RegiomontanusBackward =
    [
        58.98218328492226, 278.18201417586897, 254.20992057749456, 250.5229736000985,
        248.26068723272726, 245.63887144715022, 238.98218328492226, 98.18201417586897,
        74.20992057749459, 70.52297360009851, 68.26068723272726, 65.63887144715022,
    ];

    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public HousePlacementTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>A longitude exactly on a cusp lies in the house that starts there.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void HouseOf_LongitudeOnACusp_IsInTheHouseThatStartsThere()
    {
        int? house = HousePlacement.HouseOf(45.0, EqualFrom15);

        house.Should().Be(2);
    }

    /// <summary>
    /// A longitude one ulp before a cusp lies in the house before it. Until F6 the Lots' helper added 360° first,
    /// which rounded the longitude onto the cusp and gave the 2nd house.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void HouseOf_OneUlpBeforeACusp_IsInTheHouseBefore()
    {
        int? house = HousePlacement.HouseOf(Math.BitDecrement(45.0), EqualFrom15);

        house.Should().Be(1);
    }

    /// <summary>The 1st house runs from 350° through 0° Aries to 15°.</summary>
    /// <param name="longitude">The longitude to place.</param>
    /// <param name="cusps">Cusps 1–12.</param>
    /// <param name="expectedHouse">The house holding the longitude.</param>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(5.0, new double[] { 350, 15, 45, 75, 105, 135, 165, 195, 225, 255, 285, 315 }, 1)]
    [InlineData(355.0, new double[] { 350, 15, 45, 75, 105, 135, 165, 195, 225, 255, 285, 315 }, 1)]
    [InlineData(20.0, new double[] { 350, 15, 45, 75, 105, 135, 165, 195, 225, 255, 285, 315 }, 2)]
    [InlineData(100.0, new double[] { 350, 15, 45, 75, 105, 135, 165, 195, 225, 255, 285, 315 }, 4)]
    public void HouseOf_WrapsThroughZeroAries(double longitude, double[] cusps, int expectedHouse)
    {
        int? house = HousePlacement.HouseOf(longitude, cusps);

        house.Should().Be(expectedHouse);
    }

    /// <summary>
    /// A longitude of exactly 360.0 is 0° Aries, the start of the 1st house of Whole Sign cusps from Aries. Until F6
    /// the transit, progression and overlay helpers gave the 12th.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void HouseOf_Longitude360_IsZeroAries()
    {
        int? house = HousePlacement.HouseOf(360.0, EqualCusps(0.0));

        house.Should().Be(1);
    }

    /// <summary>15° Aries, in a sign that holds no cusp, lies in the house whose arc holds it: the 11th.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void HouseOf_InterceptedSign_IsInTheHouseThatHoldsIt()
    {
        int? house = HousePlacement.HouseOf(15.0, PlacidusInterceptingAries);

        house.Should().Be(11);
    }

    /// <summary>
    /// Cusps 2 and 3 equal: the 2nd house's arc has no length and holds nothing, so 50° lies in the 3rd. Until F6 the
    /// helpers read the equal pair as the whole circle and gave the 2nd.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void HouseOf_TwoEqualCusps_TheirArcHoldsNothing()
    {
        double[] cusps = EqualCusps(15.0);
        cusps[2] = cusps[1];

        int? house = HousePlacement.HouseOf(50.0, cusps);

        house.Should().Be(3);
    }

    /// <summary>
    /// Cusps that run backwards are followed in their own direction: house <c>i</c> runs from cusp <c>i</c> back to
    /// cusp <c>i + 1</c>. The longitudes are pyswisseph's at the cusps' instant. Until F6 the helpers took the first
    /// forward arc holding the longitude: the 1st for Venus and the 2nd for the Moon.
    /// </summary>
    /// <param name="body">The body, for the test's name.</param>
    /// <param name="longitude">Its ecliptic longitude.</param>
    /// <param name="expectedHouse">The house holding it, in the cusps' direction.</param>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Venus", 86.34279661564413, 8)]
    [InlineData("Moon", 43.180051451791265, 1)]
    public void HouseOf_CuspsRunningBackward_FollowsTheirDirection(string body, double longitude, int expectedHouse)
    {
        int? house = HousePlacement.HouseOf(longitude, RegiomontanusBackward);

        house.Should().Be(expectedHouse, $"{body} at {longitude}° lies on the backward arc of house {expectedHouse}");
    }

    /// <summary>20°, just past an Ascendant at 15°, lies in the 1st house.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void HouseOf_LongitudeJustPastTheAscendant_IsHouse1()
    {
        int? house = HousePlacement.HouseOf(20.0, EqualCusps(15.0));

        house.Should().Be(1);
    }

    /// <summary>5°, after an Ascendant at 350°, lies in the 1st house, which runs through 0° Aries to 20°.</summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void HouseOf_LongitudeAfterAWrappingAscendant_IsHouse1()
    {
        int? house = HousePlacement.HouseOf(5.0, EqualCusps(350.0));

        house.Should().Be(1);
    }

    /// <summary>
    /// Koch at 66° S 150° W, 1776-10-08 04:00 UT (JD 2370011.66666667): every body lies in a house from 1 to 12, and
    /// Pluto, at 295.981°, in the 10th (cusp 10 at 285.985°, cusp 11 at 312.648°). The library's house position
    /// fails for Pluto there ("Koch house position failed in circumpolar area") and returns 0, which until F6 the
    /// chart printed as House 0. The houses are Tier A on pyswisseph's cusps and longitudes.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Assign_Koch66South_NoBodyInHouseZero()
    {
        ChartResult chart = Cast(new DateTime(1776, 10, 8, 4, 0, 0), -66.0, -150.0, HouseSystem.Koch);

        Houses(chart).Should().Equal(new Dictionary<string, int?>
        {
            ["Sun"] = 7, ["Moon"] = 5, ["Mercury"] = 8, ["Venus"] = 7, ["Mars"] = 5, ["Jupiter"] = 4,
            ["Saturn"] = 7, ["Uranus"] = 3, ["Neptune"] = 6, ["Pluto"] = 10, ["True Node"] = 4, ["Chiron"] = 1,
        });
    }

    /// <summary>
    /// Placidus at 45° N 120° E, 2026-03-10 04:00 UT: the Moon, at 246.1445° with latitude −5.266°, lies past cusp 6
    /// (244.9443°), so it is in the 6th house. Until F6 the library's house position with the Moon's latitude
    /// (5.886) placed it in the 5th.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Assign_PlacesByLongitudeNotLatitude()
    {
        ChartResult chart = Cast(new DateTime(2026, 3, 10, 4, 0, 0), 45.0, 120.0, HouseSystem.Placidus);

        chart.Positions.Single(p => p.Name == "Moon").House.Should().Be(6);
    }

    /// <summary>
    /// J2000 New York (the golden fixture): each body's <see cref="PlanetPosition.HouseExact"/> is its house plus
    /// the fraction of the arc from its cusp to the next, so the whole part is the house. The values are Tier A on
    /// pyswisseph's cusps and longitudes. Until F6 it was the library's house position, which for Pluto read 11.686
    /// where the fraction of the arc is 11.881.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Assign_HouseExactIsTheHousePlusTheArcFraction()
    {
        Dictionary<string, double> expected = new()
        {
            ["Sun"] = 1.15382581090075, ["Moon"] = 10.59973275661238, ["Mercury"] = 12.884260281890569,
            ["Venus"] = 11.402769038327255, ["Mars"] = 2.334624476671486, ["Jupiter"] = 3.9022056985742615,
            ["Saturn"] = 4.481516620728295, ["Uranus"] = 2.017750429326632, ["Neptune"] = 1.7268585845718443,
            ["Pluto"] = 11.88104150825889, ["True Node"] = 7.74596492298793, ["Chiron"] = 11.888917431197704,
        };

        ChartResult chart = GoldenFixtures.J2000Nyc(_fixture);

        foreach (PlanetPosition position in chart.Positions)
        {
            position.HouseExact.Should().BeApproximately(expected[position.Name], HouseExactTolerance, position.Name);
            position.House.Should().Be((int)Math.Floor(expected[position.Name]), position.Name);
        }
    }

    /// <summary>Casts a chart at an explicit UTC instant.</summary>
    /// <param name="utc">The instant, as a UTC reading.</param>
    /// <param name="latitude">The place's latitude.</param>
    /// <param name="longitude">The place's longitude.</param>
    /// <param name="system">The house system.</param>
    /// <returns>The chart.</returns>
    private ChartResult Cast(DateTime utc, double latitude, double longitude, HouseSystem system) =>
        GoldenFixtures.CreateChartEngine(_fixture).Calculate(new ChartInput
        {
            Name = "F6 placement fixture",
            LocalDateTime = utc,
            TimeZone = "UTC",
            Latitude = latitude,
            Longitude = longitude,
            LocationName = "F6 placement fixture",
            EventType = "Event",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            HouseSystem = system,
        });

    /// <summary>Each body's house, by name.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>The houses.</returns>
    private static Dictionary<string, int?> Houses(ChartResult chart) =>
        chart.Positions.ToDictionary(p => p.Name, p => p.House);

    /// <summary>Equal cusps 30° apart from the given first cusp.</summary>
    /// <param name="ascendant">The first cusp, in degrees.</param>
    /// <returns>Cusps 1–12.</returns>
    private static double[] EqualCusps(double ascendant) =>
        Enumerable.Range(0, 12).Select(i => (ascendant + i * 30.0) % 360.0).ToArray();
}
