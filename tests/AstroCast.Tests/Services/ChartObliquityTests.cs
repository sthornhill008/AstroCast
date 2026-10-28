using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins <see cref="ChartResult.Obliquity"/> (branch F7, 1.3.3): every chart carries the true obliquity of the
/// ecliptic at its own Julian Day, from the call that already places its planets in their houses, and a composite
/// carries it at the midpoint Julian Day its houses are cast for. Out-of-bounds is read against it.
/// </summary>
/// <remarks>
/// <para>
/// The expected values are Tier B: the IAU mean obliquity, Meeus, <i>Astronomical Algorithms</i>, 2nd ed.,
/// eq. 22.2, ε₀ = 23°26′21.448″ − 46.8150″T − 0.00059″T² + 0.001813″T³, with T in Julian centuries from J2000.0
/// (JD 2451545.0). The true obliquity adds the nutation in obliquity, which stays under 0.0026°, so it lies within
/// 0.003° of the mean. An independent Swiss Ephemeris build (pyswisseph 2.10.03, Moshier) gives, at 1800-06-20 12:00
/// UT, mean 23.46523 and true 23.46737; at the composite's midpoint, JD 2419939.5, mean 23.45054 and true 23.45289.
/// </para>
/// <para>
/// Until 1.3.3 out-of-bounds was read against a constant, 23.4367°, the obliquity of about 2020 (foundations F5-1).
/// </para>
/// </remarks>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ChartObliquityTests
{
    /// <summary>Greenwich latitude, degrees north.</summary>
    private const double GreenwichLatitude = 51.4769;

    /// <summary>Greenwich longitude, degrees east.</summary>
    private const double GreenwichLongitude = 0.0;

    /// <summary>The Julian Day of 1800-06-20 12:00 UT.</summary>
    private const double JulianDay1800 = 2378667.0;

    /// <summary>The midpoint of 1800-06-20 12:00 UT and 2026-06-20 12:00 UT (JD 2461212.0).</summary>
    private const double MidpointJulianDay = 2419939.5;

    /// <summary>The largest the nutation in obliquity makes the true obliquity differ from the mean, with margin.</summary>
    private const double NutationTolerance = 0.003;

    private readonly EphemerisFixture _fixture;
    private readonly ChartEngine _engine;

    /// <summary>Receives the shared ephemeris fixture and builds the engine.</summary>
    /// <param name="fixture">The shared ephemeris fixture.</param>
    public ChartObliquityTests(EphemerisFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        _fixture = fixture;
        _engine = GoldenFixtures.CreateChartEngine(fixture);
    }

    /// <summary>
    /// Every path through the engine gives the chart the true obliquity at its own Julian Day: within the nutation's
    /// reach of the IAU mean obliquity there, and equal to <c>GetObliquity</c> at that Julian Day. An event chart is
    /// cast at a Julian Day labelled at an explicit offset, so <c>CalculateAtInstant</c> takes Greenwich at UTC+0.
    /// </summary>
    /// <param name="path">The engine's path.</param>
    [Theory]
    [InlineData("Calculate")]
    [InlineData("CalculateAtInstant")]
    [InlineData("CalculateForElectional (wall clock)")]
    [InlineData("CalculateForElectional (instant)")]
    [InlineData("CalculateElectionalScoringInputs")]
    public void Obliquity_EachEnginePath_IsTheTrueObliquityAtTheChartsJulianDay(string path)
    {
        ChartInput input = Greenwich(new DateTime(1800, 6, 20, 12, 0, 0));
        DateTime instant = new(1800, 6, 20, 12, 0, 0, DateTimeKind.Utc);

        ChartResult chart = path switch
        {
            "Calculate" => _engine.Calculate(input),
            "CalculateAtInstant" => _engine.CalculateAtInstant(Greenwich(input.LocalDateTime, atExplicitOffset: true), JulianDay1800),
            "CalculateForElectional (wall clock)" => _engine.CalculateForElectional(input),
            "CalculateForElectional (instant)" => _engine.CalculateForElectional(input, instant),
            "CalculateElectionalScoringInputs" => _engine.CalculateElectionalScoringInputs(input, instant),
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, "Not an engine path."),
        };

        chart.JulianDay.Should().Be(JulianDay1800);
        chart.Obliquity.Should().BeApproximately(MeanObliquity(chart.JulianDay), NutationTolerance);
        chart.Obliquity.Should().Be(_fixture.EphemerisService.GetObliquity(chart.JulianDay));
    }

    /// <summary>
    /// A composite carries the true obliquity at the midpoint Julian Day its houses are cast for, not either
    /// parent's: 1800-06-20 12:00 UT and 2026-06-20 12:00 UT at Greenwich meet at JD 2419939.5.
    /// </summary>
    [Fact]
    public void Obliquity_Composite_IsTheTrueObliquityAtTheMidpointJulianDay()
    {
        ChartResult chartA = _engine.Calculate(Greenwich(new DateTime(1800, 6, 20, 12, 0, 0)));
        ChartResult chartB = _engine.Calculate(Greenwich(new DateTime(2026, 6, 20, 12, 0, 0)));
        ILogger logger = new LoggerConfiguration().CreateLogger();
        DignityService dignity = new();
        CompositeService composites = new(
            _fixture.EphemerisService,
            new AspectService(_fixture.EphemerisService, logger),
            new MidpointService(logger),
            dignity,
            new MoonConditionService(),
            new ChartContextService(dignity, new SectService()),
            new FixedStarService(_fixture.EphemerisService, logger),
            logger);

        ChartResult composite = composites.Calculate(chartA, chartB).CompositeChart;

        composite.JulianDay.Should().Be(MidpointJulianDay);
        composite.Obliquity.Should().BeApproximately(MeanObliquity(MidpointJulianDay), NutationTolerance);
        Math.Abs(composite.Obliquity - chartA.Obliquity).Should().BeGreaterThan(0.01);
        Math.Abs(composite.Obliquity - chartB.Obliquity).Should().BeGreaterThan(0.01);
    }

    /// <summary>
    /// The IAU mean obliquity of the ecliptic: Meeus, <i>Astronomical Algorithms</i>, 2nd ed., eq. 22.2,
    /// ε₀ = 23°26′21.448″ − 46.8150″T − 0.00059″T² + 0.001813″T³, T in Julian centuries from J2000.0.
    /// </summary>
    /// <param name="julianDay">The Julian Day.</param>
    /// <returns>The mean obliquity in degrees.</returns>
    private static double MeanObliquity(double julianDay)
    {
        double t = (julianDay - 2451545.0) / 36525.0;
        double arcseconds = ((23.0 * 60.0) + 26.0) * 60.0 + 21.448 - (46.8150 * t) - (0.00059 * t * t) + (0.001813 * t * t * t);
        return arcseconds / 3600.0;
    }

    /// <summary>A chart at Greenwich on UTC's clock, in Placidus.</summary>
    /// <param name="localDateTime">The wall clock, which on UTC's clock is the instant.</param>
    /// <param name="atExplicitOffset">True to label the chart at the explicit offset UTC+0 rather than read it on the zone.</param>
    /// <returns>The input.</returns>
    private static ChartInput Greenwich(DateTime localDateTime, bool atExplicitOffset = false) => new()
    {
        Name = "Greenwich",
        LocalDateTime = localDateTime,
        TimeZone = "UTC",
        Latitude = GreenwichLatitude,
        Longitude = GreenwichLongitude,
        LocationName = "Greenwich",
        UtcOffsetHours = atExplicitOffset ? 0.0 : null,
        UseExplicitOffset = atExplicitOffset,
    };
}
