using AstroCast.Core.Enums;
using AstroCast.Core.MedicalAstrology;
using FluentAssertions;
using Xunit;

namespace AstroCast.Tests.MedicalAstrology;

/// <summary>
/// Tests for the <see cref="ElementHelper"/> static mapping methods.
/// </summary>
public sealed class ElementHelperTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(ZodiacSign.Aries, "Fire")]
    [InlineData(ZodiacSign.Leo, "Fire")]
    [InlineData(ZodiacSign.Sagittarius, "Fire")]
    [InlineData(ZodiacSign.Taurus, "Earth")]
    [InlineData(ZodiacSign.Virgo, "Earth")]
    [InlineData(ZodiacSign.Capricorn, "Earth")]
    [InlineData(ZodiacSign.Gemini, "Air")]
    [InlineData(ZodiacSign.Libra, "Air")]
    [InlineData(ZodiacSign.Aquarius, "Air")]
    [InlineData(ZodiacSign.Cancer, "Water")]
    [InlineData(ZodiacSign.Scorpio, "Water")]
    [InlineData(ZodiacSign.Pisces, "Water")]
    public void GetElement_ReturnsCorrectElement(ZodiacSign sign, string expectedElement)
    {
        ElementHelper.GetElement(sign).Should().Be(expectedElement);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData("Fire", Temperament.Choleric)]
    [InlineData("Earth", Temperament.Melancholic)]
    [InlineData("Air", Temperament.Sanguine)]
    [InlineData("Water", Temperament.Phlegmatic)]
    public void GetTemperament_ReturnsCorrectMapping(string element, Temperament expected)
    {
        ElementHelper.GetTemperament(element).Should().Be(expected);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(ZodiacSign.Aries, CelestialBodyId.Mars)]
    [InlineData(ZodiacSign.Taurus, CelestialBodyId.Venus)]
    [InlineData(ZodiacSign.Cancer, CelestialBodyId.Moon)]
    [InlineData(ZodiacSign.Leo, CelestialBodyId.Sun)]
    [InlineData(ZodiacSign.Scorpio, CelestialBodyId.Mars)]
    [InlineData(ZodiacSign.Aquarius, CelestialBodyId.Saturn)]
    [InlineData(ZodiacSign.Pisces, CelestialBodyId.Jupiter)]
    public void GetTraditionalRuler_ReturnsCorrectRuler(ZodiacSign sign, CelestialBodyId expectedRuler)
    {
        ElementHelper.GetTraditionalRuler(sign).Should().Be(expectedRuler);
    }
}
