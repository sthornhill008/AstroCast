using System.Globalization;

using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins <see cref="ChartSect"/>, the horizon as the chart reads it (branch F6, 1.3.3): hayz takes "above the
/// horizon" from the body's true altitude (finding F4-6), and a chart is a day chart when the Sun's longitude lies
/// on the half of the ecliptic above the horizon (ruling D3, with S′ where the ecliptic's upper-meridian point is
/// below the horizon), in every house system.
/// </summary>
/// <remarks>
/// Each chart is cast through the engine at an explicit UTC instant. The altitudes are Tier B: pyswisseph's
/// <c>swe_azalt</c> with the ephemeris files, geometric, without refraction. Hayz is Lilly's rule: in sect by the
/// chart, a diurnal planet above the horizon in a masculine sign or a nocturnal planet below it in a feminine sign.
/// </remarks>
[Collection("Ephemeris")]
public sealed class ChartSectTests
{
    /// <summary>How far the engine's closed-form altitude may lie from <c>swe_azalt</c>'s, in degrees.</summary>
    private const double AltitudeTolerance = 1e-6;

    private readonly EphemerisFixture _fixture;

    /// <summary>Receives the shared ephemeris fixture.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ChartSectTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
    }

    /// <summary>
    /// A day chart (the Sun 17.45° up) at 45° N 120° E, 2026-11-26 06:00 UT, Placidus: Saturn at 8.127° Aries,
    /// latitude −2.605°, is 1.749° below the horizon, so it is not in hayz. Its longitude lies above the horizon, in
    /// the 12th house, so a hemisphere read from the longitude or the house would put it in hayz.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Analyze_SaturnBelowTheHorizonByAltitude_IsNotHayz()
    {
        ChartResult chart = Cast(new DateTime(2026, 11, 26, 6, 0, 0), 45.0, 120.0, HouseSystem.Placidus);

        Body(chart, "Saturn").Altitude.Should().BeApproximately(-1.7490284031219436, AltitudeTolerance);
        Hayz(chart, "Saturn").Should().Be(false);
    }

    /// <summary>
    /// A night chart (the Sun 29.09° down) at 45° N 60° W, 1991-02-09 00:00 UT, Whole Sign: Venus at 13°26′
    /// Pisces, a feminine sign, is 7.844° below the horizon in the 7th sign, so it is in hayz. Until F6 the
    /// hemisphere was the house number, and the 7th house read as above the horizon.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Analyze_VenusBelowTheHorizonInWholeSignHouse7_IsHayz()
    {
        ChartResult chart = Cast(new DateTime(1991, 2, 9, 0, 0, 0), 45.0, -60.0, HouseSystem.WholeSign);

        Body(chart, "Venus").Altitude.Should().BeApproximately(-7.843736129222677, AltitudeTolerance);
        Hayz(chart, "Venus").Should().Be(true);
    }

    /// <summary>
    /// A night chart (the Sun 3.07° down) at 66° N 30° E, 2031-05-11 00:00 UT, Placidus: the Moon at 282.868°
    /// Capricorn, latitude +3.728°, is 2.830° above the horizon, so it is not in hayz. Its longitude lies below the
    /// horizon, in the 1st house, so a hemisphere read from the longitude or the house would put it in hayz.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void Analyze_MoonAboveTheHorizonByAltitude_IsNotHayz()
    {
        ChartResult chart = Cast(new DateTime(2031, 5, 11, 0, 0, 0), 66.0, 30.0, HouseSystem.Placidus);

        Body(chart, "Moon").Altitude.Should().BeApproximately(2.8298189321479286, AltitudeTolerance);
        Hayz(chart, "Moon").Should().Be(false);
    }

    /// <summary>
    /// Whole Sign at 45° N 120° E, 1993-09-30 00:00 UT: the Sun, at 186.848° Libra, is 20.22° up, before the
    /// Ascendant's degree (209.348° Libra) in the rising sign, so the chart is a day chart. Until F6 a day chart
    /// needed the Sun in houses 7 to 12, and in Whole Sign the rising sign is the 1st house.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void IsDayChart_WholeSign_SunTwentyDegreesUpInTheRisingSign_IsDay()
    {
        ChartResult chart = Cast(new DateTime(1993, 9, 30, 0, 0, 0), 45.0, 120.0, HouseSystem.WholeSign);

        Body(chart, "Sun").Altitude.Should().BeApproximately(20.218519751190634, AltitudeTolerance);
        IsDayChart(chart).Should().Be(true);
    }

    /// <summary>
    /// Morinus at 66° S 30° E, 2008-01-16 16:00 UT: the Sun, at 295.897°, is 20.05° up in Morinus house 6, so the
    /// chart is a day chart. Until F6 a day chart needed the Sun in houses 7 to 12.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void IsDayChart_Morinus_SunTwentyDegreesUpInHouse6_IsDay()
    {
        ChartResult chart = Cast(new DateTime(2008, 1, 16, 16, 0, 0), -66.0, 30.0, HouseSystem.Morinus);

        Body(chart, "Sun").Altitude.Should().BeApproximately(20.053740734200584, AltitudeTolerance);
        Body(chart, "Sun").House.Should().Be(6);
        IsDayChart(chart).Should().Be(true);
    }

    /// <summary>
    /// The axis's two ends: a Sun exactly on the Descendant is above the horizon, a day chart; one exactly on the
    /// Ascendant is below it, a night chart.
    /// </summary>
    /// <param name="point">The axis's end the Sun is on, for the test's name.</param>
    /// <param name="sunLongitude">The Sun's longitude, with the Ascendant at 100° and the Descendant at 280°.</param>
    /// <param name="expectedDay">True for a day chart.</param>
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Descendant", 280.0, true)]
    [InlineData("Ascendant", 100.0, false)]
    public void IsDayChart_SunOnTheAxis(string point, double sunLongitude, bool expectedDay)
    {
        HouseData houses = new()
        {
            Cusps = Enumerable.Range(0, 12).Select(i => (100.0 + i * 30.0) % 360.0).ToList(),
            Ascendant = 100.0,
            Midheaven = 10.0,
            Armc = 10.0,
            Vertex = 0.0,
        };

        bool isDay = ChartSect.IsDayChart(sunLongitude, houses);

        isDay.Should().Be(expectedDay, $"the Sun is on the {point}");
    }

    /// <summary>
    /// Where the ecliptic's upper-meridian point is below the horizon, its lower half is above, so the day test
    /// follows the Sun (S′). Equal at 86.5° N 30° E, 2043-05-16 05:00 UT: the Sun is 20.000° up, a day chart, which
    /// the axis test alone would call night. Regiomontanus at 69.6492° N 18.9553° E, 2026-05-15 23:45 UT: the Sun is
    /// 0.559° down, a night chart, which the axis test alone would call day.
    /// </summary>
    /// <param name="system">The house system.</param>
    /// <param name="utc">The instant, yyyy-MM-ddTHH:mm UTC.</param>
    /// <param name="latitude">The place's latitude.</param>
    /// <param name="longitude">The place's longitude.</param>
    /// <param name="sunAltitude">The Sun's altitude, Tier B.</param>
    /// <param name="expectedDay">True for a day chart.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(HouseSystem.Equal, "2043-05-16T05:00", 86.5, 30.0, 19.999897818686904, true)]
    [InlineData(HouseSystem.Regiomontanus, "2026-05-15T23:45", 69.6492, 18.9553, -0.5592349407958891, false)]
    public void IsDayChart_MidheavenPointBelowTheHorizon_FollowsTheSun(
        HouseSystem system, string utc, double latitude, double longitude, double sunAltitude, bool expectedDay)
    {
        DateTime instant = DateTime.ParseExact(utc, "yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

        ChartResult chart = Cast(instant, latitude, longitude, system);

        Body(chart, "Sun").Altitude.Should().BeApproximately(sunAltitude, AltitudeTolerance);
        IsDayChart(chart).Should().Be(expectedDay);
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
            Name = "F6 sect fixture",
            LocalDateTime = utc,
            TimeZone = "UTC",
            Latitude = latitude,
            Longitude = longitude,
            LocationName = "F6 sect fixture",
            EventType = "Event",
            UseExplicitOffset = true,
            UtcOffsetHours = 0.0,
            HouseSystem = system,
        });

    /// <summary>The named body's position.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="name">The body's name.</param>
    /// <returns>Its position.</returns>
    private static PlanetPosition Body(ChartResult chart, string name) =>
        chart.Positions.Single(p => p.Name == name);

    /// <summary>The chart's day or night, from its context.</summary>
    /// <param name="chart">The chart.</param>
    /// <returns>True for a day chart, or null when the chart has no context.</returns>
    private static bool? IsDayChart(ChartResult chart) => chart.ChartContext?.IsDayChart;

    /// <summary>The named planet's hayz, from the chart's sect table.</summary>
    /// <param name="chart">The chart.</param>
    /// <param name="name">The planet's name.</param>
    /// <returns>Its hayz, or null when the chart has no context.</returns>
    private static bool? Hayz(ChartResult chart, string name) =>
        chart.ChartContext?.PlanetarySect.Single(s => s.PlanetName == name).IsHayz;
}
