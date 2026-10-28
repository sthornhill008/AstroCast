using AstroCast.Core.Configuration;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Integration tests for <see cref="ParanService"/> verifying angular crossing
/// detection and paran simultaneity matching against the Swiss Ephemeris.
/// Uses the shared "Ephemeris" collection fixture for real ephemeris calculations.
/// </summary>
[Collection("Ephemeris")]
[Trait("Category", "Integration")]
public sealed class ParanServiceTests
{
    /// <summary>London latitude (north positive).</summary>
    private const double LondonLat = 51.5074;

    /// <summary>London longitude (east positive; west of Greenwich = negative).</summary>
    private const double LondonLon = -0.1278;

    private readonly ParanService _service;
    private readonly EphemerisService _ephemeris;

    /// <summary>
    /// Receives the shared ephemeris fixture and builds a ParanService.
    /// </summary>
    public ParanServiceTests(EphemerisFixture fixture)
    {
        _ephemeris = fixture.EphemerisService;
        ILogger logger = new LoggerConfiguration().CreateLogger();
        _service = new ParanService(_ephemeris, logger);
    }

    /// <summary>
    /// Returns the Julian Day for 2026-06-15 at noon UT (mid-latitude summer date).
    /// </summary>
    private double TestJulianDay => _ephemeris.DateToJulianDay(2026, 6, 15, 12.0);

    #region Phase 1 — Angular Crossings

    [Fact]
    public void Calculate_Should_ReturnExpectedCrossingCount_When_AllBodiesRiseAndSet()
    {
        // Arrange — London in summer, all bodies should rise/set
        double jd = TestJulianDay;

        // Act
        ParanResult result = _service.Calculate(jd, LondonLat, LondonLon);

        // Assert — 12 bodies x 4 angles = 48 expected crossings
        // Some may be missing due to circumpolar conditions at 51.5°N
        result.Crossings.Should().HaveCountGreaterThanOrEqualTo(40,
            "at London latitude in summer, most of the 12 bodies should produce 4 crossings each");
    }

    #endregion

    #region Phase 2 — Paran Detection

    [Fact]
    public void Calculate_Should_DetectAtLeastOneParan_When_DefaultOrb()
    {
        // Arrange
        double jd = TestJulianDay;

        // Act
        ParanResult result = _service.Calculate(jd, LondonLat, LondonLon);

        // Assert — with 40+ crossings and a 4-minute orb, at least one paran is statistically very likely
        result.Parans.Should().NotBeEmpty(
            "with 12 bodies and a 4-minute orb, at least one paran should be detected");
    }

    [Fact]
    public void Calculate_Should_ReturnFewerParans_When_TightOrb()
    {
        // Arrange
        double jd = TestJulianDay;
        ParanOptions tightOrb = new() { OrbMinutes = 0.01 };
        ParanOptions defaultOrb = ParanOptions.Default;

        // Act
        ParanResult tightResult = _service.Calculate(jd, LondonLat, LondonLon, tightOrb);
        ParanResult defaultResult = _service.Calculate(jd, LondonLat, LondonLon, defaultOrb);

        // Assert
        tightResult.Parans.Count.Should().BeLessThanOrEqualTo(defaultResult.Parans.Count,
            "a tighter orb (0.01 min) should find fewer or equal parans than the default (4 min)");
    }

    [Fact]
    public void Calculate_Should_ReturnMoreOrEqualParans_When_WideOrb()
    {
        // Arrange
        double jd = TestJulianDay;
        ParanOptions wideOrb = new() { OrbMinutes = 8.0 };
        ParanOptions defaultOrb = ParanOptions.Default;

        // Act
        ParanResult wideResult = _service.Calculate(jd, LondonLat, LondonLon, wideOrb);
        ParanResult defaultResult = _service.Calculate(jd, LondonLat, LondonLon, defaultOrb);

        // Assert
        wideResult.Parans.Count.Should().BeGreaterThanOrEqualTo(defaultResult.Parans.Count,
            "an 8-minute orb should find at least as many parans as the default 4-minute orb");
    }

    #endregion

    #region ExcludeSameAngle

    [Fact]
    public void Calculate_Should_ReturnMoreOrEqualParans_When_SameAngleNotExcluded()
    {
        // Arrange
        double jd = TestJulianDay;
        ParanOptions includeSame = new() { ExcludeSameAngle = false };
        ParanOptions excludeSame = ParanOptions.Default; // ExcludeSameAngle = true

        // Act
        ParanResult includeResult = _service.Calculate(jd, LondonLat, LondonLon, includeSame);
        ParanResult excludeResult = _service.Calculate(jd, LondonLat, LondonLon, excludeSame);

        // Assert
        includeResult.Parans.Count.Should().BeGreaterThanOrEqualTo(excludeResult.Parans.Count,
            "including same-angle parans should find at least as many parans as excluding them");
    }

    #endregion

    #region Circumpolar Handling

    [Fact]
    public void Calculate_Should_NotThrow_When_CircumpolarLatitude()
    {
        // Arrange — Troms\u00f8, Norway (69.6°N) in summer
        double jd = TestJulianDay;
        const double tromsoLat = 69.6;
        const double tromsoLon = 19.0;

        // Act
        ParanResult result = _service.Calculate(jd, tromsoLat, tromsoLon);

        // Assert — should not throw
        result.Crossings.Should().NotBeNull("the service should handle circumpolar conditions gracefully");
        result.Crossings.Count.Should().BeLessThan(48,
            "at 69.6\u00b0N in summer, some bodies will not rise/set (circumpolar)");

        // MC/IC crossings should still occur for all 12 bodies (24 minimum)
        int mcIcCount = result.Crossings.Count(c =>
            c.Angle == AngleType.Culminating || c.Angle == AngleType.AntiCulminating);
        mcIcCount.Should().BeGreaterThanOrEqualTo(20,
            "MC/IC crossings always occur regardless of latitude; expect at least 20 of 24");
    }

    #endregion

    #region Body Subset

    [Fact]
    public void Calculate_Should_OnlyIncludeSpecifiedBodies_When_SubsetConfigured()
    {
        // Arrange
        double jd = TestJulianDay;
        ParanOptions sunMoonOnly = new()
        {
            Bodies = [CelestialBodyId.Sun, CelestialBodyId.Moon],
        };

        // Act
        ParanResult result = _service.Calculate(jd, LondonLat, LondonLon, sunMoonOnly);

        // Assert — 2 bodies x 4 angles = up to 8 crossings
        result.Crossings.Should().HaveCountLessThanOrEqualTo(8,
            "only Sun and Moon should produce crossings");
        result.Crossings.Should().OnlyContain(c =>
            c.Body == CelestialBodyId.Sun || c.Body == CelestialBodyId.Moon,
            "crossings should only involve Sun and Moon");

        // Any parans should only involve Sun and Moon
        result.Parans.Should().OnlyContain(p =>
            (p.BodyA == CelestialBodyId.Sun || p.BodyA == CelestialBodyId.Moon) &&
            (p.BodyB == CelestialBodyId.Sun || p.BodyB == CelestialBodyId.Moon),
            "parans should only involve the configured bodies");
    }

    #endregion

    #region Deduplication

    [Fact]
    public void Calculate_Should_NotContainSelfParans()
    {
        // Arrange
        double jd = TestJulianDay;

        // Act
        ParanResult result = _service.Calculate(jd, LondonLat, LondonLon);

        // Assert — no paran should have the same body on both sides
        result.Parans.Should().NotContain(p =>
            p.BodyA == p.BodyB && p.BodyAName == p.BodyBName,
            "a body cannot form a paran with itself");
    }

    #endregion

    #region Sort Order

    [Fact]
    public void Calculate_Should_SortParansByOrbAscending()
    {
        // Arrange
        double jd = TestJulianDay;

        // Act
        ParanResult result = _service.Calculate(jd, LondonLat, LondonLon);

        // Assert
        if (result.Parans.Count > 1)
        {
            for (int i = 1; i < result.Parans.Count; i++)
            {
                result.Parans[i].OrbMinutes.Should().BeGreaterThanOrEqualTo(
                    result.Parans[i - 1].OrbMinutes,
                    $"paran at index {i} should have orb >= paran at index {i - 1} (tightest first)");
            }
        }
    }

    [Fact]
    public void Calculate_Should_SortCrossingsChronologically()
    {
        // Arrange
        double jd = TestJulianDay;

        // Act
        ParanResult result = _service.Calculate(jd, LondonLat, LondonLon);

        // Assert
        if (result.Crossings.Count > 1)
        {
            for (int i = 1; i < result.Crossings.Count; i++)
            {
                result.Crossings[i].JulianDayUt.Should().BeGreaterThanOrEqualTo(
                    result.Crossings[i - 1].JulianDayUt,
                    $"crossing at index {i} should be chronologically after index {i - 1}");
            }
        }
    }

    #endregion
}
