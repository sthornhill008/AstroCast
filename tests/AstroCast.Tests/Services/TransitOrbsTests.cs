using AstroCast.Core.Enums;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="TransitOrbs"/> verifying major orbs, minor orbs,
/// and constant values for the transit orb system.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransitOrbsTests
{
    #region Major Aspect Orbs — Jupiter/Saturn

    [Fact]
    public void GetOrb_Jupiter_Conjunction_Should_Return_2_0()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Jupiter, 0.0, false);
        orb.Should().Be(2.0);
    }

    [Fact]
    public void GetOrb_Jupiter_Trine_Should_Return_1_5()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Jupiter, 120.0, false);
        orb.Should().Be(1.5);
    }

    [Fact]
    public void GetOrb_Saturn_Opposition_Should_Return_2_0()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Saturn, 180.0, false);
        orb.Should().Be(2.0);
    }

    [Fact]
    public void GetOrb_Saturn_Square_Should_Return_1_5()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Saturn, 90.0, false);
        orb.Should().Be(1.5);
    }

    #endregion

    #region Major Aspect Orbs — Outer Planets

    [Fact]
    public void GetOrb_Uranus_Conjunction_Should_Return_1_5()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Uranus, 0.0, false);
        orb.Should().Be(1.5);
    }

    [Fact]
    public void GetOrb_Uranus_Sextile_Should_Return_1_0()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Uranus, 60.0, false);
        orb.Should().Be(1.0);
    }

    [Fact]
    public void GetOrb_Neptune_Conjunction_Should_Return_1_5()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Neptune, 0.0, false);
        orb.Should().Be(1.5);
    }

    [Fact]
    public void GetOrb_Pluto_Trine_Should_Return_1_0()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Pluto, 120.0, false);
        orb.Should().Be(1.0);
    }

    [Fact]
    public void GetOrb_Chiron_Opposition_Should_Return_1_5()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Chiron, 180.0, false);
        orb.Should().Be(1.5);
    }

    [Fact]
    public void GetOrb_NorthNode_Square_Should_Return_1_0()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.NorthNode, 90.0, false);
        orb.Should().Be(1.0);
    }

    [Fact]
    public void GetOrb_Mars_Conjunction_Should_Return_1_5()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Mars, 0.0, false);
        orb.Should().Be(1.5);
    }

    #endregion

    #region Minor Aspect Orbs

    [Fact]
    public void GetOrb_Jupiter_MinorAspect_Should_Return_1_0()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Jupiter, 45.0, true);
        orb.Should().Be(1.0);
    }

    [Fact]
    public void GetOrb_Saturn_MinorAspect_Should_Return_1_0()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Saturn, 135.0, true);
        orb.Should().Be(1.0);
    }

    [Fact]
    public void GetOrb_Uranus_MinorAspect_Should_Return_0_75()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Uranus, 45.0, true);
        orb.Should().Be(0.75);
    }

    [Fact]
    public void GetOrb_Pluto_MinorAspect_Should_Return_0_75()
    {
        double orb = TransitOrbs.GetOrb(TransitBody.Pluto, 150.0, true);
        orb.Should().Be(0.75);
    }

    #endregion

    #region Constants

    [Fact]
    public void MidpointOrb_Should_Be_10_ArcMinutes()
    {
        Math.Abs(TransitOrbs.MidpointOrb - (10.0 / 60.0)).Should().BeLessThan(0.0001);
    }

    [Fact]
    public void MarsSlowMotionThreshold_Should_Be_0_3()
    {
        TransitOrbs.MarsSlowMotionThreshold.Should().Be(0.3);
    }

    #endregion
}
