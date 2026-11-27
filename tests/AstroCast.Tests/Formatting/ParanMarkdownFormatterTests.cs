using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Unit tests for <see cref="ParanMarkdownFormatter"/> verifying Markdown output
/// structure, position formatting, and empty-state handling.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ParanMarkdownFormatterTests
{
    #region Format Output Structure

    [Fact]
    public void Format_Should_ProduceValidOutput_When_ParansExist()
    {
        // Arrange
        ParanResult result = CreateSampleResult(paranCount: 2, crossingCount: 4);

        // Act
        string markdown = ParanMarkdownFormatter.Format(result, "John Smith");

        // Assert
        markdown.Should().Contain("## Parans (Paranatellonta)",
            "output should contain the main heading");
        markdown.Should().Contain("John Smith",
            "output should contain the chart name");
        markdown.Should().Contain("### Paran Events",
            "output should contain the paran events table heading");
        markdown.Should().Contain("| Body A | Angle A |",
            "output should contain the paran table header row");
        markdown.Should().Contain("### Angular Crossings",
            "output should contain the angular crossings table heading");
        markdown.Should().Contain("Sun",
            "output should contain body names");
        markdown.Should().Contain("Moon",
            "output should contain body names");
    }

    [Fact]
    public void Format_Should_ShowEmptyMessage_When_NoParansDetected()
    {
        // Arrange
        ParanResult result = CreateSampleResult(paranCount: 0, crossingCount: 4);

        // Act
        string markdown = ParanMarkdownFormatter.Format(result, "Test Chart");

        // Assert
        markdown.Should().Contain("No paran events detected",
            "output should show empty message when no parans found");
        markdown.Should().NotContain("### Paran Events",
            "paran events table heading should not appear when empty");
    }

    [Fact]
    public void Format_Should_ContainCrossingsTable_Always()
    {
        // Arrange
        ParanResult result = CreateSampleResult(paranCount: 0, crossingCount: 2);

        // Act
        string markdown = ParanMarkdownFormatter.Format(result, "Test");

        // Assert
        markdown.Should().Contain("### Angular Crossings",
            "crossings table should always appear");
        markdown.Should().Contain("| Body | Angle | Position | JD (UT) |",
            "crossings table should have the header row");
    }

    #endregion

    #region Position Formatting

    [Theory]
    [InlineData(0.05, "0\u00b0Ar03'")]
    [InlineData(150.05, "0\u00b0Vi03'")]
    [InlineData(90.0, "0\u00b0Cn00'")]
    [InlineData(119.999, "29\u00b0Cn59'")]
    [InlineData(359.99, "29\u00b0Pi59'")]
    [InlineData(22.383, "22\u00b0Ar22'")]
    public void FormatShortLongitude_Should_FormatCorrectly(double longitude, string expected)
    {
        // Act
        string formatted = ParanMarkdownFormatter.FormatShortLongitude(longitude);

        // Assert
        formatted.Should().Be(expected,
            $"longitude {longitude}\u00b0 should format to '{expected}'");
    }

    #endregion

    #region Metadata

    [Fact]
    public void Format_Should_ContainLocationAndOrbMetadata()
    {
        // Arrange
        ParanResult result = CreateSampleResult(paranCount: 1, crossingCount: 2);

        // Act
        string markdown = ParanMarkdownFormatter.Format(result, "Test");

        // Assert
        markdown.Should().Contain("**Location:**",
            "output should contain location metadata");
        markdown.Should().Contain("**Orb:**",
            "output should contain orb metadata");
        markdown.Should().Contain("**Bodies:**",
            "output should contain body count");
        markdown.Should().Contain("**Parans found:**",
            "output should contain paran count");
    }

    /// <summary>
    /// The lower culmination prints as "Anti-Culm.", the name the chart export's Parans table gives it, not
    /// the code's "AntiCulminating" (branch ExportText, 1.7.2; found beside finding N11).
    /// </summary>
    [Fact]
    public void Format_Should_NameTheLowerCulminationAsTheChartExportDoes()
    {
        // Arrange -- the fourth crossing of the sample is on the lower meridian
        ParanResult result = CreateSampleResult(paranCount: 1, crossingCount: 4);

        // Act
        string markdown = ParanMarkdownFormatter.Format(result, "Test");

        // Assert
        markdown.Should().Contain("| Moon | Anti-Culm. |").And.NotContain("AntiCulminating");
        markdown.Should().Contain("| Moon | Rising |").And.Contain("| Saturn | Culminating |");
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a sample <see cref="ParanResult"/> with the specified number of
    /// paran events and angular crossings for testing the formatter.
    /// </summary>
    private static ParanResult CreateSampleResult(int paranCount, int crossingCount)
    {
        List<AngularCrossing> crossings = [];
        for (int i = 0; i < crossingCount; i++)
        {
            crossings.Add(new AngularCrossing
            {
                Body = i % 2 == 0 ? CelestialBodyId.Sun : CelestialBodyId.Moon,
                BodyName = i % 2 == 0 ? "Sun" : "Moon",
                Angle = (AngleType)(i % 4),
                JulianDayUt = 2460000.5 + i * 0.01,
                Longitude = 15.0 + i * 30.0,
            });
        }

        List<ParanEvent> parans = [];
        for (int i = 0; i < paranCount; i++)
        {
            parans.Add(new ParanEvent
            {
                BodyA = CelestialBodyId.Moon,
                BodyAName = "Moon",
                AngleA = AngleType.Rising,
                JulianDayA = 2460000.5 + i * 0.001,
                LongitudeA = 120.0 + i * 10.0,
                BodyB = CelestialBodyId.Saturn,
                BodyBName = "Saturn",
                AngleB = AngleType.Culminating,
                JulianDayB = 2460000.501 + i * 0.001,
                LongitudeB = 345.0 + i * 5.0,
                OrbMinutes = 1.24 + i * 0.5,
            });
        }

        return new ParanResult
        {
            Latitude = 51.5074,
            Longitude = -0.1278,
            StartJulianDay = 2460000.5,
            EndJulianDay = 2460001.497,
            OrbMinutes = 4.0,
            Crossings = crossings.AsReadOnly(),
            Parans = parans.AsReadOnly(),
            BodyCount = 12,
            CalculatedAt = DateTime.UtcNow,
        };
    }

    #endregion
}
