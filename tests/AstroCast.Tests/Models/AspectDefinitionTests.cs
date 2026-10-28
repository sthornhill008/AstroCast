using AstroCast.Core.Models;

using FluentAssertions;

namespace AstroCast.Tests.Models;

/// <summary>
/// Unit tests for <see cref="AspectDefinition"/> static configuration,
/// verifying that the aspect table is complete and internally consistent.
/// </summary>
[Trait("Category", "Unit")]
public sealed class AspectDefinitionTests
{
    [Fact]
    public void All_Should_Contain9Definitions_When_Accessed()
    {
        // Assert — 5 major + 4 Ebertin = 9 total
        AspectDefinition.All.Should().HaveCount(9,
            "there should be 9 aspect definitions (5 major + 4 Ebertin)");
    }

    [Fact]
    public void All_Should_HaveUniqueAngles_When_Accessed()
    {
        // Assert
        IEnumerable<double> angles = AspectDefinition.All.Select(a => a.Angle);
        angles.Should().OnlyHaveUniqueItems(
            "each aspect definition must have a distinct angle");
    }

    [Fact]
    public void All_Should_HavePositiveOrbs_When_Accessed()
    {
        // Assert
        foreach (AspectDefinition definition in AspectDefinition.All)
        {
            definition.Orb.Should().BePositive(
                $"{definition.Type} should have a positive orb allowance");
        }
    }
}
