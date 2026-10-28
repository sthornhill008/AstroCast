using AstroCast.Core.Models;

using FluentAssertions;

namespace AstroCast.Tests.Models;

/// <summary>
/// Unit tests for <see cref="Midpoint"/> model properties and formatting.
/// </summary>
[Trait("Category", "Unit")]
public sealed class MidpointTests
{
    [Fact]
    public void Notation_Should_FormatCorrectly_When_Created()
    {
        // Arrange
        Midpoint midpoint = new()
        {
            Body1Name = "Sun",
            Body1Longitude = 280.0,
            Body2Name = "Moon",
            Body2Longitude = 120.0,
            Longitude = 200.0,
        };

        // Assert
        midpoint.Notation.Should().Be("Sun/Moon",
            "COSI notation should be Body1/Body2");
    }

    [Fact]
    public void FormattedPosition_Should_DisplayCorrectly_When_LongitudeGiven()
    {
        // Arrange — 0° ecliptic longitude is 0° Aries
        Midpoint midpoint = new()
        {
            Body1Name = "Sun",
            Body1Longitude = 350.0,
            Body2Name = "Moon",
            Body2Longitude = 10.0,
            Longitude = 0.0,
        };

        // Assert
        midpoint.FormattedPosition.Should().Contain("Aries",
            "0° ecliptic longitude should be in Aries");
    }
}
