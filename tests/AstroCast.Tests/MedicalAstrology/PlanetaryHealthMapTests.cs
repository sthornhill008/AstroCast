using AstroCast.Core.Enums;
using AstroCast.Core.MedicalAstrology;
using FluentAssertions;
using Xunit;

namespace AstroCast.Tests.MedicalAstrology;

/// <summary>
/// Tests for the <see cref="PlanetaryHealthMap"/> static correspondence data.
/// </summary>
public sealed class PlanetaryHealthMapTests
{
    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(CelestialBodyId.Sun)]
    [InlineData(CelestialBodyId.Moon)]
    [InlineData(CelestialBodyId.Mercury)]
    [InlineData(CelestialBodyId.Venus)]
    [InlineData(CelestialBodyId.Mars)]
    [InlineData(CelestialBodyId.Jupiter)]
    [InlineData(CelestialBodyId.Saturn)]
    [InlineData(CelestialBodyId.Uranus)]
    [InlineData(CelestialBodyId.Neptune)]
    [InlineData(CelestialBodyId.Pluto)]
    public void GetBodyRegions_ReturnsNonEmpty_ForAllPlanets(CelestialBodyId body)
    {
        var regions = PlanetaryHealthMap.GetBodyRegions(body);
        regions.Should().NotBeEmpty($"{body} should have body region associations");
    }

    [Theory]
    [Trait("Category", "Unit")]
    [InlineData(CelestialBodyId.Sun)]
    [InlineData(CelestialBodyId.Moon)]
    [InlineData(CelestialBodyId.Mercury)]
    [InlineData(CelestialBodyId.Venus)]
    [InlineData(CelestialBodyId.Mars)]
    [InlineData(CelestialBodyId.Jupiter)]
    [InlineData(CelestialBodyId.Saturn)]
    [InlineData(CelestialBodyId.Uranus)]
    [InlineData(CelestialBodyId.Neptune)]
    [InlineData(CelestialBodyId.Pluto)]
    public void GetHealthRole_ReturnsNonEmpty_ForAllPlanets(CelestialBodyId body)
    {
        PlanetaryHealthMap.GetHealthRole(body).Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void TrueNode_ReturnsEmpty_NoMedicalCorrespondence()
    {
        PlanetaryHealthMap.GetBodyRegions(CelestialBodyId.TrueNode).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Chiron_ReturnsEmpty_NoMedicalCorrespondence()
    {
        PlanetaryHealthMap.GetBodyRegions(CelestialBodyId.Chiron).Should().BeEmpty();
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Mars_RulesMuscles()
    {
        var regions = PlanetaryHealthMap.GetBodyRegions(CelestialBodyId.Mars);
        regions.Should().Contain(r => r.Name.Contains("Muscle", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Saturn_RulesBones()
    {
        var regions = PlanetaryHealthMap.GetBodyRegions(CelestialBodyId.Saturn);
        regions.Should().Contain(r => r.Name.Contains("Bone", StringComparison.OrdinalIgnoreCase));
    }
}
