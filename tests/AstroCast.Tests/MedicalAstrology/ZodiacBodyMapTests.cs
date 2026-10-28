using AstroCast.Core.Enums;
using AstroCast.Core.MedicalAstrology;
using FluentAssertions;
using Xunit;

namespace AstroCast.Tests.MedicalAstrology;

/// <summary>
/// Tests for the <see cref="ZodiacBodyMap"/> static correspondence data.
/// </summary>
public sealed class ZodiacBodyMapTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(ZodiacSign.Aries)]
    [InlineData(ZodiacSign.Taurus)]
    [InlineData(ZodiacSign.Gemini)]
    [InlineData(ZodiacSign.Cancer)]
    [InlineData(ZodiacSign.Leo)]
    [InlineData(ZodiacSign.Virgo)]
    [InlineData(ZodiacSign.Libra)]
    [InlineData(ZodiacSign.Scorpio)]
    [InlineData(ZodiacSign.Sagittarius)]
    [InlineData(ZodiacSign.Capricorn)]
    [InlineData(ZodiacSign.Aquarius)]
    [InlineData(ZodiacSign.Pisces)]
    public void GetBodyRegions_ReturnsNonEmpty_ForAllSigns(ZodiacSign sign)
    {
        var regions = ZodiacBodyMap.GetBodyRegions(sign);
        regions.Should().NotBeEmpty($"{sign} should have body region associations");
        regions.Should().HaveCountGreaterThanOrEqualTo(3);
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(ZodiacSign.Aries)]
    [InlineData(ZodiacSign.Taurus)]
    [InlineData(ZodiacSign.Gemini)]
    [InlineData(ZodiacSign.Cancer)]
    [InlineData(ZodiacSign.Leo)]
    [InlineData(ZodiacSign.Virgo)]
    [InlineData(ZodiacSign.Libra)]
    [InlineData(ZodiacSign.Scorpio)]
    [InlineData(ZodiacSign.Sagittarius)]
    [InlineData(ZodiacSign.Capricorn)]
    [InlineData(ZodiacSign.Aquarius)]
    [InlineData(ZodiacSign.Pisces)]
    public void GetHealthSummary_ReturnsNonEmpty_ForAllSigns(ZodiacSign sign)
    {
        var summary = ZodiacBodyMap.GetHealthSummary(sign);
        summary.Should().NotBeNullOrWhiteSpace();
        summary.Should().Contain("Focus areas:");
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void AllRegions_Contains12Signs()
    {
        ZodiacBodyMap.AllRegions.Should().HaveCount(12);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Aries_RulesHead()
    {
        var regions = ZodiacBodyMap.GetBodyRegions(ZodiacSign.Aries);
        regions.Should().Contain(r => r.Name.Contains("Head", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Pisces_RulesFeet()
    {
        var regions = ZodiacBodyMap.GetBodyRegions(ZodiacSign.Pisces);
        regions.Should().Contain(r => r.Name.Contains("Feet", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Leo_RulesHeart()
    {
        var regions = ZodiacBodyMap.GetBodyRegions(ZodiacSign.Leo);
        regions.Should().Contain(r => r.Name.Contains("Heart", StringComparison.OrdinalIgnoreCase));
    }
}
