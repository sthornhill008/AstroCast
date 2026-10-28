using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Core.Services;

using FluentAssertions;

namespace AstroCast.Tests.Services;

/// <summary>
/// Tests for <see cref="PlanetConditionService"/> — accidental condition analysis
/// including combustion hierarchy (cazimi, combust, under the beams), besiegement,
/// and benefic/malefic influence from conjunctions and applying aspects.
/// All tests are pure unit tests requiring no external resources.
/// </summary>
public sealed class PlanetConditionServiceTests
{
    private readonly PlanetConditionService _service = new();

    #region Cazimi Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectCazimi_When_PlanetWithin017ArcMinutesOfSun()
    {
        // Mercury at 120.1° — Sun at 120.0° — separation = 0.1° < 0.2833°
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 120.1));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.IsCazimi.Should().BeTrue();
        mercury.IsCombust.Should().BeFalse();
        mercury.IsUnderTheBeams.Should().BeFalse();
        mercury.AccidentalScore.Should().Be(5);
        mercury.Conditions.Should().Contain("Cazimi");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectCazimi_ForMoon()
    {
        // Moon at 120.2° — Sun at 120.0° — separation = 0.2° < 0.2833°
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Moon", 120.2));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition moon = result.First(c => c.PlanetName == "Moon");
        moon.IsCazimi.Should().BeTrue();
        moon.AccidentalScore.Should().Be(5);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectCazimi_WhenWrappingAround360()
    {
        // Sun at 359.9° — Venus at 0.1° — separation = 0.2° < 0.2833°
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 359.9), ("Venus", 0.1));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition venus = result.First(c => c.PlanetName == "Venus");
        venus.IsCazimi.Should().BeTrue();
    }

    #endregion

    #region Combustion Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectCombust_When_PlanetWithin8DegreesOfSun()
    {
        // Mercury at 125.0° — Sun at 120.0° — separation = 5° (within 8°, outside cazimi)
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 125.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.IsCombust.Should().BeTrue();
        mercury.IsCazimi.Should().BeFalse();
        mercury.IsUnderTheBeams.Should().BeFalse();
        mercury.AccidentalScore.Should().Be(-5);
        mercury.Conditions.Should().Contain("Combust");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotDetectCombust_ForMoon()
    {
        // Moon at 125.0° — Sun at 120.0° — separation = 5° but Moon is exempt
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Moon", 125.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Any(c => c.PlanetName == "Moon").Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectCombust_ForVenus()
    {
        // Venus at 117.0° — Sun at 120.0° — separation = 3°
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Venus", 117.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition venus = result.First(c => c.PlanetName == "Venus");
        venus.IsCombust.Should().BeTrue();
        venus.AccidentalScore.Should().Be(-5);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectCombust_ForMars()
    {
        // Mars at 114.0° — Sun at 120.0° — separation = 6°
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mars", 114.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mars = result.First(c => c.PlanetName == "Mars");
        mars.IsCombust.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectCombust_ForJupiter()
    {
        // Jupiter at 127.5° — Sun at 120.0° — separation = 7.5°
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Jupiter", 127.5));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition jupiter = result.First(c => c.PlanetName == "Jupiter");
        jupiter.IsCombust.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectCombust_ForSaturn()
    {
        // Saturn at 113.0° — Sun at 120.0° — separation = 7°
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Saturn", 113.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition saturn = result.First(c => c.PlanetName == "Saturn");
        saturn.IsCombust.Should().BeTrue();
    }

    #endregion

    #region Under the Beams Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectUnderTheBeams_When_Planet8To17DegreesFromSun()
    {
        // Mercury at 132.0° — Sun at 120.0° — separation = 12° (8-17 range)
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 132.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.IsUnderTheBeams.Should().BeTrue();
        mercury.IsCombust.Should().BeFalse();
        mercury.IsCazimi.Should().BeFalse();
        mercury.AccidentalScore.Should().Be(-2);
        mercury.Conditions.Should().Contain("Under the Beams");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotDetectUnderTheBeams_ForMoon()
    {
        // Moon at 132.0° — Sun at 120.0° — separation = 12° but Moon is exempt
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Moon", 132.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Any(c => c.PlanetName == "Moon").Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotDetectAnyCombustion_When_PlanetBeyond17Degrees()
    {
        // Mercury at 140.0° — Sun at 120.0° — separation = 20° (beyond 17°)
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 140.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Any(c => c.PlanetName == "Mercury").Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotApplyCombustion_ToOuterPlanets()
    {
        // Uranus at 125.0° — Sun at 120.0° — separation = 5° but Uranus not combustible
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Uranus", 125.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Any(c => c.PlanetName == "Uranus").Should().BeFalse();
    }

    #endregion

    #region Besiegement Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectBesiegement_When_PlanetBetweenMarsAndSaturn()
    {
        // Mercury at 100° — Mars at 95° — Saturn at 105° — no benefic between
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 200.0), ("Mercury", 100.0), ("Mars", 95.0), ("Saturn", 105.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.IsBesieged.Should().BeTrue();
        mercury.AccidentalScore.Should().Be(-5);
        mercury.Conditions.Should().Contain("Besieged");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotDetectBesiegement_When_BeneficIntervenes()
    {
        // Mercury at 100° — Mars at 95° — Saturn at 105° — Jupiter at 102° intervenes
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 200.0), ("Mercury", 100.0), ("Mars", 95.0),
            ("Saturn", 105.0), ("Jupiter", 102.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Any(c => c.PlanetName == "Mercury" && c.IsBesieged).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotDetectBesiegement_When_VenusIntervenes()
    {
        // Moon at 100° — Mars at 95° — Saturn at 110° — Venus at 103° intervenes
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 200.0), ("Moon", 100.0), ("Mars", 95.0),
            ("Saturn", 110.0), ("Venus", 103.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Any(c => c.PlanetName == "Moon" && c.IsBesieged).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectBesiegement_ForMoon()
    {
        // Moon at 100° — Mars at 95° — Saturn at 105° — no benefic between
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 200.0), ("Moon", 100.0), ("Mars", 95.0), ("Saturn", 105.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition moon = result.First(c => c.PlanetName == "Moon");
        moon.IsBesieged.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotDetectBesiegement_ForMaleficPlanets()
    {
        // Mars cannot be besieged (it's a besieger)
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 200.0), ("Mars", 100.0), ("Saturn", 105.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Any(c => c.PlanetName == "Mars" && c.IsBesieged).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotDetectBesiegement_ForBeneficPlanets()
    {
        // Jupiter cannot be besieged (it's a protector)
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 200.0), ("Jupiter", 100.0), ("Mars", 95.0), ("Saturn", 105.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Any(c => c.PlanetName == "Jupiter" && c.IsBesieged).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectBesiegement_AcrossZeroDegrees()
    {
        // Mercury at 2° — Mars at 355° — Saturn at 8° — short arc = Mars(355)→Saturn(8) = 13°
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 200.0), ("Mercury", 2.0), ("Mars", 355.0), ("Saturn", 8.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.IsBesieged.Should().BeTrue();
    }

    #endregion

    #region Benefic/Malefic Influence Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectConjunctBenefic_When_ConjunctJupiter()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 200.0), ("Jupiter", 202.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Mercury", "Jupiter", AspectType.Conjunction, 2.0, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.IsConjunctBenefic.Should().BeTrue();
        mercury.AccidentalScore.Should().Be(2);
        mercury.Conditions.Should().Contain("Conjunct benefic");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectConjunctBenefic_When_ConjunctVenus()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Moon", 200.0), ("Venus", 203.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Moon", "Venus", AspectType.Conjunction, 3.0, isApplying: false));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition moon = result.First(c => c.PlanetName == "Moon");
        moon.IsConjunctBenefic.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectConjunctMalefic_When_ConjunctMars()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 200.0), ("Mars", 203.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Mercury", "Mars", AspectType.Conjunction, 3.0, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.IsConjunctMalefic.Should().BeTrue();
        mercury.AccidentalScore.Should().Be(-2);
        mercury.Conditions.Should().Contain("Conjunct malefic");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectConjunctMalefic_When_ConjunctSaturn()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Moon", 200.0), ("Saturn", 205.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Moon", "Saturn", AspectType.Conjunction, 5.0, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition moon = result.First(c => c.PlanetName == "Moon");
        moon.IsConjunctMalefic.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectApplyingBeneficAspect_When_ApplyingTrineFromJupiter()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 200.0), ("Jupiter", 80.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Mercury", "Jupiter", AspectType.Trine, 0.5, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.HasApplyingBeneficAspect.Should().BeTrue();
        mercury.AccidentalScore.Should().Be(1);
        mercury.Conditions.Should().Contain("Applying benefic aspect");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectApplyingMaleficAspect_When_ApplyingSquareFromSaturn()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Moon", 200.0), ("Saturn", 110.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Moon", "Saturn", AspectType.Square, 0.3, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition moon = result.First(c => c.PlanetName == "Moon");
        moon.HasApplyingMaleficAspect.Should().BeTrue();
        moon.AccidentalScore.Should().Be(-1);
        moon.Conditions.Should().Contain("Applying malefic aspect");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotDetectApplyingAspect_When_Separating()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 200.0), ("Jupiter", 80.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Mercury", "Jupiter", AspectType.Trine, 0.5, isApplying: false));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        // Mercury has no conditions (separating benefic trine does not score)
        result.Any(c => c.PlanetName == "Mercury").Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_NotDetectApplyingAspect_ForMinorAspects()
    {
        // Semi-sextile is not a major aspect
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 200.0), ("Jupiter", 230.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Mercury", "Jupiter", AspectType.SemiSextile, 0.5, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Any(c => c.PlanetName == "Mercury").Should().BeFalse();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Jupiter", AspectType.Square)]
    [InlineData("Jupiter", AspectType.Opposition)]
    [InlineData("Venus", AspectType.Square)]
    [InlineData("Venus", AspectType.Opposition)]
    public void Analyze_Should_NotDetectApplyingBeneficAspect_When_ApplyingHardAspectFromBenefic(
        string benefic, AspectType type)
    {
        // The Techniques reference (:159): only an applying trine or sextile from Jupiter or Venus
        // is an applying benefic aspect
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 200.0), (benefic, 80.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Mercury", benefic, type, 0.5, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Any(c => c.PlanetName == "Mercury").Should().BeFalse();
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Mars", AspectType.Trine)]
    [InlineData("Mars", AspectType.Sextile)]
    [InlineData("Saturn", AspectType.Trine)]
    [InlineData("Saturn", AspectType.Sextile)]
    public void Analyze_Should_NotDetectApplyingMaleficAspect_When_ApplyingSoftAspectFromMalefic(
        string malefic, AspectType type)
    {
        // The Techniques reference (:160): only an applying square or opposition from Mars or
        // Saturn is an applying malefic aspect
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Moon", 200.0), (malefic, 80.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Moon", malefic, type, 0.5, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Any(c => c.PlanetName == "Moon").Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectApplyingBeneficAspect_When_ApplyingSextileFromVenus()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 200.0), ("Venus", 140.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Mercury", "Venus", AspectType.Sextile, 0.5, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.HasApplyingBeneficAspect.Should().BeTrue();
        mercury.AccidentalScore.Should().Be(1);
        mercury.Conditions.Should().Contain("Applying benefic aspect");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectApplyingMaleficAspect_When_ApplyingOppositionFromMars()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Moon", 200.0), ("Mars", 20.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Moon", "Mars", AspectType.Opposition, 0.5, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition moon = result.First(c => c.PlanetName == "Moon");
        moon.HasApplyingMaleficAspect.Should().BeTrue();
        moon.AccidentalScore.Should().Be(-1);
        moon.Conditions.Should().Contain("Applying malefic aspect");
    }

    #endregion

    #region Combined Condition Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_CombineMultipleConditions()
    {
        // Mercury combust (5° from Sun) AND conjunct malefic Mars
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 125.0), ("Mars", 126.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Mercury", "Mars", AspectType.Conjunction, 1.0, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.IsCombust.Should().BeTrue();
        mercury.IsConjunctMalefic.Should().BeTrue();
        mercury.AccidentalScore.Should().Be(-7); // -5 combust + -2 conjunct malefic
        mercury.Conditions.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_CombineBesiegementWithApplyingMaleficAspect()
    {
        // Mercury besieged between Mars(95) and Saturn(105), plus applying square from Mars
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 200.0), ("Mercury", 100.0), ("Mars", 95.0), ("Saturn", 105.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Mercury", "Mars", AspectType.Conjunction, 5.0, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.IsBesieged.Should().BeTrue();
        mercury.IsConjunctMalefic.Should().BeTrue();
        mercury.AccidentalScore.Should().Be(-7); // -5 besieged + -2 conjunct malefic
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ScoreCazimiWithBeneficInfluence()
    {
        // Venus cazimi (0.1° from Sun) AND applying trine from Jupiter
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Venus", 120.1), ("Jupiter", 0.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Venus", "Jupiter", AspectType.Trine, 0.5, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition venus = result.First(c => c.PlanetName == "Venus");
        venus.IsCazimi.Should().BeTrue();
        venus.HasApplyingBeneficAspect.Should().BeTrue();
        venus.AccidentalScore.Should().Be(6); // +5 cazimi + +1 applying benefic
    }

    #endregion

    #region Edge Cases

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_HandleEmptyPositions()
    {
        IReadOnlyList<PlanetCondition> result = _service.Analyze([], []);
        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_ExcludeSunFromResults()
    {
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_OmitPlanets_WithNoConditions()
    {
        // Mercury at 200° — Sun at 120° — separation = 80° (no combustion)
        // No aspects, no besiegement
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 200.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_HandleMissingMarsOrSaturn_ForBesiegement()
    {
        // Mercury at 100° — only Mars at 95° — no Saturn = no besiegement possible
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 200.0), ("Mercury", 100.0), ("Mars", 95.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Any(c => c.PlanetName == "Mercury" && c.IsBesieged).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_HandleNoSun_ForCombustion()
    {
        // No Sun in positions — no combustion analysis possible
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Mercury", 120.0), ("Moon", 125.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        result.Any(c => c.IsCazimi || c.IsCombust || c.IsUnderTheBeams).Should().BeFalse();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectBothBeneficAndMaleficInfluence()
    {
        // Mercury conjunct Jupiter AND conjunct Saturn
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 200.0), ("Jupiter", 202.0), ("Saturn", 198.0));
        IReadOnlyList<AspectResult> aspects = CreateAspects(
            ("Mercury", "Jupiter", AspectType.Conjunction, 2.0, isApplying: true),
            ("Mercury", "Saturn", AspectType.Conjunction, 2.0, isApplying: true));

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.IsConjunctBenefic.Should().BeTrue();
        mercury.IsConjunctMalefic.Should().BeTrue();
        mercury.AccidentalScore.Should().Be(0); // +2 - 2 = 0
        mercury.Conditions.Should().HaveCount(2);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectCombustionAtExactBoundary_8Degrees()
    {
        // Mercury at exactly 8° from Sun — should be combust (within 8°)
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 128.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.IsCombust.Should().BeTrue();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_Should_DetectUnderBeamsAtExactBoundary_17Degrees()
    {
        // Mercury at exactly 17° from Sun — should be under the beams (within 17°)
        IReadOnlyList<PlanetPosition> positions = CreatePositions(
            ("Sun", 120.0), ("Mercury", 137.0));
        IReadOnlyList<AspectResult> aspects = [];

        IReadOnlyList<PlanetCondition> result = _service.Analyze(positions, aspects);

        PlanetCondition mercury = result.First(c => c.PlanetName == "Mercury");
        mercury.IsUnderTheBeams.Should().BeTrue();
    }

    #endregion

    #region Test Helpers

    /// <summary>
    /// Creates a list of test PlanetPositions from name/longitude tuples.
    /// </summary>
    private static IReadOnlyList<PlanetPosition> CreatePositions(
        params (string Name, double Longitude)[] specs)
    {
        return specs.Select(s => new PlanetPosition
        {
            Body = CelestialBodyId.Sun, // Body ID not used by PlanetConditionService
            Name = s.Name,
            Longitude = s.Longitude,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = 1.0,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
            House = 1,
        }).ToList();
    }

    /// <summary>
    /// Creates a list of test AspectResults from specification tuples.
    /// </summary>
    private static IReadOnlyList<AspectResult> CreateAspects(
        params (string Body1, string Body2, AspectType Type, double Orb, bool isApplying)[] specs)
    {
        return specs.Select(s =>
        {
            AspectDefinition aspect = AspectDefinition.All.First(a => a.Type == s.Type);
            return new AspectResult
            {
                Body1Name = s.Body1,
                Body1Longitude = 0.0,
                Body2Name = s.Body2,
                Body2Longitude = 0.0,
                Aspect = aspect,
                Orb = s.Orb,
                IsApplying = s.isApplying,
            };
        }).ToList();
    }

    #endregion
}
