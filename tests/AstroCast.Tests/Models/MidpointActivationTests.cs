using AstroCast.Core.Formatting;
using AstroCast.Core.Models;

using FluentAssertions;

namespace AstroCast.Tests.Models;

/// <summary>
/// Unit tests for <see cref="MidpointActivation"/> model properties and formatting.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MidpointActivationTests
{
    /// <summary>Creates a test midpoint for use in activation tests.</summary>
    private static Midpoint CreateTestMidpoint() => new()
    {
        Body1Name = "Sun",
        Body1Longitude = 280.0,
        Body2Name = "Moon",
        Body2Longitude = 120.0,
        Longitude = 200.0,
    };

    [Fact]
    public void Notation_Should_ShowDirectFormat_When_HarmonicIsZero()
    {
        // Arrange
        MidpointActivation activation = new()
        {
            Midpoint = CreateTestMidpoint(),
            ActivatorName = "Mars",
            ActivatorLongitude = 200.5,
            HarmonicAngle = 0,
            Orb = 0.5,
        };

        // Assert
        activation.Notation.Should().Be("Sun/Moon = Mars",
            "direct contact (0°) should not show a harmonic angle suffix");
    }

    [Fact]
    public void Notation_Should_ShowHarmonicAngle_When_HarmonicIsNonZero()
    {
        // Arrange
        MidpointActivation activation = new()
        {
            Midpoint = CreateTestMidpoint(),
            ActivatorName = "Mars",
            ActivatorLongitude = 290.5,
            HarmonicAngle = 90,
            Orb = 0.5,
        };

        // Assert
        activation.Notation.Should().Contain("(90°)",
            "non-direct activation should show the harmonic angle");
    }

    [Fact]
    public void FormattedActivation_Should_DisplayOrb_When_Created()
    {
        // Arrange — 0.75° = 0°45′
        MidpointActivation activation = new()
        {
            Midpoint = CreateTestMidpoint(),
            ActivatorName = "Mars",
            ActivatorLongitude = 200.75,
            HarmonicAngle = 0,
            Orb = 0.75,
        };

        // Assert
        PositionFormatter.FormatActivation(activation).Should().Contain("0°45′",
            "orb of 0.75° should display as 0°45′");
    }
}
