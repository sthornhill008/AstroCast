using AstroCast.Core.Configuration;
using AstroCast.Core.Enums;

using FluentAssertions;

using static AstroCast.Core.Configuration.SynastryOrbs;

namespace AstroCast.Tests.Models;

/// <summary>
/// Tests for <see cref="SynastryOrbs"/> verifying planet category classification
/// and synastry-specific orb lookup by aspect type and body pair.
/// </summary>
[Trait("Category", "Unit")]
public sealed class SynastryOrbsTests
{
    [Theory]
    [InlineData("Sun", PlanetCategory.Luminary)]
    [InlineData("Moon", PlanetCategory.Luminary)]
    [InlineData("A Sun", PlanetCategory.Luminary)]
    [InlineData("B Moon", PlanetCategory.Luminary)]
    public void GetCategory_Should_ReturnLuminary_When_SunOrMoon(
        string bodyName, PlanetCategory expected)
    {
        // Act
        PlanetCategory result = SynastryOrbs.GetCategory(bodyName);

        // Assert
        result.Should().Be(expected,
            $"'{bodyName}' should be classified as Luminary");
    }

    [Theory]
    [InlineData("Mercury", PlanetCategory.Personal)]
    [InlineData("Venus", PlanetCategory.Personal)]
    [InlineData("Mars", PlanetCategory.Personal)]
    public void GetCategory_Should_ReturnPersonal_When_InnerPlanet(
        string bodyName, PlanetCategory expected)
    {
        // Act
        PlanetCategory result = SynastryOrbs.GetCategory(bodyName);

        // Assert
        result.Should().Be(expected,
            $"'{bodyName}' should be classified as Personal");
    }

    [Theory]
    [InlineData("ASC", PlanetCategory.Angle)]
    [InlineData("MC", PlanetCategory.Angle)]
    [InlineData("B MC", PlanetCategory.Angle)]
    public void GetCategory_Should_ReturnAngle_When_AscOrMc(
        string bodyName, PlanetCategory expected)
    {
        // Act
        PlanetCategory result = SynastryOrbs.GetCategory(bodyName);

        // Assert
        result.Should().Be(expected,
            $"'{bodyName}' should be classified as Angle");
    }

    [Theory]
    [InlineData("Jupiter", PlanetCategory.Outer)]
    [InlineData("Saturn", PlanetCategory.Outer)]
    [InlineData("Pluto", PlanetCategory.Outer)]
    [InlineData("Chiron", PlanetCategory.Outer)]
    [InlineData("True Node", PlanetCategory.Outer)]
    public void GetCategory_Should_ReturnOuter_When_OuterPlanet(
        string bodyName, PlanetCategory expected)
    {
        // Act
        PlanetCategory result = SynastryOrbs.GetCategory(bodyName);

        // Assert
        result.Should().Be(expected,
            $"'{bodyName}' should be classified as Outer");
    }

    [Fact]
    public void GetOrb_Should_ReturnWiderOrb_When_MixedCategories()
    {
        // Sun (Luminary) + Pluto (Outer) → uses Luminary orb (wider)
        double orb1 = SynastryOrbs.GetOrb(AspectType.Conjunction, "Sun", "Pluto");
        orb1.Should().Be(8.0,
            "conjunction between Luminary and Outer should use the wider Luminary orb");

        // Mars (Personal) + Saturn (Outer) → uses Personal orb (wider)
        double orb2 = SynastryOrbs.GetOrb(AspectType.Conjunction, "Mars", "Saturn");
        orb2.Should().Be(7.0,
            "conjunction between Personal and Outer should use the wider Personal orb");
    }

    [Fact]
    public void GetOrb_Should_ReturnTighterOrb_When_MinorAspect()
    {
        // Semi-square between two outer planets → 1.5°
        double orb = SynastryOrbs.GetOrb(AspectType.SemiSquare, "Jupiter", "Neptune");
        orb.Should().Be(1.5,
            "semi-square between two Outer planets should return 1.5° orb");
    }

    [Fact]
    public void GetOrb_Should_ReturnLuminaryOrb_When_BothLuminaries()
    {
        // Trine between Sun and Moon → 7.0°
        double orb = SynastryOrbs.GetOrb(AspectType.Trine, "Sun", "Moon");
        orb.Should().Be(7.0,
            "trine between two Luminaries should return 7.0° orb");
    }

    [Fact]
    public void GetOrb_Should_ReturnAngleOrb_When_BothAngles()
    {
        // Square between ASC and MC → 4.0°
        double orb = SynastryOrbs.GetOrb(AspectType.Square, "A ASC", "B MC");
        orb.Should().Be(4.0,
            "square between two Angles should return 4.0° orb");
    }

    [Fact]
    public void GetOrb_Should_HandlePrefixedNames_When_Called()
    {
        // A Sun + B Venus → Luminary + Personal → uses Luminary orb
        double orb = SynastryOrbs.GetOrb(AspectType.Opposition, "A Sun", "B Venus");
        orb.Should().Be(8.0,
            "opposition between prefixed Luminary and Personal should use Luminary orb");
    }
}
