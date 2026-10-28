using AstroCast.Core.Configuration;
using AstroCast.Core.Enums;
using AstroCast.Core.Models;
using AstroCast.Tests.Fixtures;

using FluentAssertions;

namespace AstroCast.Tests.Models;

/// <summary>
/// Unit tests for paran-related models: <see cref="AngleType"/>,
/// <see cref="ParanEvent"/>, <see cref="ParanResult"/>, and
/// <see cref="ParanOptions"/>.
/// </summary>
[Trait("Category", "Unit")]
public sealed class ParanModelTests
{
    #region AngleType Enum

    [Fact]
    public void AngleType_Should_HaveExactlyFourValues()
    {
        // Assert
        Enum.GetValues<AngleType>().Should().HaveCount(4,
            "there are exactly four local-space angles: Rising, Culminating, Setting, Anti-Culminating");
    }

    [Theory]
    [InlineData(AngleType.Rising, 0)]
    [InlineData(AngleType.Culminating, 1)]
    [InlineData(AngleType.Setting, 2)]
    [InlineData(AngleType.AntiCulminating, 3)]
    public void AngleType_Should_HaveExpectedIntegerValues(AngleType angle, int expected)
    {
        // Assert
        ((int)angle).Should().Be(expected,
            $"{angle} should map to integer value {expected}");
    }

    #endregion

    #region ParanEvent Computed Properties

    [Fact]
    public void MidpointJulianDay_Should_ReturnAverageOfTwoCrossingTimes()
    {
        // Arrange
        ParanEvent paran = CreateParanEvent(julianDayA: 2460000.5, julianDayB: 2460000.6);

        // Act
        double midpoint = paran.MidpointJulianDay;

        // Assert
        midpoint.Should().BeApproximately(2460000.55, 1e-10,
            "midpoint should be the average of the two Julian Day values");
    }

    [Fact]
    public void MidpointJulianDay_Should_ReturnExactValue_When_CrossingsAreSimultaneous()
    {
        // Arrange
        ParanEvent paran = CreateParanEvent(julianDayA: 2460000.5, julianDayB: 2460000.5);

        // Act
        double midpoint = paran.MidpointJulianDay;

        // Assert
        midpoint.Should().Be(2460000.5,
            "when both crossings occur at the same time, midpoint equals that time");
    }

    [Fact]
    public void Description_Should_FormatCorrectly()
    {
        // Arrange
        ParanEvent paran = new()
        {
            BodyA = CelestialBodyId.Sun,
            BodyAName = "Sun",
            AngleA = AngleType.Rising,
            JulianDayA = 2460000.5,
            LongitudeA = 15.0,
            BodyB = CelestialBodyId.Moon,
            BodyBName = "Moon",
            AngleB = AngleType.Culminating,
            JulianDayB = 2460000.502,
            LongitudeB = 120.0,
            OrbMinutes = 2.3,
        };

        // Act
        string description = paran.Description;

        // Assert
        description.Should().Be("Sun Rising \u2014 Moon Culminating (orb: 2.3 min)",
            "description should follow the pattern 'BodyA AngleA \u2014 BodyB AngleB (orb: N.N min)'");
    }

    #endregion

    #region ParanOptions Defaults

    [Fact]
    public void Default_Should_HaveTwelveBodies()
    {
        // Act
        ParanOptions options = ParanOptions.Default;

        // Assert
        options.Bodies.Should().HaveCount(12,
            "default paran calculation includes all 12 standard bodies (Sun through Chiron)");
    }

    [Fact]
    public void Default_Should_HaveFourMinuteOrb()
    {
        // Act
        ParanOptions options = ParanOptions.Default;

        // Assert
        options.OrbMinutes.Should().Be(4.0,
            "default Brady orb is 4.0 minutes (~1\u00b0 of right ascension)");
    }

    [Fact]
    public void Default_Should_HaveEmptyFixedStars()
    {
        // Act
        ParanOptions options = ParanOptions.Default;

        // Assert
        options.FixedStars.Should().BeEmpty(
            "fixed star parans are opt-in via configuration");
    }

    [Fact]
    public void Default_Should_ExcludeSameAngle()
    {
        // Act
        ParanOptions options = ParanOptions.Default;

        // Assert
        options.ExcludeSameAngle.Should().BeTrue(
            "same-angle parans are excluded by default");
    }

    #endregion

    #region ParanResult Construction

    [Fact]
    public void ParanResult_Should_ConstructWithEmptyCollections()
    {
        // Act
        ParanResult result = new()
        {
            Latitude = 51.5074,
            Longitude = -0.1278,
            StartJulianDay = 2460000.5,
            EndJulianDay = 2460001.497,
            OrbMinutes = 4.0,
            Crossings = [],
            Parans = [],
            BodyCount = 12,
            CalculatedAt = DateTime.UtcNow,
        };

        // Assert
        result.Crossings.Should().BeEmpty("no crossings were provided");
        result.Parans.Should().BeEmpty("no parans were provided");
        result.BodyCount.Should().Be(12);
        result.Latitude.Should().BeApproximately(51.5074, 1e-4);
        result.Longitude.Should().BeApproximately(-0.1278, 1e-4);
    }

    #endregion

    #region AngularCrossing Construction

    [Fact]
    public void AngularCrossing_Should_StoreAllProperties()
    {
        // Arrange & Act
        AngularCrossing crossing = new()
        {
            Body = CelestialBodyId.Mars,
            BodyName = "Mars",
            Angle = AngleType.Setting,
            JulianDayUt = 2460000.75,
            Longitude = 245.5,
        };

        // Assert
        crossing.Body.Should().Be(CelestialBodyId.Mars);
        crossing.BodyName.Should().Be("Mars");
        crossing.Angle.Should().Be(AngleType.Setting);
        crossing.JulianDayUt.Should().Be(2460000.75);
        crossing.Longitude.Should().Be(245.5);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Creates a <see cref="ParanEvent"/> with the given Julian Day values
    /// for testing computed properties.
    /// </summary>
    private static ParanEvent CreateParanEvent(
        double julianDayA = 2460000.5, double julianDayB = 2460000.6)
    {
        return new ParanEvent
        {
            BodyA = CelestialBodyId.Sun,
            BodyAName = "Sun",
            AngleA = AngleType.Rising,
            JulianDayA = julianDayA,
            LongitudeA = 15.0,
            BodyB = CelestialBodyId.Moon,
            BodyBName = "Moon",
            AngleB = AngleType.Culminating,
            JulianDayB = julianDayB,
            LongitudeB = 120.0,
            OrbMinutes = Math.Abs(julianDayB - julianDayA) * 1440.0,
        };
    }

    #endregion

    #region Culture independence

    // The paran description is built with the invariant culture, so its en-US
    // expectation holds whatever the machine's regional settings (branch O-1).

    /// <summary>
    /// <see cref="Description_Should_FormatCorrectly"/> under each of <see cref="CultureScope.OtherCultures"/>,
    /// against the same en-US expectation.
    /// </summary>
    /// <param name="culture">The culture the test runs under.</param>
    [Theory]
    [MemberData(nameof(CultureScope.OtherCultures), MemberType = typeof(CultureScope))]
    public void Description_Should_FormatCorrectly_UnderCulture(string culture)
    {
        using CultureScope scope = new(culture);

        Description_Should_FormatCorrectly();
    }

    #endregion
}
