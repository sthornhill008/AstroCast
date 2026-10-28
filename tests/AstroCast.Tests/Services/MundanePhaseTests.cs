using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using AstroCast.Tests.Fixtures;

using FluentAssertions;

using Serilog;

namespace AstroCast.Tests.Services;

/// <summary>
/// Pins waxing and waning (finding T5-1, branch B): a cycle's phase is measured from the slower body
/// to the faster, phase = faster − slower wrapped to 0–360°, waxing up to 180° and waning beyond, and
/// the Cycle° column prints that phase, whichever body is named first. Until branch B the phase was
/// measured from the first body named to the second, so every square, semi-square and
/// sesquiquadrate carried the opposite word when the faster body was named first.
/// </summary>
/// <remarks>
/// Every expected instant and phase is Tier B (direct <c>swe_calc_ut</c>): the instant of each
/// aspect found by root-finding on the pair's longitude difference, and the faster body's longitude
/// minus the slower's there. Faster means the shorter mean sidereal period (Tier A).
/// </remarks>
[Collection("Ephemeris")]
public sealed class MundanePhaseTests
{
    private readonly EphemerisFixture _fixture;
    private readonly ILogger _logger = new LoggerConfiguration().CreateLogger();

    /// <summary>
    /// Receives the shared ephemeris fixture, which has already set the ephemeris path.
    /// </summary>
    /// <param name="fixture">xUnit-injected shared ephemeris fixture.</param>
    public MundanePhaseTests(EphemerisFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// Mars minus Uranus at 2026-12-04 19:28:16.360 UT is +90.0000° (Tier B): Mars, the faster body,
    /// is 90° ahead after their conjunction of 2026-07-04, the opening square. It is "Waxing Square"
    /// at 90°, with Mars named first and with Uranus named first. Until branch B, Mars first gave
    /// "Waning Square" at 270°.
    /// </summary>
    /// <param name="body1">The body named first.</param>
    /// <param name="body2">The body named second.</param>
    [Theory]
    [Trait("Category", "Integration")]
    [InlineData(CelestialBodyId.Mars, CelestialBodyId.Uranus)]
    [InlineData(CelestialBodyId.Uranus, CelestialBodyId.Mars)]
    public void MarsSquareUranus20261204_IsWaxingAt90_WhicheverIsNamedFirst(CelestialBodyId body1, CelestialBodyId body2)
    {
        MundaneAspectSeries series = Service().FindAspects(body1, body2, "90° Harmonic", 2026, 2026);

        MundaneAspectResult square = series.Aspects.Should().ContainSingle(a =>
            Near(a.ExactUtc, Utc(2026, 12, 4, 19, 28, 16.360))).Subject;
        square.PhaseLabel.Should().Be("Waxing");
        square.AspectName.Should().Be("Waxing Square");
        square.CyclePhase.Should().BeApproximately(90.0, 1e-4);
    }

    /// <summary>
    /// Guard: with the slower body named first the measure was already right and is unchanged.
    /// Saturn then Jupiter, 2025–2035: Jupiter minus Saturn is +90° at 2025-06-15 14:36:00.387 UT
    /// (Waxing Square) and +270° at 2035-04-25 19:35:35.750 UT (Waning Square), Tier B.
    /// </summary>
    [Fact]
    [Trait("Category", "Integration")]
    public void SaturnJupiter_SlowerNamedFirst_IsUnchanged()
    {
        MundaneAspectSeries series = Service().FindAspects(CelestialBodyId.Saturn, CelestialBodyId.Jupiter, "90° Harmonic", 2025, 2035);

        MundaneAspectResult opening = series.Aspects.Should().ContainSingle(a =>
            Near(a.ExactUtc, Utc(2025, 6, 15, 14, 36, 0.387))).Subject;
        opening.AspectName.Should().Be("Waxing Square");
        opening.CyclePhase.Should().BeApproximately(90.0, 1e-4);

        MundaneAspectResult closing = series.Aspects.Should().ContainSingle(a =>
            Near(a.ExactUtc, Utc(2035, 4, 25, 19, 35, 35.750))).Subject;
        closing.AspectName.Should().Be("Waning Square");
        closing.CyclePhase.Should().BeApproximately(270.0, 1e-4);
    }

    #region Helpers

    private MundaneAspectService Service() => new(_fixture.EphemerisService, _logger);

    private static bool Near(DateTime actual, DateTime expected) =>
        Math.Abs((actual - expected).TotalSeconds) <= 1.0;

    private static DateTime Utc(int year, int month, int day, int hour, int minute, double second) =>
        new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc).AddSeconds(second);

    #endregion
}
