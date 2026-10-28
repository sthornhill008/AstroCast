using AstroCast.Core.Enums;
using AstroCast.Core.Formatting;
using AstroCast.Core.Models;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// Tests for <see cref="PositionFormatter"/> verifying correct formatting of
/// longitudes, positions, aspects, and midpoint activations.
/// </summary>
[Trait("Category", "Unit")]
public sealed class PositionFormatterTests
{
    [Fact]
    public void FormatLongitude_Should_FormatCorrectly_When_GivenAriesTongitude()
    {
        // Arrange — 15.3964° ecliptic = 15°23'47" Aries
        double longitude = 15.3964;

        // Act
        string result = PositionFormatter.FormatLongitude(longitude);

        // Assert
        result.Should().StartWith("15\u00b023", "degrees and minutes should match");
        result.Should().Contain("Aries", "should be in Aries (0-30°)");
    }

    [Fact]
    public void FormatLongitude_Should_FormatCorrectly_When_GivenCancerLongitude()
    {
        // Arrange — 112.14° ecliptic = Cancer (90-120°), 22°08' Cancer
        double longitude = 112.14;

        // Act
        string result = PositionFormatter.FormatLongitude(longitude);

        // Assert
        result.Should().StartWith("22\u00b008", "degrees and minutes should match");
        result.Should().Contain("Cancer", "should be in Cancer (90-120°)");
    }

    [Fact]
    public void FormatPosition_Should_MatchLegacyFormat_When_GivenPlanetPosition()
    {
        // Arrange
        PlanetPosition position = new()
        {
            Body = CelestialBodyId.Sun,
            Name = "Sun",
            Longitude = 15.3964,
            Latitude = 0.0,
            Distance = 1.0,
            SpeedInLongitude = 1.017,
            SpeedInLatitude = 0.0,
            SpeedInDistance = 0.0,
        };

        // Act
        string result = PositionFormatter.FormatPosition(position);

        // Assert
        result.Should().Contain("Aries", "Sun at 15.3964° should be in Aries");
        result.Should().StartWith("15\u00b023", "degrees and minutes should match");
    }

    [Fact]
    public void FormatAspect_Should_FormatCorrectly_When_GivenAspectResult()
    {
        // Arrange
        AspectResult aspect = new()
        {
            Body1Name = "Sun",
            Body1Longitude = 15.0,
            Body2Name = "Moon",
            Body2Longitude = 105.0,
            Aspect = AspectDefinition.All[0], // Conjunction
            Orb = 2.25,
            IsApplying = true,
        };

        // Act
        string result = PositionFormatter.FormatAspect(aspect);

        // Assert
        result.Should().Contain("Sun", "should contain first body name");
        result.Should().Contain("Moon", "should contain second body name");
        result.Should().Contain("cnj", "should contain aspect abbreviation");
        result.Should().Contain("Ap", "should show applying");
        result.Should().Contain("2\u00b015", "should show orb as 2°15'");
    }

    [Fact]
    public void FormatActivation_Should_FormatCorrectly_When_GivenDirectActivation()
    {
        // Arrange
        MidpointActivation activation = new()
        {
            Midpoint = new Midpoint
            {
                Body1Name = "Sun",
                Body1Longitude = 30.0,
                Body2Name = "Moon",
                Body2Longitude = 90.0,
                Longitude = 60.0,
            },
            ActivatorName = "Mars",
            ActivatorLongitude = 60.5,
            HarmonicAngle = 0,
            Orb = 0.5,
        };

        // Act
        string result = PositionFormatter.FormatActivation(activation);

        // Assert
        result.Should().Contain("Sun/Moon", "should contain midpoint notation");
        result.Should().Contain("Mars", "should contain activator name");
        result.Should().Contain("0\u00b030", "should show orb as 0°30'");
    }

    [Fact]
    public void FormatLongitude_Should_HandleZeroDegrees_When_GivenZero()
    {
        // Arrange — 0.0° = 0°00'00" Aries
        double longitude = 0.0;

        // Act
        string result = PositionFormatter.FormatLongitude(longitude);

        // Assert
        result.Should().StartWith("00\u00b000", "zero longitude should format as 00°00'");
        result.Should().Contain("Aries", "0° ecliptic is Aries");
    }

    [Fact]
    public void FormatLongitude_Should_HandlePiscesEnd_When_GivenNear360()
    {
        // Arrange — 359.5° = 29°30' Pisces
        double longitude = 359.5;

        // Act
        string result = PositionFormatter.FormatLongitude(longitude);

        // Assert
        result.Should().StartWith("29\u00b030", "359.5° should be 29°30' in sign");
        result.Should().Contain("Pisces", "359.5° ecliptic is Pisces");
    }

    #region FormatLatitude Tests

    [Fact]
    public void FormatLatitude_PositiveValue_FormatsWithPlusSign()
    {
        // Arrange — +1.3964° latitude
        double latitude = 1.3964;

        // Act
        string result = PositionFormatter.FormatLatitude(latitude);

        // Assert
        result.Should().StartWith("+01\u00b023", "positive latitude should have + prefix");
    }

    [Fact]
    public void FormatLatitude_NegativeValue_FormatsWithMinusSign()
    {
        // Arrange — -2.5° latitude
        double latitude = -2.5;

        // Act
        string result = PositionFormatter.FormatLatitude(latitude);

        // Assert
        result.Should().StartWith("-02\u00b030", "negative latitude should have - prefix");
    }

    [Fact]
    public void FormatLatitude_Zero_FormatsAsPositiveZero()
    {
        // Act
        string result = PositionFormatter.FormatLatitude(0.0);

        // Assert
        result.Should().StartWith("+00\u00b000", "zero latitude should format as +00°00'");
    }

    #endregion

    #region FormatDeclination Tests

    [Fact]
    public void FormatDeclination_NorthDeclination_FormatsWithPlusSign()
    {
        // Arrange — +23.4367° (obliquity threshold)
        double declination = 23.4367;

        // Act
        string result = PositionFormatter.FormatDeclination(declination);

        // Assert
        result.Should().StartWith("+23\u00b026", "north declination should have + prefix");
    }

    [Fact]
    public void FormatDeclination_SouthDeclination_FormatsWithMinusSign()
    {
        // Arrange — -15.1389° declination
        double declination = -15.1389;

        // Act
        string result = PositionFormatter.FormatDeclination(declination);

        // Assert
        result.Should().StartWith("-15\u00b008", "south declination should have - prefix");
    }

    #endregion

    #region FormatRightAscension Tests

    [Fact]
    public void FormatRightAscension_ZeroDegrees_FormatsAsZeroHours()
    {
        // Act
        string result = PositionFormatter.FormatRightAscension(0.0);

        // Assert
        result.Should().Be("00h 00m 00s", "0° RA should be 00h 00m 00s");
    }

    [Fact]
    public void FormatRightAscension_180Degrees_FormatsAs12Hours()
    {
        // Arrange — 180° = 12h exactly
        double ra = 180.0;

        // Act
        string result = PositionFormatter.FormatRightAscension(ra);

        // Assert
        result.Should().Be("12h 00m 00s", "180° RA should be 12h 00m 00s");
    }

    [Fact]
    public void FormatRightAscension_ArbitraryDegrees_FormatsCorrectly()
    {
        // Arrange — 90° = 6h 00m 00s exactly
        double ra = 90.0;

        // Act
        string result = PositionFormatter.FormatRightAscension(ra);

        // Assert
        result.Should().Be("06h 00m 00s", "90° RA should be 06h 00m 00s");
    }

    #endregion

    #region IsOutOfBounds Tests

    [Fact]
    public void IsOutOfBounds_AboveObliquity_ReturnsTrue()
    {
        // Arrange — declination above obliquity threshold
        double? declination = 24.5;

        // Act
        bool result = PositionFormatter.IsOutOfBounds(declination, 23.4367);

        // Assert
        result.Should().BeTrue("24.5° exceeds the obliquity threshold of 23.4367°");
    }

    [Fact]
    public void IsOutOfBounds_BelowNegativeObliquity_ReturnsTrue()
    {
        // Arrange — declination below negative obliquity threshold
        double? declination = -25.0;

        // Act
        bool result = PositionFormatter.IsOutOfBounds(declination, 23.4367);

        // Assert
        result.Should().BeTrue("-25° exceeds the obliquity threshold in the negative direction");
    }

    [Fact]
    public void IsOutOfBounds_WithinObliquity_ReturnsFalse()
    {
        // Arrange — declination within normal range
        double? declination = 20.0;

        // Act
        bool result = PositionFormatter.IsOutOfBounds(declination, 23.4367);

        // Assert
        result.Should().BeFalse("20° is within the obliquity threshold of 23.4367°");
    }

    [Fact]
    public void IsOutOfBounds_NullDeclination_ReturnsFalse()
    {
        // Act
        bool result = PositionFormatter.IsOutOfBounds((double?)null, 23.4367);

        // Assert
        result.Should().BeFalse("null declination should not be considered out-of-bounds");
    }

    [Fact]
    public void IsOutOfBounds_ExactlyAtObliquity_ReturnsFalse()
    {
        // Arrange — exactly at threshold (not exceeding)
        double? declination = 23.4367;

        // Act
        bool result = PositionFormatter.IsOutOfBounds(declination, 23.4367);

        // Assert
        result.Should().BeFalse("exactly at threshold should not be OOB (must exceed, not equal)");
    }

    #endregion

    #region FormatOrb (T9 — unified D°MM′ orb formatter, 2026-06-10 live-run review)

    [Fact]
    public void FormatOrb_FourArcminuteOrb_RendersZeroDegreesZeroFourMinutes()
    {
        // Arrange — 0.0667° = 0°04′00.12″ (Doctor Visit Candidate 1 Mars-DSC orb)
        double orb = 0.0667;

        // Act
        string result = PositionFormatter.FormatOrb(orb);

        // Assert
        result.Should().Be("0\u00b004\u2032",
            $"0.0667 deg must render as 0°04′, got '{result}'");
    }

    [Fact]
    public void FormatOrb_TwoPointThreeOneSixSevenDegrees_RendersTwoDegreesNineteenMinutes()
    {
        // Arrange — 2.3167° = 2°19′00.12″ (Doctor Visit Candidate 2 Moon-Jupiter orb)
        double orb = 2.3167;

        // Act
        string result = PositionFormatter.FormatOrb(orb);

        // Assert
        result.Should().Be("2\u00b019\u2032",
            $"2.3167 deg must render as 2°19′, got '{result}'");
    }

    /// <summary>
    /// An orb is truncated to the minute, never rounded and never carried (branch O-D3, 1.3.4):
    /// 2.3083° (2°18′29.9″) is 2°18′, 1.9999° (1°59′59.6″) is 1°59′ and the Moon's 0.063724° to
    /// Uranus of row 61 (3′49.4″) is 0°03′. Until 1.3.4 the minutes rounded half-up, so these
    /// printed 2°19′, 2°00′ and 0°04′. The floor carries a guard of 1e-9 minute (R1a): a double
    /// a hair below a whole minute by arithmetic, 2 − 0.01 × 120 = 0.7999999999999998, which is
    /// 0.8° by construction, prints that minute, 0°48′, where the plain floor printed 0°47′.
    /// </summary>
    /// <param name="orb">The orb in decimal degrees.</param>
    /// <param name="expected">The orb as truncated D°MM′.</param>
    [Theory]
    [InlineData(2.3083, "2\u00b018\u2032")]
    [InlineData(1.9999, "1\u00b059\u2032")]
    [InlineData(0.063724, "0\u00b003\u2032")]
    [InlineData(0.7999999999999998, "0\u00b048\u2032")]
    public void FormatOrb_TruncatesToTheMinute_NeverRoundsOrCarries(double orb, string expected)
    {
        PositionFormatter.FormatOrb(orb).Should().Be(expected);
    }

    [Fact]
    public void FormatOrb_JustBelowThirtySecondsBoundary_RoundsDown()
    {
        // Arrange — 2°18′29″
        double orb = 8309.0 / 3600.0;

        // Act
        string result = PositionFormatter.FormatOrb(orb);

        // Assert
        result.Should().Be("2\u00b018\u2032",
            $"2°18′29″ must round down to 2°18′, got '{result}'");
    }

    [Fact]
    public void FormatOrb_SingleDigitMinutes_ZeroPadsMinutesToTwoDigits()
    {
        // Arrange — 1.05° = 1°03′
        double orb = 1.05;

        // Act
        string result = PositionFormatter.FormatOrb(orb);

        // Assert
        result.Should().Be("1\u00b003\u2032",
            $"minutes must be zero-padded to two digits, got '{result}'");
    }

    [Fact]
    public void FormatOrb_ZeroOrb_RendersZeroDegreesZeroMinutes()
    {
        // Act
        string result = PositionFormatter.FormatOrb(0.0);

        // Assert
        result.Should().Be("0\u00b000\u2032",
            $"a zero orb must render as 0°00′, got '{result}'");
    }

    #endregion

    #region FormatArc (branch PrimaryExport, 1.5.0)

    /// <summary>An arc of direction prints as D°MM′, the minutes truncated, as an orb does.</summary>
    /// <param name="arc">The arc in degrees.</param>
    /// <param name="expected">The text.</param>
    [Theory]
    [InlineData(16.001096, "16\u00b000\u2032")]
    [InlineData(0.999, "0\u00b059\u2032")]
    [InlineData(98.9999, "98\u00b059\u2032")]
    public void FormatArc_TruncatesToTheMinute(double arc, string expected)
    {
        PositionFormatter.FormatArc(arc).Should().Be(expected);
    }

    #endregion
}
