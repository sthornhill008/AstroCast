using AstroCast.Core.Enums;
using AstroCast.Core.Models.PrimaryDirections;

using FluentAssertions;

namespace AstroCast.Tests.Formatting;

/// <summary>
/// The display names of the three Core enums the screen shows (1.4.0, E3-6), each held in one place,
/// <c>DisplayNames</c> in <c>Core/Enums</c>.
/// </summary>
/// <remarks>
/// The house-system and relationship names were private tables in <c>ChartEngine</c> and
/// <c>SynastryMarkdownFormatter</c>, which the exports print through; they moved unchanged, so the tables
/// below are the old ones and no export moves. The election-type names are new and used on screen only:
/// the electional export keeps the enum name, "BusinessLaunch" (scoping decision 5).
/// </remarks>
[Trait("Category", "Unit")]
public sealed class DisplayNameTests
{
    /// <summary>Every house system's name is the one <c>ChartEngine</c>'s private table gave the exports.</summary>
    [Fact]
    public void HouseSystem_DisplayName_IsTheExportsName()
    {
        Dictionary<HouseSystem, string> expected = new()
        {
            [HouseSystem.Placidus] = "Placidus",
            [HouseSystem.Koch] = "Koch",
            [HouseSystem.Equal] = "Equal (from ASC)",
            [HouseSystem.WholeSign] = "Whole Sign",
            [HouseSystem.Regiomontanus] = "Regiomontanus",
            [HouseSystem.Campanus] = "Campanus",
            [HouseSystem.Alcabitius] = "Alcabitius",
            [HouseSystem.Porphyry] = "Porphyry",
            [HouseSystem.Morinus] = "Morinus",
        };

        Enum.GetValues<HouseSystem>().Should().BeEquivalentTo(expected.Keys);
        foreach ((HouseSystem system, string name) in expected)
        {
            system.DisplayName.Should().Be(name);
        }
    }

    /// <summary>Every relationship type's name is the one the synastry export's private table gave it.</summary>
    [Fact]
    public void RelationshipType_DisplayName_IsTheExportsName()
    {
        Dictionary<RelationshipType, string> expected = new()
        {
            [RelationshipType.Unspecified] = "Unspecified",
            [RelationshipType.Romantic] = "Romantic Partnership",
            [RelationshipType.Marriage] = "Marriage",
            [RelationshipType.ParentChild] = "Parent & Child",
            [RelationshipType.Siblings] = "Siblings",
            [RelationshipType.Friendship] = "Friendship",
            [RelationshipType.Business] = "Business Partnership",
            [RelationshipType.Colleagues] = "Colleagues",
            [RelationshipType.TeacherStudent] = "Teacher & Student",
            [RelationshipType.TherapistClient] = "Therapist & Client",
            [RelationshipType.Adversarial] = "Adversarial",
            [RelationshipType.Other] = "Other",
        };

        Enum.GetValues<RelationshipType>().Should().BeEquivalentTo(expected.Keys);
        foreach ((RelationshipType type, string name) in expected)
        {
            type.DisplayName.Should().Be(name);
        }
    }

    /// <summary>Every election type has a name in words, as the Type box's tooltip writes "Business Launch".</summary>
    [Fact]
    public void ElectionType_DisplayName_NamesEveryValue()
    {
        Dictionary<ElectionType, string> expected = new()
        {
            [ElectionType.BusinessLaunch] = "Business Launch",
            [ElectionType.Contract] = "Contract",
            [ElectionType.Wedding] = "Wedding",
            [ElectionType.Surgery] = "Surgery",
            [ElectionType.RealEstateListing] = "Real Estate Listing",
            [ElectionType.RealEstateClosing] = "Real Estate Closing",
            [ElectionType.CreativeLaunch] = "Creative Launch",
            [ElectionType.Travel] = "Travel",
            [ElectionType.Other] = "Other",
        };

        Enum.GetValues<ElectionType>().Should().BeEquivalentTo(expected.Keys);
        foreach ((ElectionType type, string name) in expected)
        {
            type.DisplayName.Should().Be(name);
        }
    }

    /// <summary>Every primary-direction key has a name in words with its rate (branch Primary, 1.5.0).</summary>
    /// <param name="key">The key.</param>
    /// <param name="name">Its name.</param>
    [Theory]
    [InlineData(PrimaryDirectionKey.Naibod, "Naibod (0\u00b059\u203208\u2033 a year)")]
    [InlineData(PrimaryDirectionKey.Ptolemy, "Ptolemy (1\u00b0 a year)")]
    [InlineData(PrimaryDirectionKey.SolarArcRightAscension, "True solar arc in right ascension")]
    public void PrimaryDirectionKey_DisplayName_NamesTheKeyAndItsRate(PrimaryDirectionKey key, string name)
    {
        key.DisplayName.Should().Be(name);
    }
}
