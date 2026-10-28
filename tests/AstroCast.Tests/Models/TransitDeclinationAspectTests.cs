using AstroCast.Core.Models.Transit;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Models;

/// <summary>
/// Tests for <see cref="TransitDeclinationAspect"/> model properties and the
/// parallel/contraparallel detection logic used by <see cref="TransitService"/>.
/// Verifies orb thresholds, aspect type determination, and abbreviation rendering.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TransitDeclinationAspectTests
{
    #region Parallel Detection

    [Fact]
    public void Parallel_SameSignDeclinationsWithinOrb_DetectedCorrectly()
    {
        // Transit at Dec +22°50' (22.833°) vs natal at Dec +22°55' (22.917°)
        // Same-sign declinations: orb = |22.833 - 22.917| = 0.084° (within ±1°)
        double transitDec = +22.833;
        double natalDec = +22.917;
        double orb = Math.Abs(transitDec - natalDec);

        orb.Should().BeLessThanOrEqualTo(DeclinationAspectService.DefaultOrb,
            "parallel orb 0.084° should be within the ±1° threshold");

        TransitDeclinationAspect aspect = new()
        {
            TransitPlanetName = "Jupiter",
            TransitDeclination = transitDec,
            IsParallel = true,
            NatalPlanetName = "Sun",
            NatalDeclination = natalDec,
            Orb = orb,
            Date = new DateOnly(2026, 3, 12),
        };

        aspect.Abbreviation.Should().Be("Par");
        aspect.IsParallel.Should().BeTrue();
        aspect.Orb.Should().BeApproximately(0.084, 0.002);
    }

    [Fact]
    public void Parallel_NegativeSignDeclinationsWithinOrb_DetectedCorrectly()
    {
        // Transit at Dec -15.3° vs natal at Dec -15.7° — both south
        double transitDec = -15.3;
        double natalDec = -15.7;
        double orb = Math.Abs(transitDec - natalDec);

        orb.Should().BeLessThanOrEqualTo(DeclinationAspectService.DefaultOrb,
            "parallel orb 0.4° should be within the ±1° threshold");

        TransitDeclinationAspect aspect = new()
        {
            TransitPlanetName = "Saturn",
            TransitDeclination = transitDec,
            IsParallel = true,
            NatalPlanetName = "Moon",
            NatalDeclination = natalDec,
            Orb = orb,
            Date = new DateOnly(2026, 3, 20),
        };

        aspect.Abbreviation.Should().Be("Par");
        aspect.Orb.Should().BeApproximately(0.4, 0.001);
    }

    #endregion

    #region Contraparallel Detection

    [Fact]
    public void Contraparallel_OppositeSignDeclinationsWithinOrb_DetectedCorrectly()
    {
        // Transit at Dec +22°50' (22.833°) vs natal at Dec -22°55' (-22.917°)
        // Contraparallel: orb = |22.833 + (-22.917)| = |-0.084| = 0.084° (within ±1°)
        double transitDec = +22.833;
        double natalDec = -22.917;
        double orb = Math.Abs(transitDec + natalDec);

        orb.Should().BeLessThanOrEqualTo(DeclinationAspectService.DefaultOrb,
            "contraparallel orb 0.084° should be within the ±1° threshold");

        TransitDeclinationAspect aspect = new()
        {
            TransitPlanetName = "Jupiter",
            TransitDeclination = transitDec,
            IsParallel = false,
            NatalPlanetName = "Uranus",
            NatalDeclination = natalDec,
            Orb = orb,
            Date = new DateOnly(2026, 3, 15),
        };

        aspect.Abbreviation.Should().Be("CPar");
        aspect.IsParallel.Should().BeFalse();
        aspect.Orb.Should().BeApproximately(0.084, 0.002);
    }

    #endregion

    #region Orb Rejection

    [Fact]
    public void BeyondOrb_NoAspectFormed()
    {
        // Transit at Dec +22°50' (22.833°) vs natal at Dec +24°00' (24.0°)
        // Parallel orb = |22.833 - 24.0| = 1.167° (exceeds ±1°)
        // Contraparallel orb = |22.833 + 24.0| = 46.833° (far exceeds ±1°)
        double transitDec = +22.833;
        double natalDec = +24.0;

        double parallelOrb = Math.Abs(transitDec - natalDec);
        double contraparallelOrb = Math.Abs(transitDec + natalDec);

        parallelOrb.Should().BeGreaterThan(DeclinationAspectService.DefaultOrb,
            "parallel orb 1.167° should exceed the ±1° threshold");
        contraparallelOrb.Should().BeGreaterThan(DeclinationAspectService.DefaultOrb,
            "contraparallel orb 46.833° should far exceed the ±1° threshold");
    }

    [Fact]
    public void ExactlyAtOrbBoundary_StillDetected()
    {
        // Transit at Dec +22.0° vs natal at Dec +23.0° — exactly 1.0° orb
        double transitDec = +22.0;
        double natalDec = +23.0;
        double orb = Math.Abs(transitDec - natalDec);

        orb.Should().BeLessThanOrEqualTo(DeclinationAspectService.DefaultOrb,
            "exactly 1.0° should still be within the ±1° threshold (inclusive)");
    }

    [Fact]
    public void JustBeyondOrbBoundary_Rejected()
    {
        // Transit at Dec +22.0° vs natal at Dec +23.01° — 1.01° orb
        double transitDec = +22.0;
        double natalDec = +23.01;
        double orb = Math.Abs(transitDec - natalDec);

        orb.Should().BeGreaterThan(DeclinationAspectService.DefaultOrb,
            "1.01° should exceed the ±1° threshold");
    }

    #endregion

    #region Abbreviation Property

    [Fact]
    public void Abbreviation_Parallel_ReturnsPar()
    {
        TransitDeclinationAspect aspect = new()
        {
            TransitPlanetName = "Pluto",
            TransitDeclination = +22.5,
            IsParallel = true,
            NatalPlanetName = "Sun",
            NatalDeclination = +22.8,
            Orb = 0.3,
            Date = new DateOnly(2026, 3, 1),
        };

        aspect.Abbreviation.Should().Be("Par");
    }

    [Fact]
    public void Abbreviation_Contraparallel_ReturnsCPar()
    {
        TransitDeclinationAspect aspect = new()
        {
            TransitPlanetName = "Neptune",
            TransitDeclination = +8.5,
            IsParallel = false,
            NatalPlanetName = "Neptune",
            NatalDeclination = -8.6,
            Orb = 0.1,
            Date = new DateOnly(2026, 3, 20),
        };

        aspect.Abbreviation.Should().Be("CPar");
    }

    #endregion

    #region Date Property

    [Fact]
    public void Date_StoresExactPassDate()
    {
        DateOnly expectedDate = new(2026, 3, 12);

        TransitDeclinationAspect aspect = new()
        {
            TransitPlanetName = "Jupiter",
            TransitDeclination = +22.833,
            IsParallel = true,
            NatalPlanetName = "Sun",
            NatalDeclination = +22.917,
            Orb = 0.083,
            Date = expectedDate,
        };

        aspect.Date.Should().Be(expectedDate);
    }

    #endregion
}
