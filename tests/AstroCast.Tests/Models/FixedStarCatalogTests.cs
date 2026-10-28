using AstroCast.Core.Models;

using FluentAssertions;

namespace AstroCast.Tests.Models;

/// <summary>
/// Unit tests for <see cref="FixedStarCatalog"/> and <see cref="FixedStarEntry"/>.
/// Validates catalog integrity: count, uniqueness, classification flags,
/// required fields, and chart point sort ordering.
/// </summary>
[Trait("Category", "Unit")]
public sealed class FixedStarCatalogTests
{
    #region Catalog Count and Uniqueness

    [Fact]
    public void Stars_Should_Contain55UniqueEntries()
    {
        // Assert
        FixedStarCatalog.Stars.Should().HaveCount(55,
            "the catalog contains 55 unique stars after removing duplicates (#49 Wega = #38 Vega, #51 = #31)");
    }

    [Fact]
    public void Stars_Should_HaveUniqueDisplayNames()
    {
        // Arrange
        List<string> displayNames = FixedStarCatalog.Stars
            .Select(s => s.DisplayName)
            .ToList();

        // Assert
        displayNames.Should().OnlyHaveUniqueItems(
            "each star must have a unique display name");
    }

    [Fact]
    public void Stars_Should_HaveUniqueSwissEphNames()
    {
        // Arrange
        List<string> sweNames = FixedStarCatalog.Stars
            .Select(s => s.SwissEphName)
            .ToList();

        // Assert
        sweNames.Should().OnlyHaveUniqueItems(
            "each star must have a unique Swiss Ephemeris name");
    }

    #endregion

    #region Royal Stars

    [Fact]
    public void Stars_Should_ContainExactly4RoyalStars()
    {
        // Arrange
        List<FixedStarEntry> royalStars = FixedStarCatalog.Stars
            .Where(s => s.IsRoyalStar)
            .ToList();

        // Assert
        royalStars.Should().HaveCount(4,
            "there are exactly 4 Royal Stars: Aldebaran, Regulus, Antares, Fomalhaut");
    }

    [Theory]
    [InlineData("Aldebaran")]
    [InlineData("Regulus")]
    [InlineData("Antares")]
    [InlineData("Fomalhaut")]
    public void RoyalStar_Should_BeFlaggedCorrectly(string starName)
    {
        // Arrange
        FixedStarEntry? star = FixedStarCatalog.Stars
            .FirstOrDefault(s => s.DisplayName == starName);

        // Assert
        star.Should().NotBeNull($"'{starName}' must exist in the catalog");
        star!.IsRoyalStar.Should().BeTrue(
            $"'{starName}' is one of the four Royal Stars");
    }

    #endregion

    #region Behenian Stars

    [Fact]
    public void Stars_Should_ContainAtLeast15BehenianStars()
    {
        // Arrange
        List<FixedStarEntry> behenianStars = FixedStarCatalog.Stars
            .Where(s => s.IsBehenianStar)
            .ToList();

        // Assert — the traditional 15 Behenian stars
        behenianStars.Should().HaveCountGreaterThanOrEqualTo(15,
            "the catalog includes all 15 traditional Behenian stars");
    }

    [Theory]
    [InlineData("Algol")]
    [InlineData("Alcyone")]
    [InlineData("Aldebaran")]
    [InlineData("Capella")]
    [InlineData("Sirius")]
    [InlineData("Procyon")]
    [InlineData("Regulus")]
    [InlineData("Algorab")]
    [InlineData("Spica")]
    [InlineData("Arcturus")]
    [InlineData("Alphecca")]
    [InlineData("Antares")]
    [InlineData("Vega")]
    [InlineData("Altair")]
    [InlineData("Deneb Algedi")]
    public void BehenianStar_Should_BeFlaggedCorrectly(string starName)
    {
        // Arrange
        FixedStarEntry? star = FixedStarCatalog.Stars
            .FirstOrDefault(s => s.DisplayName == starName);

        // Assert
        star.Should().NotBeNull($"'{starName}' must exist in the catalog");
        star!.IsBehenianStar.Should().BeTrue(
            $"'{starName}' is one of the 15 traditional Behenian stars");
    }

    #endregion

    #region Required Fields

    [Fact]
    public void Stars_Should_AllHaveNonEmptyDisplayNames()
    {
        // Assert
        FixedStarCatalog.Stars
            .Should().AllSatisfy(s =>
                s.DisplayName.Should().NotBeNullOrWhiteSpace(
                    "every star must have a display name"));
    }

    [Fact]
    public void Stars_Should_AllHaveNonEmptySwissEphNames()
    {
        // Assert
        FixedStarCatalog.Stars
            .Should().AllSatisfy(s =>
                s.SwissEphName.Should().NotBeNullOrWhiteSpace(
                    "every star must have a Swiss Ephemeris name"));
    }

    [Fact]
    public void Stars_Should_AllHaveNonEmptyConstellations()
    {
        // Assert
        FixedStarCatalog.Stars
            .Should().AllSatisfy(s =>
                s.Constellation.Should().NotBeNullOrWhiteSpace(
                    "every star must have a constellation"));
    }

    [Fact]
    public void Stars_Should_AllHaveNonEmptyNature()
    {
        // Assert
        FixedStarCatalog.Stars
            .Should().AllSatisfy(s =>
                s.Nature.Should().NotBeNullOrWhiteSpace(
                    "every star must have a Ptolemaic nature"));
    }

    [Fact]
    public void Stars_Should_AllHaveNonEmptyKeywords()
    {
        // Assert
        FixedStarCatalog.Stars
            .Should().AllSatisfy(s =>
                s.Keywords.Should().NotBeNullOrWhiteSpace(
                    "every star must have interpretive keywords"));
    }

    [Fact]
    public void Stars_Should_AllHaveReasonableMagnitudes()
    {
        // Assert — visual magnitude range: about -1.5 (Sirius) to 5.0 (faint)
        FixedStarCatalog.Stars
            .Should().AllSatisfy(s =>
                s.Magnitude.Should().BeInRange(-2.0, 6.0,
                    $"magnitude of '{s.DisplayName}' should be in a reasonable range"));
    }

    #endregion

    #region Specific Star Name Mappings

    [Theory]
    [InlineData("Difda", "Difda")]
    [InlineData("Sharatan", "Sheratan")]
    [InlineData("Hyades", "Prima Hyadum")]
    [InlineData("South Scale", "Zuben Elgenubi")]
    [InlineData("North Scale", "Zuben Eschamali")]
    [InlineData("Etamin", "Eltanin")]
    [InlineData("Deneb Adige", "Deneb,alCyg")]
    [InlineData("North Asellus", "Asellus Borealis")]
    [InlineData("South Asellus", "Asellus Australis")]
    public void Star_Should_MapDisplayNameToSwissEphName(
        string displayName, string expectedSwissEphName)
    {
        // Arrange
        FixedStarEntry? star = FixedStarCatalog.Stars
            .FirstOrDefault(s => s.DisplayName == displayName);

        // Assert
        star.Should().NotBeNull($"'{displayName}' must exist in the catalog");
        star!.SwissEphName.Should().Be(expectedSwissEphName,
            $"'{displayName}' maps to '{expectedSwissEphName}' in sefstars.txt");
    }

    #endregion

    #region Default Orb

    [Fact]
    public void DefaultOrb_Should_Be1Degree()
    {
        // Assert
        FixedStarCatalog.DefaultOrb.Should().Be(1.0,
            "the traditional Robson standard orb for fixed stars is 1 degree 00 arc-minutes");
    }

    #endregion

    #region Chart Point Sort Order

    [Fact]
    public void ChartPointSortOrder_Should_Contain16Points()
    {
        // Assert
        FixedStarCatalog.ChartPointSortOrder.Should().HaveCount(16,
            "there are 16 chart points: 4 angles + 10 planets + True Node + Chiron");
    }

    [Fact]
    public void ChartPointSortOrder_Should_StartWithAngles()
    {
        // Assert
        FixedStarCatalog.ChartPointSortOrder[0].Should().Be("ASC");
        FixedStarCatalog.ChartPointSortOrder[1].Should().Be("MC");
        FixedStarCatalog.ChartPointSortOrder[2].Should().Be("DSC");
        FixedStarCatalog.ChartPointSortOrder[3].Should().Be("IC");
    }

    [Theory]
    [InlineData("ASC", 0)]
    [InlineData("MC", 1)]
    [InlineData("DSC", 2)]
    [InlineData("IC", 3)]
    [InlineData("Sun", 4)]
    [InlineData("Moon", 5)]
    [InlineData("Mercury", 6)]
    [InlineData("Chiron", 15)]
    public void GetChartPointSortIndex_Should_ReturnCorrectIndex(
        string chartPointName, int expectedIndex)
    {
        // Act
        int index = FixedStarCatalog.GetChartPointSortIndex(chartPointName);

        // Assert
        index.Should().Be(expectedIndex,
            $"'{chartPointName}' should have sort index {expectedIndex}");
    }

    [Fact]
    public void GetChartPointSortIndex_Should_Return99ForUnknown()
    {
        // Act
        int index = FixedStarCatalog.GetChartPointSortIndex("UnknownPoint");

        // Assert
        index.Should().Be(99,
            "unrecognized chart point names should sort last with index 99");
    }

    #endregion

    #region Royal Stars Are Also Behenian

    [Fact]
    public void RoyalStars_AlderbaranRegulusAntares_Should_AlsoBeBehenian()
    {
        // Arrange — Aldebaran, Regulus, and Antares are both Royal and Behenian
        string[] royalAndBehenian = ["Aldebaran", "Regulus", "Antares"];

        foreach (string name in royalAndBehenian)
        {
            FixedStarEntry? star = FixedStarCatalog.Stars
                .FirstOrDefault(s => s.DisplayName == name);

            // Assert
            star.Should().NotBeNull();
            star!.IsRoyalStar.Should().BeTrue($"'{name}' is a Royal Star");
            star.IsBehenianStar.Should().BeTrue($"'{name}' is also a Behenian Star");
        }
    }

    [Fact]
    public void RoyalStar_Fomalhaut_Should_NotBeBehenian()
    {
        // Arrange — Fomalhaut is Royal but not traditionally Behenian
        FixedStarEntry? fomalhaut = FixedStarCatalog.Stars
            .FirstOrDefault(s => s.DisplayName == "Fomalhaut");

        // Assert
        fomalhaut.Should().NotBeNull();
        fomalhaut!.IsRoyalStar.Should().BeTrue("Fomalhaut is a Royal Star");
        fomalhaut.IsBehenianStar.Should().BeFalse(
            "Fomalhaut is not traditionally counted among the 15 Behenian stars");
    }

    #endregion

    #region FixedStarConjunction Model

    [Fact]
    public void FixedStarConjunction_Should_ExposeAllRequiredProperties()
    {
        // Arrange
        FixedStarConjunction conjunction = new()
        {
            StarName = "Regulus",
            Constellation = "Leo",
            Magnitude = 1.36,
            StarLongitude = 149.83,
            ChartPointName = "MC",
            ChartPointLongitude = 150.05,
            Orb = 0.22,
            Nature = "Mars-Jupiter",
            Keywords = "Nobility, success if no vengeance",
            IsRoyalStar = true,
            IsBehenianStar = true,
            ChartPointSortOrder = 1,
        };

        // Assert
        conjunction.StarName.Should().Be("Regulus");
        conjunction.Constellation.Should().Be("Leo");
        conjunction.Magnitude.Should().Be(1.36);
        conjunction.StarLongitude.Should().Be(149.83);
        conjunction.ChartPointName.Should().Be("MC");
        conjunction.ChartPointLongitude.Should().Be(150.05);
        conjunction.Orb.Should().BeApproximately(0.22, 0.001);
        conjunction.Nature.Should().Be("Mars-Jupiter");
        conjunction.Keywords.Should().Be("Nobility, success if no vengeance");
        conjunction.IsRoyalStar.Should().BeTrue();
        conjunction.IsBehenianStar.Should().BeTrue();
        conjunction.ChartPointSortOrder.Should().Be(1);
    }

    #endregion
}
