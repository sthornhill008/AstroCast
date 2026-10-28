using AstroCast.Core.Utilities;

using FluentAssertions;

namespace AstroCast.Tests.Utilities;

/// <summary>
/// Tests for <see cref="AspectDirection"/>: an aspect is applying when its orb is
/// decreasing at the instant, by the rate the two points' speeds give, and
/// separating otherwise.
/// </summary>
/// <remarks>
/// Every expected value is worked by hand from the rule: with Δ the longitude
/// difference wrapped to (−180°, 180°], the orb's rate is
/// <c>sign(Δ) · (speed1 − speed2) · sign(|Δ| − aspectAngle)</c>.
/// </remarks>
[Trait("Category", "Unit")]
public sealed class AspectDirectionTests
{
    [Fact]
    public void OrbRate_Should_BeNegative_When_FasterBodyClosesOnConjunction()
    {
        // Δ = 10 − 12 = −2; separation 2 shrinking at (13 − 1) = 12°/day.
        double rate = AspectDirection.OrbRate(10.0, 13.0, 12.0, 1.0, 0.0);

        rate.Should().Be(-12.0);
        AspectDirection.IsApplying(10.0, 13.0, 12.0, 1.0, 0.0).Should().BeTrue();
    }

    [Fact]
    public void IsApplying_Should_BeFalse_When_FasterBodyHasPassedConjunction()
    {
        // Δ = 14 − 12 = +2; separation 2 growing at 12°/day.
        AspectDirection.OrbRate(14.0, 13.0, 12.0, 1.0, 0.0).Should().Be(12.0);
        AspectDirection.IsApplying(14.0, 13.0, 12.0, 1.0, 0.0).Should().BeFalse();
    }

    [Fact]
    public void IsApplying_Should_BeFalse_When_OrbIsExactlyZero()
    {
        // Separation exactly 60°: the sextile is perfect, its orb cannot decrease.
        AspectDirection.OrbRate(100.0, 13.0, 40.0, 1.0, 60.0).Should().Be(0.0);
        AspectDirection.IsApplying(100.0, 13.0, 40.0, 1.0, 60.0).Should().BeFalse(
            "an orb of exactly 0 is separating");
    }

    [Fact]
    public void IsApplying_Should_BeFalse_When_RateIsExactlyZero()
    {
        // Equal speeds: the separation, and so the orb, does not change.
        AspectDirection.OrbRate(10.0, 1.0, 12.0, 1.0, 0.0).Should().Be(0.0);
        AspectDirection.IsApplying(10.0, 1.0, 12.0, 1.0, 0.0).Should().BeFalse(
            "a rate of exactly 0 is separating");
    }

    [Fact]
    public void IsApplying_Should_BeFalse_When_BothPointsAreStill()
    {
        // A natal point and a chart angle, both held still.
        AspectDirection.IsApplying(10.0, 0.0, 12.0, 0.0, 0.0).Should().BeFalse();
    }

    [Fact]
    public void IsApplying_Should_BeTrue_When_ApproachingAcrossZeroAries()
    {
        // Δ = 359 − 1 = 358, wrapped to −2: the body at 359° moving forward closes on 1°.
        AspectDirection.OrbRate(359.0, 1.0, 1.0, 0.0, 0.0).Should().Be(-1.0);
        AspectDirection.IsApplying(359.0, 1.0, 1.0, 0.0, 0.0).Should().BeTrue();
    }

    [Fact]
    public void IsApplying_Should_BeTrue_When_RetrogradeBodyClosesOnStillPoint()
    {
        // Δ = 12 − 10 = +2; a retrograde body at −0.5°/day closes the separation.
        AspectDirection.OrbRate(12.0, -0.5, 10.0, 0.0, 0.0).Should().Be(-0.5);
        AspectDirection.IsApplying(12.0, -0.5, 10.0, 0.0, 0.0).Should().BeTrue();
    }

    [Fact]
    public void IsApplying_Should_BeTrue_When_SeparationGrowsTowardAspectAngle()
    {
        // Separation 118° growing at 1°/day toward a 120° trine: the orb shrinks.
        AspectDirection.OrbRate(118.0, 1.0, 0.0, 0.0, 120.0).Should().Be(-1.0);
        AspectDirection.IsApplying(118.0, 1.0, 0.0, 0.0, 120.0).Should().BeTrue();
    }

    [Fact]
    public void IsApplying_Should_BeFalse_When_SeparationGrowsPastAspectAngle()
    {
        // Separation 122° growing at 1°/day away from a 120° trine.
        AspectDirection.OrbRate(122.0, 1.0, 0.0, 0.0, 120.0).Should().Be(1.0);
        AspectDirection.IsApplying(122.0, 1.0, 0.0, 0.0, 120.0).Should().BeFalse();
    }

    [Fact]
    public void OrbRate_Should_NotDependOnWhichPointComesFirst()
    {
        // A body at 12° moving 13°/day, ahead of one at 10° moving 1°/day, draws away
        // (Δ = ±2, separation growing at 12°/day) whichever is passed first.
        AspectDirection.OrbRate(10.0, 1.0, 12.0, 13.0, 0.0).Should().Be(12.0);
        AspectDirection.OrbRate(12.0, 13.0, 10.0, 1.0, 0.0).Should().Be(12.0);

        // The same fast body at 8°, behind the slow one, closes on it either way round.
        AspectDirection.OrbRate(10.0, 1.0, 8.0, 13.0, 0.0).Should().Be(-12.0);
        AspectDirection.OrbRate(8.0, 13.0, 10.0, 1.0, 0.0).Should().Be(-12.0);
        AspectDirection.IsApplying(10.0, 1.0, 8.0, 13.0, 0.0).Should().BeTrue();
    }

    [Fact]
    public void IsApplying_Should_BeFalse_When_OppositionIsExact()
    {
        // Δ = 190 − 10 = 180 stays +180 under the (−180°, 180°] wrap; the orb is exactly 0.
        AspectDirection.OrbRate(190.0, 13.0, 10.0, 1.0, 180.0).Should().Be(0.0);
        AspectDirection.IsApplying(190.0, 13.0, 10.0, 1.0, 180.0).Should().BeFalse();
    }

    [Theory]
    [InlineData(double.NaN, 1.0, 10.0, 0.0, 0.0, "longitude1")]
    [InlineData(10.0, double.PositiveInfinity, 10.0, 0.0, 0.0, "speed1")]
    [InlineData(10.0, 1.0, double.NegativeInfinity, 0.0, 0.0, "longitude2")]
    [InlineData(10.0, 1.0, 10.0, double.NaN, 0.0, "speed2")]
    [InlineData(10.0, 1.0, 10.0, 0.0, double.NaN, "aspectAngle")]
    [InlineData(10.0, 1.0, 10.0, 0.0, -1.0, "aspectAngle")]
    [InlineData(10.0, 1.0, 10.0, 0.0, 181.0, "aspectAngle")]
    public void OrbRate_Should_Throw_When_ArgumentIsOutOfRange(
        double longitude1, double speed1, double longitude2, double speed2, double aspectAngle, string paramName)
    {
        Action act = () => AspectDirection.OrbRate(longitude1, speed1, longitude2, speed2, aspectAngle);

        act.Should().Throw<ArgumentOutOfRangeException>().Which.ParamName.Should().Be(paramName);
    }
}
