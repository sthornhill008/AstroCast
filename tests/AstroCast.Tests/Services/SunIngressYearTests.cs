using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;
using AstroCast.Tests.Formatting.Golden;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins the Sun's ingress of a year (finding T4-1, branch A): the Sun's ingress into a sign "for
/// year Y" is its one crossing of the sign's first degree in the UT calendar year Y, from 1 January
/// 00:00 (inclusive) to 1 January 00:00 of Y+1 (exclusive). Until branch A the search ran from
/// 1 July of Y-1 to 1 July of Y+1 and took the first crossing, so any sign entered after 1 July,
/// Libra and Capricorn of the cardinal set among them, came from the year before.
/// </summary>
/// <remarks>
/// Every expected instant is Tier B: the Sun's crossings of k × 30° found by direct
/// <c>swe_calc_ut</c> root-finding (a 0.05-day grid refined to 1e-9 day, the timing investigation's
/// truth library). Tier C (Skyfield, DE440) agrees with the cardinal four to 0.017 s. The app prints
/// an ingress to the second, so each is held to within one second.
/// </remarks>
[Collection("Ephemeris")]
public sealed class SunIngressYearTests
{
    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public SunIngressYearTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// The cardinal ingresses of 2026 are four instants in 2026: Aries 2026-03-20 14:45:58, Cancer
    /// 2026-06-21 08:24:31, Libra 2026-09-23 00:05:14 and Capricorn 2026-12-21 20:50:15 UT. Until
    /// branch A Libra and Capricorn were those of 2025 (2025-09-22 18:19:20, 2025-12-21 15:03:05).
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void CardinalIngresses2026_AreTheFourOf2026()
    {
        IReadOnlyList<IngressResult> cardinal = Service().CalculateCardinalIngresses(
            2026, 38.8977, -77.0365, "Washington, District of Columbia", "America/New_York");

        cardinal.Select(i => i.Sign).Should().Equal("Aries", "Cancer", "Libra", "Capricorn");
        cardinal[0].ExactUtc.Should().BeCloseTo(Utc(2026, 3, 20, 14, 45, 57.751), TimeSpan.FromSeconds(1));
        cardinal[1].ExactUtc.Should().BeCloseTo(Utc(2026, 6, 21, 8, 24, 30.695), TimeSpan.FromSeconds(1));
        cardinal[2].ExactUtc.Should().BeCloseTo(Utc(2026, 9, 23, 0, 5, 13.524), TimeSpan.FromSeconds(1));
        cardinal[3].ExactUtc.Should().BeCloseTo(Utc(2026, 12, 21, 20, 50, 14.551), TimeSpan.FromSeconds(1));
        cardinal.Should().OnlyContain(i => i.ExactUtc.Year == 2026 && i.Chart != null && i.Chart.UtcDateTime.Year == 2026);
    }

    /// <summary>
    /// The Sun's ingress into each of the twelve signs for 2026 is that sign's crossing in 2026
    /// (Tier B, UT). Until branch A the six signs entered after 1 July (Leo to Capricorn) gave 2025.
    /// </summary>
    /// <param name="sign">The sign entered.</param>
    /// <param name="month">Tier B month.</param>
    /// <param name="day">Tier B day.</param>
    /// <param name="hour">Tier B hour.</param>
    /// <param name="minute">Tier B minute.</param>
    /// <param name="second">Tier B second, with its fraction.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(ZodiacSign.Aries, 3, 20, 14, 45, 57.751)]
    [InlineData(ZodiacSign.Taurus, 4, 20, 1, 39, 6.801)]
    [InlineData(ZodiacSign.Gemini, 5, 21, 0, 36, 44.624)]
    [InlineData(ZodiacSign.Cancer, 6, 21, 8, 24, 30.695)]
    [InlineData(ZodiacSign.Leo, 7, 22, 19, 13, 5.535)]
    [InlineData(ZodiacSign.Virgo, 8, 23, 2, 18, 48.769)]
    [InlineData(ZodiacSign.Libra, 9, 23, 0, 5, 13.524)]
    [InlineData(ZodiacSign.Scorpio, 10, 23, 9, 37, 56.662)]
    [InlineData(ZodiacSign.Sagittarius, 11, 22, 7, 23, 20.928)]
    [InlineData(ZodiacSign.Capricorn, 12, 21, 20, 50, 14.551)]
    [InlineData(ZodiacSign.Aquarius, 1, 20, 1, 44, 56.360)]
    [InlineData(ZodiacSign.Pisces, 2, 18, 15, 51, 55.798)]
    public void SunIngress2026_IntoEachSign_IsThatSignsCrossingIn2026(
        ZodiacSign sign, int month, int day, int hour, int minute, double second)
    {
        IngressResult ingress = Service().CalculateIngress(
            CelestialBodyId.Sun, sign, 2026, 51.4779, -0.0015, "Greenwich", "Europe/London");

        ingress.ExactUtc.Should().BeCloseTo(Utc(2026, month, day, hour, minute, second), TimeSpan.FromSeconds(1));
    }

    /// <summary>
    /// Guard: a single ingress of Mars to Pluto keeps the window of a year either side and the last
    /// crossing, by design (card T-D6, branch F). Saturn into Aries "for 2025" crosses 0° on
    /// 2025-05-25, back on 2025-09-01 and for good on 2026-02-14 00:11:39 UT (Tier B): the last.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void SaturnIngressIntoAries2025_IsStillTheLastCrossing()
    {
        IngressResult ingress = Service().CalculateIngress(
            CelestialBodyId.Saturn, ZodiacSign.Aries, 2025, 51.4779, -0.0015, "Greenwich", "Europe/London");

        ingress.ExactUtc.Should().BeCloseTo(Utc(2026, 2, 14, 0, 11, 39.074), TimeSpan.FromSeconds(1));
    }

    #region Helpers

    private IngressService Service() =>
        new(_fixture.EphemerisService, GoldenFixtures.CreateChartEngine(_fixture), _logger);

    private static DateTime Utc(int year, int month, int day, int hour, int minute, double second) =>
        new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc).AddSeconds(second);

    #endregion
}
