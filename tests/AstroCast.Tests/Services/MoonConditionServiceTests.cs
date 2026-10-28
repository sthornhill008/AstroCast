using FluentAssertions;

using Serilog;

using AstroCast.Core.Enums;
using AstroCast.Core.Interfaces;
using AstroCast.Core.Models;
using AstroCast.Core.Services;
using AstroCast.Tests.Fixtures;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="MoonConditionService"/> — Moon condition analysis
/// including phase, Void of Course, first applying aspect, and Via Combusta.
/// All tests are pure unit tests that require no Swiss Ephemeris or other
/// external resources, except the integration test class at the end.
/// </summary>
public sealed class MoonConditionServiceTests
{
    private readonly MoonConditionService _service = new();

    #region Phase Calculation Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ReturnNewMoon_When_PhaseAngle10()
    {
        // Sun at 0°, Moon at 10° → phase angle = 10° → New Moon
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 10.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.Phase.Should().Be(MoonPhaseType.NewMoon);
        result.PhaseLabel.Should().Be("New Moon");
        result.PhaseAngle.Should().BeApproximately(10.0, 0.001);
        result.IsWaxing.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ReturnWaxingCrescent_When_PhaseAngle60()
    {
        // Sun at 0°, Moon at 60° → Waxing Crescent
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 60.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.Phase.Should().Be(MoonPhaseType.WaxingCrescent);
        result.PhaseLabel.Should().Be("Waxing Crescent");
        result.IsWaxing.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ReturnFirstQuarter_When_PhaseAngle90()
    {
        // Sun at 0°, Moon at 90° → First Quarter
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 90.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.Phase.Should().Be(MoonPhaseType.FirstQuarter);
        result.PhaseLabel.Should().Be("First Quarter");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ReturnWaxingGibbous_When_PhaseAngle150()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 150.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.Phase.Should().Be(MoonPhaseType.WaxingGibbous);
        result.IsWaxing.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ReturnFullMoon_When_PhaseAngle180()
    {
        // Sun at 0°, Moon at 180° → Full Moon (phase angle = 180°)
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 180.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.Phase.Should().Be(MoonPhaseType.FullMoon);
        result.PhaseLabel.Should().Be("Full Moon");
        // Phase angle 180° is the boundary — waning starts
        result.IsWaxing.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ReturnWaningGibbous_When_PhaseAngle250()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 250.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.Phase.Should().Be(MoonPhaseType.WaningGibbous);
        result.IsWaxing.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ReturnLastQuarter_When_PhaseAngle270()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 270.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.Phase.Should().Be(MoonPhaseType.LastQuarter);
        result.PhaseLabel.Should().Be("Last Quarter");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ReturnWaningCrescent_When_PhaseAngle330()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 330.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.Phase.Should().Be(MoonPhaseType.WaningCrescent);
        result.PhaseLabel.Should().Be("Waning Crescent");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_HandleWraparound_When_MoonBehindSun()
    {
        // Sun at 350°, Moon at 10° → phase angle = (10 - 350 + 360) % 360 = 20°
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 350.0, moonLong: 10.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.PhaseAngle.Should().BeApproximately(20.0, 0.001);
        result.Phase.Should().Be(MoonPhaseType.NewMoon);
    }

    #endregion

    #region Phase Position Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnEarly_When_NewMoonStart()
    {
        // 0° = start of New Moon phase → early
        MoonConditionService.GetPhasePosition(0.0).Should().Be("early");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnEarly_When_NewMoon14()
    {
        // 14° = last degree of early within New Moon
        MoonConditionService.GetPhasePosition(14.0).Should().Be("early");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnMid_When_NewMoon15()
    {
        // 15° = boundary early → mid within New Moon
        MoonConditionService.GetPhasePosition(15.0).Should().Be("mid");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnMid_When_NewMoon29()
    {
        // 29° = last degree of mid within New Moon
        MoonConditionService.GetPhasePosition(29.0).Should().Be("mid");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnLate_When_NewMoon30()
    {
        // 30° = boundary mid → late within New Moon
        MoonConditionService.GetPhasePosition(30.0).Should().Be("late");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnLate_When_NewMoon44()
    {
        // 44.9° = end of New Moon phase → late
        MoonConditionService.GetPhasePosition(44.9).Should().Be("late");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnEarly_When_FirstQuarterStart()
    {
        // 90° = start of First Quarter → early (crisis peak)
        MoonConditionService.GetPhasePosition(90.0).Should().Be("early");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnMid_When_FirstQuarter110()
    {
        // 110° = past the crisis; building and committing → mid
        MoonConditionService.GetPhasePosition(110.0).Should().Be("mid");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnLate_When_FirstQuarter130()
    {
        // 130° = transitioning toward Gibbous refinement → late
        MoonConditionService.GetPhasePosition(130.0).Should().Be("late");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnEarly_When_FullMoonStart()
    {
        // 180° = Full Moon peak illumination → early
        MoonConditionService.GetPhasePosition(180.0).Should().Be("early");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnLate_When_FullMoon218()
    {
        // 218° = past peak, proto-Disseminating → late
        MoonConditionService.GetPhasePosition(218.0).Should().Be("late");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnEarly_When_LastQuarterStart()
    {
        // 270° = crisis in consciousness peak → early
        MoonConditionService.GetPhasePosition(270.0).Should().Be("early");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnLate_When_LastQuarter308()
    {
        // 308° = releasing toward Balsamic → late
        MoonConditionService.GetPhasePosition(308.0).Should().Be("late");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnMid_When_Balsamic335()
    {
        // 335° = 20° into Balsamic phase (315°-360°) → mid
        MoonConditionService.GetPhasePosition(335.0).Should().Be("mid");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetPhasePosition_Should_ReturnLate_When_Balsamic359()
    {
        // 359° = end of cycle → late
        MoonConditionService.GetPhasePosition(359.0).Should().Be("late");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_PopulatePhasePosition()
    {
        // Sun at 0°, Moon at 110° → phase angle 110° → First Quarter, mid-phase
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 110.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.PhasePosition.Should().Be("mid");
    }

    #endregion

    #region Via Combusta Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_DetectViaCombusta_When_MoonAt200()
    {
        // Moon at 200° is 20° Libra → inside Via Combusta (195°-225°)
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 200.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsViaCombusta.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_DetectViaCombusta_When_MoonAt195Exactly()
    {
        // Moon at 195° = 15° Libra exactly → start of Via Combusta (inclusive)
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 195.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsViaCombusta.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_NotDetectViaCombusta_When_MoonAt225Exactly()
    {
        // Moon at 225° = 15° Scorpio exactly → end boundary (exclusive)
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 225.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsViaCombusta.Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_NotDetectViaCombusta_When_MoonAt100()
    {
        // Moon at 100° = 10° Cancer → outside Via Combusta
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 100.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsViaCombusta.Should().BeFalse();
    }

    #endregion

    #region Void of Course Tests — Forward Geometric Scan

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_BeVoidOfCourse_When_NoMajorAspectPerfectsBeforeSignEnd()
    {
        // Moon at 25° Aries (25.0°), 5° remaining before Taurus.
        // Sun at 100° Cancer — no major aspect candidate falls in [25°, 30°).
        //   Conjunction (0°): candidate at 100° → forward 75° > 5° → skip
        //   Sextile (60°): candidates at 160°, 40° → forward 135°, 15° → both > 5° → skip
        //   Square (90°): candidates at 190°, 10° → forward 165°, 345° → skip
        //   Trine (120°): candidates at 220°, 340° → skip
        //   Opposition (180°): candidate at 280° → skip
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 100.0, moonLong: 25.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeTrue();
        result.FirstApplyingPlanet.Should().BeNull();
        result.FirstApplyingAspectType.Should().BeNull();
        result.FirstApplyingOrb.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_NotBeVoid_When_ConjunctionPerfectsWithinSign()
    {
        // Moon at 20° Aries (20.0°), 10° remaining.
        // Mars at 27° Aries (27.0°) — conjunction candidate at 27.0°.
        // Forward distance = 7.0° < 10.0° → NOT VoC.
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 100.0, moonLong: 20.0,
            ("Mars", 27.0));
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeFalse();
        result.FirstApplyingPlanet.Should().Be("Mars");
        result.FirstApplyingAspectType.Should().Be(AspectType.Conjunction);
        result.FirstApplyingOrb.Should().BeApproximately(7.0, 0.001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_BeVoid_When_AspectCandidatePastSignBoundary()
    {
        // Moon at 28° Aries (28.0°), only 2° remaining (sign ends at 30°).
        // Jupiter at 152° Leo — trine (120°) candidates at 272° and 32°.
        //   272° → forward = 244° > 2° → skip
        //   32° → forward = 4° > 2° → skip (past Aries boundary)
        // No other planets.
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 100.0, moonLong: 28.0,
            ("Jupiter", 152.0));
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_NotBeVoid_When_SextilePerfectsFromMirrorCandidate()
    {
        // Moon at 20° Aries (20.0°), 10° remaining.
        // Venus at 87° Gemini — sextile (60°) candidates:
        //   c1 = (87 + 60) % 360 = 147° → forward 127° > 10° → skip
        //   c2 = (87 - 60 + 360) % 360 = 27° → forward 7° < 10° → HIT
        // First applying = Moon Sextile Venus, forward distance 7°.
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 100.0, moonLong: 20.0,
            ("Venus", 87.0));
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeFalse();
        result.FirstApplyingPlanet.Should().Be("Venus");
        result.FirstApplyingAspectType.Should().Be(AspectType.Sextile);
        result.FirstApplyingOrb.Should().BeApproximately(7.0, 0.001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_NotCountAspectsBehindMoon()
    {
        // Moon at 25° Aries (25.0°), 5° remaining.
        // Mars at 20° Aries (20.0°) — conjunction candidate at 20°.
        // Forward distance = (20 - 25 + 360) % 360 = 355° → not within 5° → skip.
        // The aspect is behind the Moon (separating), so VoC = true.
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 100.0, moonLong: 25.0,
            ("Mars", 20.0));
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_OnlyCheckMajorAspects()
    {
        // Moon at 20° Aries (20.0°), 10° remaining.
        // Saturn at 335° Pisces — the semi-square angle (45°) would give candidate
        //   (335 + 45) % 360 = 20° (exact current position) or (335 - 45 + 360) = 290°.
        //   Neither is a major aspect. Major aspect candidates from Saturn at 335°:
        //   Conjunction: 335° → forward 315° → skip
        //   Sextile: 395%360=35°, 275° → forward 15° > 10° (35-20), 255° → skip
        //   Square: 425%360=65°, 245° → skip
        //   Trine: 455%360=95°, 215° → skip
        //   Opposition: 515%360=155° → skip
        // Only 35° sextile candidate is nearest, but forward = 15° > 10° remaining → VoC.
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 100.0, moonLong: 20.0,
            ("Saturn", 335.0));
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_IncludeOuterPlanets()
    {
        // Moon at 20° Aries (20.0°), 10° remaining.
        // Uranus at 25° Aries (25.0°) — conjunction candidate at 25°.
        // Forward distance = 5° < 10° → NOT VoC.
        // Outer planets (Uranus, Neptune, Pluto) must be included in VoC scan.
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 100.0, moonLong: 20.0,
            ("Uranus", 25.0));
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeFalse();
        result.FirstApplyingPlanet.Should().Be("Uranus");
        result.FirstApplyingAspectType.Should().Be(AspectType.Conjunction);
        result.FirstApplyingOrb.Should().BeApproximately(5.0, 0.001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_SelectNearestAspect_When_MultipleAvailable()
    {
        // Moon at 20° Aries (20.0°), 10° remaining.
        // Venus at 87° Gemini → sextile candidate at 27° (forward 7°)
        // Mars at 24° Aries → conjunction candidate at 24° (forward 4°) ← nearest
        // First applying = Mars conjunction (4°), not Venus sextile (7°).
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 100.0, moonLong: 20.0,
            ("Venus", 87.0),
            ("Mars", 24.0));
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeFalse();
        result.FirstApplyingPlanet.Should().Be("Mars");
        result.FirstApplyingAspectType.Should().Be(AspectType.Conjunction);
        result.FirstApplyingOrb.Should().BeApproximately(4.0, 0.001);
    }

    #endregion

    #region Void of Course — Reproduction & Edge Cases

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_NotBeVoid_BugReportReproduction()
    {
        // Reproduction case from bug report:
        // Moon at 12°14′59″ Capricorn ≈ 282.2497° (270 + 12 + 14/60 + 59/3600)
        // Venus at 22°50′31″ Scorpio ≈ 232.8419° (210 + 22 + 50/60 + 31/3600)
        // Sextile (60°): candidate c1 = (232.8419 + 60) % 360 = 292.8419°
        // 292.84° is in Capricorn (270-300), ahead of Moon at 282.25°
        // Forward distance ≈ 10.59° < 17.75° remaining → NOT VoC
        double moonLon = 270.0 + 12.0 + (14.0 / 60.0) + (59.0 / 3600.0);  // ≈ 282.2497
        double venusLon = 210.0 + 22.0 + (50.0 / 60.0) + (31.0 / 3600.0); // ≈ 232.8419
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 190.0, moonLong: moonLon,
            ("Venus", venusLon));
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeFalse();
        result.FirstApplyingPlanet.Should().Be("Venus");
        result.FirstApplyingAspectType.Should().Be(AspectType.Sextile);
        result.FirstApplyingOrb.Should().BeApproximately(10.59, 0.1);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_BeVoid_GenuineVoC_MoonNearEndOfSign()
    {
        // Moon at 28° Aries (28.0°), only 2° remaining.
        // Sun at 100° Cancer. No other planets.
        // No major aspect from any planet perfects within 2°.
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 100.0, moonLong: 28.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_NotBeVoid_When_AspectJustBeforeSignBoundary()
    {
        // Moon at 28° Aries (28.0°), 2° remaining.
        // Mars at 29° Aries (29.0°) — conjunction candidate at 29°.
        // Forward distance = 1° < 2° → NOT VoC.
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 100.0, moonLong: 28.0,
            ("Mars", 29.0));
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeFalse();
        result.FirstApplyingPlanet.Should().Be("Mars");
        result.FirstApplyingAspectType.Should().Be(AspectType.Conjunction);
        result.FirstApplyingOrb.Should().BeApproximately(1.0, 0.001);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_BeVoid_When_AspectExactlyAtSignBoundary()
    {
        // Moon at 28° Aries (28.0°), 2° remaining (sign ends at 30°).
        // Mars at 30° Taurus (60.0°) — conjunction candidate at 60°.
        // Forward distance = 32° > 2° → VoC.
        // The aspect does not perfect within the current sign.
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 100.0, moonLong: 28.0,
            ("Mars", 60.0));
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_NotBeVoid_When_OuterPlanetFormsTrine()
    {
        // Moon at 20° Aries (20.0°), 10° remaining.
        // Neptune at 25° Aries (25.0°) — conjunction at 25° (forward 5°).
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 100.0, moonLong: 20.0,
            ("Neptune", 25.0));
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeFalse();
        result.FirstApplyingPlanet.Should().Be("Neptune");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_HandlePiscesAriesWraparound()
    {
        // Moon at 355° Pisces (355.0°), 5° remaining (sign ends at 360°/0°).
        // Mars at 357° Pisces (357.0°) — conjunction candidate at 357°.
        // Forward distance = 2° < 5° → NOT VoC.
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 100.0, moonLong: 355.0,
            ("Mars", 357.0));
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeFalse();
        result.FirstApplyingPlanet.Should().Be("Mars");
        result.FirstApplyingAspectType.Should().Be(AspectType.Conjunction);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_IgnoreTrueNodeAndChiron()
    {
        // Moon at 20° Aries (20.0°), 10° remaining.
        // Only True Node at 25° Aries and Chiron at 26° Aries.
        // Both should be excluded from VoC scan → VoC = true.
        IReadOnlyList<PlanetPosition> positions =
        [
            CreatePosition("Sun", 100.0),
            CreatePosition("Moon", 20.0),
            new PlanetPosition
            {
                Body = CelestialBodyId.TrueNode,
                Name = "True Node",
                Longitude = 25.0,
                Latitude = 0.0,
                Distance = 1.0,
                SpeedInLongitude = -0.05,
                SpeedInLatitude = 0.0,
                SpeedInDistance = 0.0,
            },
            new PlanetPosition
            {
                Body = CelestialBodyId.Chiron,
                Name = "Chiron",
                Longitude = 26.0,
                Latitude = 0.0,
                Distance = 1.0,
                SpeedInLongitude = 0.02,
                SpeedInLatitude = 0.0,
                SpeedInDistance = 0.0,
            },
        ];
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_DetectSquareAcrossSigns()
    {
        // Moon at 5° Cancer (95.0°), 25° remaining.
        // Jupiter at 10° Aries (10.0°) — square (90°) candidates:
        //   c1 = (10 + 90) % 360 = 100° → forward 5° < 25° → HIT
        //   c2 = (10 - 90 + 360) % 360 = 280° → forward 185° → skip
        // First applying = Moon Square Jupiter, forward 5°.
        IReadOnlyList<PlanetPosition> positions = CreatePositionsWithPlanets(
            sunLong: 0.0, moonLong: 95.0,
            ("Jupiter", 10.0));
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeFalse();
        result.FirstApplyingPlanet.Should().Be("Jupiter");
        result.FirstApplyingAspectType.Should().Be(AspectType.Square);
        result.FirstApplyingOrb.Should().BeApproximately(5.0, 0.001);
    }

    #endregion

    #region Moving Targets — The Window Ends at the Moon's Sign Exit

    /// <summary>Reference instant of the synthetic ephemerides below (Julian Day, UT).</summary>
    private const double SyntheticJulianDay = 2461000.5;

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_BeVoid_When_AMovingTargetIsReachedJustAfterTheSignExit()
    {
        // Moon at 25° Aries (+13°/day) leaves Aries after 5/13 day, at 9 h 13.8 min. Mercury at
        // 29.46° Aries runs ahead at +1.5°/day, so the Moon gains only 11.5°/day and reaches
        // the conjunction after 4.46/11.5 day, at 9 h 18.5 min: 4.6 minutes into Taurus, so it
        // does not count. Held still, Mercury's conjunction point is 4.46° ahead of the Moon,
        // inside the 5° left in Aries, and the fixed-target scan counts it.
        SyntheticSpeedEphemeris ephemeris = new SyntheticSpeedEphemeris(SyntheticJulianDay)
            .WithBody(CelestialBodyId.Moon, 13.0, 0.0, 25.0)
            .WithBody(CelestialBodyId.Sun, 1.0, 0.0, 100.0)
            .WithBody(CelestialBodyId.Mercury, 1.5, 0.0, 29.46);
        IReadOnlyList<PlanetPosition> positions = ephemeris.CalculateAllPositions(SyntheticJulianDay);

        MoonConditionInfo? result = _service.GetMoonCondition(positions, [], ephemeris, SyntheticJulianDay);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeTrue("the conjunction perfects after the Moon has left Aries");
        result.FirstApplyingPlanet.Should().BeNull();
        result.FirstApplyingAspectType.Should().BeNull();
        result.FirstApplyingOrb.Should().BeNull();
        _service.GetMoonConditionWithFixedTargets(positions, [])!.FirstApplyingPlanet.Should().Be("Mercury",
            "held still, Mercury would be reached inside Aries");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_NotBeVoid_When_AMovingTargetIsReachedJustBeforeTheSignExit()
    {
        // Moon at 25° Aries (+13°/day) leaves Aries after 5/13 day, at 9 h 13.8 min. Mars at
        // 0°20′ Taurus is retrograde at −1°/day, so the Moon gains 14°/day and reaches the
        // conjunction after 5.34/14 day, at 9 h 9.3 min, with both at 29.96° Aries: 4.6 minutes
        // before the exit, so it counts. Held still, Mars's conjunction point lies past the
        // sign boundary and the fixed-target scan calls the Moon void.
        SyntheticSpeedEphemeris ephemeris = new SyntheticSpeedEphemeris(SyntheticJulianDay)
            .WithBody(CelestialBodyId.Moon, 13.0, 0.0, 25.0)
            .WithBody(CelestialBodyId.Sun, 1.0, 0.0, 100.0)
            .WithBody(CelestialBodyId.Mars, -1.0, 0.0, 30.34);
        IReadOnlyList<PlanetPosition> positions = ephemeris.CalculateAllPositions(SyntheticJulianDay);

        MoonConditionInfo? result = _service.GetMoonCondition(positions, [], ephemeris, SyntheticJulianDay);

        result.Should().NotBeNull();
        result!.IsVoidOfCourse.Should().BeFalse("the conjunction perfects before the Moon leaves Aries");
        result.FirstApplyingPlanet.Should().Be("Mars");
        result.FirstApplyingAspectType.Should().Be(AspectType.Conjunction);
        result.FirstApplyingOrb.Should().BeApproximately(5.34, 0.0001,
            "the orb reported is the separation from exact at the chart instant");
        _service.GetMoonConditionWithFixedTargets(positions, [])!.IsVoidOfCourse.Should().BeTrue(
            "held still, Mars's conjunction point lies past the sign boundary");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ThrowOnNullEphemeris()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 45.0);

        Action act = () => _service.GetMoonCondition(positions, [], null!, SyntheticJulianDay);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Moon Sign and Longitude Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ReportMoonSign_When_MoonInTaurus()
    {
        // Moon at 45° = 15° Taurus
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 45.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.MoonSign.Should().Be(ZodiacSign.Taurus);
        result.MoonLongitude.Should().Be(45.0);
    }

    #endregion

    #region Edge Case Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ReturnNull_When_NoMoonInPositions()
    {
        // No Moon in positions list
        IReadOnlyList<PlanetPosition> positions =
        [
            CreatePosition("Sun", 0.0),
            CreatePosition("Mars", 60.0),
        ];
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ReturnNull_When_NoSunInPositions()
    {
        // No Sun in positions list
        IReadOnlyList<PlanetPosition> positions =
        [
            CreatePosition("Moon", 45.0),
            CreatePosition("Mars", 60.0),
        ];
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().BeNull();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ThrowOnNullPositions()
    {
        Action act = () => _service.GetMoonConditionWithFixedTargets(null!, []);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_ThrowOnNullAspects()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 0.0, moonLong: 45.0);

        Action act = () => _service.GetMoonConditionWithFixedTargets(positions, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetMoonCondition_Should_HandlePhaseAngle0_When_ExactConjunction()
    {
        // Sun and Moon at same longitude → phase angle = 0° → New Moon
        IReadOnlyList<PlanetPosition> positions = CreatePositions(sunLong: 100.0, moonLong: 100.0);
        IReadOnlyList<AspectResult> aspects = [];

        MoonConditionInfo? result = _service.GetMoonConditionWithFixedTargets(positions, aspects);

        result.Should().NotBeNull();
        result!.PhaseAngle.Should().Be(0.0);
        result.Phase.Should().Be(MoonPhaseType.NewMoon);
    }

    #endregion

    #region Integration Test

    /// <summary>
    /// Integration test using real Swiss Ephemeris to verify Moon conditions
    /// in a calculated chart.
    /// </summary>
    [Collection("Ephemeris")]
    public sealed class IntegrationTests
    {
        private readonly ChartEngine _engine;

        public IntegrationTests(EphemerisFixture fixture)
        {
            ILogger logger = new LoggerConfiguration().CreateLogger();
            AspectService aspects = new(fixture.EphemerisService, logger);
            MidpointService midpoints = new(logger);
            _engine = new ChartEngine(
                fixture.EphemerisService, aspects, midpoints,
                new DignityService(), new MoonConditionService(),
                new ChartContextService(new DignityService(), new SectService()), new FixedStarService(fixture.EphemerisService, logger), new DeclinationAspectService(), new PlanetConditionService(), new MoonMansionService(), new PlanetaryHourCalculator(fixture.EphemerisService), new ArabicPartService(new DignityService()), new AntisciaService(), new StationService(), new ParanService(fixture.EphemerisService, logger), logger);
        }

        [Fact]
        [Trait("Category", "Integration")]
        public void Calculate_Should_PopulateMoonCondition_ForJ2000()
        {
            ChartInput input = new()
            {
                Name = "J2000.0 Moon Test",
                LocalDateTime = new DateTime(2000, 1, 1, 7, 0, 0),
                TimeZone = "America/New_York",
                Latitude = 40.7128,
                Longitude = -74.006,
                LocationName = "New York",
                EventType = "Event",
                Gender = Gender.Unspecified,
            };

            ChartResult result = _engine.Calculate(input);

            result.MoonCondition.Should().NotBeNull();
            result.MoonCondition!.PhaseAngle.Should().BeInRange(0.0, 360.0);
            result.MoonCondition.MoonSign.Should().BeOneOf(
                ZodiacSign.Aries, ZodiacSign.Taurus, ZodiacSign.Gemini,
                ZodiacSign.Cancer, ZodiacSign.Leo, ZodiacSign.Virgo,
                ZodiacSign.Libra, ZodiacSign.Scorpio, ZodiacSign.Sagittarius,
                ZodiacSign.Capricorn, ZodiacSign.Aquarius, ZodiacSign.Pisces);
            result.MoonCondition.MoonLongitude.Should().BeInRange(0.0, 360.0);
        }
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates a minimal set of positions with Sun and Moon at the specified longitudes.
    /// </summary>
    private static IReadOnlyList<PlanetPosition> CreatePositions(double sunLong, double moonLong)
    {
        return
        [
            CreatePosition("Sun", sunLong),
            CreatePosition("Moon", moonLong),
        ];
    }

    /// <summary>
    /// Creates positions with Sun, Moon, and additional planets at specified longitudes.
    /// </summary>
    private static IReadOnlyList<PlanetPosition> CreatePositionsWithPlanets(
        double sunLong, double moonLong,
        params (string Name, double Longitude)[] planets)
    {
        List<PlanetPosition> positions =
        [
            CreatePosition("Sun", sunLong),
            CreatePosition("Moon", moonLong),
        ];

        foreach ((string name, double longitude) in planets)
        {
            positions.Add(CreatePosition(name, longitude));
        }

        return positions;
    }

    /// <summary>
    /// Creates a minimal PlanetPosition at the specified longitude.
    /// </summary>
    private static PlanetPosition CreatePosition(string name, double longitude)
    {
        CelestialBodyId body = name switch
        {
            "Sun" => CelestialBodyId.Sun,
            "Moon" => CelestialBodyId.Moon,
            "Mercury" => CelestialBodyId.Mercury,
            "Venus" => CelestialBodyId.Venus,
            "Mars" => CelestialBodyId.Mars,
            "Jupiter" => CelestialBodyId.Jupiter,
            "Saturn" => CelestialBodyId.Saturn,
            "Uranus" => CelestialBodyId.Uranus,
            "Neptune" => CelestialBodyId.Neptune,
            "Pluto" => CelestialBodyId.Pluto,
            _ => CelestialBodyId.Sun,
        };

        return new PlanetPosition
        {
            Body = body,
            Name = name,
            Longitude = longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = 1.0,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
        };
    }

    #endregion
}
